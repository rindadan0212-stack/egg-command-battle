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

    /// <summary>技に付く**条件**。⭐ 満たしていなければ、その効果だけが出ない。
    ///
    /// ⚠️ <see cref="TraitWhen"/> と**別物**。あちらは「瞬間」（〜したとき）で、
    /// 特性が戦闘に割り込むための札。⭐ 技は**押した瞬間に判定する**ので瞬間は使えない
    /// （「攻撃を受けたとき」に押すことはできない）。だから技の条件は**状態**で作る。
    ///
    /// ⭐ 選ぶ基準は特性と同じ ── **プレイヤーが画面で確かめられ、作りに行けるものだけ**。
    /// ⚠️ 相手の CT（画面に出さないと決めてある）、ゲージ（動き続ける）、
    /// 経過ターン（本作に「全体のターン」が無い）は採らない。</summary>
    public enum SkillWhen
    {
        /// <summary>効果の当たる相手に弱化が1つでも付いている。⭐ 状態アイコンで分かる。</summary>
        FoeWeakened,
        /// <summary>同・強化が1つでも付いている。</summary>
        FoeBoosted,
        /// <summary>同・スタンか睡眠で止まっている。</summary>
        FoeStopped,
        /// <summary>同・残り HP が最大の半分以下。⭐ HP バーで分かる。</summary>
        FoeHalf,
        /// <summary>自分の残り HP が最大の半分以下。</summary>
        SelfHalf,
    }

    /// <summary>⭐ **盤面の何を数えて効き目を変えるか。**
    ///
    /// ⭐ これがある理由（2026-08-20）: 本作の効果は全部**定数**で、
    /// 「盤面を数えて効き目が変わる技」が1本も無かった。
    /// ⚠️ そのせいで仕込む技（毒・弱化・CT延長）は在るのに**回収する側が無く**、
    /// 仕込みが「ゆっくり効くダメージ」にしかならなかった。
    ///
    /// ⚠️ 数えられるのは**ダメージだけ**。状態そのものを数で増やすと、
    /// 上限の無い積み上げ（毒10重ね等）への入口になる。</summary>
    public enum Tally
    {
        /// <summary>数えない（既定）。</summary>
        None,
        /// <summary>⭐ 相手に乗っている**弱化の種類数**。仕込みの回収先そのもの。</summary>
        FoeBanes,
        /// <summary>相手に乗っている強化の数。⭐ 積んだ相手への罰。</summary>
        FoeBoons,
        /// <summary>自分に乗っている強化の数。⭐ 「配ってから撃つ」を筋にする。</summary>
        OwnBoons,
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

        // ── 2026-08-27 に足した5つ（参考作品との突き合わせで「本作に語彙が無い」と出たもの）──
        // ⚠️ **技には落とし込んでいない**（作者の指示）。語彙として在るだけ。

        /// <summary>⭐ **封印。**⚠️ 枠2・3 が押せなくなる（枠1 の通常攻撃だけ残る）。
        /// ⭐ スタンとの違い: **手番は来る。**来るが、できることが1つに減る。
        /// ⚠️ だから「動けない」ではなく「**選べない**」── 手番を奪う札より軽い。</summary>
        Seal,
        /// <summary>⭐ **固着（弱化解除無効）。**⚠️ 乗っている弱化が**落とせなくなる**。
        /// ⭐ 仕込む側の札 ── 毒や弱化を置いたあとに固めると、回復役の解除が効かない。</summary>
        Anchor,
        /// <summary>⭐ **無敵。**⚠️ ダメージを**一切受けない**（シールドと違って枚数で減らない）。
        /// ⚠️ 毒は通る（`DealDamage` を通らないため）── 「殴られない」であって「無傷」ではない。</summary>
        Invincible,
        /// <summary>⭐ **弱化延長。**⚠️ 持続を持たない**即時**の効果。
        /// 相手に乗っている弱化の残りを <see cref="Effect.Turns"/> ぶん伸ばす。
        /// ⭐ 何も乗っていなければ何も起きない ── 単体では成立しない、仕込みの回収札。</summary>
        Extend,
        /// <summary>⭐ **反撃。**⚠️ 受けたダメージの一部を返す状態（<see cref="Effect.Turns"/> 回ぶん）。
        /// ⚠️ 特性の「返し身」と**同じ働き**だが、層が違う ──
        /// あちらは生まれつき常時、こちらは**札で買って数手だけ**。</summary>
        Counter,
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

        /// <summary>damage。⭐ **相手に乗っている強化を、この一撃にだけ無かったことにする**
        /// （2026-08-27・作者の指示「強化効果無視攻撃」）。
        ///
        /// ⚠️ <see cref="Pierce"/>（防御無視）と**別物**:
        /// あちらは**素の防御ステ**を踏み倒し、こちらは**買った守り**を踏み倒す。
        /// ⭐ 消えるのは 防御力UP・シールド・無敵・ガッツ の4つ ── どれも「強化」だから。
        /// ⚠️ **毒やリジェネは消えない**（守りではない）。
        ///
        /// ⭐ **無敵に答えがあるべき**なので、無敵も無視する対象に入れてある。
        /// 無効化できない札を1枚も作らない、というのがこの取り合いの前提。</summary>
        public readonly bool Bare;

        /// <summary>効果が通る率（%）。⭐ 100 なら必ず通る（乱数を1度も引かない）。
        ///
        /// ⭐ **効果量と確率をトレードオフにする欄。**
        /// 「効き目は小さいが必ず通る」と「効き目は大きいが半分外す」を、
        /// 同じ効果の種類のまま**別の技として並べられる**。技を増やす軸がこれで1本増える。
        ///
        /// ⚠️ **確率が付くのは、相手が抵抗するものだけ**（<see cref="Skills.IsHarmful"/>）。
        /// ⭐ 味方・自分に掛けるもの（回復・盾・ガッツ・免疫・リジェネ・ゲージ・蘇生）は
        /// **必ず通る**（2026-08-21・作者の指示「味方へのバフの確率は不要」）。
        ///
        /// ⚠️ 外していた頃の形は「効き目は大きいが半分外す」という博打札だった。
        /// ⭐ やめた理由は、**押した手番が丸ごと消える**のが支える側だけに起きること ──
        /// 攻撃側はダメージが必ず入るので、同じ「外れ」でも損の重さが揃っていなかった。
        /// ⚠️ ダメージにも外れは無い（攻撃役の出力が運で決まってしまう）。
        ///
        /// ⚠️ 相手に掛けるものは、実際の率が命中と抵抗の差で上下する（<see cref="Battle.LandChanceOf"/>）。
        ///
        /// ⚠️ **100 のときは乱数を引かない。** これで移植した21技の試合は
        /// 1手も変わらず、較正済みの照合がそのまま生きる。</summary>
        public readonly int Chance;

        /// <summary>⭐ **この効果だけの狙い先。**null なら技の狙い先（<see cref="Skill.Target"/>）に従う。
        ///
        /// ⭐ これがある理由: 技は狙い先を1つしか持てないので、
        /// 「敵全体を殴りながら**自分を回復する**」という**1手2役**が丸ごと書けなかった
        /// （2026-08-20・参考作品の R帯に3技あった）。
        /// ⚠️ 効果の種類は1つも増やしていない ── 既にある効果の**飛び先を変える**だけ。
        /// ⚠️ 付いている効果は本体とは**別の回**で撃つ（<see cref="Battle.PerformAction"/>）。
        ///    同じ回に混ぜると乱数の引き順が変わり、移植した21技の照合が死ぬ。</summary>
        public readonly Target? Own;

        /// <summary>⭐ **生まれつき。**パッシブ技だけが持てる。
        ///
        /// ⭐ 素のステそのものに畳み込むので、次の2つで普通の強化と違う:
        /// <list type="bullet">
        ///   <item>⚠️ **剥がせない**（強化解除・強化強奪の的にならない）</item>
        ///   <item>⭐ **HP にも乗る**（修正枠が無い HP を動かせる唯一の道）</item>
        /// </list>
        /// ⚠️ 効き目は普通の強化より小さい（<see cref="Skills.InnatePercent"/> 対
        /// <see cref="Skills.BuffPercent"/>）── **手番を1回も払わない**ぶんの値段。</summary>
        public readonly bool Innate;

        /// <summary>⭐ **この効果が出る条件。**null なら無条件。
        ///
        /// ⚠️ 判定は**効果が当たる瞬間・対象ごと**。全体技なら敵1体ずつ別々に見る
        /// （⭐ 弱化が付いた敵にだけ深く入る全体技が書ける）。
        /// ⚠️ **技の最初の効果には付けない**（<see cref="Skills.Faults(IReadOnlyList{Skill}, IReadOnlyList{Species})"/>
        /// が落とす）── 全部が条件つきだと、外して押した手番が**丸ごと空振り**する。
        /// ⭐ 「押したら決まったことが起きる」の反対側の失敗を作らないための決まり。</summary>
        public readonly SkillWhen? When;

        /// <summary>⭐ **盤面の何を数えて効き目を変えるか。**<see cref="Tally.None"/> なら定数のまま。
        /// ⚠️ ダメージ専用。効き目は数×<see cref="Skills.PerBonusPercent"/>%
        /// （<see cref="Skills.PerCap"/> で頭打ち）。</summary>
        public readonly Tally Per;

        private Effect(EffectKind kind, PowerTier power, DamageScale scale, StatKey stat, int sign,
            int turns, int stacks, int percent, int count, int delta, int hits, int repeat = 1,
            int chance = 100, bool pierce = false, Target? own = null, bool innate = false,
            SkillWhen? when = null, Tally per = Tally.None, bool bare = false)
        {
            Bare = bare;
            When = when;
            Per = per;
            Innate = innate;
            Own = own;
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
            bool pierce = false, bool bare = false) =>
            new Effect(EffectKind.Damage, power, scale, default, 0, 0, 0, 0, 0, 0, 0, repeat,
                100, pierce, null, false, null, Tally.None, bare);

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

        // ── 2026-08-27 に足した5つ ─────────────────────────

        /// <summary>封印。⭐ 枠2・3 が押せなくなる（枠1 だけ残る）。</summary>
        public static Effect Seal(int turns, int chance = 100) =>
            new Effect(EffectKind.Seal, default, default, default, 0, turns, 0, 0, 0, 0, 0,
                1, chance);

        /// <summary>固着。⭐ 乗っている弱化を落とせなくする。</summary>
        public static Effect Anchor(int turns, int chance = 100) =>
            new Effect(EffectKind.Anchor, default, default, default, 0, turns, 0, 0, 0, 0, 0,
                1, chance);

        /// <summary>無敵。⭐ ダメージを受けない。⚠️ 味方に配る札なので率は付けない。</summary>
        public static Effect Invincible(int turns) =>
            new Effect(EffectKind.Invincible, default, default, default, 0, turns, 0, 0, 0, 0, 0,
                1, 100);

        /// <summary>弱化延長。⭐ 相手に乗っている弱化の残りを伸ばす（即時）。</summary>
        public static Effect Extend(int turns, int chance = 100) =>
            new Effect(EffectKind.Extend, default, default, default, 0, turns, 0, 0, 0, 0, 0,
                1, chance);

        /// <summary>反撃。⭐ 受けたダメージの一部を返す（味方に配る札）。</summary>
        public static Effect Counter(int turns) =>
            new Effect(EffectKind.Counter, default, default, default, 0, turns, 0, 0, 0, 0, 0,
                1, 100);

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

        /// <summary>弱化解除。⭐ 乗っている**弱化**を <paramref name="count"/> 個落とす。
        ///
        /// ⚠️ 効果の種類は増やしていない ── <see cref="Dispel"/> の**負の側**そのもの。
        /// ⭐ 「符号で向きが変わるものは種類を分けない」流儀（CT・ゲージ・割合と同じ）。
        /// 名前を付けたのは、技表に <c>Dispel(-2)</c> と書くと**読めない**から。
        /// ⚠️ 落ちる順は「重いものから」（<see cref="Battle.StripBanes"/>）。
        /// スタンや毒を残して弱化だけ消えると、治した手応えにならない。</summary>
        public static Effect Cleanse(int count, int chance = 100) => Dispel(-count, chance);

        /// <summary>⭐ **この効果だけを別の相手へ飛ばす。**技の狙い先は変えない。
        ///
        /// ⭐ 使いどころ: 「敵全体<c>.To(Self)</c> で自分だけ回復」のような1手2役。
        /// ⚠️ 元の効果は書き換えない（新しい1つを返す）。</summary>
        public Effect To(Target target) => Copy(own: target);

        /// <summary>⭐ **この効果に条件を付ける。**満たさなければこの効果だけ出ない。</summary>
        public Effect If(SkillWhen when) => Copy(when: when);

        /// <summary>⭐ **盤面を数えて効き目を変える。**⚠️ ダメージにだけ付く。</summary>
        public Effect Each(Tally per)
        {
            if (Kind != EffectKind.Damage)
                throw new ArgumentException($"数えられるのはダメージだけ（{Kind} に付けようとした）");
            return Copy(per: per);
        }

        /// <summary>欄を1つだけ差し替えた写し。⚠️ 元は書き換えない。</summary>
        private Effect Copy(Target? own = null, SkillWhen? when = null, Tally? per = null) =>
            new Effect(Kind, Power, Scale, Stat, Sign, Turns, Stacks, Percent, Count, Delta, Hits,
                Repeat, Chance, Pierce, own ?? Own, Innate, when ?? When, per ?? Per, Bare);

        /// <summary>⭐ **生まれつきのステ上昇・下降。**パッシブ技だけが持てる。
        ///
        /// ⚠️ <see cref="Buff"/> と違って **HP も動かせる**
        /// （素のステに畳み込むので、修正枠が要らない）。
        /// ⚠️ 確率は無い ── 生まれつきなので外れようがない。</summary>
        public static Effect Always(StatKey stat, int sign)
        {
            if (stat != StatKey.Hp && stat != StatKey.Atk && stat != StatKey.Def
                && stat != StatKey.Spd)
                throw new ArgumentException($"生まれつきは hp/atk/def/spd のみ（{stat} が渡された）");
            if (sign != 1 && sign != -1)
                throw new ArgumentException($"生まれつきの sign は ±1（{sign} が渡された）");
            return new Effect(EffectKind.Buff, default, default, stat, sign, Skills.Lasting, 0, 0,
                0, 0, 0, 1, 100, false, null, innate: true);
        }
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
        /// <summary>生まれつきの効き目 +<see cref="Skills.GainInnatePoints"/>pt。
        /// ⭐ パッシブ技だけが持つ軸（⚠️ CT が無いので他に伸ばす先が無い）。</summary>
        Innate,
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
        /// <summary>生まれつきの効き目に足す %ポイント。</summary>
        public int ExtraInnate;

        public bool IsNone => PowerPercent == 0 && CtCut == 0 && ChancePoints == 0
            && ExtraTurns == 0 && ExtraRepeat == 0 && ExtraPercent == 0 && ExtraCount == 0
            && ExtraAmount == 0 && ExtraInnate == 0;
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
        /// <summary>使ったあと、自分が何回行動するまで使えないか。⚠️ 枠1では常に 0 扱い。
        ///
        /// ⭐ **技表に書かない。**<see cref="Skills.PriceOf"/> が効果から導く（2026-08-20）。
        /// ⚠️ 手で書いていた頃、64技中 44技（69%）が CT5 に張り付いていた ──
        /// 値段が一律だと「重いが強い／軽いが弱い」の比べ方が起きない。
        /// ⚠️ 気に入らない技だけ `CtOverrides` に書く（成長表と同じ流儀）。</summary>
        public int Ct => _ct >= 0 ? _ct : (_ct = Skills.PriceOf(this));

        // ⚠️ 1度だけ数えて持つ。⭐ 毎手番に呼ばれるので、都度たどると効果の数だけ回る
        private int _ct = -1;
        public readonly Target Target;
        public readonly IReadOnlyList<Effect> Effects;
        /// <summary>卵の枠2・3 のどちらから出るかを決める型。
        ///
        /// ⚠️ **手で書く。**効果から導いていたが、
        /// 「攻撃しつつスタンを付ける」のような複合技が必ずアタックに寄ってしまい、
        /// **デバフ枠に置けなかった**（＝崩す札として作ったのに崩す枠から出ない）。
        /// ⭐ どちらの枠から出したいかは作り手が決めることなので、宣言する。</summary>
        public readonly SkillType Type;

        /// <summary>⭐ **押せない技。**枠は普通に1つ使うが、選ぶ対象には出てこない。
        ///
        /// ⭐ これがある理由（2026-08-20・作者の指示「まもダンにそろえて」）:
        /// 「常に防御が高い」を**手番1回で買う**形にしていたが、参考作品は
        /// **枠を1つ潰しっぱなしにして**買っている。⭐ そちらに合わせた。
        /// ⚠️ 値段の払い方が違う ── 手番ではなく**枠**で払う。
        /// だから効き目は小さく（<see cref="Skills.InnatePercent"/>）、代わりに永久で剥がせない。
        ///
        /// ⚠️ パッシブが持てるのは<see cref="Effect.Always"/>（生まれつき）だけ。
        /// ⭐ 「攻撃するたび〜」のような**引き金つき**は特性（<see cref="Trait"/>）の仕事で、
        /// ここには入れない（同じことを2か所で表せるようにしない）。</summary>
        public readonly bool Passive;

        public Skill(string id, string name, string gist, SkillType type, Target target,
            params Effect[] effects)
            : this(id, name, gist, type, target, false, effects)
        {
        }

        /// <summary>パッシブを作る。⚠️ CT は必ず 0（押せないので待ちようが無い）。</summary>
        public static Skill Always(string id, string name, string gist, SkillType type,
            params Effect[] effects) =>
            new Skill(id, name, gist, type, Target.Self, true, effects);

        private Skill(string id, string name, string gist, SkillType type, Target target,
            bool passive, Effect[] effects)
        {
            Passive = passive;
            Id = id;
            Name = name;
            Gist = gist;
            Type = type;
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
                case SkillGain.Innate: return "生まれつき";
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

        /// <summary>攻撃・防御の強化／弱化が動かす割合（%）。⭐ **作者の指示 2026-08-27 で 30 → 50。**
        ///
        /// ⚠️ **30 では手番の元が取れなかった。**`sim turnvalue` の実測で
        /// 攻撃力UP が 0.63手ぶん（枠1 で殴るのが 1.0）── 掛けるより殴ったほうが得だった。
        /// ⭐ 50% × 3ターン × 後効きの割引 0.7 ＝ 約 1.05手ぶんで、ようやく釣り合う。</summary>
        public const int BuffStatPercent = 50;

        /// <summary>速度の強化／弱化が動かす割合（%）。⚠️ **攻撃・防御より小さい**（作者の指示）。
        ///
        /// ⭐ 速度は「行動回数」も「潜入の飛距離」も持っていて**二重に効く**ので、
        /// 同じ 50% にすると1つの札で2つの軸を動かせてしまう。</summary>
        public const int BuffSpdPercent = 30;

        /// <summary>その軸の強化／弱化が動かす割合（%）。⭐ **唯一の出所。**
        /// ⚠️ 呼ぶ側で <c>stat == Spd ? 30 : 50</c> と書かないこと ──
        /// 軸を足した日に、書き写した側だけが古い値のまま残る。</summary>
        public static int BuffPercentOf(StatKey stat) =>
            stat == StatKey.Spd ? BuffSpdPercent : BuffStatPercent;

        /// <summary>⭐ **防御の強化／弱化は、ステではなく「被ダメージ」に掛かる**（2026-08-27）。
        ///
        /// ⚠️ **ステに掛けると、言った割合が実際には出ない。**ダメージ式の軽減は
        /// <c>(DefSoften ÷ (DefSoften ＋ 防御))²</c> で、DefSoften が防御そのものより
        /// ずっと大きい。実測では **防御 +30% が被ダメ −3%** にしかならず、
        /// 防御力UP・DOWN は 0.06手ぶん ＝ ゲーム内で最も無意味な2技だった。
        ///
        /// ⚠️ **ステ側で 50% を出すことはできない。**式を解くと DefSoften を
        /// 防御の 0.2倍（1700 → 約40）まで落とす必要があり、そのときの軽減率は 97% になる。
        /// ⭐ だから**掛ける先を変える** ── 防御力UP ＝ 被ダメ ×0.5、DOWN ＝ ×1.5。
        ///
        /// ⚠️ **副作用**: 防御依存攻撃（<see cref="DamageScale.Def"/>）は
        /// **素の防御**で伸びる。防御力UP を掛けても火力は上がらない
        /// （硬さの源と、被ダメの割引を、別のものとして分けた）。</summary>
        public static bool GuardsDamage(StatKey stat) => stat == StatKey.Def;

        /// <summary>⭐ **切れない持続。**<see cref="Effect.Buff"/> の残り回数にこれを渡すと、
        /// 戦闘が終わるまで残る強化／弱化になる。
        ///
        /// ⭐ これがある理由: 参考作品には「常に防御が上がっている」型の札があるが、
        /// あちらは**技枠を1つ潰しっぱなし**にして買っている（パッシブ）。
        /// 本作に枠を潰す形は無いので、**手番1回**で買う形に置き換えた（2026-08-20）。
        /// ⚠️ 剥がせる（<see cref="EffectKind.Dispel"/>・<see cref="EffectKind.Steal"/> の対象）。
        /// 剥がせないと「先に掛けた者勝ち」になって読み合いが消える。
        /// ⚠️ 負の値なのは、残り回数を数える側（<see cref="Battle"/>）が
        /// **0 かどうかだけ**を見て「掛かっているか」を判じているから。
        /// 大きな正の数で代用すると、数え続けていつか切れる ── それは永続ではない。
        /// ⚠️ スキルレベルの「持続が伸びる」は乗らない（既に切れないので伸びしろが無い）。</summary>
        public const int Lasting = -1;

        /// <summary>生まれつきのステ上昇・下降が動かす割合（%）。
        ///
        /// ⚠️ <see cref="BuffPercent"/> より**小さい**。⭐ 払い方が違うから:
        /// 強化は**手番1回**で買って数手で切れる。生まれつきは**枠1つ**で買って永久に続く。
        /// ⚠️ ここを強化と同じにすると、強化を掛ける技を選ぶ理由が消える。</summary>
        public const int InnatePercent = 10;

        /// <summary>スキルレベル1段で、生まれつきの効き目に足す %ポイント。</summary>
        public const int GainInnatePoints = 2;

        /// <summary>盤面を数える技が、1つにつき増やす威力（%）。</summary>
        public const int PerBonusPercent = 30;

        /// <summary>⚠️ 数える上限。⭐ 頭打ちが無いと積み上げが青天井になる。</summary>
        public const int PerCap = 4;

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

            // 🔴 **味方に配る持続もの「だけ」を持つ技は、専用式で育てる**（2026-08-27・作者の指示）。
            //    ⭐ 基礎CTを「持続＋1＋段数」まで伸ばして置き、CT を段数ぶん縮めて
            //    ちょうど下限（持続＋1）に着地させる ── 唯一の出所は AllyPersistentSteps
            //    （PriceOf の床・CapFor の天井もここを見る）。
            //    ⚠️ 他に軸がある技（tailwind・dash・harden・warcry 等）はここを通らない
            //    （下の一般式が、今までどおり Turns だけ外した軸で育てる）。
            if (IsPureAllyPersistent(skill))
            {
                int pureSteps = slot == 0 ? 0 : AllyPersistentSteps(LongestAllyTurns(skill));
                var pure = new SkillGain[pureSteps];
                for (int i = 0; i < pureSteps; i++) pure[i] = SkillGain.Ct;
                return pure;
            }

            // ⚠️ 🔴 **味方に配る持続効果（免疫・リジェネ・ガッツ・強化）を持つ技は、
            //    「持続」を成長軸へ入れない**（2026-08-27）。
            //    ⚠️ 持続を伸ばすと、CT をどれだけ縮めても「CT ≧ 持続＋1」がいつか壊れる
            //    （縮む CT と伸びる持続が別の歩幅で動くため）。
            bool guardsAllies = GuardsAllies(skill);
            var axes = NaturalAxesOf(skill, guardsAllies);

            // 🔴 **guardsAllies が Turns・CT を外した結果、軸が1つしか残らない技がある**
            //    （早駆け＝Amount・硬化＝Count・追い風＝Percent・鬨の声＝Chance。2026-08-27 監査で発覚）。
            //    ⚠️ 判定は <see cref="IsSingleAxisFromGuard"/> **1つに寄せる**（ここで
            //    `axes.Count==1` を独自に書くと、Audit 側の判定式と2つに割れる）。

            // ⚠️ 枠1 では CT が効かないので、軸から外してから割り当てる
            if (slot == 0) axes.Remove(SkillGain.Ct);
            if (axes.Count == 0)
            {
                // ⚠️ 伸ばせる軸が1つも無い技。⭐ Audit が読める形で報告できるよう、
                //    ここでは落とさずに空を返す（0除算で落ちると原因が読めない）。
                // ⚠️ **`IsPureAllyPersistent` の技はここへ来ない**（関数の先頭で先に抜けている）。
                //    ここに来るのは、それ以外の理由で軸が無い技 ── 今のところ無いはずの防衛線。
                return new SkillGain[0];
            }

            // 🔴 **軸が1本しか無い技は、4段を無理に埋めない**（作者の指示 2026-08-27）。
            //    ⭐ 段数は `AllyPersistentGrowthSteps` と同じ 2（＝上限 Lv3）に揃える ──
            //    「持続を持つ技は、伸びる軸の広さを問わず CT だけを2段縮めて着地する」という
            //    今日の設計判断と歩調を合わせるための数字で、他に根拠はない。
            //    ⚠️ 1本のまま4段（早駆け＝CT短縮 2→6、硬化＝盾 1→5枚、追い風＝ゲージ 25→45%）は
            //    どれも「単機能の技」の上位互換を専門技より安く踏み倒す（例: 硬化Lv5の盾5枚は
            //    シールド・大の固定4枚＝★5を超える）── 2段（Lv3）なら踏み倒さない。
            int steps = ExpectedGrowthStepsOf(skill);
            var growth = new SkillGain[steps];
            for (int i = 0; i < growth.Length; i++) growth[i] = axes[i % axes.Count];
            return growth;
        }

        /// <summary>その技の成長表が（枠を問わない一般形で）本来何段であるべきか。
        /// ⭐ **唯一の出所** ── <see cref="GrowthOf(Skill, int)"/> 本体・Audit（<see cref="Faults"/>）・
        /// xUnit（未配布でも成長表と採点は揃っている）の3か所が、同じ式を別々に書くと
        /// いつかどれか1つだけ古くなる（②で踏んだのと同じ形の罠を先回りで防ぐ）。
        /// ⚠️ この関数を呼ぶ時点で <see cref="IsPureAllyPersistent"/> 分岐は
        /// 素通りしている想定（<see cref="GrowthOf(Skill, int)"/> はその分岐を先に抜けているので
        /// ここへ来ない。Audit・xUnit 側は素通しでよい ── 内側でもう一度確かめるだけ）。</summary>
        public static int ExpectedGrowthStepsOf(Skill skill)
        {
            if (IsPureAllyPersistent(skill)) return AllyPersistentSteps(LongestAllyTurns(skill));
            // 🔴 **軸が1本しか無い技は、4段を無理に埋めない**（作者の指示 2026-08-27）。
            //    ⭐ 段数は `AllyPersistentGrowthSteps` と同じ 2（＝上限 Lv3）に揃える ──
            //    「持続を持つ技は、伸びる軸の広さを問わず CT だけを2段縮めて着地する」という
            //    今日の設計判断と歩調を合わせるための数字で、他に根拠はない。
            //    ⚠️ 1本のまま4段（早駆け＝CT短縮 2→6、硬化＝盾 1→5枚、追い風＝ゲージ 25→45%）は
            //    どれも「単機能の技」の上位互換を専門技より安く踏み倒す（例: 硬化Lv5の盾5枚は
            //    シールド・大の固定4枚＝★5を超える）── 2段（Lv3）なら踏み倒さない。
            if (IsSingleAxisFromGuard(skill)) return SingleAxisGrowthSteps;
            return MaxLevel - 1;
        }

        /// <summary>guardsAllies で軸が1本に削れた技が育つ段数。
        /// ⚠️ <see cref="AllyPersistentGrowthSteps"/>（味方持続もの専用の2段）と値は同じだが、
        /// **意味が違うので定数を分けてある**（あちらは「持続＋CT」専用の式、こちらは
        /// 「軸が1本しか無い」を汎用に検出した結果）── 値が同じだけで出所が同じではない。</summary>
        public const int SingleAxisGrowthSteps = 2;

        /// <summary>⭐ **その技の上限レベル。唯一の出所** ── 技表に手で書かない。
        ///
        /// 1 ＋ その技の成長の段数（<see cref="GrowthOf(Skill, int)"/> の長さ）。
        /// ⭐ ほとんどの技は 1+4=5（<see cref="MaxLevel"/> と同じ）。
        /// ⚠️ 味方に配る持続もの「だけ」の技（<see cref="IsPureAllyPersistent"/>）は、
        /// 段数が最大2段（<see cref="AllyPersistentSteps"/>）に縮むぶん、
        /// 上限も Lv3 以下（0段なら Lv1 のまま＝育たない）に縮む。
        /// ⭐ <see cref="MaxLevel"/> は「どの技もこれを超えない」**全体の天井**として残る
        /// （<see cref="GrowthOf(Skill, int)"/> が MaxLevel-1 段より多くは返さないため、
        /// ここが超えることは無い）。</summary>
        public static int MaxLevelOf(Skill skill) => 1 + GrowthOf(skill).Count;

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
                    case SkillGain.Innate: boost.ExtraInnate += GainInnatePoints; break;
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
                case SkillGain.Innate: return HasInnate(skill) ? null : "生まれつきの効果が無い";
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

        /// <summary>生まれつきの効き目を持つか。⭐ パッシブ技だけが持つ。</summary>
        private static bool HasInnate(Skill skill)
        {
            foreach (var e in skill.Effects) if (e.Innate) return true;
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

        /// <summary>敵全体からここまでゲージを奪うものは「盤面をひっくり返す」扱い（2026-08-26）。
        /// ⚠️ 手番を丸ごと飛ばすのと同じ重さ ── 短い CT で回すと相手が一度も動けない。</summary>
        public const int HeavyGaugePercent = 50;

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

        /// <summary>⭐ **技の値段（CT）を効果から導く。唯一の出所。**
        ///
        /// ⭐ これがある理由（2026-08-20）: 64技のうち 44技（69%）が CT5 に張り付いていた。
        /// 値段が一律だと「重いが強い／軽いが弱い」という比べ方が起きず、
        /// **強さの差を CT で表していない**状態になっていた。
        ///
        /// ⚠️ **技ごとに手で決めない。**手で決めると、効果を足した日に値段が置き去りになる
        /// （成長表を導出にしたのと同じ理由）。気に入らない技だけ
        /// <see cref="CtOverrides"/> に書く。
        ///
        /// 式: <c>1 ＋ Σ効果の重さ − Σ支払い</c>、床 <see cref="CtFloor"/>／
        /// 天井 <see cref="CtCap"/>／ひっくり返す級は <see cref="CtHeavy"/>。</summary>
        public static int PriceOf(Skill skill)
        {
            int written;
            if (CtOverrides.TryGetValue(skill.Id, out written)) return written;
            // ⚠️ パッシブは押せないので値段が無い
            if (skill.Passive) return 0;
            // ⭐ 盤面をひっくり返す級は式の外（1回きりが持ち味・既存の規則のまま）
            if (IsHeavyCt(skill)) return CtHeavy;

            int longest;
            int price = LoadOf(skill, out longest);

            // ⚠️ **張りっぱなしを止める。**免疫3T を CT2 で回すと永久免疫になる。
            //    ⭐ 味方に配る持続ものだけ「持続＋1」を下限にする。
            //    ⚠️ 近似 ── 持続は**受け手**の行動で減り、CT は**掛け手**の行動で減る。
            //    🔴 2026-08-27 追記: 「持続もの**だけ**」の技（他に伸ばせる軸が無い）は、
            //    床を「持続＋1＋段数」まで上げる ── 段数ぶんは CT の成長として使い切り、
            //    最大まで育てるとちょうど元の床（持続＋1）に着地する。
            //    ⚠️ 唯一の出所は AllyPersistentSteps（GrowthOf の段数と同じ数字を返す）。
            if (longest > 0)
            {
                int floor = IsPureAllyPersistent(skill)
                    ? longest + 1 + AllyPersistentSteps(longest)
                    : longest + 1;
                if (price < floor) price = floor;
            }

            if (price < CtFloor) price = CtFloor;
            int cap = CapFor(skill, longest);
            if (price > cap) price = cap;
            return price;
        }

        /// <summary>⭐ その技に許される CT の天井。**唯一の出所**（<see cref="PriceOf"/> 自身と、
        /// `Faults` の CT上限チェックの両方がここを呼ぶ ── 別々に
        /// <c>IsHeavyCt(skill) ? CtHeavy : CtCap</c> と書いていた頃は、`Faults` 側だけ
        /// 更新を忘れて `guts-deep`/`immune-long` を「盤面をひっくり返す技でもないのに
        /// 天井超え」と誤って落としていた。2026-08-27 発見）。
        ///
        /// ⚠️ 盤面をひっくり返す級（<see cref="IsHeavyCt"/>）は既存の規則のまま
        /// <see cref="CtHeavy"/>。
        /// ⭐ 🔴 2026-08-27 追記: 味方に配る持続ものの下限（`longest+1+段数`）が通常の天井
        /// （<see cref="CtCap"/>）を超える技（当時の `guts-deep`/`immune-long`＝持続6T→下限7。
        /// 同日中に持続4T＋回復・弱化解除の複合へ作り直したが、床は 4+1+2＝7 のまま同じなので
        /// この例は今も成り立つ ── 判定は id ではなく `longest` で見ているので、今後 id が
        /// 増減しても崩れない）も、ここで <see cref="CtHeavy"/> まで許す ── 許さないと天井で
        /// 刈られて「CT ≧ 持続＋1」が Lv1 から破れる（免疫がずっと乗ったままになりうる）。
        /// ⚠️ **`longest` が原因のときだけ**。`LoadOf` の生の重さ（`price`）が
        /// 単に大きいだけの技（毒牙の乱打＝生10・崩落＝生7）まで緩めると、
        /// 「盛れば CT7 まで伸びる」抜け道になる ── そちらは今までどおり
        /// <see cref="CtCap"/> で刈る（`LoadOf` の doc が挙げる実例のとおり CT5 のまま）。</summary>
        private static int CapFor(Skill skill, int longest)
        {
            if (IsHeavyCt(skill)) return CtHeavy;
            // 🔴 2026-08-27 追記: PriceOf の床と同じ式（IsPureAllyPersistent 用に伸ばした
            //    「持続＋1＋段数」）で天井を見る ── 床だけ伸ばして天井を昔のままにすると、
            //    伸ばした床そのものが天井に刈られてしまう（regen/regen-heavy が実際にこれで壊れた）。
            int floor = IsPureAllyPersistent(skill)
                ? longest + 1 + AllyPersistentSteps(longest)
                : longest + 1;
            return floor > CtCap ? CtHeavy : CtCap;
        }

        /// <summary>⭐ **その技が1回で起こすことの量**（床・天井・例外を掛ける**前**の生の値段）。
        ///
        /// ⭐ これがある理由（2026-08-27）: <see cref="PriceOf"/> は最後に
        /// <see cref="CtCap"/> で刈るので、**上に振り切れた技がそこで見えなくなる**。
        /// 実際に「毒牙の乱打」は 10、「崩落」は 7 を出しているのに、画面ではどちらも
        /// CT5 ── ただの「乱打」（6）や「毒・全体」（5）と同じ値段に見えていた。
        /// ⚠️ **技の格が天井に吸われて消えていた**のがこれ。
        ///
        /// ⭐ 刈る前の数は捨てずに取り出せるようにして、**格の物差し**に使う
        /// （<see cref="GradeOf"/>）。⚠️ CT の式そのものは何も変えていない。</summary>
        public static int LoadOf(Skill skill) => LoadOf(skill, out _);

        private static int LoadOf(Skill skill, out int longest)
        {
            int load = 1;
            foreach (var effect in skill.Effects)
            {
                // ⚠️ **自分への弱化は代償。**重さに数えず、逆に値引く（捨て身の突きの型）
                bool cost = IsSelfCost(effect);
                if (cost) { load -= 1; continue; }

                load += WeightOf(effect);
                // ⚠️ 条件つきは「作りに行く手間」を値段から引く
                if (effect.When != null) load -= 1;
            }
            // ⚠️ **判断は `LongestAllyTurns`（＝`IsAllyPersistent`）1つに寄せる**
            //    （`GrowthOf`/`Faults`/`PriceOf` の床がすべて同じ出所を見る）。
            longest = LongestAllyTurns(skill);

            // ⭐ 面で当たるものは高い。⚠️ 敵全体は挑発の縛りも受けない
            var at = skill.Target;
            if (at == Target.EnemyAll) load += 2;
            else if (at == Target.AllyAll) load += 1;
            // ⚠️ 狙えないことは値段。⭐ 単体と同じ効き目を安く買える取引（既存の設計意図）
            else if (at == Target.EnemyRandom) load -= 1;

            return load;
        }

        /// <summary>味方に配る持続効果のうち、いちばん長い持続。⚠️ 無ければ 0。
        /// ⭐ **唯一の出所**（<see cref="LoadOf(Skill, out int)"/> の床・<see cref="GrowthOf(Skill, int)"/>
        /// の段数・<see cref="Faults()"/> の検査がすべてここを見る。2026-08-27）。</summary>
        private static int LongestAllyTurns(Skill skill)
        {
            int longest = 0;
            foreach (var effect in skill.Effects)
            {
                if (effect.Turns > longest && IsAllyPersistent(skill, effect)) longest = effect.Turns;
            }
            return longest;
        }

        /// <summary>味方に配る持続効果を1つでも持つか。⭐ **唯一の出所**
        /// （<see cref="GrowthOf(Skill, int)"/> と <see cref="Faults()"/> が別々に同じ
        /// foreach を書いていたのを1つに寄せた。2026-08-27）。</summary>
        private static bool GuardsAllies(Skill skill)
        {
            foreach (var effect in skill.Effects) if (IsAllyPersistent(skill, effect)) return true;
            return false;
        }

        /// <summary>その技が実際に持っている成長軸（枠は問わない・guardsAllies を反映済み）。
        /// ⭐ **唯一の出所**（<see cref="GrowthOf(Skill, int)"/> 本体と、
        /// <see cref="IsSingleAxisFromGuard"/> 経由で Audit の両方がここを読む）。
        /// ⚠️ 同じ「軸を集める」計算を2か所に書くと、いつかどちらかだけ古くなる
        /// （②の xUnit 並列と同じ形の罠 ── 直した本人の目の前でもう1つ増やしていた、では
        /// 洒落にならないので先に1本化した）。</summary>
        private static List<SkillGain> NaturalAxesOf(Skill skill, bool guardsAllies)
        {
            var axes = new List<SkillGain>();
            if (HasDamage(skill)) axes.Add(SkillGain.Power);
            if (HasRepeat(skill)) axes.Add(SkillGain.Repeat);
            if (HasChance(skill)) axes.Add(SkillGain.Chance);
            if (!guardsAllies && HasTurns(skill)) axes.Add(SkillGain.Turns);
            // ⚠️ 回復の割合と盾の枚数は Turns でも Power でも表せない。
            //    これが無いと、それらの技は伸びる軸が CT しか無くなり、
            //    4段とも CT ＝ 途中で下限 0 に当たって**死に段**になる（導出して初めて見えた）
            if (HasPercent(skill)) axes.Add(SkillGain.Percent);
            if (HasCount(skill)) axes.Add(SkillGain.Count);
            if (HasAmount(skill)) axes.Add(SkillGain.Amount);
            // ⚠️ パッシブは CT が無いので、これが無いと**伸ばす軸が1つも無くなる**
            if (HasInnate(skill)) axes.Add(SkillGain.Innate);
            if (!guardsAllies && skill.Ct > 0) axes.Add(SkillGain.Ct);
            return axes;
        }

        /// <summary>guardsAllies が Turns・CT を外した結果、軸が1本しか残らなかった技か。
        /// ⭐ <see cref="GrowthOf(Skill, int)"/> の段数を <see cref="SingleAxisGrowthSteps"/> に
        /// 縮める対象と、Audit（<see cref="Faults"/>）が「成長表は4段のはず」の例外にする対象は
        /// **必ず同じ技の集合**でなければならない ── ここが唯一の判定。
        /// ⚠️ パッシブ3件（生命力・頑丈・身軽）は軸が Innate の1本だけだが guardsAllies が
        /// 掛かっていない（CT を最初から持たないので削られてもいない）ので対象外のまま
        /// （4段で育つのが元の姿）。</summary>
        private static bool IsSingleAxisFromGuard(Skill skill)
        {
            bool guardsAllies = GuardsAllies(skill);
            return guardsAllies && NaturalAxesOf(skill, guardsAllies).Count == 1;
        }

        /// <summary>味方に配る持続効果**だけ**を持ち、他に伸ばせる軸が無い技か。
        ///
        /// ⭐ この形の技（攻撃力UP・防御力UP・スピードUP・ガッツ・免疫・リジェネ・リジェネ大・
        /// 踏ん張り・厄払いの9本）だけが、<see cref="AllyPersistentSteps"/> の
        /// 専用式で育つ（作者の指示 2026-08-27）。
        /// ⚠️ 他に軸がある技（tailwind・dash・harden・warcry 等）は対象外 ──
        /// そちらは今までどおり Turns だけ外した軸（Percent・Amount・Count・Chance）で育つ。
        ///
        /// ⚠️ 🔴 **例外は1つだけ許す**（2026-08-27 追記）: <see cref="IsFixedAllyAssist"/>
        /// に当てはまる「その場で効くだけの、固定の手当て」（HP割合回復の正／弱化解除）。
        /// ⭐ 踏ん張り（ガッツ＋HP割合回復）・厄払い（弱化解除＋免疫）がこれ ── 添えた手当ては
        /// レベルを問わず一定でよく、伸びるのは CT だけという作者の指示（基礎CT=持続+1+段数）に
        /// 合わせるための例外。⚠️ **ゲージ・盾・強奪までは許さない**（`IsFixedAllyAssist` が
        /// HealRatio の正と Dispel の負の2種類だけに絞っているのはそのため）── ここを広げると
        /// tailwind（Gauge）・harden（Shield）まで「pure」に化けて、いま Percent/Count 軸で
        /// 育っている挙動が変わってしまう。</summary>
        private static bool IsPureAllyPersistent(Skill skill)
        {
            if (!GuardsAllies(skill)) return false;
            if (HasDamage(skill) || HasRepeat(skill) || HasChance(skill) || HasAmount(skill)
                || HasInnate(skill)) return false;
            foreach (var effect in skill.Effects)
            {
                if (IsAllyPersistent(skill, effect)) continue;
                if (IsFixedAllyAssist(effect)) continue;
                return false;
            }
            return true;
        }

        /// <summary>⭐ **持続ものに添える、その場限りの固定の手当てか。**
        /// ⚠️ **唯一の出所**（<see cref="IsPureAllyPersistent"/> だけが呼ぶ）。
        ///
        /// ⚠️ HP割合回復の**正**（味方を戻す）と弱化解除（<see cref="EffectKind.Dispel"/> の負）
        /// の2種類**だけ**を許す。⭐ どちらも「レベルで伸ばす意味が薄い、添え物」という共通点がある
        /// （回復は致命傷の穴埋め・解除は免疫を張る前の掃除で、量そのものが主役ではない）。
        /// ⚠️ ゲージ・盾・強奪はここに入れない ── 「伸ばすほど嬉しい」軸なので、
        /// tailwind・harden のように Percent/Count 軸で育てたほうが技として筋が通る。</summary>
        private static bool IsFixedAllyAssist(Effect effect) =>
            effect.Turns == 0 &&
            ((effect.Kind == EffectKind.HealRatio && effect.Percent > 0)
                || (effect.Kind == EffectKind.Dispel && effect.Count < 0));

        /// <summary>味方に配る持続もの「だけ」の技が CT を伸ばす段数。既定
        /// <see cref="AllyPersistentGrowthSteps"/>。
        ///
        /// ⭐ 基礎CT（Lv1）＝ <paramref name="longest"/>＋1＋段数、最大まで育てると
        /// ちょうど下限（<paramref name="longest"/>＋1）に着地する（作者の指示 2026-08-27）。
        /// ⚠️ 基礎CTが <see cref="CtHeavy"/> を超えてしまう技は、超えなくなるまで
        /// 段数を 2→1→0 と減らして収める。**0 になったら「育たない技」**
        /// （他に安全な軸が無いので、安全装置を壊すより上げられないほうを選ぶ）。
        /// ⭐ 唯一の出所 ── <see cref="PriceOf"/> の床・<see cref="CapFor"/> の天井・
        /// <see cref="GrowthOf(Skill, int)"/> の成長本数がすべてここを呼ぶ。</summary>
        private static int AllyPersistentSteps(int longest)
        {
            int steps = AllyPersistentGrowthSteps;
            while (steps > 0 && longest + 1 + steps > CtHeavy) steps--;
            return steps;
        }

        /// <summary>味方に配る持続ものが CT を伸ばす段数の既定値。⭐ 定数にして名前を付ける
        /// （作者の指示 2026-08-27・「段数は既定2」）。</summary>
        public const int AllyPersistentGrowthSteps = 2;

        /// <summary>効果1つぶんの重さ。⭐ **ゲーム全体でこの1枚**（技ごとに数を書かない）。</summary>
        private static int WeightOf(Effect effect)
        {
            switch (effect.Kind)
            {
                case EffectKind.Damage:
                {
                    // ⭐ 段位の番号（小1・中2・大3・特大4）＋ 多段の1発は段位1つぶん×2
                    int weight = (int)effect.Power + 1 + (effect.Repeat - 1) * 2;
                    // ⭐ 相手の守りを踏み倒すぶん
                    if (effect.Pierce) weight += 1;
                    // ⚠️ **強化無視は防御無視より高い。**踏み倒すのが4つ（防御力UP・
                    //    シールド・無敵・ガッツ）あり、どれも相手が1手payして買った札だから。
                    if (effect.Bare) weight += 2;
                    // ⚠️ **数えるぶんにも値段が要る。**付け忘れていた頃、
                    //    「小の一撃 ＋ 数え」が CT2（一番安い帯）に落ちていた ──
                    //    満載なら大の一撃を超えるのに、弱化の単品と同じ値段だった。
                    // ⭐ 天井まで積めば威力は約2.2倍（段位2つぶん）。半分を値段に取る
                    //    ── 残り半分は「仕込みを先に作る手間」で既に払っているため。
                    if (effect.Per != Tally.None) weight += TallyWeight;
                    return weight;
                }
                // ⭐ **手番を奪うものが一番高い**（1手番 ≒ 技1つぶん）
                case EffectKind.Stun:
                case EffectKind.Sleep: return effect.Turns * 3;
                case EffectKind.Poison:
                case EffectKind.Regen: return effect.Stacks * 2;
                // ⚠️ 割合もの。⭐ 負（削り）は相手の防御も属性も見ないので、絶対値で数える
                case EffectKind.HealRatio:
                case EffectKind.Gauge:
                case EffectKind.Revive:
                {
                    int amount = effect.Percent < 0 ? -effect.Percent : effect.Percent;
                    int weight = (amount + PercentPerWeight - 1) / PercentPerWeight;
                    // ⭐ 最大HP の割合削りは防御を見ない ── 防御無視と同じ値引きの逆
                    if (effect.Kind == EffectKind.HealRatio && effect.Percent < 0) weight += 1;
                    return weight;
                }
                case EffectKind.Shield: return effect.Count;
                case EffectKind.Ct: return effect.Delta < 0 ? -effect.Delta : effect.Delta;
                case EffectKind.Taunt: return (effect.Hits + 1) / 2;
                case EffectKind.Guts:
                case EffectKind.Immune: return (effect.Turns + 2) / 3;
                case EffectKind.Block: return (effect.Turns + 1) / 2;
                // ── 2026-08-27 に足した5つ ─────────────────────────
                // ⭐ **封印は手番を奪わない。**⚠️ できることが1つに減るだけなので、
                //    スタン（1ターンにつき3）より軽く、ブロックと同じ帯に置く
                case EffectKind.Seal: return effect.Turns;
                // ⭐ 固着は「相手の解除を無駄にする」── 免疫の裏返しなので同じ数え方
                case EffectKind.Anchor: return (effect.Turns + 2) / 3;
                // ⚠️ **無敵は一番高い。**受ける一撃をまるごと消すので、1ターンが盾1枚より重い
                case EffectKind.Invincible: return effect.Turns * 2;
                // ⭐ 延長は「既に乗っている弱化を伸ばす」── 単体では何も起きない仕込みの回収札
                case EffectKind.Extend: return effect.Turns;
                // ⭐ 反撃は返すぶんだけ。⚠️ 殴られないと働かないので、無敵の半分に置く
                case EffectKind.Counter: return effect.Turns;
                case EffectKind.Dispel:
                case EffectKind.Steal:
                    return effect.Count < 0 ? -effect.Count : effect.Count;
                case EffectKind.Buff: return 1;
                // ⚠️ 黙って 0 にしない。⭐ 効果を足したのにここへ来ないと、その技が**只**になる
                default: throw new ArgumentOutOfRangeException(nameof(effect), effect.Kind,
                    "値段の付いていない効果。Skills.WeightOf に足すこと");
            }
        }

        /// <summary>割合もの何%ぶんで重さ1つか。</summary>
        public const int PercentPerWeight = 25;

        /// <summary>盤面を数える効果の重さ。⭐ 天井まで積んだときの伸びの半分。</summary>
        public const int TallyWeight = 1;

        /// <summary>値段の床。⚠️ CT1 は「1回おきに押せる」で枠1（CT0）と区別が付かない。</summary>
        public const int CtFloor = 2;

        /// <summary>⭐ 式の答えが気に入らない技だけ、ここに書く。
        /// ⚠️ <see cref="GrowthOverrides"/> と同じ流儀 ── **既定は導出・例外だけ手書き**。
        /// ⚠️ ここに書いたら理由をコメントで残すこと（書かないと次に触る人が式を疑う）。</summary>
        private static readonly Dictionary<string, int> CtOverrides = new Dictionary<string, int>();

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
                // 🔴 **敵全体の手番をまとめて奪うのも、盤面をひっくり返す**（2026-08-26）。
                //    ⚠️ 実測で `stagnate`（全体ゲージ-60%＋スタン）が **98.7%** を出した
                //    ── CT5 では2体が交互に撃つだけで相手が一度も動けなくなる。
                //    ⭐ 蘇生・全体全快と同じ「1回きりだから許される」級として CT7 に置く。
                if (e.Kind == EffectKind.Gauge && skill.Target == Target.EnemyAll
                    && e.Percent <= -HeavyGaugePercent) return true;
                if (e.Kind != EffectKind.Damage || skill.Target != Target.EnemyAll) continue;
                // ⭐ 全体に「深く」当たるもの ── 一撃が大きいか、多段で盾を広く剥がすか
                if (e.Power >= PowerTier.Large || e.Repeat > 1) return true;
            }
            return false;
        }

        private static readonly Skill[] List =
        {
            // ── 攻撃 ──────────────────────────────
            new Skill("attack", "攻撃", "敵1体にダメージ", SkillType.Attack, Target.EnemyOne,
                Effect.Damage(PowerTier.Medium, DamageScale.Atk)),
            new Skill("attack-heavy", "強攻撃", "敵1体に大きなダメージ。次が遠い", SkillType.Attack, Target.EnemyOne,
                Effect.Damage(PowerTier.Large, DamageScale.Atk)),
            // ⚠️ 全体なので1段下げて「小」
            new Skill("attack-all", "全体攻撃", "敵全体にダメージ", SkillType.Attack, Target.EnemyAll,
                Effect.Damage(PowerTier.Small, DamageScale.Atk)),
            new Skill("attack-all-heavy", "全体強攻撃", "敵全体に大きなダメージ。次がとても遠い", SkillType.Attack, Target.EnemyAll,
                Effect.Damage(PowerTier.Large, DamageScale.Atk)),
            new Skill("attack-def", "防御依存攻撃", "防御力が高いほど強い一撃", SkillType.Attack, Target.EnemyOne,
                Effect.Damage(PowerTier.Medium, DamageScale.Def)),

            // ── ステータス系 ──────────────────────
            // ⭐ **強化は配る札。**⚠️ 全部 Self にしていた頃は「誰に」の選択が1つも無かった。
            new Skill("atk-up", "攻撃力UP", "味方1体の攻撃力を上げる", SkillType.Support, Target.AllyOne,
                Effect.Buff(StatKey.Atk, 1, 3)),
            new Skill("atk-down", "攻撃力DOWN", "敵1体の攻撃力を下げる", SkillType.Debuff, Target.EnemyOne,
                Effect.Buff(StatKey.Atk, -1, 3, chance: 90)),
            new Skill("def-up", "防御力UP", "味方1体の防御力を上げる", SkillType.Support, Target.AllyOne,
                Effect.Buff(StatKey.Def, 1, 3)),
            new Skill("def-down", "防御力DOWN", "敵1体の防御力を下げる", SkillType.Debuff, Target.EnemyOne,
                Effect.Buff(StatKey.Def, -1, 3, chance: 85)),
            new Skill("spd-up", "スピードUP", "味方1体のスピードを上げる", SkillType.Support, Target.AllyOne,
                Effect.Buff(StatKey.Spd, 1, 3)),
            new Skill("spd-down", "スピードDOWN", "敵1体のスピードを下げる", SkillType.Debuff, Target.EnemyOne,
                Effect.Buff(StatKey.Spd, -1, 3, chance: 85)),

            // ── HP系 ──────────────────────────────
            new Skill("poison", "毒", "敵1体が行動するたびに削れる", SkillType.Debuff, Target.EnemyOne,
                Effect.Poison(1, 4, chance: 75)),
            new Skill("regen", "リジェネ", "味方1体が行動するたびに回復する", SkillType.Heal, Target.AllyOne,
                Effect.Regen(1, 4)),
            new Skill("heal-ratio", "HP割合回復", "味方1体の HP を最大値の割合ぶん回復", SkillType.Heal, Target.AllyOne,
                Effect.HealRatio(30)),
            new Skill("shield", "シールド", "味方1体に、HP より先に減る盾を張る", SkillType.Support, Target.AllyOne,
                Effect.Shield(2)),

            // ── 行動系 ────────────────────────────
            new Skill("stun", "スタン", "敵1体の手番を飛ばす", SkillType.Debuff, Target.EnemyOne,
                Effect.Stun(1, chance: 60)),
            new Skill("ct-short", "CT短縮", "自分の技の待ちを縮める", SkillType.Support, Target.Self,
                Effect.Ct(-2)),
            new Skill("ct-long", "CT延長", "敵1体の技の待ちを延ばす", SkillType.Debuff, Target.EnemyOne,
                Effect.Ct(2, chance: 80)),
            // ⚠️ **2026-08-18 まで Target.Self だったため、一度も発動していなかった。**
            //    効果は「相手に付ける弱化」に作り替えたのに技の狙い先を直し忘れていた。
            //    自分に掛かると TauntBy が自分になり、敵側から探す縛りが一致しない。
            new Skill("taunt", "挑発", "敵1体が、自分しか狙えなくなる", SkillType.Debuff, Target.EnemyOne,
                Effect.Taunt(3)),

            // ── 特殊 ──────────────────────────────
            new Skill("guts", "ガッツ", "味方1体が致命傷を HP1 で耐える", SkillType.Support, Target.AllyOne,
                Effect.Guts(3)),
            // ⭐ **先手で配る札。**⚠️ 弱化を通されてから貼っても手遅れ
            new Skill("immune", "免疫", "味方1体が弱化を受けなくなる", SkillType.Support, Target.AllyOne,
                Effect.Immune(3)),

            // ── ここから増やしたぶん（2026-08-17）────────────────
            // ⭐ 新しい効果の種類を1つも足していない。既にある11種の**組み合わせ**と、
            //    多段（Repeat）の掛け算だけで書いてある。
            // ⚠️ 足すたびに `sim skills` で「一度も選ばれない技」が出ていないか見る。

            // 多段。⭐ 盾は1発ごとに剥がれるので、大きな一撃と役割が分かれる
            new Skill("attack-twice", "連撃", "敵1体に小さな一撃を2回", SkillType.Attack, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk, 2)),
            new Skill("attack-thrice", "乱打", "敵1体に小さな一撃を3回。盾を剥がす", SkillType.Attack, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk, 3)),
            new Skill("attack-def-twice", "堅陣突き", "防御が高いほど強い一撃を2回", SkillType.Attack, Target.EnemyOne,
                Effect.Damage(PowerTier.Medium, DamageScale.Def, 2)),

            // 複合。⭐ 1手で2つのことをする代わりに CT が長い
            // ⭐ ここから下の弱化は**外れることがある**（命中と抵抗の差で上下する）。
            // ⚠️ 上の移植した21技は 100% のまま。較正済みの照合が1手も変わらないように残してある。
            // ⚠️ ダメージの側は必ず当たる。外れるのは弱化だけ
            new Skill("venom-fang", "毒牙", "ダメージを与え、高い確率で毒も入れる", SkillType.Attack, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk),
                Effect.Poison(1, 4, chance: 75)),
            new Skill("crush", "打ち崩し", "ダメージを与え、高い確率で防御力を下げる", SkillType.Attack, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk),
                Effect.Buff(StatKey.Def, -1, 3, chance: 75)),
            new Skill("dash", "早駆け", "自分のスピードを上げ、技の待ちも縮める", SkillType.Support, Target.Self,
                Effect.Buff(StatKey.Spd, 1, 3),
                Effect.Ct(-2)),
            new Skill("harden", "硬化", "防御力を上げ、盾も張る", SkillType.Support, Target.Self,
                Effect.Buff(StatKey.Def, 1, 3),
                Effect.Shield(1)),
            // ⚠️ 挑発が弱化になったので、相方も敵に掛かるものへ替えた
            //    （Self のままだと防御UP が敵に乗る）。
            new Skill("bulwark", "受けの構え", "敵1体を釘付けにし、その攻撃を鈍らせる", SkillType.Debuff, Target.EnemyOne,
                Effect.Taunt(2),
                Effect.Buff(StatKey.Atk, -1, 3, chance: 75)),
            // ⭐ 2つ掛けるので1つずつの通りは低い。速い個体が使うと両方通りやすい
            new Skill("curse", "呪詛", "敵1体の攻撃力とスピードを下げる", SkillType.Debuff, Target.EnemyOne,
                Effect.Buff(StatKey.Atk, -1, 3, chance: 70),
                Effect.Buff(StatKey.Spd, -1, 3, chance: 70)),

            // 濃さを変えただけのもの。⭐ 段位ではなくスタック数・割合で差を出す
            new Skill("venom-heavy", "毒・大", "毒を2重に入れる。やや外れやすい", SkillType.Debuff, Target.EnemyOne,
                Effect.Poison(2, 4, chance: 65)),
            new Skill("heal-big", "HP割合回復・大", "味方1体の HP を大きく回復", SkillType.Heal, Target.AllyOne,
                Effect.HealRatio(55)),

            // ⚠️ 全体は1段下げる。全体の弱化は単体よりずっと効く
            // ⚠️ 全体なので通りは低め。全員に確実に入ると1手で試合が決まる
            new Skill("slow-all", "スピードDOWN・全体", "敵全体のスピードを下げる", SkillType.Debuff, Target.EnemyAll,
                Effect.Buff(StatKey.Spd, -1, 3, chance: 60)),

            // ── 効き目と確率のトレードオフ（2026-08-17）─────────────
            // ⭐ **同じ効果の種類のまま、別の技として並べる軸。**
            //    「小さいが必ず通る」の隣に「大きいが半分外す」を置くと、
            //    どちらを枠に入れるかが**編成ごとに変わる判断**になる。
            // ⚠️ 上に並んでいる移植ぶんが「確実side」の役を兼ねているので、
            //    ここは主に博打sideを足している。
            // ⚠️ **軸が効くのは相手に掛ける札だけ**（2026-08-21・作者の指示）。
            //    味方に掛ける「・大」は確率を外したので、⭐ いまは同じ種類の**濃い側**
            //    ── 大きく効くぶん CT が長い、という値段の付き方に一本化されている。

            new Skill("heal-miracle", "HP割合回復・特大", "味方1体を全快させる", SkillType.Heal, Target.AllyOne,
                Effect.HealRatio(100)),
            new Skill("shield-wall", "シールド・大", "味方1体に盾を4枚張る", SkillType.Support, Target.AllyOne,
                Effect.Shield(4)),
            // ⚠️ 🔴 **2026-08-27 作り直し（作者の指示）。**元は上の2本と同じ「・長＝濃い側」
            //    （持続だけを伸ばした版）だったが、持続6T→下限7（=持続+1）が
            //    ちょうど天井（CtHeavy）に乗ってしまい、縮める余地が無く**Lv1のまま育たない技**
            //    になっていた。実測（`sim guess`・各300戦）でも、持続を3T→6Tへ倍にして
            //    実際に働いた回数は 0.09回/掛 → 0.08回/掛 と**増えていない**
            //    ── 縛っているのは持続ではなく「4体のうちその1体が狙われるか」だった。
            // ⭐ 持続を4Tへ落として `Skills.AllyPersistentSteps` の型（基礎CT＝持続+1+段数＝7、
            //    Lv3で下限CT5）にちゃんと乗せ、代わりに回復・弱化解除を足して
            //    「やることが増える」形に作り直した。
            // ⚠️ **id は変えていない。**`guts-deep`/`immune-long` という字面はもう中身と合わないが、
            //    種族の技袋（<see cref="SpeciesTable"/>）・試練の敵編成（Trial.cs）・
            //    実測ツール（GuessProbe.cs 等）が id を直に指しており、変えるとそちら全部の
            //    書き換えが要る。⭐ 画面に出る名前の出所はここ1か所だけなので、
            //    id はそのまま残し、字面と中身がずれた経緯をこのコメントに残す。
            new Skill("guts-deep", "踏ん張り", "味方1体を致命傷から守り、HPも戻す",
                SkillType.Support, Target.AllyOne,
                // ⭐ ガッツは致命傷を HP1 で耐える札。**耐えた直後の1が回復で戻る**ので、
                //    2つ揃って初めて「生き残る」になる。⚠️ 割合は heal-ratio と同じ30%
                //    （既存の標準に合わせた ── CT は longest+1+段 の床が支配するので、
                //    割合をいくつにしても CT は動かない）。
                Effect.Guts(4),
                Effect.HealRatio(30)),
            new Skill("immune-long", "厄払い", "味方1体の弱化を落とし、免疫も張る",
                SkillType.Support, Target.AllyOne,
                // ⭐ 免疫は「これから来る弱化」を弾く札で、**既に付いている弱化は落とせない**。
                //    落としてから張れば穴が無い ── だから Cleanse を先に置く。
                //    ⚠️ 個数は既存の cleanse（弱化解除）と同じ2個（釣り合いを合わせた）。
                Effect.Cleanse(2),
                Effect.Immune(4)),

            // ⚠️ 相手に掛ける側は（命中 − 抵抗）÷2 ポイント動く。命中に振った個体が使うと通りやすい
            new Skill("stun-heavy", "スタン・大", "2回ぶん手番を飛ばす。よく外す", SkillType.Debuff, Target.EnemyOne,
                Effect.Stun(2, chance: 40)),
            new Skill("ct-lock", "CT延長・大", "敵の技の待ちを大きく延ばす", SkillType.Debuff, Target.EnemyOne,
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
            new Skill("gauge-drain", "ゲージ減少", "敵1体のゲージを大きく戻す", SkillType.Debuff, Target.EnemyOne,
                Effect.Gauge(-40, chance: 65)),
            new Skill("gauge-boost", "ゲージ上昇", "味方1体のゲージを進める", SkillType.Support, Target.AllyOne,
                Effect.Gauge(30)),

            // ⭐ 打ち消しの層。⚠️ 免疫は**強化**なので、これで剥がせる
            //    ＝「剥がしてから弱化を通す」という2手の組み立てが生まれる
            new Skill("dispel", "強化解除", "敵1体の強化を2つ消す", SkillType.Debuff, Target.EnemyOne,
                Effect.Dispel(2, chance: 70)),
            new Skill("buff-steal", "強化強奪", "敵1体の強化を1つ、自分へ移す", SkillType.Debuff, Target.EnemyOne,
                Effect.Steal(1, chance: 55)),

            // ⭐ 外から受け取る回復と強化だけを止める。⚠️ 自然に溜まるゲージと CT は止まらない
            // ⚠️ 名前を「封印」にしていたが、CT を延ばす「封じ」と紛らわしかった。
            //    ⭐ 効果そのものの名（毒・スタン・免疫・シールドと同じ付け方）に揃える。
            new Skill("block", "ブロック", "敵1体が回復と強化を受け取れなくなる", SkillType.Debuff, Target.EnemyOne,
                Effect.Block(2, chance: 60)),

            // ⭐ スタンと別物。⚠️ **殴ると起きる**ので、殴る順番が問われる
            new Skill("sleep", "睡眠", "敵1体を眠らせる。攻撃を受けると起きる", SkillType.Debuff, Target.EnemyOne,
                Effect.Sleep(2, chance: 55)),

            // ⭐ 硬い相手を抜く。⚠️ 盾は抜けない（そこは剥がすか多段で削る）
            new Skill("pierce-strike", "防御無視攻撃", "防御を計算に入れずに斬る", SkillType.Attack, Target.EnemyOne,
                Effect.Damage(PowerTier.Medium, DamageScale.Atk, pierce: true)),

            // ⭐ 倒れてからの手。⚠️ 強化も弱化も持ち越さない（ゲージと CT は続き）
            new Skill("revive", "蘇生", "倒れた味方1体を呼び戻す", SkillType.Heal, Target.AllyDown,
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
            new Skill("poison-all", "毒・全体", "敵全体に毒を入れる。半分は外れる", SkillType.Debuff, Target.EnemyAll,
                Effect.Poison(1, 3, chance: 50)),
            // ⭐ 殴りながら置く。⚠️ ダメージは必ず当たり、外れるのはスタンだけ
            new Skill("stun-strike", "痺れ打ち", "ダメージを与え、たまにスタンも入れる", SkillType.Debuff, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk),
                Effect.Stun(1, chance: 45)),

            // ⭐ 剥がしてから通す層の複合版。強化解除（2個・CT5）より軽い代わりに殴れる
            new Skill("strip-strike", "剥ぎ打ち", "ダメージを与え、高い確率で強化も1つ消す", SkillType.Attack, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk),
                Effect.Dispel(1, chance: 70)),

            // ⭐ 毒・大（2重）の回復側の鏡。⚠️ こちらは味方に掛けるので外れない
            new Skill("regen-heavy", "リジェネ・大", "リジェネを2重に掛ける", SkillType.Heal, Target.AllyOne,
                Effect.Regen(2, 4)),
            // ⭐ ゲージ上昇（30%）の濃い側。⚠️ 味方に掛けるので外れない
            new Skill("gauge-boost-heavy", "ゲージ上昇・大", "味方1体のゲージを大きく進める", SkillType.Support, Target.AllyOne,
                Effect.Gauge(60)),
            // ⭐ 挑発（3回・確実・CT3）の長持ち側。置き土産・背水の「わざと受ける」相方
            new Skill("taunt-long", "挑発・長", "敵1体を長く釘付けにする。3割は外す", SkillType.Debuff, Target.EnemyOne,
                Effect.Taunt(5, chance: 70)),

            // ⭐ 硬い相手への締めの一撃。⚠️ 防御無視攻撃（中・CT5）より1段上で、そのぶん遠い
            new Skill("pierce-strike-heavy", "防御無視強攻撃", "防御を計算に入れない大きな一撃。次が遠い", SkillType.Attack, Target.EnemyOne,
                Effect.Damage(PowerTier.Large, DamageScale.Atk, pierce: true)),
            // ⭐ 全体 × 多段。盾を**面で**剥がす唯一の札（執念持ちの敵には貢ぎ物になる読み合い）
            new Skill("attack-all-twice", "全体連撃", "敵全体に小さな一撃を2回。盾を広く剥がす", SkillType.Attack, Target.EnemyAll,
                Effect.Damage(PowerTier.Small, DamageScale.Atk, 2)),

            // ⭐ 蘇生（40%）の手厚い側。⚠️ そのぶん次が遠い
            new Skill("revive-heavy", "蘇生・大", "倒れた味方を手厚く呼び戻す", SkillType.Heal, Target.AllyDown,
                Effect.Revive(70)),
            // ⭐ 複合（スピードUP＋ゲージ上昇）。⚠️ 複合は名前で両方言えないので短い名
            new Skill("tailwind", "追い風", "味方1体のスピードを上げ、ゲージも進める", SkillType.Support, Target.AllyOne,
                Effect.Buff(StatKey.Spd, 1, 3),
                Effect.Gauge(25)),

            // ── 返す手・1手2役・構えの層（2026-08-20・🚧 まだ配っていない）──────
            // ⭐ 参考作品の R帯60体を突き合わせて、**本作の語彙で書けなかった3つ**を足した層。
            //    ⚠️ 足りなかったのは効果の種類ではなく、次の3つの**形**だった:
            //      ・弱化を**落とす**（Dispel の負の側。プリミティブは在ったのに技が1本も無かった）
            //      ・**1手2役**（技が狙い先を1つしか持てず「殴りながら支える」が書けなかった）
            //      ・**切れない持続**（あちらはパッシブで持つ「常に防御が高い」型）
            // ⭐ 効果の種類は1つも増やしていない。増えたのは Effect の欄が2つだけ（飛び先・永続）。

            // ⭐ **弱化に返す手。**⚠️ これが無いと、弱化は掛けた側の一方通行だった
            //    ── 速度DOWN を通されたら、こちらに打つ手が1つも無い（⭐ 読み合いが片道）。
            // ⚠️ 落ちる順は「重いものから」（スタン → 睡眠 → 毒 …）。
            //    軽いものから落ちると「治したのに手応えが無い」になる。
            new Skill("cleanse", "弱化解除", "味方1体に乗った弱化を2つ落とす", SkillType.Heal, Target.AllyOne,
                Effect.Cleanse(2)),
            // ⚠️ 全体なので1段下げる: 単体2個・確実 に対し 1個・確実
            new Skill("cleanse-all", "弱化解除・全体", "味方全体の弱化を1つずつ落とす", SkillType.Heal, Target.AllyAll,
                Effect.Cleanse(1)),

            // ⭐ **1手2役。**狙い先の違う効果を1つの技に載せる形。
            //    ⚠️ どれも「得だけ」にしない ── 得だけなら他の技を選ぶ理由が消える。

            // ⭐ 殴った手で自分を立て直す。回復役を1枠空けられる代わりに、一撃は小さい
            new Skill("drain-all", "吸い上げ", "敵全体に小さな一撃。自分のHPが少し戻る", SkillType.Attack, Target.EnemyAll,
                Effect.Damage(PowerTier.Small, DamageScale.Atk),
                Effect.HealRatio(15).To(Target.Self)),
            // ⭐ 大きく踏み込む代わりに、自分の守りが薄くなる
            new Skill("reckless", "捨て身の突き", "大きな一撃。そのあと自分の防御が下がる", SkillType.Attack, Target.EnemyOne,
                Effect.Damage(PowerTier.Large, DamageScale.Atk),
                Effect.Buff(StatKey.Def, -1, 3).To(Target.Self)),
            // ⭐ 相手を下げながら味方を上げる。⚠️ 下げる側だけ外れる（上げる側は自分たちに掛かる）
            new Skill("warcry", "鬨の声", "敵全体の攻撃を下げ、味方全体の攻撃を上げる", SkillType.Debuff, Target.EnemyAll,
                Effect.Buff(StatKey.Atk, -1, 3, chance: 60),
                Effect.Buff(StatKey.Atk, 1, 3).To(Target.AllyAll)),

            // ⭐ **パッシブ（押せない技）。**枠を1つ潰しっぱなしにして、常時の底上げを買う。
            //    ⚠️ 2026-08-20 の初版では「手番1回で買う切れない強化」にしていたが、
            //    作者の指示「まもダンにそろえて」でパッシブに直した。
            //    ⭐ 払い方が手番ではなく**枠**なので、効き目は小さく（InnatePercent）、
            //    代わりに永久で**剥がせない**。
            //    ⭐ HP に乗せられるのはこの形だけ（強化の修正枠が HP を持っていないため）。
            Skill.Always("vigor", "生命力", "常にHPが上がっている", SkillType.Support,
                Effect.Always(StatKey.Hp, 1)),
            Skill.Always("sturdy", "頑丈", "常に防御力が上がっている", SkillType.Support,
                Effect.Always(StatKey.Def, 1)),
            Skill.Always("nimble", "身軽", "常にスピードが上がっている", SkillType.Support,
                Effect.Always(StatKey.Spd, 1)),

            // ── 仕込みを回収する層（2026-08-20・🚧 まだ配っていない）───────────
            // ⭐ **仕込む技は在るのに、回収する側が無かった。**
            //    毒・弱化・CT延長は「ゆっくり効くダメージ」にしかならず、
            //    測定でも R:毒撒き 48% / R:足止め 36% / R:CT縛り 22% と沈んでいた。
            // ⭐ ここは「盤面を数える」「条件を見る」の2つで、その回収先を作る層。
            // ⚠️ 効果の種類は1つも増えていない（数え方と条件は効果の**欄**）。

            // ⭐ 弱化を撒いてから殴る筋の回収先。⚠️ 数えるのは**種類**（毒を重ねても1つ）
            new Skill("chase-down", "追い崩し", "敵1体を攻撃。相手に付いた弱化の種類だけ強くなる",
                SkillType.Attack, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk).Each(Tally.FoeBanes)),
            // ⭐ 面で撒いて面で回収する。⚠️ 数えるのは**相手ごと**（深く入る相手と浅い相手が出る）
            new Skill("sweep-down", "総崩し", "敵全体を攻撃。相手ごとに弱化の種類だけ強くなる",
                SkillType.Attack, Target.EnemyAll,
                Effect.Damage(PowerTier.Small, DamageScale.Atk).Each(Tally.FoeBanes)),

            // ⭐ 積んだ相手への罰。⚠️ 剥がさずに殴るので、強化解除・強奪と棲み分ける
            new Skill("pride-strike", "驕り討ち", "敵1体を攻撃。相手に付いた強化の数だけ強くなる",
                SkillType.Attack, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk).Each(Tally.FoeBoons)),
            // ⭐ 「配ってから撃つ」を筋にする。⚠️ 味方に配る札の回収先
            new Skill("stacked-shot", "積み放ち", "敵1体を攻撃。自分に付いた強化の数だけ強くなる",
                SkillType.Attack, Target.EnemyOne,
                Effect.Damage(PowerTier.Medium, DamageScale.Atk).Each(Tally.OwnBoons)),

            // ⭐ 硬い相手・HPが多い相手への答え。⚠️ 防御も属性も見ないので、確率だけが防ぎ手
            new Skill("life-cut", "命削り", "敵1体の最大HPを削る。防御を見ない",
                SkillType.Attack, Target.EnemyOne,
                Effect.HealRatio(-30, chance: 70)),

            // ⭐ 止め筋の回収先。⚠️ **睡眠は1発目で起きる**ので、スタンと組む技
            //    （ここに小さな読みが1つ生まれる）
            new Skill("ambush-strike", "寝込み討ち", "敵1体を攻撃。相手が動けないなら深く入る",
                SkillType.Attack, Target.EnemyOne,
                Effect.Damage(PowerTier.Medium, DamageScale.Atk),
                Effect.Damage(PowerTier.Medium, DamageScale.Atk).If(SkillWhen.FoeStopped)),
            // ⭐ 回復持ちを閾値ごと割る／多数戦の掃除
            new Skill("finisher", "止めの一撃", "敵1体を攻撃。相手が半分以下なら大きく入る",
                SkillType.Attack, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk),
                Effect.Damage(PowerTier.Large, DamageScale.Atk).If(SkillWhen.FoeHalf)),

            // ⭐ 弱化を受けたあとの切り返しを1手にまとめる（弱化解除の上位でなく複合）
            new Skill("rally", "立て直し", "味方1体の弱化を2つ落とし、HPも少し戻す",
                SkillType.Heal, Target.AllyOne,
                Effect.Cleanse(2),
                Effect.HealRatio(20)),

            // ── UR 級（2026-08-26・作者の指示「URキャラのスキルを持ってきて試す」）─────
            //
            // ⭐ `参考/まもダン_全キャラスキル.md` の UR デバッファーを読んで分かった設計:
            //    ① ダメージと弱化を**同時に**載せる（当たれば必ず盤面が動く）
            //    ② 多段ヒットで**毎回判定**（外れ続けない）
            //    ③ 1発で**複数の弱化**（1手の重みが違う）
            //    ④ **ゲージ操作**で手番そのものを奪う
            // ⚠️ **名前も数値も持ってきていない。**⭐ 借りたのは上の4つの形だけ。
            // ⚠️ 既存の弱化技は①〜④をどれも満たしていない（純粋弱化・1回判定・単体）。
            //    実測でも「弱化役を攻撃役に替えると +22〜27pt」＝席を取れていなかった。
            // 🚧 **まだ種族に配っていない**（`SkillPool` に載せていない）── 測ってから配る。

            // ①② ダメージ3回 ＋ 毒2重。⭐ 毒は防御を無視するので高耐久への回答になる
            new Skill("venom-barrage", "毒牙の乱打", "小さな一撃を3回。高い確率で毒を2つ入れる",
                SkillType.Debuff, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk, 3),
                Effect.Poison(2, 4, chance: 70)),

            // ①③ 全体攻撃 ＋ 防御と速度を同時に落とす
            // ⚠️ 全体なので率は控えめ（単体の `crush` 85% に対して 60%）
            new Skill("collapse", "崩落", "敵全体を攻撃し、防御力とスピードを同時に下げる",
                SkillType.Debuff, Target.EnemyAll,
                Effect.Damage(PowerTier.Small, DamageScale.Atk),
                Effect.Buff(StatKey.Def, -1, 3, chance: 60),
                Effect.Buff(StatKey.Spd, -1, 3, chance: 60)),

            // ④ 手番そのものを奪う。⚠️ ダメージは無い ── 効き目は「相手が動けない」だけ
            // ⚠️ 🔴 **スタンを重ねてはいけない**（2026-08-26 実測）。ゲージ剥ぎもスタンも
            //    「手番を奪う」ので二重取りになり、CT7 にしても **96.7〜98.7%** で
            //    ほぼ確定勝ちだった（2体が交互に撃つと相手が一度も動けない）。
            //    ⭐ ゲージ剥ぎ1本に絞り、量も -60 → -50 に落としてある。
            new Skill("stagnate", "停滞", "敵全体のゲージを大きく戻す",
                SkillType.Debuff, Target.EnemyAll,
                Effect.Gauge(-50, chance: 65)),

            // ── 2026-08-27 に足した6効果を配る（作者の指示）─────────────
            // ⭐ Seal / Anchor / Invincible / Extend / Counter / Effect.Bare は
            //    エンジン・説明文・Audit まで通っていたが、どの技にも使っていなかった
            //    （SkillTextTests.NotYetUsed に「まだ」の印）。ここで技に落とす。
            // ⚠️ 効果の種類は増やしていない。組み合わせは brew の型（攻＋弱・強＋強 等）に倣う。

            // ⭐ Seal。⚠️ 単体では0にならない（枠1は残る）ので、そのまま単発でも成立する。
            //    小さな一撃で押しながら封じる ── venom-fang / stun-strike と同じ「攻＋弱」。
            new Skill("seal-strike", "技封じ", "小さな一撃を与え、たまに枠2・3を封じる",
                SkillType.Debuff, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk),
                Effect.Seal(3, chance: 55)),

            // ⭐ Anchor。防御DOWNを自分で置いてから固着する ── 「落とせない弱化」を
            //    1手で作る型。⚠️ curse（弱化2つ）と同じ「弱＋弱」。
            new Skill("bind-down", "呪縛", "敵の防御力を下げ、高い確率でその弱化を落とせなくする",
                SkillType.Debuff, Target.EnemyOne,
                Effect.Buff(StatKey.Def, -1, 3, chance: 75),
                Effect.Anchor(3, chance: 60)),

            // ⭐ Invincible。⚠️ 味方に配る側は確率を付けない（既存の決まり）。
            //    盾・ガッツ・免疫と並ぶ、単発の守り札。
            // 🔴 **2ターンを1ターンに縮めた**（2026-08-27・`sim skillvalue` 実測）。
            //    ⚠️ `Invincible` は「命中した回数」ではなく「自分の行動回数」で減る
            //    （<see cref="Battle.TickStatus"/> 系と同じ数え方）── 相手の手番の間ずっと
            //    無敵なので、2ターンぶんは**その間に受ける全ヒットを無制限に無効化**した。
            //    実測 1体だけ+12.0pt は無敵2(★4見積り)にしては強すぎ、盾（+4.0pt・確実に
            //    2発だけ防ぐ）の3倍を超えていた。⭐ 1ターンに縮め、盾と近い帯へ寄せた。
            new Skill("invincible", "無敵", "味方1体がダメージを受けなくなる",
                SkillType.Support, Target.AllyOne,
                Effect.Invincible(1)),

            // ⭐ Extend。⚠️ ダメージは弱化を作らないので、伸ばす先は
            //    **味方の別の技が置いた弱化**に限る（「しかけて回収する層」と同じ、
            //    自作自演にならない組み方）。
            new Skill("extend-strike", "弱化延長", "小さな一撃を与え、敵に乗っている弱化を長引かせる",
                SkillType.Debuff, Target.EnemyOne,
                Effect.Damage(PowerTier.Small, DamageScale.Atk),
                Effect.Extend(3, chance: 60)),

            // ⭐ Counter。
            // 🔴 **防御UPを外し、持続も3→1に縮めた**（2026-08-27・`sim skillvalue` 実測）。
            //    ⚠️ 最初は harden（防御UP＋盾）と同じ「強＋強」で Buff(Def,+1,3)＋Counter(3) にしていたが、
            //    `反撃（札）` は「殴られるたび、枠1 をノーコストで撃ち返す」── 多段技を受けると
            //    1回の相手の手番で何度も発動する。実測 1体だけ+17.8pt は、
            //    崩落（★5・+18.0pt）や全体強攻撃（★4・+16.5pt）と並ぶ強さで、
            //    格4（3.15手ぶん）という見積りに対して**実戦は格5相当**だった。
            //    ⭐ 防御UPを外す（被弾を増やして反撃の回数を稼ぐ副作用を消す）＋
            //    持続を1回ぶんに削って、実測を他の格4帯（無敵 +12.0pt 等）に近づけた。
            new Skill("counter-stance", "反撃の構え", "反撃を1回ぶん構える",
                SkillType.Support, Target.Self,
                Effect.Counter(1)),

            // ⭐ Effect.Bare。⚠️ pierce-strike（防御無視）と対になる「守りの買い物を無視する」側。
            //    盾・防御UP・無敵・ガッツを踏み倒す。単発でシンプルに。
            new Skill("bare-strike", "強化無視攻撃", "相手の防御力UP・盾・無敵・ガッツを無視して斬る",
                SkillType.Attack, Target.EnemyOne,
                Effect.Damage(PowerTier.Medium, DamageScale.Atk, bare: true)),

            // ⭐ Seal ＋ Anchor。⚠️ **新しい2効果だけの組**。枠を封じつつ、
            //    今かかっている弱化も落とせなくする ── 支配の締めくくり。
            new Skill("lockdown", "封鎖", "敵の枠2・3を封じ、弱化も落とせなくする",
                SkillType.Debuff, Target.EnemyOne,
                Effect.Seal(3, chance: 55),
                Effect.Anchor(3, chance: 55)),

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
                || effect.Kind == EffectKind.Taunt
                // ⭐ 2026-08-27 に足した3つ（無敵・反撃は味方に配る側なので入らない）
                || effect.Kind == EffectKind.Seal || effect.Kind == EffectKind.Anchor
                || effect.Kind == EffectKind.Extend;
        }

        /// <summary>⭐ **味方・自分に配る「持続する強化」か。**⚠️ **唯一の出所**（2026-08-27）。
        ///
        /// `LoadOf` が「張りっぱなしを止める下限（`longest+1`）」を立てる相手、
        /// `GrowthOf` が「持続と CT を同時に成長軸へ入れない」相手、`Faults` が
        /// 「全レベルで CT ≧ 持続＋1」を確かめる相手 ── **この3つが同じ判断を
        /// 別々に書くと、いつかどれか1つだけ古くなる**（実際に `GrowthOf` 側だけが
        /// この判断を欠いていて、Lv3・Lv5 で下限が壊れていた。2026-08-27 発見）。
        /// ⚠️ **敵に掛けるデバフ・自分への代償（`reckless` の防御DOWN 等）は含めない**
        /// （作者の指示 2026-08-27・デバフに上限は設けない）── `!IsHarmful` で弾く。</summary>
        private static bool IsAllyPersistent(Skill skill, Effect effect) =>
            effect.Turns > 0 && !AtFoe(effect.Own ?? skill.Target) && !IsHarmful(effect);

        /// <summary>⭐ **自分への弱化は代償か。**⚠️ **唯一の出所**（2026-08-27）。
        ///
        /// ⚠️ 値段（<see cref="LoadOf"/>）と AI の採点（<see cref="Ai.ScoreOf"/> 系）が
        /// 同じ判断（「自分に配る弱化＝代償」）を別々の場所に書いていて、**AI 側だけ**
        /// 符号を見ずに得点として足していた（`reckless`＝捨て身の突きの防御DOWNが
        /// 加点されていた）。⭐ 判断を1か所に寄せて、両方がこれを呼ぶ。</summary>
        public static bool IsSelfCost(Effect effect) => effect.Own == Target.Self && IsHarmful(effect);

        /// <summary>その効果の値打ち（`magnitude` ＝ 大きさ・符号なし）を、代償なら向きを
        /// 反転して返す。⭐ **「代償かどうかを見たあと何をするか」の唯一の出所**
        /// （2026-08-27・`SkillValue.cs` が3か所目になったのを機に追加）。
        ///
        /// ⚠️ `IsSelfCost` は判定だけで、使いみち（「じゃあ値を引く」）は呼び側がそれぞれ
        /// 書いていた（`Ai.ScoreOfSkill` は `IsSelfCost(effect) ? -gain * BuffValue : ...`、
        /// `SkillValues.Of` は判定すら無く常に加点）。⭐ 同じ三項演算子を呼び側にまた書くと
        /// 4か所目を生む ── ここへ寄せる。</summary>
        public static double SignedByCost(Effect effect, double magnitude) =>
            IsSelfCost(effect) ? -magnitude : magnitude;

        /// <summary>相手が受け取る「強化」か。⭐ ブロックが止める側。
        /// ⚠️ 自然に溜まるゲージ・自然に減る CT は含まない（あれは買った分ではない）。</summary>
        public static bool IsBoon(Effect effect)
        {
            switch (effect.Kind)
            {
                case EffectKind.Buff: return effect.Sign > 0;
                case EffectKind.Ct: return effect.Delta < 0;
                case EffectKind.Gauge: return effect.Percent > 0;
                // 🔴 **符号を見る。**⚠️ `IsHarmful`（上）は `HealRatio` の符号で弱化かどうかを
                //    決めているのに、ここは符号を見ず一律 true にしていた ── 負（命を削る）
                //    まで「強化」に分類され、Block が弾いてしまっていた（試練 段5・トゲルの
                //    枠2=block／枠3=life-cut で自分のブロックが自分の命削りを消し、
                //    与ダメが 8505→0 になった。2026-08-25 監査で発覚）。
                case EffectKind.HealRatio: return effect.Percent > 0;
                case EffectKind.Regen:
                case EffectKind.Shield:
                case EffectKind.Guts:
                case EffectKind.Immune:
                case EffectKind.Revive:
                // ⭐ 2026-08-27 に足した2つ（味方が受け取る側なのでブロックが止める）
                case EffectKind.Invincible:
                case EffectKind.Counter:
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
        public static readonly HashSet<string> Undistributed = new HashSet<string>
        {
            // ⭐ 2026-08-20 に足した「返す手・1手2役・構え」の7本。
            // ⚠️ どの種族のプールにも入れていない（作者指示 2026-08-19「あてはめはまだいらない」）。
            "cleanse", "cleanse-all",
            "drain-all", "reckless", "warcry",
            "vigor", "sturdy", "nimble",
            // ⭐ 2026-08-20 に足した「回収する側」の8本
            "chase-down", "sweep-down", "pride-strike", "stacked-shot",
            "life-cut", "ambush-strike", "finisher", "rally",
        };

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
        /// <summary>その狙い先が敵側か。⭐ **唯一の出所**（狙い先を足したとき数え落とさないため）。</summary>
        private static bool AtFoe(Target target) =>
            target == Target.EnemyOne || target == Target.EnemyAll || target == Target.EnemyRandom;

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
                // ⚠️ **味方に掛けるものへ確率を付け直さない**（2026-08-21・作者の指示）。
                //    ⭐ 註に書くだけだと、次に「・大」を足すときに同じ形で戻ってくる。
                foreach (var effect in skill.Effects)
                {
                    if (effect.Chance < 100 && !IsHarmful(effect))
                    {
                        problems.Add($"{skill.Id}: 相手が抵抗しない効果に確率 {effect.Chance}% が付いている"
                            + "（味方・自分に掛けるものは必ず通す）");
                    }
                }
                // ⚠️ **上限は約束ごと。**注釈に書いてあるだけだと、次に技を足す人が踏む
                // ⚠️ 出所は `PriceOf` と同じ `CapFor`（2026-08-27）。
                // ⚠️ 🔴 `LoadOf` の**戻り値**（load）でなく **out の `longest`** を渡す。
                LoadOf(skill, out int skillLongest);
                int cap = CapFor(skill, skillLongest);
                if (skill.Ct > cap)
                {
                    problems.Add($"{skill.Id}: CT {skill.Ct} は上限 {cap} を超えている"
                        + (cap == CtCap ? $"（盤面をひっくり返す技だけが {CtHeavy} を許される）" : ""));
                }

                // ⚠️ **上げても何も起きない段**を弾く。これが無いと
                //    「Lv3 にしたのに何も変わらない」が黙って通る（画面には出るのに実体が無い）
                var growth = GrowthOf(skill);
                // ⚠️ 🔴 **段数の「あるべき姿」は `ExpectedGrowthStepsOf` 1本に寄せる**（2026-08-27）。
                //    ⚠️ 味方に配る持続もの「だけ」の技（`IsPureAllyPersistent`）は 0〜2 段、
                //    guardsAllies で軸が1本に削れた技（早駆け・硬化・追い風・鬨の声）は2段、
                //    それ以外は4段 ── この3つを検査側でも別々に書くと、②で踏んだ
                //    「直した本人の目の前で片方だけ古くなる」がここでも起きる。
                if (growth.Count != ExpectedGrowthStepsOf(skill))
                {
                    problems.Add($"{skill.Id}: 成長表が {growth.Count} 段"
                        + $"（{ExpectedGrowthStepsOf(skill)} 段のはず）");
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
                    // ⚠️ 🔴 天井は技ごと（MaxLevelOf）で見る ── グローバルな MaxLevel-1 を
                    //    決め打ちすると、技ごとに上限が縮む世界（2026-08-27）で古くなる。
                    if (asSlot1.Count != MaxLevelOf(skill) - 1)
                        problems.Add($"{skill.Id}: {species.Id} の枠1 で伸ばせる軸が無い");
                }
                int cuts = 0;
                foreach (var gain in growth)
                {
                    string? dead = DeadGain(skill, gain);
                    if (dead != null) problems.Add($"{skill.Id}: {gain} が効かない（{dead}）");
                    // ⚠️ CT は 0 が下限。技の CT より多く縮める段は**何も起きない**
                    // ⚠️ 🔴 **境界は `>=`。**⭐ `cuts == skill.Ct` を見逃すと
                    //    `EffectiveCt` がちょうど 0 になる段を検査が通してしまう
                    //    （2026-08-27 発見）── CT0 は枠1（常に CT0 の通常攻撃）と
                    //    区別が付かなくなる境目なので、縮めた先が「0 になる」ことも
                    //    「0 を割り込む」ことと同じく落とす。
                    if (gain == SkillGain.Ct && ++cuts >= skill.Ct)
                    {
                        problems.Add($"{skill.Id}: CT を {cuts} 回縮めるが、元の CT は {skill.Ct}");
                    }
                }

                // ⚠️ 🔴 **「CT ≧ 持続＋1」は Lv1 限定だった**（2026-08-27 発見）。
                //    ⭐ `PriceOf` が立てる下限（`longest + 1`）は**素の Turns**しか見ておらず、
                //    育てて持続が伸び・CT が縮む技（成長表が `持続→CT→持続→CT` の形）では
                //    Lv3・Lv5 で下限が壊れる ── 免疫・リジェネ・強化（攻撃力/防御力/
                //    スピードUP）・ガッツがこれで永久バフに化けていた。
                //    ⚠️ ここは**敵に掛けるデバフ・自分への代償（reckless 等）には
                //    適用しない**（作者の指示 2026-08-27・デバフに上限は設けない）──
                //    `LoadOf`/`GrowthOf` と**同じ判断**（`IsAllyPersistent`）をそのまま
                //    使う（出所を1つに。ここだけ `!AtFoe` にすると reckless の自傷まで
                //    誤って拾う）。
                //    ⭐ 全レベル（Lv1〜MaxLevelOf）を直に確かめる ── 天井（CtCap）に当たって
                //    Lv1 から破れる技（当時＝持続6T版の `guts-deep`/`immune-long`）も、
                //    これで初めて見える。
                //    ⚠️ 🔴 技ごとの上限（MaxLevelOf）までで確かめる（2026-08-27）── グローバルな
                //    MaxLevel まで回しても誤りは出ないが（BoostOf は上限で頭打ちする）、
                //    「この技が実際に届く範囲」を確かめる形に揃える。
                foreach (var effect in skill.Effects)
                {
                    if (!IsAllyPersistent(skill, effect)) continue;
                    for (int level = 1; level <= MaxLevelOf(skill); level++)
                    {
                        var boost = BoostOf(skill, level);   // ⚠️ 枠1 以外を見る（既定の slot=-1）
                        int turnsNow = effect.Turns + boost.ExtraTurns;
                        // ⭐ 枠1 でなければ何でもいい ── slot=1 は「枠1 でない」の代表値
                        int ctNow = EffectiveCt(1, skill, boost);
                        if (ctNow < turnsNow + 1)
                        {
                            problems.Add($"{skill.Id}: Lv{level} で CT{ctNow} が持続{turnsNow}+1 を下回る"
                                + "（味方に配る持続が、育てた先で切れなくなりうる）");
                        }
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
            // ⚠️ **パッシブの決まり。**押せない技なので、普通の技の検査が素通りしてしまう
            //    （狙い先も CT も意味を持たないため）。ここで別に数える。
            foreach (var skill in table)
            {
                if (skill.Passive)
                {
                    if (skill.Ct != 0)
                        problems.Add($"{skill.Id}: パッシブなのに CT {skill.Ct}（押せないので待てない）");
                    if (skill.Target != Target.Self)
                        problems.Add($"{skill.Id}: パッシブの狙いが「{SkillText.TargetOf(skill.Target)}」"
                            + "（自分にしか効かない）");
                    foreach (var e in skill.Effects)
                    {
                        if (!e.Innate)
                            problems.Add($"{skill.Id}: パッシブが「生まれつき」以外の効果を持っている"
                                + "（引き金つきの働きは特性の仕事）");
                    }
                }
                else
                {
                    foreach (var e in skill.Effects)
                    {
                        if (e.Innate)
                            problems.Add($"{skill.Id}: パッシブでないのに「生まれつき」を持っている");
                    }
                }
            }

            // ⚠️ **条件つきの決まり。**（設計案 §2）
            foreach (var skill in table)
            {
                for (int i = 0; i < skill.Effects.Count; i++)
                {
                    var e = skill.Effects[i];
                    // ⚠️ **1つ目の効果に条件を付けない。**全部が条件つきだと、
                    //    条件を外して押した手番が丸ごと空振りする
                    if (i == 0 && e.When != null)
                        problems.Add($"{skill.Id}: 最初の効果に条件が付いている（外すと空振りする技になる）");
                    // ⚠️ 数えられるのはダメージだけ
                    if (e.Per != Tally.None && e.Kind != EffectKind.Damage)
                        problems.Add($"{skill.Id}: ダメージ以外に数えが付いている（{e.Kind}）");
                }
                // ⚠️ **自作自演の禁止。**同じ技で条件を作って同じ技で回収すると、条件が常に真になる
                foreach (var e in skill.Effects)
                {
                    if (e.When == null && e.Per == Tally.None) continue;
                    bool wantsBane = e.When == SkillWhen.FoeWeakened || e.Per == Tally.FoeBanes;
                    bool wantsStop = e.When == SkillWhen.FoeStopped;
                    foreach (var other in skill.Effects)
                    {
                        if (ReferenceEquals(other, e)) continue;
                        bool atFoe = AtFoe(other.Own ?? skill.Target);
                        if (!atFoe) continue;
                        if (wantsBane && IsHarmful(other) && other.Kind != EffectKind.Damage)
                            problems.Add($"{skill.Id}: 同じ技で弱化を付けて、同じ技でそれを見ている（条件が常に真になる）");
                        if (wantsStop && (other.Kind == EffectKind.Stun || other.Kind == EffectKind.Sleep))
                            problems.Add($"{skill.Id}: 同じ技で止めて、同じ技でそれを見ている（条件が常に真になる）");
                    }
                }
            }

            foreach (var skill in table)
            {
                if (skill.Passive) continue;   // ⚠️ 狙い先の検査は押せる技だけ
                bool mainAtFoe = AtFoe(skill.Target);

                // ⭐ **技の狙い先へ飛ぶぶん**（飛び先を持たない効果）は、これまでどおり束で見る。
                bool harmful = false, kindly = false;
                foreach (var e in skill.Effects)
                {
                    if (e.Own != null) continue;
                    if (IsHarmful(e)) harmful = true;
                    else if (e.Kind != EffectKind.Damage) kindly = true;
                }
                if (harmful && !mainAtFoe)
                    problems.Add($"{skill.Id}: 弱化を持つのに狙いが「{SkillText.TargetOf(skill.Target)}」");
                if (kindly && !harmful && mainAtFoe)
                    problems.Add($"{skill.Id}: 味方に効くものを「{SkillText.TargetOf(skill.Target)}」へ向けている");

                // ⭐ **飛び先を持つぶん（1手2役）は1つずつ見る。**
                // ⚠️ 束で見ると、代償として自分に掛ける弱化まで「狙いが敵でない」と落ちる
                //    ── それは事故ではなく、その技の値段そのもの。
                foreach (var e in skill.Effects)
                {
                    if (e.Own == null) continue;
                    var aside = e.Own.Value;
                    // ⚠️ **敵へ飛ばすなら害でなければならない。**
                    //    ここが無いと「敵全体を回復する」書き間違いが黙って通る。
                    if (AtFoe(aside) && !IsHarmful(e) && e.Kind != EffectKind.Damage)
                        problems.Add($"{skill.Id}: 味方に効くものを飛び先「{SkillText.TargetOf(aside)}」へ向けている");
                    // ⚠️ **同じ側へ飛ばすなら、飛ばす意味が無い。**
                    //    普通の効果として書けるので、書き間違いのほうを疑う。
                    if (AtFoe(aside) == mainAtFoe)
                        problems.Add($"{skill.Id}: 飛び先「{SkillText.TargetOf(aside)}」が狙いと同じ側"
                            + "（飛ばさずに書ける）");
                }
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
