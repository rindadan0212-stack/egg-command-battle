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

            Assert.True(true, where);
        }
    }
}

public class StatsGoldenTests
{
    [Fact]
    public void 上限の定数が一致する()
    {
        var golden = Golden.Load("stats");
        Assert.Equal(golden.GetProperty("wildStatMax").GetInt32(), Stats.WildStatMax);
        Assert.Equal(golden.GetProperty("wildTotalMax").GetInt32(), Stats.WildTotalMax);
        Assert.Equal(golden.GetProperty("mutationCapSteps").GetInt32(), Stats.MutationCapSteps);

        // ⭐ 合計上限は常に1ステ上限の2倍。この比が「得意を2つ作れる」を保証している
        foreach (var entry in golden.GetProperty("maxFor").EnumerateArray())
        {
            int mutation = entry.GetProperty("mutation").GetInt32();
            Assert.Equal(entry.GetProperty("statMax").GetInt32(), Stats.WildStatMaxFor(mutation));
            Assert.Equal(entry.GetProperty("totalMax").GetInt32(), Stats.WildTotalMaxFor(mutation));
        }
    }

    [Fact]
    public void ステの並びが一致する()
    {
        var golden = Golden.Load("stats");
        var expected = Golden.Strings(golden.GetProperty("statKeys"));
        Assert.Equal(expected.Count, Stats.Keys.Length);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(Golden.StatKey(expected[i]), Stats.Keys[i]);
        }
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
    /// 同値のステが複数あるときの削り順まで一致していないと、育成の結果が変わる。</summary>
    [Fact]
    public void 合計上限の削り方が一致する()
    {
        var golden = Golden.Load("stats");
        foreach (var entry in golden.GetProperty("applyTotalCap").EnumerateArray())
        {
            var wild = Golden.Block(entry.GetProperty("wild"));
            int mutation = entry.GetProperty("mutation").GetInt32();
            var expected = Golden.Block(entry.GetProperty("out"));
            var actual = Stats.ApplyTotalCap(wild, mutation);
            Assert.Equal(expected, actual);
            Assert.Equal(entry.GetProperty("total").GetInt32(), Stats.TotalOf(actual));
        }
    }

    [Fact]
    public void 実値の求め方が一致する()
    {
        var golden = Golden.Load("stats");
        foreach (var entry in golden.GetProperty("actualStats").EnumerateArray())
        {
            var actual = Stats.ActualStats(
                Golden.Block(entry.GetProperty("base")),
                Golden.Block(entry.GetProperty("wild")),
                Golden.Block(entry.GetProperty("trained")));
            Assert.Equal(Golden.Block(entry.GetProperty("out")), actual);
        }
    }
}

public class SkillsGoldenTests
{
    [Fact]
    public void 威力と割合の表が一致する()
    {
        var golden = Golden.Load("skills");
        var power = golden.GetProperty("damagePower");
        Assert.Equal(power.GetProperty("小").GetInt32(), Skills.DamagePowerOf(PowerTier.Small));
        Assert.Equal(power.GetProperty("中").GetInt32(), Skills.DamagePowerOf(PowerTier.Medium));
        Assert.Equal(power.GetProperty("大").GetInt32(), Skills.DamagePowerOf(PowerTier.Large));
        Assert.Equal(power.GetProperty("特大").GetInt32(), Skills.DamagePowerOf(PowerTier.Huge));
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
            Assert.Equal(entry.GetProperty("gist").GetString(), skill.Gist);
            Assert.Equal(entry.GetProperty("ct").GetInt32(), skill.Ct);
            Assert.Equal(Golden.Target(entry.GetProperty("target").GetString()!), skill.Target);

            // ⭐ 枠1 の CT は常に 0。CT は技ではなく枠の性質
            Assert.Equal(entry.GetProperty("ctSlot0").GetInt32(), Skills.EffectiveCt(0, skill));
            Assert.Equal(entry.GetProperty("ctSlot1").GetInt32(), Skills.EffectiveCt(1, skill));
            Assert.Equal(entry.GetProperty("ctSlot2").GetInt32(), Skills.EffectiveCt(2, skill));

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
                Assert.Equal(harmful, Skills.IsHarmful(effect));
                e++;
            }
        }
    }

    /// <summary>⭐ 種族ごとにプールを分けていること。枠1と同じ技が外れていること。
    ///
    /// ⚠️ **ここだけは「完全に同じ」を要求する。** プールは乱数で引く対象なので、
    /// 既にある種族のプールに1つ足すと、そこから孵る卵の技が全部ずれ、
    /// nest / game / breeding の照合がまとめて落ちる。
    ///
    /// ⭐ つまり **移植済みの4種のプールは凍結**。新しい技は**新しい種族のプールへ**入れる。
    /// 既存種族に技を足したくなったら、それは golden を捨てる判断なので、先に決める。</summary>
    [Fact]
    public void 移植した卵ガチャのプールが1つも変わっていない()
    {
        var golden = Golden.Load("skills");
        foreach (var entry in golden.GetProperty("gachaPools").EnumerateArray())
        {
            string species = entry.GetProperty("species").GetString()!;
            string skill1 = entry.GetProperty("skill1").GetString()!;
            var expected = Golden.Strings(entry.GetProperty("pool"));
            Assert.Equal(expected, Skills.GachaPoolOf(species, skill1));
            Assert.DoesNotContain(skill1, Skills.GachaPoolOf(species, skill1));
        }
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
        Assert.Equal(golden.GetProperty("baseTotal").GetInt32(), SpeciesTable.BaseTotal);

        var list = golden.GetProperty("list");

        foreach (var entry in list.EnumerateArray())
        {
            var species = SpeciesTable.ById(entry.GetProperty("id").GetString()!);
            Assert.Equal(entry.GetProperty("id").GetString(), species.Id);
            Assert.Equal(entry.GetProperty("name").GetString(), species.Name);
            Assert.Equal(Golden.Element(entry.GetProperty("element").GetString()!), species.Element);
            Assert.Equal(entry.GetProperty("skill1").GetString(), species.Skill1);
            Assert.Equal(Golden.Block(entry.GetProperty("base")), species.Base);

            // ⚠️ 種族ごとに基礎値の合計を変えない
            Assert.Equal(entry.GetProperty("baseTotal").GetInt32(), Stats.TotalOf(species.Base));
            Assert.Equal(SpeciesTable.BaseTotal, Stats.TotalOf(species.Base));

            Assert.Equal(entry.GetProperty("skill1Name").GetString(), Skills.ById(species.Skill1).Name);
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
