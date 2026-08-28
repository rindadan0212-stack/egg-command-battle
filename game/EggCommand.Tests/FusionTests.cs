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
        // 🔴 **点を得ただけでは1つも伸びない**（2026-08-26・ARK式の自由配分）。
        //    ⚠️ 2026-08-19〜08-26 は「6本すべてが自動で伸びる」だった。
        //    ⭐ いまは振り先を選ぶのが遊びなので、得た点は未使用のまま置く。
        Assert.Equal(4, Creatures.UnspentOf(c));
        foreach (var key in Stats.Keys)
            Assert.Equal(0, c.Trained[key]);

        // ⭐ 振った先**だけ**が伸びる
        Creatures.Spend(c, StatKey.Atk, 4);
        Assert.Equal(0, Creatures.UnspentOf(c));
        Assert.True(c.Trained[StatKey.Atk] > 0, "振った攻撃力が伸びていない");
        Assert.Equal(0, c.Trained[StatKey.Def]);
    }

    /// <summary>⭐ **伸びる量は素質の割合。**⚠️ 平らな ＋1 に戻ると、
    /// 1点の価値がステで 22 倍ちがう状態に戻る（2026-08-19 実測）。</summary>
    [Fact]
    public void 育てた分は素質の割合で決まる()
    {
        // ⚠️ 6本すべてに同じ点を振って、伸び方の式だけを見る（合計上限に触れない量で）
        const int Each = 5;
        var born = Creatures.BornStatsOf("tamaru", new StatBlock(10, 10, 10, 10));
        foreach (var key in Stats.Keys)
        {
            var c = Make("x", 10, 10, 10, 10);
            Creatures.Grow(c, Each);
            Creatures.Spend(c, key, Each);
            int want = (int)System.Math.Floor(
                (double)born[key] * Creatures.GrowthPermilOf(key) * Each / 1000.0
                + Creatures.GrowthFlatOf(key) * Each + 0.5);
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
        // ⭐ **同じ点を同じステへ**振って比べる（2026-08-26・自由配分になったので明示する）
        Creatures.Spend(low, StatKey.Hp, Levels.GrowMax);
        Creatures.Spend(high, StatKey.Hp, Levels.GrowMax);
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
        // ⚠️ 命中と耐性の両方に振るので、上限の半分ずつにする
        const int Half = 10;
        Creatures.Spend(low, StatKey.Acc, Half);
        Creatures.Spend(low, StatKey.Res, Half);
        Creatures.Spend(high, StatKey.Acc, Half);
        // ⚠️ 平らな伸びは**弱化2本の単位**（`Stats.DebuffScale` の目盛り・1点＝+1）
        int want = Half * Creatures.GrowthFlatOf(StatKey.Acc);
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
        Creatures.Spend(plain, StatKey.Atk, Levels.GrowMax);
        Creatures.Spend(slanted, StatKey.Atk, Levels.GrowMax);
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
        // ⚠️ **育て切った親で見る**（2026-08-21）。尖りは「育てた分」に乗るようになったので、
        //    育てていない親どうしだと伸びしろが 0 ＝ 平均されるだけになる。
        //    ⭐ これは仕様（育てないと何も起きない）── 検査の入力を本番の使い方へ合わせた。
        var a = Make("a", 10, 30, 6, 4, earned: Creatures.TrainMax);
        var b = Make("b", 8, 28, 8, 6, earned: Creatures.TrainMax);
        var rng = new Rng(11);
        int sharper = 0;
        for (int i = 0; i < 50; i++)
        {
            var child = Fusion.Fuse(rng,
                Make("a", 10, 30, 6, 4, earned: Creatures.TrainMax),
                Make("b", 8, 28, 8, 6, earned: Creatures.TrainMax), i).Egg.Wild;
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

    // ── 上限は世代が押し上げる（2026-08-21・作者の指示）──────

    /// <summary>⭐ **1代進むごとに上限が上がる。**⚠️ 野生（1代目）は素の上限。</summary>
    [Fact]
    public void 上限は世代で上がる()
    {
        Assert.Equal(Stats.WildStatMax, Stats.WildStatMaxFor(1));
        Assert.Equal(Stats.WildStatMax + 1, Stats.WildStatMaxFor(2));
        Assert.Equal(Stats.WildStatMax + Stats.GenerationCapSteps,
            Stats.WildStatMaxFor(1 + Stats.GenerationCapSteps));
        // ⚠️ 天井の先は伸びない
        Assert.Equal(Stats.WildStatMaxFor(1 + Stats.GenerationCapSteps),
            Stats.WildStatMaxFor(500));
        // ⭐ 合計は常に3倍
        for (int gen = 1; gen <= 25; gen++)
            Assert.Equal(Stats.WildStatMaxFor(gen) * 3, Stats.WildTotalMaxFor(gen));
    }

    /// <summary>⭐ **作者の指示（2026-08-21）**:
    /// 「弱い個体の配合では上限は上がるが実値は弱いまま」。
    ///
    /// ⚠️ ここが崩れると、配合を空打ちするだけで強くなる ── 育てる意味が消える。</summary>
    [Fact]
    public void 育てずに配合しても実値は増えない()
    {
        var rng = new Rng(2026_08_21).Stream("cap-test");
        int serial = 0;
        var one = Make("a0", 20, 20, 20, 0);
        int startTotal = Stats.TotalOf(one.Wild);

        // ⚠️ 変異は「その子だけ +2」の上振れなので、出たぶんは数に入れて許す
        //    （それ以外に増える道が無いことを見たい）
        int fromLuck = 0;
        for (int i = 0; i < 12; i++)
        {
            var mate = Make($"b{i}", 20, 20, 20, 0);
            var made = Fusion.Fuse(rng, one, mate, ++serial);
            fromLuck += made.Mutations * Breeding.MutationStep;
            one = Nests.Hatch(rng, egg: made.Egg, id: $"c{i}");
        }

        // ⭐ 枠は広がっている
        Assert.True(Stats.WildTotalMaxFor(one.Generation) > Stats.WildTotalMax,
            "世代を重ねても上限が上がっていない");
        // ⚠️ **配合そのものでは増えない**（育てていないので）
        Assert.True(Stats.TotalOf(one.Wild) <= startTotal + fromLuck,
            $"育てずに配合したのに素質が {startTotal} → {Stats.TotalOf(one.Wild)}"
            + $"（変異ぶんの上振れは {fromLuck} まで）");
    }

    /// <summary>⭐ **育てれば中身が増える。**⚠️ 増える量は育てた分の
    /// <see cref="Fusion.Carry"/>（両親ぶん）。</summary>
    [Fact]
    public void 育ててから配合すると実値が増える()
    {
        var rng = new Rng(99).Stream("carry-test");
        var a = Make("a", 20, 20, 20, 0, earned: Creatures.TrainMax);
        var b = Make("b", 20, 20, 20, 0, earned: Creatures.TrainMax);
        int before = Stats.TotalOf(a.Wild);

        var egg = Fusion.Fuse(rng, a, b, 1).Egg;

        int want = (int)System.Math.Floor(Creatures.TrainMax * 2 * Fusion.Carry + 0.5);
        Assert.Equal(before + want, Stats.TotalOf(egg.Wild));
    }

    /// <summary>⭐ **同じ形どうしを掛けると、その形のまま濃くなる**（2026-08-21・作者の指摘）。
    ///
    /// ⚠️ 直す前は「決まった合計を (a+b)^1.6 の重みで**配り直す**」形だったので、
    /// 同じ形の親どうしでも上位2本が3本目を食い、代を重ねるほど形が壊れた
    /// （[40 40 30] → [46 46 28] → … → [60 60 0]）。
    /// ⚠️ 「尖った個体を作りたいのに、同じ形どうしを掛けてはいけない」というあべこべだった。</summary>
    [Fact]
    public void 同じ形どうしを掛けると形が保たれる()
    {
        var rng = new Rng(2026_08_21).Stream("shape");
        int serial = 0;
        // ⭐ HP・攻撃・防御の3本だけに寄せた形
        var shape = new[] { StatKey.Hp, StatKey.Atk, StatKey.Def };
        var one = Make("s0", 20, 20, 20, 0, earned: Creatures.TrainMax);

        for (int i = 0; i < 20; i++)
        {
            var mate = Made($"m{i}", one.Wild, Creatures.TrainMax);
            var egg = Fusion.Fuse(rng, one, mate, ++serial).Egg;
            // ⚠️ **形が崩れていないこと**を毎代見る（最後だけ見ると、途中の崩れを見逃す）
            foreach (var key in Stats.Keys)
            {
                bool inShape = System.Array.IndexOf(shape, key) >= 0;
                if (inShape) continue;
                Assert.True(egg.Wild[key] <= 2,
                    $"{i}代目: 形に無い {Stats.LabelOf(key)} が {egg.Wild[key]} になった");
            }
            one = Made($"c{i}", egg.Wild, Creatures.TrainMax, egg.Generation);
        }

        // ⭐ 3本とも上限まで濃くなっている（＝枠を埋め切れる）
        foreach (var key in shape)
        {
            Assert.True(one.Wild[key] >= Stats.WildStatMax,
                $"{Stats.LabelOf(key)} が {one.Wild[key]} までしか伸びていない");
        }
    }

    /// <summary>⭐ **2本に尖らせた血統も、その2本のまま濃くなる。**
    /// ⚠️ 3本目が勝手に生えない（生えると「尖らせた」ことにならない）。</summary>
    [Fact]
    public void 二本に尖らせた形も保たれる()
    {
        var rng = new Rng(7).Stream("shape2");
        int serial = 0;
        var one = Made("t0", new StatBlock(0, 30, 0, 30, 0, 0), Creatures.TrainMax);

        for (int i = 0; i < 20; i++)
        {
            var mate = Made($"n{i}", one.Wild, Creatures.TrainMax);
            var egg = Fusion.Fuse(rng, one, mate, ++serial).Egg;
            one = Made($"d{i}", egg.Wild, Creatures.TrainMax, egg.Generation);
        }

        Assert.True(one.Wild[StatKey.Atk] >= Stats.WildStatMax, "攻撃が伸びていない");
        Assert.True(one.Wild[StatKey.Spd] >= Stats.WildStatMax, "スピードが伸びていない");
        Assert.True(one.Wild[StatKey.Hp] <= 2, $"HP が {one.Wild[StatKey.Hp]} に生えた");
        Assert.True(one.Wild[StatKey.Def] <= 2, $"防御が {one.Wild[StatKey.Def]} に生えた");
    }

    /// <summary>素質と育成を指定した個体。⚠️ 配合の入力を組むためだけの道具。</summary>
    private static Creature Made(string id, StatBlock wild, int earned, int generation = 1)
    {
        var c = new Creature(id, "tamaru", wild, new StatBlock(0, 0, 0, 0), 0, 0,
            null, null, 0, null, null, generation);
        if (earned > 0) Creatures.Grow(c, earned);
        return c;
    }

    /// <summary>⚠️ **変異はもう上限を押し上げない**（2026-08-21）。
    /// ⭐ 変異カウンタが高くても、世代が浅ければ上限は浅いまま。</summary>
    [Fact]
    public void 変異は上限を押し上げない()
    {
        var wide = new StatBlock(60, 60, 60, 60, 60, 60);
        // 1代目・変異カウンタは関係しない
        Assert.Equal(Stats.WildTotalMax, Stats.TotalOf(Stats.ApplyTotalCap(wide, 1)));
        // 21代目なら天井まで入る
        Assert.Equal(Stats.WildTotalMaxFor(21), Stats.TotalOf(Stats.ApplyTotalCap(wide, 21)));
    }

    // ── 偏り4本（2026-08-21・作者の指示）─────────────────

    /// <summary>⭐ **孵ると4本とも別のステに乗る。**
    /// ⚠️ 重なった組は <see cref="Creatures.Slanted(StatBlock, Creature)"/> が両方とも捨てるので、
    /// 重なった個体だけ軸が1本消える（画面には▲が出たまま）。</summary>
    [Fact]
    public void 孵った個体の偏りは四本とも別のステ()
    {
        var game = Games.NewGame(4321);
        var nest = Nests.ById("thicket-fang");
        for (int i = 0; i < 200; i++)
        {
            var egg = Nests.MakeEgg(game.RngEgg, nest, EggOrigin.Defeated, ++game.Serial);
            StatKey best, strong, weak, worst;
            Nests.RollSlant(game.RngSlant, out best, out strong, out weak, out worst);
            var born = Nests.Hatch(game.RngHatch, egg, $"h{i}", strong, weak, best, worst);

            var keys = new HashSet<StatKey?> { born.Best, born.Strong, born.Weak, born.Worst };
            Assert.Equal(4, keys.Count);
        }
    }

    /// <summary>⚠️ **配合でも重ねない。**親から継ぐと、素で引くより重なりやすい
    /// （両親の同じ欄が同じステを指していることがある）。</summary>
    [Fact]
    public void 配合の子の偏りも四本とも別のステ()
    {
        var rng = new Rng(99).Stream("slant-fuse");
        int serial = 0;
        for (int i = 0; i < 200; i++)
        {
            var a = Make($"a{i}", 20, 20, 20, 20, StatKey.Atk, StatKey.Def);
            var b = Make($"b{i}", 20, 20, 20, 20, StatKey.Atk, StatKey.Def);
            var egg = Fusion.Fuse(rng, a, b, ++serial).Egg;

            var keys = new HashSet<StatKey?> { egg.Best, egg.Strong, egg.Weak, egg.Worst };
            Assert.Equal(4, keys.Count);
        }
    }

    /// <summary>⭐ **大得意は +30%・得意は +15%**（<see cref="Creatures.GreatSlant"/>）。
    /// ⚠️ 掛ける順で答えが変わらないこと（別のステに乗るので、掛け算が交わらない）。</summary>
    [Fact]
    public void 大得意は得意のちょうど二倍動く()
    {
        var flat = new StatBlock(1000, 1000, 1000, 1000, 1000, 1000);
        var made = Creatures.Slanted(flat, StatKey.Atk, StatKey.Def, StatKey.Hp, StatKey.Spd);

        Assert.Equal(1300, made[StatKey.Hp]);    // 大得意 +30%
        Assert.Equal(1150, made[StatKey.Atk]);   // 得意 +15%
        Assert.Equal(850, made[StatKey.Def]);    // 不得意 −15%
        Assert.Equal(700, made[StatKey.Spd]);    // 大不得意 −30%
        Assert.Equal(1000, made[StatKey.Acc]);   // 何も付かない
        Assert.Equal(1000, made[StatKey.Res]);
    }

    /// <summary>⚠️ **重なった組は両方とも捨てる。**⭐ 片方だけ効かせると、
    /// 画面の▲と実際の数が食い違う（どちらが効いたのか読めない）。</summary>
    [Fact]
    public void 同じステに重なった偏りは両方とも効かない()
    {
        var flat = new StatBlock(1000, 1000, 1000, 1000, 1000, 1000);
        // 大得意と大不得意が同じ → その組だけ捨て、得意/不得意は生きる
        var made = Creatures.Slanted(flat, StatKey.Atk, StatKey.Def, StatKey.Hp, StatKey.Hp);
        Assert.Equal(1000, made[StatKey.Hp]);
        Assert.Equal(1150, made[StatKey.Atk]);
        Assert.Equal(850, made[StatKey.Def]);
    }

    /// <summary>⭐ **2本のままの古い個体も、そのまま読める。**
    /// ⚠️ 大得意を持たない個体（null）に ±30% を掛けない。</summary>
    [Fact]
    public void 大得意を持たない個体は今までどおり動く()
    {
        var flat = new StatBlock(1000, 1000, 1000, 1000, 1000, 1000);
        var made = Creatures.Slanted(flat, StatKey.Atk, StatKey.Def);
        Assert.Equal(1150, made[StatKey.Atk]);
        Assert.Equal(850, made[StatKey.Def]);
        Assert.Equal(1000, made[StatKey.Hp]);
    }

    /// <summary>⚠️ 保存して読み直しても4本とも残ること。</summary>
    [Fact]
    public void 偏り四本は保存して読み直しても残る()
    {
        var game = Games.NewGame(606);
        var save = Snapshots.Save(game);
        var back = Snapshots.Load(save);
        Assert.NotNull(back);

        for (int i = 0; i < game.Storage.Creatures.Count; i++)
        {
            var was = game.Storage.Creatures[i];
            var now = back!.Storage.Creatures[i];
            Assert.Equal(was.Best, now.Best);
            Assert.Equal(was.Strong, now.Strong);
            Assert.Equal(was.Weak, now.Weak);
            Assert.Equal(was.Worst, now.Worst);
        }
    }

    /// <summary>⚠️ **大得意より前の保存も読める。**⭐ 欄が無ければ「持たない」（-1）。</summary>
    [Fact]
    public void 大得意を知らない古いセーブも読める()
    {
        var game = Games.NewGame(607);
        var save = Snapshots.Save(game);
        foreach (var c in save.Creatures) { c.Best = -1; c.Worst = -1; }

        var back = Snapshots.Load(save);
        Assert.NotNull(back);
        Assert.Null(back!.Storage.Creatures[0].Best);
        Assert.Null(back.Storage.Creatures[0].Worst);
        // ⭐ 得意・不得意はそのまま生きている
        Assert.NotNull(back.Storage.Creatures[0].Strong);
    }
}
