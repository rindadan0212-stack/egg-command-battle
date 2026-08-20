using System.Collections.Generic;
using EggCommand.Core;

namespace EggCommand.Tests;

public class BattleGoldenTests
{
    /// <summary>⚠️ **挑発を作り替えたので経過が変わる対戦**（2026-08-18）。
    ///
    /// 移植元の挑発は「味方に付けて、味方への単体攻撃を引き受ける」＝強化だった。
    /// ⭐ 相手に付けて「掛けた本人しか狙えなくする」＝弱化に変えたので、
    /// 挑発を持つ個体が出る対戦は狙い先が変わり、手数も変わる。
    ///
    /// ⚠️ **ここに書いたものだけが許される。**書いていない対戦が変わったら落ちる。
    /// ⚠️ ゴールデンは作り直さない ── 作り直すと「移植元と一致している」証明が消える。
    /// ⭐ 開幕の並び（最大HP・手数倍率・速度）は挑発と無関係なので**全件で見続ける**。</summary>
    /// ⚠️ 実測で洗い出した6件は 鱗の巣（shallow-scale / deep-scale）とヌシ、
    /// 種2つ（seed 1 / 20260816）＝ **同属性の6対戦すべて**だった。
    /// 残る6対戦は属性が食い違うので、不利倍率を変えた時点で既に比べられない。
    /// ⭐ つまり 12/12。除外の表は「全件」になったので持たない
    /// （表として残すと、まだ一部だけ除いているように読めてしまう）。

    [Fact]
    public void 較正済みの定数が一致する()
    {
        var golden = Golden.Load("battle");
        // ⚠️ **桁を上げた**（2026-08-19・作者の指示）。ステと同じ倍率で動かした定数は
        //    倍率で戻して照合する。⭐ どれも比の式なので、揃えて動かせば釣り合いは1つも動かない
        //    ── その証拠が下の damageNormalize（倍率が約分されて移植元と同じ値のまま）。
        Assert.Equal(golden.GetProperty("gaugeMax").GetInt32() * Stats.Scale, Battle.GaugeMax);
        Assert.Equal(golden.GetProperty("gaugeBase").GetInt32() * Stats.Scale, Battle.GaugeBase);
        Assert.Equal(golden.GetProperty("maxActions").GetInt32(), Battle.MaxActions);
        // ⚠️ HP は「ステの桁」に加えて HpBoost ぶん大きい（技の威力にも同じだけ掛けてある）
        Assert.Equal(golden.GetProperty("hpScale").GetInt32() * Battle.HpBoost, Battle.HpScale);
        Assert.Equal(golden.GetProperty("elementAdvantage").GetDouble(), Battle.ElementAdvantage);
        Assert.Equal(golden.GetProperty("atkSoften").GetInt32() * Stats.Scale, Battle.AtkSoften);
        Assert.Equal(golden.GetProperty("defSoften").GetInt32() * Stats.Scale, Battle.DefSoften);
        Assert.Equal(golden.GetProperty("damageNormalize").GetDouble(), Battle.DamageNormalize);
    }

    [Fact]
    public void ダメージの式が一致する()
    {
        var golden = Golden.Load("battle");
        // ⚠️ **遊びの式は 2026-08-19 に組み替えた**（威力が「攻撃力の何倍か」になった）。
        //    ⭐ 移植元の式は <see cref="Battle.DamageOfPorted"/> に残してあり、ここが唯一の使い手。
        //    消すと「移植が正しい」証明が消えるので残す（Breeding と Fusion の関係と同じ）。
        // ⭐ 攻撃・防御を桁ぶん大きくして渡せば、軟化定数も同じ倍率なので約分され、
        //    移植元と**1つも違わない**答えが出る。
        foreach (var entry in golden.GetProperty("damageOf").EnumerateArray())
        {
            int power = entry.GetProperty("power").GetInt32();
            int atk = entry.GetProperty("atk").GetInt32() * Stats.Scale;
            int def = entry.GetProperty("def").GetInt32() * Stats.Scale;
            double mult = entry.GetProperty("mult").GetDouble();
            int got = Battle.DamageOfPorted(power, atk, def, mult);
            Assert.True(entry.GetProperty("out").GetInt32() == got,
                $"power={power} atk={atk} def={def} mult={mult} → {got}");
        }
    }

