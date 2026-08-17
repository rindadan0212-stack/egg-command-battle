#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>行動を選ぶ側。
    ///
    /// ⭐ 賢くしない。勝敗を決めるのは育てた個体なので、ここは緩めてよいと決めてある。
    /// 凝るほど、個体の差が AI の差に埋もれて測れなくなる。
    ///
    /// ⚠️ 乱数を使わない。同じ状況からは必ず同じ手を選ぶ。
    /// そうしないと「1万回の勝率」が AI のブレを測ってしまう。
    /// </summary>
    public static class Ai
    {
        /// <summary>ステータスを1%動かすことの価値。</summary>
        private const double BuffValue = 0.5;
        /// <summary>相手の手番を1つ奪うことの価値。⭐ 行動回数は全出力への倍率なので高く見る。</summary>
        private const double StunValue = 26;
        /// <summary>CT を1つ動かすことの価値。</summary>
        private const double CtValue = 6;
        /// <summary>狙い先を縛る1回ぶんの価値。</summary>
        private const double TauntValue = 7;
        /// <summary>ゲージを1%動かすことの価値。⭐ 手番の奪い合いなので高め。</summary>
        private const double GaugeValue = 0.35;
        /// <summary>強化を1個 剥がすことの価値。⭐ 奪うほうは自分にも乗るので倍。</summary>
        private const double DispelValue = 9;
        /// <summary>倒れた味方を1体戻すことの価値。</summary>
        private const double ReviveValue = 40;
        /// <summary>ガッツ・免疫の価値（状況が読みにくいので控えめの固定値）。</summary>
        private const double GuardianValue = 10;

        /// <summary>味方に倒れている者が居るか。⚠️ 蘇生の採点だけに使う。</summary>
        private static bool IsAnyDown(BattleState state, Unit actor)
        {
            foreach (var unit in state.Units)
            {
                if (unit.Side == actor.Side && !Battle.IsAlive(unit)) return true;
            }
            return false;
        }

        private static int EstimateDamage(Unit actor, Unit target, PowerTier tier, DamageScale scale)
        {
            var a = Creatures.StatsOf(actor.Creature);
            var t = Creatures.StatsOf(target.Creature);
            int attackStat = scale == DamageScale.Atk
                ? Battle.EffectiveStat(a.Atk, actor.Status.Atk)
                : Battle.EffectiveStat(a.Def, actor.Status.Def);
            int defenseStat = Battle.EffectiveStat(t.Def, target.Status.Def);
            double mult = Battle.ElementMultiplier(
                actor.Creature.Element,
                target.Creature.Element);
            return Battle.DamageOf(Skills.DamagePowerOf(tier), attackStat, defenseStat, mult);
        }

        /// <summary>多段ぶんを見込んだ見積り。⚠️ 盾は1発ごとに剥がれるので、
        /// 盾持ちに対しては多段のほうが通る（そこまでは数えていない — 概算でよい）。</summary>
        private static int EstimateTotal(Unit actor, Unit target, Effect effect)
        {
            return EstimateDamage(actor, target, effect.Power, effect.Scale) * effect.Repeat;
        }

        private static double ScoreOf(BattleState state, Unit actor, int slot)
        {
            var skill = Battle.ActionSkill(actor, slot);
            var foes = Battle.LivingOf(state, actor.Side == Side.Ally ? Side.Enemy : Side.Ally);
            var friends = Battle.LivingOf(state, actor.Side);
            if (foes.Count == 0) return 0;

            var byHp = new List<Unit>(foes);
            byHp.Sort((a, b) => a.Hp != b.Hp ? a.Hp - b.Hp : a.Slot - b.Slot);
            var focus = byHp[0];

            var byRatio = new List<Unit>(friends);
            byRatio.Sort((a, b) =>
            {
                double ra = (double)a.Hp / a.MaxHp;
                double rb = (double)b.Hp / b.MaxHp;
                if (ra != rb) return ra < rb ? -1 : 1;
                return a.Slot - b.Slot;
            });
            var weakest = byRatio[0];

            // その効果が誰に向くか
            var subject = skill.Target == Target.Self ? actor
                : skill.Target == Target.AllyLowest ? weakest
                : focus;

            double score = 0;
            foreach (var effect in skill.Effects)
            {
                // ⭐ 外れる技は、外れるぶん安く見積もる。
                //    ⚠️ これが無いと AI が「必ず通る前提」で弱化を選び続ける
                double land = Battle.LandChanceOf(effect, actor, subject) / 100.0;
                double before = score;

                switch (effect.Kind)
                {
                    case EffectKind.Damage:
                        if (skill.Target == Target.EnemyAll)
                        {
                            // ⚠️ 過剰打撃を価値に数えない。残 HP で頭打ちにする
                            foreach (var foe in foes)
                            {
                                int hit = EstimateTotal(actor, foe, effect);
                                score += foe.Hp < hit ? foe.Hp : hit;
                            }
                        }
                        else
                        {
                            int hit = EstimateTotal(actor, focus, effect);
                            score += focus.Hp < hit ? focus.Hp : hit;
                        }
                        break;

                    case EffectKind.Buff:
                    {
                        // 既に同じ向きで掛かっているなら重ねる意味が薄い
                        var now = subject.Status.ModOf(effect.Stat);
                        int sign = now.Percent > 0 ? 1 : now.Percent < 0 ? -1 : 0;
                        double gain = now.Turns > 0 && sign == effect.Sign ? 0 : Skills.BuffPercent;
                        score += gain * BuffValue;
                        break;
                    }

                    case EffectKind.Poison:
                    {
                        // ⭐ スタックするので重ね掛けにも価値がある。⚠️ 相手の残 HP で頭打ち
                        int stacked = subject.Status.Poison.Turns > 0 ? subject.Status.Poison.Stacks : 0;
                        int perTurn = (int)Math.Floor((double)(subject.MaxHp * Skills.TickPercent * effect.Stacks) / 100);
                        int total = perTurn * effect.Turns;
                        // 既に重なっているぶんは「上乗せ」の価値だけを見る
                        score += (subject.Hp < total ? subject.Hp : total) / (1 + stacked * 0.5);
                        break;
                    }

                    case EffectKind.Regen:
                    {
                        int stacked = subject.Status.Regen.Turns > 0 ? subject.Status.Regen.Stacks : 0;
                        int perTurn = (int)Math.Floor((double)(subject.MaxHp * Skills.TickPercent * effect.Stacks) / 100);
                        int missing = subject.MaxHp - subject.Hp;
                        int total = perTurn * effect.Turns;
                        score += (missing < total ? missing : total) * 0.7 / (1 + stacked * 0.5);
                        break;
                    }

                    case EffectKind.HealRatio:
                    {
                        // ⚠️ 「HPを戻す」と「敵のHPを削る」は同じ単位ではない。緊急度で割り引く
                        int amount = (int)Math.Floor((double)(subject.MaxHp * effect.Percent) / 100);
                        int missing = subject.MaxHp - subject.Hp;
                        double urgency = 0.5 + 0.5 * (1 - (double)subject.Hp / subject.MaxHp);
                        score += (amount < missing ? amount : missing) * urgency;
                        break;
                    }

                    case EffectKind.Shield:
                    {
                        // ⭐ 枚数ぶんの攻撃を完全に無効化する。1枚の価値は「相手の一撃ぶん」で見る
                        int incoming = EstimateDamage(focus, subject, PowerTier.Medium, DamageScale.Atk);
                        score += subject.Status.Shield > 0 ? 0 : incoming * effect.Count * 0.7;
                        break;
                    }

                    case EffectKind.Stun:
                        score += subject.Status.Stun > 0 ? 0 : StunValue * effect.Turns;
                        break;

                    case EffectKind.Ct:
                    {
                        // ⚠️ 枠1には効かないので、枠2・3 が実際に動くぶんだけ価値がある
                        int moved = 0;
                        for (int i = 1; i < subject.Cooldowns.Length; i++)
                        {
                            int now = subject.Cooldowns[i];
                            moved += Math.Abs(Math.Max(0, now + effect.Delta) - now);
                        }
                        score += moved * CtValue;
                        break;
                    }

                    case EffectKind.Taunt:
                        // ⭐ 相手に付ける弱化になった。まだ縛られていない相手にだけ価値がある
                        score += subject.Status.Taunt == 0 ? effect.Hits * TauntValue : 0;
                        break;

                    case EffectKind.Gauge:
                    {
                        // ⚠️ 減らす側は「相手が溜めている分」までしか削れない
                        double moved = effect.Percent < 0
                            ? Math.Min(-effect.Percent, subject.Gauge * 100.0 / Battle.GaugeMax)
                            : effect.Percent;
                        score += moved * GaugeValue;
                        break;
                    }

                    case EffectKind.Sleep:
                        // ⚠️ 殴ると解けるのでスタンより安く見る
                        score += subject.Status.Sleep == 0 ? effect.Turns * StunValue * 0.6 : 0;
                        break;

                    case EffectKind.Block:
                        // ⭐ 相手が回復や強化で立て直す前に置く
                        score += subject.Status.Block == 0 ? effect.Turns * GuardianValue * 0.8 : 0;
                        break;

                    case EffectKind.Dispel:
                    case EffectKind.Steal:
                    {
                        // ⭐ 乗っている強化の個数だけ価値がある。何も乗っていなければ 0
                        int boons = 0;
                        foreach (var key in Stats.Keys)
                        {
                            if (key == StatKey.Hp) continue;
                            ref var mod = ref subject.Status.ModOf(key);
                            if (mod.Turns > 0 && mod.Percent > 0) boons++;
                        }
                        if (subject.Status.Shield > 0) boons++;
                        if (subject.Status.Guts > 0) boons++;
                        if (subject.Status.Immune > 0) boons++;
                        if (subject.Status.Regen.Turns > 0) boons++;
                        int take = Math.Min(boons, effect.Count);
                        score += take * DispelValue * (effect.Kind == EffectKind.Steal ? 2 : 1);
                        break;
                    }

                    case EffectKind.Revive:
                        // ⚠️ 倒れた味方が居るときだけ
                        score += IsAnyDown(state, actor) ? ReviveValue : 0;
                        break;

                    case EffectKind.Guts:
                    {
                        // 追い詰められているときだけ価値がある
                        bool hurt = (double)actor.Hp / actor.MaxHp < 0.5;
                        score += hurt && actor.Status.Guts == 0 ? GuardianValue : 0;
                        break;
                    }

                    case EffectKind.Immune:
                        // 既に弱化を受けているなら、掛け直しても消えないので価値は低い
                        score += actor.Status.Immune == 0 ? GuardianValue : 0;
                        break;

                    // ⚠️ 効果を足したのにここへ来ないと、その技のスコアは 0 のまま。
                    //    コンパイルは通り、検査も通り、**AI が永久にその技を選ばない**だけになる。
                    //    「型は通る・ただ効かなくなる」が一番気づけない形なので必ず投げる。
                    default:
                        throw new ArgumentOutOfRangeException(nameof(effect.Kind),
                            $"{effect.Kind} を AI が採点できない。ScoreOf に case を足す");
                }

                if (land < 1.0) score = before + (score - before) * land;
            }
            return score;
        }

        /// <summary>その効果を AI が採点できるか。⭐ 技を足す前に <see cref="Skills.Audit"/> が数える。
        /// ⚠️ 上の switch に case を足したら、ここにも足す。
        /// 二重管理だが、実際に打たせてみないと分からない状態よりは良い。</summary>
        public static bool Knows(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.Damage:
                case EffectKind.Buff:
                case EffectKind.Poison:
                case EffectKind.Regen:
                case EffectKind.HealRatio:
                case EffectKind.Shield:
                case EffectKind.Stun:
                case EffectKind.Ct:
                case EffectKind.Taunt:
                case EffectKind.Guts:
                case EffectKind.Immune:
                case EffectKind.Gauge:
                case EffectKind.Sleep:
                case EffectKind.Block:
                case EffectKind.Dispel:
                case EffectKind.Steal:
                case EffectKind.Revive:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>⚠️ 同点は並び順で決める。実行ごとに変わると比較にならない。
        /// ⭐ 枠1（種族固定）は CT 0 なので必ず使える。既定の手はこれ。</summary>
        public static int ChooseAction(BattleState state, Unit actor)
        {
            int best = 0;
            double bestScore = double.NegativeInfinity;

            for (int slot = 0; slot < 3; slot++)
            {
                if (!Battle.IsUsable(actor, slot)) continue;
                double score = ScoreOf(state, actor, slot);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = slot;
                }
            }
            return best;
        }
    }
}
