#nullable enable
using System;
using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>AI が見積もる一撃と、Battle が実際に与える一撃が一致すること
/// （⚠️ ただし**盾・無敵は除く** ── 下の注記が唯一の出所）。
///
/// 🔴 **実際に踏んだバグ**（2026-08-26・2026-08-27）: 軽減が二乗になった日と、
/// 防御の強化が「ステ」ではなく「被ダメージ」へ掛かるように移った日に、
/// **本体（`Battle.DamageOf`/`Battle.Guarded`）だけ直って `Ai.EstimateDamage` の
/// 見積りが古いままになりかけた**。AI が見る一撃と、実際に飛ぶ一撃が別の式を
/// 通っていると、AI は「本当は弱い技」を強いと思い込んで選び続ける。
///
/// ⚠️ **AI は盾（Shield）と無敵（Invincible）を見ずに見積もる。**
/// 作者の判断 2026-08-27・C＝**このまま据え置き**。⭐ 賢くすると `sim skillvalue` の
/// 釣り合いの実測値が全部動いてしまうため。⚠️ `Ai.EstimateDamage` は
/// `target.Status.Def`（防御の強化・弱化）しか読まない ── `Status.Shield` /
/// `Status.Invincible` はそもそも式に出てこない（穴が空いているのではなく、
/// **見ない設計**）。
///
/// ⭐ **この検査は「見えている軸」と「見えていない軸」を混ぜない**:
/// 1. **素／防御力UP／防御力DOWN**（＝ `Battle.Guarded` が読む軸）は、
///    AI が実際に見て判断に使っているので、見積りと実際が**一致しなければならない**
///    （ここが崩れたら本物のバグ）。
/// 2. **シールド／無敵**は、見積りが「盾・無敵が無かったとき」と**同じ値のまま
///    動かないこと**を確かめる。⚠️ 「実際と一致するか」は見ない
///    （一致しないのが**いまの仕様**なので、そこを検査すると仕様を検査に固定できない）。
///    ⭐ 将来 AI にこの2つを見せるよう直したら、その日にここが「見積りが動いた」と
///    教えてくれる（穴を隠さず、「いまはこう決めた」を記録する形）。
///
/// ⚠️ 見積り側は `Ai.EstimateHitFor`（本体の `EstimateTotal` をそのまま横流しする
/// 検査向けの入口）を呼ぶだけ ── 判定式を検査側で作り直さない。</summary>
public class AiEstimateTests
{
    private static Creature Make(string id, int hp, int atk, int def, int spd) =>
        new Creature(id, "tamaru", new StatBlock(hp, atk, def, spd),
            new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1);

    private const int BigHp = 200_000;

    /// <summary>攻め役・受け役、代表2体。⭐ 素質を偏らせて、どの依存ステで撃っても
    /// 意味のある一撃になるようにする。</summary>
    private static (BattleState state, Unit actor, Unit target) FreshPair()
    {
        var attacker = Make("attacker", BigHp, 320, 180, 140);
        var defender = Make("defender", BigHp, 140, 220, 160);
        var s = Battle.CreateBattle(new List<Creature> { attacker }, new List<Creature> { defender },
            new Rng(1));
        return (s, s.Units[0], s.Units[1]);
    }

    private static readonly (string label, Effect effect)[] Effects =
    {
        ("小・攻撃依存", Effect.Damage(PowerTier.Small, DamageScale.Atk)),
        ("特大・防御依存", Effect.Damage(PowerTier.Huge, DamageScale.Def)),
        ("中・速度依存", Effect.Damage(PowerTier.Medium, DamageScale.Spd)),
        ("大・防御無視", Effect.Damage(PowerTier.Large, DamageScale.Atk, pierce: true)),
        ("大・強化無視", Effect.Damage(PowerTier.Large, DamageScale.Atk, bare: true)),
        ("小・3連", Effect.Damage(PowerTier.Small, DamageScale.Atk, repeat: 3)),
    };

    /// <summary>⚠️ `AiSees`＝AI が実際に読んで判断へ使っている軸かどうか
    /// （`Ai.EstimateDamage` が読むのは `target.Status.Def` だけ）。
    /// ⭐ **唯一の出所**（2026-08-27）── ここを false にした2つ（盾・無敵）が、
    /// 上の注記でいう「見えていない軸」。</summary>
    private static readonly (string label, Action<Unit> setup, bool aiSees)[] Statuses =
    {
        ("素", _ => { }, true),
        ("防御力UP", t => t.Status.Def = new Modifier { Percent = 50, Turns = 3 }, true),
        ("防御力DOWN", t => t.Status.Def = new Modifier { Percent = -50, Turns = 3 }, true),
        ("シールド2", t => t.Status.Shield = 2, false),
        ("無敵2", t => t.Status.Invincible = 2, false),
    };

    /// <summary>🔴 AI の見積りは、①AI が実際に見ている軸（防御力UP/DOWN）の下では
    /// 実際の一撃と一致し、②AI が見ていない軸（盾・無敵）の下では見積りそのものが
    /// 動かない（＝盾・無敵が無いときと同じ値のまま）ことを確かめる。</summary>
    [Fact]
    public void AIの見積りと本体の一撃は防御力UPDOWNの下で一致し盾無敵では見積りが動かない()
    {
        var broken = new List<string>();
        foreach (var (effLabel, effect) in Effects)
        {
            // ⭐ 比較の基準＝盾・無敵が無い（＝「素」の）ときの見積り
            var (plainState, plainActor, plainTarget) = FreshPair();
            int plainEstimate = Ai.EstimateHitFor(plainActor, plainTarget, effect);

            foreach (var (stLabel, setup, aiSees) in Statuses)
            {
                var (state, actor, target) = FreshPair();
                setup(target);

                int estimate = Ai.EstimateHitFor(actor, target, effect);

                if (aiSees)
                {
                    // ⭐ AI が実際に見ている軸 ── 見積りと実際が一致しないのは本物のバグ
                    int before = target.Hp;
                    Battle.ApplyOne(state, actor, target, effect);
                    int actual = before - target.Hp;
                    if (estimate != actual)
                    {
                        broken.Add($"{effLabel} × {stLabel}: 見積り={estimate} 実際={actual}"
                            + $"（差={estimate - actual}）");
                    }
                }
                else
                {
                    // ⚠️ AI は盾・無敵を見ない（作者の判断 2026-08-27・C＝据え置き）。
                    //    ここでは「実際と一致するか」ではなく「盾・無敵が無いときと
                    //    見積りが変わらないか」を確かめる ── 見積りが動いたら、
                    //    どこかで盾・無敵を読むように変わったということ
                    //    （そのときはこの検査ごと書き直すこと）。
                    if (estimate != plainEstimate)
                    {
                        broken.Add($"{effLabel} × {stLabel}: 見積りが「素」から動いた"
                            + $"（見積り={estimate} 素の見積り={plainEstimate}）"
                            + "── AIが盾・無敵を見るようになったなら、この検査を書き直すこと");
                    }
                }
            }
        }
        Assert.True(broken.Count == 0,
            "AIの見積りが、見ているはずの軸とずれているか、見ていないはずの軸で動いた組み合わせ:"
            + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", broken));
    }
}