    [Fact]
    public void 実効値とゲージが一致する()
    {
        var golden = Golden.Load("battle");
        foreach (var entry in golden.GetProperty("effectiveStat").EnumerateArray())
        {
            var mod = new Modifier
            {
                Percent = entry.GetProperty("percent").GetInt32(),
                Turns = entry.GetProperty("turns").GetInt32(),
            };
            Assert.Equal(entry.GetProperty("out").GetInt32(),
                Battle.EffectiveStat(entry.GetProperty("base").GetInt32(), mod));
        }

        // ⭐ ゲージも桁ぶん大きい（GaugeMax も同じ倍率なので、手番の来る速さは1つも動かない）。
        // ⚠️ 丸めは倍率のあとに1回だけ掛かるので、ぴったり倍にならない場合がある。
        //    ⭐ ずれても倍率ぶん（Stats.Scale）未満であることまで見る。
        foreach (var entry in golden.GetProperty("gaugeRate").EnumerateArray())
        {
            int want = entry.GetProperty("out").GetInt32() * Stats.Scale;
            int got = Battle.GaugeRate(
                entry.GetProperty("speed").GetInt32() * Stats.Scale,
                entry.GetProperty("tempo").GetDouble());
            Assert.True(System.Math.Abs(got - want) < Stats.Scale,
                $"speed={entry.GetProperty("speed").GetInt32()} → {got}（移植元 × 倍率 = {want}）");
        }
    }

    /// <summary>⭐ 「HP=体数比 / 手数=増分の半分」。掃引で決めた比なので、ここがずれると難易度が全部変わる。</summary>
    [Fact]
    public void 体数比の扱いが一致する()
    {
        var golden = Golden.Load("battle");
        foreach (var entry in golden.GetProperty("lone").EnumerateArray())
        {
            int allies = entry.GetProperty("allies").GetInt32();
            int enemies = entry.GetProperty("enemies").GetInt32();
            double scale = Battle.LoneScale(allies, enemies);
            Assert.Equal(entry.GetProperty("scale").GetDouble(), scale);
            Assert.Equal(entry.GetProperty("hp").GetDouble(), Battle.LoneHp(scale));
            Assert.Equal(entry.GetProperty("tempo").GetDouble(), Battle.LoneTempo(scale));
        }
    }

    /// <summary>得意・不得意を外した同じ個体を作り直す（移植元と同じ形）。</summary>
    private static List<Creature> Plain(IReadOnlyList<Creature> party)
    {
        var plain = new List<Creature>();
        foreach (var c in party)
        {
            plain.Add(new Creature(c.Id, c.SpeciesId, c.Wild, c.Trained, c.Earned,
                c.MutationCounter, c.Skill2, c.Skill3, c.PaletteIndex,
                c.ParentA, c.ParentB, c.Generation));
        }
        return plain;
    }

