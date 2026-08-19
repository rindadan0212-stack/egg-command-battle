using System.Collections.Generic;
using System.Text;
using EggCommand.Core;

namespace EggCommand.Tests;

/// <summary>TS の実出力と C# の出力を突き合わせる。⚠️ 落ちたら移植を直す。golden は直さない。</summary>
public class RngGoldenTests
{
    [Fact]
    public void 系統名のハッシュが一致する()
    {
        var golden = Golden.Load("rng");
        foreach (var entry in golden.GetProperty("hashString").EnumerateArray())
        {
            string text = entry.GetProperty("text").GetString()!;
            uint expected = entry.GetProperty("hash").GetUInt32();
            Assert.Equal(expected, Rng.HashString(text));
        }
    }

    /// <summary>⚠️ 呼ぶ順が golden と1つでも違うと以降が全部ずれる。
    /// scripts/goldens.mjs の並びと同じ順で消費すること。</summary>
    [Fact]
    public void 乱数の系列が1ビットも違わない()
    {
        var golden = Golden.Load("rng");
        foreach (var entry in golden.GetProperty("streams").EnumerateArray())
        {
            long seed = entry.GetProperty("seed").GetInt64();
            string stream = entry.GetProperty("stream").GetString()!;
            var rng = stream.Length == 0 ? new Rng(seed) : new Rng(seed).Stream(stream);
            string where = $"seed={seed} stream='{stream}'";

            Assert.Equal(entry.GetProperty("seedOfRng").GetUInt32(), rng.Seed);

            foreach (var v in entry.GetProperty("u32").EnumerateArray())
                Assert.Equal(v.GetUInt32(), rng.U32Value());

            foreach (var v in entry.GetProperty("float").EnumerateArray())
                Assert.Equal(v.GetDouble(), rng.Float());

            foreach (var v in entry.GetProperty("int0to100").EnumerateArray())
                Assert.Equal(v.GetInt32(), rng.Int(0, 100));

            foreach (var v in entry.GetProperty("intNeg").EnumerateArray())
                Assert.Equal(v.GetInt32(), rng.Int(-5, 5));

            foreach (var v in entry.GetProperty("chance025").EnumerateArray())
                Assert.Equal(v.GetBoolean(), rng.Chance(0.025));

            var letters = new[] { "a", "b", "c", "d" };
            foreach (var v in entry.GetProperty("pick").EnumerateArray())
                Assert.Equal(v.GetString(), rng.Pick(letters));

            var toShuffle = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };
            rng.Shuffle(toShuffle);
            Assert.Equal(Golden.Ints(entry.GetProperty("shuffle")), toShuffle);

            var sampled = rng.Sample(new[] { 10, 20, 30, 40 }, 2);
            Assert.Equal(Golden.Ints(entry.GetProperty("sample2")), sampled);
        }
    }
}

public class StatsGoldenTests
{
    /// <summary>⚠️ **ステを4本から6本に増やした（2026-08-18）。**
    ///
    /// 弱化命中・抵抗を足して、弱化の通る率を速度から切り離した。
    /// あわせて合計上限を 1ステ上限 ×2 → **×3** にした（6本のうち3本まで伸ばせる）。
    ///
    /// ⚠️ **移植元（TS）は4本 ×2 のまま。**ここは意図した差分なので、
    /// ⭐ **移植元の4本ぶんは1つも動いていないこと**を確かめる形に変えてある。
    /// ⚠️ ゴールデンは作り直さない ── 作り直すと「移植元と一致している」証明が消える。</summary>
    private const int PortedStatCount = 4;

    [Fact]
    public void 上限の定数が一致する()
    {
        var golden = Golden.Load("stats");
        // ⭐ 1ステの上限は動かしていない
        Assert.Equal(golden.GetProperty("wildStatMax").GetInt32(), Stats.WildStatMax);
        Assert.Equal(golden.GetProperty("mutationCapSteps").GetInt32(), Stats.MutationCapSteps);

        // ⚠️ 合計上限だけ意図して変えた（×2 → ×3）
        Assert.Equal(Stats.WildStatMax * 2, golden.GetProperty("wildTotalMax").GetInt32());
        Assert.Equal(Stats.WildStatMax * 3, Stats.WildTotalMax);

        foreach (var entry in golden.GetProperty("maxFor").EnumerateArray())
        {
            int mutation = entry.GetProperty("mutation").GetInt32();
            // 1ステ上限は移植元のまま
            Assert.Equal(entry.GetProperty("statMax").GetInt32(), Stats.WildStatMaxFor(mutation));
            // ⭐ 合計は常に「1ステ上限 × 3」であること
            Assert.Equal(Stats.WildStatMaxFor(mutation) * 3, Stats.WildTotalMaxFor(mutation));
        }
    }

