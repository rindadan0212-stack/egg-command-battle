#nullable enable
using System;
using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>自分への代償（弱化）を足した技は、値打ちが下がって見えること。
///
/// 🔴 **実際に踏んだバグ**（2026-08-27）: `Ai.cs` の強化・弱化の採点が、符号も
/// 相手の側も見ずに**常に加点**していた。`捨て身の突き`（`reckless` ＝ 自分に
/// 防御DOWN という代償を払う技）の**代償が得点**になっていた。
/// ⭐ <see cref="Skills.LoadOf"/>（CT の値段）は同じ形を正しく「代償」として値引いている
/// ── **同じ判断が2か所にあって、片方だけ正しかった。**
///
/// ⭐ ここでは「ある技」と「それに自分への弱化を足した技」を組んで、
/// <see cref="Skills.LoadOf"/>（CT の値段）・<see cref="Ai.ScoreOfSkill"/>（AI の採点）・
/// <see cref="SkillValues.Of"/>（手ぶん）の**3つが同じ向き**（代償を足すと下がる）を
/// 向いているかを1本で押さえる。</summary>
public class SelfCostValueTests
{
    private static Creature Make(string id, int hp, int atk, int def, int spd) =>
        new Creature(id, "tamaru", new StatBlock(hp, atk, def, spd),
            new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1);

    /// <summary>🔴 自分への代償は、3つの物差し全部で値打ちを下げなければならない。</summary>
    [Fact]
    public void 自分への代償は技の値打ちを下げる()
    {
        var plain = new Skill("test-plain-cost", "検査用・素の一撃", "", SkillType.Attack,
            Target.EnemyOne, Effect.Damage(PowerTier.Large, DamageScale.Atk));
        // ⭐ reckless（捨て身の突き）と同じ形 ── 攻撃のあと、自分に弱化を掛ける
        var withCost = new Skill("test-with-cost", "検査用・代償つき", "", SkillType.Attack,
            Target.EnemyOne,
            Effect.Damage(PowerTier.Large, DamageScale.Atk),
            Effect.Buff(StatKey.Def, -1, 3).To(Target.Self));

        var broken = new List<string>();

        int loadPlain = Skills.LoadOf(plain);
        int loadCost = Skills.LoadOf(withCost);
        if (loadCost >= loadPlain)
        {
            broken.Add($"Skills.LoadOf（CTの値段）が下がっていない: "
                + $"素={loadPlain} 代償つき={loadCost}");
        }

        var s = Battle.CreateBattle(
            new List<Creature> { Make("a", 30, 30, 30, 30) },
            new List<Creature> { Make("e", 30, 30, 30, 30) });
        var actor = s.Units[0];
        double scorePlain = Ai.ScoreOfSkill(s, actor, plain);
        double scoreCost = Ai.ScoreOfSkill(s, actor, withCost);
        if (scoreCost >= scorePlain)
        {
            broken.Add($"Ai.ScoreOfSkill（AIの採点）が下がっていない: "
                + $"素={scorePlain:0.00} 代償つき={scoreCost:0.00}");
        }

        double valuePlain = SkillValues.Of(plain, out string whyPlain);
        double valueCost = SkillValues.Of(withCost, out string whyCost);
        if (valueCost >= valuePlain)
        {
            broken.Add($"SkillValues.Of（手ぶん）が下がっていない: "
                + $"素={valuePlain:0.00}（{whyPlain}） 代償つき={valueCost:0.00}（{whyCost}）");
        }

        Assert.True(broken.Count == 0,
            "自分への代償が値打ちを下げていない実装がある（符号か相手の側を見ていない疑い）:"
            + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", broken));
    }
}
