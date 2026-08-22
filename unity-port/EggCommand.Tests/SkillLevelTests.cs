using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>スキルレベル。⭐ **孵化前の卵の唯一の出口。**
///
/// ⚠️ この出口が「★＝強さ」を成立させている。無くなると
/// 「★5から順に孵すだけ」になり、正典が避けた「待てば良いだけ」が戻る。
///
/// ⚠️ 移植元に無い機能なので goldens では守れない。ここが唯一の見張り。</summary>
public class SkillLevelTests
{
    private static Creature Make(string id, string? skill2 = "attack", string? skill3 = null) =>
        new Creature(id, "tamaru", new StatBlock(20, 20, 20, 20),
            new StatBlock(0, 0, 0, 0), 0, 0, skill2, skill3, 0, null, null, 1);

    // ── 値段と卵の対応 ──────────────────────────────

    [Fact]
    public void 値段と卵のポイントが食い違っていない()
    {
        SkillCosts.Audit();
    }

    /// <summary>⭐ **★N の卵1個で、ちょうど Lv(N−1) → Lv N。**説明が1行で済む形。</summary>
    [Fact]
    public void 星N個の卵ひとつでちょうど一段上がる()
    {
        for (int rarity = 2; rarity <= Rarities.Max; rarity++)
        {
            int before = SkillCosts.TotalFor(rarity - 1);
            Assert.Equal(rarity - 1, SkillCosts.LevelOf(before));

            int after = before + Rarities.PointsOf(rarity);
            Assert.Equal(rarity, SkillCosts.LevelOf(after));
        }
    }

    /// <summary>⚠️ **直線にしない。**★1が★5の 1/5 でしかないと、
    /// 低い★を延々入れるほうが得になり「時間さえかければ埋まる」形に戻る。</summary>
    [Fact]
    public void 低い星で高い段を埋めるのは割に合わない()
    {
        int lastStep = SkillCosts.CostOf(Skills.MaxLevel - 1);
        int byOnes = lastStep / Rarities.PointsOf(1);
        Assert.True(byOnes >= 27, $"★1 で最後の段を埋めるのに {byOnes} 個しか要らない");

        // ⭐ 一番上の★なら1個でよい
        Assert.Equal(1, lastStep / Rarities.PointsOf(Skills.MaxLevel));
    }

    [Fact]
    public void 上限を超えて溜まらない()
    {
        int max = SkillCosts.TotalFor(Skills.MaxLevel);
        Assert.True(SkillCosts.IsMaxed(max));
        Assert.Equal(Skills.MaxLevel, SkillCosts.LevelOf(max * 10));
        Assert.Equal(0, SkillCosts.ToNext(max));
    }

    // ── 出口 ────────────────────────────────────────

    [Fact]
    public void 卵を食わせるとポイントが入り卵は減る()
    {
        var game = Games.NewGame(2026_08_17);
        var eater = game.Storage.Creatures[0];
        var egg = Games.TakeEgg(game, Nests.ById("thicket-fang"), EggOrigin.Defeated);
        int eggs = game.Eggs.Count;

        int gained = Games.FeedEggToSkill(game, eater.Id, 1, egg.Id);

        Assert.Equal(Rarities.PointsOf(egg.Rarity), gained);
        Assert.Equal(gained, eater.SkillPoints[1]);
        Assert.Equal(eggs - 1, game.Eggs.Count);
    }

    /// <summary>⭐ **選んだぶんをまとめて注ぐ**（2026-08-21・作者の指示）。
    /// ⚠️ 直す前は1個押すごとに入っていたので、10個入れるには10回押すことになり、
    /// そのたびに裏でレベルが上がっていた。</summary>
    [Fact]
    public void 卵をまとめて食わせると合計が入る()
    {
        var game = Games.NewGame(2026_08_21);
        var eater = game.Storage.Creatures[0];
        var nest = Nests.ById("thicket-fang");
        var ids = new List<string>();
        int want = 0;
        for (int i = 0; i < 5; i++)
        {
            var egg = Games.TakeEgg(game, nest, EggOrigin.Defeated);
            ids.Add(egg.Id);
            want += Rarities.PointsOf(egg.Rarity);
        }
        int eggs = game.Eggs.Count;

        int gained = Games.FeedEggsToSkill(game, eater.Id, 1, ids);

        Assert.Equal(want, gained);
        Assert.Equal(want, eater.SkillPoints[1]);
        Assert.Equal(eggs - ids.Count, game.Eggs.Count);
    }

    /// <summary>⚠️ **上限を超える卵はまとめてでも受け取らない。**
    /// ⭐ 入る順に入れて、入らなくなったらそこで止まる ── 卵は棚に残る
    /// （丸めて受け取ると、2時間待った★5 が黙って蒸発する）。</summary>
    [Fact]
    public void まとめて注いでも上限を超える卵は残る()
    {
        var game = Games.NewGame(2026_08_22);
        var eater = game.Storage.Creatures[0];
        var nest = Nests.ById("thicket-fang");
        var ids = new List<string>();
        for (int i = 0; i < 3; i++) ids.Add(Games.TakeEgg(game, nest, EggOrigin.Defeated).Id);

        // ⭐ 上限の1つ手前まで埋めておく（どの卵も入らない状態にする）
        eater.SkillPoints[1] = SkillCosts.TotalFor(Skills.MaxLevel) - 1;
        int eggs = game.Eggs.Count;

        Assert.Equal(0, Games.FeedEggsToSkill(game, eater.Id, 1, ids));
        Assert.Equal(eggs, game.Eggs.Count);
    }

