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

    // ── ③ 切れない持続 ──────────────────────────

    /// <summary>⭐ 何手番たっても切れないこと。⚠️ 普通の強化なら 3T で消える。</summary>
    [Fact]
    public void 構えは切れない()
    {
        var state = Field("stance");
        var actor = Ally(state, 0);

        Battle.PerformAction(state, actor, 1);
        Assert.True(Battle.IsOn(actor.Status.Def));
        int lifted = actor.Status.Def.Percent;

        for (int turn = 0; turn < 12; turn++) Battle.PerformAction(state, actor, 0);

        Assert.True(Battle.IsOn(actor.Status.Def), "12手番で切れている");
        Assert.Equal(lifted, actor.Status.Def.Percent);
    }

    /// <summary>⚠️ **剥がせること。**剥がせないと「先に掛けた者勝ち」で読み合いが消える。</summary>
    [Fact]
    public void 切れない持続も剥がせる()
    {
        var state = Field();
        var actor = Ally(state, 0);
        Battle.ApplyOne(state, actor, actor, Effect.Buff(StatKey.Def, 1, Skills.Lasting));
        Assert.True(Battle.IsOn(actor.Status.Def));

        Battle.ApplyOne(state, Foe(state, 0), actor, Effect.Dispel(1));

        Assert.False(Battle.IsOn(actor.Status.Def), "永続が剥がせない");
    }

    /// <summary>⚠️ スキルレベルの「持続+1」を足して**普通の強化に戻さない**こと。
    /// ⭐ -1 に +2 すると +1 ＝ 1手番で切れる強化になる。</summary>
    [Fact]
    public void 切れない持続に持続の上乗せを足さない()
    {
        var state = Field();
        var actor = Ally(state, 0);
        Battle.ApplyOne(state, actor, actor, Effect.Buff(StatKey.Def, 1, Skills.Lasting),
            new SkillBoost { ExtraTurns = 2 });

        Assert.True(actor.Status.Def.Turns < 0, $"永続でなくなっている（{actor.Status.Def.Turns}）");
    }

    /// <summary>⭐ 奪ったときも「切れないほうが強い」と判じること。</summary>
    [Fact]
    public void 強奪は切れない持続を短い持続で上書きしない()
    {
        var state = Field();
        var thief = Ally(state, 0);
        var victim = Foe(state, 0);
        Battle.ApplyOne(state, thief, thief, Effect.Buff(StatKey.Atk, 1, 2));
        Battle.ApplyOne(state, victim, victim, Effect.Buff(StatKey.Atk, 1, Skills.Lasting));

        Battle.ApplyOne(state, thief, victim, Effect.Steal(1));

        Assert.True(thief.Status.Atk.Turns < 0, "奪ったのに短いほうが残っている");
    }

    // ── 表に出る形 ──────────────────────────────

    /// <summary>⭐ 説明文が読める形になること。⚠️ 「-1T」「-2個」と出さない。</summary>
    [Theory]
    [InlineData("cleanse", "弱化を2個")]
    [InlineData("stance", "戦闘の間ずっと")]
    [InlineData("drain-all", "さらに自分")]
    public void 新しい語彙の説明文が読める(string id, string wanted)
    {
        string text = SkillText.Describe(Skills.ById(id));
        Assert.Contains(wanted, text);
        Assert.DoesNotContain("-1T", text);
        Assert.DoesNotContain("個数", text);
    }
}
