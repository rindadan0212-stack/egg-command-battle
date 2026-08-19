#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>放置の状態。ホームで編成が右へ進み続けている。</summary>
    public sealed class IdleRun
    {
        /// <summary>溜まっている EXP。⭐ これを BOX で Lv に変える。
        /// ⚠️ 2026-08-19 まで「素材」と呼んでいたもの。古い保存の <c>Materials</c> 欄は
        /// 読むときだけ拾う（<c>Snapshot</c>）。</summary>
        public int Exp;
        /// <summary>最後に清算した時刻。⚠️ 経過は「今 − ここ」で出す。
        /// 残り時間で持つと、見ていない間の時間が進まない（孵化器と同じ約束）。</summary>
        public long LastUnix;
        /// <summary>倒した数。⭐ 進んだ距離そのもの。背景を流す量に使う。</summary>
        public int Defeated;

        /// <summary>いまの敵の残り。⚠️ 0 以下なら次の敵へ。</summary>
        public double EnemyHp;
        /// <summary>次の敵が現れてから、まだ手が出ていない秒。
        /// ⭐ 0 になるまで削らない（作者の指示 2026-08-19「出現してから1拍おいてから」）。</summary>
        public double Spawn;
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
        /// ⚠️ **実値と直に比べているので、桁を上げたら一緒に動かす**（2026-08-19）。
        /// 置き去りにすると、遊び始めの編成が敵を瞬殺して誰も倒れなくなる。
        public const double EnemyHp = 420.0 * Stats.Scale;

        /// <summary>敵が一撃を放つまでの秒。⭐ 遊び始めはぎりぎり間に合わないことがある。</summary>
        public const double ChargeSeconds = 4.0;

        /// <summary>敵が現れてから手が出るまでの間。⭐ **1拍**（作者の指示 2026-08-19）。
        /// ⚠️ 前は現れた刻みでいきなり削れていて、出てきたのが見えなかった。
        /// ⚠️ 倒すのに要る時間が延びるので、<see cref="ExpPerKill"/> で埋め合わせてある。</summary>
        public const double SpawnSeconds = 0.5;

        /// <summary>倒れてから起き上がるまでの秒。</summary>
        public const int ReviveSeconds = 20;

        /// <summary>1体倒すごとに入る EXP。
        /// ⚠️ **1レベルの値段が「到達 Lv」で決まるようになったので上げた**（2026-08-19）。
        /// 1 のままだと、遊び始めの編成（Lv35 前後）でも1体ぶん育てるのに 45分かかった。
        /// ⭐ **5 で、前と同じ「10分で1体ぶん」に戻る**（実測: 10分で 1,000 EXP、
        /// 遊び始めの Lv35 から上限の +20 までがちょうど 930 EXP）。</summary>
        public const int ExpPerKill = 5;

        // ⚠️ **1レベルの値段はここに無い。**<see cref="Levels.ExpToNext"/> が唯一の出所
        //    （レベルが高いほど高くなるので、定数では表せない）。

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
            // ⚠️ **1体目にも1拍を置く。**初回だけ置いていなかったので、
            //    始めた瞬間の敵だけがいきなり削られていた（2026-08-19 の監査）。
            if (run.LastUnix <= 0)
            {
                run.LastUnix = nowUnix;
                run.EnemyHp = EnemyHp;
                run.Spawn = SpawnSeconds;
                return 0;
            }
            // ⚠️ **時計が巻き戻ったら、基準を合わせ直す。**そのまま返していた頃は
            //    `LastUnix` が未来のままになり、追いつくまで放置が**永久に止まって**いた
            //    （端末の時刻を戻す・タイムゾーンをまたぐ、で起きる）。
            if (nowUnix <= run.LastUnix)
            {
                run.LastUnix = nowUnix;
                return 0;
            }

            long elapsed = nowUnix - run.LastUnix;
            if (elapsed > CatchUpMax) elapsed = CatchUpMax;
            run.LastUnix = nowUnix;

            if (run.EnemyHp <= 0.0) { run.EnemyHp = EnemyHp; run.Spawn = SpawnSeconds; }

            int gained = 0;
            // ⚠️ 復活の判定に使う時計は、刻みごとに進める。
            //    now で判定すると「まだ寝ているはずの者」が最初から立ってしまう
            long clock = nowUnix - elapsed;
            for (double t = 0.0; t < elapsed; t += Step)
            {
                clock = nowUnix - elapsed + (long)t;
                double power = PowerOf(run, party, clock);

                // ⭐ 現れてすぐには手が出ない。⚠️ 溜めもこの間は進まない（出てきていないので）
                if (run.Spawn > 0.0)
                {
                    run.Spawn -= Step;
                    continue;
                }

                if (power > 0.0)
                {
                    run.EnemyHp -= power * Step;
                    if (run.EnemyHp <= 0.0)
                    {
                        // 倒した。⭐ 素材が入り、次の敵へ進む。溜めもここで切れる
                        gained += ExpPerKill;
                        run.Defeated++;
                        run.EnemyHp = EnemyHp;
                        run.Charge = 0.0;
                        run.Spawn = SpawnSeconds;
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

            run.Exp += gained;
            return gained;
        }

        /// <summary>EXP で Lv を1つ上げる。⭐ **1回で1レベル**。
        /// ⚠️ 一気に上限まで入れると、上げ止めどころを選べない。
        /// どこで止めるかは持ち主が決める。
        /// ⚠️ 値段は個体の**いまの Lv** で決まる（<see cref="Levels.ExpToNext"/>）。</summary>
        /// <returns>上がったなら 1、EXP か上限が足りなければ 0。</returns>
        public static int Spend(IdleRun run, Creature creature)
        {
            if (creature.Earned >= Levels.GrowMax) return 0;
            // ⚠️ 値段はその個体の**いまの Lv**（素質の合計 ＋ 育てた分）で決まる
            int cost = Levels.ExpToNext(creature);
            if (cost <= 0 || run.Exp < cost) return 0;

            int gained = Creatures.Grow(creature, 1);
            if (gained <= 0) return 0;
            run.Exp -= cost;
            return gained;
        }
    }
}
