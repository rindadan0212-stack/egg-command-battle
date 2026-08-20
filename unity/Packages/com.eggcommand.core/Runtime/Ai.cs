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
        // ⚠️ **固定値の単位は「実HP」。**採点の土俵はダメージ・回復・毒の見積もり（＝実HP）なので、
        //    固定値も同じ桁に乗せないと比べ物にならない。
        //    ⭐ 2026-08-19 の桁上げ（実ダメージが Battle.HpSpace 倍）でここが取り残され、
        //    弱化・スタン・CT・挑発・ゲージ・強化解除・蘇生・ガッツ・免疫の23技が
        //    **採用率 0.00**（AI が一度も選ばない）になっていた ── `sim skills` で発見。
        //    数字どうしの比は較正済みのまま、倍率だけ揃えて戻す。
        //    ⚠️ 桁を動かすときは必ずこの8つも一緒に動くか確かめること（比で書いてあれば自動で動く）。

        /// <summary>ステータスを1%動かすことの価値。</summary>
        private const double BuffValue = 0.5 * Battle.HpSpace;
        /// <summary>相手の手番を1つ奪うことの価値。⭐ 行動回数は全出力への倍率なので高く見る。</summary>
        private const double StunValue = 26 * Battle.HpSpace;
        /// <summary>CT を1つ動かすことの価値。</summary>
        private const double CtValue = 6 * Battle.HpSpace;
        /// <summary>狙い先を縛る1回ぶんの価値。</summary>
        private const double TauntValue = 7 * Battle.HpSpace;
        /// <summary>ゲージを1%動かすことの価値。⭐ 手番の奪い合いなので高め。</summary>
        private const double GaugeValue = 0.35 * Battle.HpSpace;
        /// <summary>強化を1個 剥がすことの価値。⭐ 奪うほうは自分にも乗るので倍。</summary>
        private const double DispelValue = 9 * Battle.HpSpace;
        /// <summary>倒れた味方を1体戻すことの価値。</summary>
        private const double ReviveValue = 40 * Battle.HpSpace;
        /// <summary>ガッツ・免疫の価値（状況が読みにくいので控えめの固定値）。</summary>
        private const double GuardianValue = 10 * Battle.HpSpace;

        /// <summary>味方に倒れている者が居るか。⚠️ 蘇生の採点だけに使う。</summary>
        /// <summary>倒れている味方の先頭。⚠️ 居なければ null（蘇生の見積もりが 0 になる）。</summary>
        private static Unit? FirstDown(BattleState state, Side side)
        {
            Unit? found = null;
            foreach (var unit in state.Units)
            {
                if (unit.Side != side || Battle.IsAlive(unit)) continue;
                if (found == null || unit.Slot < found.Slot) found = unit;
            }
            return found;
        }

        private static bool IsAnyDown(BattleState state, Unit actor)
        {
            foreach (var unit in state.Units)
            {
                if (unit.Side == actor.Side && !Battle.IsAlive(unit)) return true;
            }
            return false;
        }

        private static int EstimateDamage(Unit actor, Unit target, PowerTier tier, DamageScale scale,
            bool pierce = false)
        {
            var a = Creatures.StatsOf(actor.Creature);
            var t = Creatures.StatsOf(target.Creature);
            int attackStat = Battle.AttackStatOf(a, actor.Status, scale);
            // ⚠️ 防御無視をここで数えないと、AI から見て「防御無視攻撃」が素の攻撃と同じ値になり、
            //    CT の短い技に必ず負けて**一度も選ばれない**（実測 0.00 だった）
            int defenseStat = pierce ? 0 : Battle.EffectiveStat(t.Def, target.Status.Def);
            double mult = Battle.ElementMultiplier(
                actor.Creature.Element,
                target.Creature.Element);
            return Battle.DamageOf(Skills.DamagePowerOf(tier), attackStat, defenseStat, mult);
        }

        /// <summary>多段ぶんを見込んだ見積り。⚠️ 盾は1発ごとに剥がれるので、
        /// 盾持ちに対しては多段のほうが通る（そこまでは数えていない — 概算でよい）。</summary>
        private static int EstimateTotal(Unit actor, Unit target, Effect effect)
        {
            return EstimateDamage(actor, target, effect.Power, effect.Scale, effect.Pierce) * effect.Repeat;
        }

        /// <summary>その者に乗っている**強化**の数。⚠️ <see cref="Battle.StripBoons"/> と
        /// 同じ面々を数える（片方だけ増やすと、AI の見積もりと実際に剥がれる数がずれる）。</summary>
        private static int BoonsOn(Unit unit)
        {
            int n = 0;
            foreach (var key in Stats.BuffKeys)
            {
                ref var mod = ref unit.Status.ModOf(key);
                if (Battle.IsOn(mod) && mod.Percent > 0) n++;
            }
            if (unit.Status.Shield > 0) n++;
            if (unit.Status.Guts > 0) n++;
            if (unit.Status.Immune > 0) n++;
            if (unit.Status.Regen.Turns > 0) n++;
            return n;
        }

        /// <summary>その者に乗っている**弱化**の数。⚠️ <see cref="Battle.StripBanes"/> と同じ面々。</summary>
        private static int BanesOn(Unit unit)
        {
            int n = 0;
            foreach (var key in Stats.BuffKeys)
            {
                ref var mod = ref unit.Status.ModOf(key);
                if (Battle.IsOn(mod) && mod.Percent < 0) n++;
            }
            if (unit.Status.Stun > 0) n++;
            if (unit.Status.Sleep > 0) n++;
            if (unit.Status.Poison.Turns > 0) n++;
            if (unit.Status.Taunt > 0) n++;
            if (unit.Status.Block > 0) n++;
            return n;
        }

        private static double ScoreOf(BattleState state, Unit actor, int slot)
        {
            var skill = Battle.ActionSkill(actor, slot);
            // ⭐ 本体（技の狙い先へ飛ぶぶん）
            double total = ScoreGroup(state, actor, skill, skill.Target, null);
            // ⭐ **1手2役のぶんも足す。**⚠️ 足さないと AI から見て
            //    「回復が付いている技」と「付いていない技」が同点になり、選ぶ理由が消える。
            foreach (var effect in skill.Effects)
            {
                if (effect.Own == null) continue;
                total += ScoreGroup(state, actor, skill, effect.Own.Value, effect);
            }
            return total;
        }

        /// <summary>1つの飛び先ぶんを採点する。</summary>
        /// <param name="target">この回で見る飛び先。⚠️ <c>skill.Target</c> とは限らない。</param>
        /// <param name="only">null なら「飛び先を持たない効果」を全部。
        /// ⭐ 指定があればその1つだけ（1手2役の後半）。</param>
        private static double ScoreGroup(BattleState state, Unit actor, Skill skill,
            Target target, Effect? only)
        {
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
            // ⚠️ AllyDown を focus（敵）にしていると、蘇生を敵に撃つ見積もりになる
            var downed = FirstDown(state, actor.Side);
            // ⚠️ **味方に配る技は Battle に聞く。**ここで「一番弱った味方」と決め打ちしていた頃は、
            //    実際の配り先（伸ばす札はそのステが一番高い味方）とずれていて、
            //    「もう掛かっているか」の判定が常に別人を見ていた。
            var subject = target == Target.Self ? actor
                : target == Target.AllyLowest || target == Target.AllyOne
                    ? Battle.AllyLandingFor(state, actor, skill) ?? weakest
                : target == Target.AllyDown ? downed
                // ⭐ 味方全体は「一番弱った味方」を代表にして測る
                : target == Target.AllyAll ? weakest
                // ⚠️ **ランダムは狙えない。**`focus`（一番弱った敵）にしていた頃、
                //    単体技と**完全に同点**になっていた ── 瀕死が1体居ると
                //    「その1体の残HP」まで値打ちが落ち、居なければ狙えるのと同じ値だった
                //    （2026-08-19 の監査）。⭐ 真ん中の相手を代表にする。
                : target == Target.EnemyRandom ? byHp[byHp.Count / 2]
                : focus;
            // ⚠️ 倒れた味方が居ないなら蘇生は0点（撃っても何も起きない）
            if (target == Target.AllyDown && subject == null) return 0;

            // ⚠️ **全体に効く技は、ダメージ以外も対象数ぶん効く。**
            //    ⭐ ダメージだけ `foes` を回して足していたので、毒・弱化・スタン・ゲージ・
            //    強化解除の全体版は**1体ぶんの見積もり**になり、単体版に負けていた。
            //    実測（2026-08-19 の監査）: 毒・全体 3,326 対 毒 9,240 で逆転し、
            //    `sim skills` で**採用率 0.00**（57技で唯一）だった。
            // ⚠️ ダメージは上で残HPの頭打ちを入れているので、ここでは掛けない。
            // ⚠️ **味方全体を「代表1体 × 人数」で測らない。**
            //    ⭐ 味方に配るものは対象ごとに頭打ち（満タンには回復が乗らない・
            //    既に盾がある相手には無駄）があるので、代表を人数倍すると桁が狂う。
            //    実測（2026-08-19 の監査）: 満タン2体＋瀕死1体に全体回復を撃つと、
            //    実際の値打ち 8,505 に対し **25,515**（3倍）と見積もっていた。
            //    逆に代表が盾持ちだと、他2体が裸でも **0点**になっていた。
            // ⭐ だから「配るもの」は下で1体ずつ回す。ここでは掛けない。
            var spreadOver = target == Target.EnemyAll ? foes
                : target == Target.AllyAll ? friends : null;

            // ⭐ **「何体に効くか」で数える。**⚠️ 人数をそのまま掛けない。
            //    実測（2026-08-19 の監査）: 満タン2体＋瀕死1体に全体回復を撃つと、
            //    実際の値打ち 8,505 に対し **25,515**（3倍）と見積もっていた。
            //    逆に代表が盾持ちだと、他2体が裸でも **0点**になっていた。
            int Useful(Effect e)
            {
                if (spreadOver == null) return 1;
                int n = 0;
                foreach (var one in spreadOver)
                {
                    switch (e.Kind)
                    {
                        // ⚠️ 満タンの相手に回復は乗らない
                        case EffectKind.HealRatio:
                        case EffectKind.Regen:
                            if (one.Hp < one.MaxHp) n++;
                            break;
                        // ⚠️ もう持っている相手には掛け直すだけ
                        case EffectKind.Shield: if (one.Status.Shield == 0) n++; break;
                        case EffectKind.Guts: if (one.Status.Guts == 0) n++; break;
                        case EffectKind.Immune: if (one.Status.Immune == 0) n++; break;
                        case EffectKind.Block: if (one.Status.Block == 0) n++; break;
                        case EffectKind.Stun: if (one.Status.Stun == 0) n++; break;
                        case EffectKind.Sleep: if (one.Status.Sleep == 0) n++; break;
                        default: n++; break;
                    }
                }
                return n;
            }

            double score = 0;
            foreach (var effect in skill.Effects)
            {
                // ⭐ この回で見るぶんだけ。⚠️ 分けずに全部足すと、飛び先の違う効果を
                //    **この回の相手**に当てた前提で数えてしまう（自分への回復を敵の残HPで測る等）
                if (only == null ? effect.Own != null : !ReferenceEquals(effect, only)) continue;
                // ⭐ 外れる技は、外れるぶん安く見積もる。
                //    ⚠️ これが無いと AI が「必ず通る前提」で弱化を選び続ける
                double land = Battle.LandChanceOf(effect, actor, subject) / 100.0;
                double before = score;
                // ⭐ ダメージは自前で全員ぶんを足すので、掛け算の対象から外す
                int fanOut = effect.Kind == EffectKind.Damage ? 1 : Useful(effect);


                switch (effect.Kind)
                {
                    case EffectKind.Damage:
                        if (target == Target.EnemyAll)
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
                        // ⭐ 落とせるものが何個乗っているかだけが価値。0 なら撃つ意味が無い。
                        // ⚠️ **個数が負なら見るのは弱化のほう**（弱化解除）。
                        //    符号を見ずに Math.Min していた頃は take が負になり、
                        //    AI から見て弱化解除は**撃つほど損な技**だった。
                        bool undo = effect.Count < 0;
                        int found = undo ? BanesOn(subject) : BoonsOn(subject);
                        int take = Math.Min(found, undo ? -effect.Count : effect.Count);
                        score += take * DispelValue * (effect.Kind == EffectKind.Steal ? 2 : 1);
                        break;
                    }

                    case EffectKind.Revive:
                        // ⚠️ 倒れた味方が居るときだけ
                        score += IsAnyDown(state, actor) ? ReviveValue : 0;
                        break;

                    // ⚠️ **どちらも subject（掛かる相手）を見る。**actor（掛ける本人）を
                    //    見ていた頃は、味方1体に配る技なのに自分の状態で判断していたので、
                    //    ガッツは「自分が瀕死でなければ0点」＝味方がどれだけ瀕死でも撃たず、
                    //    免疫は自分に付いていれば味方が丸裸でも撃たなかった。
                    case EffectKind.Guts:
                    {
                        // 追い詰められているときだけ価値がある
                        bool hurt = (double)subject.Hp / subject.MaxHp < 0.5;
                        score += hurt && subject.Status.Guts == 0 ? GuardianValue : 0;
                        break;
                    }

                    case EffectKind.Immune:
                        // 既に免疫が付いているなら、掛け直しても増えないので価値は低い
                        score += subject.Status.Immune == 0 ? GuardianValue : 0;
                        break;

                    // ⚠️ 効果を足したのにここへ来ないと、その技のスコアは 0 のまま。
                    //    コンパイルは通り、検査も通り、**AI が永久にその技を選ばない**だけになる。
                    //    「型は通る・ただ効かなくなる」が一番気づけない形なので必ず投げる。
                    default:
                        throw new ArgumentOutOfRangeException(nameof(effect.Kind),
                            $"{effect.Kind} を AI が採点できない。ScoreOf に case を足す");
                }

                // ⭐ 外れるぶんを割り引き、全体に効くものは対象数ぶん掛ける
                score = before + (score - before) * land * fanOut;
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
