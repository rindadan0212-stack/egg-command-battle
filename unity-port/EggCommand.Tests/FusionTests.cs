using System;
using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>育成の骨格。⚠️ 移植元に無い規則なので、較正値ではなく**規則そのもの**を検査する。
///
/// ここが崩れると「最良×最良を繰り返すだけ」に戻る。設計の芯なので厚めに置く。</summary>
public class FusionTests
{
    private static Creature Make(string id, int hp, int atk, int def, int spd,
        StatKey? strong = null, StatKey? weak = null, int earned = 0)
    {
        var c = new Creature(id, "tamaru", new StatBlock(hp, atk, def, spd),
            new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1, strong, weak);
        if (earned > 0) Creatures.Grow(c, earned);
        return c;
    }

    // ── レベル ──────────────────────────────────────

    [Fact]
    public void レベルは素質の合計と育てた分の和()
    {
        var c = Make("x", 10, 8, 6, 4);
        Assert.Equal(28, Levels.BirthOf(c));
        Assert.Equal(28, Levels.Of(c));

        Creatures.Grow(c, 5);
        Assert.Equal(28, Levels.BirthOf(c));   // 生まれつきは動かない
        Assert.Equal(33, Levels.Of(c));
    }

    [Fact]
    public void 育てられる分は全個体で共通()
    {
        var weak = Make("a", 5, 5, 5, 5);
        var strong = Make("b", 30, 20, 20, 10);
        Creatures.Grow(weak, 999);
        Creatures.Grow(strong, 999);
        Assert.Equal(Levels.GrowMax, weak.Earned);
        Assert.Equal(Levels.GrowMax, strong.Earned);
        // ⭐ 上限が共通なので、上へ行くには生まれつきの高い個体が要る
        Assert.True(Levels.MaxOf(strong) > Levels.MaxOf(weak));
    }

    [Fact]
    public void 育てた分は全ステに乗る()
    {
        var c = Make("x", 10, 10, 10, 10, StatKey.Spd, StatKey.Hp);
        Creatures.Grow(c, 4);
        // ⭐ **6本すべてが伸びる。**⚠️ 得意1本だけに乗せていた頃は、
        //    要らないステが得意の個体は育てても救えなかった（2026-08-19 に変更）。
        foreach (var key in Stats.Keys)
            Assert.True(c.Trained[key] > 0, $"{Stats.LabelOf(key)} が伸びていない");
    }

    /// <summary>⭐ **伸びる量は素質の割合。**⚠️ 平らな ＋1 に戻ると、
    /// 1点の価値がステで 22 倍ちがう状態に戻る（2026-08-19 実測）。</summary>
    [Fact]
    public void 育てた分は素質の割合で決まる()
    {
        var c = Make("x", 10, 10, 10, 10);
        Creatures.Grow(c, Levels.GrowMax);
        var born = Creatures.BornStatsOf(c.SpeciesId, c.Wild);
        foreach (var key in Stats.Keys)
        {
            int want = (int)System.Math.Floor(
                (double)born[key] * Creatures.GrowthPermilOf(key) * Levels.GrowMax / 1000.0
                + Creatures.GrowthFlatOf(key) * Levels.GrowMax + 0.5);
            Assert.Equal(want, c.Trained[key]);
        }
    }

    /// <summary>⭐ **素質が高いほど、その分野の伸びも大きい。**
    /// 作者の「素質が高い個体がその分野で有利になっていく」を育成でも通す。</summary>
    [Fact]
    public void 素質が高いほど伸びも大きい()
    {
        var low = Make("low", 0, 0, 0, 0);
        var high = Make("high", 40, 0, 0, 0);
        Creatures.Grow(low, Levels.GrowMax);
        Creatures.Grow(high, Levels.GrowMax);
        Assert.True(high.Trained[StatKey.Hp] > low.Trained[StatKey.Hp],
            $"素質の高い側の伸び {high.Trained[StatKey.Hp]} が "
            + $"低い側 {low.Trained[StatKey.Hp]} を上回っていない");
    }