    /// <summary>⭐ **開幕の並びが移植元と一致する。**体数・key・名前・手数倍率を全12対戦で見る。
    ///
    /// ⚠️ **経過（試合そのもの）はここでは見ていない。**名前が「試合が丸ごと一致する」
    /// だった頃は、中で全件が飛ばされているのに名前だけが強い主張をしていた。
    /// ⭐ 経過は SeriesRecordTests（現行の記録）が持つ。</summary>
    [Fact]
    public void 開幕の並びが一致する()
    {
        var golden = Golden.Load("battle");
        foreach (var matchup in golden.GetProperty("matchups").EnumerateArray())
        {
            int seed = matchup.GetProperty("seed").GetInt32();
            string name = matchup.GetProperty("name").GetString()!;
            string where = $"seed={seed} vs {name}";

            // ⚠️ **較正した当時の体数で再生する。**⭐ この検査が見ているのは開幕の**並び順**で、
            //    体数はその対象ではない（2026-08-20 に 3 → 4）。
            var game = Games.NewGame(seed, startWith: Games.CalibratedParty);
            // ⚠️ 得意・不得意は移植元に無い概念。ここは**戦闘そのもの**が移植元と
            //    一致することの検査なので、入力を移植元と同じ形に戻してから渡す。
            //    （得意を付けたまま比べると、engine ではなく個体の違いで落ちる）
            var allies = Plain(Games.PartyOf(game));
            var enemies = name == "boss"
                ? Nests.MakeBossParty()
                : Nests.MakeDefenders(new Rng(555).Stream(name), Nests.ById(name));

            var state = Battle.CreateBattle(allies, enemies);

            // 開幕の並び。⚠️ tempo と maxHp は体数の比から決まる
            var setup = matchup.GetProperty("setup");
            Assert.True(setup.GetArrayLength() == state.Units.Count, $"{where}: 体数が {state.Units.Count}");
            int i = 0;
            foreach (var entry in setup.EnumerateArray())
            {
                var unit = state.Units[i++];
                Assert.True(entry.GetProperty("key").GetString() == unit.Key, $"{where}: key");
                Assert.True(entry.GetProperty("name").GetString() == unit.Name, $"{where}: 名前");
                // ⭐ 手数倍率は体数の比だけで決まる。個体が変わっても動かないので丸ごと照合
                Assert.True(entry.GetProperty("tempo").GetDouble() == unit.Tempo,
                    $"{where}/{unit.Key}: 手数倍率が {unit.Tempo}");
                // ⚠️ 最大HPと速度は素質から来る。素質が6本になって系列が変わった（下の注記）
                Assert.True(unit.MaxHp > 0 && Battle.SpeedOf(unit) > 0, $"{where}/{unit.Key}: 実値が 0");
            }

            // ⚠️ **経過（手番の順・CT・出来事の並び・最終HP）はもうここで比べられない。**
            //
            // 移植元から意図して変えたものが2つ重なっている:
            //   1. 属性の不利倍率 1/1.5（0.667）→ 0.75 … 属性の食い違う6対戦が別経過になる
            //   2. 挑発を「自分に掛ける」→「相手に付ける弱化」… 残る同属性6対戦が別経過になる
            // 合わせて 12/12。⭐ 除外を1件ずつ足していった結果ではなく、
            // **2つの仕様変更で全件が覆われた**という状態。
            //
            // ⚠️ ここには照合の続き（決着・行動数・出来事40+10件・最終HP）が42行あったが、
            //    上の2つの continue に全件が掛かるので**一度も走っていなかった**。
            //    ⭐ 走らない検査を置いておくと「守られている」と読み違えるので畳んだ。
            //
            // ⭐ 経過の担保は SeriesRecordTests（現行の記録・digest つき・同じ12対戦）が持つ。
            // ⚠️ ゴールデンは作り直さない ── 開幕の並び（体数・key・名前・手数倍率）は
            //    属性とも挑発とも無関係なので、**全12対戦で今も見ている**。
        }
    }
}

/// <summary>発射フェーズ。
///
/// ⚠️ 盤の寸法（FieldWidth / GapWidth / Lean / ParentWidth）は移植後に**意図して変えた**。
/// FieldWidth は跳ね返りの壁そのものなので、移植元と同じ軌跡はもう出ない。
/// ⭐ そこで「移植元と一致するか」ではなく**規則そのもの**を検査する。
/// 変えていない較正値（飛距離の係数・当たりの半径・段ごとの奥行き）はそのまま比べる。
/// </summary>
public class StealGoldenTests
{
    [Fact]
    public void 変えていない較正値は移植元と一致する()
    {
        var golden = Golden.Load("steal");
        // ⚠️ 盤は 0〜1 の座標なので、ステの桁上げぶんはここで割り戻してある（2026-08-19）
        Assert.Equal(golden.GetProperty("speedToDistance").GetDouble() / Stats.Scale,
            Steal.SpeedToDistance);
        Assert.Equal(golden.GetProperty("eggRadius").GetDouble(), Steal.EggRadius);
        Assert.Equal(golden.GetProperty("runnerRadius").GetDouble(), Steal.RunnerRadius);

        foreach (var entry in golden.GetProperty("depths").EnumerateArray())
        {
            Assert.Equal(entry.GetProperty("depth").GetDouble(),
                Steal.DepthForTier(entry.GetProperty("tier").GetInt32()));
        }
    }

