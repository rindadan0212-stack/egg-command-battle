#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>マスの見た目の種類。⭐ **画面が絵を選ぶためだけに在る。**
    ///
    /// ⚠️ **効き目はここに書かない。**効き目は <see cref="Gift"/> の並びが持つ。
    /// ⭐ そう分けてあるので、**新しいマスを足しても規則側は1行も変わらない**
    /// （作者の指示 2026-08-21「今後マスに追加機能を足しても破綻しない作りに」）。</summary>
    public enum SquareKind
    {
        /// <summary>何も起きない。⭐ **中立。**⚠️ 消さないこと ──
        /// 「どれも欲しくないときに選ぶ先」が無いと、毎手番が損得の計算になる。</summary>
        Plain,
        /// <summary>雑魚。⭐ 倒すと**振れる回数が戻る**。⚠️ 戦闘を挟む。</summary>
        Mob,
        /// <summary>ステが**一時的に上がる**。⭐ 関門で払う軍資金になる。</summary>
        Boon,
        /// <summary>ステが**一時的に下がる**。⚠️ 払えるはずだった関門が払えなくなる。</summary>
        Bane,
        /// <summary>⭐ **関門。払うと対価がもらえるマス。**
        ///
        /// ⚠️ **入るのに許しは要らない**（作者の指示 2026-08-21
        /// 「払わなくても入れる。対価を払えば有利になる」）。
        /// ⭐ 払えば得をするだけで、払えなくても素通りできる。
        /// ⚠️ 2026-08-21 まではステが足りないと**入れなかった**。
        /// そのせいで盤の生成が関門を知る必要があり（通れる道を必ず残す）、
        /// そこから詰みの不具合が3つ出た。⭐ いまは**盤の形はマスの種類を一切知らない**。</summary>
        Gate,
    }

    /// <summary>マスがくれるもの。⭐ **ここに1つ足すだけで、新しいマスが作れる。**
    ///
    /// ⚠️ 技の <c>Effect</c> と同じ考え方 ── 効き目を**データ**にして、
    /// 適用する場所を1か所（<see cref="Trails.Give"/>）に閉じ込める。
    /// ⭐ 新しい効き目を足すとき、触るのは
    /// 「<see cref="Trails.Give"/> に1件」と「画面の絵の表に1行」だけ。</summary>
    public enum GiftKind
    {
        /// <summary>振れる回数 +N。⭐ **距離に直に効く**（1回 ≒ 3.5マス）。</summary>
        Rolls,
        /// <summary>その場でもう N マス進める。⭐ **確実な距離。**
        /// ⚠️ さいころと違って外れが無い代わりに、伸びもしない。</summary>
        Hop,
        /// <summary>ステが一時的に ±N%。⚠️ 何回ぶん効くかは <see cref="Gift.Turns"/>。</summary>
        Stat,
        /// <summary>その場で戦闘。⚠️ 呼び側が回す（Core は戦闘を知らない）。</summary>
        Fight,
    }

    /// <summary>マスがくれるもの1件。</summary>
    public sealed class Gift
    {
        public readonly GiftKind Kind;
        /// <summary>量。⚠️ 意味は <see cref="Kind"/> で変わる（回数／マス数／%）。</summary>
        public readonly int Amount;
        /// <summary>どのステか。⚠️ <see cref="GiftKind.Stat"/> のときだけ意味を持つ。</summary>
        public readonly StatKey Stat;
        /// <summary>何回ぶん効くか。⚠️ 同上。</summary>
        public readonly int Turns;

        public Gift(GiftKind kind, int amount, StatKey stat = StatKey.Atk, int turns = 0)
        {
            Kind = kind;
            Amount = amount;
            Stat = stat;
            Turns = turns;
        }
    }

    /// <summary>そのマスで払えるもの。⭐ **払うかどうかはプレイヤーが決める。**
    ///
    /// ⚠️ 払っても「通れるようになる」のではない ── **入るのは只**。
    /// ⭐ 払って得られるのは <see cref="Square.OnPay"/> のほう。</summary>
    public sealed class Toll
    {
        /// <summary>何で払うか。⭐ 壁＝攻撃 / 床＝HP / 重圧＝防御。</summary>
        public readonly GimmickKind Kind;
        /// <summary>払う量。⚠️ **減る。**その潜入のあいだ戻らない。</summary>
        public readonly int Price;
        /// <summary>段（★1〜★5）。⭐ 値段と対価の**両方**がこれに比例する
        /// ── だから「どの段が得か」の計算は生まれない（交換の率は一定）。</summary>
        public readonly int Grade;

        public Toll(GimmickKind kind, int price, int grade)
        {
            Kind = kind;
            Price = price;
            Grade = grade;
        }
    }

    /// <summary>マスとマスを繋ぐ道。⭐ **行き先だけを持つ。**
    ///
    /// ⚠️ 以前は道が関門（要るステと量）を持っていた（2026-08-20 まで）。
    /// ⭐ **関門は1マスになった**（作者の指示「関門は1マスとしてカウントする」）ので、
    /// 道は繋がりを表すだけでよくなった。
    /// ⚠️ **どの道もちょうど1段だけ進む。**段飛ばしの近道は 2026-08-21 に捨てた ──
    /// 関門が只で入れるようになった時点で、近道も只になって「遠回りする理由」が消えたため。
    /// ⭐ 距離の伸び縮みは、いまは**マスがくれる <see cref="GiftKind.Hop"/>** が担う。</summary>
    public sealed class Way
    {
        /// <summary>入る先のマス。⚠️ 必ず自分より後ろ（添字が増える向き）。</summary>
        public readonly int To;

        public Way(int to) { To = to; }
    }

    /// <summary>道の1マス。⭐ **持ち物は3つだけ** ── 絵／止まったらくれるもの／払えるもの。</summary>
    public sealed class Square
    {
        private static readonly Gift[] Nothing = new Gift[0];

        /// <summary>画面が絵を選ぶための種類。⚠️ 規則はここを見ない。</summary>
        public readonly SquareKind Kind;

        /// <summary>⭐ **止まったらくれるもの。**⚠️ 通り抜けただけでは効かない。</summary>
        public readonly IReadOnlyList<Gift> OnLand;

        /// <summary>⭐ **戦闘に勝ったらくれるもの。**
        ///
        /// ⚠️ <see cref="OnLand"/> と分けてある。⭐ 止まった瞬間に配ると、
        /// **負けても報酬が残る**（2026-08-21 の監査で実測 ── 戦う前に回数 +5 が入り、
        /// 負けたあとも残っていた）。⭐ 「倒すと素材をくれる雑魚」はこちらへ書く。</summary>
        public readonly IReadOnlyList<Gift> OnWin;

        /// <summary>払えるもの。⚠️ null なら払うものが無い。</summary>
        public readonly Toll? Toll;
        /// <summary>⭐ **払ったらくれるもの。**⚠️ ここに戦闘は書けない（下の作り方が弾く）。</summary>
        public readonly IReadOnlyList<Gift> OnPay;

        /// <summary>ここから出ていく道。⭐ **2本以上なら分かれ道**。0本なら卵。</summary>
        public readonly List<Way> Ways = new List<Way>();

        /// <summary>入口から数えた段。⭐ **画面が並べるための目安**。
        /// ⚠️ 画面側で道を辿って割り出さない。作るときに形が分かっているので、そこで書いておく。</summary>
        public int Row;
        /// <summary>左右の寄り。⭐ **-3 〜 +3**（画面はこの幅で割って置く）。
        /// ⚠️ 揺らぎは持たない ── 2026-08-21 に外した。
        /// **段が「1段＝1歩」を運んでいる**ので、段の揃いを崩すと歩数が目で数えられなくなる。</summary>
        public int Lane;

        /// <summary>⚠️ **外から直に呼ばない。**⭐ 下の名前つきの作り方を使うこと。
        ///
        /// ⚠️ 絵（<see cref="Kind"/>）と効き目（<see cref="OnLand"/>）を別々に渡せると、
        /// **食い違ったマスが作れてしまう**。実際に <c>new Square(SquareKind.Mob)</c> で
        /// 「髑髏の絵だが戦闘にならないマス」ができ、検査が4件落ちた（2026-08-21）。
        /// ⭐ 名前つきの作り方しか無ければ、その食い違いは**書けない**。</summary>
        private Square(SquareKind kind, IReadOnlyList<Gift>? onLand,
            IReadOnlyList<Gift>? onWin = null)
        {
            Kind = kind;
            OnLand = onLand ?? Nothing;
            OnWin = onWin ?? Nothing;
            OnPay = Nothing;
        }

        private Square(Toll toll, IReadOnlyList<Gift> onPay)
        {
            Kind = SquareKind.Gate;
            Toll = toll;
            OnPay = onPay;
            OnLand = Nothing;
            OnWin = Nothing;
        }

        /// <summary>何も起きないマス。</summary>
        public Square() : this(SquareKind.Plain, null) { }

        /// <summary>⚠️ 戦闘は引数を持たないので、1つ作って使い回す。</summary>
        private static readonly Gift[] Fight = { new Gift(GiftKind.Fight, 0) };

        /// <summary>雑魚のマス。⭐ **必ず戦闘になる。**
        /// ⚠️ 倒してからくれる物は <paramref name="onWin"/> へ ── 止まった瞬間に配ると、
        /// **負けても残る**。</summary>
        public static Square Mob(params Gift[] onWin) =>
            new Square(SquareKind.Mob, Fight, onWin);

        /// <summary>▲ / ▼ のマス。⭐ 符号が絵を決めるので、食い違わない。</summary>
        public static Square Swing(StatKey key, int percent, int turns) =>
            new Square(percent >= 0 ? SquareKind.Boon : SquareKind.Bane,
                new[] { new Gift(GiftKind.Stat, percent, key, turns) });

        /// <summary>関門のマス。⚠️ **対価の無い関門は作れない**（只働きにしない）。</summary>
        public static Square Gate(Toll toll, params Gift[] onPay)
        {
            if (toll == null) throw new ArgumentNullException(nameof(toll));
            if (onPay == null || onPay.Length == 0)
                throw new ArgumentException("払っても何ももらえない関門", nameof(onPay));
            // ⚠️ **払いに戦闘は混ぜられない。**⭐ `Pay` は `Onward` で段を上書きするので、
            //    混ぜると戦闘が黙って起きない（2026-08-21 の監査で実測）。
            foreach (var gift in onPay)
                if (gift.Kind == GiftKind.Fight)
                    throw new ArgumentException("払いに戦闘は混ぜられない", nameof(onPay));
            return new Square(toll, onPay);
        }

        /// <summary>払うものが在るか。</summary>
        public bool IsGate => Toll != null;

        /// <summary>分かれ道か。</summary>
        public bool IsJunction => Ways.Count > 1;
        /// <summary>卵か。</summary>
        public bool IsGoal => Ways.Count == 0;

        /// <summary>⭐ **画面が数と絵を出すための代表の1件。**⚠️ 無ければ null。
        /// ⚠️ 画面に <c>switch (Kind)</c> を増やさないために在る。</summary>
        public Gift? Face =>
            OnLand.Count > 0 ? OnLand[0] : OnPay.Count > 0 ? OnPay[0] : null;
    }

    /// <summary>巣へ続く道。⭐ **分岐するすごろく**（作者の指示 2026-08-20）。
    ///
    /// ⚠️ **飛ばす遊び（<see cref="Steal"/>）と混ぜないこと。**
    /// あちらは移植元の規則で、較正済みの照合（`goldens/steal.json`）が踏んでいるので残してある。
    /// <see cref="Breeding"/> と <see cref="Fusion"/> の関係と同じ。
    ///
    /// ⭐ 形は**成り行き**（作者の指示 2026-08-20「もはや道は完全にランダムでもいいかもよ。
    /// 関門を置く位置さえ気を付ければ」）。段ごとの幅が 1〜4 の間をゆっくり動き、
    /// 隣の段へ 1〜2 本ずつ繋ぐだけ。
    /// <code>
    ///   ○─○─○─○
    ///   │╲│ │╲│      ← 段と段を素直に繋ぐ
    ///   ○ ○─▣ ○      ← ▣ が関門（**1マスとして数える**）
    ///    ╲___↗         ← 近道（1段飛ばし）。**その先は必ず関門**
    /// </code>
    ///
    /// ⭐ **気を配るのは関門の置き方だけ。**決まりは3つ:
    /// <list type="bullet">
    ///   <item>関門を1つも通らずに卵まで行ける道が残る</item>
    ///   <item>その道が**一番長い** ── 近道の先を必ず関門にすることで成り立つ</item>
    ///   <item>どのマスからも「関門でない1段先」がある ── 関門は**遠回りさせるだけ**で塞がない</item>
    /// </list>
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

        private readonly List<Square> _squares;

        public Trail(IReadOnlyList<Square> squares, int tier, IReadOnlyList<int> junctions)
        {
            _squares = new List<Square>(squares);
            Squares = _squares;
            Tier = tier;
            Junctions = junctions;
        }

        /// <summary>マスを差し替える。⚠️ **関門を置くときだけ**使う
        /// （置いてみて、通れる道が消えたら戻すため）。</summary>
        public void Swap(int at, Square square) => _squares[at] = square;

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

        /// <summary>速度いくつで1回振れるか。⭐ **188**（= 47 × 4体）。
        ///
        /// ⚠️ **この註は3体だった頃の数（140）のまま2日ずれていた**（2026-08-21 に直した）。
        /// ⭐ 較正の根拠は「参照編成の速度を割ると1段ずつ増えること」だったので、
        /// 数が変わったなら**根拠のほうを測り直す**。
        ///
        /// ⚠️ 実測（`sim trail` の「速度と距離の釣り合い」・2026-08-21）:
        /// 参照編成の速度合計は段1〜5で **957／1,120／1,278／1,441／1,623**。
        /// 188 で割ると **5／5／6／7／8** ── ⚠️ **段2で足踏みする**。
        /// ⭐ 140 を選んだときに避けたはずの症状が、4体化で戻っている。
        /// 🚧 直すかは釣り合いの判断（[釣り合い]）。**数は先にここへ合わせてある。**</summary>
        public static int SpeedPerRoll => SpeedPerRollEach * Games.PartySize;

        /// <summary>⭐ **1体あたり**の、さいころ1回ぶんの速度。
        /// ⚠️ 3体で 140 と較正した値を3で割った（140 ÷ 3 ≒ 47・2026-08-20 の4体化）。
        /// ⭐ 体数に連動させたのは、増やしたときに**さいころが勝手に増える**のを防ぐため。
        /// ⚠️ **ただし合計は 140 → 188 に 34% 上がっている**（3体ぶん→4体ぶん）ので、
        /// 「140 のときと同じ」ではない。上の <see cref="SpeedPerRoll"/> を見ること。</summary>
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

        /// <summary>盤の段数。⭐ **素で行くと届かない長さ**（2026-08-21・作者の指示
        /// 「素の状態でゴールする難易度を上げる」）。
        ///
        /// ⚠️ 短すぎるとどの指し手でも届き、長すぎると払っても届かない。
        /// ⭐ 段5（39段）での実測（2026-08-21・払う指し手を計測器に入れ直したあと）:
        /// **素 18% / 払う 72% / 関門を拾って払う 90% / 敵を拾って払う 69%**。
        /// ⚠️ 10 + 段×5（35段）だと素 31%・払う 82% で、
        /// 「払うのが当たり前」になって駆け引きが消えていた。</summary>
        public static int RowsFor(int tier) => 14 + tier * 5;

        /// <summary>1つの段に並ぶマスの最大。⚠️ 画面の幅（1080）で割れる数まで。</summary>
        public const int LanesMax = 4;

        /// <summary>引く筋の本数。⭐ **多いほど編み目が細かくなる。**
        ///
        /// ⭐ 盤は「幅を決めてから繋ぐ」のではなく **筋を引いて、通らなかったマスを捨てる**
        /// （2026-08-21・`Slay the Spire` のマップ生成の作りを見て置き換えた）。
        /// ⚠️ 幅から作ると、幅そのものが乱数になって盤が**塊**に見える。
        /// ⭐ 筋から作ると幅は結果になり、**編み目**に見える。
        /// ⚠️ 孤立したマスも出ない（どのマスも必ずどれかの筋の上に在る）ので、
        /// 以前あった「誰からも来られないマスを繋ぎ直す修理」も
        /// 「幅1が続くのを見張る `ThinRun`」も要らなくなった。</summary>
        public static int PathsFor(int tier) => 4 + tier;

        /// <summary>×型に交わった繋ぎを**両方**外す割合（%）。
        /// ⭐ 残りは片方だけ外す。⚠️ いつも同じ側を外すと、盤に癖が出る。</summary>
        public const int CrossBoth = 20;

        // ── マスの出方 ──────────────────────────────
        // ⭐ **ここが「盤に何が並ぶか」の唯一の出所。**
        // ⚠️ 新しいマスを足すときは、この並びに1行と <see cref="SquareKind"/> に1つ、
        //    そして <see cref="Trails.Born"/> に1件。**盤の形の側は1行も触らない。**

        /// <summary>入口側での出方（%）。⚠️ 合計 100。</summary>
        public const int PlainShare = 38;
        public const int MobShare = 12;
        public const int BoonShare = 14;
        public const int BaneShare = 12;
        public const int GateShare = 24;

        /// <summary>一番奥の段での上乗せ（%）。⭐ **奥ほど濃い。**
        /// ⚠️ 入口の近くで敵に当たると、まだ何も拾っていないまま終わってしまう。
        /// ⚠️ 上乗せしたぶんは <see cref="PlainShare"/> から引く（合計は 100 のまま）。</summary>
        public const int DeepMob = 10;
        public const int DeepGate = 6;

        /// <summary>⭐ **関門の段の数。**1〜5。</summary>
        public const int GateGrades = 5;

        /// <summary>段の出方。⭐ **小さい段ほど多い。**⚠️ 一様に散らすと、
        /// 1回の潜入でもらえる回数が多くなりすぎる（実測 2026-08-21: 素 31% に対して
        /// 払うと 85% ＝ 払うのが当たり前になり、駆け引きが消えた）。
        /// ⭐ 大きい段を稀にすると、そこが**財布を空にするか迷う一場面**になる。</summary>
        public static readonly int[] GradeShares = { 40, 26, 18, 11, 5 };

        /// <summary>段1つぶんの**払う量**（参照編成の持ち分に対する %）。
        ///
        /// ⭐ 段1 = **14%** … 段5 = **70%**（段 × 14%）。
        /// ⚠️ この註は 8%…40% と書いてあった ── <c>TollShare</c> を 8 から 14 へ
        /// 上げたときに数だけ置き去りになっていた（2026-08-21 に直した）。
        ///
        /// ⚠️ **検査ではなく消費**なので、2026-08-20 までの 25%刻み（段5 で 125%）とは
        /// 物差しが違う。あの値は「持っているか」を見るためのもので、
        /// そのまま消費に使うと**1回払って空になる**（実測: 持ち分 1,867 に対し 段2 で 950）。
        ///
        /// ⭐ 実測（`sim trail`・段5・攻の場合・2026-08-21）:
        /// 持ち分 1,867 を払い切ると 段1 で 7回・段2 で 3回・段3 で 2回・段4 と段5 は 1回。
        /// ⭐ 1回の潜入で実際に払う回数は **2.5回**（近い道）〜 **4.8回**（関門を拾う道）。</summary>
        public const int TollShare = 14;

        /// <summary>対価の**回数**。⭐ 段2つで1回（1,1,2,2,3）。
        ///
        /// ⚠️ 段に**そのまま**比例させると、1回の潜入で +4.4回 も増えて
        /// 「払えば当たり前に届く」になった（実測 2026-08-21: 素 31% → 払う 85%）。
        /// ⭐ **速度投資の代わり**であって、上位互換ではない ── という狙いの量。
        /// ⚠️ 実測で確かめること（2026-08-21・註の数を計算で埋めて2日ずれた反省）:
        ///
        /// <list type="bullet">
        ///   <item>速度を「関門を1つも通らない」まで寄せると **段1 +1回 … 段5 +4回**</item>
        ///   <item>払って得る回数は **+2.0回**（近い道）── ⭐ ここは釣り合っている</item>
        ///   <item>⚠️ ただし**関門を拾いに行くと +3.9回 と +8.1マス**。
        ///     寄り道の費用がほぼ無い（どの繋ぎも1段しか進まない）ので、
        ///     この指し手だけ速度投資を追い越す。🚧 釣り合いの判断が要る</item>
        /// </list>
        ///
        /// ⚠️ 段が選べるなら「一番安い段だけ買う」が最善になるが、
        /// **どの段の関門に当たるかは盤が決める**ので、その計算は生まれない。</summary>
        public static int RollsForGrade(int grade) => (grade + 1) / 2;
        /// <summary>回数の代わりに距離をもらうとき、段1つぶんのマス数。
        /// ⚠️ さいころ1回の平均が 3.5 マスなので、これで回数とだいたい釣り合う。</summary>
        public const int TollHop = 2;
        /// <summary>対価が「距離」になる割合（%）。残りは「回数」。</summary>
        public const int TollHopShare = 40;

        /// <summary>⭐ 出す数の丸め。⚠️ 参照編成の持ち分は 1,141 のような半端な数なので、
        /// そのまま割ると読めない。**この単位に丸める**。</summary>
        public const int PriceRound = 50;

        // ── ▲▼ の効き ──────────────────────────────
        /// <summary>▲ の上げ幅（%）と、効く回数。</summary>
        public const int BoonLow = 30, BoonHigh = 60, BoonTurnsLow = 4, BoonTurnsHigh = 7;
        /// <summary>▼ の下げ幅（%）と、効く回数。⚠️ ▲ より軽く・短く。</summary>
        public const int BaneLow = 15, BaneHigh = 35, BaneTurnsLow = 2, BaneTurnsHigh = 3;

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

        /// <summary>⭐ **段から出す、払う量。**
        ///
        /// ⚠️ **消費の物差し**（2026-08-21）。2026-08-20 までは「持っているか」を見る
        /// 検査の物差しで、段5 が持ち分の 125% だった。⭐ 消費になった以上、
        /// あの値では**1回払って空になる**（持ち分 1,867 に対し 段2 が 950 だった）。
        /// ⚠️ 揺らぎを掛けない・丸める ── 盤に並ぶ数が読めることを優先する
        /// （2026-08-20・作者の指示「固定値にして段をつけたら」）。</summary>
        public static int PriceOfGrade(GimmickKind gate, int tier, int grade)
        {
            if (grade <= 0) return 0;
            int raw = RefStat(gate, tier) * grade * Trail.TollShare / 100;
            // ⭐ 読める単位に丸める。⚠️ 0 にはしない（只と混ざる）
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
        /// <summary>⭐ **払うか決めている。**関門のマスに止まり、払える量が在る。
        /// ⚠️ 払わなくてもよい（作者の指示 2026-08-21「払わなくても入れる」）。
        /// 呼び側は <see cref="Trails.Pay"/> か <see cref="Trails.Pass"/> を呼ぶ。</summary>
        Offered,
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
        /// <summary>編成の合計ステ。⭐ **潜入のあいだの財布。**
        /// ⚠️ ここ自体は減らない ── 使ったぶんは <see cref="Spent"/> が持つ。</summary>
        public StatBlock Pool;
        /// <summary>⭐ **関門で払ったぶん。**⚠️ その潜入のあいだ戻らない。
        ///
        /// ⭐ **ステに出口ができた**のが 2026-08-21 の変更の芯（作者の指示）。
        /// ⚠️ それまでステは「持っているか」を調べられるだけで減らなかったので、
        /// **使い道の無い資源**だった ── だから ▲ に止まりたくならなかった。</summary>
        public StatBlock Spent;
        /// <summary>もう払ったマス。⚠️ 二重に払わせない。</summary>
        public readonly HashSet<int> Paid = new HashSet<int>();
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
            Spent = new StatBlock(0, 0, 0, 0);
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

        /// <summary>関門に払える量の**元**。⭐ **編成ぜんぶ**（<see cref="Games.PartySize"/> 体）を足す。
        ///
        /// ⚠️ 註が「3体ぶん」のままだった（2026-08-21 に直した）── 4体化しても
        /// この関数は体数を数えていないので**実装は正しく、註だけがずれていた**。
        /// ⭐ 体数を書かない形にしてある（<c>foreach</c>）ので、増やしても壊れない。
        ///
        /// ⚠️ **いま払える額はここではない。**払ったぶんと一時増減を入れた
        /// <see cref="Usable"/> のほうが「出せる額」。ここは素の持ち分。
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
            // ⚠️ **払ったぶんを引く**（2026-08-21）。関門は消費になった
            int had = raid.Pool[key] - raid.Spent[key];
            if (had < 0) had = 0;
            int pct = raid.TempLeft[key] > 0 ? raid.Temp[key] : 0;
            long v = (long)had * (100 + pct) / 100;
            return v < 0 ? 0 : (int)v;
        }

        /// <summary>その関門を**いま払えるか**。⚠️ 払えなくても入れる。</summary>
        public static bool CanPay(Raid raid, int square)
        {
            var toll = raid.Trail.Squares[square].Toll;
            return toll != null && !raid.Paid.Contains(square)
                && Usable(raid, StatOf(toll.Kind)) >= toll.Price;
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
            // ⭐ **段ごとに好きな幅で並べ、隣の段へ素直に繋ぐ。**
            //    （2026-08-20・作者の指示「もはや道は完全にランダムでもいいかもよ。
            //      関門を置く位置さえ気を付ければ」）
            // ⚠️ 以前は「分かれて／合流して」を繰り返す作りで、盤がどこも同じ顔になり、
            //    しかも合流のたびに1マスへ集まって不自然だった。
            // ⭐ いまは幅が段ごとにゆっくり変わるだけ。関門は**置き方だけ**を見張る。
            int rows = Trail.RowsFor(tier);
            int cols = Trail.LanesMax;
            int mid = cols / 2;

            // ── ① 筋を引く ──────────────────────────────
            // ⭐ 格子（段 × 列）の上を、入口から卵まで**乱歩する筋**を何本も引く。
            // ⚠️ 幅を先に決めない ── 幅は筋の結果として決まる。
            var used = new bool[rows + 1, cols];
            var links = new HashSet<int>();
            used[0, mid] = true;
            used[rows, mid] = true;
            int paths = Trail.PathsFor(tier);
            for (int n = 0; n < paths; n++)
            {
                int col = mid;
                for (int r = 0; r < rows; r++)
                {
                    // ⭐ 入口からは**どの列へも**開く（真ん中から扇に広がる）。
                    // ⚠️ ここを ±1 にすると、端の列が最初の数段だけ空いて不格好になる。
                    // ⚠️ 1本目と2本目は**必ず違う列へ**出す ── そうしないと
                    //    「分かれ道が1つも無い」盤が出うる（前の作りで実際に出た）。
                    int next = r == 0
                        ? (n < cols ? n : rng.Int(0, cols))
                        : col + rng.Int(0, 3) - 1;
                    if (next < 0) next = 0;
                    if (next >= cols) next = cols - 1;
                    // ⚠️ **卵へ寄せる漏斗。**残りの段より横に離れていると、もう戻れない
                    int room = rows - (r + 1);
                    if (next - mid > room) next = mid + room;
                    if (mid - next > room) next = mid - room;
                    used[r + 1, next] = true;
                    links.Add(Bond(r, col, next, cols));
                    col = next;
                }
            }

            // ── ② 通らなかったマスを捨てる ────────────────
            var squares = new List<Square>();
            var at = new int[rows + 1, cols];
            for (int r = 0; r <= rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    at[r, c] = -1;
                    if (!used[r, c]) continue;
                    at[r, c] = squares.Count;
                    var sq = r == 0 || r == rows ? new Square() : Born(rng, tier, r, rows);
                    sq.Row = r;
                    // ⭐ 入口と卵は真ん中に据える（列の数が偶数だと中心が無いため）
                    sq.Lane = r == 0 || r == rows ? 0 : LaneOf(c, cols);
                    squares.Add(sq);
                }

            // ── ③ 繋ぐ ──────────────────────────────────
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    for (int d = 0; d < cols; d++)
                    {
                        if (!links.Contains(Bond(r, c, d, cols))) continue;
                        squares[at[r, c]].Ways.Add(new Way(at[r + 1, d]));
                    }

            // ── ④ ×型に交わった繋ぎをほどく ─────────────
            Untangle(rng, squares, at, rows, cols);

            // ⚠️ **関門を後から置かない**（2026-08-21）。関門はマスの種類の1つになったので、
            //    他のマスと同じく <see cref="Born"/> が出す。
            // ⭐ おかげで**盤の形を作る側は、マスの種類を1つも知らない**。
            //    ⚠️ 知っていた頃（「関門なしの道を必ず残す」を守らせていた頃）は、
            //    そこから詰みの不具合が3つ出た。
            return new Trail(squares, tier, Forks(squares));
        }

        /// <summary>分かれ道（行き先が2つ以上あるマス）の並び。</summary>
        private static List<int> Forks(IReadOnlyList<Square> squares)
        {
            var forks = new List<int>();
            for (int i = 0; i < squares.Count; i++)
                if (squares[i].IsJunction) forks.Add(i);
            return forks;
        }

        /// <summary>格子の列を、どの車線に置くか。⭐ **列は段によらず同じ場所**。
        /// ⚠️ 段ごとに均等割りすると、同じ筋のマスが段ごとに横へ跳ねる。</summary>
        private static int LaneOf(int col, int cols) =>
            cols <= 1 ? 0 : -Trail.LaneEdge + col * (Trail.LaneEdge * 2) / (cols - 1);

        /// <summary>段と列の組を1つの数にする。⭐ 引いた筋を覚えておくための鍵。</summary>
        private static int Bond(int row, int from, int to, int cols) =>
            (row * cols + from) * cols + to;

        /// <summary>×型に交わった繋ぎをほどく。
        ///
        /// ⭐ 隣り合う2列で「左→右上」と「右→左上」が同時にあると、線が交わって
        /// **どこへ行けるのか目で追えなくなる**。
        /// ⭐ 先に真っ直ぐの繋ぎを足してから、斜めを外す ── 足してから外すので、
        /// ⚠️ **どのマスも行き先と来し方を失わない**。</summary>
        private static void Untangle(Rng rng, List<Square> squares, int[,] at, int rows, int cols)
        {
            for (int r = 0; r < rows; r++)
                for (int c = 0; c + 1 < cols; c++)
                {
                    int a0 = at[r, c], b0 = at[r, c + 1];
                    int a1 = at[r + 1, c], b1 = at[r + 1, c + 1];
                    if (a0 < 0 || b0 < 0 || a1 < 0 || b1 < 0) continue;
                    if (!squares[a0].Ways.Exists(w => w.To == b1)) continue;
                    if (!squares[b0].Ways.Exists(w => w.To == a1)) continue;

                    // ⭐ 先に真っ直ぐを足す
                    if (!squares[a0].Ways.Exists(w => w.To == a1)) squares[a0].Ways.Add(new Way(a1));
                    if (!squares[b0].Ways.Exists(w => w.To == b1)) squares[b0].Ways.Add(new Way(b1));

                    // ⭐ そのうえで斜めを外す。⚠️ いつも同じ側だと盤に癖が出る
                    int roll = rng.Int(0, 100);
                    if (roll < Trail.CrossBoth || roll < 60)
                        squares[a0].Ways.RemoveAll(w => w.To == b1);
                    if (roll < Trail.CrossBoth || roll >= 60)
                        squares[b0].Ways.RemoveAll(w => w.To == a1);
                }
        }

        /// <summary>⭐ **マスを1つ生む。ここが「盤に何が並ぶか」の唯一の出所。**
        ///
        /// ⭐ **新しいマスを足すときに触るのはここだけ**（作者の指示 2026-08-21
        /// 「今後マスに追加機能を足しても破綻しない作りにすること」）:
        /// <list type="bullet">
        ///   <item><see cref="SquareKind"/> に1つ足す（絵のため）</item>
        ///   <item>この関数に1件足す（何をくれるか）</item>
        ///   <item>割合の決まりを1つ足す</item>
        /// </list>
        /// ⚠️ **盤の形（<see cref="Make"/>）も、進行（<see cref="Land"/>）も触らない。**
        /// 形はマスの種類を知らず、進行は <see cref="Gift"/> しか見ないため。
        ///
        /// ⭐ **奥ほど濃い。**⚠️ 入口の近くで敵に当たると、
        /// まだ何も拾っていないまま終わってしまう。</summary>
        private static Square Born(Rng rng, int tier, int row, int rows)
        {
            int deep = rows <= 1 ? 0 : row * 100 / rows;
            int mob = Trail.MobShare + deep * Trail.DeepMob / 100;
            int gate = Trail.GateShare + deep * Trail.DeepGate / 100;

            int roll = rng.Int(0, 100);
            if ((roll -= mob) < 0) return Square.Mob();
            if ((roll -= gate) < 0) return Tollgate(rng, tier);
            if ((roll -= Trail.BoonShare) < 0)
                return Swing(rng, Trail.BoonLow, Trail.BoonHigh,
                    Trail.BoonTurnsLow, Trail.BoonTurnsHigh, up: true);
            if ((roll -= Trail.BaneShare) < 0)
                return Swing(rng, Trail.BaneLow, Trail.BaneHigh,
                    Trail.BaneTurnsLow, Trail.BaneTurnsHigh, up: false);
            // ⭐ 残りは素通り。⚠️ 消さないこと ── 「どれも要らない」を選べる先が要る
            return new Square();
        }

        /// <summary>▲ / ▼ のマス。</summary>
        private static Square Swing(Rng rng, int low, int high,
            int turnsLow, int turnsHigh, bool up)
        {
            int percent = low + rng.Int(0, (high - low) / 5 + 1) * 5;
            int turns = turnsLow + rng.Int(0, turnsHigh - turnsLow + 1);
            return Square.Swing(PickStat(rng), up ? percent : -percent, turns);
        }

        /// <summary>⭐ **関門のマス。払うと対価がもらえる。**
        ///
        /// ⚠️ 払わなくても入れる（作者の指示 2026-08-21）。
        /// ⭐ 対価は「振れる回数」か「その場で進むマス数」。どちらも段に比例するので、
        /// **交換の率は段によらず一定** ── 「どの段が得か」の計算が生まれない。</summary>
        private static Square Tollgate(Rng rng, int tier)
        {
            var kinds = new[] { GimmickKind.Wall, GimmickKind.Damage, GimmickKind.Pressure };
            var kind = kinds[rng.Int(0, kinds.Length)];
            int grade = Grade(rng);
            var toll = new Toll(kind, Trail.PriceOfGrade(kind, tier, grade), grade);
            var gift = rng.Int(0, 100) < Trail.TollHopShare
                ? new Gift(GiftKind.Hop, grade * Trail.TollHop)
                : new Gift(GiftKind.Rolls, Trail.RollsForGrade(grade));
            return Square.Gate(toll, gift);
        }

        /// <summary>段を引く。⭐ **小さい段ほど多い。**</summary>
        private static int Grade(Rng rng)
        {
            int total = 0;
            foreach (int share in Trail.GradeShares) total += share;
            int roll = rng.Int(0, total);
            for (int i = 0; i < Trail.GradeShares.Length; i++)
                if ((roll -= Trail.GradeShares[i]) < 0) return i + 1;
            return Trail.GateGrades;
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
            // ⚠️ **振る前に道を選ばせない**（2026-08-20・作者の指示
            //    「さいころを回した後に分岐を選ぶようにして」）。
            // ⭐ 入口が分かれ道でも、まず振る ── 行ける先は振ってから並べる。
            // ⚠️ 以前は「どの道にも入れないなら、ここで終わり」を見ていたが、
            //    関門が只で入れるようになったので**入れない道はもう無い**（2026-08-21）。
            var raid = new Raid(trail, party, RollsFor(party, raids), PoolOf(party));
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
            // ⚠️ **通れない道はもう無い**（2026-08-21・関門は只で入れるようになった）。
            //    ⭐ だから「行き止まりで止まる」の手当ても要らない。
            foreach (var way in here.Ways)
            {
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

            // ⚠️ **出目と歩数が食い違ったら、そこで止める**（作者の報告 2026-08-22
            //    「さいころの目が6を表示しているのに1しか進めない」）。
            // ⭐ 食い違いは**黙って進む**のが一番たちが悪い ── 何が起きたか誰も分からない。
            // ⚠️ 卵に着いたときだけは短くてよい（`Walk` が行き過ぎないように止める）。
            // ⚠️ **`Pending > 0` を条件にしない**（2026-08-22 に外した）。
            //    ⭐ ここへ来られるのは `Choosing` のときだけで、`Choosing` は
            //    `Pending > 0` でしか立たない ── つまり 0 で来ること自体が異常。
            //    ⚠️ 条件を付けていると、その異常だけが**見張りをすり抜けて**
            //    「1マスだけ進む」として通ってしまう。
            int steps = path.Count - 1;
            bool stopped = raid.Trail.Squares[path[path.Count - 1]].IsGoal;
            if (steps != raid.Pending && !stopped)
            {
                throw new InvalidOperationException(
                    $"出目 {raid.Pending} なのに {steps} マスぶんの道筋を渡された");
            }

            for (int n = 0; n + 1 < path.Count; n++)
            {
                var ways = raid.Trail.Squares[path[n]].Ways;
                int took = -1;
                for (int k = 0; k < ways.Count; k++)
                {
                    if (ways[k].To != path[n + 1]) continue;
                    took = k;
                    break;
                }
                if (took < 0) throw new ArgumentException("繋がっていない道筋", nameof(path));
                // ⭐ 分かれ道だけ覚える（1本道は覚えるまでもない）
                if (ways.Count > 1) raid.Took[path[n]] = took;
                raid.At = path[n + 1];
            }

            raid.Pending = 0;
            return Land(raid);
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
            // ⭐ **倒してから配る。**⚠️ 負けた側（<see cref="Lost"/>）には配らない
            foreach (var gift in raid.Trail.Squares[raid.At].OnWin) Give(raid, gift);
            return Offer(raid);
        }

        /// <summary>雑魚に負けた。⚠️ そこで見つかる。</summary>
        public static RaidStep Lost(Raid raid)
        {
            Require(raid, RaidStep.Met);
            raid.Result = StealOutcome.Blocked;
            return raid.Step = RaidStep.Caught;
        }

        /// <summary>⚠️ **1マスも動けない。**
        /// ⭐ 2026-08-21 以降は起きない（関門が道を塞がなくなった）。安全網として残す。</summary>
        public static RaidStep Stuck(Raid raid)
        {
            raid.Result = StealOutcome.Blocked;
            return raid.Step = RaidStep.Caught;
        }

        // ── 払う ────────────────────────────────────

        /// <summary>⭐ **払う。**⚠️ 払えるときだけ呼べる。</summary>
        public static RaidStep Pay(Raid raid)
        {
            Require(raid, RaidStep.Offered);
            var here = raid.Trail.Squares[raid.At];
            var toll = here.Toll;
            if (toll == null) throw new InvalidOperationException("払うものが無い");
            if (!CanPay(raid, raid.At)) throw new InvalidOperationException("払えない");

            var key = StatOf(toll.Kind);
            raid.Spent = raid.Spent.With(key, raid.Spent[key] + toll.Price);
            raid.Paid.Add(raid.At);
            foreach (var gift in here.OnPay) Give(raid, gift);
            return Onward(raid);
        }

        /// <summary>⭐ **払わない。**⚠️ 何も起きずに先へ進む。</summary>
        public static RaidStep Pass(Raid raid)
        {
            Require(raid, RaidStep.Offered);
            return Onward(raid);
        }

        // ── 止まったマスを片付ける ──────────────────

        /// <summary>⭐ **止まったマスがくれるものを配り、次に何を待つか決める。**
        ///
        /// ⚠️ **ここに <c>switch (Kind)</c> を書かない。**マスの種類ではなく
        /// <see cref="Gift"/> の並びを見る ── だから新しいマスを足しても、
        /// この関数は1行も変わらない（作者の指示 2026-08-21）。</summary>
        private static RaidStep Land(Raid raid)
        {
            var here = raid.Trail.Squares[raid.At];
            if (here.IsGoal)
            {
                raid.Result = StealOutcome.Success;
                return raid.Step = RaidStep.Reached;
            }
            foreach (var gift in here.OnLand)
            {
                Give(raid, gift);
                // ⚠️ **戦闘が立ったら、そこで止める。**⭐ 続きを配ると、
                //    負けても報酬が残る（2026-08-21 の監査で実測）。
                //    ⭐ 倒してから配る物は <see cref="Square.OnWin"/> に書く。
                if (raid.Step == RaidStep.Met) return raid.Step;
            }
            return Offer(raid);
        }

        /// <summary>払えるものが在れば、払うかを訊く。</summary>
        private static RaidStep Offer(Raid raid) =>
            CanPay(raid, raid.At) ? raid.Step = RaidStep.Offered : Onward(raid);

        /// <summary>次に何を待つか。</summary>
        private static RaidStep Onward(Raid raid)
        {
            // ⭐ もらった距離が残っていれば、振らずにもう一度選ばせる
            if (raid.Pending > 0) return raid.Step = RaidStep.Choosing;
            if (raid.Rolls <= 0)
            {
                raid.Result = StealOutcome.Stalled;
                return raid.Step = RaidStep.Caught;
            }
            return raid.Step = RaidStep.Moved;
        }

        /// <summary>⭐ **もらったものを1つ配る。ここが唯一の適用場所。**
        ///
        /// ⚠️ 新しい効き目を足すときに触るのは、ここに1件だけ。</summary>
        private static void Give(Raid raid, Gift gift)
        {
            switch (gift.Kind)
            {
                case GiftKind.Rolls:
                    raid.Rolls += gift.Amount;
                    break;

                case GiftKind.Hop:
                    // ⭐ **もう N マス進める。**⚠️ 行き先は今までどおり選ばせる
                    //    （Choosing に戻るだけなので、画面に新しい仕掛けが要らない）。
                    raid.Pending += gift.Amount;
                    break;

                case GiftKind.Stat:
                    // ⭐ 上書きする（重ねない）。⚠️ 重ねると桁が読めなくなる
                    raid.Temp = raid.Temp.With(gift.Stat, gift.Amount);
                    raid.TempLeft = raid.TempLeft.With(gift.Stat, gift.Turns);
                    break;

                case GiftKind.Fight:
                    // ⚠️ 倒した相手とは二度と戦わない
                    if (!raid.Beaten.Contains(raid.At)) raid.Step = RaidStep.Met;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(gift), gift.Kind, "知らないもらい物");
            }
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
        /// ⚠️ **関門は数に入れない**（2026-08-21 から只で入れる）。
        /// ⭐ 画面に出すのは「いちばん短く行けるとしたら何マスか」。</summary>
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

        /// <summary>そのマスから卵まで、あと何マスか。⚠️ -1 は「その先が詰む」。
        /// ⭐ 関門を**通れるかは見ない**（盤の形だけ）── 画面が「最短マス数」を出すのに使う。</summary>
        public static int LeftFrom(Trail trail, int from)
        {
            var steps = new int[trail.Count];
            for (int i = 0; i < steps.Length; i++) steps[i] = -1;
            steps[trail.Goal] = 0;
            for (int i = trail.Count - 2; i >= 0; i--)
            {
                foreach (var way in trail.Squares[i].Ways)
                {
                    if (steps[way.To] < 0) continue;
                    int here = steps[way.To] + 1;
                    if (steps[i] < 0 || here < steps[i]) steps[i] = here;
                }
            }
            return steps[from];
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

    }
}
