using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>卵強奪。⭐ **すごろく**（作者の指示 2026-08-20）。
    ///
    /// 速度の合計で振れる回数が決まり、さいころで道を進む。途中の分かれ道は
    /// ステを払えば壊せて先へ飛べる。振り切って卵に届かなければ親が帰ってくる。
    ///
    /// ⚠️ 弾いて飛ばす遊び（<see cref="StealScreen"/>）は**別物として残してある**。
    /// あちらは移植元の規則で、較正済みの照合が踏んでいる。
    ///
    /// ⭐ 画面の作り。⚠️ 判断に要る数を**3つだけ**上に出す:
    /// <list type="bullet">
    /// <item>あと何回振れるか ── 尽きたら見つかる</item>
    /// <item>力 ── 分かれ道を壊すのに払う</item>
    /// <item>届く見込み ── このまま歩いたら間に合うか</item>
    /// </list>
    ///
    /// ⭐ 色は**1色だけ**（<see cref="Ui.Accent"/>）。いま居るマスと、
    /// **いま払える分かれ道**にだけ点く。⚠️ 関門の種類は色でなく字（攻/HP/防）で示す
    /// ── 色を3つに割ると、どれが「押せる」印なのか読めなくなる。</summary>
    public static class TrailScreen
    {
        // ── 寸法 ────────────────────────────────────
        /// <summary>上の帯の高さ。⚠️ 中の段（見出し 16 / 数字 46〜122 / 値引き 152〜200）を
        /// 全部収める。⭐ 変えるときは <see cref="Header"/> の段も一緒に見る。</summary>
        private const float HeaderHeight = 220f;
        /// <summary>下の操作帯。⭐ **状態が変わっても高さを変えない。**
        /// ⚠️ 変えると盤が上下に跳ねて、さっき見ていたマスを見失う。</summary>
        private const float DockHeight = 392f;
        /// <summary>試す列の数。⭐ **マスが一番大きくなる並び**を採る。
        ///
        /// ⚠️ 5列に決め打ちすると、段1（19マス）で 4段にしかならず盤の下半分が空いた
        /// （実機で確認 2026-08-20）。道の長さは段で変わるので、列も一緒に動かす。</summary>
        private static readonly int[] ColumnChoices = { 4, 5, 6 };
        private const float CellGap = 10f;
        /// <summary>上の帯と盤のあいだ。⚠️ 線で区切らず**余白で離す**。</summary>
        private const float GroupGap = 14f;
        /// <summary>卵の帯の高さ。⭐ 盤の一番上に**横いっぱい**で置く。
        /// ⚠️ 1マスとして並べると最上段に1つだけ浮いて、行き先に見えなかった（実機で確認）。</summary>
        private const float GoalHeight = 104f;

        private static readonly Color Board = new Color(0.04f, 0.06f, 0.10f, 0.55f);
        private static readonly Color Plate = new Color(1f, 1f, 1f, 0.86f);
        private static readonly Color PlateGone = new Color(1f, 1f, 1f, 0.34f);
        /// <summary>マスとマスを繋ぐ線。⚠️ 薄いと蛇行の向きが読めない（実機で確認）。</summary>
        private static readonly Color Road = new Color(1f, 1f, 1f, 0.44f);

        /// <summary>告知を出して次へ渡している最中。⚠️ 組み直すたびに作らないための札。</summary>
        private static bool _handing;
        /// <summary>さいころを回している最中。⚠️ 二重に振らせない。</summary>
        private static bool _rolling;
        /// <summary>その札を立てたときの潜入。
        ///
        /// ⚠️ 札は static なので、潜入が別物に差し替わっても取り残される。
        /// 取り残されると <see cref="Build"/> が決着を出さないまま黙り、
        /// 画面が動かなくなる（実機で確認 2026-08-20）。
        /// ⭐ 誰の札かを持たせて、違う潜入になったら下ろす。</summary>
        private static Raid _flagged;

        /// <summary>巣を選んで潜入へ。
        ///
        /// ⚠️ 道は <see cref="Trails.OfNest"/> ＝ **巣ごとに固定**。挑むたびに引き直すと、
        /// 画面を出入りするだけで道を選び直せてしまう。
        /// ⭐ 固定だからこそ「壁の多い巣には攻撃に寄せた編成で行く」が成り立つ。</summary>
        public static void Enter(App app, Nest nest)
        {
            app.CurrentNest = nest;
            app.CurrentIsBoss = false;
            _handing = false;
            _rolling = false;
            _flagged = null;

            int raids = Games.RaidsOn(app.Game, nest);
            // ⚠️ 守りが最大の巣は**入れないのではなく、入れば戦闘**（[巣の寿命]）。
            //    ⭐ 行き止まりではない ── そこで勝てば最後の卵が手に入る
            if (Steal.IsSealed(raids))
            {
                app.Raid = null;
                // ⚠️ ここは Trail 画面を出さない（探索の上に告知を出すだけ）ので、
                //    見張りは「まだその巣を見ているか」で掛ける
                BannerView.Show(app.Overlay, "親が道をふさいでいる！", () =>
                {
                    if (!ReferenceEquals(app.CurrentNest, nest)) return;
                    app.EnterBattle(nest, false);
                });
                return;
            }

            app.Raid = Trails.Begin(Trails.OfNest(nest), Games.PartyOf(app.Game), raids);
            _flagged = app.Raid;
            app.Show(Screen.Trail);
        }

        public static void Build(App app, RectTransform body)
        {
            var raid = app.Raid;
            if (raid == null) { app.Show(Screen.Nests); return; }

            // ⚠️ 前の潜入の札を持ち越さない
            if (!ReferenceEquals(_flagged, raid)) { _handing = false; _rolling = false; }

            float boardTop = HeaderHeight + GroupGap;
            float boardHeight = Ui.H - Ui.TopBarHeight - boardTop - DockHeight;

            Header(app, body, raid);
            var cells = BoardOf(app, body, raid, boardTop, boardHeight);
            Dock(app, body, raid, cells);

            // ── 決着と雑魚 ──────────────────────────
            // ⚠️ ここに押しどころを置かない。届いたなら持ち帰るしかないし、
            //    見つかったなら戦うしかない。選択肢でないものを押させない。
            if (_handing || _rolling) return;

            if (raid.Step == RaidStep.Met) { Meet(app, raid); return; }
            if (raid.Result != null) Finish(app, raid);
        }

        // ── 上の帯 ──────────────────────────────────

        /// <summary>判断に要る数。⭐ **回数・力・見込みの3つだけ。**
        ///
        /// ⚠️ 均等に並べない。⭐ 一番効くのは「あと何回振れるか」なので、そこだけ大きく出す。</summary>
        private static void Header(App app, RectTransform body, Raid raid)
        {
            var strip = Ui.Block(body, "Header", Board, 0f, 0f, Ui.W, HeaderHeight);
            var faint = new Color(1f, 1f, 1f, 0.62f);

            // ⚠️ 段（top）は**必ず前の段の下端から**取る。目分量で重ねると、
            //    字の範囲で比べる検査に落ちる（実際 2件落ちた 2026-08-20）
            // ⚠️ 字は器からはみ出して描かれる（Ui.Label は縦のはみ出しを許す）ので、
            //    段の高さは**字の実寸**で取る。器の高さで詰めると被る（実測 2026-08-20）。
            const float CapTop = 14f, CapHigh = 24f;
            const float BigTop = 40f, BigHigh = 72f;        // 40〜112
            const float SubTop = 116f, SubHigh = 30f;       // 116〜146
            const float PillTop = 150f, PillHigh = 48f;     // 150〜198

            // ── あと何回振れるか（左・主役） ──────────
            Ui.Label(strip, "RollsCap", "のこり", 24, faint,
                TextAnchor.LowerLeft, Ui.Margin, CapTop, 200f, CapHigh);
            var rolls = Ui.Label(strip, "Rolls", raid.Rolls.ToString(), 60,
                raid.Rolls <= 1 ? Ui.Accent : Color.white,
                TextAnchor.UpperLeft, Ui.Margin, BigTop, 100f, BigHigh);
            rolls.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Label(strip, "RollsUnit", "回", 28, faint,
                TextAnchor.LowerLeft, Ui.Margin + 106f, BigTop, 70f, BigHigh);

            // ── 届く見込み（右） ────────────────────
            // ⭐ **隠さない。**壊すか歩くかを決めるのはこの数字なので、材料は全部出す。
            // ⚠️ 歩き通した場合だけを出すと、盗まれた巣では 0% になり「もう詰んだ」に見える
            //    ── 分かれ道を壊せば届くのに。⭐ **2つ並べて、差そのものを見せる**
            // ⚠️ 使い残した目はここで足す。Core 側では足さない（両方でやると二重に数える）
            int carried = raid.Step == RaidStep.AtFork ? raid.Pending : 0;
            int bare = Trails.Odds(raid, carried);
            int spent = Trails.Odds(raid, carried + Trails.Sparable(raid));

            const float OddsWide = 260f;
            float oddsLeft = Ui.W - Ui.Margin - OddsWide;
            Ui.Label(strip, "OddsCap", "とどく", 24, faint,
                TextAnchor.LowerRight, oddsLeft, CapTop, OddsWide, CapHigh);
            var odds = Ui.Label(strip, "Odds", $"{bare}%", 48,
                bare < 40 ? Ui.Accent : Color.white,
                TextAnchor.UpperRight, oddsLeft, BigTop, OddsWide, BigHigh);
            odds.horizontalOverflow = HorizontalWrapMode.Overflow;
            // ⭐ 「壊せば」は目安なので小さく添える。⚠️ 同じ数なら出さない（読む物を増やさない）
            if (spent > bare)
                Ui.Label(strip, "OddsSpent", $"壊せば {spent}%", 22, Ui.Accent,
                    TextAnchor.UpperRight, oddsLeft, SubTop, OddsWide, SubHigh);

            // ── 力（分かれ道を壊すのに払う・中） ────────
            float barLeft = Ui.Margin + 220f;
            float barWide = oddsLeft - barLeft - 24f;
            Ui.Label(strip, "PowerCap", "ちから", 24, faint,
                TextAnchor.LowerLeft, barLeft, CapTop, barWide, CapHigh);
            int had = raid.Pool.Atk + raid.Pool.Hp + raid.Pool.Def;
            Ui.Bar(strip, "Power", had <= 0 ? 0f : (float)raid.Power / had, Ui.Accent,
                barLeft, BigTop + 4f, barWide, 18f);
            Ui.Label(strip, "PowerNum", Ui.Digits(raid.Power), 30, Color.white,
                TextAnchor.UpperLeft, barLeft, BigTop + 30f, barWide, 40f);

            // ── 値引き（なぜその値段なのか） ──────────
            // ⭐ 寄せたステの関門は安い。⚠️ ここを出さないと、値段が理不尽に見える
            float pillWide = (Ui.W - Ui.Margin * 2f - 24f) / 3f;
            var gates = new[] { GimmickKind.Wall, GimmickKind.Damage, GimmickKind.Pressure };
            for (int i = 0; i < gates.Length; i++)
            {
                int slant = Trails.SlantOf(raid, gates[i]);
                var pill = Ui.Block(strip, $"Slant {i}",
                    new Color(1f, 1f, 1f, slant >= 115 ? 0.20f : 0.09f),
                    Ui.Margin + (pillWide + 12f) * i, PillTop, pillWide, PillHigh);
                Ui.Label(pill, "T", $"{GateName(gates[i])} {StatName(gates[i])} {slant}%", 24,
                    slant >= 115 ? Ui.Accent : new Color(1f, 1f, 1f, slant >= 85 ? 0.78f : 0.45f),
                    TextAnchor.MiddleCenter, 0f, 0f, pillWide, PillHigh);
            }
        }

        // ── 盤 ──────────────────────────────────────

        /// <summary>マスを蛇行に並べる。⭐ **入口が左下、卵が上。**
        ///
        /// ⚠️ 一直線に並べるとスクロールが要る。スクロールすると
        /// 「あと何マス」が一目で読めなくなる ── 盤は**必ず1画面に収める**。
        /// ⭐ 戻り値はマスの矩形（駒と行き先の印を後から重ねるため）。</summary>
        private static RectTransform[] BoardOf(App app, RectTransform body, Raid raid,
            float top, float height)
        {
            var ground = Ui.Block(body, "Board", Board, 0f, top, Ui.W, height);

            int walk = raid.Trail.Length;                       // 歩くマス（卵は別）
            float gridTop = GoalHeight + CellGap * 2f;
            float roomH = height - gridTop - 16f;
            float roomW = Ui.W - Ui.Margin * 2f;

            int columns = ColumnChoices[0];
            int rows = 1;
            float cellW = 0f, cellH = 0f, best = -1f;
            foreach (int cols in ColumnChoices)
            {
                int lines = Mathf.CeilToInt((float)walk / cols);
                float w = (roomW - CellGap * (cols - 1)) / cols;
                // ⚠️ 縦に伸ばさない（横長のマスは字が散る）。⭐ 正方までで頭打ち
                float h = Mathf.Min(w, (roomH - CellGap * (lines - 1)) / lines);
                if (h <= 0f || w * h <= best) continue;
                best = w * h;
                columns = cols; rows = lines; cellW = w; cellH = h;
            }

            float boardH = cellH * rows + CellGap * (rows - 1);
            float padTop = gridTop + (height - gridTop - boardH) / 2f;

            var cells = new RectTransform[walk + 1];
            var spots = new Vector2[walk + 1];

            for (int i = 0; i < walk; i++)
            {
                int row = i / columns;                          // 0 が入口の段
                int col = i % columns;
                // ⭐ 蛇行。⚠️ 奇数段は右から左（すごろくの読み順）
                if (row % 2 == 1) col = columns - 1 - col;
                float left = Ui.Margin + col * (cellW + CellGap);
                float cellTop = padTop + (rows - 1 - row) * (cellH + CellGap);
                spots[i] = new Vector2(left + cellW / 2f, cellTop + cellH / 2f);

                cells[i] = Cell(ground, raid, i, left, cellTop, cellW, cellH);
            }

            // ⭐ 卵は**横いっぱいの帯**。⚠️ マスとして並べると最上段に1つだけ浮く
            var goal = Ui.Block(ground, "Goal", Ui.Accent,
                Ui.Margin, CellGap, Ui.W - Ui.Margin * 2f, GoalHeight);
            Ui.Label(goal, "T", "卵", 46, Ui.OnLead, TextAnchor.MiddleCenter,
                0f, 0f, Ui.W - Ui.Margin * 2f, GoalHeight);
            cells[walk] = goal;
            spots[walk] = new Vector2(spots[walk - 1].x, CellGap + GoalHeight / 2f);

            // ⭐ 道の線。⚠️ 蛇行はどちらへ進むか一目では読めないので、繋いで示す
            for (int i = 0; i + 1 < spots.Length; i++) Link(ground, spots[i], spots[i + 1], i);

            // ⭐ 行き先の印。⚠️ `At + Saves` ではなく **実際に止まるマス**
            //    （途中の分かれ道で止まるので、跨いだ先には着かない）
            if (raid.Step == RaidStep.AtFork && Trails.CanBreak(raid))
                Ring(ground, cells[Mathf.Min(Trails.LandingOf(raid), walk)]);

            Piece(cells[Mathf.Min(raid.At, walk)], raid, cellH);
            return cells;
        }

        /// <summary>1マス。⭐ 中身は**進むか止まるかに関わるものだけ**。</summary>
        private static RectTransform Cell(RectTransform ground, Raid raid, int index,
            float left, float top, float w, float h)
        {
            bool behind = index < raid.At;
            var space = raid.Trail.Squares[index];
            var cell = Ui.Rect($"Cell {index}", ground);
            Ui.Place(cell, left, top, w, h);

            var face = cell.gameObject.AddComponent<Image>();
            face.color = behind ? PlateGone : Plate;

            switch (space.Kind)
            {
                case SquareKind.Fork:
                    Fork(cell, raid, index, space, w, h, behind);
                    break;

                case SquareKind.Mob:
                    bool beaten = raid.Beaten.Contains(index);
                    face.color = beaten ? PlateGone : new Color(0.10f, 0.12f, 0.18f, 0.92f);
                    Ui.Label(cell, "T", beaten ? "×" : "敵", 34,
                        beaten ? new Color(1f, 1f, 1f, 0.45f) : Color.white,
                        TextAnchor.MiddleCenter, 0f, 0f, w, h);
                    break;

                case SquareKind.Boon:
                case SquareKind.Bane:
                    bool up = space.Kind == SquareKind.Boon;
                    var ink = behind ? Ui.InkFaint : up ? Ui.GoodInk : Ui.DangerInk;
                    Ui.Label(cell, "T", up ? "▲" : "▼", 30, ink,
                        TextAnchor.UpperCenter, 0f, h * 0.16f, w, 34f);
                    Ui.Label(cell, "S", $"{StatName(space.Stat)}{space.Percent:+0;-0}%", 24, ink,
                        TextAnchor.LowerCenter, 0f, h - 46f, w, 32f);
                    break;

                default:
                    // ⭐ 何も起きないマスは点だけ。⚠️ 数字を振らない（読む物が増えるだけ）
                    Ui.Round(cell, "Dot", w / 2f - 7f, h / 2f - 7f, 14f,
                        new Color(0f, 0f, 0f, behind ? 0.10f : 0.20f));
                    break;
            }
            return cell;
        }

        /// <summary>分かれ道のマス。⭐ **飛べる数と値段を両方出す。**
        ///
        /// ⚠️ 「お得」などと判じて出さない。値と代価が並んでいれば読む側が決められる
        /// （作者の方針: 読めば強い弱いがなんとなく分かる形にする）。
        /// ⭐ いま払えるものだけ、左の一辺に差し色を入れる。</summary>
        private static void Fork(RectTransform cell, Raid raid, int index, Square space,
            float w, float h, bool behind)
        {
            bool broken = raid.Broken.Contains(index);
            bool passed = raid.Passed.Contains(index);
            int cost = Trails.CostOf(raid, space);
            bool afford = !broken && !passed && raid.Power >= cost;

            if (broken)
            {
                Ui.Label(cell, "T", "通", 32, Ui.InkFaint, TextAnchor.MiddleCenter, 0f, 0f, w, h);
                return;
            }

            // ⭐ 区切りは線で囲わず、**一辺だけ**。押せる合図をここに集める
            if (afford) Ui.Block(cell, "Edge", Ui.Accent, 0f, 0f, 8f, h);

            var ink = passed ? Ui.InkFaint : afford ? Ui.Ink : Ui.InkDim;

            // ⚠️ **3段を器の高さから割り付ける。**マスの高さは段によって 119〜189 と変わるので、
            //    真ん中寄せ＋下寄せを目分量で重ねると、低いマスだけ被る
            //    （段4の 119px で +N と値段が被った。総当たりで数えて発覚 2026-08-20）。
            const float NameHigh = 26f, CostHigh = 32f;
            // ⚠️ 名前は真ん中に置かない。⭐ 駒が左上に立つので**右上**へ寄せる
            Ui.Label(cell, "Gate", GateName(space.Gate), 22, ink,
                TextAnchor.UpperRight, 0f, 2f, w - 10f, NameHigh);
            // ⭐ 残った真ん中の段。⚠️ **駒が立つ左上を空けた残り**の真ん中へ
            float room = PieceRoom(h);
            float midTop = 2f + NameHigh;
            var saves = Ui.Label(cell, "Saves", $"+{space.Saves}", 36, ink,
                TextAnchor.MiddleCenter, room, midTop, w - room, h - midTop - CostHigh - 2f);
            saves.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Label(cell, "Cost", Ui.Digits(cost), 22,
                afford ? Ui.AccentInk : Ui.InkFaint,
                TextAnchor.LowerCenter, 0f, h - CostHigh - 2f, w, CostHigh);
        }

        /// <summary>マスとマスを繋ぐ線。⚠️ 飾りではなく**進む向き**を示す。</summary>
        private static void Link(RectTransform ground, Vector2 a, Vector2 b, int index)
        {
            var mid = (a + b) / 2f;
            bool sideways = Mathf.Abs(a.x - b.x) > Mathf.Abs(a.y - b.y);
            float w = sideways ? Mathf.Abs(a.x - b.x) : 6f;
            float h = sideways ? 6f : Mathf.Abs(a.y - b.y);
            var line = Ui.Block(ground, $"Link {index}", Road,
                mid.x - w / 2f, mid.y - h / 2f, w, h);
            line.SetAsFirstSibling();
        }

        /// <summary>いま居るマスに置く駒。⭐ **3体で1つ**（作者の決定）。
        ///
        /// ⚠️ マスの真ん中に大きく置かない。分かれ道に立つと、その値段が駒に隠れて
        /// **何を選ぶのか読めなくなる**（実機で確認 2026-08-20）。
        /// ⭐ 角に寄せて小さく置く ── 顔が乗っているので、小さくても見つかる。</summary>
        private static void Piece(RectTransform cell, Raid raid, float cellH)
        {
            float size = PieceSize(Mathf.Min(cellH, cell.sizeDelta.y));
            // ⚠️ 左**上**。下に置くと値段の行を食う（実機で確認 2026-08-20）。
            //    上の真ん中は関門の名前と ▲▼ が使うので、角へ寄せる
            const float Inset = 4f;
            var disc = Ui.Round(cell, "Piece", Inset, Inset, size, Ui.Accent);
            if (raid.Party.Count > 0)
                Ui.PixelOf(disc, "Art", raid.Party[0], size * 0.14f, size * 0.14f, size * 0.72f);
            Jolt.Play(disc, new Vector2(0f, 14f), 0.20f);
        }

        /// <summary>駒の大きさ。⭐ **マスの中身を避けるのはこの数から逆算する。**
        ///
        /// ⚠️ 見た目で決めて後から中身をずらす、をやると、段ごとにマスの高さが違うので
        /// どこかの段だけ当たる（段5の 141px のマスだけ +N に掛かっていた。
        /// 総当たりで数えて発覚 2026-08-20）。⭐ 中身の側がこの数を見て場所を空ける。</summary>
        private static float PieceSize(float cellH) => Mathf.Min(cellH * 0.44f, 72f);

        /// <summary>駒が使う左上の幅（間を足したもの）。</summary>
        private static float PieceRoom(float cellH) => PieceSize(cellH) + 10f;

        /// <summary>壊すと出る先の印。⭐ 選択は角丸の輪に揃える（一覧の升と同じ約束）。
        ///
        /// ⚠️ マスの中に敷かない。<see cref="Ui.Ring"/> は内側を白で塗るので、
        /// 行き先が敵のマスでも真っ白になる（実機で塗り潰した 2026-08-20）。
        /// ⭐ **マスより一回り大きく、マスの後ろ**に敷いて、はみ出た縁だけを見せる。</summary>
        private static void Ring(RectTransform ground, RectTransform cell)
        {
            const float Halo = 5f;
            var at = cell.anchoredPosition;
            var ring = Ui.Ring(ground, "Landing",
                at.x - Halo, -at.y - Halo,
                cell.sizeDelta.x + Halo * 2f, cell.sizeDelta.y + Halo * 2f);
            ring.SetAsFirstSibling();
        }

        // ── 下の操作帯 ──────────────────────────────

        /// <summary>いま押せるもの。⭐ **1画面に1つの主導線。**
        ///
        /// ⚠️ 高さは状態で変えない（<see cref="DockHeight"/>）。変えると盤が跳ねる。</summary>
        private static void Dock(App app, RectTransform body, Raid raid, RectTransform[] cells)
        {
            float top = Ui.H - Ui.TopBarHeight - DockHeight;
            var dock = Ui.Block(body, "Dock", Board, 0f, top, Ui.W, DockHeight);
            float w = Ui.W - Ui.Margin * 2f;

            if (raid.Result != null || raid.Step == RaidStep.Met || _rolling)
            {
                Ui.Label(dock, "Wait", raid.Step == RaidStep.Met ? "囲まれた" : "",
                    32, new Color(1f, 1f, 1f, 0.72f), TextAnchor.MiddleCenter,
                    Ui.Margin, 0f, w, DockHeight);
                return;
            }

            if (raid.Step == RaidStep.AtFork) { ForkChoice(app, dock, raid, w); return; }

            // ── 振る ──────────────────────────────
            int left = raid.Trail.Length - raid.At;
            Ui.Label(dock, "Left", $"卵まで あと {left} マス", 30, new Color(1f, 1f, 1f, 0.78f),
                TextAnchor.MiddleCenter, Ui.Margin, 40f, w, 40f);

            Ui.Tappable(dock, "Roll", "さいころを振る", () => RollNow(app, raid),
                Ui.Margin, 108f, w, 132f, lead: true, enabled: raid.Rolls > 0);

            // ⚠️ 「引っ張って離す」のような操作の説明は書かない。1回やれば分かる。
            //    ⭐ 代わりに**尽きたらどうなるか**だけ置く（これは1回では分からない）
            Ui.Label(dock, "Warn", raid.Rolls <= 1
                    ? "これで最後。届かなければ親が帰ってくる"
                    : "振り切って届かなければ親が帰ってくる",
                26, raid.Rolls <= 1 ? Ui.Accent : new Color(1f, 1f, 1f, 0.55f),
                TextAnchor.MiddleCenter, Ui.Margin, 262f, w, 40f);
        }

        /// <summary>分かれ道での2択。⭐ **どちらを選ぶと見込みがどう動くかを並べて出す。**
        ///
        /// ⚠️ ここが遊びの芯なので、材料を隠さない。壊せば残った目を捨て、
        /// 歩けば残った目のぶん進む ── どちらが得かは出目で変わる。</summary>
        private static void ForkChoice(App app, RectTransform dock, Raid raid, float w)
        {
            var space = raid.Trail.Squares[raid.At];
            int cost = Trails.CostOf(raid, space);
            bool afford = raid.Power >= cost;

            // ⚠️ 飛べる数をそのまま出さない。**実際に止まるマスまで**で数える
            //    （途中に別の分かれ道があるとそこで止まるので、印も数もずれる）
            int gainBreak = Trails.LandingOf(raid) - raid.At;
            int gainWalk = Trails.WalkingTo(raid) - raid.At;
            int ifBreak = Trails.Odds(raid, gainBreak);
            int ifWalk = Trails.Odds(raid, gainWalk);

            Ui.Label(dock, "Cap", $"{GateName(space.Gate)}がふさいでいる", 30,
                new Color(1f, 1f, 1f, 0.86f), TextAnchor.MiddleCenter, Ui.Margin, 22f, w, 40f);

            float half = (w - 20f) / 2f;
            Ui.Tappable(dock, "Break", $"壊す  +{gainBreak}",
                () => { Trails.Break(raid); app.Refresh(); },
                Ui.Margin, 80f, half, 132f, lead: true, enabled: afford);
            Ui.Tappable(dock, "Walk", $"進む  +{gainWalk}",
                () => { Trails.Walk(raid); app.Refresh(); },
                Ui.Margin + half + 20f, 80f, half, 132f);

            // ⭐ 代価と見込みを、押しどころの真下に対で置く
            Ui.Label(dock, "BreakCost",
                afford ? $"ちから {Ui.Digits(cost)}" : $"ちからが {Ui.Digits(cost - raid.Power)} 足りない",
                26, afford ? Ui.Accent : new Color(1f, 1f, 1f, 0.45f),
                TextAnchor.MiddleCenter, Ui.Margin, 224f, half, 34f);
            Ui.Label(dock, "WalkCost", "ちからは減らない", 26, new Color(1f, 1f, 1f, 0.55f),
                TextAnchor.MiddleCenter, Ui.Margin + half + 20f, 224f, half, 34f);

            Ui.Label(dock, "BreakOdds", $"とどく {ifBreak}%", 30,
                afford ? Color.white : new Color(1f, 1f, 1f, 0.40f),
                TextAnchor.MiddleCenter, Ui.Margin, 264f, half, 40f);
            Ui.Label(dock, "WalkOdds", $"とどく {ifWalk}%", 30, Color.white,
                TextAnchor.MiddleCenter, Ui.Margin + half + 20f, 264f, half, 40f);

            // ⚠️ 「残った目は捨てる」は1回では気づけない。⭐ 選ぶ前に置く
            Ui.Label(dock, "Note", "壊すと残りの目は捨てる", 24, new Color(1f, 1f, 1f, 0.50f),
                TextAnchor.MiddleCenter, Ui.Margin, 314f, w, 34f);
        }

        // ── 進行 ────────────────────────────────────

        /// <summary>振る。⭐ 出目は Core が決め、画面は**それを見せてから**組み直す。</summary>
        private static void RollNow(App app, Raid raid)
        {
            if (_rolling || raid.Rolls <= 0) return;
            // ⚠️ 巣が無ければ種が作れない（`Games.RaidsOn` は巣を素で触る）
            var nest = app.CurrentNest;
            if (nest == null) { app.Show(Screen.Nests); return; }
            _rolling = true;

            // ⚠️ 種は巣と進み具合から作る。その場で引くと、
            //    画面を出入りするだけで出目を選び直せてしまう
            var rng = new Rng(0).Stream(
                $"trail:{nest.Id}:{Games.RaidsOn(app.Game, nest)}"
                + $":{raid.Rolls}:{raid.At}:{raid.Broken.Count}:{raid.Beaten.Count}");
            Trails.Roll(rng, raid);
            int face = raid.LastRoll;
            _flagged = raid;
            // ⚠️ ここで組み直さない。⭐ **さいころが止まってから**動かす
            //    （覆いは半透明なので、先に組み直すと駒がもう動いて見える）
            TrailDice.Show(app.Overlay, face, () =>
            {
                _rolling = false;
                app.Refresh();
            });
        }

        /// <summary>雑魚に出会った。⚠️ **潜入の決着ではない** ── 勝てば続きへ戻る。</summary>
        private static void Meet(App app, Raid raid)
        {
            _handing = true;
            _flagged = raid;
            var nest = app.CurrentNest;
            int space = raid.At;
            BannerView.Show(app.Overlay, "雑魚に囲まれた！", () =>
            {
                _handing = false;
                // ⚠️ 告知が流れているあいだに画面が変わっていたら、戦闘へ引きずり込まない
                if (!ReferenceEquals(app.Raid, raid) || app.Showing != Screen.Trail) return;
                app.EnterTrailMobBattle(nest, space);
            });
        }

        /// <summary>決着。⭐ 届いたら盗み、尽きたら親と戦う。</summary>
        private static void Finish(App app, Raid raid)
        {
            _handing = true;
            _flagged = raid;
            bool won = raid.Result == StealOutcome.Success;
            var nest = app.CurrentNest;
            BannerView.Show(app.Overlay, won ? "GET!" : "親に見つかった！", () =>
            {
                _handing = false;
                // ⚠️ 告知のあいだに画面が変わっていたら、卵も戦闘も起こさない
                if (!ReferenceEquals(app.Raid, raid) || app.Showing != Screen.Trail) return;
                // ⭐ ここで潜入は終わり。⚠️ 残すと次の巣に前の進み具合が付いてくる
                app.Raid = null;
                if (won)
                {
                    Games.GrowParty(Games.PartyOf(app.Game));
                    // ⚠️ 盗んだ卵は素質が落ちる（倒したほうが良い卵）
                    // ⭐ 盗んだ巣は**残る**。次はもっと固くなっているだけ
                    app.GainEgg(nest, EggOrigin.Stolen, closeNest: false);
                    Games.RecordRaid(app.Game, nest);
                }
                else
                {
                    // ⚠️ 潜入で負った傷と CT を持ち込む（雑魚と戦うほどここが苦しくなる）
                    app.EnterBattle(nest, false, raid.Hp, raid.Cooldowns);
                }
            });
        }

        // ── 呼び名 ──────────────────────────────────

        private static string GateName(GimmickKind gate)
        {
            switch (gate)
            {
                case GimmickKind.Wall: return "壁";
                case GimmickKind.Damage: return "床";
                default: return "重圧";
            }
        }

        private static string StatName(GimmickKind gate) => StatName(Trails.StatOf(gate));

        private static string StatName(StatKey key)
        {
            switch (key)
            {
                case StatKey.Atk: return "攻";
                case StatKey.Hp: return "HP";
                case StatKey.Def: return "防";
                case StatKey.Spd: return "速";
                default: return "";
            }
        }
    }
}
