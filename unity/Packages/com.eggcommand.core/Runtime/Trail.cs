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
        /// <summary>何も起きない。⭐ 分かれ道と分かれ道のあいだの「間」。</summary>
        Plain,
        /// <summary>雑魚。⭐ 倒すと**振れる回数が戻る**。⚠️ 戦闘を挟む。</summary>
        Mob,
        /// <summary>分かれ道。⭐ 塞いだ物をステで壊すと**先へ飛べる**。
        /// ⚠️ **踏まなくても、通り過ぎようとすると止まる**（<see cref="Trails.Roll"/>）。
        /// 踏んだときだけ効く物にすると、1回の潜入で判断が 0.65 回しか起きなかった
        /// （2026-08-20 の実測）。</summary>
        Fork,
        /// <summary>ステが**一時的に上がる**。⭐ 「いまなら壊せる」を作る。</summary>
        Boon,
        /// <summary>ステが**一時的に下がる**。⚠️ 予定していた近道が消える。</summary>
        Bane,
    }

    /// <summary>道の1マス。</summary>
    public sealed class Square
    {
        public readonly SquareKind Kind;

        // ── 分かれ道のとき ────────────────────────────
        /// <summary>何で塞がれているか。⭐ 壁＝攻撃 / 床＝HP / 重圧＝防御。</summary>
        public readonly GimmickKind Gate;
        /// <summary>壊すのに払う量。⚠️ **閾値ではなく消費**（作者の指示 2026-08-19）。</summary>
        public readonly int Requires;
        /// <summary>壊すと何マス先へ出るか。</summary>
        public readonly int Saves;

        // ── 増減のとき ──────────────────────────────
        public readonly StatKey Stat;
        /// <summary>±何%。</summary>
        public readonly int Percent;
        /// <summary>何回ぶん効くか（振る回数で数える）。</summary>
        public readonly int Rolls;

        public Square(SquareKind kind, GimmickKind gate = GimmickKind.Wall, int requires = 0,
            int saves = 0, StatKey stat = StatKey.Atk, int percent = 0, int rolls = 0)
        {
            Kind = kind;
            Gate = gate;
            Requires = requires;
            Saves = saves;
            Stat = stat;
            Percent = percent;
            Rolls = rolls;
        }
    }

    /// <summary>巣へ続く道。⭐ **すごろく**（作者の指示 2026-08-20）。
    ///
    /// ⚠️ **飛ばす遊び（<see cref="Steal"/>）と混ぜないこと。**
    /// あちらは移植元の規則で、較正済みの照合（`goldens/steal.json`）が踏んでいるので残してある。
    /// <see cref="Breeding"/> と <see cref="Fusion"/> の関係と同じ。
    ///
    /// ⭐ なぜ作り替えたか（2026-08-20 の実測）:
    /// <list type="bullet">
    /// <item>投げた1回の **63〜76% が何にも当たらず力尽きて**いた</item>
    /// <item>当たっても**最初の1つで飛行が終わる**ので、接触の6〜7割を捨てていた</item>
    /// <item>的が盤の **6〜8%** しか覆っておらず、跳ね返り自体は起きていたのに当たる物が無かった</item>
    /// <item>⚠️ そして**一番大きい的（親）が「当ててはいけないもの」** ── 弾く遊びの見た目で
    ///   「避ける遊び」を回していた</item>
    /// </list>
    ///
    /// ⭐ すごろくにすると「親の留守のあいだに戻れるか」が制限になり、
    /// **こっそり盗む**という話と、遊びの制限が同じものになる。</summary>
    public sealed class Trail
    {
        public readonly IReadOnlyList<Square> Squares;
        /// <summary>どの段の巣か。⚠️ 値段と長さの出どころ。</summary>
        public readonly int Tier;

        public Trail(IReadOnlyList<Square> spaces, int tier)
        {
            Squares = spaces;
            Tier = tier;
        }

        public int Length => Squares.Count;

        /// <summary>速度いくつで1回振れるか。⭐ **140**。
        ///
        /// ⚠️ 参照編成の速度合計は段1〜5で 759／911／1047／1199／1359。
        /// 140 で割ると **5／6／7／8／9** と1段ずつきれいに増える
        /// （150 だと 5,6,6,7,9 と段3で足踏みする）。2026-08-20 の実測。</summary>
        public const int SpeedPerRoll = 140;

        /// <summary>さいころの目。⭐ 1〜<see cref="Pips"/>。</summary>
        public const int Pips = 6;

        /// <summary>分かれ道が飛ばすマスの**ならし**。⭐ **5**。
        ///
        /// ⚠️ さいころの最大が6なので、**5 は「1回ぶんちょっと」**。
        /// 壊すと残りの目を捨てるので、⭐ 出目が小さいときほど壊す価値が高い。
        /// ⚠️ 実際の1本ずつは <see cref="SavesMin"/>〜<see cref="SavesMax"/> に散る。</summary>
        public const int ForkSaves = 5;

        /// <summary>1本ずつの飛べるマス。⭐ **3〜8**。
        ///
        /// ⚠️ 全部が同じ +5 だったとき、**最初に払えた1本を壊すのが常に最善**になり、
        /// 「出目を見て選ぶ」と「壊せるだけ壊す」が同じ成績になった（2026-08-20 の実測）。
        /// ⭐ 飛べる数と値段を**別々に**振ると、割安な本と割高な本が生まれ、
        /// 「ここで払うか、次の割安を待つか」という問いになる。</summary>
        public const int SavesMin = 3;
        public const int SavesMax = 8;

        /// <summary>値段の振れ幅。⭐ 相場の **70%〜130%**。</summary>
        public const int PriceLow = 70;
        public const int PriceHigh = 130;

        /// <summary>分かれ道の数。⭐ **道の長さ ÷ 6**（3〜5本）。
        ///
        /// ⚠️ 3〜4本を固定で置いたら、跨いだのは1回の潜入で **1.8〜2.4回**しかなかった
        /// （2026-08-20 の実測）。壊すと5マス飛ぶので、後ろの分かれ道を跳び越すため。
        /// ⭐ 長さに比例させると 3／3／4／5／5 になり、跨ぐ回数が 3〜4 に乗る。</summary>
        public static int ForksFor(int length) => length / 6;

        /// <summary>分かれ道どうしの最小の間。⚠️ 隣り合わせだけ避ける。</summary>
        public const int ForkGap = 2;

        /// <summary>雑魚を倒すと戻る回数。</summary>
        public const int MobRefund = 1;

        /// <summary>その巣から1回盗むごとに減る、振れる回数。⭐ **1**。
        ///
        /// ⭐ 巣の寿命（[巣の寿命] 4回で封鎖）を、すごろくでも効かせるための取り方。
        /// ⚠️ **盤の形は変えない。**盗むたびに道を作り直すと、下見して編成を選ぶ
        /// という遊びの芯が消える。⭐ 代わりに**親が早く帰ってくる**ようにする。
        ///
        /// ⚠️ <see cref="Steal.RaidsToSeal"/> に達した巣は入れば必ず戦闘なので、
        /// ここで 0 以下になる心配はしなくてよい（呼び側が先に振り分ける）。</summary>
        public const int RollsLostPerRaid = 1;

        /// <summary>段ごとの道の長さ。⭐ **15 + 4×段** → 19／23／27／31／35。
        ///
        /// ⚠️ **編成の速さで長さを変えてはいけない。**変えると速さが打ち消される。
        /// ⭐ 参照編成が「分かれ道を1つも壊さないと 6割落ちる」帯に置いてある。</summary>
        public static int LengthFor(int tier) => 15 + 4 * tier;

        /// <summary>その段の参照編成が、そのステに持っている量。⚠️ `sim trail` の実測（2026-08-20）。
        ///
        /// ⭐ **値引きの基準**に使う。ここより多く持っていれば安く、少なければ高くつく。</summary>
        public static int RefStat(GimmickKind gate, int tier)
        {
            switch (gate)
            {
                case GimmickKind.Wall: return 817 + 113 * (tier - 1);
                case GimmickKind.Damage: return 899 + 96 * (tier - 1);
                case GimmickKind.Pressure: return 1154 + 179 * (tier - 1);
                default: throw new ArgumentOutOfRangeException(nameof(gate), gate, "知らない関門");
            }
        }

        /// <summary>その段の参照編成の**合計**（攻＋HP＋防）。⭐ これが財布の大きさの基準。</summary>
        public static int RefTotal(int tier) =>
            RefStat(GimmickKind.Wall, tier) + RefStat(GimmickKind.Damage, tier)
            + RefStat(GimmickKind.Pressure, tier);

        /// <summary><see cref="ForkSaves"/> マス飛ぶ1本が、財布の何割を持っていくか。⭐ **40%**。
        ///
        /// ⚠️ ここが遊びの芯。**払える数 &lt; 跨ぐ数** でなければ「どれに払うか」が問いにならない。
        /// 跨ぐのは1回の潜入で 4.3 回なので、⭐ 40% ＝ 2.5本ぶん ＝ **2本ぶん足りない**。
        ///
        /// ⚠️ 財布を3つ（ステごと）に分けていたときは、どの財布も種類ごとに 1.7本しか
        /// 機会が無く、余った。⭐ 1つにまとめると全部の分かれ道が同じ金を取り合う
        /// （作者の答え「合計から引く」2026-08-20）。</summary>
        public const int PriceShare = 40;

        /// <summary><see cref="ForkSaves"/> マス飛ぶ1本の相場。⚠️ 種類では変わらない
        /// （種類は <see cref="Trails.SlantOf"/> の値引きで効く）。</summary>
        public static int PriceFor(int tier) => RefTotal(tier) * PriceShare / 100;

        /// <summary>飛べるマス数に比例した相場。</summary>
        public static int FairPrice(int tier, int saves) => PriceFor(tier) * saves / ForkSaves;
    }

    /// <summary>1回振ったあと、いま何を待っているか。</summary>
    public enum RaidStep
    {
        /// <summary>止まった。⭐ 次を振れる。</summary>
        Moved,
        /// <summary>分かれ道で止まった。⭐ **壊すか、歩いて通り過ぎるか**を選ぶ。</summary>
        AtFork,
        /// <summary>雑魚と出会った。⚠️ 呼び側が戦闘を回す。</summary>
        Met,
        /// <summary>卵に届いた。</summary>
        Reached,
        /// <summary>親が帰ってきた。</summary>
        Caught,
    }

    /// <summary>進行中の1回の潜入。</summary>
    public sealed class Raid
    {
        public readonly Trail Trail;
        public readonly IReadOnlyList<Creature> Party;

        /// <summary>いま何マス目に居るか。⭐ 0 が入口、道の長さが卵。</summary>
        public int At;
        /// <summary>あと何回振れるか。⚠️ 0 になって届いていなければ親が帰ってくる。</summary>
        public int Rolls;
        /// <summary>編成の合計ステ。⚠️ **減らない** ── 値引きの基準として最後まで効く。</summary>
        public StatBlock Pool;
        /// <summary>財布。⭐ 攻＋HP＋防 の合計から始まり、**分かれ道を壊すと減る**（戻らない）。</summary>
        public int Power;
        /// <summary>一時的な増減（%）。</summary>
        public StatBlock Temp;
        /// <summary>その増減があと何回ぶん効くか。</summary>
        public StatBlock TempLeft;

        /// <summary>分かれ道で止まったときの、**使い残した目**。
        /// ⭐ 壊せば捨てる／歩けば進める。</summary>
        public int Pending;

        /// <summary>倒した雑魚のマス。</summary>
        public readonly HashSet<int> Beaten = new HashSet<int>();
        /// <summary>壊した分かれ道のマス。</summary>
        public readonly HashSet<int> Broken = new HashSet<int>();
        /// <summary>歩いて通り過ぎた分かれ道のマス。⚠️ 二度は止まらない。</summary>
        public readonly HashSet<int> Passed = new HashSet<int>();

        /// <summary>いまの残り HP（<see cref="Party"/> と同じ並び）。
        /// ⭐ **雑魚との戦闘で負った傷は潜入のあいだ残る。**⚠️ -1 は「満タン」。</summary>
        public readonly List<int> Hp = new List<int>();
        /// <summary>いまの CT（個体 × 枠3）。⭐ 傷と同じく引き継がれる。</summary>
        public readonly List<int[]> Cooldowns = new List<int[]>();
        // ⚠️ 経験値の欄は置かない。報酬を配るのは戦闘の決着（呼び側）で、
        //    ここに写しを持つと「増えないのに増えると書いてある欄」ができる
        //    （実際そうなっていた。レビューで発覚 2026-08-20）。

        /// <summary>直前の出目。⚠️ 画面が出すため。0 はまだ振っていない。</summary>
        public int LastRoll;
        /// <summary>いま何を待っているか。</summary>
        public RaidStep Step = RaidStep.Moved;

        /// <summary>決着。⚠️ null なら続行中。</summary>
        public StealOutcome? Result;

        public Raid(Trail trail, IReadOnlyList<Creature> party, int rolls, StatBlock pool)
        {
            Trail = trail;
            Party = party;
            Rolls = rolls;
            Pool = pool;
            Power = pool.Atk + pool.Hp + pool.Def;
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

        /// <summary>分かれ道を壊すのに使える合計ステ。⭐ **3体ぶんを足す。**
        ///
        /// ⚠️ 素質は1体につき3ステまでしか上限に届かない（<see cref="Stats.WildStatMax"/> ×3 ＝
        /// <see cref="Stats.WildTotalMax"/>）ので、**寄せると1本が 1.5倍**になり、
        /// 代わりに別の1本が半分になる。⭐ ここが「どういう編成を作るか」の問いになる。</summary>
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

        /// <summary>その分かれ道が要求するステ。⭐ <see cref="Steal"/> と同じ対応を使う。</summary>
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

        /// <summary>そのステを、その段の参照編成に対して何%持っているか（一時増減こみ）。
        ///
        /// ⭐ **これが値引き率**。100 が並。150 なら払う量が 2/3、50 なら 2倍。
        /// ⚠️ ここが「寄せた編成」の効き所 ── 素質は1体3ステまでしか上限に届かないので、
        /// 1本を 150 にすると別の1本が 50 になる。</summary>
        public static int SlantOf(Raid raid, GimmickKind gate)
        {
            var key = StatOf(gate);
            int had = raid.Pool[key];
            int pct = raid.TempLeft[key] > 0 ? raid.Temp[key] : 0;
            long mine = (long)had * (100 + pct) / 100;
            int refer = Trail.RefStat(gate, raid.Trail.Tier);
            if (refer <= 0) return 100;
            int slant = (int)(mine * 100 / refer);
            return slant < 10 ? 10 : slant;   // ⚠️ 0除算と青天井の値段を避ける
        }

        /// <summary>いまこの分かれ道を壊すのに、財布からいくら要るか。</summary>
        public static int CostOf(Raid raid, Square space) =>
            (int)((long)space.Requires * 100 / SlantOf(raid, space.Gate));

        /// <summary>道を作る。</summary>
        public static Trail Make(Rng rng, int tier)
        {
            int length = Trail.LengthFor(tier);
            var kinds = new List<GimmickKind>
                { GimmickKind.Wall, GimmickKind.Damage, GimmickKind.Pressure };

            // ⭐ 分かれ道は長さに比例。⚠️ 入口と卵の直前は空けておく（選ぶ余地が要る）
            int wants = Trail.ForksFor(length);
            var slots = new List<int>();
            for (int i = 2; i < length - 2; i++) slots.Add(i);

            // ⚠️ **引いてから間引くだけにしない。**間引いたぶんを引き直さないと、
            //    実際の本数が意図より 0.5〜0.6 本ぶん下振れする
            //    （段3で「4本」のはずが 2本 2.3% / 3本 43% だった。レビューで実測 2026-08-20）。
            // ⭐ 引ける場所が尽きるまで、間隔を守れる位置から足していく。
            var kept = new List<int>();
            var pool = new List<int>(slots);
            while (kept.Count < wants && pool.Count > 0)
            {
                int at = pool[rng.Int(0, pool.Count)];
                kept.Add(at);
                // ⚠️ 隣り合うと、片方を壊した先がもう片方になって判断が潰れる
                pool.RemoveAll(i => i > at - Trail.ForkGap && i < at + Trail.ForkGap);
            }
            kept.Sort();

            var spaces = new List<Square>();
            for (int i = 0; i < length; i++)
            {
                if (kept.Contains(i))
                {
                    var gate = kinds[rng.Int(0, kinds.Count)];
                    int saves = rng.Int(Trail.SavesMin, Trail.SavesMax + 1);
                    // ⭐ 相場に対して 70〜130%。⚠️ 飛べる数とは**別に**振る（連動させると一律になる）
                    int price = Trail.FairPrice(tier, saves)
                        * rng.Int(Trail.PriceLow, Trail.PriceHigh + 1) / 100;
                    spaces.Add(new Square(SquareKind.Fork, gate, requires: price, saves: saves));
                    continue;
                }
                spaces.Add(Ordinary(rng, i, length));
            }

            return new Trail(spaces, tier);
        }

        /// <summary>分かれ道でないマス。⭐ 雑魚と増減を散らす。</summary>
        private static Square Ordinary(Rng rng, int index, int length)
        {
            // ⚠️ 入口と卵の直前は素通りにする（開幕と決着に余計なものを挟まない）
            if (index < 2 || index >= length - 1) return new Square(SquareKind.Plain);

            int roll = rng.Int(0, 100);
            if (roll < 16) return new Square(SquareKind.Mob);
            if (roll < 28)
                return new Square(SquareKind.Boon, stat: PickStat(rng),
                    percent: 15 + rng.Int(0, 3) * 5, rolls: 2 + rng.Int(0, 2));
            if (roll < 38)
                return new Square(SquareKind.Bane, stat: PickStat(rng),
                    percent: -(15 + rng.Int(0, 3) * 5), rolls: 2 + rng.Int(0, 2));
            return new Square(SquareKind.Plain);
        }

        /// <summary>増減が乗るステ。⭐ 分かれ道の通貨になっている3本だけ。</summary>
        private static StatKey PickStat(Rng rng)
        {
            var keys = new[] { StatKey.Atk, StatKey.Hp, StatKey.Def };
            return keys[rng.Int(0, keys.Length)];
        }

        /// <summary>その巣の道。⭐ **巣ごとに固定**（同じ巣はいつ来ても同じ道）。
        ///
        /// ⚠️ 毎回作り直すと下見が意味を失う。⭐ 巣の中身（<see cref="Nests.SkillsOfNest"/>）と
        /// 同じやり方 ── 巣の id から流れを起こすので、保存する物が増えない。
        ///
        /// ⭐ ここが「寄せた編成」の置き場所: 道の関門の種類が見えるので、
        /// **攻に寄せた編成は壁だらけの巣へ行く**という選び方ができる。</summary>
        public static Trail OfNest(Nest nest) =>
            Make(new Rng(0).Stream($"trail:{nest.Id}"), nest.Tier);

        /// <summary>始める。</summary>
        /// <param name="raids">その巣から既に盗んだ回数。⭐ そのぶん振れる回数が減る。</param>
        public static Raid Begin(Trail trail, IReadOnlyList<Creature> party, int raids = 0) =>
            new Raid(trail, party, RollsFor(party, raids), PoolOf(party));

        /// <summary>1回振る。⭐ 戻り値は**次に何を待っているか**。
        ///
        /// ⚠️ ここでは雑魚の戦闘を始めない。始めるのは呼び側（画面）で、
        /// 勝ったら <see cref="Beat"/>、負けたら <see cref="Lost"/> を呼ぶ。
        /// ⭐ Core は戦闘の進行を知らない。</summary>
        public static RaidStep Roll(Rng rng, Raid raid)
        {
            Require(raid, RaidStep.Moved);
            if (raid.Rolls <= 0) throw new InvalidOperationException("もう振れない");

            raid.LastRoll = 1 + rng.Int(0, Trail.Pips);
            raid.Rolls--;
            Age(raid);
            return Advance(raid, raid.LastRoll);
        }

        /// <summary>分かれ道を壊さずに、残った目のぶん歩く。</summary>
        public static RaidStep Walk(Raid raid)
        {
            Require(raid, RaidStep.AtFork);
            raid.Passed.Add(raid.At);
            int pips = raid.Pending;
            raid.Pending = 0;
            if (pips <= 0) return Settle(raid);
            return Advance(raid, pips);
        }

        /// <summary>いま止まっている分かれ道を壊せるか。</summary>
        public static bool CanBreak(Raid raid)
        {
            if (raid.Step != RaidStep.AtFork) return false;
            return raid.Power >= CostOf(raid, raid.Trail.Squares[raid.At]);
        }

        /// <summary>分かれ道を壊して先へ出る。⭐ **払った量は戻らず、残った目も捨てる。**
        ///
        /// ⚠️ だから「出目が小さいときほど得」── 壊すか歩くかは出目で変わる。
        /// ⭐ 振る回数は減らない（壊すのは進むことの代わりではなく、道を縮める行為）。</summary>
        public static RaidStep Break(Raid raid)
        {
            if (!CanBreak(raid)) throw new InvalidOperationException("ここは壊せない");
            var space = raid.Trail.Squares[raid.At];
            raid.Power -= CostOf(raid, space);
            if (raid.Power < 0) raid.Power = 0;
            raid.Broken.Add(raid.At);
            raid.Pending = 0;
            return Advance(raid, space.Saves);
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

        /// <summary>マスを進める。⭐ **分かれ道は踏まなくても止まる**（跨ごうとした時点で）。</summary>
        private static RaidStep Advance(Raid raid, int steps)
        {
            int goal = raid.Trail.Length;
            for (int n = 1; n <= steps; n++)
            {
                int next = raid.At + 1;
                if (next >= goal)
                {
                    raid.At = goal;
                    raid.Result = StealOutcome.Success;
                    return raid.Step = RaidStep.Reached;
                }
                raid.At = next;

                // ⚠️ 壊した／通り過ぎた分かれ道では、もう止まらない
                if (raid.Trail.Squares[next].Kind == SquareKind.Fork
                    && !raid.Broken.Contains(next) && !raid.Passed.Contains(next))
                {
                    raid.Pending = steps - n;
                    return raid.Step = RaidStep.AtFork;
                }
            }
            return Settle(raid);
        }

        /// <summary>止まったマスの効き目を出し、次に何を待つか決める。</summary>
        private static RaidStep Settle(Raid raid)
        {
            var space = raid.Trail.Squares[raid.At];
            if (space.Kind == SquareKind.Boon || space.Kind == SquareKind.Bane)
            {
                // ⭐ 上書きする（重ねない）。重ねると桁が読めなくなる
                raid.Temp = raid.Temp.With(space.Stat, space.Percent);
                raid.TempLeft = raid.TempLeft.With(space.Stat, space.Rolls);
            }
            if (space.Kind == SquareKind.Mob && !raid.Beaten.Contains(raid.At))
                return raid.Step = RaidStep.Met;

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

        /// <summary>あといくつ進めば卵か。</summary>
        public static int Left(Raid raid) => raid.Trail.Length - raid.At;

        /// <summary>いまの分かれ道を壊したら、**実際に止まるマス**。
        ///
        /// ⚠️ `At + Saves` ではない。<see cref="Advance"/> は途中の未処理の分かれ道で
        /// 止まるので、跨いだ先には着かない（分かれ道の最小間隔は
        /// <see cref="Trail.ForkGap"/>、飛べるのは最大 <see cref="Trail.SavesMax"/> なので
        /// **跨ぐのが普通**）。画面が指す印がここを使わないと、印と実際がずれる
        /// （レビューで発覚 2026-08-20）。</summary>
        public static int LandingOf(Raid raid)
        {
            if (raid.Step != RaidStep.AtFork) return raid.At;
            int saves = raid.Trail.Squares[raid.At].Saves;
            int goal = raid.Trail.Length;
            for (int n = 1; n <= saves; n++)
            {
                int next = raid.At + n;
                if (next >= goal) return goal;
                var sq = raid.Trail.Squares[next];
                if (sq.Kind == SquareKind.Fork
                    && !raid.Broken.Contains(next) && !raid.Passed.Contains(next)) return next;
            }
            return raid.At + saves;
        }

        /// <summary>いまの分かれ道を歩いて通ったら、**実際に止まるマス**。</summary>
        public static int WalkingTo(Raid raid)
        {
            if (raid.Step != RaidStep.AtFork) return raid.At;
            int goal = raid.Trail.Length;
            for (int n = 1; n <= raid.Pending; n++)
            {
                int next = raid.At + n;
                if (next >= goal) return goal;
                var sq = raid.Trail.Squares[next];
                if (sq.Kind == SquareKind.Fork
                    && !raid.Broken.Contains(next) && !raid.Passed.Contains(next)) return next;
            }
            return raid.At + raid.Pending;
        }

        /// <summary>この先の分かれ道を壊して稼げるマス数。⭐ **いまの財布で届く範囲の目安。**
        ///
        /// ⚠️ **目安であって最適解ではない。**1マスあたりの安さが良い順に買うだけの見積り
        /// （本当の最適は積み荷問題になる）。画面に「壊せばここまで行ける」を出すためだけに使う。
        /// ⭐ 少なめに出ることはあっても、**払えない額を数えることはない**
        /// （＝出した数より実際が悪くなることはない）。
        ///
        /// ⚠️ 残った目を捨てる損は数えない（いま何を振ったかに依るので、ここでは決まらない）。</summary>
        public static int Sparable(Raid raid)
        {
            var ahead = new List<Square>();
            for (int i = raid.At; i < raid.Trail.Length; i++)
            {
                var sq = raid.Trail.Squares[i];
                if (sq.Kind != SquareKind.Fork) continue;
                if (raid.Broken.Contains(i) || raid.Passed.Contains(i)) continue;
                ahead.Add(sq);
            }
            // ⭐ 1マスあたりが安い順
            ahead.Sort((a, b) =>
            {
                long left = (long)CostOf(raid, a) * b.Saves;
                long right = (long)CostOf(raid, b) * a.Saves;
                return left.CompareTo(right);
            });

            int purse = raid.Power, saved = 0;
            foreach (var sq in ahead)
            {
                int cost = CostOf(raid, sq);
                if (cost > purse) continue;
                purse -= cost;
                saved += sq.Saves;
            }
            return saved;
        }

        /// <summary>このまま振り続けて届く見込み（%）。⭐ **画面に出して判断の材料にする。**
        ///
        /// ⚠️ 分かれ道と雑魚は数えない（＝**歩き通した場合**の下限）。
        /// ⭐ 「壊すか歩くか」を決めるのはこの数字なので、隠さず出す。</summary>
        public static int Odds(Raid raid, int extraSteps = 0)
        {
            int need = Left(raid) - extraSteps;
            int rolls = raid.Rolls;
            if (need <= 0) return 100;
            // ⚠️ ここで <see cref="Raid.Pending"/> を足さない。**呼ぶ側が渡す。**
            //    両方でやると同じ目を二重に数え、最後の1振りが分かれ道で止まった場面で
            //    「壊しても歩いても 100%」という嘘になる（レビューで発覚 2026-08-20）。
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
    }
}
