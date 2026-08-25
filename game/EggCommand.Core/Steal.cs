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
        /// <summary>飛び切って盤面に降りた。⚠️ **失敗ではない** — 以降の発射台になる。
        /// ⭐ 後ろに足す（既にある3つの値を動かさない）。</summary>
        Landed,
        /// <summary>道中の雑魚に当たった。⭐ **その場で 3対3 の戦闘**になる。
        /// ⚠️ 失敗ではない — 勝てば投げる回数が戻り、経験値も入る。
        /// ⚠️ 親に当たったとき（<see cref="Blocked"/>）と混同しない。あちらは潜入の終わり。</summary>
        Fought,
    }

    /// <summary>道中の雑魚。⭐ **関門とは別物**。
    ///
    /// ⚠️ 関門は「ステを要求して通す／止める」もの。雑魚は要求を持たず、
    /// 当たると**戦闘になる**。同じ型に押し込むと <c>Requires</c> が意味を持たない欄になる。
    ///
    /// ⭐ 当たるのは損ではない。勝てば**投げる回数が戻り**、経験値も入る
    /// （親に当たると潜入が終わるのと逆）。だから「わざと当てに行く」が手になる。</summary>
    public sealed class Mob
    {
        public readonly Point At;
        public readonly double Radius;

        public Mob(Point at, double radius)
        {
            At = at;
            Radius = radius;
        }
    }

    /// <summary>経路上の関門。⭐ **要求するステが種類で決まる**（値は個別に持たない）。
    ///
    /// ⭐ これがある理由: 飛距離だけが問われると、編成は速度一色になる。
    /// 道の途中で他のステを要求すれば、3体それぞれに別の役目が生まれる。
    /// ⚠️ 関門ごとに「何を要求するか」を自由に書けるようにしない。
    /// 種類と要求値の対応が1箇所で決まっていないと、画面の絵と判定が食い違う。</summary>
    public enum GimmickKind
    {
        /// <summary>壁。攻撃力が足りれば**壊して貫通**し、以降は誰でも通れる。</summary>
        Wall,
        /// <summary>ダメージ床。HP が足りなければ踏破中に力尽きる。</summary>
        Damage,
        /// <summary>重圧のエリア。防御力が足りなければ耐えられない。</summary>
        Pressure,
    }

    /// <summary>関門ひとつ。盤を横切る帯として置く。</summary>
    public sealed class Gimmick
    {
        public readonly GimmickKind Kind;
        public readonly double From, To;
        public readonly double Top, Bottom;
        /// <summary>通るのに要るステの値。⚠️ どのステかは <see cref="Kind"/> が決める。</summary>
        public readonly int Requires;

        public Gimmick(GimmickKind kind, double from, double to, double top, double bottom, int requires)
        {
            Kind = kind;
            From = from;
            To = to;
            Top = top;
            Bottom = bottom;
            Requires = requires;
        }
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
        /// <summary>経路上の関門。⚠️ 空でもよい（移植元の盤には無い）。</summary>
        public readonly IReadOnlyList<Gimmick> Gimmicks;
        /// <summary>道中の雑魚。⚠️ 空でもよい。⭐ 1つの巣に <see cref="Steal.MobsMax"/> か所まで。</summary>
        public readonly IReadOnlyList<Mob> Mobs;

        public StealField(double height, FieldSide side, double gapFrom, double gapTo,
            double bandTop, double bandBottom, Point egg, Point start,
            IReadOnlyList<Gimmick>? gimmicks = null, IReadOnlyList<Mob>? mobs = null)
        {
            Mobs = mobs ?? new Mob[0];
            Height = height;
            Side = side;
            GapFrom = gapFrom;
            GapTo = gapTo;
            BandTop = bandTop;
            BandBottom = bandBottom;
            Egg = egg;
            Start = start;
            Gimmicks = gimmicks ?? new Gimmick[0];
        }
    }

    public sealed class StealRun
    {
        public readonly StealOutcome Outcome;
        /// <summary>通った軌跡。画面がこれをなぞって描く。</summary>
        public readonly List<Point> Path;
        public readonly double Traveled;
        /// <summary>止まった場所。⭐ 次の発射台になる。</summary>
        public readonly Point Landing;
        /// <summary>通れずに止められた関門の添字。
        /// ⚠️ **常に -1。**2026-08-19 に「関門では止まらず弾く」に変えたので、
        /// ここに入る場面が無くなった。⭐ 弾かれたかは <see cref="Bounced"/> を見ること。
        /// 🚧 読み手が居ないまま欄だけ残っている（次に触るときに消す）。</summary>
        public readonly int StoppedBy;
        /// <summary>この一投で壊した壁の添字。⭐ 以降は誰でも通れる。</summary>
        public readonly IReadOnlyList<int> Broke;
        /// <summary>当たった雑魚の添字。⚠️ 無ければ -1。</summary>
        public readonly int Mob;
        /// <summary>関門に弾かれたか。⭐ 画面が「通れなかった」と描くための印。
        /// ⚠️ 弾かれても飛び続けるので、着地点は関門の場所とは限らない。</summary>
        public readonly bool Bounced;

        private static readonly int[] Nothing = new int[0];

        public StealRun(StealOutcome outcome, List<Point> path, double traveled,
            int stoppedBy = -1, IReadOnlyList<int>? broke = null, int mob = -1,
            bool bounced = false)
        {
            Bounced = bounced;
            Mob = mob;
            Outcome = outcome;
            Path = path;
            Traveled = traveled;
            Landing = path.Count > 0 ? path[path.Count - 1] : new Point(0, 0);
            StoppedBy = stoppedBy;
            Broke = broke ?? Nothing;
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
        /// ⚠️ **盤の広さは実値の桁と無関係**（盤は 0〜1 の座標）。
        /// 桁を上げたぶんはここで割り戻す（2026-08-19）。置き去りにすると
        /// スピード合計が5倍になって、誰でも一投で親まで届いてしまう。
        public const double SpeedToDistance = 3.0 / Stats.Scale;

        /// <summary>進みの刻み。⚠️ 整数で刻んで決定論を保つ。</summary>
        private const double Step = 1;

        /// <summary>卵の位置（上端から）。⚠️ 盤を作る所と揃える。</summary>
        public const double EggY = 26;

        /// <summary>卵から親までの間合い。⭐ **50m 以内**（作者の指示 2026-08-19）。
        /// ⚠️ 縮めるほど「帯を抜けたら即・卵」になり、抜ける角度の精度が要る。
        /// ⭐ 44 は卵の半径（13）と帯の厚み（{@link BandThickness}）を差し引いても隙間が残る値。</summary>
        public const double ParentGap = 44;

        public const double EggRadius = 13;
        public const double RunnerRadius = 7;

        /// <summary>親が塞ぐ帯の厚み。位置は奥行きに合わせて動く。
        /// ⚠️ 卵との縦の余裕が要る。帯を卵に近づけすぎると、
        /// 隙間を抜けた後に横へ寄せきれず、どんな飛距離でも不能になる（走査で発覚）。</summary>
        /// ⚠️ **絵の高さ（56）とは一致していない。**親の絵は塞ぐ幅まで大きくしてあるので、
        /// 実測（2026-08-19）で 絵 y73〜129 に対し判定は y86〜116。
        /// **絵の上下 13 ずつは「描いてあるのに当たらない」。**
        /// ⭐ 作者の指示（「見えない判定を作るな／絵で道を塞げ」）は満たしている
        /// ── 逆向きのずれなので、**当たるのに見えない場所は無い**。
        /// ⚠️ ここを 56 にすると通路が 26 短くなり、段2 の関門が 2枚 → 1枚に落ちた（実測）。
        /// 釣り合いが動くので、直すなら [釣り合い] で決めてから。
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

        /// <summary>親が塞ぎ切るまでに盗める回数。⭐ **巣には寿命がある。**
        ///
        /// ⚠️ 無限に盗めると、良い巣を1つ見つけたら探索が要らなくなる。
        /// 上限があるので「次の巣を探す」が輪の駆動力として残る。</summary>
        /// ⚠️ 5 にしていたとき、最後の1回（raids 3）は隙間 20 に対し走者が 14 で、
        /// 通る角度が 2〜7度しか無かった。**幾何の上では通れるのに遊べない**状態。
        /// 4 にすると幾何の封鎖（隙間 &lt; 走者）と遊べる限界が一致する。
        public const int RaidsToSeal = 4;

        /// <summary>その回数だけ盗まれたあとの隙間の幅。⭐ **盗むほど狭まる。**
        /// ⚠️ 数値を上げるのではなく道を狭める（「親が強くなった」ではなく「守りを固めた」）。
        /// ⚠️ <see cref="RaidsToSeal"/> に達すると 0 ＝ 親が完全にふさぐ。もう潜入できない。</summary>
        /// <summary>最後まで残す隙間の幅。⚠️ **0 へ向けて直線的に詰めない。**
        ///
        /// ⚠️ 直線で詰めていたとき、最後に潜れる回（隙間 25.5・走者 14）の通る角度が
        /// **2〜5度**しかなかった。「難しい」ではなく「遊べない」。
        /// ⭐ 潜れるあいだは通れる幅を保ち、**塞ぐときは一気に塞ぐ**。
        /// 難易度の上がりぶんは関門の数が持つ（仕様が言う「ギミックが増える」ほう）。</summary>
        /// ⚠️ **42 に上げた**（2026-08-19）。親を卵の近く（<see cref="ParentGap"/>）へ動かしたぶん、
        /// 始点から隙間までの距離が伸び、同じ幅でも**見込む角度が狭くなった**
        /// （段5・raids3 で 5度＝出荷の検査が落ちた）。⭐ 幅で戻す。
        public const double GapFloor = 42;

        public static double GapWidthFor(int raids)
        {
            if (raids <= 0) return GapWidth;
            // ⭐ 塞ぎ切るのは一度きり。手前までは通れる幅を残す
            if (raids >= RaidsToSeal) return 0;
            double t = (double)raids / (RaidsToSeal - 1);
            if (t > 1) t = 1;
            return GapWidth + (GapFloor - GapWidth) * t;
        }

        /// <summary>その巣が死んでいるか。⭐ **盗んだ回数だけで決まる。**
        ///
        /// ⚠️ **以前は「隙間が走者より狭いか」で見ていた**（<c>GapWidthFor(raids) &lt;=
        /// RunnerRadius * 2</c>）。⭐ 弾いて飛ばす遊びの**幾何**であって、
        /// いまの遊び（すごろく）はその盤を1マスも使っていない。
        ///
        /// ⚠️ 実測すると raids 0〜6 のすべてで答えは一致していた ── つまり
        /// **たまたま正しかっただけ**（2026-08-21 の討論で発覚）。
        /// ⭐ 余裕は薄い: 最後の隙間 <see cref="GapFloor"/> = 42 に対し走者の直径は 14。
        /// ⚠️ 誰かが <see cref="GapFloor"/> を 14 未満へ詰めた日から、
        /// **巣が1回早く封鎖される** ── しかも黙って。
        ///
        /// ⭐ だから回数で書く。⚠️ 幾何のほうが先に破綻していないかは
        /// <c>InfiltrationTests.塞ぐ回数と隙間が閉じる回数が一致する</c> が見張る。</summary>
        public static bool IsSealed(int raids) => raids >= RaidsToSeal;

        /// <summary>親の寄り。⭐ 隙間が必ず片方の壁に接する（親は反対側の端に固まる）。
        /// ⚠️ <see cref="Lean"/> は raids 0 のときのこの値。定数のほうは移植元の照合が踏んでいる。</summary>
        public static double LeanFor(double gap) => FieldWidth / 2 - gap / 2;

        /// <summary>⚠️ 1マス幅の切れ端を返さない。
        /// 隙間が壁に接すると反対側に幅 1 の帯が残り、当たり判定には効かないのに
        /// 画面には線が出る。見た目が「何かある」と言っているのに実体が無いのは嘘。</summary>
        private const double MinSpan = 2;

        private static double JsRound(double value) => Math.Floor(value + 0.5);

        /// <param name="raids">その巣から今までに盗んだ回数。⭐ **関門が増える**。
        /// ⚠️ 数値を上げるのではなく関門を増やす（「強くなった」ではなく「守りが厚くなった」）。
        /// ⚠️ 既定 0 なら移植元の盤とまったく同じ（較正済みの照合が生きる）。</param>
        /// <param name="rng">関門の車線を振る乱数。⚠️ **null なら決め打ち**（移植元の照合と検査用）。
        /// ⚠️ 遊びから呼ぶときは <see cref="MakeValidatedField"/> を通すこと。
        /// ここは検査を通らない盤も返す。</param>
        public static StealField MakeField(int tier, FieldSide side, int raids = 0, Rng? rng = null)
        {
            double height = DepthForTier(tier);
            double gap = GapWidthFor(raids);
            double lean = LeanFor(gap);
            // 親が右へ寄る＝隙間は左寄り
            double center = side == FieldSide.Right ? FieldWidth / 2 - lean : FieldWidth / 2 + lean;
            // ⚠️ **親は卵のすぐ手前に置く**（作者の指示 2026-08-19）。
            //    前は盤の高さの 36% に置いていたので、奥が深い段ほど親が卵から離れ、
            //    段5では **114m** も空いていた（卵 Y26 に対し帯が Y140）。
            //    ⭐ 卵を守っているように見えないうえ、帯を抜けたあと惰性で届いてしまう。
            double bandTop = JsRound(EggY + ParentGap);
            var start = new Point(FieldWidth / 2, height - 14);
            // ⚠️ **関門は1度だけ作って両方に渡す。**雑魚用にもう1度（しかも rng を渡さずに）
            //    呼んでいた頃は、雑魚が**実在しない配置**を見て隙間を測っていたので、
            //    塞がった巣（検査を飛ばす経路）で関門の帯に重なり、要求値の字が読めなくなった。
            var gates = MakeGimmicks(tier, raids, side, bandTop + BandThickness, start.Y, rng);
            return new StealField(
                height,
                side,
                Math.Max(0, center - gap / 2),
                Math.Min(FieldWidth, center + gap / 2),
                bandTop,
                bandTop + BandThickness,
                new Point(FieldWidth / 2, EggY),
                start,
                gates,
                MakeMobs(tier, gates, bandTop + BandThickness, start.Y, rng));
        }

        /// <summary>関門の帯の厚み。
        /// ⚠️ 厚いほど、迂回する側は「空いた車線に留まったまま」越える距離が伸びる。
        /// 18 では迂回できる角度が 0.1度しか無かった（走査で発覚）。</summary>
        public const double GimmickThickness = 12;

        /// <summary>関門が塞ぐ横幅の割合。⭐ **塞ぎ切らない。**
        /// ⚠️ 全幅を塞ぐと「要求を満たす個体を持っているか」だけの検査になる。
        /// 空きを残せば「満たして直進する / 迂回して距離を払う」の二択になり、
        /// 速さが**どこで消費するかの資源**だという芯とつながる。
        ///
        /// ⚠️ 0.62 では迂回が**実質不能**だった。空いた車線が 40 しかないのに、
        /// 帯を越えるのに 32 ぶん上がる必要があり、留まれる角度の幅が 0.1度になる。
        /// しかも外へ寄せすぎると壁で跳ね返って関門へ戻る。⭐ **走査で決めた値。**</summary>
        public const double GimmickSpan = 0.5;

        /// <summary>出発点から最初の関門までの、最低限の間合い。
        ///
        /// ⭐ **出てすぐに判定を始めない。**誰を投げるか・どこから投げるかを
        /// 決めるための助走がここ。
        ///
        /// ⚠️ 通路を等分するだけだと、段3 raids0 で 47.3 まで寄り、
        /// 揺らぎが乗ると 33 まで来た（実測）。段2 raids2 のような
        /// 「浅いのに関門が多い」盤では、さらに詰まる。
        /// ⭐ 等分する区間そのものを、この間合いのぶん手前で打ち切る。</summary>
        public const double FirstGimmickClearance = 50;

        /// <summary>関門が要求する値。⭐ **想定編成から導く。手で置かない。**
        ///
        /// ⭐ 返すのは「その段階の想定編成の中で、そのステが一番高い個体の値」。
        /// つまり**寄せた1体はちょうど通り、他の2体は通れない**。
        /// これで関門が「誰に任せるか」の問いになる。
        ///
        /// ⚠️ 手で 28/32/36/40/44 と書いていたとき、段1 の壁は攻撃力 28 を要求するのに
        /// 段1 の想定編成は最大 27 だった ── **誰にも通れない関門**が1つ混じっていた。
        /// ⭐ ランダマイザが「持っていない鍵の後ろに扉を置かない」ために
        /// 到達可能な集合から逆算するのと同じ考え方。表に書くと必ずいつかずれる。</summary>
        public static int RequirementFor(int tier, GimmickKind kind)
        {
            var key = StatOf(kind);
            int best = 0;
            foreach (var creature in ReferenceParty(tier))
            {
                int value = Creatures.StatsOf(creature)[key];
                if (value > best) best = value;
            }
            return best;
        }

        /// <summary>関門の並び。⚠️ 乱数を渡さないときの決め打ち（移植元の照合と検査用）。</summary>
        private static readonly GimmickKind[] Order =
        {
            GimmickKind.Wall, GimmickKind.Damage, GimmickKind.Pressure,
        };

        /// <summary>縦の位置を枠の何割まで揺らすか。
        /// ⚠️ 大きくすると隣と重なるか、親の帯や出発点に食い込む。</summary>
        public const double JitterShare = 0.6;

        /// <summary>その盤に出す関門の種類を選ぶ。
        ///
        /// ⭐ **同じ種類を2つ出さない。** 関門がある理由は「3体それぞれに別の役目を作る」ことなので、
        /// 壁を3枚並べると「攻撃力を持っているか」だけの検査に戻ってしまう。
        ///
        /// ⚠️ **壁を一番奥にしない。** 壁は壊すと後続が通れるのが値打ちで、
        /// 一番奥だと後続がもう通らないから、その値打ちが丸ごと消える。
        /// ⭐ 消えても壊れはしないが、**種類ごとの違いが無くなる**ので避ける。</summary>
        private static List<GimmickKind> PickKinds(int count, Rng? rng)
        {
            var kinds = new List<GimmickKind>();
            if (rng == null)
            {
                for (int i = 0; i < count; i++) kinds.Add(Order[i]);
                return kinds;
            }

            kinds = rng.Sample(Order, count);
            if (count >= 2 && kinds[count - 1] == GimmickKind.Wall)
            {
                kinds[count - 1] = kinds[0];
                kinds[0] = GimmickKind.Wall;
            }
            return kinds;
        }

        /// <summary>関門の数。⚠️ 段階と、盗まれた回数で増える。上限は <see cref="Order"/> の長さ。</summary>
        public static int GimmickCountFor(int tier, int raids)
        {
            int count = tier - 1 + raids;
            if (count < 0) count = 0;
            return count > Order.Length ? Order.Length : count;
        }

        /// <summary>関門を置く。⭐ **空ける車線を先に決めてから塞ぐ。**
        ///
        /// ⭐ Spelunky が「解の道を先に彫ってから飾る」のと同じ考え方。
        /// 後から置くものが道を壊しようがない形にしておけば、検査は確認で済む。
        ///
        /// ⚠️ 左右を機械的に交互にすると、隣り合う関門の縦の間隔しだいで
        /// 車線を乗り換える角度が立ちすぎ、**通れるのに通れない**盤ができる。
        /// 乗り換えに要る角度が 45度を超えるなら、同じ側に空けて素直な道を残す。</summary>
        private static List<Gimmick> MakeGimmicks(int tier, int raids, FieldSide side,
            double corridorTop, double corridorBottom, Rng? rng)
        {
            var list = new List<Gimmick>();
            int count = GimmickCountFor(tier, raids);
            if (count == 0) return list;

            double span = FieldWidth * GimmickSpan;
            double lane = FieldWidth - span;          // 空けておく車線の幅
            double swap = FieldWidth - lane;          // 車線を乗り換えるのに要る横移動

            // ⭐ **間合いを空けるのは、梯子ごと押し上げることで作る。**
            // ⚠️ 等分する区間そのものを縮めると、空いたぶんが丸ごと
            //    雑魚の置き場から消えた（実測: 段2 の雑魚が 1 → 0、段5 が 3 → 1.9）。
            //    ⭐ 間隔は元のまま。出発点との間に空く区間が雑魚の置き場になる。
            double corridor = corridorBottom - corridorTop;
            // 梯子と、上下に要る余白（親の帯ぶん＋間合い）が収まるか
            double room = corridor - FirstGimmickClearance;
            if (room <= GimmickThickness * 1.5) return list;

            // ⚠️ **入らない数を詰め込まない。**⭐ 減らすほうを選ぶ
            //    （重ねて出すより、少ないほうが嘘が無い）。
            while (count > 1
                && (count - 1) * (corridor / (count + 1.0)) + GimmickThickness * 1.5 > room)
            {
                count--;
            }

            double slot = corridor / (count + 1.0);

            var kinds = PickKinds(count, rng);

            // ── 縦の位置。⭐ 枠の中で揺らす（同じ段でも盤の顔つきが変わる）
            // ⚠️ 揺らすと隣り合う関門の間隔が変わる。**その実測値で**車線の乗り換えを決める
            //    （名目の間隔で決めると、詰まった箇所で乗り換えを要求してしまう）
            //
            // ⚠️ **揺らぎ幅は間隔から出す。**割合（JitterShare）だけで振ると、
            //    詰まった盤では隣と重なった。⭐ どれだけ振っても帯1枚ぶんは必ず空ける。
            double swing = Math.Min(slot * JitterShare, Math.Max(0, slot - GimmickThickness));
            var ys = new double[count];
            for (int i = 0; i < count; i++)
            {
                double t = (i + 1.0) / (count + 1.0);
                double y = corridorBottom - corridor * t;
                if (rng != null) y += (rng.Float() - 0.5) * swing;
                ys[i] = y;
            }

            // ⭐ **一番手前の1枚が間合いに入っていたら、梯子ごと同じだけ上げる。**
            // ⚠️ 1枚だけ上げない ── 間隔が詰まって、隣と重なるか乗り換えられなくなる。
            double nearestBottom = double.MinValue;
            for (int i = 0; i < count; i++)
            {
                double bottom = ys[i] + GimmickThickness / 2;
                if (bottom > nearestBottom) nearestBottom = bottom;
            }
            double over = nearestBottom - (corridorBottom - FirstGimmickClearance);
            if (over > 0) for (int i = 0; i < count; i++) ys[i] -= over;

            // ⚠️ 親の帯へ食い込ませない。⭐ 上の押し上げで足りることは room の検査が保証する
            for (int i = 0; i < count; i++)
            {
                double top = corridorTop + GimmickThickness;
                if (ys[i] < top) ys[i] = top;
            }

            // ⭐ **出口から逆算する。**親が右へ寄っている＝隙間は左なので、
            //    一番奥の関門は左を空ける。ここを揃えないと、最後の一投が盤を横断させられる。
            // ⚠️ 車線と隙間を独立に決めていたときは、段1 でも通る角度が 1度になる盤が出た。
            //    Spelunky が「解の道を先に彫る」のと同じで、**出口まで含めて**先に決める。
            bool openRight = side != FieldSide.Right;
            var lanes = new bool[count];
            for (int i = count - 1; i >= 0; i--)
            {
                lanes[i] = openRight;
                if (i > 0)
                {
                    // ⭐ 乗り換えられるだけの縦の余裕があるときだけ、車線を反対側へ振る。
                    //    ⚠️ 余裕が無いのに振ると、要る角度が立ちすぎて通れない盤になる
                    bool canSwap = ys[i - 1] - ys[i] >= swap;
                    bool wantSwap = rng == null ? true : rng.Chance(0.6);
                    if (canSwap && wantSwap) openRight = !openRight;
                }
            }

            for (int i = 0; i < count; i++)
            {
                // ⚠️ **空いているのが車線。**塞ぐ側はその裏返しとして出す（食い違いようがない）
                double from = lanes[i] ? 0 : lane;
                // ⚠️ 要求値は関門の種類ごとに違う（要求するステが違うので当然）
                list.Add(new Gimmick(kinds[i], from, from + span,
                    ys[i] - GimmickThickness / 2, ys[i] + GimmickThickness / 2,
                    RequirementFor(tier, kinds[i])));
            }
            return list;
        }

        /// <summary>雑魚の当たりの大きさ。⚠️ 走者より大きくしないと避けようがない。</summary>
        public const double MobRadius = 11;

        /// <summary>雑魚の数。⭐ 深い巣ほど多い。⚠️ <see cref="MobsMax"/> で頭打ち。</summary>
        public static int MobCountFor(int tier)
        {
            int count = tier / 2;                       // 段1-2:0 / 段3-4:1〜2 / 段5:2
            if (tier >= 5) count = MobsMax;
            return count > MobsMax ? MobsMax : count < 0 ? 0 : count;
        }

        /// <summary>雑魚を置く。⭐ **関門の隙間（縦の間）に置く。**
        ///
        /// ⚠️ 関門と同じ高さに重ねない。重ねると「関門で止まったのか雑魚に当たったのか」が
        /// 読めなくなる。⭐ 当たるかどうかはプレイヤーの狙いで決まるべきなので、
        /// 横は広く散らす（真ん中に固めると必ず当たる／端に寄せると当たらない）。
        ///
        /// ⚠️ **ここは「置いてよさそうな場所」を挙げるだけ。**
        /// 解ける道を塞がないかは <see cref="PlaceMobs"/> が1体ずつ確かめる。</summary>
        private static List<Mob> MakeMobs(int tier, IReadOnlyList<Gimmick> gimmicks,
            double corridorTop, double corridorBottom, Rng? rng)
        {
            var list = new List<Mob>();
            int count = MobCountFor(tier);
            if (count == 0 || rng == null) return list;

            // ⭐ **雑魚も出発点から離す。**関門と同じ間合い（FirstGimmickClearance）。
            // ⚠️ 当たると戦闘が始まるので、遊ぶ側から見れば関門より重い障害物。
            //    関門だけ離して雑魚を残すと、「出てすぐ判定」が雑魚の側から戻ってくる。
            corridorBottom -= FirstGimmickClearance;
            if (corridorBottom <= corridorTop) return list;

            // ⭐ **関門の間に空いている縦の区間を測ってから置く。**
            // ⚠️ 以前は「関門の数で割った位置」に置いていたが、浅い巣は通路が短く、
            //    分数で離したつもりでも重なった（実測: 段2 raids2 で雑魚 y212 と関門 200〜212）。
            //    ⭐ 重なると関門の要求の字が絵の下に隠れて読めない。
            var gaps = FreeBands(gimmicks, corridorTop, corridorBottom);
            if (gaps.Count == 0) return list;

            // 広い区間から順に使う。⚠️ 足りなければ**置ける数だけ**にする
            gaps.Sort((a, b) => (b.To - b.From).CompareTo(a.To - a.From));
            for (int i = 0; i < count && i < gaps.Count; i++)
            {
                double y = (gaps[i].From + gaps[i].To) / 2;
                double x = MobRadius + RunnerRadius
                    + rng.Float() * (FieldWidth - (MobRadius + RunnerRadius) * 2);
                list.Add(new Mob(new Point(x, y), MobRadius));
            }
            return list;
        }

        /// <summary>関門にも親の帯にも掛からない、雑魚を置ける縦の区間。
        /// ⚠️ 雑魚の半径ぶんの余白を両側に取る（触れてもいけない）。</summary>
        private static List<Span> FreeBands(IReadOnlyList<Gimmick> gimmicks,
            double corridorTop, double corridorBottom)
        {
            var edges = new List<double> { corridorTop };
            var sorted = new List<Gimmick>(gimmicks);
            sorted.Sort((a, b) => a.Top.CompareTo(b.Top));
            foreach (var gate in sorted) { edges.Add(gate.Top); edges.Add(gate.Bottom); }
            edges.Add(corridorBottom);

            var bands = new List<Span>();
            for (int i = 0; i < edges.Count; i += 2)
            {
                double from = edges[i] + MobRadius;
                double to = edges[i + 1] - MobRadius;
                if (to - from >= MobRadius) bands.Add(new Span { From = from, To = to });
            }
            return bands;
        }

        /// <summary>雑魚だけ入れ替えた同じ盤。⚠️ 盤は作り直す（欄は書き換えない）。</summary>
        private static StealField WithMobs(StealField field, IReadOnlyList<Mob> mobs) =>
            new StealField(field.Height, field.Side, field.GapFrom, field.GapTo,
                field.BandTop, field.BandBottom, field.Egg, field.Start, field.Gimmicks, mobs);

        /// <summary>1体ずつ置いて、**解ける道を塞いだら戻す**。
        ///
        /// ⭐ 「盤は雑魚に頼らずに解ける」を**作り方で守る**。
        /// ⚠️ 検査が雑魚を無視するだけでは足りなかった。雑魚は飛行を止めるので、
        /// 無造作に置くと**通れる道そのものを食う**。実測で 段5・raids0 の
        /// 通る角度が 12度 → 解なし に落ちた。
        ///
        /// ⭐ 形は Brogue と同じ ── 置く → 通れるか確かめる → 駄目なら巻き戻す → 上限で諦める。
        /// ⚠️ 諦めた場合は**その雑魚を出さない**（置けた数だけになる）。
        /// 無理に置くと、置けた盤と置けない盤で難しさが黙って変わる。</summary>
        private static StealField PlaceMobs(StealField bare, IReadOnlyList<Creature> party,
            IReadOnlyList<Shot> plan, int tier, Rng rng)
        {
            int want = MobCountFor(tier);
            if (want <= 0 || plan == null || plan.Count == 0) return bare;

            var placed = new List<Mob>();
            var spots = MakeMobs(tier, bare.Gimmicks, bare.BandBottom, bare.Start.Y, rng);

            for (int i = 0; i < spots.Count; i++)
            {
                for (int attempt = 0; attempt < MobPlaceTries; attempt++)
                {
                    // ⚠️ 1回目は素の場所。駄目なら横だけ振り直す（高さは関門との間に保つ）
                    double x = attempt == 0
                        ? spots[i].At.X
                        : MobRadius + RunnerRadius
                            + rng.Float() * (FieldWidth - (MobRadius + RunnerRadius) * 2);
                    var candidate = new List<Mob>(placed);
                    candidate.Add(new Mob(new Point(x, spots[i].At.Y), MobRadius));
                    if (BlocksPlan(WithMobs(bare, candidate), party, plan)) continue;
                    placed = candidate;
                    break;
                }
            }
            return WithMobs(bare, placed);
        }

        /// <summary>雑魚を1体置くのに何回まで場所を振り直すか。
        /// ⚠️ 上限が無いと、道が細い盤で総当たりになる。</summary>
        private const int MobPlaceTries = 24;

        /// <summary>その手順が、この盤でもそのまま通るか。
        /// ⚠️ 雑魚に当たったら**その時点で駄目**（手順が途切れる）。</summary>
        private static bool BlocksPlan(StealField field, IReadOnlyList<Creature> party,
            IReadOnlyList<Shot> plan)
        {
            var run = new Infiltration(field, party);
            foreach (var shot in plan)
            {
                if (run.Result != null) break;
                if (Hop(run, shot.Member, shot.Pad, shot.Angle).Outcome == StealOutcome.Fought)
                    return true;
            }
            return run.Result != StealOutcome.Success;
        }

        // ── 生成の検査 ────────────────────────────────────

        /// <summary>通る角度の幅がこれ未満の盤は出荷しない。
        /// ⭐ 旧設計（一投）の実測が 6〜17度だったので、その下限を借りている。
        /// ⚠️ 幅1度の盤は「解ける」が遊べない。プレイヤーには運が悪いとしか見えない。</summary>
        public const int MinWindowDegrees = 6;

        /// <summary>その段階で来ると想定している編成。⭐ **検査の相手はこれ。**
        ///
        /// ⚠️ 関門の鍵は**盤の外**（プレイヤーの編成）にある。
        /// だから「解ける盤か」は盤だけでは決まらない。
        /// ⭐ 線引き: **参照編成で解けないのはバグ / プレイヤーの編成で解けないのは仕様**
        /// （「壁に対して正しい個体を作れたか」が game の核なので、後者は起きてよい）。
        ///
        /// ⚠️ 形は「役割分担」。実測でこれだけが全段を通ったので、想定編成として妥当。</summary>
        public static List<Creature> ReferenceParty(int tier)
        {
            int total = Nests.WildTotalForTier(tier);
            // ⭐ 得意3・薄い3。⚠️ 素質の上限が「1ステ上限×3」になったので、
            //    2つ振りの形で測ると、実際に来る編成より遅い相手で盤を検査することになる。
            int high = total * 4 / 15;
            int low = total / 15;
            var species = SpeciesTable.Fallback.Id;
            // ⚠️ 得意を明示する。⭐ 育てた分は得意へ自動で乗るので、
            //    ここを省くと「速」役の伸びが防御へ流れて、想定編成が届かなくなる
            var shapes = new[]
            {
                //          HP    攻    防    速    命中  抵抗
                new StatBlock(low,  high, low,  high, high, low),   // 攻め（弱化を通す側）
                new StatBlock(high, low,  high, low,  low,  high),  // 壁（弱化を受けない側）
                new StatBlock(low,  low,  high, high, low,  high),  // 速（先に動いて耐える）
                // ⭐ 4体目（2026-08-20 の4体化）。前衛 ── HP・攻・防に寄せる。
                // ⚠️ 3つの関門（壁＝攻 / 床＝HP / 重圧＝防）に**均等に効く**形を選んだ。
                //    片寄った形を足すと、その関門だけが相対的に緩くなる。
                new StatBlock(high, high, high, low,  low,  low),   // 前衛（3つの関門に均等）
            };
            var strong = new[] { StatKey.Atk, StatKey.Hp, StatKey.Spd, StatKey.Def };
            var weak = new[] { StatKey.Def, StatKey.Spd, StatKey.Atk, StatKey.Res };

            // ⚠️ **体数は編成の決まりから引く。**⭐ 形が足りなければ最初から繰り返す
            //    （体数を増やしたのに参照編成だけ3体のまま、という取り残しを防ぐ）。
            var party = new List<Creature>();
            for (int i = 0; i < Games.PartySize; i++)
            {
                int shape = i % shapes.Length;
                // ⭐ **特性も持たせる**（2026-08-21）。⚠️ 参照編成だけ持たないと、
                //    敵は種族の特性つき・こちらは無し、という**遊びに無い盤**で測ることになる。
                var creature = new Creature($"ref{i}", species, Stats.ApplyTotalCap(shapes[shape]),
                    new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1,
                    strong[shape], weak[shape], null, Creatures.TraitIdFor(species));
                // ⭐ 育てた分も持たせる。⚠️ 素の孵化直後で検査すると、想定より弱い相手で測ることになる
                //    （段1 は速度合計 69 に対し必要 65 で、通る角度が 1度しか無かった）
                Creatures.Grow(creature, Creatures.TrainMax);
                party.Add(creature);
            }
            return party;
        }

        /// <summary>その巣の盤を作る乱数。⭐ **種は巣と盗んだ回数だけで決まる。**
        ///
        /// ⚠️ 挑むたびに振り直すと、画面を出入りするだけで盤を選び直せる。
        /// 盗むまでは同じ盤、盗んだら別の盤 ── これで「粘って良い盤を引く」が消える。</summary>
        public static Rng RngFor(Nest nest, int raids) =>
            new Rng(0).Stream($"field:{nest.Id}:{raids}");

        /// <summary>生成して**検査して**、駄目なら振り直す。⭐ 出荷する盤はここを必ず通す。
        ///
        /// ⚠️ 検査を生成の外に置くと、悪い出目がそのまま出る。
        /// Brogue は地形を置く前に連結を判定し、駄目なら盤ごと巻き戻して、湖20回・machine10回で打ち切る。
        /// ここも同じ形にする — **振り直す / 上限で打ち切る / 一番マシなものへ落ちる**。
        ///
        /// ⚠️ 塞ぎ切った巣（<see cref="IsSealed"/>）は**解けないのが仕様**なので検査しない。</summary>
        /// <param name="rng">⚠️ 巣と raids から作った専用の系統を渡すこと。
        /// 呼ぶたびに違う種を渡すと、画面を出入りするだけで盤が振り直せてしまう。</param>
        /// <param name="window">出荷する盤で、参照編成が通れる角度の幅。
        /// ⚠️ <see cref="MinWindowDegrees"/> 未満なら**検査に落ちたまま出している**。
        /// 呼び側はここを見て記録できる（黙って悪い盤を出さないための唯一の手掛かり）。</param>
        public static StealField MakeValidatedField(int tier, FieldSide side, int raids, Rng rng,
            out int window, int tries = 8, int samples = 13)
        {
            // ⚠️ 塞ぎ切った巣は解けないのが仕様。検査しない（走査するだけ無駄）
            if (IsSealed(raids))
            {
                window = 0;
                return MakeField(tier, side, raids, rng);
            }

            var party = ReferenceParty(tier);
            StealField best = MakeField(tier, side, raids, rng);
            int bestWindow = -1;

            for (int attempt = 0; attempt < tries; attempt++)
            {
                var field = attempt == 0 ? best : MakeField(tier, side, raids, rng);
                // ⭐ **雑魚を外してから測る。**雑魚は飛行を止めるので、
                //    付けたまま測ると「雑魚に当たらずに通れる道」を数え損ねる
                var bare = WithMobs(field, new Mob[0]);
                List<Shot> plan;
                int found;
                FindRoomySolution(bare, party, samples, MinWindowDegrees, 12, out plan, out found);
                if (found >= MinWindowDegrees)
                {
                    window = found;
                    // ⭐ 通った道を塞がない場所にだけ雑魚を置く
                    return PlaceMobs(bare, party, plan, tier, rng);
                }
                if (found > bestWindow)
                {
                    bestWindow = found;
                    best = PlaceMobs(bare, party, plan, tier, rng);
                }
            }
            // ⚠️ 諦めた出目をそのまま返さない。測った中で一番マシなものを返す
            window = bestWindow < 0 ? 0 : bestWindow;
            return best;
        }

        public static StealField MakeValidatedField(int tier, FieldSide side, int raids, Rng rng)
        {
            int window;
            return MakeValidatedField(tier, side, raids, rng, out window);
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

        /// <summary>まだ倒していない雑魚に触れたか。⚠️ 倒したものは通り抜ける。</summary>
        private static int HitsMob(StealField field, Point p, HashSet<int>? cleared)
        {
            for (int i = 0; i < field.Mobs.Count; i++)
            {
                if (cleared != null && cleared.Contains(i)) continue;
                var mob = field.Mobs[i];
                double dx = p.X - mob.At.X;
                double dy = p.Y - mob.At.Y;
                double reach = mob.Radius + RunnerRadius;
                if (dx * dx + dy * dy <= reach * reach) return i;
            }
            return -1;
        }

        private static bool HitsEgg(StealField field, Point p)
        {
            double dx = p.X - field.Egg.X;
            double dy = p.Y - field.Egg.Y;
            double reach = EggRadius + RunnerRadius;
            return dx * dx + dy * dy <= reach * reach;
        }

        /// <summary>発射して結果を出す。⚠️ 角度以外に入力は無い（完全に決まる）。
        /// ⚠️ **移植元の一投。**関門も発射元の指定も無い。較正済みの照合が踏んでいるので残す。
        /// 遊びで使うのは <see cref="Hop"/>。</summary>
        /// <param name="angle">上向きを 0 とし、時計回りの弧度。</param>
        public static StealRun Launch(StealField field, double angle, double budget) =>
            Fly(field, field.Start, angle, budget, null, null);

        /// <summary>飛ばす。**唯一の出所。** <see cref="Launch"/> も <see cref="Hop"/> もここを通る。
        ///
        /// ⚠️ <paramref name="runner"/> が null なら関門を一切見ない ＝ 移植元と1ビットも変わらない。</summary>
        /// <param name="broken">既に壊れている壁の添字。⭐ 開通は盤に残るので、投げるたびに渡す。</param>
        private static StealRun Fly(StealField field, Point from, double angle, double budget,
            Creature? runner, HashSet<int>? broken, HashSet<int>? cleared = null)
        {
            var path = new List<Point> { from };
            double x = from.X;
            double y = from.Y;
            // 上向きが -y。角度は上向き基準の時計回り
            double dx = Math.Sin(angle);
            double dy = -Math.Cos(angle);
            double traveled = 0;
            bool bounced = false;
            List<int>? broke = null;
            // ⚠️ ステは1度だけ引く。⭐ 以前は関門の判定で**1歩ごとに引き直して**いた
            //    （種族表引き＋StatBlock 生成＋得意不得意を1飛行あたり200〜380回）
            var runnerStats = runner == null
                ? new StatBlock(0, 0, 0, 0)
                : Creatures.StatsOf(runner);

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

                // ⚠️ 壊した壁は**どの終わり方でも**持って帰る。
                //    親に当たった投で落とすと、盤の開通と画面の絵が食い違う
                if (HitsEgg(field, here))
                    return new StealRun(StealOutcome.Success, path, traveled, -1, broke);
                if (HitsParent(field, here))
                    return new StealRun(StealOutcome.Blocked, path, traveled, -1, broke);

                if (runner == null) continue;

                // ⭐ 雑魚に当たったら、**その場**が着地点になって戦闘へ。
                // ⚠️ 倒した雑魚はもう居ない（同じ場所で何度も稼げないように）
                int mob = HitsMob(field, here, cleared);
                if (mob >= 0)
                    return new StealRun(StealOutcome.Fought, path, traveled, -1, broke, mob);

                // ⭐ 関門は**通過したときに**判定する（着地点だけでは、飛び越えれば無効になる）
                int blockedBy = StepThrough(field, here, runnerStats, broken, ref broke);
                if (blockedBy >= 0)
                {
                    // ⭐ **関門では止まらず跳ね返る**（作者の指示 2026-08-19）。
                    // ⚠️ 前は関門の手前まで下がって**その場で止まって**いた。
                    //    「力尽きた」のか「弾かれた」のかが見た目で区別できず、
                    //    ビタ止まりで何が起きたか読めなかった。
                    // ⭐ 弾いて距離が尽きるまで飛ばせば、通れなかったことが動きで分かる。
                    // ⚠️ 関門は横帯なので**上下向き**を反転する。
                    //    ⚠️ 帯の外へ押し戻してから反転しないと、次の刻みでまた同じ帯に入って
                    //    その場で振動する（無限には回らないが、見た目が止まって見える）。
                    // ⚠️ **押し戻したぶんを `traveled` から引いてはいけない。**
                    //    引くと 1周あたりの進みが 0 以下になりうる ── 帯の中をほぼ水平に
                    //    飛んでいる（dy≈0）と、上下を反転しても帯から出られず、
                    //    押し戻し→再突入を**永久に**繰り返して終わらない
                    //    （2026-08-19 に実際に引いてみて、テストのホストごと落ちた）。
                    // ⭐ `traveled` は距離ではなく**燃料**だと考える。弾かれるのも燃料を使う。
                    //    これで毎周きっかり `Step` ずつ減り、必ず終わる。
                    var gate = field.Gimmicks[blockedBy];
                    path.RemoveAt(path.Count - 1);
                    while (path.Count > 1 && Inside(gate, path[path.Count - 1]))
                        path.RemoveAt(path.Count - 1);
                    var back = path[path.Count - 1];
                    x = back.X;
                    y = back.Y;
                    dy = -dy;
                    bounced = true;
                    continue;
                }
            }
            // ⚠️ 弾かれた関門の添字は返さない（もう「そこで止まった」わけではない）。
            //    ⭐ 代わりに「弾かれたか」だけを持ち帰る
            return new StealRun(runner == null ? StealOutcome.Stalled : StealOutcome.Landed,
                path, traveled, -1, broke, -1, bounced);
        }

        /// <summary>いま居る点の関門をさばく。
        /// ⭐ 足りていれば通す（壁なら壊して開通させる）。足りなければその添字を返す。</summary>
        /// <returns>止められた関門の添字。通れたなら -1。</returns>
        private static int StepThrough(StealField field, Point here, StatBlock stats,
            HashSet<int>? broken, ref List<int>? broke)
        {
            for (int i = 0; i < field.Gimmicks.Count; i++)
            {
                var gate = field.Gimmicks[i];
                if (broken != null && gate.Kind == GimmickKind.Wall && broken.Contains(i)) continue;
                if (!Inside(gate, here)) continue;

                if (stats[StatOf(gate.Kind)] < gate.Requires) return i;

                // ⭐ 壁だけは通った跡が盤に残る。床と重圧は通った本人にしか効かない
                if (gate.Kind == GimmickKind.Wall)
                {
                    broken?.Add(i);
                    if (broke == null) broke = new List<int>();
                    if (!broke.Contains(i)) broke.Add(i);
                }
            }
            return -1;
        }

        /// <summary>その関門が要求するステ。⚠️ **対応はここだけで決める**（画面もここを引く）。</summary>
        public static StatKey StatOf(GimmickKind kind)
        {
            switch (kind)
            {
                case GimmickKind.Wall: return StatKey.Atk;
                case GimmickKind.Damage: return StatKey.Hp;
                case GimmickKind.Pressure: return StatKey.Def;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static bool Inside(Gimmick gate, Point p) =>
            p.X + RunnerRadius > gate.From && p.X - RunnerRadius < gate.To &&
            p.Y + RunnerRadius > gate.Top && p.Y - RunnerRadius < gate.Bottom;

        /// <summary>その個体ひとりで飛べる距離。⭐ **編成合計ではない。**
        ///
        /// ⚠️ 以前は編成のスピード合計で1体を飛ばしていた。
        /// 「3体ぶんの速さで1体が飛ぶ」理屈が画面から読めず、課題に上がっていた。
        /// ⭐ 3回に分けても**合計は変わらない**ので、<see cref="DepthForTier"/> の較正はそのまま生きる。</summary>
        public static double DistanceFor(Creature runner) =>
            Creatures.StatsOf(runner).Spd * SpeedToDistance;

        /// <summary>一度の潜入。⭐ **3体を順に投げる**。着地した個体は盤に残り、次の発射台になる。
        ///
        /// ⭐ ここが設計の芯: 速い個体を先に使えば前線基地ができて遅い個体が奥へ届くが、
        /// **最終区間の飛距離を失う**。温存すればラスト1本は確実だが、序盤を遅い個体だけで処理する。
        /// ⚠️ 速さは「強さ」ではなく **「どこで消費するか」の資源**。
        ///
        /// ⚠️ 乱数を使わない。入力は「誰を・どこから・どの角度で」の3つだけ。</summary>
        public sealed class Infiltration
        {
            public readonly StealField Field;
            public readonly IReadOnlyList<Creature> Party;
            /// <summary>まだ投げていない個体の添字。</summary>
            public readonly List<int> Left = new List<int>();
            /// <summary>着地した個体の場所。⭐ そのまま発射台の並び。</summary>
            public readonly List<Point> Pads = new List<Point>();
            /// <summary>各発射台が誰か（<see cref="Party"/> の添字）。画面が絵を出すのに使う。</summary>
            public readonly List<int> PadOwner = new List<int>();
            /// <summary>壊れた壁。⭐ 開通は盤に残るので、次の個体も通れる。</summary>
            public readonly HashSet<int> Broken = new HashSet<int>();
            /// <summary>倒した雑魚。⭐ もう居ないので通り抜けられる。</summary>
            public readonly HashSet<int> Cleared = new HashSet<int>();

            /// <summary>いまの残り HP（<see cref="Party"/> と同じ並び）。
            /// ⭐ **戦闘で負った傷は潜入のあいだ残る。**⚠️ 負ける -1 は「満タン」。</summary>
            public readonly List<int> Hp = new List<int>();
            /// <summary>いまの CT（個体 × 枠3）。⭐ 傷と同じく引き継がれる。</summary>
            public readonly List<int[]> Cooldowns = new List<int[]>();

            /// <summary>この潜入で溜めた経験値。⭐ 雑魚を倒すたびに増える。</summary>
            public int Earned;

            /// <summary>決着。⚠️ null なら続行中。</summary>
            public StealOutcome? Result;

            public Infiltration(StealField field, IReadOnlyList<Creature> party)
            {
                Field = field;
                Party = party;
                for (int i = 0; i < party.Count; i++)
                {
                    Left.Add(i);
                    Hp.Add(-1);                      // -1 ＝ 満タン（まだ傷を負っていない）
                    Cooldowns.Add(new int[3]);
                }
            }
        }

        /// <summary>投げる。
        ///
        /// ⚠️ 決着しているのに投げようとしたら投げる（黙って何もしないと、
        /// 画面が「押したのに何も起きない」になる）。</summary>
        /// <param name="member"><see cref="Infiltration.Party"/> の添字。⚠️ まだ投げていない個体だけ。</param>
        /// <param name="pad">発射元。⚠️ **-1 は初期位置**（何体着地しても初期位置からは投げ続けられる）。
        /// それ以外は <see cref="Infiltration.Pads"/> の添字。</param>
        public static StealRun Hop(Infiltration run, int member, int pad, double angle)
        {
            if (run.Result != null)
                throw new InvalidOperationException("この潜入はもう決着している");
            if (!run.Left.Contains(member))
                throw new ArgumentException($"{member} 番はもう投げている");
            if (pad < -1 || pad >= run.Pads.Count)
                throw new ArgumentException($"発射台 {pad} は無い（-1 が初期位置）");

            var from = pad < 0 ? run.Field.Start : run.Pads[pad];
            var runner = run.Party[member];
            var flight = Fly(run.Field, from, angle, DistanceFor(runner), runner,
                run.Broken, run.Cleared);

            run.Left.Remove(member);

            // ⭐ 雑魚に当たった場所も着地点。次の発射台になる
            if (flight.Outcome == StealOutcome.Landed || flight.Outcome == StealOutcome.Fought)
            {
                run.Pads.Add(flight.Landing);
                run.PadOwner.Add(member);
            }

            // ⚠️ 親に触れた時点で戦闘。残りの個体は投げられない
            if (flight.Outcome == StealOutcome.Blocked) run.Result = StealOutcome.Blocked;
            else if (flight.Outcome == StealOutcome.Success) run.Result = StealOutcome.Success;
            // ⚠️ 雑魚に当たったら決着させない。⭐ 勝てば続く（呼び側が Beat を呼ぶ）
            else if (flight.Outcome == StealOutcome.Fought) { }
            // ⭐ 3体使い切って届かなければ、そこで戦闘へ
            else if (run.Left.Count == 0) run.Result = StealOutcome.Stalled;

            return flight;
        }

        /// <summary>投げる前に**同じ式で**飛ばしてみる。⭐ 画面の予告線はこれを描く。
        ///
        /// ⚠️ 予告を画面側で「まっすぐな線」として描いていた頃は、
        /// **狙った角度に飛ばない**という話になっていた ── 実際には壁で跳ね返っていて、
        /// 予告だけが嘘をついていた（作者の指摘 2026-08-19）。
        /// ⭐ ここを通せば、予告と実際が**必ず一致する**（同じ関数だから）。
        ///
        /// ⚠️ **状態を1つも変えない。**投げた扱いにも、壁を壊した扱いにもしない。</summary>
        public static StealRun Preview(Infiltration run, int member, int pad, double angle)
        {
            // ⚠️ **発射台も見る。**`Hop` は範囲を確かめているのに、ここだけ確かめずに
            //    `run.Pads[pad]` を引いていた ── 着地で発射台が増減した直後に、
            //    予告線を描くだけで**例外で落ちる**（毎フレーム通る道なので即クラッシュ）。
            //    ⭐ 下見は投げる前に何度でも呼ばれるので、外れていたら黙って空を返す。
            if (member < 0 || member >= run.Party.Count
                || pad < -1 || pad >= run.Pads.Count) return new StealRun(
                StealOutcome.Stalled, new List<Point>(), 0, -1, null);
            var from = pad < 0 ? run.Field.Start : run.Pads[pad];
            var runner = run.Party[member];
            // ⚠️ 壊した壁の集合は**複製**して渡す。そのまま渡すと下見で盤が開通してしまう
            return Fly(run.Field, from, angle, DistanceFor(runner), runner,
                new HashSet<int>(run.Broken), new HashSet<int>(run.Cleared));
        }

        /// <summary>1つの巣に置ける雑魚の数。⚠️ 増やすと潜入が戦闘の連続になる。</summary>
        public const int MobsMax = 3;

        /// <summary>雑魚を倒すともらえる経験値。⭐ 育成ポイントと同じ単位。</summary>
        public const int MobReward = 1;

        /// <summary>雑魚を倒した。⭐ **投げる回数が戻り**、経験値が入る。
        ///
        /// ⭐ これが「わざと当てに行く」を成立させている。
        /// 親に当たると潜入が終わるのに対し、雑魚は**続ける手段**になる。
        /// ⚠️ 倒した雑魚はもう居ない。同じ場所で何度も稼げない。
        ///
        /// ⚠️ 傷と CT は呼び側が渡す（Core は戦闘の結果を知らない）。
        /// **満タンに戻さない** — 引き継ぐからこそ「何度も戦えば削られる」が効く。</summary>
        /// <param name="hp">戦闘後の残り HP（<see cref="Infiltration.Party"/> と同じ並び）。
        /// ⚠️ null なら傷を更新しない。</param>
        /// <param name="cooldowns">戦闘後の CT。⚠️ null なら更新しない。</param>
        public static void Beat(Infiltration run, int mob, IReadOnlyList<int>? hp = null,
            IReadOnlyList<int[]>? cooldowns = null)
        {
            if (run.Result != null)
                throw new InvalidOperationException("この潜入はもう決着している");
            if (mob < 0 || mob >= run.Field.Mobs.Count)
                throw new ArgumentException($"雑魚 {mob} は盤に居ない");
            if (!run.Cleared.Add(mob)) return;   // ⚠️ 二重に数えない

            // ⭐ 発射回数のリセット。⚠️ 着地した個体は盤に残ったまま（台は消えない）
            run.Left.Clear();
            for (int i = 0; i < run.Party.Count; i++) run.Left.Add(i);

            run.Earned += MobReward;

            if (hp != null)
            {
                for (int i = 0; i < run.Hp.Count && i < hp.Count; i++) run.Hp[i] = hp[i];
            }
            if (cooldowns != null)
            {
                for (int i = 0; i < run.Cooldowns.Count && i < cooldowns.Count; i++)
                {
                    var from = cooldowns[i];
                    for (int slot = 0; slot < run.Cooldowns[i].Length && slot < from.Length; slot++)
                    {
                        run.Cooldowns[i][slot] = from[slot];
                    }
                }
            }
        }

        /// <summary>その巣のその雑魚の編成。⭐ **巣・盗んだ回数・番号だけで決まる。**
        ///
        /// ⚠️ その場で引かない。引くと画面を出入りするだけで顔ぶれを選び直せてしまう
        /// （盤そのものを <see cref="RngFor"/> で固定しているのと同じ理由）。
        /// ⭐ 決まっているので、**盤に出す絵と戦闘に出る相手が必ず一致する**。</summary>
        public static List<Creature> MobPartyOf(Nest nest, int raids, int mob)
        {
            var rng = RngFor(nest, raids).Stream($"mob:{mob}");
            return Nests.MakeMobParty(rng, nest, mob, SpeciesTable.Roll(rng));
        }

        /// <summary>雑魚戦に負けた。⚠️ 潜入はそこで終わり。
        /// ⭐ 「戦って負けた巣は引き直す」という既にある規則に揃える。</summary>
        public static void LostTo(Infiltration run) => run.Result = StealOutcome.Blocked;

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

        /// <summary>一投ぶんの手。⭐ 誰を・どこから・どの角度で。</summary>
        public struct Shot
        {
            public int Member;
            /// <summary>-1 は初期位置。</summary>
            public int Pad;
            public double Angle;
        }

        /// <summary>その編成でこの巣が**解けるか**（と、解く手順）。
        ///
        /// ⭐ 画面には出さない。設計が解けるものになっているかを機械で確かめるために使う。
        /// ⚠️ 「解けない巣」を出荷したら、プレイヤーは運が悪いのだと思ってしまう。
        /// ⚠️ リレーが入ったぶん、一投ぶんの走査では足りない。**順番と発射台まで含めて**探す。</summary>
        /// <param name="budget">投げてよい回数。⚠️ **上限が要る。**
        /// 上限が無いと探索の深さが伸び続ける。</param>
        ///
        /// <remarks>⭐ **雑魚を経由する手順は数えない。**
        ///
        /// ⭐ 盤は**雑魚に頼らずに解ける**こと。雑魚は「取れば楽になる」ものであって、
        /// 「取らないと解けない」ものにしない。
        ///
        /// ⚠️ 数えたときは、どの盤も**通る角度が1度**になった。
        /// 雑魚に当てるのは半径18の的を狙う精密な行為なので、そこを通る手順は必ず狭くなる。
        /// 狭い手順を「解けます」と数えると、検査が守りたかったものが崩れる。</remarks>
        public static bool FindRelaySolution(StealField field, IReadOnlyList<Creature> party,
            int samples, out List<Shot> plan, int budget = SearchBudget)
        {
            plan = new List<Shot>();
            var run = new Infiltration(field, party);
            int flights = 0;
            return Solve(run, samples, plan, ref flights, budget);
        }

        private static bool Solve(Infiltration run, int samples, List<Shot> plan,
            ref int flights, int budget)
        {
            // ⚠️ 手を1つ試すたびに状態が変わるので、枝ごとに写して戻す
            var left = new List<int>(run.Left);
            foreach (int member in left)
            {
                for (int pad = -1; pad < run.Pads.Count; pad++)
                {
                    // ⭐ 卵の方角から試す（上限つきの探索では順序が「見つかるか」を決める）
                    var order = AnglesToward(pad < 0 ? run.Field.Start : run.Pads[pad],
                        run.Field, samples);
                    for (int i = 0; i < samples; i++)
                    {
                        if (flights >= budget) return false;

                        double angle = order[i];
                        var snapshot = Copy(run);
                        var flight = Hop(run, member, pad, angle);
                        flights++;
                        plan.Add(new Shot { Member = member, Pad = pad, Angle = angle });

                        if (flight.Outcome == StealOutcome.Success) return true;
                        // ⭐ 親に触れた枝は死に枝。⚠️ 失速（着地）はまだ続く
                        if (flight.Outcome == StealOutcome.Landed && run.Result == null
                            && Solve(run, samples, plan, ref flights, budget)) return true;

                        plan.RemoveAt(plan.Count - 1);
                        Restore(run, snapshot);
                    }
                }
            }
            return false;
        }

        /// <summary>その手順が成功し続ける角度の幅（度）。⭐ **返すのは一番狭い一投の幅。**
        ///
        /// ⚠️ **「解が在るか」だけでは足りない。** 走査は等間隔のサンプルなので、
        /// 「解あり」も「解なし」も証明ではない。幅で見れば刻みの粗さに強くなるうえ、
        /// **手先の勝負になっていないか**も同じ数字で分かる。
        ///
        /// ⚠️ 実際に踏んだ: 関門の幅を 62% にしたとき解は在ったが、通る角度は 0.1度しか無かった。
        /// 旧設計（一投）は「成功する角度の幅 6〜17度」を測っていた。リレーでもその指標を持つ。</summary>
        /// <param name="budget">測るのに投げてよい回数。⚠️ **上限の外に置かない。**
        /// 幅の測定は1解あたり最大 320回の再生を回すので、探索の上限だけ絞っても
        /// ここが野放しだと「総当たりを止める」という狙いが効かない。</param>
        public static int WindowOf(StealField field, IReadOnlyList<Creature> party,
            IReadOnlyList<Shot> plan, int budget = int.MaxValue)
        {
            int narrowest = int.MaxValue;
            int spent = 0;
            for (int i = 0; i < plan.Count; i++)
            {
                double center = plan[i].Angle * 180.0 / Math.PI;
                int width = 1;   // 選んだ角度そのもの
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    for (int step = 1; step <= 160; step++)
                    {
                        double deg = center + dir * step;
                        if (deg < -80 || deg > 80) break;
                        if (spent >= budget) break;
                        spent += plan.Count;
                        if (!Replay(field, party, plan, i, deg * Math.PI / 180.0)) break;
                        width++;
                    }
                }
                if (width < narrowest) narrowest = width;
            }
            return narrowest == int.MaxValue ? 0 : narrowest;
        }

        /// <summary>1投だけ角度を差し替えて、手順を最後まで流し直す。
        /// ⚠️ 前の投の着地が変わると台の場所も変わる。**そこまで含めて**測る。</summary>
        private static bool Replay(StealField field, IReadOnlyList<Creature> party,
            IReadOnlyList<Shot> plan, int changeAt, double angle)
        {
            var run = new Infiltration(field, party);
            for (int i = 0; i < plan.Count; i++)
            {
                if (run.Result != null) break;
                var shot = plan[i];
                // ⚠️ 前の投が着地しなかったら台の番号がずれる。そこで打ち切る
                if (shot.Pad >= run.Pads.Count) return false;
                if (!run.Left.Contains(shot.Member)) return false;
                Hop(run, shot.Member, shot.Pad, i == changeAt ? angle : shot.Angle);
            }
            return run.Result == StealOutcome.Success;
        }

        /// <summary>**通る角度に幅がある**解を探す。⭐ 生成が検査に使うのはこちら。
        ///
        /// ⚠️ <see cref="FindRelaySolution"/> は最初に見つけた解を返すので、
        /// 幅 1度の針の穴でも「解けます」と答える。それを出荷すると、
        /// プレイヤーには「運が悪い」としか見えない。</summary>
        /// <param name="wantDegrees">これだけの幅が取れたら即座に良しとする。</param>
        /// <param name="give">解を何本まで測るか。⚠️ 上限が無いと総当たりになる。</param>
        /// <summary>探索で投げてよい回数の上限。⭐ **無いと総当たりになる。**
        ///
        /// ⚠️ 上限を入れる前、段5 の盤を1枚検査するのに **7.2秒**かかっていた。
        /// 解が狭い盤ほど枝が枯れずに探索が膨らむ ── つまり**一番遅いのが一番出したくない盤**。
        /// ⭐ Brogue も湖20回・machine10回で打ち切る。上限で諦めた盤は「駄目」として振り直す。</summary>
        public const int SearchBudget = 20000;

        /// <summary>その発射元から試す角度を、**卵の方角に近い順**に並べる。
        ///
        /// ⭐ 上限つきの探索では、**どこから試すか**が「見つかるか」を決める。
        /// 端の -80度から順に試すと、解に届く前に上限で尽きた（段3以上が全滅した）。
        /// ⚠️ 上限を上げるのではなく順序を直す。上限を上げると遅い盤がそのまま遅くなる。</summary>
        private static double[] AnglesToward(Point from, StealField field, int samples)
        {
            // 上向きを 0 とし時計回り。dx = sin, dy = -cos の逆算
            double toEgg = Math.Atan2(field.Egg.X - from.X, from.Y - field.Egg.Y) * 180.0 / Math.PI;
            var degrees = new double[samples];
            for (int i = 0; i < samples; i++) degrees[i] = -80 + 160.0 * i / (samples - 1);
            Array.Sort(degrees, (a, b) =>
            {
                double da = Math.Abs(a - toEgg);
                double db = Math.Abs(b - toEgg);
                return da != db ? da.CompareTo(db) : a.CompareTo(b);
            });
            for (int i = 0; i < samples; i++) degrees[i] *= Math.PI / 180.0;
            return degrees;
        }

        public static bool FindRoomySolution(StealField field, IReadOnlyList<Creature> party,
            int samples, int wantDegrees, int give, out List<Shot> plan, out int window,
            int budget = SearchBudget)
        {
            var best = new List<Shot>();
            int bestWindow = 0;
            int measured = 0;
            int flights = 0;

            var run = new Infiltration(field, party);
            var found = new List<Shot>();
            SolveRoomy(run, samples, found, field, party, wantDegrees, give,
                ref measured, ref bestWindow, best, ref flights, budget);

            plan = best;
            window = bestWindow;
            return bestWindow > 0;
        }

        private static bool SolveRoomy(Infiltration run, int samples, List<Shot> plan,
            StealField field, IReadOnlyList<Creature> party, int wantDegrees, int give,
            ref int measured, ref int bestWindow, List<Shot> best, ref int flights, int budget)
        {
            var left = new List<int>(run.Left);
            foreach (int member in left)
            {
                for (int pad = -1; pad < run.Pads.Count; pad++)
                {
                    var order = AnglesToward(pad < 0 ? run.Field.Start : run.Pads[pad],
                        run.Field, samples);
                    for (int i = 0; i < samples; i++)
                    {
                        if (flights >= budget) return false;

                        double angle = order[i];
                        var snapshot = Copy(run);
                        var flight = Hop(run, member, pad, angle);
                        flights++;
                        plan.Add(new Shot { Member = member, Pad = pad, Angle = angle });

                        if (flight.Outcome == StealOutcome.Success)
                        {
                            measured++;
                            // ⚠️ 幅の測定も上限の内側で数える
                            int width = WindowOf(field, party, plan, budget - flights);
                            flights += plan.Count * 8;
                            if (width > bestWindow)
                            {
                                bestWindow = width;
                                best.Clear();
                                best.AddRange(plan);
                            }
                            // ⭐ 十分な幅が取れたら打ち切る。⚠️ measured の上限でも打ち切る
                            if (bestWindow >= wantDegrees || measured >= give)
                            {
                                plan.RemoveAt(plan.Count - 1);
                                Restore(run, snapshot);
                                return true;
                            }
                        }
                        else if (flight.Outcome == StealOutcome.Landed && run.Result == null)
                        {
                            if (SolveRoomy(run, samples, plan, field, party, wantDegrees, give,
                                ref measured, ref bestWindow, best, ref flights, budget))
                            {
                                plan.RemoveAt(plan.Count - 1);
                                Restore(run, snapshot);
                                return true;
                            }
                        }

                        plan.RemoveAt(plan.Count - 1);
                        Restore(run, snapshot);
                    }
                }
            }
            return false;
        }

        private sealed class Snapshot
        {
            public List<int> Left = new List<int>();
            public List<Point> Pads = new List<Point>();
            public List<int> PadOwner = new List<int>();
            public HashSet<int> Broken = new HashSet<int>();
            /// <summary>⚠️ 倒した雑魚も巻き戻す。忘れると枝をまたいで「もう倒した」ことになる。</summary>
            public HashSet<int> Cleared = new HashSet<int>();
            public int Earned;
            public StealOutcome? Result;
        }

        private static Snapshot Copy(Infiltration run) => new Snapshot
        {
            Left = new List<int>(run.Left),
            Pads = new List<Point>(run.Pads),
            PadOwner = new List<int>(run.PadOwner),
            Broken = new HashSet<int>(run.Broken),
            Cleared = new HashSet<int>(run.Cleared),
            Earned = run.Earned,
            Result = run.Result,
        };

        private static void Restore(Infiltration run, Snapshot from)
        {
            run.Left.Clear();
            run.Left.AddRange(from.Left);
            run.Pads.Clear();
            run.Pads.AddRange(from.Pads);
            run.PadOwner.Clear();
            run.PadOwner.AddRange(from.PadOwner);
            run.Broken.Clear();
            foreach (int i in from.Broken) run.Broken.Add(i);
            run.Cleared.Clear();
            foreach (int i in from.Cleared) run.Cleared.Add(i);
            run.Earned = from.Earned;
            run.Result = from.Result;
        }
    }
}