    /// <summary>⚠️ **弱化命中・弱化耐性だけは平らに伸ばす。**
    /// 通る率は「命中 − 抵抗」という引き算なので、割合で伸ばすと差まで倍になり、
    /// 床25%/天井95% の帯からはみ出して軸が死ぬ（2026-08-19 に分けた）。</summary>
    [Fact]
    public void 弱化の2本は素質によらず同じだけ伸びる()
    {
        var low = Make("low", 0, 0, 0, 0);
        var high = Make("high", 0, 0, 0, 0, StatKey.Hp, StatKey.Atk);
        Creatures.Grow(low, Levels.GrowMax);
        Creatures.Grow(high, Levels.GrowMax);
        // ⚠️ 平らな伸びは**実値の単位**（野生レベル1点ぶん ＝ Stats.Scale）
        int want = Levels.GrowMax * Creatures.GrowthFlatOf(StatKey.Acc);
        Assert.Equal(want, low.Trained[StatKey.Acc]);
        Assert.Equal(want, high.Trained[StatKey.Acc]);
        Assert.Equal(want, low.Trained[StatKey.Res]);
    }

    /// <summary>⭐ 得意・不得意は**育てた分にも掛かる**（実値に最後に乗るので自動）。
    /// ⚠️ 掛からないと「素質が高い個体がその分野で有利になる」が育成で崩れる。</summary>
    [Fact]
    public void 育てた分にも得意と不得意が乗る()
    {
        var plain = Make("p", 20, 20, 20, 20);
        var slanted = Make("s", 20, 20, 20, 20, StatKey.Atk, StatKey.Def);
        int gainedPlain = Creatures.StatsOf(plain)[StatKey.Atk];
        int gainedSlanted = Creatures.StatsOf(slanted)[StatKey.Atk];
        Creatures.Grow(plain, Levels.GrowMax);
        Creatures.Grow(slanted, Levels.GrowMax);
        int plainUp = Creatures.StatsOf(plain)[StatKey.Atk] - gainedPlain;
        int slantedUp = Creatures.StatsOf(slanted)[StatKey.Atk] - gainedSlanted;
        Assert.True(slantedUp > plainUp,
            $"得意の伸び {slantedUp} が並 {plainUp} を上回っていない");
    }

    [Fact]
    public void 得意は上がり不得意は下がる()
    {
        var plain = Make("p", 20, 20, 20, 20);
        var slanted = Make("s", 20, 20, 20, 20, StatKey.Atk, StatKey.Def);
        var p = Creatures.StatsOf(plain);
        var s = Creatures.StatsOf(slanted);

        Assert.True(s[StatKey.Atk] > p[StatKey.Atk]);
        Assert.True(s[StatKey.Def] < p[StatKey.Def]);
        Assert.Equal(p[StatKey.Hp], s[StatKey.Hp]);   // 関係ないステは動かない
    }

    [Fact]
    public void 得意を持たない個体は移植元と同じ()
    {
        var c = Make("x", 20, 18, 16, 14);
        Assert.Equal(Stats.ActualStats(SpeciesTable.ById("tamaru").Base, c.Wild, c.Trained),
            Creatures.StatsOf(c));
    }

    // ── 配合 ────────────────────────────────────────

    [Fact]
    public void 配合は素質の合計を素では増やさない()
    {
        var rng = new Rng(4);
        for (int i = 0; i < 200; i++)
        {
            var a = Make("a", 20, 14, 10, 6);
            var b = Make("b", 18, 16, 8, 8);
            int parents = (Stats.TotalOf(a.Wild) + Stats.TotalOf(b.Wild)) / 2;
            var outcome = Fusion.Fuse(rng, a, b, i);
            int child = Stats.TotalOf(outcome.Egg.Wild);
            // 変異だけが上乗せしてよい
            Assert.True(child <= parents + outcome.Mutations * Breeding.MutationStep,
                $"{child} > {parents} (+変異 {outcome.Mutations})");
        }
    }

