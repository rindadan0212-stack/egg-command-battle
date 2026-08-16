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
        public int Guts;
        public int Immune;

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

        public BattleState(List<Unit> units)
        {
            Units = units;
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

        public static BattleState CreateBattle(IReadOnlyList<Creature> allies, IReadOnlyList<Creature> enemies)
        {
            double scale = LoneScale(allies.Count, enemies.Count);
            var units = new List<Unit>(allies.Count + enemies.Count);
            for (int i = 0; i < allies.Count; i++) units.Add(MakeUnit(allies[i], Side.Ally, i));
            for (int i = 0; i < enemies.Count; i++)
                units.Add(MakeUnit(enemies[i], Side.Enemy, i, LoneHp(scale), LoneTempo(scale)));
            return new BattleState(units);
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
                return best;
            }
            return null;
        }

        private static List<Unit> TargetsOf(BattleState state, Unit actor, Skill skill, Unit? chosen)
        {
            var foes = LivingOf(state, actor.Side == Side.Ally ? Side.Enemy : Side.Ally);
            var friends = LivingOf(state, actor.Side);

            switch (skill.Target)
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

                    // ⭐ 挑発している者がいれば、そちらへ逸らす（「壁」の実体）。
                    // ⚠️ 全体攻撃は逸らさない（全員に当たるので引き受ける意味が無い）
                    var guards = new List<Unit>();
                    foreach (var unit in foes)
                    {
                        if (unit.Status.Taunt > 0 && !ReferenceEquals(unit, picked)) guards.Add(unit);
                    }
                    guards.Sort((a, b) => a.Status.Taunt != b.Status.Taunt
                        ? b.Status.Taunt - a.Status.Taunt
                        : a.Slot - b.Slot);
                    if (guards.Count > 0)
                    {
                        guards[0].Status.Taunt--;
                        return new List<Unit> { guards[0] };
                    }
                    return new List<Unit> { picked };
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
                    throw new ArgumentOutOfRangeException(nameof(skill));
            }
        }

        /// <summary>ダメージを通す。
        ///
        /// ⭐ シールドは枚数。1回の攻撃につき1枚消費して、
        /// 威力に関係なくその攻撃を完全に無効化する（100 ダメージでも 1 ダメージでも同じ1枚）。
        /// 枚数が尽きたら以降は素通し。
        /// ⭐ だから「大きな一撃」には滅法強く、「手数」には弱い。</summary>
        private static void DealDamage(BattleState state, Unit target, int amount)
        {
            if (target.Status.Shield > 0)
            {
                target.Status.Shield--;
                state.Log.Add(new BattleEvent(BattleEventKind.Damage, target.Key,
                    amount: 0, hp: target.Hp, absorbed: amount));
                return;
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

            state.Log.Add(new BattleEvent(BattleEventKind.Damage, target.Key,
                amount: before - target.Hp, hp: target.Hp, absorbed: 0));
            if (target.Hp == 0) state.Log.Add(new BattleEvent(BattleEventKind.Down, target.Key));
        }

        private static void ApplyEffect(BattleState state, Unit actor, Unit target, Effect effect)
        {
            // ⭐ 免疫は弱い側の効果だけを弾く
            if (Skills.IsHarmful(effect) && target.Status.Immune > 0)
            {
                state.Log.Add(new BattleEvent(BattleEventKind.Blocked, target.Key));
                return;
            }

            switch (effect.Kind)
            {
                case EffectKind.Damage:
                {
                    var actorStats = Creatures.StatsOf(actor.Creature);
                    var targetStats = Creatures.StatsOf(target.Creature);
                    int attackStat = effect.Scale == DamageScale.Atk
                        ? EffectiveStat(actorStats.Atk, actor.Status.Atk)
                        : EffectiveStat(actorStats.Def, actor.Status.Def);
                    int defenseStat = EffectiveStat(targetStats.Def, target.Status.Def);
                    double mult = ElementMultiplier(
                        Creatures.SpeciesOf(actor.Creature).Element,
                        Creatures.SpeciesOf(target.Creature).Element);
                    DealDamage(state, target,
                        DamageOf(Skills.DamagePowerOf(effect.Power), attackStat, defenseStat, mult));
                    break;
                }

                case EffectKind.Buff:
                {
                    // ⚠️ 掛け直しは上書き。積み上げにすると青天井になる
                    int percent = Skills.BuffPercent * effect.Sign;
                    ref var mod = ref target.Status.ModOf(effect.Stat);
                    mod.Percent = percent;
                    mod.Turns = effect.Turns;
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
                        Turns = effect.Turns,
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
                        Turns = effect.Turns,
                    };
                    state.Log.Add(new BattleEvent(BattleEventKind.Applied, target.Key,
                        label: $"リジェネ×{target.Status.Regen.Stacks}", turns: effect.Turns));
                    break;
                }

                case EffectKind.HealRatio:
                {
                    // ⚠️ 割合は技ごとに違う（段位を使わない）
                    int amount = (int)Math.Floor((double)(target.MaxHp * effect.Percent) / 100);
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
                    target.Status.Shield = effect.Count;
                    state.Log.Add(new BattleEvent(BattleEventKind.Shield, target.Key, amount: effect.Count));
                    break;
                }

                case EffectKind.Stun:
                {
                    target.Status.Stun += effect.Turns;
                    state.Log.Add(new BattleEvent(BattleEventKind.Stun, target.Key, turns: effect.Turns));
                    break;
                }

                case EffectKind.Ct:
                {
                    // ⚠️ 枠1は触らない。「必ず打てる札」に CT を乗せると手が無くなる
                    for (int i = 1; i < target.Cooldowns.Length; i++)
                    {
                        target.Cooldowns[i] = Math.Max(0, target.Cooldowns[i] + effect.Delta);
                    }
                    state.Log.Add(new BattleEvent(BattleEventKind.Ct, target.Key, delta: effect.Delta));
                    break;
                }

                case EffectKind.Taunt:
                {
                    target.Status.Taunt = effect.Hits;
                    state.Log.Add(new BattleEvent(BattleEventKind.Taunt, target.Key, hits: effect.Hits));
                    break;
                }

                case EffectKind.Guts:
                {
                    target.Status.Guts = effect.Turns;
                    state.Log.Add(new BattleEvent(BattleEventKind.Guts, target.Key));
                    break;
                }

                case EffectKind.Immune:
                {
                    target.Status.Immune = effect.Turns;
                    state.Log.Add(new BattleEvent(BattleEventKind.Immune, target.Key));
                    break;
                }
            }
        }

        /// <summary>その者に行動させる。ゲージを引き、CT を進める。</summary>
        public static void PerformAction(BattleState state, Unit actor, int slot, Unit? chosen = null)
        {
            if (!IsUsable(actor, slot))
                throw new InvalidOperationException($"{actor.Key} は今その行動を選べない");

            var skill = ActionSkill(actor, slot);
            state.Log.Add(new BattleEvent(BattleEventKind.Act, actor.Key, label: skill.Name));

            foreach (var target in TargetsOf(state, actor, skill, chosen))
            {
                foreach (var effect in skill.Effects)
                {
                    ApplyEffect(state, actor, target, effect);
                }
            }

            // ⚠️ CT は「本人の行動回数」で減る。何をしたかに関わらず1回ぶん進む
            for (int i = 0; i < actor.Cooldowns.Length; i++)
            {
                actor.Cooldowns[i] = Math.Max(0, actor.Cooldowns[i] - 1);
            }
            // ⭐ CT は技ではなく枠の性質。枠1は常に 0
            actor.Cooldowns[slot] = Skills.EffectiveCt(slot, skill);

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
            return output;
        }

        private static string Sign(int n) => n > 0 ? $"+{n}" : n.ToString();
    }
}