    /// <summary>⭐ **移植元の4本は、並びも位置も1つも動いていない。**
    /// ⚠️ 削りの順（同値なら先に来たものから削る）がここに依存している。</summary>
    [Fact]
    public void ステの並びが一致する()
    {
        var golden = Golden.Load("stats");
        var expected = Golden.Strings(golden.GetProperty("statKeys"));
        Assert.Equal(PortedStatCount, expected.Count);
        Assert.Equal(PortedStatCount + 2, Stats.Keys.Length);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(Golden.StatKey(expected[i]), Stats.Keys[i]);
        }
        // ⚠️ 足したぶんは必ず後ろ（前に入れると削りの順がずれる）
        Assert.Equal(StatKey.Acc, Stats.Keys[4]);
        Assert.Equal(StatKey.Res, Stats.Keys[5]);
    }

    [Fact]
    public void 合計の数え方が一致する()
    {
        var golden = Golden.Load("stats");
        foreach (var entry in golden.GetProperty("totalOf").EnumerateArray())
        {
            var block = Golden.Block(entry.GetProperty("block"));
            Assert.Equal(entry.GetProperty("total").GetInt32(), Stats.TotalOf(block));
        }
    }

    /// <summary>⭐ 「何かが特化していれば何かが伸びない」の本体。
    /// 同値のステが複数あるときの削り順まで一致していないと、育成の結果が変わる。
    ///
    /// ⚠️ 合計上限の**倍率**は ×2 → ×3 に変えた（2026-08-18）。
    /// ⭐ 削り方そのものは1行も変えていないので、**移植元の倍率を渡して**丸ごと照合する。
    /// ⚠️ ゴールデンは作り直さない。倍率を変えたことは下の「倍率だけが違う」で別に固定する。</summary>
    [Fact]
    public void 合計上限の削り方が一致する()
    {
        var golden = Golden.Load("stats");
        foreach (var entry in golden.GetProperty("applyTotalCap").EnumerateArray())
        {
            var wild = Golden.Block(entry.GetProperty("wild"));
            int mutation = entry.GetProperty("mutation").GetInt32();
            var expected = Golden.Block(entry.GetProperty("out"));

            int statMax = Stats.WildStatMaxFor(mutation);
            var actual = Stats.CapTo(wild, statMax, statMax * PortedTotalRatio);
            Assert.Equal(expected, actual);
            Assert.Equal(entry.GetProperty("total").GetInt32(), Stats.TotalOf(actual));
        }
    }

    /// <summary>⚠️ 移植元の「合計上限 = 1ステ上限 × 2」。⭐ いまは ×3。
    /// 得意を2つまで → 3つまで、に広げたのがこの数字1つ。</summary>
    private const int PortedTotalRatio = 2;

    /// <summary>⭐ 変えたのは倍率だけ ── どの変異段階でも比が保たれていることを見る。</summary>
    [Fact]
    public void 合計上限は倍率だけが違う()
    {
        var golden = Golden.Load("stats");
        foreach (var entry in golden.GetProperty("maxFor").EnumerateArray())
        {
            int mutation = entry.GetProperty("mutation").GetInt32();
            int statMax = Stats.WildStatMaxFor(mutation);
            Assert.Equal(entry.GetProperty("totalMax").GetInt32(), statMax * PortedTotalRatio);
            Assert.Equal(statMax * 3, Stats.WildTotalMaxFor(mutation));
        }

        // ⭐ 削りの結果も「倍率を戻せば移植元に戻る」ことを1件で押さえる
        var wide = new StatBlock(30, 30, 30, 30);
        Assert.Equal(Stats.WildStatMax * PortedTotalRatio,
            Stats.TotalOf(Stats.CapTo(wide, Stats.WildStatMax, Stats.WildStatMax * PortedTotalRatio)));
        Assert.Equal(Stats.WildTotalMax, Stats.TotalOf(Stats.ApplyTotalCap(wide)));
    }

    /// <summary>⚠️ **野生レベルに <see cref="Stats.Scale"/> が掛かるようになった**
    /// （2026-08-19・作者の指示で桁を上げた）。
    ///
    /// ⭐ 足し算そのものは1行も変えていないので、**移植元の入力を倍率で戻して**丸ごと照合する。
    /// 野生レベルを Scale で割った値を渡せば、移植元と同じ答えが出る。
    /// ⚠️ ゴールデンは作り直さない ── 作り直すと「移植元と一致している」証明が消える。</summary>
    [Fact]
    public void 実値の求め方が一致する()
    {
        var golden = Golden.Load("stats");
        foreach (var entry in golden.GetProperty("actualStats").EnumerateArray())
        {
            var wild = Golden.Block(entry.GetProperty("wild"));
            var actual = Stats.ActualStats(
                Golden.Block(entry.GetProperty("base")),
                wild,
                Golden.Block(entry.GetProperty("trained")));
            // ⭐ 増えたのは「野生レベル × (Scale − 1)」ちょうど
            var want = Golden.Block(entry.GetProperty("out"));
            foreach (var key in Stats.Keys)
                Assert.Equal(want[key] + wild[key] * (Stats.Scale - 1), actual[key]);
        }
    }
}

