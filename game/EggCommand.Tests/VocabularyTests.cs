using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>2026-08-20 に足した3つの**形**が実際に動くか。
///
/// ⭐ 参考作品の R帯60体を突き合わせて、本作の語彙で書けなかったのは
/// 効果の種類ではなく次の3つだった:
/// <list type="bullet">
///   <item>弱化を**落とす**（プリミティブは在ったのに技が1本も無かった）</item>
///   <item>**1手2役**（技が狙い先を1つしか持てなかった）</item>
///   <item>**切れない持続**（あちらはパッシブで持つ「常に防御が高い」型）</item>
/// </list>
/// ⚠️ ここが通らないまま技表に並ぶと、遊べてしまうのに効かない技になる。</summary>
public class VocabularyTests
{
    private static Creature Make(string id, string? skill2 = null, string? skill3 = null) =>
        new Creature(id, "tamaru", new StatBlock(30, 30, 30, 30),
            new StatBlock(0, 0, 0, 0), 0, 0, skill2, skill3, 0, null, null, 1);

    private static BattleState Field(string? mine = null)
    {
        var allies = new List<Creature> { Make("a0", mine), Make("a1"), Make("a2") };
        var foes = new List<Creature> { Make("e0"), Make("e1"), Make("e2") };
        return Battle.CreateBattle(allies, foes);
    }

    private static Unit Ally(BattleState s, int i) => s.Units.Find(u => u.Key == $"ally-{i}")!;
    private static Unit Foe(BattleState s, int i) => s.Units.Find(u => u.Key == $"enemy-{i}")!;

    // ── ① 弱化解除 ───────────────────────────────

    /// <summary>⭐ 味方に乗った弱化が落ちること。⚠️ これが無いと弱化は一方通行だった。</summary>
    [Fact]
    public void 弱化解除は味方の弱化を落とす()
    {
        var state = Field();
        var friend = Ally(state, 1);
        Battle.ApplyOne(state, Foe(state, 0), friend, Effect.Buff(StatKey.Spd, -1, 3));
        Battle.ApplyOne(state, Foe(state, 0), friend, Effect.Poison(1, 4));
        Assert.True(Battle.IsOn(friend.Status.Spd));
        Assert.True(friend.Status.Poison.Turns > 0);

        Battle.ApplyOne(state, Ally(state, 0), friend, Effect.Cleanse(2));

        Assert.False(Battle.IsOn(friend.Status.Spd));
        Assert.Equal(0, friend.Status.Poison.Turns);
    }

    /// <summary>⚠️ 強化まで巻き添えにしない。⭐ 落とすのは弱化だけ。</summary>
    [Fact]
    public void 弱化解除は強化を落とさない()
    {
        var state = Field();
        var friend = Ally(state, 1);
        Battle.ApplyOne(state, Ally(state, 0), friend, Effect.Buff(StatKey.Atk, 1, 3));
        Battle.ApplyOne(state, Foe(state, 0), friend, Effect.Buff(StatKey.Def, -1, 3));

        Battle.ApplyOne(state, Ally(state, 0), friend, Effect.Cleanse(2));

        Assert.True(Battle.IsOn(friend.Status.Atk));
        Assert.Equal(Skills.BuffPercent, friend.Status.Atk.Percent);
        Assert.False(Battle.IsOn(friend.Status.Def));
    }

    /// <summary>⚠️ **育てるほど弱くならないこと。**個数が負なので、
    /// スキルレベルの上乗せをそのまま足すと落とす数が **減る**（2→1）。</summary>
    [Fact]
    public void 弱化解除はレベルを上げると多く落とす()
    {
        Assert.Equal(3, CleansedWith(new SkillBoost { ExtraCount = 1 }));
        Assert.Equal(2, CleansedWith(new SkillBoost()));
    }

