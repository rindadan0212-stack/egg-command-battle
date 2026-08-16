#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>放置の状態。ホームで編成が右へ進み続けている。</summary>
    public sealed class IdleRun
    {
        /// <summary>溜まっている素材。⭐ これを BOX で Lv に変える。</summary>
        public int Materials;
        /// <summary>最後に清算した時刻。⚠️ 経過は「今 − ここ」で出す。
        /// 残り時間で持つと、見ていない間の時間が進まない（孵化器と同じ約束）。</summary>
        public long LastUnix;
        /// <summary>倒した数。⭐ 進んだ距離そのもの。背景を流す量に使う。</summary>
        public int Defeated;

        /// <summary>いまの敵の残り。⚠️ 0 以下なら次の敵へ。</summary>
        public double EnemyHp;
        /// <summary>敵が溜めた秒。⚠️ <see cref="Idle.ChargeSeconds"/> に届くと一撃が出る。</summary>
        public double Charge;

        /// <summary>倒れている者と、その復活時刻。</summary>
        public readonly Dictionary<string, long> DownUntil = new Dictionary<string, long>();
    }

    /// <summary>ホームの放置。⭐ **編成が敵を倒しきる速さ 対 敵の溜めの速さ**。
    ///
    /// 強い編成は殴られる前に倒しきるので誰も倒れず、素材が最速で溜まる。
    /// 弱いと溜めが先に届いて1体が倒れ、生きている者だけで戦うので遅くなる。
    /// ⭐ これで「編成の合計が速さを決める」が、数値の掛け算ではなく**間に合うかどうか**で出る。
    ///
    /// ⚠️ ここは時計を持たない。経過秒は呼び側が渡す（孵化器と同じ）。
    /// ⚠️ 乱数も使わない。同じ編成と同じ経過からは必ず同じ結果になる。
    /// </summary>
    public static class Idle
    {
        /// <summary>刻み。⚠️ 大きくすると溜めと討伐の前後が入れ替わって結果が変わる。</summary>
        public const double Step = 0.25;

        /// <summary>敵1体の硬さ。⭐ 遊び始めの編成でおよそ3秒。
        /// ⚠️ 3秒ごとに何か起きる速さにしてある。長いと見ていて退屈になる。
        /// ⚠️ 700 で試したら**遊び始めの編成が毎回倒れた**（5分で +14、狙いは +95）。
        /// 検査で使っていた編成が実物より強すぎたのが原因。
        /// 実物の遊び始めは合計およそ 140 なので、140 × 3秒 = 420 に直してある。
        /// ⭐ 溜め4秒より短く倒せるので、遊び始めでも誰も倒れない。</summary>
        public const double EnemyHp = 420.0;

        /// <summary>敵が一撃を放つまでの秒。⭐ 遊び始めはぎりぎり間に合わないことがある。</summary>
        public const double ChargeSeconds = 4.0;

        /// <summary>倒れてから起き上がるまでの秒。</summary>
        public const int ReviveSeconds = 20;

        /// <summary>1体倒すごとに入る素材。</summary>
        public const int MaterialPerKill = 1;

        /// <summary>Lv を1上げるのに要る素材。
        /// ⭐ 素材10で1Lv、3秒に1素材なので、10分でおよそ 200素材 = 20Lv = 1体ぶん。</summary>
        public const int MaterialPerLevel = 10;

        /// <summary>まとめて清算できる上限（秒）。⚠️ 何日ぶんも一度に流し込まない。</summary>
        public const long CatchUpMax = 12 * 60 * 60;

        /// <summary>いま戦えている者の合計。⭐ 攻撃と速さの和。
        /// ⚠️ HP と防御は入れない — それは「倒れにくさ」の側の話。</summary>
        public static double PowerOf(IdleRun run, IReadOnlyList<Creature> party, long nowUnix)
        {
            double power = 0.0;
            foreach (var creature in party)
            {
                if (IsDown(run, creature, nowUnix)) continue;
                var stats = Creatures.StatsOf(creature);
                power += stats.Atk + stats.Spd;
            }
            return power;
        }

        public static bool IsDown(IdleRun run, Creature creature, long nowUnix)
        {
            long until;
            return run.DownUntil.TryGetValue(creature.Id, out until) && nowUnix < until;
        }

        /// <summary>敵を1体倒すのにかかる秒。⭐ 画面が「あと何秒か」を出すのに使う。
        /// ⚠️ 誰も立っていなければ無限（0 で割らない）。</summary>
        public static double SecondsToKill(double power) =>
            power <= 0.0 ? double.PositiveInfinity : EnemyHp / power;

        /// <summary>経過ぶんを進める。⭐ 唯一の出所。画面はここが返した数を描くだけ。</summary>
        /// <returns>この清算で増えた素材。</returns>
        public static int Advance(IdleRun run, IReadOnlyList<Creature> party, long nowUnix)
        {
            if (run.LastUnix <= 0) { run.LastUnix = nowUnix; run.EnemyHp = EnemyHp; return 0; }
            if (nowUnix <= run.LastUnix) return 0;

            long elapsed = nowUnix - run.LastUnix;
            if (elapsed > CatchUpMax) elapsed = CatchUpMax;
            run.LastUnix = nowUnix;

            if (run.EnemyHp <= 0.0) run.EnemyHp = EnemyHp;

            int gained = 0;
            // ⚠️ 復活の判定に使う時計は、刻みごとに進める。
            //    now で判定すると「まだ寝ているはずの者」が最初から立ってしまう
            long clock = nowUnix - elapsed;
            for (double t = 0.0; t < elapsed; t += Step)
            {
                clock = nowUnix - elapsed + (long)t;
                double power = PowerOf(run, party, clock);

                if (power > 0.0)
                {
                    run.EnemyHp -= power * Step;
                    if (run.EnemyHp <= 0.0)
                    {
                        // 倒した。⭐ 素材が入り、次の敵へ進む。溜めもここで切れる
                        gained += MaterialPerKill;
                        run.Defeated++;
                        run.EnemyHp = EnemyHp;
                        run.Charge = 0.0;
                        continue;
                    }
                    run.Charge += Step;
                }

                if (run.Charge < ChargeSeconds) continue;

                // 溜めが届いた。⚠️ 立っている者のうち**防御が最も低い者**が倒れる
                run.Charge = 0.0;
                Creature? target = null;
                foreach (var creature in party)
                {
                    if (IsDown(run, creature, clock)) continue;
                    if (target == null ||
                        Creatures.StatsOf(creature).Def < Creatures.StatsOf(target).Def)
                    {
                        target = creature;
                    }
                }
                if (target != null) run.DownUntil[target.Id] = clock + ReviveSeconds;
            }

            run.Materials += gained;
            return gained;
        }

        /// <summary>素材で Lv を上げる。⭐ 1回で、足りるぶんだけ一気に上限まで。
        /// ⚠️ 1回1Lv にすると20回押すことになる。作業を増やさない。</summary>
        /// <returns>実際に上がった Lv。</returns>
        public static int Spend(IdleRun run, Creature creature)
        {
            int room = Levels.GrowMax - creature.Earned;
            if (room <= 0) return 0;

            int affordable = run.Materials / MaterialPerLevel;
            int steps = room < affordable ? room : affordable;
            if (steps <= 0) return 0;

            int gained = Creatures.Grow(creature, steps);
            run.Materials -= gained * MaterialPerLevel;
            return gained;
        }
    }
}