public class SkillsGoldenTests
{
    /// <summary>意図して移植元から「弱化」の扱いを変えた技。
    /// ⭐ **ここに書いたものだけが許される。**書いていない技が変わったら落ちる。
    ///
    /// ⚠️ 移植元では CT を動かす効果が弱化に数えられておらず、
    /// 免疫で防げず・速度差でも動かず・狙い澄ましも効かない**唯一の弱化**になっていた。
    /// 「免疫はすべての弱化を防ぐ」と決めたので、CT延長を弱化に加えた（2026-08-17）。
    ///
    /// ⚠️ **挑発（taunt）を作り替えた（2026-08-18）。**
    /// 移植元では「味方に付けて、味方への単体攻撃を引き受ける」＝強化だった。
    /// ⭐ 相手に付けて「掛けた本人しか狙えなくする」＝**弱化**に変えた。
    /// 引き受け役は盾・かばうと役割が重なっていて、狙い先を縛るほうが手として太い。</summary>
    private static readonly HashSet<string> Reclassified = new HashSet<string> { "ct-long", "taunt" };

    /// <summary>意図して CT を下げた技（2026-08-19・作者の指示）。
    /// ⭐ **ここに書いたものだけが許される。**書いていない技の CT が変わったら落ちる。
    ///
    /// ⚠️ 1体が動けるのは1戦闘でおよそ **5.6手**（`sim pace`）。CT6・7 の技は
    /// **1戦闘に1回しか撃てず**、全手番の **68.8% が枠1（種族の通常攻撃・CT0）**になっていた。
    /// ⭐ 上限を **5** に下げた。
    /// ⚠️ **盤面をひっくり返しうる4件（蘇生・蘇生・大・全体強攻撃・全体連撃）は 7 のまま。**
    /// 1回きりであることが持ち味なので、短くすると別物になる。
    ///
    /// ⚠️ ゴールデンは作り直さない ── 作り直すと「移植元と一致している」証明が消える。</summary>
    private static readonly HashSet<string> CtLowered = new HashSet<string>
    {
        "attack-heavy", "stun", "guts", "attack-thrice", "attack-def-twice", "venom-heavy",
        "heal-big", "slow-all", "heal-miracle", "shield-wall", "guts-deep", "immune-long",
        "stun-heavy", "ct-lock", "buff-steal", "sleep",
    };

    /// <summary>下げたあとの CT。⭐ 全部これ1つ（上限をそろえたので値も1つ）。</summary>
    private const int CtCap = 5;

    /// <summary>意図して移植元から**狙い先**を変えた技（2026-08-18）。
    ///
    /// ⚠️ 移植元では強化が全部「自分」、回復が全部「一番弱った味方（自動）」だった。
    /// ⭐ **味方1体を選んで掛ける**形にしたので、狙い先と説明文が変わる。
    /// 理由: 強化13技すべてが Self だったため、プレイヤーが決めているのが
    /// 「いま動く1体が3枠のどれを押すか」だけになっていた ── 「誰に配るか」の軸が無かった。
    ///
    /// ⚠️ **挑発は不具合の修正でもある。**効果を「相手に付ける弱化」に作り替えたのに
    /// 技の狙い先が Self のままで、縛りが一度も発動していなかった。
    ///
    /// ⚠️ **ここに書いたものだけが許される。**⭐ CT・名前・効果の中身は全件そのまま見続ける。</summary>
    private static readonly HashSet<string> Retargeted = new HashSet<string>
    {
        "atk-up", "def-up", "spd-up",       // 自分 → 味方1体
        "regen", "heal-ratio", "shield",    // 一番弱った味方 → 味方1体
        "guts", "immune",                   // 自分 → 味方1体
        "taunt",                            // 自分 → 敵1体（不具合の修正）
    };


