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
    /// ⚠️ 実測で洗い出した6件。⭐ **挑発を持つ個体が出る対戦だけ**が変わっている
    /// （鱗の巣とヌシ。牙・羽の巣は1手も変わらない）。
    private static readonly HashSet<string> TauntChanged = new HashSet<string>
    {
        "seed=1 vs shallow-scale",
        "seed=1 vs deep-scale",
        "seed=1 vs boss",
        "seed=20260816 vs shallow-scale",
        "seed=20260816 vs deep-scale",
        "seed=20260816 vs boss",
    };

    [Fact]
    public void 較正済みの定数が一致する()
    {
        var golden = Golden.Load("battle");
        Assert.Equal(golden.GetProperty("gaugeMax").GetInt32(), Battle.GaugeMax);
        Assert.Equal(golden.GetProperty("gaugeBase").GetInt32(), Battle.GaugeBase);
        Assert.Equal(golden.GetProperty("maxActions").GetInt32(), Battle.MaxActions);
        Assert.Equal(golden.GetProperty("hpScale").GetInt32(), Battle.HpScale);
        Assert.Equal(golden.GetProperty("elementAdvantage").GetDouble(), Battle.ElementAdvantage);
        Assert.Equal(golden.GetProperty("atkSoften").GetInt32(), Battle.AtkSoften);
        Assert.Equal(golden.GetProperty("defSoften").GetInt32(), Battle.DefSoften);
        Assert.Equal(golden.GetProperty("damageNormalize").GetDouble(), Battle.DamageNormalize);
    }

    [Fact]
    public void ダメージの式が一致する()
    {
        var golden = Golden.Load("battle");
        foreach (var entry in golden.GetProperty("damageOf").EnumerateArray())
        {
            int power = entry.GetProperty("power").GetInt32();
            int atk = entry.GetProperty("atk").GetInt32();
            int def = entry.GetProperty("def").GetInt32();
            double mult = entry.GetProperty("mult").GetDouble();
            Assert.True(entry.GetProperty("out").GetInt32() == Battle.DamageOf(power, atk, def, mult),
                $"power={power} atk={atk} def={def} mult={mult} → {Battle.DamageOf(power, atk, def, mult)}");
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

        foreach (var entry in golden.GetProperty("gaugeRate").EnumerateArray())
        {
            Assert.Equal(entry.GetProperty("out").GetInt32(),
                Battle.GaugeRate(entry.GetProperty("speed").GetInt32(), entry.GetProperty("tempo").GetDouble()));
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

    /// <summary>⭐ 戦闘に乱数は無いので、同じ編成からは必ず同じ試合になる。
    /// 1手でもずれたら、較正済みの HP3倍 / 手数2倍 が意味を失う。</summary>
    [Fact]
    public void 試合が丸ごと一致する()
    {
        var golden = Golden.Load("battle");
        foreach (var matchup in golden.GetProperty("matchups").EnumerateArray())
        {
            int seed = matchup.GetProperty("seed").GetInt32();
            string name = matchup.GetProperty("name").GetString()!;
            string where = $"seed={seed} vs {name}";

            var game = Games.NewGame(seed);
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
                Assert.True(entry.GetProperty("maxHp").GetInt32() == unit.MaxHp,
                    $"{where}/{unit.Key}: 最大HPが {unit.MaxHp}（期待 {entry.GetProperty("maxHp").GetInt32()}）");
                Assert.True(entry.GetProperty("tempo").GetDouble() == unit.Tempo,
                    $"{where}/{unit.Key}: 手数倍率が {unit.Tempo}");
                Assert.True(entry.GetProperty("speed").GetInt32() == Battle.SpeedOf(unit),
                    $"{where}/{unit.Key}: 速度が {Battle.SpeedOf(unit)}");
            }

            // ⚠️ **ここから先は属性が絡む組み合わせでは比べられない。**
            // 不利倍率を 1/1.5（0.667）から 0.75 へ変えると決めたので、
            // 属性の食い違う対戦は移植元と違う経過をたどる。数値を変えた判断そのものは
            // 課題.md（属性の有利が 100%/0%）に基づく。
            //
            // ⭐ 属性の同じ対戦（浅瀬・深み・ヌシ）は倍率を一度も通らないので、
            // 手番の順・CT・状態異常・出来事の並びは**そのまま丸ごと照合できている**。
            // ⚠️ 上の開幕の並び（最大HP・手数倍率・速度）は倍率と無関係なので全件で見る。
            var allyElement = allies[0].Element;
            bool crossElement = false;
            foreach (var foe in enemies)
            {
                if (foe.Element != allyElement) crossElement = true;
            }
            if (crossElement) continue;
            // ⚠️ 挑発の作り替えで経過が変わる対戦。開幕の並びまでは上で照合済み
            if (TauntChanged.Contains(where)) continue;

            int guard = 0;
            while (state.Result == null && guard++ < Battle.MaxActions * 3)
            {
                var actor = Battle.NextActor(state);
                if (actor == null) break;
                int slot = Ai.ChooseAction(state, actor);
                Battle.PerformAction(state, actor, slot);
            }

            Assert.True(Golden.Result(matchup.GetProperty("outcome").GetString()!) == state.Result,
                $"{where}: 決着が {state.Result}（期待 {matchup.GetProperty("outcome").GetString()}）");
            Assert.True(matchup.GetProperty("actions").GetInt32() == state.Actions,
                $"{where}: 行動数が {state.Actions}（期待 {matchup.GetProperty("actions").GetInt32()}）");
            Assert.True(matchup.GetProperty("logLength").GetInt32() == state.Log.Count,
                $"{where}: 出来事の数が {state.Log.Count}（期待 {matchup.GetProperty("logLength").GetInt32()}）");

            // 先頭40件
            int e = 0;
            foreach (var entry in matchup.GetProperty("logHead").EnumerateArray())
            {
                Golden.SameEvent(entry, state.Log[e], $"{where} log[{e}]");
                e++;
            }
            // 末尾10件
            var tail = matchup.GetProperty("logTail");
            int offset = state.Log.Count - tail.GetArrayLength();
            e = 0;
            foreach (var entry in tail.EnumerateArray())
            {
                Golden.SameEvent(entry, state.Log[offset + e], $"{where} logTail[{e}]");
                e++;
            }

            i = 0;
            foreach (var entry in matchup.GetProperty("finalHp").EnumerateArray())
            {
                var unit = state.Units[i++];
                Assert.True(entry.GetProperty("hp").GetInt32() == unit.Hp,
                    $"{where}/{unit.Key}: 最終HPが {unit.Hp}（期待 {entry.GetProperty("hp").GetInt32()}）");
            }
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
        Assert.Equal(golden.GetProperty("speedToDistance").GetDouble(), Steal.SpeedToDistance);
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