    private static int CleansedWith(SkillBoost boost)
    {
        var state = Field();
        var friend = Ally(state, 1);
        var enemy = Foe(state, 0);
        Battle.ApplyOne(state, enemy, friend, Effect.Buff(StatKey.Atk, -1, 3));
        Battle.ApplyOne(state, enemy, friend, Effect.Buff(StatKey.Def, -1, 3));
        Battle.ApplyOne(state, enemy, friend, Effect.Buff(StatKey.Spd, -1, 3));
        Battle.ApplyOne(state, Ally(state, 0), friend, Effect.Cleanse(2), boost);

        int left = 0;
        foreach (var key in Stats.BuffKeys)
        {
            if (Battle.IsOn(friend.Status.ModOf(key))) left++;
        }
        return 3 - left;
    }

    // ── ② 1手2役（混在ターゲット）─────────────────────

    /// <summary>⭐ 敵全体を殴りながら、自分だけ回復すること。
    /// ⚠️ これが書けなかったので「殴りながら支える」札が丸ごと作れなかった。</summary>
    [Fact]
    public void 吸い上げは敵を殴って自分だけ回復する()
    {
        var state = Field("drain-all");
        var actor = Ally(state, 0);
        actor.Hp = actor.MaxHp / 2;
        int before = actor.Hp;
        var friend = Ally(state, 1);
        friend.Hp = friend.MaxHp / 2;
        int friendBefore = friend.Hp;

        Battle.PerformAction(state, actor, 1);

        Assert.True(actor.Hp > before, "撃った本人が回復していない");
        Assert.Equal(friendBefore, friend.Hp);
        for (int i = 0; i < 3; i++)
        {
            Assert.True(Foe(state, i).Hp < Foe(state, i).MaxHp, $"enemy-{i} に当たっていない");
        }
    }

    /// <summary>⭐ 代償の弱化が**自分に**乗ること（狙った相手ではなく）。</summary>
    [Fact]
    public void 捨て身の突きは自分の防御を下げる()
    {
        var state = Field("reckless");
        var actor = Ally(state, 0);

        Battle.PerformAction(state, actor, 1);

        Assert.True(Battle.IsOn(actor.Status.Def), "自分に代償が乗っていない");
        Assert.True(actor.Status.Def.Percent < 0);
        Assert.False(Battle.IsOn(Foe(state, 0).Status.Def), "相手に代償が乗っている");
    }

    /// <summary>⭐ 敵を下げるのと味方を上げるのが1手で起きること。</summary>
    [Fact]
    public void 鬨の声は敵を下げて味方を上げる()
    {
        var state = Field("warcry");
        var actor = Ally(state, 0);

        Battle.PerformAction(state, actor, 1);

        for (int i = 0; i < 3; i++)
        {
            Assert.True(Battle.IsOn(Ally(state, i).Status.Atk), $"ally-{i} が上がっていない");
            Assert.True(Ally(state, i).Status.Atk.Percent > 0);
        }
    }

    /// <summary>⚠️ 飛び先を持つ効果は**技の狙い先には掛からない**。
    /// ⭐ 二重に撃っていないことの確かめ。</summary>
    [Fact]
    public void 飛び先の効果は狙い先には掛からない()
    {
        var state = Field("reckless");
        var actor = Ally(state, 0);
        Battle.PerformAction(state, actor, 1);

        int hurt = 0;
        for (int i = 0; i < 3; i++) if (Battle.IsOn(Foe(state, i).Status.Def)) hurt++;
        Assert.Equal(0, hurt);
    }

    // ── ③ パッシブ（枠を潰して買う常時の底上げ）──────────────

    /// <summary>⭐ 押せないこと。⚠️ 選べてしまうと、CT 0 の何もしない技になる。</summary>
    [Fact]
    public void パッシブは選べない()
    {
        var state = Field("vigor");
        var actor = Ally(state, 0);
        Assert.False(Battle.IsUsable(actor, 1), "パッシブが選べてしまう");
        Assert.NotEqual(1, Ai.ChooseAction(state, actor));
    }