    [Fact]
    public void 威力と割合の表が一致する()
    {
        var golden = Golden.Load("skills");
        // ⚠️ **威力の意味そのものを変えた**（2026-08-19・作者の指示）。
        //    移植元は「絶対値」（12/20/30/42）、いまは「攻撃力の何倍か」（×1.2/1.5/2.0/3.0）。
        //    ⭐ 単位が違うので数の照合はできない。**段位の順が崩れていないこと**だけを見る。
        // ⚠️ ゴールデンは作り直さない ── 移植元の値はここに残り続ける。
        var power = golden.GetProperty("damagePower");
        Assert.True(power.GetProperty("小").GetInt32() < power.GetProperty("中").GetInt32());
        Assert.True(power.GetProperty("中").GetInt32() < power.GetProperty("大").GetInt32());
        Assert.True(power.GetProperty("大").GetInt32() < power.GetProperty("特大").GetInt32());
        Assert.True(Skills.DamagePowerOf(PowerTier.Small) < Skills.DamagePowerOf(PowerTier.Medium));
        Assert.True(Skills.DamagePowerOf(PowerTier.Medium) < Skills.DamagePowerOf(PowerTier.Large));
        Assert.True(Skills.DamagePowerOf(PowerTier.Large) < Skills.DamagePowerOf(PowerTier.Huge));
        // ⭐ 等倍（PowerUnit）より下の段位は作らない ── 「攻撃するより弱い技」は要らない
        Assert.True(Skills.DamagePowerOf(PowerTier.Small) >= Skills.PowerUnit);
        Assert.Equal(golden.GetProperty("buffPercent").GetInt32(), Skills.BuffPercent);
        Assert.Equal(golden.GetProperty("tickPercent").GetInt32(), Skills.TickPercent);
    }

