using System;
using System.Collections.Generic;
using EggCommand.Core;
using EggCommand.Sim;
using Xunit;

namespace EggCommand.Tests;

/// <summary>技1つの「手ぶん」（<see cref="Program.TurnValueOf"/>）。
///
/// ⭐ この物差しは**釣り合いを決める側**なので、壊れていると気づけない。
/// AI も乱数も通さない算数なので、狂っていても検査を書かないかぎり誰も見ない。
///
/// 🔴 **この一族のバグを6回踏んでいる**（うち3件は 2026-08-27 に発見）:
/// <list type="bullet">
///   <item>ゲージ … 減らす側の `Percent` が負 → −0.26 で並んでいた</item>
///   <item>挑発 … 回数が `Hits` にあるのに `Count` を読み、0.00 で並んでいた</item>
///   <item>味方全体 … 対象数を掛け忘れ、全体回復が単体と同じ値だった</item>
///   <item>弱化解除 … `Cleanse` の `Count` が負 → **−1.80 / −3.60**</item>
///   <item>命削り … 最大HP削りの `Percent` が負 → **−0.72**</item>
///   <item>パッシブ … `Lasting`（−1）を回数として掛けて → **−0.02〜−0.21**</item>
/// </list>
/// ⭐ **形はどれも同じ** ── 「向きの印に使っている負の値を、大きさとして掛けた」。
/// ⚠️ 個別に直しても次の効果を足した日にまた踏むので、**一族ごと**ここで止める。</summary>
public class TurnValueTests
{
    /// <summary>🔴 **どの技も、手ぶんが負にならない。**
    ///
    /// ⚠️ 弱い技があるのは構わない（0.36 の毒は「殴ったほうが得」というだけ）。
    /// ⭐ だが**負**は「押すと損をする」という意味で、そんな技は1本も無い ──
    /// 出たならそれは技ではなく**物差しの故障**。</summary>
    [Fact]
    public void どの技も手ぶんが負にならない()
    {
        var broken = new List<string>();
        foreach (var skill in Skills.All)
        {
            double value = SkillValues.Of(skill, out string why);
            if (value < 0) broken.Add($"{skill.Name} = {value:0.00}手ぶん（{why}）");
        }
        Assert.True(broken.Count == 0,
            "手ぶんが負の技（物差しの故障。負の値を向きの印に使っている欄を疑う）:"
            + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", broken));
    }

    /// <summary>⭐ **切れない強化は、切れる強化より価値が高い。**
    ///
    /// ⚠️ <see cref="Skills.Lasting"/> は −1 なので、回数として掛けると符号が逆になる。
    /// ⭐ 「3ターンの強化」と「切れない強化」を並べて、後者が上に来ることで
    /// **持続の扱いが向きだけでなく大きさとしても正しい**ことを確かめる。</summary>
    [Fact]
    public void 切れない強化は切れる強化より高く出る()
    {
        var short_ = new Skill("test-short", "検査用・3T", "", SkillType.Support, Target.AllyOne,
            Effect.Buff(StatKey.Atk, 1, 3));
        var lasting = new Skill("test-lasting", "検査用・切れない", "", SkillType.Support, Target.AllyOne,
            Effect.Buff(StatKey.Atk, 1, Skills.Lasting));

        double a = SkillValues.Of(short_, out _);
        double b = SkillValues.Of(lasting, out _);
        Assert.True(b > a, $"切れない強化 {b:0.00} が 3ターンの強化 {a:0.00} 以下になっている");
    }

    /// <summary>⭐ **弱化を落とす技は、落とす数が多いほど高く出る。**
    /// ⚠️ <see cref="Effect.Cleanse"/> は個数を**負**で持つ（強化消しと同じ欄を使う）。
    /// 大きさとして掛けていないと、数を増やすほど**下がる**。</summary>
    [Fact]
    public void 弱化を多く落とすほど高く出る()
    {
        var one = new Skill("test-cleanse1", "検査用・1つ", "", SkillType.Heal, Target.AllyOne,
            Effect.Cleanse(1));
        var two = new Skill("test-cleanse2", "検査用・2つ", "", SkillType.Heal, Target.AllyOne,
            Effect.Cleanse(2));
        Assert.True(SkillValues.Of(two, out _) > SkillValues.Of(one, out _),
            "弱化を2つ落とす技が、1つ落とす技より低く出ている");
    }

    /// <summary>⭐ **最大HPを削る技は、削る割合が多いほど高く出る。**
    /// ⚠️ 削りは回復と同じ欄（<see cref="EffectKind.HealRatio"/>）を負で使う。</summary>
    [Fact]
    public void 最大HPを多く削るほど高く出る()
    {
        var small = new Skill("test-shave1", "検査用・20%", "", SkillType.Attack, Target.EnemyOne,
            Effect.HealRatio(-20));
        var big = new Skill("test-shave2", "検査用・40%", "", SkillType.Attack, Target.EnemyOne,
            Effect.HealRatio(-40));
        Assert.True(SkillValues.Of(big, out _) > SkillValues.Of(small, out _),
            "最大HPを40%削る技が、20%削る技より低く出ている");
    }

    /// <summary>⭐ **相手のゲージを多く削るほど高く出る。**
    /// ⚠️ <see cref="EffectKind.Gauge"/> は減らす側の <see cref="Effect.Percent"/> が**負**
    /// （<see cref="Effect.Gauge"/> 参照）。大きさとして掛けていないと、
    /// 削る割合を増やすほど**下がる**（実際 −0.26 で並んでいた ── `wiki/開発/罠と教訓.md`）。
    ///
    /// ⚠️ `TurnValueTests` 既存の検査（最大HP削り・弱化解除）と重複しないぶんだけ足す ──
    /// この一族で唯一まだ検査が無かった欄が <see cref="EffectKind.Gauge"/> と
    /// <see cref="EffectKind.Ct"/>。</summary>
    [Fact]
    public void ゲージを多く削るほど高く出る()
    {
        var small = new Skill("test-gauge1", "検査用・削り10%", "", SkillType.Debuff, Target.EnemyOne,
            Effect.Gauge(-10));
        var big = new Skill("test-gauge2", "検査用・削り40%", "", SkillType.Debuff, Target.EnemyOne,
            Effect.Gauge(-40));
        Assert.True(SkillValues.Of(big, out _) > SkillValues.Of(small, out _),
            "相手のゲージを40%削る技が、10%削る技より低く出ている");
    }

    /// <summary>⭐ **CT を多く動かすほど高く出る。**
    /// ⚠️ <see cref="EffectKind.Ct"/> は自分への短縮が**負**の <see cref="Effect.Delta"/>
    /// （相手への延長は正）。向きの印を大きさとして扱っていると、短縮幅を増やすほど
    /// **下がる**。</summary>
    [Fact]
    public void CTを多く縮めるほど高く出る()
    {
        var small = new Skill("test-ct1", "検査用・CT-1", "", SkillType.Support, Target.Self,
            Effect.Ct(-1));
        var big = new Skill("test-ct2", "検査用・CT-4", "", SkillType.Support, Target.Self,
            Effect.Ct(-4));
        Assert.True(SkillValues.Of(big, out _) > SkillValues.Of(small, out _),
            "自分のCTを4縮める技が、1縮める技より低く出ている");
    }

    /// <summary>⚠️ パッシブの効き目は**強化より小さい**（枠で買うので永久・剥がれない代わり）。
    /// ⭐ 同じ 30% で数えていると、パッシブが強化と同じ強さに見える。</summary>
    [Fact]
    public void パッシブは生まれつきの効き目で数える()
    {
        var passive = Skill.Always("test-innate", "検査用・生まれつき", "", SkillType.Support,
            Effect.Always(StatKey.Atk, 1));
        SkillValues.Of(passive, out string why);
        Assert.Contains($"{Skills.InnatePercent}%", why);
        Assert.DoesNotContain($"{Skills.BuffPercentOf(StatKey.Atk)}%", why);
    }
}
