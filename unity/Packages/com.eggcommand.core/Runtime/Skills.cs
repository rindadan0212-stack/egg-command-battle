#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>誰に効くか。</summary>
    public enum Target
    {
        /// <summary>敵1体</summary>
        EnemyOne,
        /// <summary>敵全体</summary>
        EnemyAll,
        /// <summary>残 HP 割合が最も低い味方（自分を含む）</summary>
        AllyLowest,
        Self,
        /// <summary>倒れている味方。⚠️ 蘇生のためだけの狙い先。居なければ何も起きない。</summary>
        AllyDown,
    }

    /// <summary>効き目の段位。
    ///
    /// ⭐ 技ごとに数値を置かない。段位を選ぶだけにする。
    /// 独立した数値が「技の数」から効果の種類ごとに4つまで減り、
    /// 較正は表を動かすだけで済む（勘で置いた数値が散らばらない）。
    ///
    /// ⚠️ 全体に効くものは1段下げて選ぶ。
    /// 全体の「中」は単体の「中」よりずっと強いので、同じ段位にすると段位が意味を失う。</summary>
    public enum PowerTier
    {
        Small,
        Medium,
        Large,
        Huge,
    }

    /// <summary>ダメージが何のステで伸びるか。</summary>
    public enum DamageScale
    {
        Atk,
        Def,
    }

    public enum EffectKind
    {
        Damage,
        Buff,
        Poison,
        Regen,
        HealRatio,
        Shield,
        Stun,
        Ct,
        Taunt,
        Guts,
        Immune,
        /// <summary>ゲージを増やす／減らす。⭐ <see cref="Effect.Percent"/> が符号付きの割合。</summary>
        Gauge,
        /// <summary>睡眠。⚠️ **攻撃を受けると即座に解ける。**
        /// ⭐ スタンとの違いはここだけ ── 眠らせた相手を殴ると自分で起こしてしまう。</summary>
        Sleep,
        /// <summary>ブロック。⭐ **外から受け取る回復と強化を無効化する。**
        /// ⚠️ 自然に溜まるゲージと自然に減る CT は止めない（止まるのは「買った分」だけ）。</summary>
        Block,
        /// <summary>強化解除。⭐ 相手に乗っている強化を <see cref="Effect.Count"/> 個消す。</summary>
        Dispel,
        /// <summary>強化強奪。⭐ 消すのではなく**自分へ移す**。</summary>
        Steal,
        /// <summary>蘇生。⚠️ 倒れた味方を <see cref="Effect.Percent"/>% の HP で戻す。</summary>
        Revive,
    }

    /// <summary>効果のプリミティブ。
    ///
    /// ⚠️ ここを増やすときは、本当に組み合わせで表せないか先に疑う。
    /// ⚠️ 持続するものの単位は「その個体の行動回数」。CT と同じ数え方に揃えてある。
    ///
    /// 種類ごとに型を分けず1つの型に畳んであるのは、TS 側が判別共用体の**データ表**として
    /// 持っているのと同じ形にするため。作り方は下の静的メソッドに寄せて、
    /// 意味の無い組み合わせを外から作れないようにしている。</summary>
    public sealed class Effect
    {
        /// <summary>確率の下限。⚠️ これより低いと「たまたま通った」だけの技になり、
        /// 選ぶ判断ができなくなる。</summary>
        public const int MinChance = 20;

        public readonly EffectKind Kind;
        /// <summary>damage</summary>
        public readonly PowerTier Power;
        /// <summary>damage</summary>
        public readonly DamageScale Scale;
        /// <summary>buff</summary>
        public readonly StatKey Stat;
        /// <summary>buff: +1 で UP、-1 で DOWN</summary>
        public readonly int Sign;
        /// <summary>buff / poison / regen / stun / guts / immune</summary>
        public readonly int Turns;
        /// <summary>poison / regen。⭐ スタックする</summary>
        public readonly int Stacks;
        /// <summary>healRatio。⚠️ 技ごとに割合が違う（段位を使わない）</summary>
        public readonly int Percent;
        /// <summary>shield。⭐ 点数ではなく枚数</summary>
        public readonly int Count;
        /// <summary>ct。負で短縮・正で延長</summary>
        public readonly int Delta;
        /// <summary>taunt</summary>
        public readonly int Hits;
        /// <summary>damage。⭐ **1回の技で何発当てるか。**
        ///
        /// ⭐ これを足すだけで「連続攻撃」「追撃」が段位の掛け算で書ける。
        /// **新しい効果の種類を足さずに**表現が増えるのがこの欄の狙い。
        /// ⭐ 盾は1発につき1枚剥がれるので、多段は「大きな一撃」と違う役割を持つ。
        /// ⚠️ ダメージそのものに外れは無い（<see cref="Chance"/> が付かない唯一の効果）。</summary>
        public readonly int Repeat;

        /// <summary>damage。⭐ **防御を無視して当てる。**
        /// ⚠️ 効果の種類を増やさず、ダメージの性質として持つ
        /// （「防御無視の攻撃」であって「防御無視」という別の効果ではない）。
        /// ⚠️ 盾は無視しない ── 盾を抜くのは手数の仕事。</summary>
        public readonly bool Pierce;

        /// <summary>効果が通る率（%）。⭐ 100 なら必ず通る（乱数を1度も引かない）。
        ///
        /// ⭐ **効果量と確率をトレードオフにする欄。**
        /// 「効き目は小さいが必ず通る」と「効き目は大きいが半分外す」を、
        /// 同じ効果の種類のまま**別の技として並べられる**。技を増やす軸がこれで1本増える。
        ///
        /// ⚠️ 確率が付くのは **ダメージと強化以外**（弱化・状態異常・回復・盾・挑発・ガッツ・免疫・CT）。
        /// ダメージに外れを作ると、攻撃役の出力が運で決まってしまう。
        /// 強化（自分に掛ける側）は外す意味が無い。
        ///
        /// ⚠️ **相手に掛けるもの**だけ、実際の率が速度差で上下する（<see cref="Battle.LandChanceOf"/>）。
        /// 自分・味方に掛けるものは速度と関係なく、素の率がそのまま「賭け」になる。
        ///
        /// ⚠️ **100 のときは乱数を引かない。** これで移植した21技の試合は
        /// 1手も変わらず、較正済みの照合がそのまま生きる。</summary>
        public readonly int Chance;

        private Effect(EffectKind kind, PowerTier power, DamageScale scale, StatKey stat, int sign,
            int turns, int stacks, int percent, int count, int delta, int hits, int repeat = 1,
            int chance = 100, bool pierce = false)
        {
            Pierce = pierce;
            Repeat = repeat < 1 ? 1 : repeat;
            Chance = chance < MinChance ? MinChance : chance > 100 ? 100 : chance;
            Kind = kind;
            Power = power;
            Scale = scale;
            Stat = stat;
            Sign = sign;
            Turns = turns;
            Stacks = stacks;
            Percent = percent;
            Count = count;
            Delta = delta;
            Hits = hits;
        }

        /// <summary>scale が Def のものは「防御が高いほど強い一撃」になる。</summary>
        public static Effect Damage(PowerTier power, DamageScale scale, int repeat = 1,
            bool pierce = false) =>
            new Effect(EffectKind.Damage, power, scale, default, 0, 0, 0, 0, 0, 0, 0, repeat,
                100, pierce);

        /// <summary>攻撃力/防御力/スピードの UP・DOWN。⚠️ 効き目は一律 <see cref="Skills.BuffPercent"/>。段位は使わない。</summary>
        public static Effect Buff(StatKey stat, int sign, int turns, int chance = 100)
        {
            if (stat != StatKey.Atk && stat != StatKey.Def && stat != StatKey.Spd)
                throw new ArgumentException($"buff は atk/def/spd のみ（{stat} が渡された）");
            if (sign != 1 && sign != -1)
                throw new ArgumentException($"buff の sign は ±1（{sign} が渡された）");
            // ⚠️ 強化（自分に掛ける側）に確率は要らない。外す意味が無い
            return new Effect(EffectKind.Buff, default, default, stat, sign, turns, 0, 0, 0, 0, 0,
                1, sign > 0 ? 100 : chance);
        }

        /// <summary>毒。1行動ごとに最大HPの TickPercent × スタック数 ぶん減る。</summary>
        public static Effect Poison(int stacks, int turns, int chance = 100) =>
            new Effect(EffectKind.Poison, default, default, default, 0, turns, stacks, 0, 0, 0, 0,
                1, chance);

        /// <summary>リジェネ。1行動ごとに回復。</summary>
        public static Effect Regen(int stacks, int turns, int chance = 100) =>
            new Effect(EffectKind.Regen, default, default, default, 0, turns, stacks, 0, 0, 0, 0,
                1, chance);

        /// <summary>HP割合回復。即時。</summary>
        public static Effect HealRatio(int percent, int chance = 100) =>
            new Effect(EffectKind.HealRatio, default, default, default, 0, 0, 0, percent, 0, 0, 0,
                1, chance);

        /// <summary>シールド。1回の攻撃につき1枚消費し、その攻撃を威力に関係なく完全に無効化する。
        /// ⭐ つまり「大きな一撃」に強く、「手数」に弱い。</summary>
        public static Effect Shield(int count, int chance = 100) =>
            new Effect(EffectKind.Shield, default, default, default, 0, 0, 0, 0, count, 0, 0,
                1, chance);

        /// <summary>スタン。その回数ぶん手番を飛ばす。</summary>
        public static Effect Stun(int turns, int chance = 100) =>
            new Effect(EffectKind.Stun, default, default, default, 0, turns, 0, 0, 0, 0, 0,
                1, chance);

        /// <summary>CT短縮（負）/ CT延長（正）。⚠️ 枠1には効かない。</summary>
        public static Effect Ct(int delta, int chance = 100) =>
            new Effect(EffectKind.Ct, default, default, default, 0, 0, 0, 0, 0, delta, 0,
                1, chance);

        /// <summary>挑発。味方への単体攻撃を、あと hits 回ぶん自分が引き受ける。</summary>
        public static Effect Taunt(int hits, int chance = 100) =>
            new Effect(EffectKind.Taunt, default, default, default, 0, 0, 0, 0, 0, 0, hits,
                1, chance);

        /// <summary>ガッツ。致死のダメージを HP1 で耐える。</summary>
        public static Effect Guts(int turns, int chance = 100) =>
            new Effect(EffectKind.Guts, default, default, default, 0, turns, 0, 0, 0, 0, 0,
                1, chance);

        /// <summary>免疫。弱化を受けない。</summary>
        public static Effect Immune(int turns, int chance = 100) =>
            new Effect(EffectKind.Immune, default, default, default, 0, turns, 0, 0, 0, 0, 0,
                1, chance);

        /// <summary>ゲージを動かす。⚠️ <paramref name="percent"/> は符号付き
        /// （+ で上昇・− で減少）。満タンに対する割合。</summary>
        public static Effect Gauge(int percent, int chance = 100) =>
            new Effect(EffectKind.Gauge, default, default, default, 0, 0, 0, percent, 0, 0, 0,
                1, chance);

        /// <summary>睡眠。⚠️ 攻撃を受けると即座に解ける。</summary>
        public static Effect Sleep(int turns, int chance = 100) =>
            new Effect(EffectKind.Sleep, default, default, default, 0, turns, 0, 0, 0, 0, 0,
                1, chance);

        /// <summary>ブロック。外から受け取る回復と強化を無効化する。</summary>
        public static Effect Block(int turns, int chance = 100) =>
            new Effect(EffectKind.Block, default, default, default, 0, turns, 0, 0, 0, 0, 0,
                1, chance);

        /// <summary>強化解除。相手の強化を <paramref name="count"/> 個消す。</summary>
        public static Effect Dispel(int count, int chance = 100) =>
            new Effect(EffectKind.Dispel, default, default, default, 0, 0, 0, 0, count, 0, 0,
                1, chance);

        /// <summary>強化強奪。相手の強化を <paramref name="count"/> 個、自分へ移す。</summary>
        public static Effect Steal(int count, int chance = 100) =>
            new Effect(EffectKind.Steal, default, default, default, 0, 0, 0, 0, count, 0, 0,
                1, chance);

        /// <summary>蘇生。倒れた味方を最大HP の <paramref name="percent"/>% で戻す。</summary>
        public static Effect Revive(int percent, int chance = 100) =>
            new Effect(EffectKind.Revive, default, default, default, 0, 0, 0, percent, 0, 0, 0,
                1, chance);
    }

    /// <summary>スキルレベルが1つ上がったときに伸びるもの。
    ///
    /// ⭐ **語彙をここで固定する。**技ごとに数値を置かない（効果のプリミティブと同じ約束）。
    /// 技が選ぶのは「どの段でどれが伸びるか」だけで、伸び幅は語彙ごとに1つ。
    ///
    /// ⚠️ 増やす前に、既にある語彙で書けないか疑うこと。</summary>
    public enum SkillGain
    {
        /// <summary>威力 +<see cref="Skills.GainPowerPercent"/>%。
        /// ⭐ **段位（小/中/大/特大）は動かさない。**
        /// 動かすと「全体は1段下げる」という規則ごと崩れる。</summary>
        Power,
        /// <summary>CT −1。⚠️ 枠1 では効かない（元から 0）。</summary>
        Ct,
        /// <summary>通る率 +<see cref="Skills.GainChancePoints"/>pt。</summary>
        Chance,
        /// <summary>継続の回数 +1。</summary>
        Turns,
        /// <summary>多段の発数 +1。</summary>
        Repeat,
        /// <summary>割合回復の割合 +<see cref="Skills.GainHealPoints"/>pt。</summary>
        Percent,
        /// <summary>盾の枚数 +1。</summary>
        Count,
        /// <summary>CT を動かす技の動かし幅 +1 / 引き受ける回数 +1。
        /// ⭐ 「その技が持っている数」を伸ばす最後の受け皿。</summary>
        Amount,
    }

    /// <summary>スキルレベルぶんの上乗せ。⭐ Lv1 なら全部 0 ＝ **1ビットも変わらない**。</summary>
    public struct SkillBoost
    {
        public int PowerPercent;
        public int CtCut;
        public int ChancePoints;
        public int ExtraTurns;
        public int ExtraRepeat;
        public int ExtraPercent;
        public int ExtraCount;
        public int ExtraAmount;

        public bool IsNone => PowerPercent == 0 && CtCut == 0 && ChancePoints == 0
            && ExtraTurns == 0 && ExtraRepeat == 0 && ExtraPercent == 0 && ExtraCount == 0
            && ExtraAmount == 0;
    }

    public sealed class Skill
    {
        public readonly string Id;
        public readonly string Name;
        /// <summary>何をするスキルなのかの短い説明。</summary>
        public readonly string Gist;
        /// <summary>使ったあと、自分が何回行動するまで使えないか。⚠️ 枠1では常に 0 扱い。</summary>
        public readonly int Ct;
        public readonly Target Target;
        public readonly IReadOnlyList<Effect> Effects;

        public Skill(string id, string name, string gist, int ct, Target target, params Effect[] effects)
        {
            Id = id;
            Name = name;
            Gist = gist;
            Ct = ct;
            Target = target;
            Effects = effects;
        }
    }

    /// <summary>スキル表。
    ///
    /// ⚠️ 「たたかう」は無い。枠1が CT 0 なので、全スキルが CT 中でも必ず打てる札が残る。
    /// ⚠️ スキルを個別にコードで書かない。効果のプリミティブの組み合わせをデータで表す。
    /// ⚠️ 効果の名前は画面にそのまま出す語。凝った名前を付けない。</summary>
    public static class Skills
    {
        /// <summary>攻撃の威力。</summary>
        public static int DamagePowerOf(PowerTier tier)
        {
            switch (tier)
            {
                case PowerTier.Small: return 12;
                case PowerTier.Medium: return 20;
                case PowerTier.Large: return 30;
                case PowerTier.Huge: return 42;
                default: throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }

        /// <summary>画面に出す段位の語。⚠️ TS 側は段位そのものがこの文字列。</summary>
        public static string LabelOf(PowerTier tier)
        {
            switch (tier)
            {
                case PowerTier.Small: return "小";
                case PowerTier.Medium: return "中";
                case PowerTier.Large: return "大";
                case PowerTier.Huge: return "特大";
                default: throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }

        /// <summary>ステータス系が動かす割合（%）。⭐ ステータスの数値そのものに掛かる。
        /// ⚠️ 段位を使わない。威力とは別の軸なので揃えない。UP も DOWN も一律この値。</summary>
        public const int BuffPercent = 30;

        // ── スキルレベル ─────────────────────────────────
        // ⭐ 伸び幅は語彙ごとに1つだけ。技ごとの数値は置かない。

        /// <summary>スキルの最大レベル。⚠️ Lv1 が素の状態。</summary>
        public const int MaxLevel = 5;

        public const int GainPowerPercent = 10;
        public const int GainChancePoints = 10;
        public const int GainHealPoints = 5;

        /// <summary>手で書いた成長表。⚠️ **例外だけ。**既定は効果から導く（<see cref="GrowthOf"/>）。</summary>
        private static readonly Dictionary<string, SkillGain[]> GrowthOverrides =
            new Dictionary<string, SkillGain[]>();

        /// <summary>その技の成長表（Lv2・Lv3・Lv4・Lv5 の順）。
        ///
        /// ⭐ **既定は効果から導く。**手で 33技 × 4段 を書くと、必ずどこかに
        /// 「上げても何も起きない段」が混じる（ダメージの無い技に威力を付ける等）。
        /// ⚠️ 導いた結果が気に入らない技だけ <see cref="GrowthOverrides"/> に書く。</summary>
        /// <param name="slot">どの枠に入っているか。⚠️ **枠1（0）では CT を外す。**
        /// 枠1 の CT は常に 0 なので、縮める段があっても何も起きない。
        /// ⭐ -1 なら枠を問わない一覧（図鑑がこれを出す）。</param>
        public static IReadOnlyList<SkillGain> GrowthOf(Skill skill, int slot = -1)
        {
            SkillGain[]? written;
            if (GrowthOverrides.TryGetValue(skill.Id, out written) && written != null)
            {
                return slot == 0 ? WithoutCt(skill, written) : written;
            }

            // ⭐ その技が実際に持っている軸だけを並べ、最後に CT を足して順繰りに割り当てる
            var axes = new List<SkillGain>();
            if (HasDamage(skill)) axes.Add(SkillGain.Power);
            if (HasRepeat(skill)) axes.Add(SkillGain.Repeat);
            if (HasChance(skill)) axes.Add(SkillGain.Chance);
            if (HasTurns(skill)) axes.Add(SkillGain.Turns);
            // ⚠️ 回復の割合と盾の枚数は Turns でも Power でも表せない。
            //    これが無いと、それらの技は伸びる軸が CT しか無くなり、
            //    4段とも CT ＝ 途中で下限 0 に当たって**死に段**になる（導出して初めて見えた）
            if (HasPercent(skill)) axes.Add(SkillGain.Percent);
            if (HasCount(skill)) axes.Add(SkillGain.Count);
            if (HasAmount(skill)) axes.Add(SkillGain.Amount);
            if (skill.Ct > 0) axes.Add(SkillGain.Ct);

            // ⚠️ 枠1 では CT が効かないので、軸から外してから割り当てる
            if (slot == 0) axes.Remove(SkillGain.Ct);
            if (axes.Count == 0)
            {
                // ⚠️ 伸ばせる軸が1つも無い技。⭐ Audit が読める形で報告できるよう、
                //    ここでは落とさずに空を返す（0除算で落ちると原因が読めない）
                return new SkillGain[0];
            }

            var growth = new SkillGain[MaxLevel - 1];
            for (int i = 0; i < growth.Length; i++) growth[i] = axes[i % axes.Count];
            return growth;
        }

        /// <summary>手で書いた成長表から CT を抜いて詰め直す。⚠️ 枠1 用。</summary>
        private static SkillGain[] WithoutCt(Skill skill, SkillGain[] written)
        {
            var kept = new List<SkillGain>();
            foreach (var gain in written) if (gain != SkillGain.Ct) kept.Add(gain);
            if (kept.Count == 0) return new SkillGain[0];

            var growth = new SkillGain[MaxLevel - 1];
            for (int i = 0; i < growth.Length; i++) growth[i] = kept[i % kept.Count];
            return growth;
        }

        /// <summary>Lv までに積み上がった上乗せ。⚠️ Lv1 なら何も乗らない。</summary>
        /// <param name="slot">どの枠に入っているか。⚠️ **枠1 では CT の成長が効かない**
        /// （元から CT 0）ので、その段を詰めて別の軸に置き換える。
        /// ⭐ 渡さないと「★5の卵を払って何も変わらない段」が残る
        /// （tamaru・tsunoga など5種の枠1 で Lv3・Lv5 が死んでいた）。</param>
        public static SkillBoost BoostOf(Skill skill, int level, int slot = -1)
        {
            var boost = new SkillBoost();
            if (level <= 1) return boost;

            var growth = GrowthOf(skill, slot);
            int steps = level - 1;
            if (steps > growth.Count) steps = growth.Count;
            for (int i = 0; i < steps; i++)
            {
                switch (growth[i])
                {
                    case SkillGain.Power: boost.PowerPercent += GainPowerPercent; break;
                    case SkillGain.Ct: boost.CtCut += 1; break;
                    case SkillGain.Chance: boost.ChancePoints += GainChancePoints; break;
                    case SkillGain.Turns: boost.ExtraTurns += 1; break;
                    case SkillGain.Repeat: boost.ExtraRepeat += 1; break;
                    case SkillGain.Percent: boost.ExtraPercent += GainHealPoints; break;
                    case SkillGain.Count: boost.ExtraCount += 1; break;
                    case SkillGain.Amount: boost.ExtraAmount += 1; break;
                }
            }
            return boost;
        }

        /// <summary>その成長がその技で死んでいる理由。⚠️ 効くなら null。</summary>
        private static string? DeadGain(Skill skill, SkillGain gain)
        {
            switch (gain)
            {
                case SkillGain.Power: return HasDamage(skill) ? null : "ダメージが無い";
                case SkillGain.Repeat: return HasDamage(skill) ? null : "ダメージが無い";
                case SkillGain.Chance: return HasChance(skill) ? null : "外れる効果が無い";
                case SkillGain.Turns: return HasTurns(skill) ? null : "続く効果が無い";
                case SkillGain.Percent: return HasPercent(skill) ? null : "割合で効くものが無い";
                case SkillGain.Count: return HasCount(skill) ? null : "枚数で効くものが無い";
                case SkillGain.Amount: return HasAmount(skill) ? null : "回数で効くものが無い";
                case SkillGain.Ct: return skill.Ct > 0 ? null : "CT が元から 0";
                default: return "知らない成長";
            }
        }

        private static bool HasDamage(Skill skill)
        {
            foreach (var e in skill.Effects) if (e.Kind == EffectKind.Damage) return true;
            return false;
        }

        private static bool HasRepeat(Skill skill)
        {
            foreach (var e in skill.Effects) if (e.Kind == EffectKind.Damage && e.Repeat > 1) return true;
            return false;
        }

        private static bool HasChance(Skill skill)
        {
            foreach (var e in skill.Effects) if (e.Chance < 100) return true;
            return false;
        }

        private static bool HasTurns(Skill skill)
        {
            foreach (var e in skill.Effects) if (e.Turns > 0) return true;
            return false;
        }

        private static bool HasPercent(Skill skill)
        {
            foreach (var e in skill.Effects) if (e.Kind == EffectKind.HealRatio) return true;
            return false;
        }

        private static bool HasCount(Skill skill)
        {
            foreach (var e in skill.Effects) if (e.Kind == EffectKind.Shield) return true;
            return false;
        }

        private static bool HasAmount(Skill skill)
        {
            foreach (var e in skill.Effects)
            {
                if (e.Kind == EffectKind.Ct || e.Kind == EffectKind.Taunt) return true;
            }
            return false;
        }

        /// <summary>毒・リジェネの1スタックが、1行動ごとに動かす最大HP の割合（%）。
        /// ⭐ スタックする。2重なら 10%、3重なら 15%。
        /// ⚠️ 上限を置いていない。掛け続けられると青天井になる形なので、実測で見張る。</summary>
        public const int TickPercent = 5;

        private static readonly Skill[] List =
        {
            // ── 攻撃 ──────────────────────────────
            new Skill("attack", "攻撃", "敵1体にダメージ", 3, Target.EnemyOne,
                Effect.Damage(PowerTier.Medium, DamageScale.Atk)),
            new Skill("attack-heavy", "強攻撃", "敵1体に大きなダメージ。次が遠い", 6, Target.EnemyOne,
                Effect.Damage(PowerTier.Large, DamageScale.Atk)),
            // ⚠️ 全体なので1段下げて「小」
            new Skill("attack-all", "全体攻撃", "敵全体にダメージ", 5, Target.EnemyAll,
                Effect.Damage(PowerTier.Small, DamageScale.Atk)),
            new Skill("attack-all-heavy", "全体強攻撃", "敵全体に大きなダメージ。次がとても遠い", 7, Target.EnemyAll,
                Effect.Damage(PowerTier.Large, DamageScale.Atk)),
            new Skill("attack-def", "防御依存攻撃", "防御力が高いほど強い一撃", 3, Target.EnemyOne,
                Effect.Damage(PowerTier.Medium, DamageScale.Def)),

            // ── ステータス系 ──────────────────────
            new Skill("atk-up", "攻撃力UP", "自分の攻撃力を上げる", 4, Target.Self,
                Effect.Buff(StatKey.Atk, 1, 3)),
            new Skill("atk-down", "攻撃力DOWN", "敵1体の攻撃力を下げる", 4, Target.EnemyOne,
                Effect.Buff(StatKey.Atk, -1, 3)),
            new Skill("def-up", "防御力UP", "自分の防御力を上げる", 4, Target.Self,
                Effect.Buff(StatKey.Def, 1, 3)),
            new Skill("def-down", "防御力DOWN", "敵1体の防御力を下げる", 4, Target.EnemyOne,
                Effect.Buff(StatKey.Def, -1, 3)),
            new Skill("spd-up", "スピードUP", "自分のスピードを上げる", 4, Target.Self,
                Effect.Buff(StatKey.Spd, 1, 3)),
            new Skill("spd-down", "スピードDOWN", "敵1体のスピードを下げる", 4, Target.EnemyOne,
                Effect.Buff(StatKey.Spd, -1, 3)),

            // ── HP系 ──────────────────────────────
            new Skill("poison", "毒", "敵1体が行動するたびに削れる", 5, Target.EnemyOne,
                Effect.Poison(1, 4)),
            new Skill("regen", "リジェネ", "味方1体が行動するたびに回復する", 5, Target.AllyLowest,
                Effect.Regen(1, 4)),
            new Skill("heal-ratio", "HP割合回復", "味方1体の HP を最大値の割合ぶん回復", 4, Target.AllyLowest,
                Effect.HealRatio(30)),
            new Skill("shield", "シールド", "味方1体に、HP より先に減る盾を張る", 4, Target.AllyLowest,
                Effect.Shield(2)),

            // ── 行動系 ────────────────────────────
            new Skill("stun", "スタン", "敵1体の手番を飛ばす", 6, Target.EnemyOne,
                Effect.Stun(1)),
            new Skill("ct-short", "CT短縮", "自分の技の待ちを縮める", 4, Target.Self,
                Effect.Ct(-2)),
            new Skill("ct-long", "CT延長", "敵1体の技の待ちを延ばす", 5, Target.EnemyOne,
                Effect.Ct(2)),
            new Skill("taunt", "挑発", "味方への攻撃を自分が引き受ける", 3, Target.Self,
                Effect.Taunt(3)),

            // ── 特殊 ──────────────────────────────
            new Skill("guts", "ガッツ", "致命傷を HP1 で耐える", 6, Target.Self,
                Effect.Guts(3)),
            new Skill("immune", "免疫", "DOWN・毒・スタンを受けなくなる", 5, Target.Self,
                Effect.Immune(3)),

            // ── ここから増やしたぶん（2026-08-17）────────────────
            // ⭐ 新しい効果の種類を1つも足していない。既にある11種の**組み合わせ**と、
            //    多段（Repeat）の掛け算だけで書いてある。
            // ⚠️ 足すたびに `sim skills` で「一度も選ばれない技」が出ていないか見る。

            // 多段。⭐ 盾は1発ごとに剥がれるので、大きな一撃と役割が分かれる
            new Skill("attack-twice", "連撃", "敵1体に小さな一撃を2回", 4, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk, 2)),
            new Skill("attack-thrice", "乱打", "敵1体に小さな一撃を3回。盾を剥がす", 6, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk, 3)),
            new Skill("attack-def-twice", "堅陣突き", "防御が高いほど強い一撃を2回", 6, Target.EnemyOne,
                Effect.Damage(PowerTier.Medium, DamageScale.Def, 2)),

            // 複合。⭐ 1手で2つのことをする代わりに CT が長い
            // ⭐ ここから下の弱化は**外れることがある**（速度差で上下する）。
            // ⚠️ 上の移植した21技は 100% のまま。較正済みの照合が1手も変わらないように残してある。
            // ⚠️ ダメージの側は必ず当たる。外れるのは弱化だけ
            new Skill("venom-fang", "毒牙", "ダメージを与え、高い確率で毒も入れる", 5, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk),
                Effect.Poison(1, 4, chance: 75)),
            new Skill("crush", "打ち崩し", "ダメージを与え、高い確率で防御力を下げる", 5, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk),
                Effect.Buff(StatKey.Def, -1, 3, chance: 75)),
            new Skill("dash", "早駆け", "自分のスピードを上げ、技の待ちも縮める", 5, Target.Self,
                Effect.Buff(StatKey.Spd, 1, 3),
                Effect.Ct(-2)),
            new Skill("harden", "硬化", "防御力を上げ、盾も張る", 5, Target.Self,
                Effect.Buff(StatKey.Def, 1, 3),
                Effect.Shield(1)),
            new Skill("bulwark", "受けの構え", "攻撃を引き受け、防御力も上げる", 4, Target.Self,
                Effect.Taunt(2),
                Effect.Buff(StatKey.Def, 1, 3)),
            // ⭐ 2つ掛けるので1つずつの通りは低い。速い個体が使うと両方通りやすい
            new Skill("curse", "呪詛", "敵1体の攻撃力とスピードを下げる", 5, Target.EnemyOne,
                Effect.Buff(StatKey.Atk, -1, 3, chance: 70),
                Effect.Buff(StatKey.Spd, -1, 3, chance: 70)),

            // 濃さを変えただけのもの。⭐ 段位ではなくスタック数・割合で差を出す
            new Skill("venom-heavy", "猛毒", "毒を2重に入れる。やや外れやすい", 6, Target.EnemyOne,
                Effect.Poison(2, 4, chance: 65)),
            new Skill("heal-big", "大回復", "味方1体の HP を大きく回復", 6, Target.AllyLowest,
                Effect.HealRatio(55)),

            // ⚠️ 全体は1段下げる。全体の弱化は単体よりずっと効く
            // ⚠️ 全体なので通りは低め。全員に確実に入ると1手で試合が決まる
            new Skill("slow-all", "鎮めの風", "敵全体のスピードを下げる", 6, Target.EnemyAll,
                Effect.Buff(StatKey.Spd, -1, 3, chance: 60)),

            // ── 効き目と確率のトレードオフ（2026-08-17）─────────────
            // ⭐ **同じ効果の種類のまま、別の技として並べる軸。**
            //    「小さいが必ず通る」の隣に「大きいが半分外す」を置くと、
            //    どちらを枠に入れるかが**編成ごとに変わる判断**になる。
            // ⚠️ 上に並んでいる移植ぶんが「確実side」の役を兼ねているので、
            //    ここは主に博打sideを足している。
            // ⚠️ 自分・味方に掛けるものは速度で率が動かない。素の率がそのまま賭けになる。

            new Skill("heal-miracle", "奇跡の手当て", "HP を全快させる。半分は失敗する", 6, Target.AllyLowest,
                Effect.HealRatio(100, chance: 50)),
            new Skill("shield-wall", "鉄壁", "盾を4枚張る。3回に1回は失敗する", 6, Target.Self,
                Effect.Shield(4, chance: 65)),
            new Skill("guts-deep", "不屈", "長く粘れるが、掛かるかは五分", 6, Target.Self,
                Effect.Guts(6, chance: 50)),
            new Skill("immune-long", "浄化の衣", "長く効く免疫。4割は失敗する", 6, Target.Self,
                Effect.Immune(6, chance: 60)),

            // ⚠️ 相手に掛ける側は速度差で ±30pt 動く。速い個体が使うと通りやすい
            new Skill("stun-heavy", "強打", "2回ぶん手番を飛ばす。よく外す", 7, Target.EnemyOne,
                Effect.Stun(2, chance: 40)),
            new Skill("ct-lock", "封じ", "敵の技の待ちを大きく延ばす", 6, Target.EnemyOne,
                Effect.Ct(4, chance: 55)),
        };

        public static IReadOnlyList<Skill> All => List;

        private static readonly Dictionary<string, Skill> Index = BuildIndex();

        private static Dictionary<string, Skill> BuildIndex()
        {
            var map = new Dictionary<string, Skill>(List.Length);
            foreach (var skill in List) map.Add(skill.Id, skill);
            return map;
        }

        /// <summary>表にあるか。⚠️ 投げずに聞けるのは**セーブの読み込み**のためだけ。</summary>
        public static bool Has(string id) => Index.ContainsKey(id);

        /// <summary>知らない id を黙って握りつぶさない。表に無いものは「効かないだけ」で気づけないため。</summary>
        public static Skill ById(string id)
        {
            Skill? skill;
            if (!Index.TryGetValue(id, out skill)) throw new ArgumentException($"スキル表に {id} が無い");
            return skill!;
        }

        /// <summary>⭐ 枠1（種族固定）の CT は常に 0。
        ///
        /// ⚠️ CT は技ではなく枠の性質として扱う。
        /// 同じ技が、ある種族では枠1（CTなし）に、別の種族では枠2・3（CTあり）に入りうるため。</summary>
        public static int EffectiveCt(int slot, Skill skill) => slot == 0 ? 0 : skill.Ct;

        /// <summary>スキルレベルぶん縮めた CT。⚠️ 枠1 は元から 0 なので変わらない。</summary>
        public static int EffectiveCt(int slot, Skill skill, SkillBoost boost)
        {
            int ct = EffectiveCt(slot, skill) - boost.CtCut;
            return ct < 0 ? 0 : ct;
        }

        /// <summary>スキルレベルぶん上乗せした威力。
        /// ⭐ 段位の表（<see cref="DamagePowerOf"/>）が唯一の出所のまま。ここは掛けるだけ。</summary>
        public static int BoostedPower(PowerTier tier, SkillBoost boost)
        {
            int power = DamagePowerOf(tier);
            if (boost.PowerPercent == 0) return power;
            return (int)Math.Floor((double)(power * (100 + boost.PowerPercent)) / 100);
        }

        /// <summary>弱い側の効果か。⭐ **免疫が防ぐのはここが true のものすべて。**
        ///
        /// ⚠️ この判定は3つを同時に決めている:
        /// 免疫が弾くか / 速度差で通る率が動くか / 特性（狙い澄まし・意地）が効くか。
        /// ⭐ 「弱化」というひとつの括りなので、3つが揃って動くのが正しい。
        ///
        /// ⚠️ **CT延長・封じが漏れていた**（2026-08-17 修正）。
        /// 免疫で防げず、速度差でも動かず、狙い澄ましも効かない**唯一の弱化**になっていた。
        /// ⚠️ CT の効果は短縮（自分に掛ける）と延長（相手に掛ける）が同じ種類なので、
        /// **向きで見分ける**（延長だけが弱化）。</summary>
        public static bool IsHarmful(Effect effect)
        {
            if (effect.Kind == EffectKind.Buff) return effect.Sign < 0;
            if (effect.Kind == EffectKind.Ct) return effect.Delta > 0;
            // ⚠️ ゲージは符号で向きが変わる。減らす側だけが弱化
            if (effect.Kind == EffectKind.Gauge) return effect.Percent < 0;
            return effect.Kind == EffectKind.Poison || effect.Kind == EffectKind.Stun
                || effect.Kind == EffectKind.Sleep || effect.Kind == EffectKind.Block
                || effect.Kind == EffectKind.Dispel || effect.Kind == EffectKind.Steal
                || effect.Kind == EffectKind.Taunt;
        }

        /// <summary>相手が受け取る「強化」か。⭐ ブロックが止める側。
        /// ⚠️ 自然に溜まるゲージ・自然に減る CT は含まない（あれは買った分ではない）。</summary>
        public static bool IsBoon(Effect effect)
        {
            switch (effect.Kind)
            {
                case EffectKind.Buff: return effect.Sign > 0;
                case EffectKind.Ct: return effect.Delta < 0;
                case EffectKind.Gauge: return effect.Percent > 0;
                case EffectKind.HealRatio:
                case EffectKind.Regen:
                case EffectKind.Shield:
                case EffectKind.Guts:
                case EffectKind.Immune:
                case EffectKind.Revive:
                    return true;
                default: return false;
            }
        }

        /// <summary>技表とガチャプールの整合を数える。
        ///
        /// ⭐ **件数を数えない。** 数えると技を足すたびに落ちるので、
        /// 検査を緩める圧力になる（そして緩めたら二度と戻らない）。
        /// 見るのは「規則を守っているか」と「繋がっているか」だけ。
        ///
        /// ⚠️ ここが catch するのは、足した日には気づけない類のものばかり:
        /// AI が採点しない効果 / どの巣からも出ない技 / 実在しない id を指すプール。
        /// どれも**コンパイルは通り、遊べてしまう**。</summary>
        public static void Audit()
        {
            var problems = new List<string>();

            var seen = new HashSet<string>();
            foreach (var skill in List)
            {
                if (!seen.Add(skill.Id)) problems.Add($"技 id が重複している: {skill.Id}");
                if (skill.Effects.Count == 0) problems.Add($"{skill.Id}: 効果が1つも無い");
                if (skill.Name.Length == 0) problems.Add($"{skill.Id}: 名前が空");
                if (skill.Gist.Length == 0) problems.Add($"{skill.Id}: 画面に出す短い説明が空");
                if (skill.Ct < 0) problems.Add($"{skill.Id}: CT が {skill.Ct}");

                // ⚠️ **上げても何も起きない段**を弾く。これが無いと
                //    「Lv3 にしたのに何も変わらない」が黙って通る（画面には出るのに実体が無い）
                var growth = GrowthOf(skill);
                if (growth.Count != MaxLevel - 1)
                {
                    problems.Add($"{skill.Id}: 伸ばせる軸が1つも無い（成長表が {growth.Count} 段）");
                }
                // ⚠️ 枠1（種族固定）に入る技は、CT の段が死ぬ。詰め替えが効いているか数える
                foreach (var species in SpeciesTable.All)
                {
                    if (species.Skill1 != skill.Id) continue;
                    var asSlot1 = GrowthOf(skill, 0);
                    foreach (var gain in asSlot1)
                    {
                        if (gain == SkillGain.Ct)
                            problems.Add($"{skill.Id}: {species.Id} の枠1 なのに CT の段がある（効かない）");
                    }
                    if (asSlot1.Count != MaxLevel - 1)
                        problems.Add($"{skill.Id}: {species.Id} の枠1 で伸ばせる軸が無い");
                }
                int cuts = 0;
                foreach (var gain in growth)
                {
                    string? dead = DeadGain(skill, gain);
                    if (dead != null) problems.Add($"{skill.Id}: {gain} が効かない（{dead}）");
                    // ⚠️ CT は 0 が下限。技の CT より多く縮める段は**何も起きない**
                    if (gain == SkillGain.Ct && ++cuts > skill.Ct)
                    {
                        problems.Add($"{skill.Id}: CT を {cuts} 回縮めるが、元の CT は {skill.Ct}");
                    }
                }

                foreach (var effect in skill.Effects)
                {
                    if (!Ai.Knows(effect.Kind))
                    {
                        problems.Add(
                            $"{skill.Id}: {effect.Kind} を AI が採点しない。" +
                            "スコア0になって**永久に選ばれない技**になる（Ai.ScoreOf に case を足す）");
                    }
                }
            }

            // ── 卵ガチャ。⭐ ここが「技を手に入れる唯一の経路」なので、切れていると入手不能になる
            var reachable = new HashSet<string>();
            foreach (var species in SpeciesTable.All)
            {
                reachable.Add(species.Skill1);

                var pool = species.Gacha;
                if (pool.Count == 0)
                {
                    problems.Add($"{species.Id}: 卵ガチャのプールが空（種族を足したら必ず要る）");
                    continue;
                }

                var inPool = new HashSet<string>();
                foreach (var id in pool)
                {
                    if (!Index.ContainsKey(id)) problems.Add($"{species.Id} のプールが実在しない技 {id} を指している");
                    if (!inPool.Add(id)) problems.Add($"{species.Id} のプールで {id} が重複している");
                    reachable.Add(id);
                }

                // ⭐ 枠2・3 を別々に引くので、枠1を除いて2件は要る
                int usable = GachaPoolOf(species.Id, species.Skill1).Count;
                if (usable < 2)
                {
                    problems.Add($"{species.Id}: 枠1を除いたプールが {usable} 件。枠2・3 を別々に引けない");
                }
            }

            foreach (var skill in List)
            {
                if (!reachable.Contains(skill.Id))
                {
                    problems.Add($"{skill.Id}: どの種族の枠1にもプールにも無い。**手に入らない技**になっている");
                }
            }

            if (problems.Count > 0)
            {
                throw new InvalidOperationException("技表の不備:\n  " + string.Join("\n  ", problems));
            }
        }

        /// <summary>その種族の卵から出うる技。⚠️ 表に無い種族は黙って空にせず投げる。
        /// 枠1（種族固定）と同じ技はここで外す。
        /// ⭐ プールの実体は <see cref="Species.Gacha"/>（種族の行）が持つ。ここは絞るだけ。</summary>
        public static List<string> GachaPoolOf(string speciesId, string skill1)
        {
            var pool = SpeciesTable.ById(speciesId).Gacha;

            var result = new List<string>(pool.Count);
            foreach (var id in pool)
            {
                if (id != skill1) result.Add(id);
            }
            return result;
        }
    }
}
