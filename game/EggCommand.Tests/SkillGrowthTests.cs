#nullable enable
using System;
using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>技を育てても効き目が弱くならないこと。
///
/// 🔴 **実際に踏んだバグ**（2026-08-27）: 命削り（`life-cut` ＝ 最大HPの30%を削る）は
/// `Effect.HealRatio(-30)`。成長は <see cref="SkillBoost.ExtraPercent"/> を**正の値**で
/// 足すので、`-(Percent + Extra)` が小さくなり、**Lv5 で 30%→25% に弱くなっていた**。
/// ⭐ 同じ形を <see cref="EffectKind.Gauge"/> と <see cref="EffectKind.Dispel"/> では
/// 既に符号対応済み ── **削りだけ漏れていた。**
///
/// ⭐ 「強い」の向きは欄ごとに違う。<see cref="MeasureGrowth"/> が**唯一の出所**。
/// ⚠️ ここは `Battle.ApplyOne`（本体）を実際に走らせて、その結果として状態がどれだけ
/// 動いたかを読むだけ ── 判定式を検査側で作り直さない。作り直すと本体の式が壊れても
/// 検査側の式が同じだけ壊れていれば緑のまま通ってしまう（`wiki/開発/罠と教訓.md` の
/// 「模様の量を測っていた」と同じ罠）。</summary>
public class SkillGrowthTests
{
    private static Creature Make(string id, int hp, int atk, int def, int spd) =>
        new Creature(id, "tamaru", new StatBlock(hp, atk, def, spd),
            new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1);

    /// <summary>⚠️ 乱数は1回だけ引かせて、必ず当たる目に固定する。
    /// ⭐ <see cref="Effect.MinChance"/>(20) が下限なので、判定に使う `land` は常に20以上
    /// ── 最初の1振りが20未満の種を選べば、確率つきの効果も外れない
    /// （実測: seed=1 の最初の `Int(0,100)` は 4）。</summary>
    private static Rng FreshRng() => new Rng(1);

    /// <summary>⚠️ 「野生ステ」に置く HP。⭐ 実際の MaxHp は `HpScale`(105) 倍になるので、
    /// ここを大きくしすぎると `target.MaxHp * (割合)` が **int32 を溢れる**
    /// （実際に 200,000 で試して踏んだ ── 溢れて負になった値が
    /// `if (amount &lt; 1) amount = 1` に落ち、「Lv3=2000万→Lv4=1」という
    /// 見せかけの「弱くなった」を作った。これは実装のバグではなく**この検査の作り方の
    /// バグ**だった ── 現実のMaxHp上限は「10万HP」（wiki）で、そこまでは溢れない）。
    /// ⭐ MaxHp ≈ 5,000×105 ＝ 525,000。現実の上限(10万)の5倍あり、ダメージ系の
    /// 頭打ち回避には十分な余白で、かつ割合×MaxHpが int32 を溢れない。</summary>
    private const int BigHp = 5_000;

    private static void LoadManyBoons(Unit u)
    {
        u.Status.Atk = new Modifier { Percent = 50, Turns = 5 };
        u.Status.Def = new Modifier { Percent = 50, Turns = 5 };
        u.Status.Spd = new Modifier { Percent = 30, Turns = 5 };
        u.Status.Shield = 5;
        u.Status.Guts = 5;
        u.Status.Immune = 5;
        u.Status.Regen = new Stacking { Stacks = 1, Turns = 5 };
    }

    private static void LoadManyBanes(Unit u)
    {
        u.Status.Atk = new Modifier { Percent = -50, Turns = 5 };
        u.Status.Def = new Modifier { Percent = -50, Turns = 5 };
        u.Status.Spd = new Modifier { Percent = -30, Turns = 5 };
        u.Status.Stun = 5;
        u.Status.Sleep = 5;
        u.Status.Poison = new Stacking { Stacks = 1, Turns = 5 };
        u.Status.Taunt = 2;
        u.Status.Block = 3;
    }