    [Fact]
    public void 育てた分は子の生まれつきに変わる()
    {
        var rng = new Rng(4);
        var before = rng.State;
        var raw = Fusion.PreviewBirthLevel(Make("a", 20, 14, 10, 6), Make("b", 18, 16, 8, 8));
        var grown = Fusion.PreviewBirthLevel(
            Make("a", 20, 14, 10, 6, StatKey.Atk, StatKey.Def, earned: Levels.GrowMax),
            Make("b", 18, 16, 8, 8, StatKey.Atk, StatKey.Hp, earned: Levels.GrowMax));
        Assert.True(grown > raw, $"育てても増えていない（{raw} → {grown}）");
        // ⚠️ 合成の見積りは乱数を引かない。⭐ 引いていないことを「状態が動いていない」で示す
        //    （`rng.Int(0,2) >= 0` は必ず成立するので、何も守っていなかった）
        Assert.Equal(before, rng.State);
    }

    [Fact]
    public void 配合は両親が共に高いステへ寄る()
    {
        // 両親とも攻撃が高い。⭐ 子は攻撃へ寄るはず
        var a = Make("a", 10, 30, 6, 4);
        var b = Make("b", 8, 28, 8, 6);
        var rng = new Rng(11);
        int sharper = 0;
        for (int i = 0; i < 50; i++)
        {
            var child = Fusion.Fuse(rng, Make("a", 10, 30, 6, 4), Make("b", 8, 28, 8, 6), i).Egg.Wild;
            double parentShare = (a.Wild[StatKey.Atk] + b.Wild[StatKey.Atk])
                / (double)(Stats.TotalOf(a.Wild) + Stats.TotalOf(b.Wild));
            double childShare = child[StatKey.Atk] / (double)Stats.TotalOf(child);
            if (childShare > parentShare) sharper++;
        }
        Assert.True(sharper >= 45, $"尖ったのは 50 回中 {sharper} 回");
    }

    [Fact]
    public void 配合は両親を失う()
    {
        var game = Games.NewGame(2026_08_16);
        var ids = new List<string>();
        foreach (var c in game.Storage.Creatures) ids.Add(c.Id);
        int before = game.Storage.Creatures.Count;

        Games.FusePair(game, ids[0], ids[1]);

        Assert.Equal(before - 2, game.Storage.Creatures.Count);
        Assert.DoesNotContain(game.Storage.Creatures, c => c.Id == ids[0]);
        Assert.DoesNotContain(game.Storage.Creatures, c => c.Id == ids[1]);
        Assert.Single(game.Eggs);
    }

    [Fact]
    public void 同じ個体どうしは配合できない()
    {
        var a = Make("a", 10, 10, 10, 10);
        Assert.Throws<InvalidOperationException>(() => Fusion.Fuse(new Rng(1), a, a, 1));
    }

    // ── 分解 ────────────────────────────────────────

    [Fact]
    public void 分解は個体を失いEXPになる()
    {
        var game = Games.NewGame(777);
        var ids = new List<string>();
        foreach (var c in game.Storage.Creatures) ids.Add(c.Id);
        int before = game.Idle.Exp;
        int want = Levels.DissolveExpOf(Games.CreatureById(game, ids[1]));

        int got = Games.Dissolve(game, new List<string> { ids[1] });

        Assert.Equal(want, got);
        Assert.Equal(before + want, game.Idle.Exp);
        Assert.DoesNotContain(game.Storage.Creatures, c => c.Id == ids[1]);
    }

