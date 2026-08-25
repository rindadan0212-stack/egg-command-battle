using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>試練。⚠️ **goldens では守れない**（移植元に無い）。ここが唯一の見張り。</summary>
public class TrialTests
{
    [Fact]
    public void 表に不備がない()
    {
        Assert.Equal(new List<string>(), Trials.Faults());
        Trials.Audit();
    }

    /// <summary>⭐ **5段ある**（作者の指示 2026-08-21）。
    /// ⚠️ 数を変えるなら、変える理由を <c>仕様変更履歴</c> に書くこと。</summary>
    [Fact]
    public void 五段ある()
    {
        Assert.Equal(5, Trials.All.Count);
        for (int i = 0; i < Trials.All.Count; i++)
            Assert.Equal(i + 1, Trials.StepOf(Trials.All[i].Id));
    }

    /// <summary>⭐ **毎回まったく同じ顔ぶれ。**
    /// ⚠️ 引き直せると「何が足りなかったか考えて、組み直して、挑み直す」が成り立たない。</summary>
    [Fact]
    public void 顔ぶれは何度作っても同じ()
    {
        foreach (var trial in Trials.All)
        {
            var a = Trials.PartyOf(trial);
            var b = Trials.PartyOf(trial);
            Assert.Equal(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.Equal(a[i].SpeciesId, b[i].SpeciesId);
                Assert.Equal(a[i].Element, b[i].Element);
                Assert.Equal(a[i].Skill2, b[i].Skill2);
                Assert.Equal(a[i].Skill3, b[i].Skill3);
                Assert.Equal(Stats.TotalOf(Creatures.StatsOf(a[i])),
                    Stats.TotalOf(Creatures.StatsOf(b[i])));
            }
        }
    }

    /// <summary>⚠️ **体数はこちらと同じ。**揃わないと <see cref="Battle.LoneScale"/> が働いて、
    /// 手で書いたとおりの強さで来なくなる。</summary>
    [Fact]
    public void 体数はパーティと同じ()
    {
        foreach (var trial in Trials.All)
        {
            Assert.Equal(Games.PartySize, trial.Foes.Count);
            Assert.Equal(Games.PartySize, Trials.PartyOf(trial).Count);
        }
    }

    /// <summary>⭐ 相手は**育て切って**来る。⚠️ 素の孵化直後だと案山子になる。</summary>
    [Fact]
    public void 相手は育て切っている()
    {
        foreach (var trial in Trials.All)
            foreach (var foe in Trials.PartyOf(trial))
                Assert.Equal(Creatures.TrainMax, foe.Earned);
    }

    /// <summary>⭐ 特性は種族から決まる（2026-08-21 の決まりが試練でも守られていること）。</summary>
    [Fact]
    public void 相手も種族の特性を持つ()
    {
        foreach (var trial in Trials.All)
            foreach (var foe in Trials.PartyOf(trial))
                Assert.Equal(Creatures.TraitIdFor(foe.SpeciesId), foe.TraitId);
    }

    /// <summary>⚠️ **段が上がるほど素質が上がる。**⭐ 見た目の順番と実際の重さを揃える。</summary>
    [Fact]
    public void 段が上がるほど素質が上がる()
    {
        int before = 0;
        foreach (var trial in Trials.All)
        {
            int total = 0;
            foreach (var foe in Trials.PartyOf(trial)) total += Stats.TotalOf(foe.Wild);
            Assert.True(total > before,
                $"{trial.Id}: 素質の合計 {total} が前の段（{before}）以下");
            before = total;
        }
    }

    // ── 勝った記録 ──────────────────────────────────

    [Fact]
    public void 勝つと印が付き二重には付かない()
    {
        var game = Games.NewGame(2026_08_21);
        string id = Trials.All[0].Id;

        Assert.False(Games.BeatTrial(game, id));
        Assert.Equal(0, Games.TrialsCleared(game));

        Assert.True(Games.MarkTrial(game, id));
        Assert.True(Games.BeatTrial(game, id));
        Assert.Equal(1, Games.TrialsCleared(game));

        // ⚠️ 2度目は false（既に付いている）
        Assert.False(Games.MarkTrial(game, id));
        Assert.Equal(1, Games.TrialsCleared(game));
    }

    [Fact]
    public void 知らない試練の印は付けられない()
    {
        var game = Games.NewGame(3);
        Assert.Throws<System.ArgumentException>(() => Games.MarkTrial(game, "no-such-trial"));
    }

    [Fact]
    public void 勝った印は保存して読み直しても残る()
    {
        var game = Games.NewGame(11);
        Games.MarkTrial(game, Trials.All[0].Id);
        Games.MarkTrial(game, Trials.All[2].Id);

        var back = Snapshots.Load(Snapshots.Save(game));
        Assert.NotNull(back);
        Assert.True(Games.BeatTrial(back!, Trials.All[0].Id));
        Assert.False(Games.BeatTrial(back, Trials.All[1].Id));
        Assert.True(Games.BeatTrial(back, Trials.All[2].Id));
    }

    /// <summary>⚠️ 表から消えた id は読み込みで落とす。
    /// ⭐ 残すと「勝った印が付いているのに、その段が無い」状態になる。</summary>
    [Fact]
    public void 表に無い試練の印は読み込みで落ちる()
    {
        var game = Games.NewGame(13);
        var save = Snapshots.Save(game);
        save.Trials.Add("no-such-trial");

        var notes = new List<string>();
        var back = Snapshots.Load(save, notes);
        Assert.NotNull(back);
        Assert.Equal(0, Games.TrialsCleared(back!));
        Assert.NotEmpty(notes);
    }

    /// <summary>⚠️ 試練より前の保存も読めること。</summary>
    [Fact]
    public void 試練を知らない古いセーブも読める()
    {
        var game = Games.NewGame(17);
        var save = Snapshots.Save(game);
        save.Trials.Clear();

        var back = Snapshots.Load(save);
        Assert.NotNull(back);
        Assert.Equal(0, Games.TrialsCleared(back!));
    }

    /// <summary>⭐ **戦闘として成立している。**⚠️ 決着が付かずに打ち切られる盤を置かない
    /// （挑発と蘇生が噛み合うと、両側が減らないまま上限に達することがある）。</summary>
    [Fact]
    public void どの段も決着が付く()
    {
        foreach (var trial in Trials.All)
        {
            var state = Battle.CreateBattle(Steal.ReferenceParty(5), Trials.PartyOf(trial),
                new Rng(7).Stream("trial-test"));
            int steps = 0;
            while (state.Result == null && steps < Battle.MaxActions)
            {
                var actor = Battle.NextActor(state);
                if (actor == null) break;
                Battle.PerformAction(state, actor, Ai.ChooseAction(state, actor));
                steps++;
            }
            Assert.True(state.Result != null, $"{trial.Id}: {steps} 手で決着が付かなかった");
        }
    }
}
