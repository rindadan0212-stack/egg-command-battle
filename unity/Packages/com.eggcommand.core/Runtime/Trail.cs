#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>マスの種類。⭐ **判断が生まれるものだけ置く**。
    ///
    /// ⚠️ 「◯◯を得た」だけのマスは置かない。振って読むだけの時間が増える。
    /// ⭐ ここに在るものは全部、**卵に届くかどうかの算数に直に効く**。</summary>
    public enum SquareKind
    {
        /// <summary>何も起きない。</summary>
        Plain,
        /// <summary>雑魚。⭐ 倒すと**振れる回数が戻る**。⚠️ 戦闘を挟む。</summary>
        Mob,
        /// <summary>ステが**一時的に上がる**。⭐ 「この先の関門が通れる」を作る。</summary>
        Boon,
        /// <summary>ステが**一時的に下がる**。⚠️ 予定していた道が閉じる。</summary>
        Bane,
    }

    /// <summary>マスから出ていく道。⭐ **関門は道が持つ**（マスではなく）。
    ///
    /// ⚠️ 道が分かれるからこそ「こっちは攻撃が足りないが、あっちは防御で通れる」が成り立つ
    /// （作者の指示 2026-08-20）。関門をマスに置くと、**同じ1本道の上で払うか払わないか**に
    /// なってしまい、その比べ方ができない。</summary>
    public sealed class Way
    {
        /// <summary>入る先のマス。⚠️ 必ず自分より後ろ（添字が増える向き）。</summary>
        public readonly int To;
        /// <summary>何が塞いでいるか。⭐ 壁＝攻撃 / 床＝HP / 重圧＝防御。</summary>
        public readonly GimmickKind Gate;
        /// <summary>通るのに要る量。⭐ **足りていれば通れる。減りはしない。**
        ///
        /// ⚠️ 最初は「払って減らす」形にしていたが、分岐と噛み合わなかった（実測 2026-08-20）:
        /// 2本とも関門付きなので払い切ると**行き止まり**になり、詰みが 63% 出た。
        /// さらに**寄せた編成ほど早く尽きる**ので、ならした編成のほうが強かった。
        /// ⭐ 分岐では**道の長さそのものが代価**（振れる回数を食う）。
        /// その上に消費を重ねると二重に取ることになる。
        /// 0 なら関門なし。</summary>
        public readonly int Requires;
        /// <summary>この道を選ぶと、合流まで何マスか。⭐ 選ぶ前に見せる。</summary>
        public readonly int Length;

        /// <summary>⭐ **関門の段。**1〜<see cref="Trail.GateGrades"/>、0 なら関門なし。
        ///
        /// ⭐ これがある理由（2026-08-20・作者の指示
        /// 「関門は数値を細かくいじらず固定値にして段をつけたら」）:
        /// ⚠️ 以前は参照編成に対する割合（115% / 60% / 25%）に**さらに ±20% の揺らぎ**を
        /// 掛けていたので、盤に「2,185」「1,351」「279」と**読めない数**が並んでいた。
        /// ⭐ 段にすると、画面で「★★★の壁」と読めて、道どうしを見比べられる。</summary>
        public readonly int Grade;

        public Way(int to, GimmickKind gate = GimmickKind.Wall, int requires = 0, int length = 1,
            int grade = 0)
        {
            To = to;
            Gate = gate;
            Requires = requires;
            Length = length;
            Grade = grade;
        }

        public bool IsGated => Requires > 0;
    }

    /// <summary>道の1マス。</summary>
    public sealed class Square
    {
        public readonly SquareKind Kind;

        // ── 増減のとき ──────────────────────────────
        public readonly StatKey Stat;
        /// <summary>±何%。</summary>
        public readonly int Percent;
        /// <summary>何回ぶん効くか（振る回数で数える）。</summary>
        public readonly int Rolls;

        /// <summary>ここから出ていく道。⭐ **2本以上なら分かれ道**。0本なら卵。</summary>
        public readonly List<Way> Ways = new List<Way>();

        /// <summary>入口から数えた段。⭐ **画面が並べるための目安**。
        ///
        /// ⚠️ 画面側で道を辿って割り出さない。ひし形の連なりは**作るときに形が分かっている**ので、
        /// そこで書いておく。辿って割り出した版はマスが重なって線が交差した
        /// （実機で確認 2026-08-20）。</summary>
        public int Row;
        /// <summary>左右の寄り。⭐ **-1 が近い道、+1 が遠い道、0 が分かれ道と合流点**。</summary>
        public int Lane;

        public Square(SquareKind kind = SquareKind.Plain,
            StatKey stat = StatKey.Atk, int percent = 0, int rolls = 0)
        {
            Kind = kind;
            Stat = stat;
            Percent = percent;
            Rolls = rolls;
        }

        /// <summary>分かれ道か。</summary>
        public bool IsJunction => Ways.Count > 1;
        /// <summary>卵か。</summary>
        public bool IsGoal => Ways.Count == 0;
    }

    /// <summary>巣へ続く道。⭐ **分岐するすごろく**（作者の指示 2026-08-20）。
    ///
    /// ⚠️ **飛ばす遊び（<see cref="Steal"/>）と混ぜないこと。**
    /// あちらは移植元の規則で、較正済みの照合（`goldens/steal.json`）が踏んでいるので残してある。
    /// <see cref="Breeding"/> と <see cref="Fusion"/> の関係と同じ。
    ///
    /// ⭐ 形は**ひし形の連なり**。分かれ道から2本出て、少し先で合流し、また分かれる。
    /// <code>
    ///          ○─○─○          ← 遠い道（関門は軽い・実りが多い）
    ///         ╱       ╲
    ///   ●───●          ●───   ← ● が分かれ道
    ///         ╲       ╱
    ///          ○─○            ← 近い道（関門は重い）
    /// </code>
    ///
    /// ⭐ なぜ1本道をやめたか（2026-08-20 の実測）:
    /// 1本道に「払って飛ぶ近道」を置くと、**払える数と跨ぐ数のつり合いだけ**が問題になり、
    /// 「壊せるだけ壊す」が常に最善になった。⚠️ ステの種類は値引きにしか効かず、
    /// **どのステが要るかを比べる場面が無かった**。
    /// ⭐ 分岐にすると「この道は攻撃、あの道は防御」と**種類そのものを比べる**ことになる。</summary>
    public sealed class Trail
    {
        public readonly IReadOnlyList<Square> Squares;
        /// <summary>どの段の巣か。⚠️ 長さと関門の重さの出どころ。</summary>
        public readonly int Tier;
        /// <summary>分かれ道のマスの添字（入口から順）。⭐ 画面が段を組むのに使う。</summary>
        public readonly IReadOnlyList<int> Junctions;

        public Trail(IReadOnlyList<Square> squares, int tier, IReadOnlyList<int> junctions)
        {
            Squares = squares;
            Tier = tier;
            Junctions = junctions;
        }

        public int Count => Squares.Count;
        /// <summary>卵のマス。⚠️ 必ず最後。</summary>
        public int Goal => Squares.Count - 1;
        /// <summary>いちばん深い段。⭐ 画面が盤の高さを出すのに使う。</summary>
        public int Depth
        {
            get
            {
                int deep = 0;
                foreach (var sq in Squares) if (sq.Row > deep) deep = sq.Row;
                return deep;
            }
        }

        /// <summary>速度いくつで1回振れるか。⭐ **140**。
        ///
        /// ⚠️ 参照編成の速度合計は段1〜5で 759／911／1047／1199／1359。
        /// 140 で割ると **5／6／7／8／9** と1段ずつきれいに増える
        /// （150 だと 5,6,6,7,9 と段3で足踏みする）。2026-08-20 の実測。</summary>
        public static int SpeedPerRoll => SpeedPerRollEach * Games.PartySize;

        /// <summary>⭐ **1体あたり**の、さいころ1回ぶんの速度。
        /// ⚠️ 3体で 140 と較正した値を3で割ってある（2026-08-20 の4体化）。
        /// ⭐ 体数に連動させたのは、増やしたときに**さいころが勝手に増える**のを防ぐため。</summary>
        public const int SpeedPerRollEach = 47;

        /// <summary>さいころの目。⭐ 1〜<see cref="Pips"/>。</summary>
        public const int Pips = 6;

        /// <summary>雑魚を倒すと戻る回数。</summary>
        public const int MobRefund = 1;

        /// <summary>その巣から1回盗むごとに減る、振れる回数。⭐ **1**。
        ///
        /// ⭐ 巣の寿命（4回で封鎖）を、すごろくでも効かせるための取り方。
        /// ⚠️ **道の形は変えない。**盗むたびに作り直すと、下見して編成を選ぶ
        /// という遊びの芯が消える。⭐ 代わりに**親が早く帰ってくる**ようにする。</summary>
        public const int RollsLostPerRaid = 1;

        /// <summary>ひと区切りの数。⭐ **2 + 段** → 3／4／5／6／7。
        ///
        /// ⚠️ **短くしすぎない。**4列にした当初は「2 + 段の半分」にしていたが、
        /// 段5の最短路が 39 → 18 マスに縮み、**どの指し手でも 97% 届く**盤になった
        /// （2026-08-20 に実測）。⭐ 振れる回数（段5で 8回・平均 3.5 で 28 マスぶん）に対して
        /// 最短路が短すぎると、道を選ぶ意味そのものが消える。</summary>
        public static int SectionsFor(int tier) => 1 + tier;

        /// <summary>分かれ道の数。⭐ **ひと区切りに1つ。**
        /// ⚠️ 本数（2〜4）は毎回変わるが、分かれる**場所**は区切りごとに1つ。</summary>
        public static int JunctionsFor(int tier) => SectionsFor(tier);

        /// <summary>ひと筋の長さ（小さい分かれ道から合流点まで）。
        ///
        /// ⚠️ **4本とも同じ長さにする。**⭐ そうしないと 1マス＝1段 が崩れ、
        /// 「出目のぶん進んでいない」と見える（2026-08-20・作者の指摘）。
        /// ⚠️ 以前は近い道 1〜2 マス／遠い道 3〜5 マスで、近い道の1マスが**2段ぶん**動いていた。
        /// ⭐ 長さの差でつけていた重みは、**関門の重さと道の中身**へ移した。</summary>
        /// <summary>⚠️ **1 から。**⭐ 1マスだけの道が混じると、盤に「1列の区間」ができて
        /// 3→1→1→1→2 のような顔つきになる（作者の指摘 2026-08-20）。</summary>
        public const int LaneMin = 1;
        public const int LaneMax = 5;

        /// <summary>⭐ **関門の段の数。**1〜5。0 は「関門なし」。</summary>
        public const int GateGrades = 5;

        /// <summary>段1つぶんの重さ（参照編成の持ち分に対する %）。
        /// ⭐ 段1 = 25% … 段5 = 125%。⚠️ 揺らぎは掛けない（読めなくなる）。</summary>
        public const int GradeShare = 25;

        /// <summary>⭐ 出す数の丸め。⚠️ 参照編成の持ち分は 1,141 のような半端な数なので、
        /// そのまま割ると読めない。**この単位に丸める**。</summary>
        public const int PriceRound = 50;

        /// <summary>⚠️ 一番外の道に許す段の上限。⭐ **0 なので関門は付かない。**
        /// ここが「遠回りの道には関門を少なく」（作者の指示 2026-08-20）の要。</summary>
        public const int GradeStep = 0;

        /// <summary>⭐ **1マス縮めるごとに上がる段。**
        /// ⚠️ 無関門の道より1マス短いだけなら軽く、大きく縮めるほど重い。</summary>
        public const int GradePerStep = 2;

        /// <summary>分かれ道の本数。⭐ **毎回変わる**（2〜4）。
        /// ⚠️ 4本に縛ると、盤がどこも同じ顔になる（作者の指摘 2026-08-20）。</summary>
        public const int WaysMin = 2;
        public const int WaysMax = 4;

        /// <summary>一番外の車線。⭐ 画面はこれを幅に合わせて割る。</summary>
        public const int LaneEdge = 3;

        /// <summary>その段の参照編成が、そのステに持っている量。⚠️ `sim trail` の実測（2026-08-20）。
        /// ⭐ 関門の重さはここからの割合で決める。</summary>
        // ⚠️ 参照編成を作るのは重い（個体を作って育てる）。⭐ 段ごとに1度だけ数える
        private static readonly Dictionary<int, StatBlock> Pools = new Dictionary<int, StatBlock>();

        /// <summary>その段の参照編成が払える量。⭐ **関門の重さの元。**</summary>
        private static StatBlock PoolFor(int tier)
        {
            StatBlock pool;
            if (Pools.TryGetValue(tier, out pool)) return pool;
            pool = Trails.PoolOf(Steal.ReferenceParty(tier));
            Pools[tier] = pool;
            return pool;
        }

        public static int RefStat(GimmickKind gate, int tier)
        {
            switch (gate)
            {
                // ⭐ **参照編成が実際に払える量そのもの。**
                // ⚠️ 以前は 817 / 899 / 1154 … と数を書いていたが、あれは
                //    「3体の参照編成の持ち分」を書き写しただけだった。
                //    ⭐ 書き写しをやめて元から引く ── 体数を変えても釣り合いが崩れない
                //    （2026-08-20 の4体化で、書き写した数が置き去りになりかけた）。
                case GimmickKind.Wall: return PoolFor(tier).Atk;
                case GimmickKind.Damage: return PoolFor(tier).Hp;
                case GimmickKind.Pressure: return PoolFor(tier).Def;
                default: throw new ArgumentOutOfRangeException(nameof(gate), gate, "知らない関門");
            }
        }

        /// <summary>近い道の関門が要求する量。⭐ 参照編成の持ち分の **115%**。
        ///
        /// ⭐ ここが「寄せる意味」の出どころ。振れ幅 80〜120% を掛けると
        /// 実際の要求は **持ち分の 0.92〜1.38倍**:
        /// <list type="bullet">
        /// <item>ならした編成（1.0倍）… 通れるのは **2割弱**</item>
        /// <item>1本に寄せた編成（1.5倍）… そのステの近道は **全部**通れる（＝全体の1/3）</item>
        /// <item>**2本に寄せた編成**（1.25倍）… **7割 × 2/3 ＝ ほぼ半分**</item>
        /// </list>
        /// ⚠️ つまり**2本に寄せるのが一番強い**。1本全振りは尖りすぎ、ならしは通れない。</summary>
        /// <summary>⚠️ 使わなくなった（2026-08-20 に段へ移した）。
        /// ⭐ 参照編成の持ち分を出す <see cref="Trails.RefStat"/> は残っている。</summary>
        public const int ShortShare = 115;

        /// <summary>遠い道の関門。⭐ **25%**。
        /// ⚠️ 軽くしてあるのは、**行き止まりを作らないため**
        /// （両方通れないと潜入がそこで終わる）。寄せた編成の薄いほう（0.5倍）でも通る。
        /// ⭐ 代わりに遠い ＝ 振れる回数を食う。</summary>
        public const int LongShare = 25;

        /// <summary>関門の重さの振れ幅。⭐ 相場の 80〜120%。</summary>
        public const int PriceLow = 80;
        public const int PriceHigh = 120;

        public static int PriceFor(GimmickKind gate, int tier, int share) =>
            RefStat(gate, tier) * share / 100;

        /// <summary>⭐ **段から出す、関門の要る量。**段 0 なら 0（関門なし）。
        ///
        /// ⚠️ 揺らぎを掛けない・丸める ── 盤に並ぶ数が読めることを優先する
        /// （2026-08-20・作者の指示「固定値にして段をつけたら」）。</summary>
        public static int PriceOfGrade(GimmickKind gate, int tier, int grade)
        {
            if (grade <= 0) return 0;
            int raw = RefStat(gate, tier) * grade * Trail.GradeShare / 100;
            // ⭐ 読める単位に丸める。⚠️ 0 にはしない（関門なしと混ざる）
            int step = Trail.PriceRound;
            int rounded = (raw + step / 2) / step * step;
            return rounded < step ? step : rounded;
        }
    }

    /// <summary>1回振ったあと、いま何を待っているか。</summary>
    public enum RaidStep
    {
        /// <summary>止まった。⭐ 次を振れる。</summary>
        Moved,
        /// <summary>⭐ **行ける先を選んでいる。**出目は決まっていて、あとは
        /// <see cref="Trails.Reach"/> が並べたマスから1つ選ぶだけ。
        ///
        /// ⚠️ 以前は「分かれ道に着いた」だった（<c>AtJunction</c>）。
        /// ⭐ 関門のあるマスに来るたびに**必ず止まって札で選ばせて**いたが、
        /// マスを直接押す形にしたので、止める必要が無くなった（2026-08-20・作者の指摘）。</summary>
        Choosing,
        /// <summary>雑魚と出会った。⚠️ 呼び側が戦闘を回す。</summary>
        Met,
        /// <summary>卵に届いた。</summary>
        Reached,
        /// <summary>親が帰ってきた／どの道も通れなくなった。</summary>
        Caught,
    }

    /// <summary>進行中の1回の潜入。</summary>
    public sealed class Raid
    {
        public readonly Trail Trail;
        public readonly IReadOnlyList<Creature> Party;

        /// <summary>いまどのマスに居るか。</summary>
        public int At;
        /// <summary>あと何回振れるか。⚠️ 0 になって届いていなければ親が帰ってくる。</summary>
        public int Rolls;
        /// <summary>始めたときの回数。⭐ **画面がさいころを何個並べるか**の元。
        /// ⚠️ 画面側で推測させない（推測させたら、雑魚で回数が戻った日にずれる）。</summary>
        public readonly int Given;
        /// <summary>編成の合計ステ。⚠️ **減らない** ── 関門は「足りているか」だけを見る。</summary>
        public StatBlock Pool;
        /// <summary>一時的な増減（%）。</summary>
        public StatBlock Temp;
        /// <summary>その増減があと何回ぶん効くか。</summary>
        public StatBlock TempLeft;

        /// <summary>分かれ道で止まったときの、**使い残した目**。⭐ 道を選ぶと、そのぶん進む。</summary>
        public int Pending;

        /// <summary>倒した雑魚のマス。</summary>
        public readonly HashSet<int> Beaten = new HashSet<int>();
        /// <summary>通った道（分かれ道のマス → 選んだ道の番号）。⭐ 画面が跡を描く。</summary>
        public readonly Dictionary<int, int> Took = new Dictionary<int, int>();

        /// <summary>直前の出目。⚠️ 画面が出すため。0 はまだ振っていない。</summary>
        public int LastRoll;
        /// <summary>いま何を待っているか。</summary>
        public RaidStep Step = RaidStep.Moved;

        /// <summary>いまの残り HP（<see cref="Party"/> と同じ並び）。
        /// ⭐ **雑魚との戦闘で負った傷は潜入のあいだ残る。**⚠️ -1 は「満タン」。</summary>
        public readonly List<int> Hp = new List<int>();
        /// <summary>いまの CT（個体 × 枠3）。⭐ 傷と同じく引き継がれる。</summary>
        public readonly List<int[]> Cooldowns = new List<int[]>();

        /// <summary>決着。⚠️ null なら続行中。</summary>
        public StealOutcome? Result;

        public Raid(Trail trail, IReadOnlyList<Creature> party, int rolls, StatBlock pool)
        {
            Trail = trail;
            Party = party;
            Rolls = rolls;
            Given = rolls;
            Pool = pool;
            Temp = new StatBlock(0, 0, 0, 0);
            TempLeft = new StatBlock(0, 0, 0, 0);
            for (int i = 0; i < party.Count; i++)
            {
                Hp.Add(-1);                      // -1 ＝ 満タン（まだ傷を負っていない）
                Cooldowns.Add(new int[3]);
            }
        }
    }

    /// <summary>すごろくの規則。</summary>
    public static class Trails
    {
        /// <summary>編成が何回振れるか。⭐ **速度の合計だけで決まる。**
        /// ⚠️ 配分は問わない ── 1駒なので、誰が速いかは効かない（作者の指示 2026-08-20）。
        /// ⚠️ <paramref name="raids"/> のぶん減る ＝ **盗まれた巣ほど親が早く帰ってくる**。</summary>
        public static int RollsFor(IReadOnlyList<Creature> party, int raids = 0)
        {
            int speed = 0;
            foreach (var c in party) speed += Creatures.StatsOf(c).Spd;
            int rolls = speed / Trail.SpeedPerRoll - raids * Trail.RollsLostPerRaid;
            return rolls < 1 ? 1 : rolls;
        }

        /// <summary>関門に払える量。⭐ **3体ぶんを足す。**
        ///
        /// ⚠️ 素質は1体につき3ステまでしか上限に届かない（<see cref="Stats.WildStatMax"/> ×3 ＝
        /// <see cref="Stats.WildTotalMax"/>）ので、**寄せると1本が 1.5倍**になり、
        /// 代わりに別の1本が半分になる。⭐ ここが「どの道を通れるか」を決める。</summary>
        public static StatBlock PoolOf(IReadOnlyList<Creature> party)
        {
            var sum = new StatBlock(0, 0, 0, 0);
            foreach (var c in party)
            {
                var s = Creatures.StatsOf(c);
                foreach (var key in Stats.Keys) sum = sum.With(key, sum[key] + s[key]);
            }
            return sum;
        }

        /// <summary>その関門が要求するステ。⭐ <see cref="Steal"/> と同じ対応を使う。</summary>
        public static StatKey StatOf(GimmickKind kind)
        {
            switch (kind)
            {
                case GimmickKind.Wall: return StatKey.Atk;
                case GimmickKind.Damage: return StatKey.Hp;
                case GimmickKind.Pressure: return StatKey.Def;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "知らない関門");
            }
        }

        /// <summary>いまそのステを実際にいくら使えるか（一時増減こみ）。</summary>
        public static int Usable(Raid raid, StatKey key)
        {
            int had = raid.Pool[key];
            int pct = raid.TempLeft[key] > 0 ? raid.Temp[key] : 0;
            long v = (long)had * (100 + pct) / 100;
            return v < 0 ? 0 : (int)v;
        }

        /// <summary>その道を通れるか。</summary>
        public static bool CanPass(Raid raid, Way way) =>
            !way.IsGated || Usable(raid, StatOf(way.Gate)) >= way.Requires;

        /// <summary>いま居る分かれ道の、通れる道の数。</summary>
        public static int OpenWays(Raid raid)
        {
            int open = 0;
            foreach (var way in raid.Trail.Squares[raid.At].Ways) if (CanPass(raid, way)) open++;
            return open;
        }

        // ── 盤を作る ────────────────────────────────

        /// <summary>その巣の道。⭐ **巣ごとに固定**（同じ巣はいつ来ても同じ道）。
        ///
        /// ⚠️ 毎回作り直すと下見が意味を失う。⭐ 巣の中身（<see cref="Nests.SkillsOfNest"/>）と
        /// 同じやり方 ── 巣の id から流れを起こすので、保存する物が増えない。</summary>
        public static Trail OfNest(Nest nest) =>
            Make(new Rng(0).Stream($"trail:{nest.Id}"), nest.Tier);

        /// <summary>道を作る。⭐ **ひし形の連なり**（分かれて、合流して、また分かれる）。</summary>
        public static Trail Make(Rng rng, int tier)
        {
            var squares = new List<Square>();
            var junctions = new List<int>();

            int sections = Trail.SectionsFor(tier);
            int hub = 0;
            squares.Add(new Square());                     // 入口

            for (int s = 0; s < sections; s++)
            {
                // ⭐ **分かれ道の本数は 2〜4 で毎回変わる**（2026-08-20・作者の指示
                //    「4が2回連続に縛る必要もない」「もっと自由な盤面に」）。
                int ways = rng.Int(Trail.WaysMin, Trail.WaysMax + 1);
                int root = squares[hub].Row;
                junctions.Add(hub);

                // ⭐ **道の長さは違ってよい。**⚠️ 以前は全部そろえていたが、
                //    そろえる必要は無かった（作者の指摘）。
                //    ⚠️ 「1マス＝1段」と「長さが違う」は同時に成り立たない
                //    （入口と卵を固定すると、段数＝マス数になるため）。
                //    ⭐ だから**1つの道の中だけ**マスを隣り合わせにし、長さの差は
                //    **合流までの1本の繋ぎ**に寄せる ── 数え間違えず、長さは自由になる。
                var lens = new int[ways];
                for (int k = 0; k < ways; k++)
                    lens[k] = rng.Int(Trail.LaneMin, Trail.LaneMax + 1);

                // ⭐ **関門を通らない道を必ず1本。しかもそれが一番長い。**
                //    （作者の指示「最上位の条件」）
                // ⚠️ 一番長いのが関門なしでないと、「長いのに関門もある」道ができて
                //    選ぶ理由が消える。⭐ どれか1本を最長にしてから、そこを無関門にする。
                int free = rng.Int(0, ways);
                int longest = 0;
                for (int k = 0; k < ways; k++) if (lens[k] > lens[longest]) longest = k;
                if (lens[free] <= lens[longest]) lens[free] = lens[longest] + 1;

                // ── 車線を割り当てる ────────────────────────
                // ⭐ 本数に合わせて左右へ散らす。⚠️ 無関門（一番長い）は**一番外**へ置く
                var lane = new int[ways];
                for (int k = 0; k < ways; k++)
                    lane[k] = ways <= 1 ? 0 : -Trail.LaneEdge + k * (Trail.LaneEdge * 2) / (ways - 1);
                // ⚠️ 外側は険しい（Filler）ので、無関門の道が外に来るように入れ替える
                if (ways > 1 && free != 0 && free != ways - 1)
                {
                    int edge = lane[free] < 0 ? 0 : ways - 1;
                    int keep = lane[free]; lane[free] = lane[edge]; lane[edge] = keep;
                }

                // ── 道を敷く ──────────────────────────────
                var heads = new int[ways];
                int span = 0;
                for (int k = 0; k < ways; k++) if (lens[k] > span) span = lens[k];

                for (int k = 0; k < ways; k++)
                {
                    heads[k] = squares.Count;
                    // ⭐ **険しいのは「関門なしの道」。**
                    // ⚠️ 車線の外側で決めていた頃、本数が2本だと**両方が外側**になり、
                    //    険しさの差が消えていた（2026-08-20 に実測して気づいた）。
                    // ⭐ 無関門の道は一番長く、そのうえ敵と ▲ が厚い ──
                    //    「ステは要らないが、遠くて危ない」という1本にまとまる。
                    bool harsh = k == free;
                    for (int i = 0; i < lens[k]; i++)
                    {
                        var sq = Filler(rng, harsh);
                        // ⭐ **1つの道の中は隣り合わせ。**ここが「数え間違えない」の要
                        sq.Row = root + 1 + i;
                        sq.Lane = lane[k];
                        squares.Add(sq);
                    }
                }

                // ⚠️ **合流をいつも真ん中に置かない。**⭐ 毎回 0 へ戻していたので、
                //    盤が「開いて閉じる」の繰り返しに見えていた（作者の指摘 2026-08-20）。
                //    ⭐ どれかの道の車線をそのまま引き継ぐ ── 盤が横へ流れる。
                int next = squares.Count;
                int keepLane = lane[rng.Int(0, ways)];
                squares.Add(new Square { Row = root + span + 1, Lane = keepLane });

                // ── 関門を付ける ───────────────────────────
                Gates(rng, squares[hub], heads, lens, free, tier);

                for (int k = 0; k < ways; k++)
                {
                    for (int i = 0; i < lens[k]; i++)
                    {
                        squares[heads[k] + i].Ways.Add(new Way(
                            i + 1 < lens[k] ? heads[k] + i + 1 : next));
                    }
                }

                hub = next;
            }
            return new Trail(squares, tier, junctions);
        }

        /// <summary>分かれ道に関門を付ける。
        ///
        /// ⭐ **最上位の決まり: 関門を通らない道が必ず1本あり、それが一番長い**
        /// （作者の指示 2026-08-20）。⚠️ それより短い道には必ず関門が付く ──
        /// 「短く済ませたいなら、ステで払え」という形。
        /// ⭐ 段は**短いほど重い**。本数が 2本でも 4本でも同じ規則で通る。</summary>
        private static void Gates(Rng rng, Square from, IReadOnlyList<int> heads,
            IReadOnlyList<int> lens, int free, int tier)
        {
            var kinds = new[] { GimmickKind.Wall, GimmickKind.Damage, GimmickKind.Pressure };
            int pick = rng.Int(0, kinds.Length);
            int longest = lens[free];

            for (int k = 0; k < heads.Count; k++)
            {
                if (k == free)
                {
                    // ⭐ 無関門。⚠️ 長さ（振れる回数）だけが代価
                    from.Ways.Add(new Way(heads[k], kinds[pick % kinds.Length], 0, lens[k] + 1));
                    continue;
                }
                // ⭐ **短いほど重い。**縮めた段数がそのまま段になる
                int saved = longest - lens[k];
                int grade = saved * Trail.GradePerStep;
                if (grade < 1) grade = 1;
                if (grade > Trail.GateGrades) grade = Trail.GateGrades;
                var gate = kinds[(pick + k) % kinds.Length];
                from.Ways.Add(new Way(heads[k], gate,
                    Trail.PriceOfGrade(gate, tier, grade), lens[k] + 1, grade));
            }
        }

        private static int Jitter(Rng rng, int price) =>
            price * rng.Int(Trail.PriceLow, Trail.PriceHigh + 1) / 100;

        /// <summary>道の途中に置くマス。
        ///
        /// ⭐ **遠い道のほうが実りが多い。**⚠️ そうしないと「近い道が通れるなら必ず近い道」に
        /// なって、選ぶ余地が長さと関門だけになる。
        /// ⭐ 遠回りには**雑魚（振れる回数が戻る）と ▲（ステが上がる）**を厚く置く
        /// ── 「遠いが、その先の関門を通れるようになる」という筋ができる。</summary>
        private static Square Filler(Rng rng, bool longWay)
        {
            int roll = rng.Int(0, 100);
            if (longWay)
            {
                // ⭐ **遠回りの取り柄は「敵」と「▲」。**
                // ⚠️ 敵を薄くすると、近い道が通れるときは必ず近い道になり、
                //    分かれ道の半分が「選ばない選択」になる（実測 2026-08-20）。
                //    ⭐ 敵は倒せば振れる回数が戻るので、**2体乗っている遠回りは行く価値がある**。
                // ⚠️ ただし敵は戦闘 ── 傷と CT を持ち越し、負ければそこで終わり。
                //    ⭐ 「遠くて危ないが速くなる」対「近くて安全だが痩せている」の取引にする。
                if (roll < 30) return new Square(SquareKind.Mob);
                if (roll < 58)
                    return new Square(SquareKind.Boon, PickStat(rng),
                        30 + rng.Int(0, 4) * 10, 4 + rng.Int(0, 4));
                if (roll < 58)
                    return new Square(SquareKind.Bane, PickStat(rng),
                        -(15 + rng.Int(0, 2) * 10), 2 + rng.Int(0, 2));
                return new Square();
            }
            // ⚠️ 近い道は素通りが多い。⭐ 速いことが取り柄なので、そこに実りを足さない
            if (roll < 10) return new Square(SquareKind.Mob);
            if (roll < 22)
                return new Square(SquareKind.Bane, PickStat(rng),
                    -(15 + rng.Int(0, 2) * 10), 2 + rng.Int(0, 2));
            return new Square();
        }

        /// <summary>増減が乗るステ。⭐ 関門の通貨になっている3本だけ。</summary>
        private static StatKey PickStat(Rng rng)
        {
            var keys = new[] { StatKey.Atk, StatKey.Hp, StatKey.Def };
            return keys[rng.Int(0, keys.Length)];
        }

        // ── 進む ────────────────────────────────────

        /// <summary>始める。</summary>
        /// <param name="raids">その巣から既に盗んだ回数。⭐ そのぶん振れる回数が減る。</param>
        public static Raid Begin(Trail trail, IReadOnlyList<Creature> party, int raids = 0)
        {
            var raid = new Raid(trail, party, RollsFor(party, raids), PoolOf(party));
            // ⚠️ **振る前に道を選ばせない**（2026-08-20・作者の指示
            //    「さいころを回した後に分岐を選ぶようにして」）。
            // ⭐ 入口が分かれ道でも、まず振る ── <see cref="Advance"/> が
            //    分かれ道で止めて、出た目をそのまま持ち越してくれる。
            // ⚠️ ただし**どの道にも入れない**なら、振っても仕方がないのでここで終わる。
            if (trail.Squares[0].IsJunction && OpenWays(raid) <= 0)
            {
                raid.Step = RaidStep.Caught;
                raid.Result = StealOutcome.Blocked;
            }
            return raid;
        }

        /// <summary>1回振る。⭐ 戻り値は**次に何を待っているか**。
        ///
        /// ⚠️ ここでは雑魚の戦闘を始めない。始めるのは呼び側（画面）で、
        /// 勝ったら <see cref="Beat"/>、負けたら <see cref="Lost"/> を呼ぶ。</summary>
        public static RaidStep Roll(Rng rng, Raid raid)
        {
            Require(raid, RaidStep.Moved);
            if (raid.Rolls <= 0) throw new InvalidOperationException("もう振れない");

            raid.LastRoll = 1 + rng.Int(0, Trail.Pips);
            raid.Rolls--;
            Age(raid);
            // ⚠️ **ここでは動かさない。**⭐ 行ける先は Reach が並べ、選ぶのは呼び側
            //    （2026-08-20・作者の指摘「マスを直接押すようになった」）。
            raid.Pending = raid.LastRoll;
            return raid.Step = RaidStep.Choosing;
        }

        /// <summary>⭐ **出目のぶんで行ける先を全部並べる。**
        ///
        /// ⭐ 戻りは「通る道筋」の一覧。先頭は必ずいま居るマス、末尾が止まるマス。
        /// ⚠️ 関門で通れない道は**入らない** ── だから画面は「光っているマス」だけ出せばよく、
        /// 鍵の絵も要らない（作者の指摘 2026-08-20）。
        /// ⚠️ 卵に着いたらそこで止まる（行き過ぎない）。</summary>
        public static List<List<int>> Reach(Raid raid, int pips)
        {
            var found = new List<List<int>>();
            var seen = new HashSet<int>();
            Walk(raid, new List<int> { raid.At }, pips, found, seen);
            return found;
        }

        private static void Walk(Raid raid, List<int> path, int left,
            List<List<int>> found, HashSet<int> seen)
        {
            int at = path[path.Count - 1];
            var here = raid.Trail.Squares[at];
            // ⭐ 卵に着いたら、出目が余っていてもそこで止まる
            if (left <= 0 || here.IsGoal)
            {
                // ⚠️ 同じマスへ2通りで着けるときは、先に見つけた道筋だけを残す
                if (path.Count > 1 && seen.Add(at)) found.Add(new List<int>(path));
                return;
            }
            foreach (var way in here.Ways)
            {
                if (!CanPass(raid, way)) continue;
                path.Add(way.To);
                Walk(raid, path, left - 1, found, seen);
                path.RemoveAt(path.Count - 1);
            }
        }

        /// <summary>⭐ **その道筋のとおりに動かす。**⚠️ <see cref="Reach"/> が返したものを渡すこと。
        ///
        /// ⚠️ 分かれ道を通ったときは、**通った道を記録する**（画面が跡を出すため）。</summary>
        public static RaidStep Go(Raid raid, IReadOnlyList<int> path)
        {
            Require(raid, RaidStep.Choosing);
            if (path == null || path.Count < 2)
                throw new ArgumentException("道筋が短すぎる", nameof(path));
            if (path[0] != raid.At)
                throw new ArgumentException("いま居るマスから始まっていない", nameof(path));

            for (int n = 0; n + 1 < path.Count; n++)
            {
                var ways = raid.Trail.Squares[path[n]].Ways;
                int took = -1;
                for (int k = 0; k < ways.Count; k++)
                {
                    if (ways[k].To != path[n + 1]) continue;
                    if (!CanPass(raid, ways[k]))
                        throw new InvalidOperationException("この道は通れない");
                    took = k;
                    break;
                }
                if (took < 0) throw new ArgumentException("繋がっていない道筋", nameof(path));
                // ⭐ 分かれ道だけ覚える（1本道は覚えるまでもない）
                if (ways.Count > 1) raid.Took[path[n]] = took;
                raid.At = path[n + 1];
            }

            raid.Pending = 0;
            return Settle(raid);
        }

        /// <summary>⭐ **行ける先が1つだけなら、それ。**⚠️ 無ければ null。
        /// 呼び側はこれで「選ばせずに進む」を判じる（作者の指示 2026-08-20）。</summary>
        public static IReadOnlyList<int> OnlyWay(Raid raid, int pips)
        {
            var all = Reach(raid, pips);
            return all.Count == 1 ? all[0] : null;
        }

        /// <summary>雑魚に勝った。⭐ **振れる回数が戻る。**</summary>
        public static RaidStep Beat(Raid raid)
        {
            Require(raid, RaidStep.Met);
            raid.Beaten.Add(raid.At);
            raid.Rolls += Trail.MobRefund;
            return Settle(raid);
        }

        /// <summary>雑魚に負けた。⚠️ そこで見つかる。</summary>
        public static RaidStep Lost(Raid raid)
        {
            Require(raid, RaidStep.Met);
            raid.Result = StealOutcome.Blocked;
            return raid.Step = RaidStep.Caught;
        }

        /// <summary>⚠️ **1マスも動けない。**⭐ どの道も通れない ＝ 編成が足りていない。</summary>
        public static RaidStep Stuck(Raid raid)
        {
            raid.Result = StealOutcome.Blocked;
            return raid.Step = RaidStep.Caught;
        }

        /// <summary>マスを進める。⚠️ 分かれ道に着いたらそこで止まる。</summary>
        private static RaidStep Advance(Raid raid, int steps)
        {
            for (int n = 0; n < steps; n++)
            {
                var here = raid.Trail.Squares[raid.At];
                if (here.IsGoal) break;
                int way = 0;
                if (here.IsJunction)
                {
                    // ⚠️ **まだ選んでいないなら、そこで止まる。**⭐ 選び済みならその道をたどる。
                    //    ⚠️ 選び済みを見ずに毎回止めていた頃、出目 0 で道を選んでも
                    //    1マス進んでしまい、**出目より多く動いて**いた（2026-08-20）。
                    if (!raid.Took.TryGetValue(raid.At, out way))
                    {
                        raid.Pending = steps - n;
                        return Settle(raid);
                    }
                }
                raid.At = here.Ways[way].To;
            }
            return Arrive(raid, 0);
        }

        /// <summary>着いたマスを片付けてから、残りの歩数を続ける。</summary>
        private static RaidStep Arrive(Raid raid, int rest)
        {
            if (rest > 0) return Advance(raid, rest);
            return Settle(raid);
        }

        /// <summary>止まったマスの効き目を出し、次に何を待つか決める。</summary>
        private static RaidStep Settle(Raid raid)
        {
            var here = raid.Trail.Squares[raid.At];
            if (here.IsGoal)
            {
                raid.Result = StealOutcome.Success;
                return raid.Step = RaidStep.Reached;
            }
            if (here.Kind == SquareKind.Boon || here.Kind == SquareKind.Bane)
            {
                // ⭐ 上書きする（重ねない）。重ねると桁が読めなくなる
                raid.Temp = raid.Temp.With(here.Stat, here.Percent);
                raid.TempLeft = raid.TempLeft.With(here.Stat, here.Rolls);
            }
            if (here.Kind == SquareKind.Mob && !raid.Beaten.Contains(raid.At))
                return raid.Step = RaidStep.Met;

            // ⚠️ **分かれ道でも止めない**（2026-08-20）。⭐ 選ぶのは「行ける先」であって
            //    「道」ではなくなったので、分かれ道は普通のマスと同じ。
            // ⚠️ ただし**どの道も通れない**なら、そこで終わり
            //    （「編成が足りていない」という負け方をはっきり出す）。
            if (here.IsJunction)
            {
                if (OpenWays(raid) <= 0)
                {
                    raid.Result = StealOutcome.Blocked;
                    return raid.Step = RaidStep.Caught;
                }
            }
            if (raid.Rolls <= 0)
            {
                raid.Result = StealOutcome.Stalled;
                return raid.Step = RaidStep.Caught;
            }
            return raid.Step = RaidStep.Moved;
        }

        private static void Require(Raid raid, RaidStep step)
        {
            if (raid.Step != step)
                throw new InvalidOperationException($"いまは {raid.Step} なので {step} の操作はできない");
        }

        /// <summary>一時増減の残りを1つ減らす。</summary>
        private static void Age(Raid raid)
        {
            foreach (var key in Stats.Keys)
            {
                int left = raid.TempLeft[key];
                if (left <= 0) continue;
                raid.TempLeft = raid.TempLeft.With(key, left - 1);
                if (left - 1 <= 0) raid.Temp = raid.Temp.With(key, 0);
            }
        }

        // ── 見通し ──────────────────────────────────

        /// <summary>各マスから卵までの**最短の歩数**（いま通れる道だけを数える）。
        ///
        /// ⚠️ 届かないマスは -1。⭐ 添字は必ず増える向きなので、後ろから1回なめれば出る。
        /// ⚠️ **払った後のことは見ていない**（関門を通るとステが減るので、実際はもっと厳しい）。
        /// ⭐ 画面に出すのは「いまの持ち分で、いちばん短く行けるとしたら何マスか」。</summary>
        public static int[] StepsToGoal(Raid raid)
        {
            var squares = raid.Trail.Squares;
            var dist = new int[squares.Count];
            dist[squares.Count - 1] = 0;
            for (int i = squares.Count - 2; i >= 0; i--)
            {
                int best = -1;
                foreach (var way in squares[i].Ways)
                {
                    if (!CanPass(raid, way)) continue;
                    int d = dist[way.To];
                    if (d < 0) continue;
                    if (best < 0 || d + 1 < best) best = d + 1;
                }
                dist[i] = best;
            }
            return dist;
        }

        /// <summary>あと何マスで卵か（いま通れる道での最短）。⚠️ -1 は「もう届かない」。</summary>
        public static int Left(Raid raid) => StepsToGoal(raid)[raid.At];

        /// <summary>その道を選んだら、あと何マスになるか。⚠️ -1 は「その先が詰む」。</summary>
        public static int LeftIfTake(Raid raid, int way)
        {
            var chosen = raid.Trail.Squares[raid.At].Ways[way];
            int rest = StepsToGoal(raid)[chosen.To];
            return rest < 0 ? -1 : rest + 1;
        }

        /// <summary>残り <paramref name="need"/> マスを、いまの回数で歩き切れる見込み（%）。</summary>
        private static int Chance(int rolls, int need)
        {
            if (need <= 0) return 100;
            if (rolls <= 0) return 0;
            var now = new double[1] { 1.0 };
            for (int r = 0; r < rolls; r++)
            {
                var next = new double[now.Length + Trail.Pips];
                for (int s = 0; s < now.Length; s++)
                {
                    if (now[s] == 0) continue;
                    for (int f = 1; f <= Trail.Pips; f++) next[s + f] += now[s] / Trail.Pips;
                }
                now = next;
            }
            double reach = 0;
            for (int s = need; s < now.Length; s++) reach += now[s];
            return (int)Math.Round(reach * 100);
        }

        /// <summary>このまま振り続けて届く見込み（%）。⭐ **画面に出して判断の材料にする。**
        ///
        /// ⚠️ 雑魚で回数が戻るぶんは数えない（＝控えめに出る）。
        /// ⚠️ 使い残した目は**呼び側が足す**（両方で足すと二重に数える）。</summary>
        public static int Odds(Raid raid, int extraSteps = 0)
        {
            int need = Left(raid);
            if (need < 0) return 0;
            return Chance(raid.Rolls, need - extraSteps);
        }

        /// <summary>その道を選んだときの見込み（%）。
        /// ⭐ 道へ入るのに1マス使い、使い残した目のぶんはそのまま歩ける。</summary>
        public static int OddsIfTake(Raid raid, int way)
        {
            int rest = LeftIfTake(raid, way);
            if (rest < 0) return 0;
            int carried = raid.Pending > 0 ? raid.Pending - 1 : 0;
            return Chance(raid.Rolls, rest - 1 - carried);
        }
    }
}