    /// <summary>⭐ **id で引く。並び順も件数も見ない。**
    ///
    /// ⚠️ ここを「golden の件数 == 実装の件数」で書いていたため、
    /// 技を1つ足すだけで落ちる状態だった。落ちれば golden を作り直したくなるが、
    /// これは TS 実装との一致の記録で、TS 側はもう触っているので**二度と作れない**。
    ///
    /// 今の約束: **golden にあるものは実装にもあり、値が1つも違わない。**
    /// 足すのは自由。消す・変えるのは落ちる。</summary>
    [Fact]
    public void 移植した技が1つも変わっていない()
    {
        var golden = Golden.Load("skills");
        var list = golden.GetProperty("list");

        foreach (var entry in list.EnumerateArray())
        {
            string id = entry.GetProperty("id").GetString()!;
            var skill = Skills.ById(id);

            Assert.Equal(id, skill.Id);
            Assert.Equal(entry.GetProperty("name").GetString(), skill.Name);
            if (CtLowered.Contains(id))
            {
                // ⭐ 下げた先は全部 CtCap。⚠️ 移植元が既に CtCap 以下だったなら
                //    「下げた」ことになっていないので、表から外すべき（ここで落ちる）
                Assert.True(entry.GetProperty("ct").GetInt32() > CtCap,
                    $"{id}: 移植元の CT は {entry.GetProperty("ct").GetInt32()} で、下げる必要が無い");
                Assert.Equal(CtCap, skill.Ct);
            }
            else
            {
                Assert.Equal(entry.GetProperty("ct").GetInt32(), skill.Ct);
            }
            if (Retargeted.Contains(id))
            {
                // ⚠️ 狙い先を変えたので説明文も変わる。⭐ 移植元と違うことだけ確かめる
                //    （同じなら直し忘れ）
                Assert.NotEqual(Golden.Target(entry.GetProperty("target").GetString()!), skill.Target);
            }
            else
            {
                Assert.Equal(entry.GetProperty("gist").GetString(), skill.Gist);
                Assert.Equal(Golden.Target(entry.GetProperty("target").GetString()!), skill.Target);
            }

            // ⭐ 枠1 の CT は常に 0。CT は技ではなく枠の性質
            Assert.Equal(entry.GetProperty("ctSlot0").GetInt32(), Skills.EffectiveCt(0, skill));
            if (CtLowered.Contains(id))
            {
                // ⭐ 枠2・3 は技の CT そのもの（下げた先）
                Assert.Equal(CtCap, Skills.EffectiveCt(1, skill));
                Assert.Equal(CtCap, Skills.EffectiveCt(2, skill));
            }
            else
            {
                Assert.Equal(entry.GetProperty("ctSlot1").GetInt32(), Skills.EffectiveCt(1, skill));
                Assert.Equal(entry.GetProperty("ctSlot2").GetInt32(), Skills.EffectiveCt(2, skill));
            }

            // 同じ id を引けること（表に無いものは投げる側の確認は別テスト）
            Assert.Same(skill, Skills.ById(id));

            var effects = entry.GetProperty("effects");
            Assert.Equal(effects.GetArrayLength(), skill.Effects.Count);

            int e = 0;
            foreach (var effectJson in effects.EnumerateArray())
            {
                var effect = skill.Effects[e];
                var kind = Golden.EffectKind(effectJson.GetProperty("kind").GetString()!);
                Assert.Equal(kind, effect.Kind);

                switch (kind)
                {
                    case EffectKind.Damage:
                        Assert.Equal(Golden.PowerTier(effectJson.GetProperty("power").GetString()!), effect.Power);
                        Assert.Equal(Golden.DamageScale(effectJson.GetProperty("scale").GetString()!), effect.Scale);
                        break;
                    case EffectKind.Buff:
                        Assert.Equal(Golden.StatKey(effectJson.GetProperty("stat").GetString()!), effect.Stat);
                        Assert.Equal(effectJson.GetProperty("sign").GetInt32(), effect.Sign);
                        Assert.Equal(effectJson.GetProperty("turns").GetInt32(), effect.Turns);
                        break;
                    case EffectKind.Poison:
                    case EffectKind.Regen:
                        Assert.Equal(effectJson.GetProperty("stacks").GetInt32(), effect.Stacks);
                        Assert.Equal(effectJson.GetProperty("turns").GetInt32(), effect.Turns);
                        break;
                    case EffectKind.HealRatio:
                        Assert.Equal(effectJson.GetProperty("percent").GetInt32(), effect.Percent);
                        break;
                    case EffectKind.Shield:
                        Assert.Equal(effectJson.GetProperty("count").GetInt32(), effect.Count);
                        break;
                    case EffectKind.Ct:
                        Assert.Equal(effectJson.GetProperty("delta").GetInt32(), effect.Delta);
                        break;
                    case EffectKind.Taunt:
                        Assert.Equal(effectJson.GetProperty("hits").GetInt32(), effect.Hits);
                        break;
                    case EffectKind.Stun:
                    case EffectKind.Guts:
                    case EffectKind.Immune:
                        Assert.Equal(effectJson.GetProperty("turns").GetInt32(), effect.Turns);
                        break;
                }

                // 免疫が防ぐ対象か
                bool harmful = entry.GetProperty("harmful")[e].GetBoolean();
                if (Reclassified.Contains(id))
                {
                    // ⭐ 意図して移植元と変えた技。⚠️ **下の表に書いたものだけが許される**
                    Assert.False(harmful, $"{id}: 移植元では既に弱化扱いだった（表から外す）");
                    Assert.True(Skills.IsHarmful(effect), $"{id}: 弱化扱いになっていない");
                }
                else
                {
                    Assert.Equal(harmful, Skills.IsHarmful(effect));
                }
                e++;
            }
        }
    }

