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
    public void 育てた分は得意へ自動で乗る()
    {
        var c = Make("x", 10, 10, 10, 10, StatKey.Spd, StatKey.Hp);
        Creatures.Grow(c, 4);
        Assert.Equal(4, c.Trained[StatKey.Spd]);
        Assert.Equal(0, c.Trained[StatKey.Atk]);
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
        var raw = Fusion.PreviewBirthLevel(Make("a", 20, 14, 10, 6), Make("b", 18, 16, 8, 8));
        var grown = Fusion.PreviewBirthLevel(
            Make("a", 20, 14, 10, 6, StatKey.Atk, StatKey.Def, earned: Levels.GrowMax),
            Make("b", 18, 16, 8, 8, StatKey.Atk, StatKey.Hp, earned: Levels.GrowMax));
        Assert.True(grown > raw, $"育てても増えていない（{raw} → {grown}）");
        Assert.True(rng.Int(0, 2) >= 0);   // rng を使わない検査であることの明示
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

    // ── 合成 ────────────────────────────────────────

    [Fact]
    public void 合成は食わせた個体を失い育つ()
    {
        var game = Games.NewGame(777);
        var ids = new List<string>();
        foreach (var c in game.Storage.Creatures) ids.Add(c.Id);
        var eater = Games.CreatureById(game, ids[0]);
        int before = eater.Earned;

        int gained = Games.FeedCreature(game, ids[0], ids[1]);

        Assert.True(gained > 0);
        Assert.Equal(before + gained, eater.Earned);
        Assert.DoesNotContain(game.Storage.Creatures, c => c.Id == ids[1]);
    }

    [Fact]
    public void 上限に達していたら食わせない()
    {
        var game = Games.NewGame(777);
        var ids = new List<string>();
        foreach (var c in game.Storage.Creatures) ids.Add(c.Id);
        Creatures.Grow(Games.CreatureById(game, ids[0]), Levels.GrowMax);

        int before = game.Storage.Creatures.Count;
        Assert.Equal(0, Games.FeedCreature(game, ids[0], ids[1]));
        // ⚠️ 何も起きないのに1体減る、が最悪
        Assert.Equal(before, game.Storage.Creatures.Count);
    }

    [Fact]
    public void 育てた個体ほど燃料として効く()
    {
        var plain = Make("p", 10, 10, 10, 10);
        var grown = Make("g", 10, 10, 10, 10, StatKey.Atk, StatKey.Def, earned: Levels.GrowMax);
        Assert.True(Levels.FeedValueOf(grown) > Levels.FeedValueOf(plain));
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
