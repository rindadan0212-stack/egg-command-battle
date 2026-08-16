#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    public struct Point
    {
        public double X;
        public double Y;

        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    public enum FieldSide
    {
        Left,
        Right,
    }

    public enum StealOutcome
    {
        Success,
        Blocked,
        Stalled,
    }

    public sealed class StealField
    {
        public readonly double Height;
        /// <summary>親がどちら側に寄っているか。空いているのは反対側。</summary>
        public readonly FieldSide Side;
        /// <summary>空いている隙間の範囲（x）。</summary>
        public readonly double GapFrom;
        public readonly double GapTo;
        /// <summary>親が塞ぐ帯。</summary>
        public readonly double BandTop;
        public readonly double BandBottom;
        public readonly Point Egg;
        public readonly Point Start;

        public StealField(double height, FieldSide side, double gapFrom, double gapTo,
            double bandTop, double bandBottom, Point egg, Point start)
        {
            Height = height;
            Side = side;
            GapFrom = gapFrom;
            GapTo = gapTo;
            BandTop = bandTop;
            BandBottom = bandBottom;
            Egg = egg;
            Start = start;
        }
    }

    public sealed class StealRun
    {
        public readonly StealOutcome Outcome;
        /// <summary>通った軌跡。画面がこれをなぞって描く。</summary>
        public readonly List<Point> Path;
        public readonly double Traveled;

        public StealRun(StealOutcome outcome, List<Point> path, double traveled)
        {
            Outcome = outcome;
            Path = path;
            Traveled = traveled;
        }
    }

    public struct Span
    {
        public double From;
        public double To;
    }

    /// <summary>卵強奪の発射フェーズ。
    ///
    /// 縦長のフィールド。一番上に卵。その手前に親が左右どちらかへ寄って立ちはだかる。
    /// 一番下の自分のモンスターを1回だけ引っ張って飛ばす。
    /// 卵に届けば強奪成功。親に当たるか失速したら戦闘へ。
    ///
    /// ⭐ 飛距離は編成のスピード合計。ここが設計の芯:
    /// 強奪を狙ってスピードに寄せるほど、失敗したときの戦闘で編成が偏って苦しくなる。
    /// 同じ資源（編成）が2つの軸に引っ張られる。
    ///
    /// ⚠️ 乱数を使わない。角度はプレイヤーの入力、それ以外は完全に決まる。
    /// 親がどちらへ寄るかだけは巣ごとの乱数で決める（挑むたびに変わる）。
    /// </summary>
    public static class Steal
    {
        /// ⚠️ 画面の横幅と同じにしない。道が画面いっぱいだと、
        /// 目盛りを置く場所が道の上しか無くなり、線が盤を横切る。
        /// 160 から 1/3 狭めて 107。余った左右が目盛りの置き場になる。
        public const double FieldWidth = 107;

        /// <summary>段階ごとの奥行き。
        ///
        /// ⭐ ここが「速度を積む意味」の本体。奥が深いほど、卵まで届かせるのに距離が要る。
        ///
        /// ⚠️ 最初は「隙間の幅と寄り」だけで難しさを作ろうとしたが、
        /// それだと必要な距離が段階で変わらないので速度投資が報われず、
        /// 代わりに角度の幅が 1〜2度まで狭まって精密さの勝負になってしまった（走査で発覚）。
        /// 面白さの芯は編成の選択であって狙いの精度ではないので、
        /// 狙いは寛容にして、距離で分ける。</summary>
        public static double DepthForTier(int tier)
        {
            var table = new double[] { 190, 240, 290, 340, 390 };
            int index = tier - 1;
            if (index < 0) index = 0;
            if (index > table.Length - 1) index = table.Length - 1;
            return table[index];
        }

        /// <summary>スピード合計1につき飛べる距離。
        /// ⚠️ 値は `npm run sim -- --steal` の走査で決めた。</summary>
        public const double SpeedToDistance = 3;

        /// <summary>進みの刻み。⚠️ 整数で刻んで決定論を保つ。</summary>
        private const double Step = 1;

        public const double EggRadius = 13;
        public const double RunnerRadius = 7;

        /// <summary>親が塞ぐ帯の厚み。位置は奥行きに合わせて動く。
        /// ⚠️ 卵との縦の余裕が要る。帯を卵に近づけすぎると、
        /// 隙間を抜けた後に横へ寄せきれず、どんな飛距離でも不能になる（走査で発覚）。</summary>
        private const double BandThickness = 30;

        /// <summary>隙間の幅。⚠️ 狙いは寛容にする。難しさは距離で作る。
        ///
        /// ⭐ 74 → 90 に広げた根拠（--steal の走査）:
        /// 段ごとに要るスピード合計はどちらでも変わらない（59 / 75 / 92 / 109 / 125）。
        /// 変わるのは境目の鋭さだけで、74 では「届くが幅 1°」という帯が
        /// 各段 11〜18 スピードぶん続き、そこに落ちた編成は手先を測られる。
        /// 90 にするとその帯が 0〜10 に縮み、届くマスはすべて幅 2°以上になる。
        /// ⚠️ さらに 106 まで広げると帯は消えるが、狙いが完全に無意味になる。</summary>
        /// ⚠️ 90 のときは塞ぐ幅が 92 もあり、親の絵1体では埋まらなかった。
        /// 絵を並べて埋めると「増殖している」ように見えるので、**塞ぐ幅のほうを狭めた**。
        public const double GapWidth = FieldWidth - ParentWidth;

        /// <summary>親が塞ぐ幅。⭐ **絵1体ぶん**。
        /// ⚠️ ここを広げると絵1体では埋まらず、並べて誤魔化すことになる。
        /// 見た目と当たり判定を一致させるための上限でもある。</summary>
        public const double ParentWidth = 56;

        /// <summary>親の寄り具合（中央からのずれ）。
        /// ⭐ 隙間が片方の壁に届くように寄せる ＝ 親は反対側の端で <see cref="ParentWidth"/> だけ塞ぐ。
        /// ⚠️ 手で決めた数を置かない。塞ぐ幅から出す（食い違いようがない）。</summary>
        public const double Lean = ParentWidth + GapWidth / 2 - FieldWidth / 2;

        /// <summary>⚠️ 1マス幅の切れ端を返さない。
        /// 隙間が壁に接すると反対側に幅 1 の帯が残り、当たり判定には効かないのに
        /// 画面には線が出る。見た目が「何かある」と言っているのに実体が無いのは嘘。</summary>
        private const double MinSpan = 2;

        private static double JsRound(double value) => Math.Floor(value + 0.5);

        public static StealField MakeField(int tier, FieldSide side)
        {
            double height = DepthForTier(tier);
            // 親が右へ寄る＝隙間は左寄り
            double center = side == FieldSide.Right ? FieldWidth / 2 - Lean : FieldWidth / 2 + Lean;
            double bandTop = JsRound(height * 0.36);
            return new StealField(
                height,
                side,
                Math.Max(0, center - GapWidth / 2),
                Math.Min(FieldWidth, center + GapWidth / 2),
                bandTop,
                bandTop + BandThickness,
                new Point(FieldWidth / 2, 26),
                new Point(FieldWidth / 2, height - 14));
        }

        /// <summary>飛べる距離。⭐ 編成のスピード合計から決まる。</summary>
        public static double DistanceFor(IReadOnlyList<Creature> party)
        {
            int sum = 0;
            foreach (var creature in party) sum += Creatures.StatsOf(creature).Spd;
            return sum * SpeedToDistance;
        }

        /// <summary>親が占めている x の範囲（隙間の左右2枚）。</summary>
        public static List<Span> ParentSpans(StealField field)
        {
            var output = new List<Span>();
            if (field.GapFrom >= MinSpan) output.Add(new Span { From = 0, To = field.GapFrom });
            if (FieldWidth - field.GapTo >= MinSpan) output.Add(new Span { From = field.GapTo, To = FieldWidth });
            return output;
        }

        private static bool HitsParent(StealField field, Point p)
        {
            if (p.Y + RunnerRadius < field.BandTop || p.Y - RunnerRadius > field.BandBottom) return false;
            foreach (var span in ParentSpans(field))
            {
                if (p.X + RunnerRadius > span.From && p.X - RunnerRadius < span.To) return true;
            }
            return false;
        }

        private static bool HitsEgg(StealField field, Point p)
        {
            double dx = p.X - field.Egg.X;
            double dy = p.Y - field.Egg.Y;
            double reach = EggRadius + RunnerRadius;
            return dx * dx + dy * dy <= reach * reach;
        }

        /// <summary>発射して結果を出す。⚠️ 角度以外に入力は無い（完全に決まる）。</summary>
        /// <param name="angle">上向きを 0 とし、時計回りの弧度。</param>
        public static StealRun Launch(StealField field, double angle, double budget)
        {
            var path = new List<Point> { field.Start };
            double x = field.Start.X;
            double y = field.Start.Y;
            // 上向きが -y。角度は上向き基準の時計回り
            double dx = Math.Sin(angle);
            double dy = -Math.Cos(angle);
            double traveled = 0;

            while (traveled < budget)
            {
                x += dx * Step;
                y += dy * Step;
                traveled += Step;

                // ⭐ 壁で跳ね返る。これがあるので、親を避けてから卵へ戻る道ができる
                if (x < RunnerRadius)
                {
                    x = RunnerRadius;
                    dx = -dx;
                }
                else if (x > FieldWidth - RunnerRadius)
                {
                    x = FieldWidth - RunnerRadius;
                    dx = -dx;
                }
                if (y < RunnerRadius)
                {
                    y = RunnerRadius;
                    dy = -dy;
                }
                else if (y > field.Height - RunnerRadius)
                {
                    y = field.Height - RunnerRadius;
                    dy = -dy;
                }

                var here = new Point(x, y);
                path.Add(here);

                if (HitsEgg(field, here)) return new StealRun(StealOutcome.Success, path, traveled);
                if (HitsParent(field, here)) return new StealRun(StealOutcome.Blocked, path, traveled);
            }
            return new StealRun(StealOutcome.Stalled, path, traveled);
        }

        /// <summary>その飛距離で成功する角度が1つでもあるか（と、その角度）。
        ///
        /// ⭐ 画面には出さない。設計が解けるものになっているかを機械で確かめるために使う。
        /// ⚠️ 「解けない巣」を出荷したら、プレイヤーは運が悪いのだと思ってしまう。</summary>
        public static bool FindSolution(StealField field, double budget, int samples,
            out double angle, out double traveled)
        {
            bool found = false;
            angle = 0;
            traveled = 0;
            for (int i = 0; i < samples; i++)
            {
                // 上向き ±80度 を走査（真下へ撃つ意味は無い）
                double a = (-80 + 160.0 * i / (samples - 1)) * (Math.PI / 180.0);
                var run = Launch(field, a, budget);
                if (run.Outcome != StealOutcome.Success) continue;
                if (!found || run.Traveled < traveled)
                {
                    found = true;
                    angle = a;
                    traveled = run.Traveled;
                }
            }
            return found;
        }
    }
}