    /// <summary>⚠️ **卵ガチャを枠ごとの型プールに作り替えた（2026-08-18）。**
    ///
    /// 移植元は「種族に1つのプールから枠2・3 を2つ引く」だった。
    /// ⭐ いまは **枠2 と枠3 で別の型**（アタック / サポート / デバフ / ヒール）から1つずつ引く。
    /// 理由: 狙った組み合わせが 2.8〜4.8% でしか出ず、
    /// 「この巣からは何が来るか」も読めなかった。
    ///
    /// ⚠️ **ゴールデンは作り直さない。**移植元の中身と比べられなくなったので、
    /// ⭐ ここで見続けるのは**規則のほう**にする:
    ///   ・枠1 と同じ技はプールから外れている
    ///   ・種族ごとにプールが違う（どこで奪っても同じ技、にならない）
    ///   ・移植元のプールにあった技は、いまもどこかの種族から手に入る
    /// ⚠️ 最後の1つが「技が黙って入手不能になる」を止める（プールを作り替えた日の本当の危険はそこ）。</summary>
    [Fact]
    public void 移植した技はいまも手に入る()
    {
        var golden = Golden.Load("skills");
        var anywhere = new HashSet<string>();
        foreach (var species in SpeciesTable.All)
        {
            anywhere.Add(species.Skill1);
            foreach (var id in Skills.GachaPoolOf(species.Id, species.Skill1)) anywhere.Add(id);
        }

        var shapes = new HashSet<string>();
        foreach (var entry in golden.GetProperty("gachaPools").EnumerateArray())
        {
            string species = entry.GetProperty("species").GetString()!;
            string skill1 = entry.GetProperty("skill1").GetString()!;
            var pool = Skills.GachaPoolOf(species, skill1);

            Assert.DoesNotContain(skill1, pool);
            Assert.True(shapes.Add(string.Join(",", pool)),
                $"{species} のプールが他の種族と丸ごと同じ（巣を選ぶ理由が消える）");

            foreach (string id in Golden.Strings(entry.GetProperty("pool")))
            {
                Assert.True(anywhere.Contains(id),
                    $"{id}: 移植元では {species} から出たのに、いまはどこからも手に入らない");
            }
        }
    }

    /// <summary>袋の不変条件。⭐ **型の縛りを外した代わりに置いたもの**（2026-08-19）。
    ///
    /// ⚠️ 型で縛っていた頃、この検査は「枠2 と枠3 が別の型」だった。
    /// 縛りが守っていたのは「1つの種族が単一の役割にならない」ことだったので、
    /// **それを直接数える**形へ書き直してある。
    ///
    /// ⚠️ 縛りを外したぶん、代わりに数えるものが増えた:
    /// <list type="number">
    /// <item>袋の大きさ ── ⭐ **狙える確率はここだけで決まる**（1/(a×b)）</item>
    /// <item>1つの技が入っている袋の数 ── ⚠️ 増やすと「どこで奪っても同じ」に戻る</item>
    /// <item>枠2 と枠3 の重なり ── 同じ技が2枠を占めると片方が無駄</item>
    /// <item>役割の偏り ── 2つの袋が同じ役割だけだと、分けた意味が無い</item>
    /// </list></summary>
    [Fact]
    public void 袋の不変条件()
    {
        var homes = new Dictionary<string, List<string>>();
        foreach (var species in SpeciesTable.All)
        {
            var a = species.Slot2.Pool;
            var b = species.Slot3.Pool;

            Assert.True(a.Count > 0 && b.Count > 0, $"{species.Id}: 袋が空");
            Assert.True(a.Count <= Skills.PoolMax,
                $"{species.Id}: 枠2 が {a.Count} 件（上限 {Skills.PoolMax}）");
            Assert.True(b.Count <= Skills.PoolMax,
                $"{species.Id}: 枠3 が {b.Count} 件（上限 {Skills.PoolMax}）");

            foreach (var id in a)
                Assert.DoesNotContain(id, b);

            var roles = new HashSet<SkillType>();
            foreach (var id in a) roles.Add(Skills.TypeOf(Skills.ById(id)));
            foreach (var id in b) roles.Add(Skills.TypeOf(Skills.ById(id)));
            Assert.True(roles.Count >= 2, $"{species.Id}: 2つの袋が同じ役割しか持たない");

            foreach (var id in a) Home(homes, id, $"{species.Id}枠2");
            foreach (var id in b) Home(homes, id, $"{species.Id}枠3");
        }

        foreach (var pair in homes)
        {
            Assert.True(pair.Value.Count <= Skills.SpreadMax,
                $"{pair.Key}: {pair.Value.Count} か所の袋に居る（上限 {Skills.SpreadMax}）"
                + $" ── {string.Join(" ", pair.Value)}");
        }
    }

    private static void Home(Dictionary<string, List<string>> homes, string id, string where)
    {
        if (!homes.TryGetValue(id, out var list)) homes[id] = list = new List<string>();
        list.Add(where);
    }

