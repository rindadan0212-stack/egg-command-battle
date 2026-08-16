using System.Collections.Generic;
using EggCommand.Core;

namespace EggCommand.Tests;

public class BattleGoldenTests
{
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

public class StealGoldenTests
{
    [Fact]
    public void 発射フェーズの定数が一致する()
    {
        var golden = Golden.Load("steal");
        Assert.Equal(golden.GetProperty("fieldWidth").GetDouble(), Steal.FieldWidth);
        Assert.Equal(golden.GetProperty("speedToDistance").GetDouble(), Steal.SpeedToDistance);
        Assert.Equal(golden.GetProperty("gapWidth").GetDouble(), Steal.GapWidth);
        Assert.Equal(golden.GetProperty("lean").GetDouble(), Steal.Lean);
        Assert.Equal(golden.GetProperty("eggRadius").GetDouble(), Steal.EggRadius);
        Assert.Equal(golden.GetProperty("runnerRadius").GetDouble(), Steal.RunnerRadius);

        foreach (var entry in golden.GetProperty("depths").EnumerateArray())
        {
            Assert.Equal(entry.GetProperty("depth").GetDouble(),
                Steal.DepthForTier(entry.GetProperty("tier").GetInt32()));
        }
    }

    /// <summary>⭐ 乱数を使わないので、同じ角度・同じ飛距離からは必ず同じ結果になる。
    /// ⚠️ ここは三角関数を通るので、桁の最後の1ビットが処理系で違いうる。
    /// 落ちたときは「境目に当たった1件だけか」を先に見る。</summary>
    [Fact]
    public void 発射の結果が一致する()
    {
        var golden = Golden.Load("steal");
        foreach (var entry in golden.GetProperty("fields").EnumerateArray())
        {
            int tier = entry.GetProperty("tier").GetInt32();
            var side = Golden.Side(entry.GetProperty("side").GetString()!);
            var field = Steal.MakeField(tier, side);
            string where = $"tier={tier} {side}";

            Assert.True(entry.GetProperty("height").GetDouble() == field.Height, $"{where}: 奥行き");
            Assert.True(entry.GetProperty("gapFrom").GetDouble() == field.GapFrom, $"{where}: 隙間の左");
            Assert.True(entry.GetProperty("gapTo").GetDouble() == field.GapTo, $"{where}: 隙間の右");
            Assert.True(entry.GetProperty("bandTop").GetDouble() == field.BandTop, $"{where}: 帯の上");
            Assert.True(entry.GetProperty("bandBottom").GetDouble() == field.BandBottom, $"{where}: 帯の下");

            var spans = Steal.ParentSpans(field);
            var spansJson = entry.GetProperty("spans");
            Assert.True(spansJson.GetArrayLength() == spans.Count, $"{where}: 塞ぎの枚数が {spans.Count}");
            int s = 0;
            foreach (var spanJson in spansJson.EnumerateArray())
            {
                Assert.True(spanJson.GetProperty("from").GetDouble() == spans[s].From, $"{where}: 塞ぎ{s}の左");
                Assert.True(spanJson.GetProperty("to").GetDouble() == spans[s].To, $"{where}: 塞ぎ{s}の右");
                s++;
            }

            foreach (var launchJson in entry.GetProperty("launches").EnumerateArray())
            {
                int deg = launchJson.GetProperty("deg").GetInt32();
                var run = Steal.Launch(field, deg * System.Math.PI / 180.0, 400);
                Assert.True(Golden.Steal(launchJson.GetProperty("outcome").GetString()!) == run.Outcome,
                    $"{where} {deg}°: 結果が {run.Outcome}（期待 {launchJson.GetProperty("outcome").GetString()}）");
                Assert.True(launchJson.GetProperty("traveled").GetDouble() == run.Traveled,
                    $"{where} {deg}°: 飛距離が {run.Traveled}");
                Assert.True(launchJson.GetProperty("pathLength").GetInt32() == run.Path.Count,
                    $"{where} {deg}°: 軌跡の点数が {run.Path.Count}");
            }

            // ⭐ 設計が解けるものになっているか（解けない巣を出荷しない）
            var solutionJson = entry.GetProperty("solution");
            bool found = Steal.FindSolution(field, 400, 180, out _, out double traveled);
            if (solutionJson.ValueKind == System.Text.Json.JsonValueKind.Null)
            {
                Assert.False(found, $"{where}: 解けないはずが解けた");
            }
            else
            {
                Assert.True(found, $"{where}: 解けるはずが解けなかった");
                Assert.True(solutionJson.GetProperty("traveled").GetDouble() == traveled,
                    $"{where}: 最短の飛距離が {traveled}");
            }
        }
    }
}
