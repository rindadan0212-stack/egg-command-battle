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
        /// <summary>⭐ 敵のうち**その場で引いた1体**（2026-08-19 に追加）。
        ///
        /// ⭐ 「狙えない代わりに安い」という取引を作るための狙い先。
        /// 単体技と同じ効き目を、CT か確率のぶん得しても釣り合う。
        /// ⚠️ **挑発の縛りを受けない**（狙って撃っていないため）。
        /// ⚠️ 下見（AI の採点・画面）では乱数を引かない ── 引くと本番と乱数の流れがずれる。</summary>
        EnemyRandom,
        /// <summary>残 HP 割合が最も低い味方（自分を含む）。
        /// ⚠️ 移植元の語彙。⭐ いまは技では使わず、<see cref="AllyOne"/> の**既定の落とし先**として残る。</summary>
        AllyLowest,
        /// <summary>味方1体。⭐ **プレイヤーが選ぶ。**
        /// ⚠️ 選ばなかったとき（AI・未選択）は残 HP 割合が最も低い味方へ落ちる。
        ///
        /// ⭐ これが「誰に配るか」の軸。⚠️ 強化を全部 <see cref="Self"/> にしていた頃は、
        /// プレイヤーが決めているのが「いま動く1体が3枠のどれを押すか」だけだった。</summary>
        AllyOne,
        Self,
        /// <summary>倒れている味方。⚠️ 蘇生のためだけの狙い先。居なければ何も起きない。</summary>
        AllyDown,
        /// <summary>⭐ 倒れている味方**全員**（2026-08-19 に追加）。
        /// ⚠️ <see cref="AllyDown"/> は必ず1体しか返さないので、全体蘇生が書けなかった
        /// （敵側に <see cref="EnemyAll"/> があって味方側に無かったのと同じ穴）。</summary>
        AllyDownAll,
        /// <summary>⭐ 味方全体（2026-08-19 に追加）。
        ///
        /// ⚠️ **これが無かったので、全体回復も全体強化も1つも書けなかった。**
        /// 敵側だけ <see cref="EnemyAll"/> があって味方側に無い、という穴だった。
        /// ⭐ 効き目は1段下げて置くこと（<see cref="EnemyAll"/> と同じ約束）。</summary>
        AllyAll,
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
    /// <summary>その一撃が**どのステで伸びるか**。
    ///
    /// ⭐ 効果の種類を増やさずに、技の役割を増やすための軸。
    /// 「防御依存攻撃」は硬い個体の火力になり、「スピード依存攻撃」は速い個体の火力になる。
    /// ⚠️ **どのステでも同じ式**（<see cref="Battle.DamageOf"/>）を通す。
    /// 別の式を足すと、桁の較正が種類ぶんに増える。</summary>
    public enum DamageScale
    {
        Atk,
        Def,
        /// <summary>⭐ 2026-08-19 に追加。速さに寄せた個体の使い道を1つ増やす。
        /// ⚠️ 速度は行動回数も潜入の飛距離も持っているので、**強く出やすい**。
        /// 威力の段を1つ下げて置くこと。</summary>
        Spd,
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
        /// ⚠️ **相手に掛けるもの**だけ、実際の率が命中と抵抗の差で上下する（<see cref="Battle.LandChanceOf"/>）。
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
        /// <summary>最大HP の割合で動かす。⭐ **負なら削る。**
        ///
        /// ⭐ <c>CT 増減:±N</c> や <c>ゲージ 割合:±N</c> と同じ流儀 ── 符号で向きが変わるものは
        /// 効果の種類を分けない（2026-08-19 に負を許した）。
        /// ⚠️ これで「最大HPの30%を削る」「確率つきの一撃必殺」が、
        /// **ダメージに確率を付けずに**書ける（ダメージの出力が運で決まるのを避ける原則を保てる）。
        /// ⚠️ 削る側は防御も属性も見ない。**通る率だけが防ぎ手**なので、確率は控えめに置くこと。</summary>
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
        /// <summary>割合で効くもの（割合回復・ゲージ・蘇生）の割合 +<see cref="Skills.GainHealPoints"/>pt。</summary>
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

    /// <summary>技の型。⭐ **卵の枠2・枠3 は、種族ごとに決めた型から引く。**
    ///
    /// ⚠️ 型は技に手で書かない ── <see cref="Skills.TypeOf"/> が効果から導く。
    /// 47技に手で書くと、効果を変えた日に必ずどこかがずれる。</summary>
    public enum SkillType
    {
        /// <summary>ダメージを与える。</summary>
        Attack,
        /// <summary>味方を強くする・守る（HP を戻すものを除く）。</summary>
        Support,
        /// <summary>相手を弱くする・止める。</summary>
        Debuff,
        /// <summary>HP を戻す。</summary>
        Heal,
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
        /// <summary>卵の枠2・3 のどちらから出るかを決める型。
        ///
        /// ⚠️ **手で書く。**効果から導いていたが、
        /// 「攻撃しつつスタンを付ける」のような複合技が必ずアタックに寄ってしまい、
        /// **デバフ枠に置けなかった**（＝崩す札として作ったのに崩す枠から出ない）。
        /// ⭐ どちらの枠から出したいかは作り手が決めることなので、宣言する。</summary>
        public readonly SkillType Type;

        public Skill(string id, string name, string gist, SkillType type, int ct, Target target,
            params Effect[] effects)
        {
            Id = id;
            Name = name;
            Gist = gist;
            Type = type;
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
        /// <summary>その技の型。⭐ **技表に書いてあるものをそのまま返す。**
        ///
        /// ⚠️ 以前は効果から導いていた（ダメージがあればアタック）。
        /// 複合技（攻撃＋スタンなど）が必ずアタックに寄り、デバフ枠に置けなかったため手書きにした。
        /// ⭐ 迷ったら「**どちらの枠から出したいか**」で決める（型は入手経路の宣言であって、
        /// 戦闘での挙動には一切効かない）。</summary>
        public static SkillType TypeOf(Skill skill) => skill.Type;

        public static string LabelOf(SkillType type)
        {
            switch (type)
            {
                case SkillType.Attack: return "アタック";
                case SkillType.Support: return "サポート";
                case SkillType.Debuff: return "デバフ";
                case SkillType.Heal: return "ヒール";
                default: throw new ArgumentOutOfRangeException(nameof(type), type, "名前の無い型");
            }
        }

        /// <summary>攻撃の威力。</summary>
        /// <summary>威力の千分率の分母。⭐ 1000 ＝ 攻撃力と等倍。</summary>
        public const int PowerUnit = 1000;

        /// <summary>威力（<see cref="PowerUnit"/> 分率）。⭐ **攻撃力の何倍か**。
        /// ⚠️ 実ダメージにするときは <see cref="Battle.DamageOf"/> を通すこと
        /// （HP の桁に合わせる係数はあちらが持つ）。</summary>
        public static int DamagePowerOf(PowerTier tier)
        {
            switch (tier)
            {
                // ⭐ **威力は「攻撃力の何倍か」**（作者の指示 2026-08-19）。千分率で持つ。
                // ⚠️ 前は 2,100 / 3,500 / 5,250 / 7,350 という**意味の読めない数**だった。
                //    画面にも図鑑にも出るのに、その数が何なのか説明できなかった。
                case PowerTier.Small: return 1200;
                case PowerTier.Medium: return 1500;
                case PowerTier.Large: return 2000;
                case PowerTier.Huge: return 3000;
                default: throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }

        /// <summary>画面に出す段位の語。⚠️ TS 側は段位そのものがこの文字列。</summary>
        /// <summary>伸びる軸の名前。⚠️ 出所はここ1つ（Wiki と図鑑で言葉を分けない）。</summary>
        public static string LabelOf(SkillGain gain)
        {
            switch (gain)
            {
                case SkillGain.Power: return "威力";
                case SkillGain.Ct: return "CT";
                case SkillGain.Chance: return "確率";
                case SkillGain.Turns: return "持続";
                case SkillGain.Repeat: return "発数";
                case SkillGain.Percent: return "割合";
                case SkillGain.Count: return "個数";
                case SkillGain.Amount: return "量";
                default: throw new ArgumentOutOfRangeException(nameof(gain), gain, "名前の無い軸");
            }
        }

        /// <summary>その一撃が乗るステの名前。⭐ **唯一の出所。**
        ///
        /// ⚠️ ここを作る前は、同じ対応表が**3か所**（帳面の書き出し・帳面の読み取り・
        /// 編集画面）に手で書いてあり、`Spd` を足した日に**帳面の読み取りだけ落ちた**
        /// ── 「依存:スピード」と書いた技が、C# にすると `DamageScale.Atk` に化けていた
        /// （2026-08-19。往復させて初めて分かった）。
        /// ⭐ 語を足すときはここだけ直す。</summary>
        public static string LabelOf(DamageScale scale)
        {
            switch (scale)
            {
                case DamageScale.Atk: return "攻撃";
                case DamageScale.Def: return "防御";
                case DamageScale.Spd: return "スピード";
                default: throw new ArgumentOutOfRangeException(nameof(scale), scale, "名前の無い依存");
            }
        }

        /// <summary>名前から引き直す。⚠️ 知らない語なら false（黙って既定に落とさない）。</summary>
        public static bool TryScale(string? word, out DamageScale scale)
        {
            foreach (DamageScale s in Enum.GetValues(typeof(DamageScale)))
            {
                if (LabelOf(s) == word) { scale = s; return true; }
            }
            scale = DamageScale.Atk;
            return false;
        }

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

        /// <summary>「割合」で効くか。
        /// ⚠️ **`effect.Percent` を読む効果はここに全部並べる。**
        ///    ⭐ ゲージ と 蘇生 は `Percent` を読むのに「量」の軸に入れてしまっていたので、
        ///    1段上げても **+1pt** しか伸びなかった（30% → 31%）。5段上げて 35% と、
        ///    伸びが体感できない ── 2026-08-19 の監査で発覚。</summary>
        private static bool HasPercent(Skill skill)
        {
            foreach (var e in skill.Effects)
            {
                if (e.Kind == EffectKind.HealRatio || e.Kind == EffectKind.Gauge
                    || e.Kind == EffectKind.Revive) return true;
            }
            return false;
        }

        private static bool HasCount(Skill skill)
        {
            foreach (var e in skill.Effects)
            {
                // ⚠️ count を使う効果はここに全部並べる。落とすと「伸びる軸が CT だけ」になり、
                //    途中で下限 0 に当たって死に段が出る
                if (e.Kind == EffectKind.Shield || e.Kind == EffectKind.Dispel
                    || e.Kind == EffectKind.Steal) return true;
            }
            return false;
        }

        private static bool HasAmount(Skill skill)
        {
            foreach (var e in skill.Effects)
            {
                // ⚠️ **ここは「回数」で数えるものだけ。**割合で効くものは `HasPercent` へ。
                //    CT は「何ターン」、挑発は「何回」 ── どちらも +1 で意味が変わる。
                if (e.Kind == EffectKind.Ct || e.Kind == EffectKind.Taunt) return true;
            }
            return false;
        }

        /// <summary>毒・リジェネの1スタックが、1行動ごとに動かす最大HP の割合（%）。
        /// ⭐ スタックする。2重なら 10%、3重なら 15%。
        /// ⚠️ 上限を置いていない。掛け続けられると青天井になる形なので、実測で見張る。</summary>
        public const int TickPercent = 5;

        // ⭐ **名前＝効果。**状態を付ける技は、名前を見ただけで何が起きるか分かる形にする
        //    （2026-08-18・作者方針）。⚠️ 「封じ」「眠り」のような言い換えを作らない。
        //    修飾は3つだけ:
        //      ・大   … 一度の効き目が大きい（毒×2 / 盾4枚 / スタン2回 / CT4延長）
        //      ・長   … 効いている間が長い（免疫6回 / ガッツ6回）
        //      ・全体 … 狙い先が全体
        //    ⚠️ 2つの状態を付ける複合技だけは、名前で両方を言えないので短い名を残している。
        // ── CT の上限は 5（作者の指示 2026-08-19）────────────────────
        // ⚠️ **1体が動けるのは1戦闘でおよそ 5.6手**（`sim pace`）。CT6・7 の技は
        //    **1戦闘に1回しか撃てず**、実測で全手番の **68.8% が枠1（種族の通常攻撃・CT0）**
        //    になっていた。枠2・3 は 46.4% の時間、待ちで塞がっていた。
        // ⭐ そこで上限を 5 に下げる。
        // ⚠️ **ただし盤面をひっくり返しうる技だけは 7 のまま**（作者の指示）:
        //    蘇生・蘇生・大（倒れた味方が戻る）/ 全体強攻撃・全体連撃（3体まとめて落としうる）。
        //    ⭐ 1回きりであることが持ち味なので、短くすると別物になる。
        // ⚠️ **スキルレベルで CT はさらに縮む**（成長表の Ct の段ぶん）。
        //    上限5・2段の技は Lv5 で CT3、1段の技は CT4 まで落ちる。
        //    ⭐ 縮んだ先が 0 にならないことは `Skills.Audit` が数えている。

        /// <summary>CT の上限。⭐ **1体が動けるのは1戦闘でおよそ 5.6手**（`sim pace`）なので、
        /// これを超える技は1戦闘に1回しか撃てない。
        /// ⚠️ 上限を 7 にしていた頃、全手番の **68.8% が枠1**（種族の通常攻撃・CT0）になり、
        /// 枠2・3 は 46.4% の時間を待ちで塞いでいた（作者の指示 2026-08-19 で 5 へ）。</summary>
        public const int CtCap = 5;

        /// <summary>盤面をひっくり返す技だけに許す CT。⭐ **1回きりであることが持ち味**なので、
        /// 短くすると別物になる（作者の指示 2026-08-19）。</summary>
        public const int CtHeavy = 7;

        /// <summary>その技は <see cref="CtHeavy"/> を許されるか。
        ///
        /// ⭐ **一覧ではなく規則で決める。**id を並べると、技を足した日に必ず漏れる。
        /// ⚠️ いま当てはまるのは4件 ── 蘇生・蘇生・大（倒れた味方が戻る）と
        /// 全体強攻撃・全体連撃（3体まとめて落としうる）。</summary>
        /// <summary>味方全体をここまで戻す回復は「盤面をひっくり返す」扱い。</summary>
        public const int HeavyHealPercent = 50;

        /// <summary>1つの袋に入れてよい技の数。
        /// ⭐ **狙える確率はここで決まる。**枠2×枠3 で 1/(a×b)。
        /// ⚠️ 技が増えても**ここを動かさない**。増えた技は種族と枠を足して受ける
        /// （型で縛っていた頃は受け皿が足りず、袋を太らせるしか無かった）。</summary>
        public const int PoolMax = 5;

        /// <summary>1つの技が入ってよい袋の数。
        /// ⚠️ ここを緩めると「どこで奪っても同じ」に戻る。⭐ 巣を選ぶ理由の源。</summary>
        public const int SpreadMax = 2;

        private static void Bump(Dictionary<string, int> counts, string id) =>
            counts[id] = counts.TryGetValue(id, out int n) ? n + 1 : 1;

        private static bool Has(IReadOnlyList<Skill> table, string id)
        {
            foreach (var s in table) if (s.Id == id) return true;
            return false;
        }

        private static Skill ById(IReadOnlyList<Skill> table, string id)
        {
            foreach (var s in table) if (s.Id == id) return s;
            throw new ArgumentException($"技表に {id} が無い");
        }

        /// <summary>袋の**顔つき**。⭐ 宣言ではなく**中身から読み取る**。
        ///
        /// ⚠️ 型を宣言していた頃は、これが縛りだった。いまはただの注記なので、
        /// 混ざった袋（毒と、毒が効いた相手を殴る技）も自由に作れる。</summary>
        public static string FlavorOf(IReadOnlyList<string> pool)
        {
            var kinds = new List<SkillType>();
            foreach (var id in pool)
            {
                var t = TypeOf(ById(id));
                if (!kinds.Contains(t)) kinds.Add(t);
            }
            if (kinds.Count == 0) return "";
            if (kinds.Count == 1) return LabelOf(kinds[0]);
            var names = new List<string>();
            foreach (var k in kinds) names.Add(LabelOf(k));
            return string.Join("・", names);
        }

        public static bool IsHeavyCt(Skill skill)
        {
            foreach (var e in skill.Effects)
            {
                if (e.Kind == EffectKind.Revive) return true;
                // ⭐ **味方全体の全快も、盤面をひっくり返す。**
                //    ⚠️ 敵側だけ見ていた頃、味方全体の大回復は CT7 を取れず、
                //    「蘇生を1つ混ぜないと重くできない」という妙な形になっていた（2026-08-19 の監査）。
                if (e.Kind == EffectKind.HealRatio && skill.Target == Target.AllyAll
                    && e.Percent >= HeavyHealPercent) return true;
                if (e.Kind != EffectKind.Damage || skill.Target != Target.EnemyAll) continue;
                // ⭐ 全体に「深く」当たるもの ── 一撃が大きいか、多段で盾を広く剥がすか
                if (e.Power >= PowerTier.Large || e.Repeat > 1) return true;
            }
            return false;
        }

        private static readonly Skill[] List =
        {
            // ── 攻撃 ──────────────────────────────
            new Skill("attack", "攻撃", "敵1体にダメージ", SkillType.Attack, 3, Target.EnemyOne,
                Effect.Damage(PowerTier.Medium, DamageScale.Atk)),
            new Skill("attack-heavy", "強攻撃", "敵1体に大きなダメージ。次が遠い", SkillType.Attack, 5, Target.EnemyOne,
                Effect.Damage(PowerTier.Large, DamageScale.Atk)),
            // ⚠️ 全体なので1段下げて「小」
            new Skill("attack-all", "全体攻撃", "敵全体にダメージ", SkillType.Attack, 5, Target.EnemyAll,
                Effect.Damage(PowerTier.Small, DamageScale.Atk)),
            new Skill("attack-all-heavy", "全体強攻撃", "敵全体に大きなダメージ。次がとても遠い", SkillType.Attack, 7, Target.EnemyAll,
                Effect.Damage(PowerTier.Large, DamageScale.Atk)),
            new Skill("attack-def", "防御依存攻撃", "防御力が高いほど強い一撃", SkillType.Attack, 3, Target.EnemyOne,
                Effect.Damage(PowerTier.Medium, DamageScale.Def)),

            // ── ステータス系 ──────────────────────
            // ⭐ **強化は配る札。**⚠️ 全部 Self にしていた頃は「誰に」の選択が1つも無かった。
            new Skill("atk-up", "攻撃力UP", "味方1体の攻撃力を上げる", SkillType.Support, 4, Target.AllyOne,
                Effect.Buff(StatKey.Atk, 1, 3)),
            new Skill("atk-down", "攻撃力DOWN", "敵1体の攻撃力を下げる", SkillType.Debuff, 4, Target.EnemyOne,
                Effect.Buff(StatKey.Atk, -1, 3)),
            new Skill("def-up", "防御力UP", "味方1体の防御力を上げる", SkillType.Support, 4, Target.AllyOne,
                Effect.Buff(StatKey.Def, 1, 3)),
            new Skill("def-down", "防御力DOWN", "敵1体の防御力を下げる", SkillType.Debuff, 4, Target.EnemyOne,
                Effect.Buff(StatKey.Def, -1, 3)),
            new Skill("spd-up", "スピードUP", "味方1体のスピードを上げる", SkillType.Support, 4, Target.AllyOne,
                Effect.Buff(StatKey.Spd, 1, 3)),
            new Skill("spd-down", "スピードDOWN", "敵1体のスピードを下げる", SkillType.Debuff, 4, Target.EnemyOne,
                Effect.Buff(StatKey.Spd, -1, 3)),

            // ── HP系 ──────────────────────────────
            new Skill("poison", "毒", "敵1体が行動するたびに削れる", SkillType.Debuff, 5, Target.EnemyOne,
                Effect.Poison(1, 4)),
            new Skill("regen", "リジェネ", "味方1体が行動するたびに回復する", SkillType.Heal, 5, Target.AllyOne,
                Effect.Regen(1, 4)),
            new Skill("heal-ratio", "HP割合回復", "味方1体の HP を最大値の割合ぶん回復", SkillType.Heal, 4, Target.AllyOne,
                Effect.HealRatio(30)),
            new Skill("shield", "シールド", "味方1体に、HP より先に減る盾を張る", SkillType.Support, 4, Target.AllyOne,
                Effect.Shield(2)),

            // ── 行動系 ────────────────────────────
            new Skill("stun", "スタン", "敵1体の手番を飛ばす", SkillType.Debuff, 5, Target.EnemyOne,
                Effect.Stun(1)),
            new Skill("ct-short", "CT短縮", "自分の技の待ちを縮める", SkillType.Support, 4, Target.Self,
                Effect.Ct(-2)),
            new Skill("ct-long", "CT延長", "敵1体の技の待ちを延ばす", SkillType.Debuff, 5, Target.EnemyOne,
                Effect.Ct(2)),
            // ⚠️ **2026-08-18 まで Target.Self だったため、一度も発動していなかった。**
            //    効果は「相手に付ける弱化」に作り替えたのに技の狙い先を直し忘れていた。
            //    自分に掛かると TauntBy が自分になり、敵側から探す縛りが一致しない。
            new Skill("taunt", "挑発", "敵1体が、自分しか狙えなくなる", SkillType.Debuff, 3, Target.EnemyOne,
                Effect.Taunt(3)),

            // ── 特殊 ──────────────────────────────
            new Skill("guts", "ガッツ", "味方1体が致命傷を HP1 で耐える", SkillType.Support, 5, Target.AllyOne,
                Effect.Guts(3)),
            // ⭐ **先手で配る札。**⚠️ 弱化を通されてから貼っても手遅れ
            new Skill("immune", "免疫", "味方1体が弱化を受けなくなる", SkillType.Support, 5, Target.AllyOne,
                Effect.Immune(3)),

            // ── ここから増やしたぶん（2026-08-17）────────────────
            // ⭐ 新しい効果の種類を1つも足していない。既にある11種の**組み合わせ**と、
            //    多段（Repeat）の掛け算だけで書いてある。
            // ⚠️ 足すたびに `sim skills` で「一度も選ばれない技」が出ていないか見る。

            // 多段。⭐ 盾は1発ごとに剥がれるので、大きな一撃と役割が分かれる
            new Skill("attack-twice", "連撃", "敵1体に小さな一撃を2回", SkillType.Attack, 4, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk, 2)),
            new Skill("attack-thrice", "乱打", "敵1体に小さな一撃を3回。盾を剥がす", SkillType.Attack, 5, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk, 3)),
            new Skill("attack-def-twice", "堅陣突き", "防御が高いほど強い一撃を2回", SkillType.Attack, 5, Target.EnemyOne,
                Effect.Damage(PowerTier.Medium, DamageScale.Def, 2)),

            // 複合。⭐ 1手で2つのことをする代わりに CT が長い
            // ⭐ ここから下の弱化は**外れることがある**（命中と抵抗の差で上下する）。
            // ⚠️ 上の移植した21技は 100% のまま。較正済みの照合が1手も変わらないように残してある。
            // ⚠️ ダメージの側は必ず当たる。外れるのは弱化だけ
            new Skill("venom-fang", "毒牙", "ダメージを与え、高い確率で毒も入れる", SkillType.Attack, 5, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk),
                Effect.Poison(1, 4, chance: 75)),
            new Skill("crush", "打ち崩し", "ダメージを与え、高い確率で防御力を下げる", SkillType.Attack, 5, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk),
                Effect.Buff(StatKey.Def, -1, 3, chance: 75)),
            new Skill("dash", "早駆け", "自分のスピードを上げ、技の待ちも縮める", SkillType.Support, 5, Target.Self,
                Effect.Buff(StatKey.Spd, 1, 3),
                Effect.Ct(-2)),
            new Skill("harden", "硬化", "防御力を上げ、盾も張る", SkillType.Support, 5, Target.Self,
                Effect.Buff(StatKey.Def, 1, 3),
                Effect.Shield(1)),
            // ⚠️ 挑発が弱化になったので、相方も敵に掛かるものへ替えた
            //    （Self のままだと防御UP が敵に乗る）。
            new Skill("bulwark", "受けの構え", "敵1体を釘付けにし、その攻撃を鈍らせる", SkillType.Debuff, 4, Target.EnemyOne,
                Effect.Taunt(2),
                Effect.Buff(StatKey.Atk, -1, 3, chance: 75)),
            // ⭐ 2つ掛けるので1つずつの通りは低い。速い個体が使うと両方通りやすい
            new Skill("curse", "呪詛", "敵1体の攻撃力とスピードを下げる", SkillType.Debuff, 5, Target.EnemyOne,
                Effect.Buff(StatKey.Atk, -1, 3, chance: 70),
                Effect.Buff(StatKey.Spd, -1, 3, chance: 70)),

            // 濃さを変えただけのもの。⭐ 段位ではなくスタック数・割合で差を出す
            new Skill("venom-heavy", "毒・大", "毒を2重に入れる。やや外れやすい", SkillType.Debuff, 5, Target.EnemyOne,
                Effect.Poison(2, 4, chance: 65)),
            new Skill("heal-big", "HP割合回復・大", "味方1体の HP を大きく回復", SkillType.Heal, 5, Target.AllyOne,
                Effect.HealRatio(55)),

            // ⚠️ 全体は1段下げる。全体の弱化は単体よりずっと効く
            // ⚠️ 全体なので通りは低め。全員に確実に入ると1手で試合が決まる
            new Skill("slow-all", "スピードDOWN・全体", "敵全体のスピードを下げる", SkillType.Debuff, 5, Target.EnemyAll,
                Effect.Buff(StatKey.Spd, -1, 3, chance: 60)),

            // ── 効き目と確率のトレードオフ（2026-08-17）─────────────
            // ⭐ **同じ効果の種類のまま、別の技として並べる軸。**
            //    「小さいが必ず通る」の隣に「大きいが半分外す」を置くと、
            //    どちらを枠に入れるかが**編成ごとに変わる判断**になる。
            // ⚠️ 上に並んでいる移植ぶんが「確実side」の役を兼ねているので、
            //    ここは主に博打sideを足している。
            // ⚠️ 自分・味方に掛けるものは速度で率が動かない。素の率がそのまま賭けになる。

            new Skill("heal-miracle", "HP割合回復・特大", "味方1体を全快させる。半分は失敗する", SkillType.Heal, 5, Target.AllyOne,
                Effect.HealRatio(100, chance: 50)),
            new Skill("shield-wall", "シールド・大", "味方1体に盾を4枚張る。3回に1回は失敗する", SkillType.Support, 5, Target.AllyOne,
                Effect.Shield(4, chance: 65)),
            new Skill("guts-deep", "ガッツ・長", "味方1体が長く粘れる。掛かるかは五分", SkillType.Support, 5, Target.AllyOne,
                Effect.Guts(6, chance: 50)),
            new Skill("immune-long", "免疫・長", "味方1体に長く効く免疫。4割は失敗する", SkillType.Support, 5, Target.AllyOne,
                Effect.Immune(6, chance: 60)),

            // ⚠️ 相手に掛ける側は（命中 − 抵抗）÷2 ポイント動く。命中に振った個体が使うと通りやすい
            new Skill("stun-heavy", "スタン・大", "2回ぶん手番を飛ばす。よく外す", SkillType.Debuff, 5, Target.EnemyOne,
                Effect.Stun(2, chance: 40)),
            new Skill("ct-lock", "CT延長・大", "敵の技の待ちを大きく延ばす", SkillType.Debuff, 5, Target.EnemyOne,
                Effect.Ct(4, chance: 55)),

            // ── 手番と打ち消しの層（2026-08-18）──────────────────
            // ⭐ **ここまでは「何を持ち込むか」の札しか無かった。**
            //    この8本が足すのは「**いつ・誰に**切るか」の軸:
            //      ・行動順に触る（足止め / 加速）
            //      ・相手の守りを剥がしてから通す（強化解除 / 強化強奪）
            //      ・相手の支えを止める（封印）
            //      ・止め方を分ける（眠り＝殴ると起きる / スタン＝起きない）
            //      ・硬い相手を抜く（貫き）
            //      ・倒れてからの手を残す（蘇生）
            // ⚠️ 効果の種類は1つも足していない。実装済みのプリミティブを技にしただけ。

            // ⭐ 行動順そのものへ触る2本。⚠️ これが無いと「順番」はゲージ任せのまま
            new Skill("gauge-drain", "ゲージ減少", "敵1体のゲージを大きく戻す", SkillType.Debuff, 5, Target.EnemyOne,
                Effect.Gauge(-40, chance: 65)),
            new Skill("gauge-boost", "ゲージ上昇", "味方1体のゲージを進める", SkillType.Support, 4, Target.AllyOne,
                Effect.Gauge(30)),

            // ⭐ 打ち消しの層。⚠️ 免疫は**強化**なので、これで剥がせる
            //    ＝「剥がしてから弱化を通す」という2手の組み立てが生まれる
            new Skill("dispel", "強化解除", "敵1体の強化を2つ消す", SkillType.Debuff, 5, Target.EnemyOne,
                Effect.Dispel(2, chance: 70)),
            new Skill("buff-steal", "強化強奪", "敵1体の強化を1つ、自分へ移す", SkillType.Debuff, 5, Target.EnemyOne,
                Effect.Steal(1, chance: 55)),

            // ⭐ 外から受け取る回復と強化だけを止める。⚠️ 自然に溜まるゲージと CT は止まらない
            // ⚠️ 名前を「封印」にしていたが、CT を延ばす「封じ」と紛らわしかった。
            //    ⭐ 効果そのものの名（毒・スタン・免疫・シールドと同じ付け方）に揃える。
            new Skill("block", "ブロック", "敵1体が回復と強化を受け取れなくなる", SkillType.Debuff, 5, Target.EnemyOne,
                Effect.Block(2, chance: 60)),

            // ⭐ スタンと別物。⚠️ **殴ると起きる**ので、殴る順番が問われる
            new Skill("sleep", "睡眠", "敵1体を眠らせる。攻撃を受けると起きる", SkillType.Debuff, 5, Target.EnemyOne,
                Effect.Sleep(2, chance: 55)),

            // ⭐ 硬い相手を抜く。⚠️ 盾は抜けない（そこは剥がすか多段で削る）
            new Skill("pierce-strike", "防御無視攻撃", "防御を計算に入れずに斬る", SkillType.Attack, 5, Target.EnemyOne,
                Effect.Damage(PowerTier.Medium, DamageScale.Atk, pierce: true)),

            // ⭐ 倒れてからの手。⚠️ 強化も弱化も持ち越さない（ゲージと CT は続き）
            new Skill("revive", "蘇生", "倒れた味方1体を呼び戻す", SkillType.Heal, 7, Target.AllyDown,
                Effect.Revive(40)),

            // ── しかけて回収する層（2026-08-19・🚧 まだ配っていない）───────────
            // ⭐ 特性に「条件付きの層」（追い打ち＝弱化した相手に強い、など）を足したのと対。
            //    参考にした放置RPGでは「弱化を**置く**アクティブ」と「置いた弱化を**回収する**
            //    固定パッシブ」の2段で個性を作っていた。ここは置く側の札と、
            //    既存の軸（全体は1段下げ / 効き目と確率のトレードオフ / 多段 / 複合）の空きを埋める札。
            // ⚠️ 効果の種類は1つも足していない。実装済みプリミティブの組み合わせだけ。
            // ⚠️ **どの種族のプールにも入れていない**（作者指示 2026-08-19「あてはめはまだいらない」）。
            //    配るまで Undistributed に載せる（載せないと Audit が「手に入らない」と落ちる）。

            // ⭐ 弱化を「広く置く」2本。追い打ち・狙い澄ましの回収先が増える
            // ⚠️ 全体なので1段下げる: 単体の毒（4T・100%）に対し 3T・50%
            new Skill("poison-all", "毒・全体", "敵全体に毒を入れる。半分は外れる", SkillType.Debuff, 5, Target.EnemyAll,
                Effect.Poison(1, 3, chance: 50)),
            // ⭐ 殴りながら置く。⚠️ ダメージは必ず当たり、外れるのはスタンだけ
            new Skill("stun-strike", "痺れ打ち", "ダメージを与え、たまにスタンも入れる", SkillType.Debuff, 5, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk),
                Effect.Stun(1, chance: 45)),

            // ⭐ 剥がしてから通す層の複合版。強化解除（2個・CT5）より軽い代わりに殴れる
            new Skill("strip-strike", "剥ぎ打ち", "ダメージを与え、高い確率で強化も1つ消す", SkillType.Attack, 5, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk),
                Effect.Dispel(1, chance: 70)),

            // ⭐ 濃さと確率のトレードオフ。毒・大（2重・65%）の回復側の鏡
            new Skill("regen-heavy", "リジェネ・大", "リジェネを2重に掛ける。やや外れやすい", SkillType.Heal, 5, Target.AllyOne,
                Effect.Regen(2, 4, chance: 65)),
            // ⭐ ゲージ上昇（30%・確実）の博打側。⚠️ 味方に掛けるので素の率がそのまま賭け
            new Skill("gauge-boost-heavy", "ゲージ上昇・大", "味方1体のゲージを大きく進める。半分は失敗する", SkillType.Support, 5, Target.AllyOne,
                Effect.Gauge(60, chance: 50)),
            // ⭐ 挑発（3回・確実・CT3）の長持ち側。置き土産・背水の「わざと受ける」相方
            new Skill("taunt-long", "挑発・長", "敵1体を長く釘付けにする。3割は外す", SkillType.Debuff, 5, Target.EnemyOne,
                Effect.Taunt(5, chance: 70)),

            // ⭐ 硬い相手への締めの一撃。⚠️ 防御無視攻撃（中・CT5）より1段上で、そのぶん遠い
            new Skill("pierce-strike-heavy", "防御無視強攻撃", "防御を計算に入れない大きな一撃。次が遠い", SkillType.Attack, 5, Target.EnemyOne,
                Effect.Damage(PowerTier.Large, DamageScale.Atk, pierce: true)),
            // ⭐ 全体 × 多段。盾を**面で**剥がす唯一の札（執念持ちの敵には貢ぎ物になる読み合い）
            new Skill("attack-all-twice", "全体連撃", "敵全体に小さな一撃を2回。盾を広く剥がす", SkillType.Attack, 7, Target.EnemyAll,
                Effect.Damage(PowerTier.Small, DamageScale.Atk, 2)),

            // ⭐ 蘇生（40%・確実）の博打側。⚠️ 外すと1手が丸ごと消える
            new Skill("revive-heavy", "蘇生・大", "倒れた味方を手厚く呼び戻す。4割は失敗する", SkillType.Heal, 7, Target.AllyDown,
                Effect.Revive(70, chance: 60)),
            // ⭐ 複合（スピードUP＋ゲージ上昇）。⚠️ 複合は名前で両方言えないので短い名
            new Skill("tailwind", "追い風", "味方1体のスピードを上げ、ゲージも進める", SkillType.Support, 5, Target.AllyOne,
                Effect.Buff(StatKey.Spd, 1, 3),
                Effect.Gauge(25)),
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
        /// 免疫が弾くか / 命中と抵抗の差で通る率が動くか / 特性（狙い澄まし・意地）が効くか。
        /// ⭐ 「弱化」というひとつの括りなので、3つが揃って動くのが正しい。
        ///
        /// ⚠️ **CT延長・封じが漏れていた**（2026-08-17 修正）。
        /// 免疫で防げず、命中と抵抗の差でも動かず、狙い澄ましも効かない**唯一の弱化**になっていた。
        /// ⚠️ CT の効果は短縮（自分に掛ける）と延長（相手に掛ける）が同じ種類なので、
        /// **向きで見分ける**（延長だけが弱化）。</summary>
        public static bool IsHarmful(Effect effect)
        {
            if (effect.Kind == EffectKind.Buff) return effect.Sign < 0;
            if (effect.Kind == EffectKind.Ct) return effect.Delta > 0;
            // ⚠️ ゲージは符号で向きが変わる。減らす側だけが弱化
            if (effect.Kind == EffectKind.Gauge) return effect.Percent < 0;
            // ⭐ 割合は負なら削る側 ＝ 弱化。⚠️ 正なら回復なので害ではない
            if (effect.Kind == EffectKind.HealRatio) return effect.Percent < 0;
            // ⭐ 解除は負なら「弱化を剥がす」＝ 相手の得になる。害ではない
            if (effect.Kind == EffectKind.Dispel) return effect.Count > 0;
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

        /// <summary>プレイヤーが手に入れられない技。⭐ **ヌシの看板**なので、あえて配らない。
        ///
        /// ⚠️ ここに書いたものだけが許される。書き忘れた技は Audit が「手に入らない」と落とす。
        /// ⚠️ 相手の CT を画面に出さないのは「相手の技はプレイヤーも持っている＝CT を知っている」
        /// という前提に立っているので、**この表が増えるほどその前提が崩れる**（開発/課題）。</summary>
        /// <summary>どの種族の卵ガチャにも入らない技。⭐ **相手が使うのを見るだけ。**
        /// ⚠️ public なのは Wiki と図鑑が「手に入りません」と書き添えるため
        /// （印が無いと、表に並んでいる以上プレイヤーは取れると読む）。</summary>
        public static readonly HashSet<string> BossOnly = new HashSet<string> { "attack-all-heavy" };

        /// <summary>実装と検査は済んでいるが、**まだどの種族にも配っていない技**。
        ///
        /// ⭐ 作者指示（2026-08-19）「特性とスキルの追加はする。キャラへのあてはめはまだいらない」。
        /// 技の中身と釣り合いを先に固め、どの種族のどの枠に置くかは別の判断として残す。
        /// ⚠️ 配ったらこの表から**必ず消す**（残っていると <see cref="Audit"/> が
        /// 「配ったのに印が残っている」と落ちる ── Wiki が「未配布」と嘘をつくのを止めるため）。
        /// ⚠️ public なのは Wiki と図鑑が「未配布」と書き添えるため（BossOnly と同じ理由）。</summary>
        /// ⭐ 2026-08-19 に**全件が配られた**（キバネ・イワオ・ホムラの3種族へ）ので空。
        /// ⚠️ 空でも消さない ── 次に技を足したとき、配る前でも `Audit` を通せる置き場が要る。
        public static readonly HashSet<string> Undistributed = new HashSet<string>();

        /// <summary>技表とガチャプールの整合を数える。
        ///
        /// ⭐ **件数を数えない。** 数えると技を足すたびに落ちるので、
        /// 検査を緩める圧力になる（そして緩めたら二度と戻らない）。
        /// 見るのは「規則を守っているか」と「繋がっているか」だけ。
        ///
        /// ⚠️ ここが catch するのは、足した日には気づけない類のものばかり:
        /// AI が採点しない効果 / どの巣からも出ない技 / 実在しない id を指すプール。
        /// どれも**コンパイルは通り、遊べてしまう**。</summary>
        /// <summary>表の不備を投げる。⚠️ 起動時に呼ぶ。</summary>
        public static void Audit()
        {
            var problems = Faults();
            if (problems.Count > 0)
            {
                throw new InvalidOperationException("技表の不備:\n  " + string.Join("\n  ", problems));
            }
        }

        /// <summary>不備を**投げずに数える**。
        ///
        /// ⭐ **帳面が「貼ったらどうなるか」を先に言うための口**（2026-08-19）。
        /// ⚠️ これが無かった頃は、帳面が通した技を貼った瞬間に <see cref="Audit"/> が投げ、
        /// 292件の検査のどれが自分のせいか読み解く羽目になっていた。
        /// ⭐ 規則はここ1か所。帳面用に書き写さない（写すと必ず片方が古くなる）。</summary>
        /// <summary>いまの表の不備。⭐ 起動時の <see cref="Audit"/> が使う。</summary>
        public static List<string> Faults() => Faults(List, SpeciesTable.All);

        /// <summary>**渡された表**の不備。
        ///
        /// ⭐ **帳面が「貼ったらどうなるか」を先に言うための口**（2026-08-19）。
        /// ⚠️ **規則をここ以外に書き写さない。**写した瞬間から片方が古くなる
        /// ── この道具は同じ形の食い違いを何度も踏んでいる。
        /// ⭐ 世界の状態は触らない（表を引数で受けるので、検査中に遊びが影響を受けない）。</summary>
        public static List<string> Faults(IReadOnlyList<Skill> table, IReadOnlyList<Species> speciesTable)
        {
            var problems = new List<string>();

            var seen = new HashSet<string>();
            foreach (var skill in table)
            {
                if (!seen.Add(skill.Id)) problems.Add($"技 id が重複している: {skill.Id}");
                if (skill.Effects.Count == 0) problems.Add($"{skill.Id}: 効果が1つも無い");
                if (skill.Name.Length == 0) problems.Add($"{skill.Id}: 名前が空");
                if (skill.Gist.Length == 0) problems.Add($"{skill.Id}: 画面に出す短い説明が空");
                if (skill.Ct < 0) problems.Add($"{skill.Id}: CT が {skill.Ct}");
                // ⚠️ **上限は約束ごと。**注釈に書いてあるだけだと、次に技を足す人が踏む
                int cap = IsHeavyCt(skill) ? CtHeavy : CtCap;
                if (skill.Ct > cap)
                {
                    problems.Add($"{skill.Id}: CT {skill.Ct} は上限 {cap} を超えている"
                        + (cap == CtCap ? $"（盤面をひっくり返す技だけが {CtHeavy} を許される）" : ""));
                }

                // ⚠️ **上げても何も起きない段**を弾く。これが無いと
                //    「Lv3 にしたのに何も変わらない」が黙って通る（画面には出るのに実体が無い）
                var growth = GrowthOf(skill);
                if (growth.Count != MaxLevel - 1)
                {
                    problems.Add($"{skill.Id}: 伸ばせる軸が1つも無い（成長表が {growth.Count} 段）");
                }
                // ⚠️ 枠1（種族固定）に入る技は、CT の段が死ぬ。詰め替えが効いているか数える
                foreach (var species in speciesTable)
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
            foreach (var species in speciesTable)
            {
                reachable.Add(species.Skill1);

                // ⚠️ **型の縛りを外したので、代わりにここで数える**（2026-08-19）。
                //    ⭐ 縛りが1つ消えるなら、それが守っていたものを別の形で数え直すこと。
                var a = species.Slot2.Pool;
                var b = species.Slot3.Pool;

                if (a.Count == 0 || b.Count == 0)
                    problems.Add($"{species.Id}: 袋が空（枠2 {a.Count} / 枠3 {b.Count}）");
                if (a.Count > PoolMax || b.Count > PoolMax)
                {
                    problems.Add($"{species.Id}: 袋が大きい（枠2 {a.Count} / 枠3 {b.Count}"
                        + $" ── 上限 {PoolMax}）。狙える確率はここで決まる");
                }

                // ⚠️ 同じ技が2枠に居ると、片方が無駄になる
                foreach (var id in a)
                {
                    bool twice = false;
                    foreach (var other in b) if (other == id) { twice = true; break; }
                    if (twice) problems.Add($"{species.Id}: {id} が枠2 と枠3 の両方に居る");
                }

                // ⚠️ **1つの役割に偏らない。**型で縛っていた頃はこれが自動で守られていた
                var roles = new HashSet<SkillType>();
                foreach (var id in a) if (Has(table, id)) roles.Add(TypeOf(ById(table, id)));
                foreach (var id in b) if (Has(table, id)) roles.Add(TypeOf(ById(table, id)));
                if (roles.Count < 2)
                    problems.Add($"{species.Id}: 2つの袋が同じ役割しか持たない（分けた意味が無い）");

                var inPool = new HashSet<string>();
                for (int slot = 0; slot < 2; slot++)
                {
                    var declared = slot == 0 ? species.Slot2 : species.Slot3;
                    if (declared.Pool.Count == 0)
                    {
                        problems.Add($"{species.Id}: 枠{slot + 2} のプールが空（種族を足したら必ず要る）");
                        continue;
                    }
                    foreach (var id in declared.Pool)
                    {
                        Skill? found;
                        if (!Index.TryGetValue(id, out found))
                        {
                            problems.Add($"{species.Id} 枠{slot + 2} が実在しない技 {id} を指している");
                            continue;
                        }
                        // ⚠️ **型の一致はもう見ない**（2026-08-19 に袋の型縛りを外した）。
                        //    ⭐ 代わりに上で「役割が1つに偏っていないか」を数えている。
                        if (!inPool.Add(id)) problems.Add($"{species.Id} のプールで {id} が重複している");
                        reachable.Add(id);
                    }
                }

                // ⭐ 枠2・3 を別々に引くので、枠1を除いて各1件は要る
                for (int slot = 0; slot < 2; slot++)
                {
                    int usable = SlotPoolOf(species.Id, slot + 1, species.Skill1).Count;
                    if (usable < 1)
                    {
                        problems.Add($"{species.Id}: 枠{slot + 2} が枠1を除くと空になる");
                    }
                }
            }

            // ⚠️ **狙い先と中身の食い違いは、表でも数える。**
            //    ⭐ 帳面だけが見ていた頃、C# に直接書いた「味方全体に毒」が素通りし、
            //    説明文まで作られていた（2026-08-19 の監査）。
            foreach (var skill in table)
            {
                bool harmful = false, kindly = false;
                foreach (var e in skill.Effects)
                {
                    if (IsHarmful(e)) harmful = true;
                    else if (e.Kind != EffectKind.Damage) kindly = true;
                }
                bool atFoe = skill.Target == Target.EnemyOne || skill.Target == Target.EnemyAll
                    || skill.Target == Target.EnemyRandom;
                if (harmful && !atFoe)
                    problems.Add($"{skill.Id}: 弱化を持つのに狙いが「{SkillText.TargetOf(skill.Target)}」");
                if (kindly && !harmful && atFoe)
                    problems.Add($"{skill.Id}: 味方に効くものを「{SkillText.TargetOf(skill.Target)}」へ向けている");
            }

            // ⚠️ **1つの技をあちこちの袋に入れない。**入れると「どこで奪っても同じ」に戻り、
            //    巣を選ぶ理由が消える（型で縛っていた頃、受け皿不足で実際にそうなっていた）。
            var homes = new Dictionary<string, int>();
            foreach (var species in speciesTable)
            {
                foreach (var id in species.Slot2.Pool) Bump(homes, id);
                foreach (var id in species.Slot3.Pool) Bump(homes, id);
            }
            foreach (var pair in homes)
            {
                if (pair.Value > SpreadMax)
                {
                    problems.Add($"{pair.Key}: {pair.Value} か所の袋に居る（上限 {SpreadMax}）"
                        + " ── どこで奪っても同じになる");
                }
            }

            foreach (var skill in table)
            {
                if (!reachable.Contains(skill.Id) && !BossOnly.Contains(skill.Id)
                    && !Undistributed.Contains(skill.Id))
                {
                    problems.Add($"{skill.Id}: どの種族の枠1にもプールにも無い。**手に入らない技**になっている");
                }
            }

            // ⚠️ 「未配布」の印の腐りを両方向で止める:
            //    実在しない id を指す印 / 配ったのに残っている印。
            //    後者を放すと Wiki と図鑑が「未配布」と嘘をつき続ける
            foreach (var id in Undistributed)
            {
                if (!seen.Contains(id)) problems.Add($"未配布の表が、実在しない技 {id} を指している");
                else if (reachable.Contains(id)) problems.Add($"{id}: 配ったのに未配布の印が残っている");
            }

            return problems;
        }

        /// <summary>その種族の卵から出うる技。⚠️ 表に無い種族は黙って空にせず投げる。
        /// 枠1（種族固定）と同じ技はここで外す。
        /// ⭐ プールの実体は <see cref="Species.Gacha"/>（種族の行）が持つ。ここは絞るだけ。</summary>
        public static List<string> GachaPoolOf(string speciesId, string skill1)
        {
            var species = SpeciesTable.ById(speciesId);
            var result = new List<string>();
            AddPool(result, species.Slot2.Pool, skill1);
            AddPool(result, species.Slot3.Pool, skill1);
            return result;
        }

        /// <summary>その枠だけのプール。⭐ **枠2 と枠3 は別のタイプから引く。**
        /// ⚠️ 同じプールから2つ取っていた頃は、狙った組み合わせが 2.8〜4.8% でしか出なかった。</summary>
        public static List<string> SlotPoolOf(string speciesId, int slot, string skill1)
        {
            var species = SpeciesTable.ById(speciesId);
            var source = slot == 1 ? species.Slot2.Pool : species.Slot3.Pool;
            var result = new List<string>(source.Count);
            AddPool(result, source, skill1);
            return result;
        }

        private static void AddPool(List<string> into, IReadOnlyList<string> from, string skill1)
        {
            foreach (var id in from)
            {
                // ⚠️ 枠1 と同じ技は外す。同じ技が2枠を占めると片方が無駄になる
                if (id != skill1 && !into.Contains(id)) into.Add(id);
            }
        }
    }
}