    /// <summary>⭐ **まとめて分解できる。**⚠️ 同じ id が二度来ても壊れない
    /// （画面の選び直しで重複が入りうる）。</summary>
    [Fact]
    public void 分解はまとめてできる()
    {
        var game = Games.NewGame(777);
        var ids = new List<string>();
        foreach (var c in game.Storage.Creatures) ids.Add(c.Id);
        int want = Levels.DissolveExpOf(Games.CreatureById(game, ids[0]))
            + Levels.DissolveExpOf(Games.CreatureById(game, ids[1]));

        int got = Games.Dissolve(game, new List<string> { ids[0], ids[1], ids[1] });

        Assert.Equal(want, got);
        Assert.Equal(ids.Count - 2, game.Storage.Creatures.Count);
    }

    [Fact]
    public void 育てた個体ほど分解で返る()
    {
        var plain = Make("p", 10, 10, 10, 10);
        var grown = Make("g", 10, 10, 10, 10, StatKey.Atk, StatKey.Def, earned: Levels.GrowMax);
        Assert.True(Levels.DissolveExpOf(grown) > Levels.DissolveExpOf(plain));
    }

    /// <summary>⭐ **値段は「何レベルになるか」で決まる。**（作者の指示 2026-08-19）
    ///
    /// ⚠️ 育てた回数で決めていた頃は、Lv1 の個体が Lv20 になるのと
    /// Lv80 の個体が Lv100 になるのが同じ値段だった。</summary>
    [Fact]
    public void 必要EXPは到達レベルで決まる()
    {
        int previous = 0;
        for (int level = 0; level < 200; level++)
        {
            int cost = Levels.ExpToNextAt(level);
            Assert.True(cost > previous, $"Lv{level} の値段 {cost} が Lv{level - 1} 以下");
            previous = cost;
        }

        // ⭐ 生まれつきが高いほど、同じ 20レベルぶんが高くつく
        int low = Levels.ExpBetween(1, 21);
        int high = Levels.ExpBetween(80, 100);
        Assert.True(high > low * 5, $"Lv1→21 が {low} / Lv80→100 が {high}");

        // ⭐ まとめた和と1段ずつの和が食い違わない（第2の出所を作らない）
        int sum = 0;
        for (int level = 80; level < 100; level++) sum += Levels.ExpToNextAt(level);
        Assert.Equal(sum, high);
    }

    /// <summary>⚠️ 上限に達した個体は、いくら EXP があっても上がらない。</summary>
    [Fact]
    public void 上限に達したら次の値段は0()
    {
        var maxed = Make("m", 10, 10, 10, 10, earned: Levels.GrowMax);
        Assert.Equal(0, Levels.ExpToNext(maxed));
        Assert.Equal(0, Levels.LevelsFor(maxed, 999_999));
    }

    /// <summary>⭐ 分解で返るのは「注いだ EXP ＋ 生まれつきぶん」。</summary>
    [Fact]
    public void 分解で返るのは注いだEXPと生まれつき()
    {
        var grown = Make("g", 10, 10, 10, 10, earned: Levels.GrowMax);
        int invested = Levels.ExpBetween(Levels.BirthOf(grown), Levels.Of(grown));
        Assert.Equal(invested, Levels.InvestedExpOf(grown));
        Assert.Equal(
            invested + Levels.BirthOf(grown) * Levels.BirthExp / Levels.BirthDivisor,
            Levels.DissolveExpOf(grown));
    }

    // ── 経済 ────────────────────────────────────────

    [Fact]
    public void 配合を繰り返すと手持ちが尽きる()
    {
        // ⭐ 出口があることの検査。無限に配合できてはいけない
        var game = Games.NewGame(31);
        int fused = 0;
        while (game.Storage.Creatures.Count >= 2 && fused < 50)
        {
            var ids = new List<string>();
            foreach (var c in game.Storage.Creatures) ids.Add(c.Id);
            Games.FusePair(game, ids[0], ids[1]);
            fused++;
        }
        Assert.True(game.Storage.Creatures.Count < 2, "配合し続けられてしまう");
    }
}