    [Fact]
    public void 知らない_id_は黙って握りつぶさない()
    {
        Assert.Throws<System.ArgumentException>(() => Skills.ById("no-such-skill"));
        Assert.Throws<System.ArgumentException>(() => Skills.GachaPoolOf("no-such-species", "attack"));
    }
}

public class SpeciesGoldenTests
{
    /// <summary>意図して移植元から変えた枠1。⭐ **ここに書いたものだけが許される。**
    /// ⚠️ 枠1 に CT が無いのは「行動できない手番を作らない」ためで、大技だからではない。
    /// 全体攻撃や状態異常付きが毎手番飛ぶのは通常攻撃ではないので差し替えた。</summary>
    private static readonly Dictionary<string, (string Was, string Now)> Rebuilt =
        new Dictionary<string, (string, string)>
        {
            ["haneru"] = ("attack-all", "attack-twice"),
        };


    [Fact]
    public void 三すくみが一致する()
    {
        var golden = Golden.Load("species");
        var beats = golden.GetProperty("elementBeats");
        foreach (var element in SpeciesTable.Elements)
        {
            // ⚠️ 移植元の名前で引く（牙=炎 / 羽=木 / 鱗=水）。⭐ Golden.Element と同じ対応
            string key = element switch
            {
                Element.Fire => "fang",
                Element.Wood => "plume",
                _ => "scale",
            };
            Assert.Equal(Golden.Element(beats.GetProperty(key).GetString()!), SpeciesTable.Beats(element));
        }

        // ⚠️ 画面に出す語だけは移植元と変えた（牙/羽/鱗 → 炎/水/木）ので照合しない。
        // ⭐ 上の輪の照合が通っている＝**中身は同じで名前だけ変えた**ことは示せている。
        Assert.Equal("炎", SpeciesTable.LabelOf(Element.Fire));
        Assert.Equal("水", SpeciesTable.LabelOf(Element.Water));
        Assert.Equal("木", SpeciesTable.LabelOf(Element.Wood));
    }

    /// <summary>⭐ id で引く。並び順も件数も見ない（理由は技の側と同じ）。</summary>
    [Fact]
    public void 移植した種族が1つも変わっていない()
    {
        var golden = Golden.Load("species");
        // ⚠️ **基礎値の合計は意図して変えた**（80 → 120・2026-08-19・作者の判断）。
        //    弱化命中・弱化耐性が全種族 0 で、育成の同じ ＋20 が他ステの倍の倍率
        //    （+115% 対 +52〜63%）で効いていたため。
        // ⭐ 既存の4本は1つも触っていない ── 下の照合がそれを示す。
        Assert.Equal(80, golden.GetProperty("baseTotal").GetInt32());
        // ⚠️ 合計 120 は「移植元の 80 ＋ 弱化2本ぶん 40」。さらに桁を Scale 倍してある
        Assert.Equal(120 * Stats.Scale, SpeciesTable.BaseTotal);

        var list = golden.GetProperty("list");

        foreach (var entry in list.EnumerateArray())
        {
            var species = SpeciesTable.ById(entry.GetProperty("id").GetString()!);
            Assert.Equal(entry.GetProperty("id").GetString(), species.Id);
            Assert.Equal(entry.GetProperty("name").GetString(), species.Name);
            // ⚠️ 属性は種族の欄ではなくなった（個体が持つ）。移植元の割り当ては
            //    Migrations が「昔の属性」として持っていて、古い保存と照合の入力に使う。
            //    ⭐ ここが一致していれば、その表が移植元に忠実であることの証明になる。
            Assert.Equal(Golden.Element(entry.GetProperty("element").GetString()!),
                Migrations.ElementOf(species.Id));
            // ⚠️ 枠1＝**その種族の通常攻撃**と定めた（2026-08-17）ので、
            //    通常攻撃として読めなかった種族の枠1 を差し替えた。
            // ⭐ **差し替えたものは下の表に書く。**書いていない種族が移植元と違ったら落ちる
            //    （属性の語を変えたときと同じ扱い方）。
            string id = species.Id;
            if (Rebuilt.ContainsKey(id))
            {
                Assert.Equal(Rebuilt[id].Was, entry.GetProperty("skill1").GetString());
                Assert.Equal(Rebuilt[id].Now, species.Skill1);
            }
            else
            {
                Assert.Equal(entry.GetProperty("skill1").GetString(), species.Skill1);
            }
            // ⭐ **移植元の4本（HP・攻撃・防御・速度）は1つも変えていない。**
            // ⚠️ 弱化命中・弱化耐性は移植元に無い欄なので、ここでは比べない
            //    （2026-08-19 に全種族へ配った。下で合計として検査する）。
            // ⭐ **移植元の4本は、配分が1つも動いていない。**
            // ⚠️ 桁だけ Stats.Scale 倍にした（2026-08-19・作者の指示「大きな桁にしたい」）。
            //    倍率で戻せば移植元と1つも違わないので、照合の強さは落ちていない。
            var was = Golden.Block(entry.GetProperty("base"));
            Assert.Equal(was.Hp * Stats.Scale, species.Base.Hp);
            Assert.Equal(was.Atk * Stats.Scale, species.Base.Atk);
            Assert.Equal(was.Def * Stats.Scale, species.Base.Def);
            Assert.Equal(was.Spd * Stats.Scale, species.Base.Spd);

            // ⚠️ 種族ごとに基礎値の合計を変えない
            Assert.Equal(entry.GetProperty("baseTotal").GetInt32(),
                was.Hp + was.Atk + was.Def + was.Spd);
            Assert.Equal(SpeciesTable.BaseTotal, Stats.TotalOf(species.Base));
            // ⭐ 足したぶんも全種族で同じ（差は配分だけ）
            Assert.Equal(
                SpeciesTable.BaseTotal - entry.GetProperty("baseTotal").GetInt32() * Stats.Scale,
                species.Base.Acc + species.Base.Res);

            // ⚠️ 枠1 を差し替えた種族は名前も当然変わる（上の表が根拠）
            if (!Rebuilt.ContainsKey(id))
            {
                Assert.Equal(entry.GetProperty("skill1Name").GetString(), Skills.ById(species.Skill1).Name);
            }
            Assert.Same(species, SpeciesTable.ById(species.Id));
        }
    }

