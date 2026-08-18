#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    public enum Side
    {
        Ally,
        Enemy,
    }

    public enum Outcome
    {
        Ally,
        Enemy,
        Draw,
    }

    /// <summary>ステータスに掛かる修正。⭐ 段階ではなく、ステータスの数値に対する ±%。</summary>
    public struct Modifier
    {
        public int Percent;
        /// <summary>残り。その個体の行動回数で減る（CT と同じ数え方）。</summary>
        public int Turns;
    }

    /// <summary>スタックする状態（毒・リジェネ）。</summary>
    public struct Stacking
    {
        public int Stacks;
        public int Turns;
    }

    /// <summary>持続する状態。⚠️ 数える単位は全部「その個体の行動回数」。</summary>
    public sealed class UnitStatus
    {
        public Modifier Atk;
        public Modifier Def;
        public Modifier Spd;
        /// <summary>毒。1行動ごとに最大HPの TickPercent × Stacks% 減る。</summary>
        public Stacking Poison;
        /// <summary>リジェネ。1行動ごとに回復。</summary>
        public Stacking Regen;
        /// <summary>⭐ シールドの残り枚数。1回の攻撃につき1枚消費し、その攻撃を完全に無効化する。</summary>
        public int Shield;
        /// <summary>飛ばす手番の残り。</summary>
        public int Stun;
        /// <summary>味方への単体攻撃を引き受ける残り回数。</summary>
        public int Taunt;
        /// <summary>挑発を掛けてきた相手の <see cref="Unit.Key"/>。
        /// ⭐ 挑発は**相手に付ける弱化**なので、単体攻撃の狙い先がここに固定される。</summary>
        public string? TauntBy;
        public int Guts;
        public int Immune;
        /// <summary>睡眠の残り。⚠️ 攻撃を受けた時点で 0 になる。</summary>
        public int Sleep;
        /// <summary>ブロックの残り。⭐ 外から受け取る回復と強化を弾く。</summary>
        public int Block;

        public ref Modifier ModOf(StatKey key)
        {
            switch (key)
            {
                case StatKey.Atk: return ref Atk;
                case StatKey.Def: return ref Def;
                case StatKey.Spd: return ref Spd;
                default: throw new ArgumentOutOfRangeException(nameof(key), "buff は atk/def/spd のみ");
            }
        }
    }

    public sealed class Unit
    {
        public readonly Creature Creature;
        public readonly Side Side;
        public readonly int Slot;
        public readonly string Key;
        public readonly string Name;
        public readonly int MaxHp;
        /// <summary>⭐ 手数の倍率。少数側の1体が背負う人数ぶん、ゲージが速く溜まる。
        /// ⚠️ 味方は常に 1。孤立している側だけが上がる。</summary>
        public readonly double Tempo;

        public int Hp;
        public int Gauge;
        public UnitStatus Status;
        /// <summary>スキル枠3つぶん。0 なら使える。</summary>
        public readonly int[] Cooldowns = new int[3];

        public Unit(Creature creature, Side side, int slot, string name, int maxHp, double tempo)
        {
            Creature = creature;
            Side = side;
            Slot = slot;
            Key = $"{(side == Side.Ally ? "ally" : "enemy")}-{slot}";
            Name = name;
            MaxHp = maxHp;
            Tempo = tempo;
            Hp = maxHp;
            Gauge = 0;
            Status = new UnitStatus();
        }
    }

    public enum BattleEventKind
    {
        Act, Damage, Heal, Buff, Poison, Regen, Applied, Shield, Stun, Skipped,
        Ct, Taunt, Guts, GutsSaved, Immune, Blocked, Down,
        /// <summary>ゲージが動いた。</summary>
        Gauge,
        /// <summary>眠った。</summary>
        Sleep,
        /// <summary>目を覚ました（殴られて解けた）。</summary>
        Woke,
        /// <summary>ブロックが付いた。</summary>
        Block,
        /// <summary>ブロックで弾かれた。</summary>
        Blunted,
        /// <summary>強化を消した／奪った。</summary>
        Dispelled,
        /// <summary>蘇った。</summary>
        Revived,
        /// <summary>弱化が外れた。⭐ 免疫で弾いた（Blocked）とは分ける。</summary>
        Missed,
    }

    public sealed class BattleEvent
    {
        public readonly BattleEventKind Kind;
        /// <summary>対象（または行動者）の <see cref="Unit.Key"/>。</summary>
        public readonly string Unit;
        /// <summary>act のときは技名、applied のときは表示用の札。</summary>
        public readonly string? Label;
        public readonly int Amount;
        public readonly int Hp;
        public readonly int Absorbed;
        public readonly StatKey Stat;
        public readonly int Percent;
        public readonly int Turns;
        public readonly int Delta;
        public readonly int Hits;

        public BattleEvent(BattleEventKind kind, string unit, string? label = null, int amount = 0,
            int hp = 0, int absorbed = 0, StatKey stat = default, int percent = 0,
            int turns = 0, int delta = 0, int hits = 0)
        {
            Kind = kind;
            Unit = unit;
            Label = label;
            Amount = amount;
            Hp = hp;
            Absorbed = absorbed;
            Stat = stat;
            Percent = percent;
            Turns = turns;
            Delta = delta;
            Hits = hits;
        }
    }

    public sealed class BattleState
    {
        public readonly List<Unit> Units;
        public int Actions;
        public readonly List<BattleEvent> Log = new List<BattleEvent>();
        public Outcome? Result;

        /// <summary>弱化が通るかを引く乱数。
        ///
        /// ⚠️ **戦闘が持つ唯一の乱数。** ここ以外に運を入れない
        /// （命中率も会心も無い。入れると「1万回の勝率」が個体差ではなく運を測る）。
        /// ⭐ 種を渡せば同じ試合を再現できるので、検査も測定も繰り返せる。
        /// ⚠️ 通る率が 100 の効果では**引かない**。移植した技の試合が1手も変わらないように。</summary>
        public readonly Rng Rng;

        public BattleState(List<Unit> units, Rng? rng = null)
        {
            Units = units;
            Rng = rng ?? new Rng(0).Stream("land");
        }
    }

    /// <summary>戦闘。3体同時・スピードゲージ制・スキルごとの CT。
    ///
    /// ⚠️ 戦闘そのものに乱数を入れていない（命中率も会心も無い）。
    /// ⭐ こうすると「1万回の勝率」が戦闘の運ではなく個体差の分布を測ることになり、
    /// 釣り合いの検算が濁らない。運の要素を入れるなら、入れた後で必ず測り直す。
    ///
    /// ⚠️ ゲージと CT は整数で進める。浮動小数のドリフトを持ち込まない。
    ///
    /// 強さの計算はここが唯一の出所。画面もシミュレータもこの関数群を呼ぶ。
    /// </summary>
    public static class Battle
    {
        /// <summary>ゲージが満ちる値。</summary>
        public const int GaugeMax = 1000;

        /// <summary>全員が持つ基礎テンポ。ゲージは GaugeBase + 速度 ずつ溜まる。
        ///
        /// ⚠️ これが無いと速度一強になる（実測: 速度型の勝率 100%）。
        /// 速度は「行動回数」という全出力への倍率なので、素で効かせると上限が無い。
        /// 一方ダメージは式で頭打ちになるので、攻撃はどれだけ振っても追いつけない。
        /// ⭐ 副産物として速度0でも止まらない。</summary>
        public const int GaugeBase = 55;

        /// <summary>⚠️ 決着しない戦闘を止める上限。
        /// ⚠️ 飛ばした手番もここに数える。全員がスタンし続ける形で止まらないように。</summary>
        public const int MaxActions = 300;

        /// <summary>HP の尺度。保証したいこと: 平均的な個体同士で、1体を倒すのに 5〜12 発。</summary>
        public const int HpScale = 3;

        /// <summary>属性の有利倍率。3すくみ。</summary>
        public const double ElementAdvantage = 1.5;

        /// <summary>属性の不利倍率。⚠️ <see cref="ElementAdvantage"/> の逆数**ではない**。
        /// 逆数（0.667）にしていたとき、有利側の勝率が実測で 100% になった。</summary>
        public const double ElementWeakness = 0.75;

        /// <summary>攻撃・防御それぞれの効きを飽和させる定数。
        ///
        /// ⭐ 値は2次元に掃引して決めた。防御側を大きく取ってあるのは、
        /// 集中攻撃のせいで防御が攻撃の約3倍の価値を持つため。</summary>
        public const int AtkSoften = 20;
        public const int DefSoften = 110;

        private const int Parity = 40;
        public const double DamageNormalize = (double)(DefSoften + Parity) / (AtkSoften + Parity);

        // ── 特性の効き目 ─────────────────────────────────
        // ⭐ **特性は技を強くしない。動きを強くする。**
        // 技を直に強くすると「結局その技を持つのが正解」で終わり、組み合わせの判断が消える。
        // ⚠️ 値は勘で置かない。`sim traits` で「有ると無いとで勝率が何 pt 動くか」を測って決める。

        /// <summary>狙い澄まし: 弱化が通る率に足す %ポイント。
        /// ⚠️ <see cref="LandSwing"/>（速度差の振れ幅）より小さくしてある。
        /// 上回ると「速い個体を弱化役にする」という既にある理屈を特性が押しのけてしまう。
        /// ⚠️ **30 にすると逆に下がる**（+3.5pt → +1.8pt・2026-08-17 実測 400回×2種）。
        /// 通る率は <see cref="LandCeil"/> 95% で頭打ちなうえ、弱化が通るほど相手の手も変わるので、
        /// 効き目はここの値に対して単調ではない。⭐ **上げる前に必ず測る。**</summary>
        public const int TraitAim = 20;

        /// <summary>意地: 弱化を受ける率から引く %ポイント。
        /// ⚠️ <see cref="TraitAim"/> と同じ（30 では −5.8〜−0.3pt と符号まで崩れた）。</summary>
        public const int TraitStubborn = 20;

        /// <summary>返し身: 受けたダメージのうち返す割合（%）。
        /// ⚠️ 25% では勝率が +33pt 動き、6件のうち独りだけ桁が違った。
        /// ⭐ **殴られること自体が条件**なので、他の特性と違って技を選ばずに常時働く。
        /// 常時働くものは、選んで働くものより効き目を小さくしておかないと一択になる。</summary>
        public const int TraitSpitePercent = 12;

        /// <summary>食らいつき: 与えたダメージのうち吸う割合（%）。
        /// ⚠️ <see cref="TraitSpitePercent"/> と同じ理由で下げてある（25% で +24pt だった）。</summary>
        public const int TraitLeechPercent = 15;

        /// <summary>スタンを重ねて増やせる上限。⚠️ スタンだけが**足す**効果なので、
        /// 上限が無いと強打の重ねがけで手番が延々飛ぶ。</summary>
        public const int StunStackMax = 2;

        /// <summary>執念: 盾が1枚剥がれるたびに溜まるゲージ。⭐ <see cref="GaugeMax"/> の 1/4。</summary>
        public const int TraitGritGauge = 250;

        /// <summary>その特性を持っているか。⚠️ 持たない個体では必ず false ＝ 従来どおり。</summary>
        private static bool HasTrait(Unit unit, string traitId) => unit.Creature.TraitId == traitId;

        /// <summary>割合を取る。⚠️ 0 にしない（「効いたのに何も起きない」を作らない）。</summary>
        private static int Ratio(int value, int percent)
        {
            int taken = (int)Math.Floor((double)(value * percent) / 100);
            return taken < 1 ? 1 : taken;
        }

        /// <summary>⚠️ JS の Math.round は「0.5 は上へ」。C# の既定は銀行丸めなので使わない。</summary>
        private static int JsRound(double value) => (int)Math.Floor(value + 0.5);

        // ── 唯一の出所となる計算 ──────────────────────────────

        /// <summary>修正を掛けた実効値。⚠️ 1 未満に落とさない（速度0は割り算で壊れる）。</summary>
        public static int EffectiveStat(int baseValue, Modifier mod)
        {
            int percent = mod.Turns > 0 ? mod.Percent : 0;
            int value = (int)Math.Floor((double)(baseValue * (100 + percent)) / 100);
            return value < 1 ? 1 : value;
        }

        /// <summary>属性の倍率。炎 → 木 → 水 → 炎。
        ///
        /// ⭐ **有利と不利は対称にしない。** 有利 ×1.5 に対して不利は ×0.75。
        /// 対称（1/1.5 = 0.667）だと不利側の被害が大きすぎて、
        /// 実測で**有利側の勝率が 100% / 0%** になっていた。
        /// そこまで決まりきると、種族を何種足しても選び方は
        /// 「相手の属性を見て counter を出す」の一手に収束し、組み合わせが生まれない。
        ///
        /// ⚠️ 逆に 1.0 へ寄せすぎると「有利な属性を探す」動機が消える。
        /// どちらへ動かすときも <c>sim elements</c> で測ってから決める。</summary>
        public static double ElementMultiplier(Element attacker, Element defender)
        {
            if (SpeciesTable.Beats(attacker) == defender) return ElementAdvantage;
            if (SpeciesTable.Beats(defender) == attacker) return ElementWeakness;
            return 1.0;
        }

        /// <summary>ダメージ。
        /// ⭐ power × (A+atk) / (D+def)。絶対値が効くので特化が報われ、
        /// 分子・分母とも定数で底上げしてあるので爆発も一方のステの一強も起きない。</summary>
        public static int DamageOf(int power, int attackStat, int defenseStat, double elementMult)
        {
            double raw = power * DamageNormalize * (AtkSoften + attackStat) / (DefSoften + defenseStat);
            int value = (int)Math.Floor(raw * elementMult);
            return value < 1 ? 1 : value;
        }

        /// <summary>1刻みでゲージがいくつ溜まるか。唯一の出所。</summary>
        public static int GaugeRate(int speed, double tempo = 1.0)
        {
            int s = speed < 0 ? 0 : speed;
            return JsRound((GaugeBase + s) * tempo);
        }

        public static int TicksToAct(int gauge, int speed, double tempo = 1.0) =>
            (int)Math.Ceiling((double)(GaugeMax - gauge) / GaugeRate(speed, tempo));

        // ── 組み立て ────────────────────────────────────────

        public static Unit MakeUnit(Creature creature, Side side, int slot,
            double hpScale = 1.0, double tempo = 1.0)
        {
            int maxHp = JsRound(Creatures.StatsOf(creature).Hp * HpScale * hpScale);
            return new Unit(creature, side, slot, Creatures.SpeciesOf(creature).Name, maxHp, tempo);
        }

        /// <summary>⭐ 少数側の1体が「何人分」を背負うか（体数の比）。
        /// ⚠️ ボス専用の例外を作らない。体数の比で決めるので、2体にしても3体に戻しても式は変わらない。</summary>
        public static double LoneScale(int allyCount, int enemyCount)
        {
            if (enemyCount <= 0) return 1.0;
            double scale = (double)allyCount / enemyCount;
            return scale < 1.0 ? 1.0 : scale;
        }

        /// <summary>HP は人数ぶんそのまま持つ。一つの器に同じ総量を入れるだけ。</summary>
        public static double LoneHp(double scale) => scale;

        /// <summary>⭐ 手数は増える分を半分に割り引く。
        ///
        /// ⚠️ 3体は倒すたびに弱くなるが、1体は倒れるまで弱くならない。
        /// だから同じ総量でも単体のほうが強い。人数ぶんそのまま手数を与えると
        /// 初期パーティが段1にすら勝てなくなった（実測）。
        /// 逆に手数を据え置く（1倍）と、硬いだけの案山子になって
        /// 初期パーティでボスに勝ててしまい輪が閉じなかった（実測 47行動）。
        /// ⭐ 掃引した結果、HP=体数比 / 手数=増分の半分 が
        /// 「段1・2は勝てる / 上位とボスには負ける」形になった。</summary>
        public static double LoneTempo(double scale) => 1.0 + (scale - 1.0) * 0.5;

        /// <param name="rng">弱化が通るかを引く乱数。⚠️ 渡さなければ固定の種
        /// （同じ編成からは必ず同じ試合になる）。</param>
        public static BattleState CreateBattle(IReadOnlyList<Creature> allies, IReadOnlyList<Creature> enemies,
            Rng? rng = null)
        {
            double scale = LoneScale(allies.Count, enemies.Count);
            var units = new List<Unit>(allies.Count + enemies.Count);
            for (int i = 0; i < allies.Count; i++) units.Add(MakeUnit(allies[i], Side.Ally, i));
            for (int i = 0; i < enemies.Count; i++)
                units.Add(MakeUnit(enemies[i], Side.Enemy, i, LoneHp(scale), LoneTempo(scale)));
            return new BattleState(units, rng);
        }

        /// <summary>潜入で負った傷と CT を、この戦闘の味方へ載せる。
        ///
        /// ⭐ これが「雑魚を倒して投げる回数を戻す」の対価。
        /// 戻すたびに削られるので、**戦うほど最後の親戦が苦しくなる**。
        /// ⚠️ 満タンに戻す作りにしない ── 戻すと雑魚は「無料の回数券」になる。
        ///
        /// ⚠️ **HP は 1 未満にしない。**潜入は3体を投げ続ける遊びなので、
        /// 投げられない個体ができると発射回数のリセットそのものが働かなくなる。
        /// ⭐ 倒れた個体は気絶から立つが、瀕死のまま次へ行く。</summary>
        /// <param name="hp">-1 は「満タン（まだ傷を負っていない）」。⚠️ 触らない。</param>
        public static void CarryIn(BattleState state, IReadOnlyList<int>? hp,
            IReadOnlyList<int[]>? cooldowns)
        {
            foreach (var unit in state.Units)
            {
                if (unit.Side != Side.Ally) continue;
                if (hp != null && unit.Slot < hp.Count && hp[unit.Slot] >= 0)
                {
                    int carried = hp[unit.Slot];
                    if (carried < 1) carried = 1;
                    unit.Hp = carried > unit.MaxHp ? unit.MaxHp : carried;
                }
                if (cooldowns == null || unit.Slot >= cooldowns.Count) continue;
                var from = cooldowns[unit.Slot];
                for (int slot = 0; slot < unit.Cooldowns.Length && slot < from.Length; slot++)
                {
                    unit.Cooldowns[slot] = from[slot];
                }
            }
        }

        /// <summary>戦闘のあとの味方の傷と CT を書き出す。⭐ <see cref="CarryIn"/> の対。
        /// ⚠️ 並びは編成の並び（<c>Slot</c>）。潜入の <c>Party</c> と同じ順であること。</summary>
        public static void CarryOut(BattleState state, List<int> hp, List<int[]> cooldowns)
        {
            foreach (var unit in state.Units)
            {
                if (unit.Side != Side.Ally) continue;
                if (unit.Slot < hp.Count) hp[unit.Slot] = unit.Hp;
                if (unit.Slot >= cooldowns.Count) continue;
                var into = cooldowns[unit.Slot];
                for (int slot = 0; slot < into.Length && slot < unit.Cooldowns.Length; slot++)
                {
                    into[slot] = unit.Cooldowns[slot];
                }
            }
        }

        /// <summary>強化を <paramref name="count"/> 個 剥がす。
        /// <paramref name="into"/> を渡すと**消さずにそこへ移す**（強化強奪）。
        ///
        /// ⚠️ 剥がす順は固定にする（攻→防→速→盾→ガッツ→免疫→リジェネ）。
        /// 乱数で選ぶと「同じ編成なら同じ結果」という約束が崩れる。
        /// ⭐ 数えるのは**乗っている強化の個数**であって効果量ではない。</summary>
        /// <returns>実際に剥がした個数。</returns>
        public static int StripBoons(Unit target, int count, Unit? into)
        {
            if (count <= 0) return 0;
            int gone = 0;
            var s = target.Status;

            foreach (var key in Stats.Keys)
            {
                if (gone >= count) break;
                if (key == StatKey.Hp) continue;
                ref var mod = ref s.ModOf(key);
                if (mod.Turns <= 0 || mod.Percent <= 0) continue;
                if (into != null)
                {
                    ref var to = ref into.Status.ModOf(key);
                    to.Percent = mod.Percent;
                    to.Turns = mod.Turns;
                }
                mod.Percent = 0; mod.Turns = 0;
                gone++;
            }
            if (gone < count && s.Shield > 0)
            {
                if (into != null) into.Status.Shield = s.Shield;
                s.Shield = 0; gone++;
            }
            if (gone < count && s.Guts > 0)
            {
                if (into != null) into.Status.Guts = s.Guts;
                s.Guts = 0; gone++;
            }
            if (gone < count && s.Immune > 0)
            {
                if (into != null) into.Status.Immune = s.Immune;
                s.Immune = 0; gone++;
            }
            if (gone < count && s.Regen.Turns > 0)
            {
                if (into != null) into.Status.Regen = s.Regen;
                s.Regen = new Stacking(); gone++;
            }
            return gone;
        }

        public static bool IsAlive(Unit unit) => unit.Hp > 0;

        public static List<Unit> LivingOf(BattleState state, Side side)
        {
            var list = new List<Unit>();
            foreach (var unit in state.Units)
            {
                if (unit.Side == side && IsAlive(unit)) list.Add(unit);
            }
            return list;
        }

        public static int SpeedOf(Unit unit) =>
            EffectiveStat(Creatures.StatsOf(unit.Creature).Spd, unit.Status.Spd);

        public static Skill? SkillAt(Unit unit, int slot)
        {
            var list = Creatures.SkillsOf(unit.Creature);
            return slot >= 0 && slot < list.Length ? list[slot] : null;
        }

        /// <summary>⭐ 枠1は CT 0 なので常に使える。これが「たたかう」の代わり。</summary>
        public static bool IsUsable(Unit unit, int slot)
        {
            if (SkillAt(unit, slot) == null) return false;
            return unit.Cooldowns[slot] == 0;
        }

        public static Skill ActionSkill(Unit unit, int slot)
        {
            var skill = SkillAt(unit, slot);
            if (skill == null) throw new InvalidOperationException($"{unit.Key} の枠 {slot} は空");
            return skill;
        }

        public static bool NeedsTarget(Skill skill) => skill.Target == Target.EnemyOne;

        // ── 進行 ────────────────────────────────────────────

        private static Outcome? DecideOutcome(BattleState state)
        {
            int allies = LivingOf(state, Side.Ally).Count;
            int enemies = LivingOf(state, Side.Enemy).Count;
            if (allies == 0 && enemies == 0) return Outcome.Draw;
            if (enemies == 0) return Outcome.Ally;
            if (allies == 0) return Outcome.Enemy;
            if (state.Actions >= MaxActions) return Outcome.Draw;
            return null;
        }

        /// <summary>その個体が行動する直前に、持続するものを1つ進める。
        /// ⚠️ 毒で倒れることがあるので、呼んだ側は生死を見直す。</summary>
        private static void TickStatus(BattleState state, Unit unit)
        {
            var s = unit.Status;

            // ⭐ 重なっているぶんだけ強く効く
            if (s.Poison.Turns > 0)
            {
                int amount = (int)Math.Floor((double)(unit.MaxHp * Skills.TickPercent * s.Poison.Stacks) / 100);
                if (amount < 1) amount = 1;
                unit.Hp = Math.Max(0, unit.Hp - amount);
                state.Log.Add(new BattleEvent(BattleEventKind.Poison, unit.Key, amount: amount, hp: unit.Hp));
                s.Poison.Turns--;
                if (s.Poison.Turns == 0) s.Poison.Stacks = 0;
                if (unit.Hp == 0) state.Log.Add(new BattleEvent(BattleEventKind.Down, unit.Key));
            }
            if (s.Regen.Turns > 0 && IsAlive(unit))
            {
                int amount = (int)Math.Floor((double)(unit.MaxHp * Skills.TickPercent * s.Regen.Stacks) / 100);
                if (amount < 1) amount = 1;
                int before = unit.Hp;
                unit.Hp = Math.Min(unit.MaxHp, unit.Hp + amount);
                state.Log.Add(new BattleEvent(BattleEventKind.Regen, unit.Key, amount: unit.Hp - before, hp: unit.Hp));
                s.Regen.Turns--;
                if (s.Regen.Turns == 0) s.Regen.Stacks = 0;
            }

            foreach (var key in new[] { StatKey.Atk, StatKey.Def, StatKey.Spd })
            {
                ref var mod = ref s.ModOf(key);
                if (mod.Turns > 0)
                {
                    mod.Turns--;
                    if (mod.Turns == 0) mod.Percent = 0;
                }
            }
            if (s.Guts > 0) s.Guts--;
            if (s.Immune > 0) s.Immune--;
            if (s.Sleep > 0) s.Sleep--;
            if (s.Block > 0) s.Block--;
            // ⚠️ 挑発の掛け手が居なくなったら固定を解く（居ない相手を狙い続けない）
            if (s.Taunt <= 0) s.TauntBy = null;
        }

        /// <summary>手番を1つ消費する（行動せずに）。</summary>
        private static void ConsumeTurn(BattleState state, Unit unit)
        {
            unit.Gauge -= GaugeMax;
            // ⚠️ 飛ばした手番も数える。数えないと全員スタンで止まらなくなる
            state.Actions++;
            state.Result = DecideOutcome(state);
        }

        /// <summary>ゲージを少しだけ進める。⭐ **見せるため**の刻み。
        ///
        /// <see cref="NextActor"/> は「誰かが満ちる瞬間」まで一気に飛ぶので、
        /// 画面ではゲージが一瞬で切り替わり、競り合いが見えない。
        /// これを毎フレーム少しずつ呼べば、同じ結果のまま競り合いが目に見える。
        ///
        /// ⚠️ 最初の1体が満ちるところで必ず止める。行き過ぎると全員が余分に貰い、
        /// 「内部ゲージが最も多い者が動く」の順位が変わって <see cref="NextActor"/> と
        /// 結果が食い違う（＝見せるためのコードが勝敗を変えてしまう）。
        /// </summary>
        /// <returns>実際に進めた刻み数。0 なら誰かが既に満ちている。</returns>
        public static int AdvanceGauges(BattleState state, int ticks)
        {
            if (ticks <= 0 || state.Result != null) return 0;

            var living = new List<Unit>();
            foreach (var unit in state.Units)
            {
                if (IsAlive(unit)) living.Add(unit);
            }
            if (living.Count == 0) return 0;

            int limit = int.MaxValue;
            foreach (var unit in living)
            {
                int t = TicksToAct(unit.Gauge, SpeedOf(unit), unit.Tempo);
                if (t < limit) limit = t;
            }
            if (limit <= 0 || limit == int.MaxValue) return 0;

            int step = ticks < limit ? ticks : limit;
            foreach (var unit in living) unit.Gauge += step * GaugeRate(SpeedOf(unit), unit.Tempo);
            return step;
        }

        /// <summary>次に行動する者まで時間を進める。
        /// ⚠️ 毒で倒れた者・スタン中の者は、ここで手番を消費して次へ送る。</summary>
        public static Unit? NextActor(BattleState state)
        {
            for (int guard = 0; guard < MaxActions * 2; guard++)
            {
                state.Result = DecideOutcome(state);
                if (state.Result != null) return null;

                var living = new List<Unit>();
                foreach (var unit in state.Units)
                {
                    if (IsAlive(unit)) living.Add(unit);
                }
                if (living.Count == 0) return null;

                int ticks = int.MaxValue;
                foreach (var unit in living)
                {
                    int t = TicksToAct(unit.Gauge, SpeedOf(unit), unit.Tempo);
                    if (t < ticks) ticks = t;
                }
                if (ticks > 0 && ticks != int.MaxValue)
                {
                    foreach (var unit in living) unit.Gauge += ticks * GaugeRate(SpeedOf(unit), unit.Tempo);
                }

                // ⭐ 満ちた者のうち「内部ゲージが最も多い」者が動く。速度ではない。
                // ⚠️ 以前は配列の並び順で決めていた。ゲージは満タンを超えて繰り越されるのに、
                // 超過ぶんが一切報われていなかった。
                Unit? best = null;
                foreach (var unit in living)
                {
                    if (unit.Gauge < GaugeMax) continue;
                    if (best == null || unit.Gauge > best.Gauge) best = unit;
                }
                if (best == null) return null;

                TickStatus(state, best);
                if (!IsAlive(best))
                {
                    ConsumeTurn(state, best);
                    continue;
                }
                if (best.Status.Stun > 0)
                {
                    best.Status.Stun--;
                    state.Log.Add(new BattleEvent(BattleEventKind.Skipped, best.Key));
                    ConsumeTurn(state, best);
                    continue;
                }
                // ⭐ 睡眠もスタンと同じく手番を飛ばす。⚠️ 違いは殴られると解けること
                if (best.Status.Sleep > 0)
                {
                    state.Log.Add(new BattleEvent(BattleEventKind.Skipped, best.Key));
                    ConsumeTurn(state, best);
                    continue;
                }
                return best;
            }
            return null;
        }

        /// <summary>効果を1つだけ打ち込む。⭐ **技を作らずに効果そのものを試すための入口。**
        /// ⚠️ 遊びからは使わない（技を通さない行動は存在しない）。検査と測定のためだけ。</summary>
        public static void ApplyOne(BattleState state, Unit actor, Unit target, Effect effect) =>
            ApplyEffect(state, actor, target, effect, new SkillBoost());

        /// <summary>狙い先を引く。⭐ 検査から狙いの規則（挑発など）を直に確かめるための入口。</summary>
        public static List<Unit> TargetsFor(BattleState state, Unit actor, Target target,
            Unit? chosen) => TargetsOf(state, actor, target, chosen);

        private static List<Unit> TargetsOf(BattleState state, Unit actor, Skill skill, Unit? chosen)
            => TargetsOf(state, actor, skill.Target, chosen);

        private static List<Unit> TargetsOf(BattleState state, Unit actor, Target skillTarget,
            Unit? chosen)
        {
            var foes = LivingOf(state, actor.Side == Side.Ally ? Side.Enemy : Side.Ally);
            var friends = LivingOf(state, actor.Side);

            switch (skillTarget)
            {
                case Target.Self:
                    return new List<Unit> { actor };

                case Target.EnemyAll:
                    return foes;

                case Target.EnemyOne:
                {
                    // ⭐ 指定があればそれを狙う（プレイヤーの手番）。無ければ残 HP の低い相手から
                    Unit? picked;
                    if (chosen != null && IsAlive(chosen) && chosen.Side != actor.Side)
                    {
                        picked = chosen;
                    }
                    else
                    {
                        var sorted = new List<Unit>(foes);
                        sorted.Sort((a, b) => a.Hp != b.Hp ? a.Hp - b.Hp : a.Slot - b.Slot);
                        picked = sorted.Count > 0 ? sorted[0] : null;
                    }
                    if (picked == null) return new List<Unit>();

                    // ⭐ **挑発を受けているのは行動する側。**掛けてきた相手しか狙えない。
                    // ⚠️ 全体攻撃は縛らない（全員に当たるので狙い先の意味が無い）。
                    // ⚠️ 掛け手が倒れていれば縛りは解ける（居ない相手を狙い続けない）。
                    if (actor.Status.Taunt > 0 && actor.Status.TauntBy != null)
                    {
                        foreach (var unit in foes)
                        {
                            if (unit.Key != actor.Status.TauntBy) continue;
                            actor.Status.Taunt--;
                            if (actor.Status.Taunt <= 0) actor.Status.TauntBy = null;
                            return new List<Unit> { unit };
                        }
                    }
                    return new List<Unit> { picked };
                }

                case Target.AllyDown:
                {
                    // ⚠️ 生きている側からは選べないので、全員から探す
                    Unit? down = null;
                    foreach (var unit in state.Units)
                    {
                        if (unit.Side != actor.Side || IsAlive(unit)) continue;
                        if (down == null || unit.Slot < down.Slot) down = unit;
                    }
                    return down == null ? new List<Unit>() : new List<Unit> { down };
                }

                case Target.AllyLowest:
                {
                    var sorted = new List<Unit>(friends);
                    sorted.Sort((a, b) =>
                    {
                        double ra = (double)a.Hp / a.MaxHp;
                        double rb = (double)b.Hp / b.MaxHp;
                        if (ra != rb) return ra < rb ? -1 : 1;
                        return a.Slot - b.Slot;
                    });
                    return sorted.Count > 0 ? new List<Unit> { sorted[0] } : new List<Unit>();
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(skillTarget));
            }
        }

        /// <summary>ダメージを通す。
        ///
        /// ⭐ シールドは枚数。1回の攻撃につき1枚消費して、
        /// 威力に関係なくその攻撃を完全に無効化する（100 ダメージでも 1 ダメージでも同じ1枚）。
        /// 枚数が尽きたら以降は素通し。
        /// ⭐ だから「大きな一撃」には滅法強く、「手数」には弱い。</summary>
        /// <param name="source">殴った者。⚠️ null なら誰の一撃でもない（毒など）。
        /// 特性の「与えた／受けた」はここが居るときにしか働かない。</param>
        /// <param name="reflectable">返し身がここから更に跳ね返ってよいか。
        /// ⚠️ 返した一撃では false。返し身どうしが往復し続けるのを止める唯一の止め木。</param>
        private static void DealDamage(BattleState state, Unit? source, Unit target, int amount,
            bool reflectable = true)
        {
            if (target.Status.Shield > 0)
            {
                target.Status.Shield--;
                // ⭐ 執念: 盾を「守り」から「手数の元」に変える。
                //    ⚠️ 剥がれた枚数で溜まるので、手数で殴られるほど得になる
                if (HasTrait(target, Traits.Grit)) target.Gauge += TraitGritGauge;
                state.Log.Add(new BattleEvent(BattleEventKind.Damage, target.Key,
                    amount: 0, hp: target.Hp, absorbed: amount));
                return;
            }

            // ⭐ 殴られると目を覚ます。⚠️ 眠らせた相手を殴ると自分で起こしてしまう
            if (target.Status.Sleep > 0)
            {
                target.Status.Sleep = 0;
                state.Log.Add(new BattleEvent(BattleEventKind.Woke, target.Key));
            }

            int before = target.Hp;
            target.Hp = Math.Max(0, target.Hp - amount);

            // ⭐ ガッツ: 致命傷を HP1 で耐える。⚠️ 元から1以下なら効かない（無限に粘らせない）
            if (target.Hp == 0 && target.Status.Guts > 0 && before > 1)
            {
                target.Hp = 1;
                target.Status.Guts = 0;
                state.Log.Add(new BattleEvent(BattleEventKind.GutsSaved, target.Key));
            }

            int dealt = before - target.Hp;
            state.Log.Add(new BattleEvent(BattleEventKind.Damage, target.Key,
                amount: dealt, hp: target.Hp, absorbed: 0));
            if (target.Hp == 0) state.Log.Add(new BattleEvent(BattleEventKind.Down, target.Key));

            // ⚠️ ここから下は特性だけ。持たない個体では1つも動かない
            if (source == null || ReferenceEquals(source, target) || dealt <= 0) return;

            // ⭐ 食らいつき: 与えたぶんを吸う。攻撃役が回復役の枠を1つ空ける
            if (HasTrait(source, Traits.Leech) && IsAlive(source))
            {
                int had = source.Hp;
                source.Hp = Math.Min(source.MaxHp, source.Hp + Ratio(dealt, TraitLeechPercent));
                state.Log.Add(new BattleEvent(BattleEventKind.Heal, source.Key,
                    amount: source.Hp - had, hp: source.Hp));
            }

            // ⭐ 返し身: 受けたぶんを返す。⚠️ 倒れたら返さない（働く場面は OnHurt であって OnDown ではない）
            if (reflectable && HasTrait(target, Traits.Spite) && IsAlive(target) && IsAlive(source))
            {
                DealDamage(state, target, source, Ratio(dealt, TraitSpitePercent), reflectable: false);
            }
        }

        /// <summary>効果が実際に通る率（%）。
        ///
        /// ⭐ **相手に掛けるものだけ、速い側が通しやすく速い相手には通りにくい。**
        /// これで「スピードが高い個体＝弱化役」という役割が数字の上でも成立する
        /// （速度が行動回数にしか効かないと、弱化役を作る理由が薄い）。
        ///
        /// ⚠️ 自分・味方に掛けるもの（回復・盾・ガッツ・免疫）は速度で動かさない。
        /// 誰も抵抗していないのに速さで成否が変わるのは筋が通らない。
        /// そこでは素の率がそのまま**賭け**になる（効き目が大きいぶん外れる）。
        ///
        /// ⚠️ 素の率から動かせる幅は ±<see cref="LandSwing"/> まで。
        /// 速度差だけで 0% や 100% にすると、速さが弱化の全部になってしまう。</summary>
        public static int LandChanceOf(Effect effect, Unit actor, Unit target)
        {
            // 相手が抵抗しないものは、速度でも特性でも動かさない
            if (!Skills.IsHarmful(effect)) return effect.Chance;

            // ⭐ 特性は「弱化の通しやすさ」だけに触る。狙い澄まし＝通す / 意地＝通させない
            int shift = 0;
            if (HasTrait(actor, Traits.Aim)) shift += TraitAim;
            if (HasTrait(target, Traits.Stubborn)) shift -= TraitStubborn;

            // ⚠️ 率 100 のままなら乱数を引かない（移植した技の試合が1手も変わらないように）。
            //    ⭐ 意地だけがこれを崩す — 崩さないと「必ず通る弱化」に意地が無力になり、
            //    「弱化を受ける率が下がる」という説明が画面と食い違う
            if (effect.Chance >= 100 && shift >= 0) return 100;

            int mine = SpeedOf(actor);
            int yours = SpeedOf(target);
            // 速度比を -1〜+1 に畳んでから振れ幅を掛ける
            double ratio = (double)(mine - yours) / (mine + yours);

            // ⭐ **属性の有利・不利も通る率を動かす。**
            // ⚠️ ダメージ倍率とは別枠。属性が「火力の話」だけだったのを、
            //    弱化にも接続した（属性という1つのラベルの用途を増やす）。
            double mult = ElementMultiplier(actor.Creature.Element, target.Creature.Element);
            int element = mult > 1.0 ? LandElementSwing : mult < 1.0 ? -LandElementSwing : 0;

            int moved = effect.Chance + JsRound(ratio * LandSwing) + element + shift;
            return moved < LandFloor ? LandFloor : moved > LandCeil ? LandCeil : moved;
        }

        /// <summary>速度差で動かせる幅（%ポイント）。</summary>
        public const int LandSwing = 30;

        /// <summary>属性の有利・不利で動かす幅（%ポイント）。
        ///
        /// ⚠️ **速度差（<see cref="LandSwing"/>）と足し算で重なる。**
        /// 同じ大きさにすると、有利かつ速い側が常に天井・逆が常に床になり、
        /// 通る率が「属性と速度の一致だけ」で決まってしまう。
        /// ⭐ 速度を主軸のまま残したいので、その半分から始めて `sim` で測る。</summary>
        public const int LandElementSwing = 15;
        /// <summary>⚠️ どれだけ速度で劣っても、ここまでは通る（弱化役が完全に死なないように）。</summary>
        public const int LandFloor = 25;
        /// <summary>⚠️ どれだけ速くても確実にはしない（免疫と盾の意味を残す）。</summary>
        public const int LandCeil = 95;

        /// <returns>実際に当てた発数。⭐ 「手数」の特性だけがこれを見る。
        /// ⚠️ 盾で無効化された発も1発と数える（打ち込んだことに変わりはない）。</returns>
        private static int ApplyEffect(BattleState state, Unit actor, Unit target, Effect effect,
            SkillBoost boost)
        {
            // ⭐ 免疫は弱い側の効果だけを弾く
            if (Skills.IsHarmful(effect) && target.Status.Immune > 0)
            {
                state.Log.Add(new BattleEvent(BattleEventKind.Blocked, target.Key));
                return 0;
            }

            // ⭐ ブロックは**外から受け取る回復と強化**を弾く。
            // ⚠️ 自然に溜まるゲージと自然に減る CT は止めない（止まるのは「買った分」だけ）。
            if (Skills.IsBoon(effect) && target.Status.Block > 0)
            {
                state.Log.Add(new BattleEvent(BattleEventKind.Blunted, target.Key));
                return 0;
            }

            // ⭐ 弱化は外れることがある。⚠️ 率が 100 のときは引かない
            //    （移植した技の試合が1手も変わらないようにするため）
            // ⭐ スキルレベルぶん通しやすくなる。⚠️ 素で 100 のものは 100 のまま
            //    （乱数を1度も引かない、という約束を崩さない）
            int land = LandChanceOf(effect, actor, target) + boost.ChancePoints;
            if (land > 100) land = 100;
            if (land < 100 && state.Rng.Int(0, 100) >= land)
            {
                state.Log.Add(new BattleEvent(BattleEventKind.Missed, target.Key));
                return 0;
            }

            int hits = 0;
            switch (effect.Kind)
            {
                case EffectKind.Damage:
                {
                    var actorStats = Creatures.StatsOf(actor.Creature);
                    var targetStats = Creatures.StatsOf(target.Creature);
                    int attackStat = effect.Scale == DamageScale.Atk
                        ? EffectiveStat(actorStats.Atk, actor.Status.Atk)
                        : EffectiveStat(actorStats.Def, actor.Status.Def);
                    // ⭐ 防御無視。⚠️ 0 にせず「無いもの」として扱う（式の分母は定数が残る）
                    int defenseStat = effect.Pierce
                        ? 0 : EffectiveStat(targetStats.Def, target.Status.Def);
                    double mult = ElementMultiplier(
                        actor.Creature.Element,
                        target.Creature.Element);
                    // ⭐ 威力の段位は動かさない。レベルは % で乗せるだけ
                    int hit = DamageOf(Skills.BoostedPower(effect.Power, boost),
                        attackStat, defenseStat, mult);
                    // ⭐ 多段。⚠️ 途中で倒れたら止める（死体を殴り続けない）
                    // ⚠️ **殴った側が倒れたときも止める。** 返し身が入るまで
                    //    「行動者が自分の行動中に死ぬ」経路は存在しなかった。
                    //    見ていないと、返し身で倒れた死体が2発目・3発目を打つ
                    int shots = effect.Repeat + boost.ExtraRepeat;
                    for (int shot = 0; shot < shots && IsAlive(target) && IsAlive(actor); shot++)
                    {
                        DealDamage(state, actor, target, hit);
                        hits++;
                    }
                    break;
                }

                case EffectKind.Buff:
                {
                    // ⚠️ 掛け直しは上書き。積み上げにすると青天井になる
                    int percent = Skills.BuffPercent * effect.Sign;
                    ref var mod = ref target.Status.ModOf(effect.Stat);
                    mod.Percent = percent;
                    mod.Turns = effect.Turns + boost.ExtraTurns;
                    state.Log.Add(new BattleEvent(BattleEventKind.Buff, target.Key,
                        stat: effect.Stat, percent: percent, turns: effect.Turns));
                    break;
                }

                case EffectKind.Poison:
                {
                    // ⭐ スタックする。重ねるほど1行動あたりの削りが増える
                    target.Status.Poison = new Stacking
                    {
                        Stacks = target.Status.Poison.Turns > 0
                            ? target.Status.Poison.Stacks + effect.Stacks
                            : effect.Stacks,
                        Turns = effect.Turns + boost.ExtraTurns,
                    };
                    state.Log.Add(new BattleEvent(BattleEventKind.Applied, target.Key,
                        label: $"毒×{target.Status.Poison.Stacks}", turns: effect.Turns));
                    break;
                }

                case EffectKind.Regen:
                {
                    target.Status.Regen = new Stacking
                    {
                        Stacks = target.Status.Regen.Turns > 0
                            ? target.Status.Regen.Stacks + effect.Stacks
                            : effect.Stacks,
                        Turns = effect.Turns + boost.ExtraTurns,
                    };
                    state.Log.Add(new BattleEvent(BattleEventKind.Applied, target.Key,
                        label: $"リジェネ×{target.Status.Regen.Stacks}", turns: effect.Turns));
                    break;
                }

                case EffectKind.HealRatio:
                {
                    // ⚠️ 割合は技ごとに違う（段位を使わない）
                    int amount = (int)Math.Floor(
                        (double)(target.MaxHp * (effect.Percent + boost.ExtraPercent)) / 100);
                    if (amount < 1) amount = 1;
                    int before = target.Hp;
                    target.Hp = Math.Min(target.MaxHp, target.Hp + amount);
                    state.Log.Add(new BattleEvent(BattleEventKind.Heal, target.Key,
                        amount: target.Hp - before, hp: target.Hp));
                    break;
                }

                case EffectKind.Shield:
                {
                    // ⚠️ 重ね掛けは上書き。積むと実質無敵になる
                    target.Status.Shield = effect.Count + boost.ExtraCount;
                    state.Log.Add(new BattleEvent(BattleEventKind.Shield, target.Key, amount: effect.Count));
                    break;
                }

                case EffectKind.Stun:
                {
                    // ⚠️ スタンだけは**足す**（他は上書き）。重ねると手番が延々飛ぶので、
                    //    ⭐ 1回の技で入る量を上限にして青天井を止める
                    int stun = target.Status.Stun + effect.Turns + boost.ExtraTurns;
                    int stunCap = effect.Turns + boost.ExtraTurns + StunStackMax;
                    target.Status.Stun = stun > stunCap ? stunCap : stun;
                    state.Log.Add(new BattleEvent(BattleEventKind.Stun, target.Key, turns: effect.Turns));
                    break;
                }

                case EffectKind.Ct:
                {
                    // ⚠️ 枠1は触らない。「必ず打てる札」に CT を乗せると手が無くなる
                    for (int i = 1; i < target.Cooldowns.Length; i++)
                    {
                        int delta = effect.Delta + (effect.Delta < 0 ? -boost.ExtraAmount : boost.ExtraAmount);
                        target.Cooldowns[i] = Math.Max(0, target.Cooldowns[i] + delta);
                    }
                    state.Log.Add(new BattleEvent(BattleEventKind.Ct, target.Key, delta: effect.Delta));
                    break;
                }

                case EffectKind.Taunt:
                {
                    // ⭐ **相手に付ける弱化。**掛けた本人しか狙えなくする。
                    // ⚠️ 以前は味方に付けて「単体攻撃を引き受ける」形だった。
                    //    引き受け役は残しても意味が重なるので、狙い先の固定に一本化した。
                    target.Status.Taunt = effect.Hits + boost.ExtraAmount;
                    target.Status.TauntBy = actor.Key;
                    state.Log.Add(new BattleEvent(BattleEventKind.Taunt, target.Key, hits: effect.Hits));
                    break;
                }

                case EffectKind.Guts:
                {
                    target.Status.Guts = effect.Turns + boost.ExtraTurns;
                    state.Log.Add(new BattleEvent(BattleEventKind.Guts, target.Key));
                    break;
                }

                case EffectKind.Gauge:
                {
                    // ⭐ 満タンに対する割合で動かす。⚠️ 減らす側は超過分ごと削る
                    int move = GaugeMax * (effect.Percent
                        + (effect.Percent < 0 ? -boost.ExtraAmount : boost.ExtraAmount)) / 100;
                    int before = target.Gauge;
                    target.Gauge = Math.Max(0, target.Gauge + move);
                    state.Log.Add(new BattleEvent(BattleEventKind.Gauge, target.Key,
                        amount: target.Gauge - before, percent: effect.Percent));
                    break;
                }

                case EffectKind.Sleep:
                {
                    // ⚠️ スタンと同じく足す（上限つき）。⭐ 違いは「殴ると起きる」ことだけ
                    int sleep = target.Status.Sleep + effect.Turns + boost.ExtraTurns;
                    int sleepCap = effect.Turns + boost.ExtraTurns + StunStackMax;
                    target.Status.Sleep = sleep > sleepCap ? sleepCap : sleep;
                    state.Log.Add(new BattleEvent(BattleEventKind.Sleep, target.Key, turns: effect.Turns));
                    break;
                }

                case EffectKind.Block:
                {
                    target.Status.Block = effect.Turns + boost.ExtraTurns;
                    state.Log.Add(new BattleEvent(BattleEventKind.Block, target.Key, turns: effect.Turns));
                    break;
                }

                case EffectKind.Dispel:
                {
                    int gone = StripBoons(target, effect.Count + boost.ExtraCount, null);
                    state.Log.Add(new BattleEvent(BattleEventKind.Dispelled, target.Key, amount: gone));
                    break;
                }

                case EffectKind.Steal:
                {
                    // ⭐ 消すのではなく自分へ移す。⚠️ 移せないもの（毒などの弱化）は触らない
                    int moved = StripBoons(target, effect.Count + boost.ExtraCount, actor);
                    state.Log.Add(new BattleEvent(BattleEventKind.Dispelled, target.Key,
                        amount: moved, label: "奪"));
                    break;
                }

                case EffectKind.Revive:
                {
                    // ⚠️ 倒れていない相手には何も起きない
                    if (IsAlive(target)) break;
                    int back = Math.Max(1, target.MaxHp * (effect.Percent + boost.ExtraAmount) / 100);
                    target.Hp = Math.Min(target.MaxHp, back);
                    // ⭐ 立ち上がるときは強化も弱化も無い状態から
                    target.Status = new UnitStatus();
                    target.Gauge = 0;
                    state.Log.Add(new BattleEvent(BattleEventKind.Revived, target.Key,
                        amount: target.Hp, hp: target.Hp));
                    break;
                }

                case EffectKind.Immune:
                {
                    target.Status.Immune = effect.Turns + boost.ExtraTurns;
                    state.Log.Add(new BattleEvent(BattleEventKind.Immune, target.Key));
                    break;
                }
            }
            return hits;
        }

        /// <summary>その者に行動させる。ゲージを引き、CT を進める。</summary>
        public static void PerformAction(BattleState state, Unit actor, int slot, Unit? chosen = null)
        {
            if (!IsUsable(actor, slot))
                throw new InvalidOperationException($"{actor.Key} は今その行動を選べない");

            var skill = ActionSkill(actor, slot);
            state.Log.Add(new BattleEvent(BattleEventKind.Act, actor.Key, label: skill.Name));

            // ⭐ その枠のスキルレベルぶんの上乗せ。⚠️ Lv1 なら全部 0 ＝ 1ビットも変わらない
            var boost = Creatures.SkillBoostOf(actor.Creature, slot);

            // ⭐ 「手数」が見るのは**1体に何発当てたか**。
            // ⚠️ 対象ぶん足し込んではいけない。全体攻撃は3体に当たるので合計が3になり、
            //    多段でもないのに待ちが縮む。ハネルの枠1（全体攻撃・CT 0）だと
            //    毎行動 CT が3ずつ減って別のゲームになる
            int hits = 0;
            foreach (var target in TargetsOf(state, actor, skill, chosen))
            {
                // ⚠️ 返し身で倒れていたら、残りの対象へは進まない
                if (!IsAlive(actor)) break;
                int onTarget = 0;
                foreach (var effect in skill.Effects)
                {
                    // ⚠️ 返し身で行動者が倒れたら、同じ技の残りの効果も撃たない
                    //    （打ち崩しの「ダメージ→防御DOWN」の後半を死体が撃っていた）
                    if (!IsAlive(actor)) break;
                    onTarget += ApplyEffect(state, actor, target, effect, boost);
                }
                if (onTarget > hits) hits = onTarget;
            }

            // ⚠️ CT は「本人の行動回数」で減る。何をしたかに関わらず1回ぶん進む
            for (int i = 0; i < actor.Cooldowns.Length; i++)
            {
                actor.Cooldowns[i] = Math.Max(0, actor.Cooldowns[i] - 1);
            }
            // ⭐ CT は技ではなく枠の性質。枠1は常に 0
            actor.Cooldowns[slot] = Skills.EffectiveCt(slot, skill, boost);

            // ⭐ 手数: 当てた発数だけ待ちが縮む。⚠️ **いま置いた CT の後**に効かせる —
            //    前だと使った枠がすぐ上書きされて、縮んだぶんが消える。
            //    ⚠️ 1発（単発の技）では何も起きない。特性と技が噛み合って初めて働く
            if (hits > 1 && HasTrait(actor, Traits.Flurry))
            {
                int cut = hits - 1;
                for (int i = 0; i < actor.Cooldowns.Length; i++)
                {
                    actor.Cooldowns[i] = Math.Max(0, actor.Cooldowns[i] - cut);
                }
            }

            actor.Gauge -= GaugeMax;
            state.Actions++;
            state.Result = DecideOutcome(state);
        }

        /// <summary>画面で使う小物。</summary>
        public static Palette UnitPalette(Unit unit) => Creatures.PaletteOf(unit.Creature);

        /// <summary>画面に出す、今かかっている状態の一覧。⚠️ ここが唯一の表示用まとめ。</summary>
        public static List<string> ActiveStatuses(Unit unit)
        {
            var s = unit.Status;
            var output = new List<string>();
            if (s.Atk.Turns > 0) output.Add($"攻撃{Sign(s.Atk.Percent)}%");
            if (s.Def.Turns > 0) output.Add($"防御{Sign(s.Def.Percent)}%");
            if (s.Spd.Turns > 0) output.Add($"速度{Sign(s.Spd.Percent)}%");
            if (s.Poison.Turns > 0) output.Add($"毒×{s.Poison.Stacks}({s.Poison.Turns})");
            if (s.Regen.Turns > 0) output.Add($"リジェネ×{s.Regen.Stacks}({s.Regen.Turns})");
            // ⭐ 枚数。1回の攻撃につき1枚
            if (s.Shield > 0) output.Add($"盾{s.Shield}枚");
            if (s.Stun > 0) output.Add($"スタン{s.Stun}");
            if (s.Taunt > 0) output.Add($"挑発{s.Taunt}");
            if (s.Guts > 0) output.Add($"ガッツ{s.Guts}");
            if (s.Immune > 0) output.Add($"免疫{s.Immune}");
            if (s.Sleep > 0) output.Add($"睡眠{s.Sleep}");
            if (s.Block > 0) output.Add($"ブロック{s.Block}");
            return output;
        }

        private static string Sign(int n) => n > 0 ? $"+{n}" : n.ToString();
    }
}