    /// <summary>⚠️ 温め始めた卵は取り上げない。待った時間が黙って消える。</summary>
    [Fact]
    public void 孵化器の卵は食わせられない()
    {
        var game = Games.NewGame(5);
        var eater = game.Storage.Creatures[0];
        var egg = Games.TakeEgg(game, Nests.ById("thicket-fang"), EggOrigin.Defeated);
        Hatchery.Begin(game, egg.Id, 1000);

        Assert.Throws<System.ArgumentException>(
            () => Games.FeedEggToSkill(game, eater.Id, 1, egg.Id));
    }

    /// <summary>⚠️ 押したのに何も起きず卵だけ減る、を作らない。</summary>
    [Fact]
    public void 上限の枠と空き枠には食わせない()
    {
        var game = Games.NewGame(7);
        var eater = game.Storage.Creatures[0];
        var egg = Games.TakeEgg(game, Nests.ById("thicket-fang"), EggOrigin.Defeated);
        int eggs = game.Eggs.Count;

        // 枠2 は空（NewGame の個体は枠2・3 がガチャ次第なので、空きを作って試す）
        var empty = Make("empty", skill2: null, skill3: null);
        game.Storage = Storages.Accept(game.Storage, empty);
        Assert.Equal(0, Games.FeedEggToSkill(game, empty.Id, 2, egg.Id));
        Assert.Equal(eggs, game.Eggs.Count);          // ⭐ 卵は減っていない

        // 上限に達している枠
        eater.SkillPoints[1] = SkillCosts.TotalFor(Skills.MaxLevel);
        Assert.Equal(0, Games.FeedEggToSkill(game, eater.Id, 1, egg.Id));
        Assert.Equal(eggs, game.Eggs.Count);
    }

    [Fact]
    public void スキルポイントは保存して読み直しても残る()
    {
        var game = Games.NewGame(11);
        var eater = game.Storage.Creatures[0];
        eater.SkillPoints[0] = 12;
        eater.SkillPoints[1] = 3;

        var back = Snapshots.Load(Snapshots.Save(game));
        Assert.NotNull(back);
        var same = Games.CreatureById(back!, eater.Id);
        Assert.Equal(12, same.SkillPoints[0]);
        Assert.Equal(3, same.SkillPoints[1]);
        Assert.Equal(SkillCosts.LevelOf(12), Creatures.SkillLevelOf(same, 0));
    }

    /// <summary>⚠️ スキルレベルより前の保存も読めること（ポイントが空）。</summary>
    [Fact]
    public void スキルレベルを知らない古い保存も読める()
    {
        var game = Games.NewGame(13);
        var save = Snapshots.Save(game);
        foreach (var c in save.Creatures) c.SkillPoints.Clear();

        var back = Snapshots.Load(save);
        Assert.NotNull(back);
        Assert.Equal(1, Creatures.SkillLevelOf(back!.Storage.Creatures[0], 0));
    }

    // ── 成長表 ──────────────────────────────────────

    /// <summary>⚠️ **上げても何も起きない段**が1つも無いこと。
    /// ⭐ これが無いと「Lv3 にしたのに何も変わらない」が黙って通る。</summary>
    [Fact]
    public void 死んでいる成長が一つも無い()
    {
        Skills.Audit();
        foreach (var skill in Skills.All)
        {
            var growth = Skills.GrowthOf(skill);
            Assert.Equal(Skills.MaxLevel - 1, growth.Count);
        }
    }

    /// <summary>⭐ 筆頭の約束。Lv1 なら上乗せは全部 0 ＝ **1ビットも変わらない**。</summary>
    [Fact]
    public void レベル1では何も乗らない()
    {
        foreach (var skill in Skills.All)
        {
            Assert.True(Skills.BoostOf(skill, 1).IsNone, $"{skill.Id}: Lv1 で上乗せがある");
        }
        var plain = Make("plain");
        Assert.Equal(1, Creatures.SkillLevelOf(plain, 1));
        Assert.True(Creatures.SkillBoostOf(plain, 1).IsNone);
    }

    /// <summary>⭐ 威力は上がるが、**段位の梯子（小/中/大/特大）は動かない**。
    /// ⚠️ 段位を動かすと「全体は1段下げる」という規則ごと崩れる。</summary>
    [Fact]
    public void 威力は上がるが段位の表は動かない()
    {
        var boost = new SkillBoost { PowerPercent = Skills.GainPowerPercent };
        // ⚠️ 威力は「攻撃力の何倍か」（千分率）。⭐ 中 ＝ 1.5倍
        Assert.Equal(1500, Skills.DamagePowerOf(PowerTier.Medium));
        // ⭐ スキルレベル1段で +10% ── 倍率になっても伸び方は同じ
        Assert.Equal(1650, Skills.BoostedPower(PowerTier.Medium, boost));
    }