    [Fact]
    public void 盤の寸法は塞ぐ幅から導かれる()
    {
        // ⚠️ 手で決めた数を置かない。絵と当たり判定が食い違いようがない形にしてある
        Assert.Equal(Steal.FieldWidth - Steal.ParentWidth, Steal.GapWidth);
        Assert.Equal(Steal.ParentWidth + Steal.GapWidth / 2 - Steal.FieldWidth / 2, Steal.Lean);
        Assert.True(Steal.GapWidth > Steal.RunnerRadius * 2,
            "隙間が走者より狭い。どう狙っても通れない");
    }

    [Fact]
    public void 塞ぐ幅は必ず絵一体ぶん()
    {
        // ⭐ 幅がこれを超えると、盤の側で絵を並べて埋めることになる（増殖して見える）
        foreach (int tier in new[] { 1, 2, 3, 4, 5 })
        {
            foreach (var side in new[] { FieldSide.Left, FieldSide.Right })
            {
                var field = Steal.MakeField(tier, side);
                foreach (var span in Steal.ParentSpans(field))
                {
                    Assert.True(span.To - span.From <= Steal.ParentWidth + 0.001,
                        $"tier={tier} {side}: 塞ぐ幅が {span.To - span.From}");
                }
            }
        }
    }

    [Fact]
    public void 軌跡は盤から出ない()
    {
        // ⚠️ 壁の跳ね返りが崩れると、画面の外を飛ぶ
        foreach (int tier in new[] { 1, 3, 5 })
        {
            var field = Steal.MakeField(tier, FieldSide.Right);
            for (int deg = -80; deg <= 80; deg += 5)
            {
                var run = Steal.Launch(field, deg * System.Math.PI / 180.0, 400);
                foreach (var p in run.Path)
                {
                    Assert.True(p.X >= -0.001 && p.X <= Steal.FieldWidth + 0.001,
                        $"tier={tier} {deg}°: x={p.X}");
                    Assert.True(p.Y >= -0.001 && p.Y <= field.Height + 0.001,
                        $"tier={tier} {deg}°: y={p.Y}");
                }
            }
        }
    }

    [Fact]
    public void 同じ角度からは必ず同じ結果()
    {
        // ⭐ 乱数を使っていないことの検査。腕前の勝負なので、揺れてはいけない
        var field = Steal.MakeField(3, FieldSide.Right);
        for (int deg = -60; deg <= 60; deg += 7)
        {
            var a = Steal.Launch(field, deg * System.Math.PI / 180.0, 300);
            var b = Steal.Launch(field, deg * System.Math.PI / 180.0, 300);
            Assert.Equal(a.Outcome, b.Outcome);
            Assert.Equal(a.Traveled, b.Traveled);
            Assert.Equal(a.Path.Count, b.Path.Count);
        }
    }

    [Fact]
    public void 飛距離が足りなければ届かない()
    {
        var field = Steal.MakeField(5, FieldSide.Right);
        Assert.NotEqual(StealOutcome.Success, Steal.Launch(field, 0.0, 10).Outcome);
    }

    [Fact]
    public void 隙間を抜ければ卵に届く()
    {
        // ⭐ どこかの角度では必ず成功する。成功しえない盤は詰み
        foreach (int tier in new[] { 1, 2, 3, 4, 5 })
        {
            bool any = false;
            var field = Steal.MakeField(tier, FieldSide.Right);
            for (int deg = -80; deg <= 80 && !any; deg++)
            {
                any = Steal.Launch(field, deg * System.Math.PI / 180.0, 2000).Outcome
                    == StealOutcome.Success;
            }
            Assert.True(any, $"tier={tier}: どの角度でも届かない");
        }
    }
}