    /// <summary>⭐ 添字色そのものを比べる。ここがずれると変異のパレットスワップが崩れる。</summary>
    [Fact]
    public void ドット絵の添字色が一致する()
    {
        var golden = Golden.Load("species");
        foreach (var entry in golden.GetProperty("list").EnumerateArray())
        {
            var species = SpeciesTable.ById(entry.GetProperty("id").GetString()!);
            var sprite = species.Sprite;

            Assert.Equal(entry.GetProperty("spriteWidth").GetInt32(), sprite.Width);
            Assert.Equal(entry.GetProperty("spriteHeight").GetInt32(), sprite.Height);

            var rows = Golden.Strings(entry.GetProperty("spriteRows"));
            Assert.Equal(sprite.Height, rows.Count);
            for (int y = 0; y < sprite.Height; y++)
            {
                var actual = new StringBuilder(sprite.Width);
                for (int x = 0; x < sprite.Width; x++) actual.Append((char)('0' + sprite.At(x, y)));
                Assert.Equal(rows[y], actual.ToString());
            }
        }
    }

    [Fact]
    public void パレットが一致する()
    {
        var golden = Golden.Load("species");
        foreach (var entry in golden.GetProperty("list").EnumerateArray())
        {
            var species = SpeciesTable.ById(entry.GetProperty("id").GetString()!);
            var palettes = entry.GetProperty("palettes");
            Assert.Equal(palettes.GetArrayLength(), species.Palettes.Count);

            int p = 0;
            foreach (var paletteJson in palettes.EnumerateArray())
            {
                var expected = Golden.Strings(paletteJson);
                Assert.Equal(expected, new List<string>(species.Palettes[p].Colors));
                p++;
            }
        }
    }

    /// <summary>中身を足した日に黙って壊れないための、数える検査そのもの。
    ///
    /// ⭐ **中身が増えるほど守る範囲が広がる側**の検査。golden とは役割が逆で、
    /// golden が「変えていないこと」を見るのに対し、ここは「足したものが繋がっているか」を見る。</summary>
    [Fact]
    public void 中身の数える検査が通る()
    {
        Content.Audit();
    }

    [Fact]
    public void 表に無い種族は投げる()
    {
        Assert.Throws<System.ArgumentException>(() => SpeciesTable.ById("no-such-species"));
    }
}