    /// <summary>その技をあるレベルまで育てたとき、この1つの効果が実際にどれだけ効いたか。
    ///
    /// ⭐ **「強い」を1つの数へ読み替える、唯一の出所。**効果の欄ごとに向きが違うので
    /// （威力・持続・確率は大きいほど強い／CT・削り・ゲージは絶対値が大きいほど強い、など）、
    /// ここで**符号を解決したあとの「大きいほど強い」数**を返す。
    /// ⚠️ 計算は必ず `Battle`（本体）を実際に走らせて読み返す。</summary>
    private static int MeasureGrowth(Skill skill, Effect effect, int level)
    {
        if (effect.Innate)
        {
            // ⭐ 生まれつきは通常の効果適用（ApplyEffect）を通らず、
            //    `Battle.InnateStatsOf` という別経路で素のステへ畳み込まれる
            //    （パッシブ技はここでしか効かない）。
            var creature = new Creature("innate-test", "tamaru", new StatBlock(1000, 200, 200, 200),
                new StatBlock(0, 0, 0, 0), 0, 0, skill.Id, null, 0, null, null, 1);
            creature.SkillPoints[1] = SkillCosts.TotalFor(level);
            int before = Creatures.StatsOf(creature)[effect.Stat];
            int after = Battle.InnateStatsOf(creature)[effect.Stat];
            return Math.Abs(after - before);
        }

        var boost = Skills.BoostOf(skill, level);
        var attacker = Make("attacker", BigHp, 400, 200, 200);
        var defender = Make("defender", BigHp, 150, 200, 150);
        var s = Battle.CreateBattle(new List<Creature> { attacker }, new List<Creature> { defender },
            FreshRng());
        var actor = s.Units[0];
        var target = s.Units[1];

        // ⚠️ 条件つきの効果は、条件を満たしてから撃つ（技の最初の効果には条件が付かない
        //    約束なので、ここに来る効果は必ず「1つ目ではない」効果）。
        if (effect.When == SkillWhen.FoeStopped) target.Status.Stun = 3;
        if (effect.When == SkillWhen.FoeHalf) target.Hp = target.MaxHp / 2;

        switch (effect.Kind)
        {
            case EffectKind.Damage:
            {
                int before = target.Hp;
                Battle.ApplyOne(s, actor, target, effect, boost);
                return before - target.Hp;
            }

            case EffectKind.HealRatio:
            {
                if (effect.Percent < 0)
                {
                    // ⭐ 削り。満タンで受ける ── 「cut > Hp」の頭打ちを避けて生の伸びを見る
                    //    （🔴 命削りのバグはここでしか見えない）
                    int before = target.Hp;
                    Battle.ApplyOne(s, actor, target, effect, boost);
                    return before - target.Hp;
                }
                // 回復。ほぼ空にしておく ── MaxHp での頭打ちを避ける
                target.Hp = 1;
                int beforeHeal = target.Hp;
                Battle.ApplyOne(s, actor, target, effect, boost);
                return target.Hp - beforeHeal;
            }

            case EffectKind.Poison:
                Battle.ApplyOne(s, actor, target, effect, boost);
                return target.Status.Poison.Turns;

            case EffectKind.Regen:
                Battle.ApplyOne(s, actor, target, effect, boost);
                return target.Status.Regen.Turns;

            case EffectKind.Shield:
                Battle.ApplyOne(s, actor, target, effect, boost);
                return target.Status.Shield;

            case EffectKind.Stun:
                Battle.ApplyOne(s, actor, target, effect, boost);
                return target.Status.Stun;

            case EffectKind.Sleep:
                Battle.ApplyOne(s, actor, target, effect, boost);
                return target.Status.Sleep;

            case EffectKind.Guts:
                Battle.ApplyOne(s, actor, target, effect, boost);
                return target.Status.Guts;

            case EffectKind.Immune:
                Battle.ApplyOne(s, actor, target, effect, boost);
                return target.Status.Immune;

            case EffectKind.Block:
                Battle.ApplyOne(s, actor, target, effect, boost);
                return target.Status.Block;

            case EffectKind.Taunt:
                Battle.ApplyOne(s, actor, target, effect, boost);
                return target.Status.Taunt;

            case EffectKind.Buff:
                Battle.ApplyOne(s, actor, target, effect, boost);
                // ⭐ 育つのは持続（Turns）だけ（割合は語彙ごとに固定値・育たない）。
                //    切れない持続（Skills.Lasting = -1）はレベルを通じて動かないので、
                //    比べても「弱くなった」にはならない（-1 と -1 の比較で常に釣り合う）。
                return target.Status.ModOf(effect.Stat).Turns;

            case EffectKind.Gauge:
            {
                target.Gauge = effect.Percent < 0 ? Battle.GaugeMax * 10 : 0;
                int before = target.Gauge;
                Battle.ApplyOne(s, actor, target, effect, boost);
                return Math.Abs(target.Gauge - before);
            }

            case EffectKind.Ct:
            {
                for (int i = 0; i < target.Cooldowns.Length; i++) target.Cooldowns[i] = 30;
                var before = (int[])target.Cooldowns.Clone();
                Battle.ApplyOne(s, actor, target, effect, boost);
                int moved = 0;
                for (int i = 1; i < target.Cooldowns.Length; i++)
                    moved += Math.Abs(target.Cooldowns[i] - before[i]);
                return moved;
            }

            case EffectKind.Dispel:
            {
                if (effect.Count < 0) LoadManyBanes(target); else LoadManyBoons(target);
                int before = effect.Count < 0 ? Battle.BanesOn(target) : Battle.BoonsOn(target);
                Battle.ApplyOne(s, actor, target, effect, boost);
                int after = effect.Count < 0 ? Battle.BanesOn(target) : Battle.BoonsOn(target);
                return before - after;
            }

            case EffectKind.Steal:
            {
                LoadManyBoons(target);
                int before = Battle.BoonsOn(target);
                Battle.ApplyOne(s, actor, target, effect, boost);
                int after = Battle.BoonsOn(target);
                return before - after;
            }

            case EffectKind.Revive:
                target.Hp = 0;
                Battle.ApplyOne(s, actor, target, effect, boost);
                return target.Hp;

            // ⭐ 2026-08-27 に足した6効果ぶん。⚠️ Seal/Anchor/Invincible/Counter は
            //    ガッツ・免疫・ブロック・挑発と同じ「持続を数えるだけの状態」なので、
            //    同じ形（ApplyOne → その欄を読む）で測れる。
            case EffectKind.Seal:
                Battle.ApplyOne(s, actor, target, effect, boost);
                return target.Status.Seal;

            case EffectKind.Anchor:
                Battle.ApplyOne(s, actor, target, effect, boost);
                return target.Status.Anchor;

            case EffectKind.Invincible:
                Battle.ApplyOne(s, actor, target, effect, boost);
                return target.Status.Invincible;

            case EffectKind.Counter:
                Battle.ApplyOne(s, actor, target, effect, boost);
                return target.Status.Counter;

            case EffectKind.Extend:
            {
                // ⚠️ 即時効果 ── 乗っている弱化が無いと何も起きない（単体では0の札）。
                //    ⭐ 先に毒を1つ置いて、その持続がどれだけ伸びたかを読む。
                target.Status.Poison = new Stacking { Stacks = 1, Turns = 1 };
                int before = target.Status.Poison.Turns;
                Battle.ApplyOne(s, actor, target, effect, boost);
                return target.Status.Poison.Turns - before;
            }

            default:
                // ⚠️ 黙って見送らない。この技を測る方法をここに足すこと
                //    （未対応のまま通すと「育てて弱くなっていないか」を確かめないまま緑になる）。
                throw new InvalidOperationException(
                    $"{effect.Kind} の伸びを測る方法がこの検査に無い。MeasureGrowth に測り方を足すこと");
        }
    }

    /// <summary>🔴 どの技も、育てて（スキルレベルを上げて）弱くなることはない。</summary>
    [Fact]
    public void どの技も育てて弱くならない()
    {
        var broken = new List<string>();
        foreach (var skill in Skills.All)
        {
            int maxLevel = Skills.MaxLevelOf(skill);
            if (maxLevel <= 1) continue; // 育たない技

            foreach (var effect in skill.Effects)
            {
                int prev = MeasureGrowth(skill, effect, 1);
                for (int level = 2; level <= maxLevel; level++)
                {
                    int now = MeasureGrowth(skill, effect, level);
                    if (now < prev)
                    {
                        broken.Add($"{skill.Name}（{skill.Id} / {effect.Kind}）"
                            + $" Lv{level - 1}={prev} → Lv{level}={now}（育てて弱くなった）");
                    }
                    prev = now;
                }
            }
        }
        Assert.True(broken.Count == 0,
            "育てると弱くなる技（向きの印を大きさとして扱っている疑い）:" + Environment.NewLine
            + "  " + string.Join(Environment.NewLine + "  ", broken));
    }
}