    /// <summary>⚠️ 枠1 は CT が元から 0 なので、CT の成長は効かない。</summary>
    [Fact]
    public void 枠1ではCTの成長が効かない()
    {
        var skill = Skills.ById("attack");
        var boost = new SkillBoost { CtCut = 2 };
        Assert.Equal(0, Skills.EffectiveCt(0, skill, boost));
        Assert.Equal(skill.Ct - 2, Skills.EffectiveCt(1, skill, boost));
    }

    [Fact]
    public void CTは0より下がらない()
    {
        var skill = Skills.ById("attack");
        Assert.Equal(0, Skills.EffectiveCt(1, skill, new SkillBoost { CtCut = 99 }));
    }

    // ── 戦闘に届いているか ──────────────────────────

    /// <summary>⭐ レベルを上げたら実際にダメージが増えること。
    /// ⚠️ ここが繋がっていないと、ポイントを注いでも画面の数字が動かない。</summary>
    [Fact]
    public void レベルを上げるとダメージが増える()
    {
        int plain = HitWith(0);
        int grown = HitWith(SkillCosts.TotalFor(Skills.MaxLevel));
        Assert.True(grown > plain, $"Lv1 {plain} → 最大 {grown} と増えていない");
    }

    /// <summary>枠2 の「攻撃」を1回撃ったときに相手が受けたダメージ。</summary>
    private static int HitWith(int points)
    {
        var attacker = Make("a");
        attacker.SkillPoints[1] = points;
        var target = Make("b");
        var state = Battle.CreateBattle(
            new List<Creature> { attacker }, new List<Creature> { target });

        Unit actor = null!, foe = null!;
        foreach (var unit in state.Units)
        {
            if (unit.Side == Side.Ally) actor = unit; else foe = unit;
        }
        Battle.PerformAction(state, actor, 1);
        return foe.MaxHp - foe.Hp;
    }

    // ── 監査で見つかった穴 ──────────────────────────

    /// <summary>⚠️ **上限を超える卵は受け取らない。**
    /// 丸めて受け取ると、上限の1つ手前に★5（81pt）を入れたとき 80pt が黙って消えて、
    /// 画面には「+81」と出る（2時間待った卵が蒸発する）。</summary>
    [Fact]
    public void 上限を超える卵は受け取らない()
    {
        var game = Games.NewGame(2026_08_17);
        var eater = game.Storage.Creatures[0];
        // 次の段まであと1ポイント、という状態にする
        eater.SkillPoints[1] = SkillCosts.TotalFor(Skills.MaxLevel) - 1;

        var egg = Games.TakeEgg(game, Nests.ById("thicket-fang"), EggOrigin.Defeated);
        int eggs = game.Eggs.Count;
        int points = eater.SkillPoints[1];

        int gained = Games.FeedEggToSkill(game, eater.Id, 1, egg.Id);

        if (Rarities.PointsOf(egg.Rarity) > 1)
        {
            Assert.Equal(0, gained);                      // ⭐ 受け取らない
            Assert.Equal(eggs, game.Eggs.Count);          // ⭐ 卵も減らない
            Assert.Equal(points, eater.SkillPoints[1]);
        }
    }

    /// <summary>⚠️ **枠1 では CT の成長が効かない**（元から CT 0）。
    /// ⭐ 詰め替えないと「★5の卵を払って何も変わらない段」が残る
    /// （tamaru・tsunoga など5種の枠1 で Lv3・Lv5 が死んでいた）。</summary>
    [Fact]
    public void 枠1に死んだ成長の段が無い()
    {
        foreach (var species in SpeciesTable.All)
        {
            var skill = Skills.ById(species.Skill1);
            var growth = Skills.GrowthOf(skill, 0);

            Assert.Equal(Skills.MaxLevel - 1, growth.Count);
            Assert.DoesNotContain(SkillGain.Ct, growth);
        }
    }

    /// <summary>⭐ 枠1 でも、レベルを上げれば必ず何かが変わること。</summary>
    [Fact]
    public void 枠1でもレベルごとに何かが伸びる()
    {
        foreach (var species in SpeciesTable.All)
        {
            var skill = Skills.ById(species.Skill1);
            var last = Skills.BoostOf(skill, 1, 0);
            for (int level = 2; level <= Skills.MaxLevel; level++)
            {
                var now = Skills.BoostOf(skill, level, 0);
                Assert.False(Same(last, now),
                    $"{species.Id} の枠1（{skill.Id}）: Lv{level} で何も変わらない");
                last = now;
            }
        }
    }

    private static bool Same(SkillBoost a, SkillBoost b) =>
        a.PowerPercent == b.PowerPercent && a.CtCut == b.CtCut
        && a.ChancePoints == b.ChancePoints && a.ExtraTurns == b.ExtraTurns
        && a.ExtraRepeat == b.ExtraRepeat && a.ExtraPercent == b.ExtraPercent
        && a.ExtraCount == b.ExtraCount && a.ExtraAmount == b.ExtraAmount;
}