    /// <summary>⭐ 枠に入れただけで効いていること（最大HP に乗る）。
    /// ⚠️ 強化の修正枠は HP を持たないので、これは生まれつきだけができる。</summary>
    [Fact]
    public void 生命力は最大HPを上げる()
    {
        int plain = Battle.CreateBattle(
            new List<Creature> { Make("a0") }, new List<Creature> { Make("e0") })
            .Units.Find(u => u.Side == Side.Ally)!.MaxHp;
        int grown = Battle.CreateBattle(
            new List<Creature> { Make("a0", "vigor") }, new List<Creature> { Make("e0") })
            .Units.Find(u => u.Side == Side.Ally)!.MaxHp;

        Assert.True(grown > plain, $"最大HP が上がっていない（{plain} → {grown}）");
    }

    /// <summary>⚠️ **剥がせないこと。**枠1つを永久に払っているので、
    /// 1回の強化解除で無かったことにされると、パッシブを選ぶ理由が消える。</summary>
    [Fact]
    public void 生まれつきは剥がせない()
    {
        var state = Field("sturdy");
        var actor = Ally(state, 0);
        int before = Battle.EffectiveStat(actor.Innate.Def, actor.Status.Def);

        Battle.ApplyOne(state, Foe(state, 0), actor, Effect.Dispel(3));

        Assert.Equal(before, Battle.EffectiveStat(actor.Innate.Def, actor.Status.Def));
    }

    /// <summary>⭐ 強化と**重なる**こと。⚠️ 上書きになると、パッシブ持ちに強化を掛ける意味が消える。</summary>
    [Fact]
    public void 生まれつきの上に強化が乗る()
    {
        var state = Field("sturdy");
        var actor = Ally(state, 0);
        int innate = Battle.EffectiveStat(actor.Innate.Def, actor.Status.Def);

        Battle.ApplyOne(state, actor, actor, Effect.Buff(StatKey.Def, 1, 3));

        int both = Battle.EffectiveStat(actor.Innate.Def, actor.Status.Def);
        Assert.True(both > innate, "強化が乗っていない");
    }

    /// <summary>⚠️ 効き目は普通の強化より**小さい**こと。
    /// ⭐ 手番を1回も払わないぶんの値段。ここが同じだと強化を掛ける技が要らなくなる。</summary>
    [Fact]
    public void 生まれつきは強化より効き目が小さい()
    {
        Assert.True(Skills.InnatePercent < Skills.BuffPercent,
            $"生まれつき {Skills.InnatePercent}% が強化 {Skills.BuffPercent}% 以上ある");
    }

    /// <summary>⭐ レベルを上げると効き目が伸びること。
    /// ⚠️ CT が無いので、これが伸びないとパッシブは育てても何も変わらない。</summary>
    [Fact]
    public void パッシブはレベルを上げると効き目が伸びる()
    {
        var plain = Make("a0", "sturdy");
        var grown = Make("a1", "sturdy");
        grown.SkillPoints[1] = SkillCosts.TotalFor(Skills.MaxLevel);

        Assert.True(Battle.InnateStatsOf(grown).Def > Battle.InnateStatsOf(plain).Def,
            "レベルを上げても効き目が変わらない");
    }

    // ── 表に出る形 ──────────────────────────────

    /// <summary>⭐ 説明文が読める形になること。⚠️ 「-1T」「-2個」と出さない。</summary>
    [Theory]
    [InlineData("cleanse", "弱化を2個")]
    [InlineData("vigor", "常にHP")]
    [InlineData("drain-all", "さらに自分")]
    public void 新しい語彙の説明文が読める(string id, string wanted)
    {
        string text = SkillText.Describe(Skills.ById(id));
        Assert.Contains(wanted, text);
        Assert.DoesNotContain("-1T", text);
        Assert.DoesNotContain("個数", text);
    }
}
