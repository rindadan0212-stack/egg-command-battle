#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace EggCommand.Core
{
    /// <summary>技と効果を**日本語で書く**唯一の場所。
    ///
    /// ⚠️ Wiki の生成器・図鑑・画面がそれぞれ言い回しを持っていたため、
    /// 同じ効果が「盾2枚」「シールド 2枚」「免疫（0回）」と3通りに出ていた
    /// （知らない効果を既定で「免疫」と書く不具合まで混ざっていた）。
    /// ⭐ **語彙をここ1つに集める。**足した効果はここに書かなければ落ちる。
    ///
    /// 効果文の形（作者の指定 2026-08-18）:
    /// <code>
    ///   敵1体に攻撃力DOWN30%を80%の確率で2T付与し、攻撃する
    ///   味方全体のゲージを20%上昇させ、リジェネを3T付与する
    /// </code>
    /// ⚠️ **T ＝ その個体の行動回数**（実時間でも全体のターン数でもない）。
    /// </summary>
    public static class SkillText
    {
        /// <summary>効果の名前だけ。⚠️ 数を混ぜない。
        ///
        /// ⚠️ 「HP割合回復・大だから55%」ではない。⭐ **割合も持続も技ごとの設定**で、
        /// 名前は種類を指すだけ。名前に数の意味を持たせると、名前が嘘をつき始める。</summary>
        public static string NameOf(Effect effect)
        {
            switch (effect.Kind)
            {
                case EffectKind.Damage: return "攻撃";
                case EffectKind.Buff:
                    return Stats.LabelOf(effect.Stat) + (effect.Sign > 0 ? "UP" : "DOWN");
                case EffectKind.Poison: return "毒";
                case EffectKind.Regen: return "リジェネ";
                case EffectKind.HealRatio: return "HP割合回復";
                case EffectKind.Shield: return "シールド";
                case EffectKind.Stun: return "スタン";
                case EffectKind.Sleep: return "睡眠";
                case EffectKind.Block: return "ブロック";
                case EffectKind.Ct: return effect.Delta < 0 ? "CT短縮" : "CT延長";
                case EffectKind.Gauge: return effect.Percent < 0 ? "ゲージ減少" : "ゲージ上昇";
                case EffectKind.Taunt: return "挑発";
                case EffectKind.Guts: return "ガッツ";
                case EffectKind.Immune: return "免疫";
                // ⭐ 個数が負なら**弱化のほう**を落とす（⚠️ 名前も逆になる）
                case EffectKind.Dispel: return effect.Count < 0 ? "弱化解除" : "強化解除";
                case EffectKind.Steal: return "強化強奪";
                case EffectKind.Revive: return "蘇生";
                // ⚠️ 黙って既定に落とさない。足した効果が名無しのまま表に出るのを防ぐ
                default: throw new ArgumentOutOfRangeException(
                    nameof(effect), effect.Kind, "名前の無い効果");
            }
        }

        public static string TargetOf(Target target)
        {
            switch (target)
            {
                case Target.EnemyOne: return "敵1体";
                case Target.EnemyAll: return "敵全体";
                case Target.EnemyRandom: return "敵のだれか1体";
                case Target.AllyAll: return "味方全体";
                case Target.AllyDownAll: return "倒れた味方全体";
                case Target.AllyOne: return "味方1体";
                case Target.AllyLowest: return "最も弱った味方";
                case Target.AllyDown: return "倒れた味方";
                case Target.Self: return "自分";
                default: throw new ArgumentOutOfRangeException(
                    nameof(target), target, "名前の無い狙い先");
            }
        }

        /// <summary>威力。⚠️ **ダメージのある技だけ。**他は空文字
        /// （表では空欄にする、という指定に合わせる）。</summary>
        public static string PowerOf(Skill skill)
        {
            foreach (var effect in skill.Effects)
            {
                if (effect.Kind != EffectKind.Damage) continue;
                string shots = effect.Repeat > 1 ? $" ×{effect.Repeat}発" : "";
                // ⚠️ **千分率のまま出さない。**威力は「攻撃力の何倍か」（2026-08-19 から）。
                //    生の数を出していた頃は **「威力 3000（特大）」** と表示され、
                //    3000 ダメージだと読めてしまった（実際は ×3.0）。
                double times = (double)Skills.DamagePowerOf(effect.Power) / Skills.PowerUnit;
                return $"×{times:0.0}（{Skills.LabelOf(effect.Power)}）{shots}";
            }
            return "";
        }

        /// <summary>レベルごとの上昇量。⭐ Lv2→Lv5 の4段を「→」で並べる。</summary>
        public static string GrowthOf(Skill skill)
        {
            var names = new List<string>();
            foreach (var gain in Skills.GrowthOf(skill)) names.Add(Skills.LabelOf(gain));
            return names.Count == 0 ? "" : string.Join(" → ", names.ToArray());
        }

        /// <summary>その軸を1段上げたときの**上がり幅**。⭐ 数まで言う。
        ///
        /// ⚠️ <see cref="GrowthOf"/> は軸の名前だけ（表の列が狭いので）。
        /// こちらは札を長押しして読むところで使う ── 「個数」だけでは、
        /// いくつ増えるのか分からない。
        ///
        /// ⚠️ **数を書き写さない。**<see cref="Skills"/> の定数から出す。</summary>
        public static string GainOf(SkillGain gain)
        {
            switch (gain)
            {
                case SkillGain.Power: return $"威力+{Skills.GainPowerPercent}%";
                case SkillGain.Ct: return "CT−1";
                case SkillGain.Chance: return $"確率+{Skills.GainChancePoints}pt";
                case SkillGain.Turns: return "持続+1";
                case SkillGain.Repeat: return "発数+1";
                case SkillGain.Percent: return $"割合+{Skills.GainHealPoints}pt";
                case SkillGain.Count: return "個数+1";
                case SkillGain.Amount: return "量+1";
                default: throw new ArgumentOutOfRangeException(nameof(gain), gain, "名前の無い軸");
            }
        }

        /// <summary>Lv ごとに何が上がるか。⭐ **どの Lv で何が上がるか**まで並べる。
        /// ⚠️ 「→」で繋いだだけだと、何段目の話か数えないと分からない。</summary>
        public static string StepsOf(Skill skill)
        {
            var gains = Skills.GrowthOf(skill);
            if (gains.Count == 0) return "";
            var parts = new List<string>();
            for (int i = 0; i < gains.Count; i++)
            {
                parts.Add($"Lv{i + 2} {GainOf(gains[i])}");
            }
            return string.Join("　", parts.ToArray());
        }

        // ── 効果文 ──────────────────────────────────────

        /// <summary>効果1つぶんの言い方。⭐ 助詞と動詞を**節ごとに**持たせる。
        ///
        /// ⚠️ 文全体で助詞を1つに決めると「味方1体にHPを回復する」のように崩れる。
        /// 付与するものは「〜に」、増減させるものは「〜の」。</summary>
        private struct Clause
        {
            /// <summary>狙い先に続く助詞。「に」か「の」か「を」。</summary>
            public string Particle;
            /// <summary>動詞の手前まで。</summary>
            public string Body;
            /// <summary>サ変動詞の語幹（付与 / 回復 / 上昇 …）。</summary>
            public string Verb;
            /// <summary>「する」ではなく「させる」で活用するか。</summary>
            public bool Causative;

            public string Ending(bool last) => Causative
                ? Body + Verb + (last ? "させる" : "させ")
                : Body + Verb + (last ? "する" : "し");
        }

        /// <summary>技が何をするかの1文。⭐ 狙い先・確率・持続をすべて含める。</summary>
        public static string Describe(Skill skill)
        {
            var main = new List<Effect>();
            foreach (var effect in skill.Effects)
            {
                if (effect.Own == null) main.Add(effect);
            }
            var sb = new StringBuilder(Sentence(skill.Id, skill.Target, main));
            // ⭐ **1手2役は別の文にする。**⚠️ 同じ文へ混ぜると狙い先が2つある文になり、
            //    「敵全体に自分を回復し」のような読めない並びになる。
            foreach (var effect in skill.Effects)
            {
                if (effect.Own == null) continue;
                sb.Append("、さらに")
                  .Append(Sentence(skill.Id, effect.Own.Value, new List<Effect> { effect }));
            }
            return sb.ToString();
        }

        /// <summary>1つの狙い先ぶんの文。</summary>
        private static string Sentence(string id, Target target, List<Effect> effects)
        {
            var clauses = new List<Clause>();
            foreach (var effect in effects)
            {
                // ⭐ ダメージは最後に回す。状態を付けてから殴る、という起きる順に合わせる
                if (effect.Kind == EffectKind.Damage) continue;
                clauses.Add(StateClause(effect));
            }
            foreach (var effect in effects)
            {
                if (effect.Kind == EffectKind.Damage) clauses.Add(AttackClause(effect));
            }
            if (clauses.Count == 0)
            {
                // ⚠️ 効果の無い技は表に出す前に気づきたい
                throw new InvalidOperationException($"{id}: 効果が1つも無い");
            }

            var sb = new StringBuilder();
            sb.Append(TargetOf(target)).Append(clauses[0].Particle);
            for (int i = 0; i < clauses.Count; i++)
            {
                if (i > 0) sb.Append('、');
                sb.Append(clauses[i].Ending(i == clauses.Count - 1));
            }
            return sb.ToString();
        }

        private static Clause AttackClause(Effect effect)
        {
            var how = new List<string>();
            // ⚠️ 攻撃依存は既定なので言わない（全技に付いてうるさい）
            if (effect.Scale != DamageScale.Atk)
                how.Add(Skills.LabelOf(effect.Scale) + "で伸びる");
            if (effect.Pierce) how.Add("防御力を無視する");
            string shots = effect.Repeat > 1 ? $"{effect.Repeat}回" : "";
            // ⚠️ 「攻撃を2回する」。修飾が付くときだけ「〜攻撃を」の形にする
            string body = how.Count == 0
                ? shots
                : string.Join("・", how.ToArray()) + "攻撃を" + shots;
            return new Clause
            {
                // ⚠️ 修飾が付くと「敵1体を防御力で伸びる攻撃をする」と を が二重になる。
                //    ⭐ 素の攻撃は「敵1体を攻撃する」、修飾つきは「敵1体に〜攻撃をする」
                Particle = how.Count == 0 ? "を" : "に",
                Body = body,
                Verb = how.Count == 0 ? "攻撃" : "",
                Causative = false,
            };
        }

        /// <summary>持続の書き方。⚠️ 負は <see cref="Skills.Lasting"/>（切れない）。
        /// ⭐ 「-1T」と出さないための唯一の出所。</summary>
        private static string Lasts(int turns) => turns < 0 ? "戦闘の間ずっと" : $"{turns}T";

        private static Clause StateClause(Effect effect)
        {
            string name = NameOf(effect);
            string chance = effect.Chance >= 100 ? "" : $"{effect.Chance}%の確率で";

            switch (effect.Kind)
            {
                case EffectKind.HealRatio:
                    return Of("の", $"HPを{chance}{effect.Percent}%", "回復");
                case EffectKind.Revive:
                    return Of("を", $"{chance}HP{effect.Percent}%で", "蘇生", causative: true);
                case EffectKind.Gauge:
                {
                    int amount = effect.Percent < 0 ? -effect.Percent : effect.Percent;
                    string way = effect.Percent < 0 ? "減少" : "上昇";
                    return Of("の", $"ゲージを{chance}{amount}%", way, causative: true);
                }
                case EffectKind.Ct:
                {
                    // ⚠️ **動くのは枠1 以外の全部。**1枠だけと読めると、
                    //    「どの技が止まるのか」の見積もりが外れる（枠1 は CT が無いので動かない）
                    int amount = effect.Delta < 0 ? -effect.Delta : effect.Delta;
                    return Of("の", $"全スキルのCTを{chance}{amount}", effect.Delta < 0 ? "短縮" : "延長");
                }
                case EffectKind.Dispel:
                {
                    string what = effect.Count < 0 ? "弱化" : "強化";
                    int many = effect.Count < 0 ? -effect.Count : effect.Count;
                    return Of("の", $"{what}を{chance}{many}個", "解除");
                }
                case EffectKind.Steal:
                    return Of("の", $"強化を{chance}{effect.Count}個", "強奪");
                case EffectKind.Shield:
                    return Of("に", $"{name}を{chance}{effect.Count}枚", "付与");
                case EffectKind.Poison:
                case EffectKind.Regen:
                {
                    // ⭐ 重なる効果は枚数と持続の両方を書く
                    string stacks = effect.Stacks > 1 ? $"×{effect.Stacks}" : "";
                    return Of("に", $"{name}{stacks}を{chance}{Lasts(effect.Turns)}", "付与");
                }
                case EffectKind.Buff:
                    // ⚠️ 効き目（±30%）は**ゲーム全体で固定**なので文には書かない。
                    //    技ごとに変わらない数を毎行に書くと、変わる数（確率・持続）が埋もれる。
                    //    ⭐ 数そのものは Wiki の[効果の種類]に1度だけ書いてある。
                    return Of("に", $"{name}を{chance}{Lasts(effect.Turns)}", "付与");
                case EffectKind.Taunt:
                    // ⚠️ **挑発だけ「回」。**T は「その個体の行動回数」だが、挑発が数えるのは
                    //    **相手が単体技を撃った回数**（全体技や自分に掛ける技では減らない）。
                    //    T と書いていた頃は、技一覧の T の定義と意味が食い違っていた。
                    return Of("に", $"{name}を{chance}{effect.Hits}回", "付与");
                default:
                    // スタン・睡眠・ブロック・ガッツ・免疫
                    return Of("に", $"{name}を{chance}{Lasts(effect.Turns)}", "付与");
            }
        }

        private static Clause Of(string particle, string body, string verb, bool causative = false) =>
            new Clause { Particle = particle, Body = body, Verb = verb, Causative = causative };
    }
}
