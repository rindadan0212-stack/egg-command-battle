using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>卵強奪。⭐ **分岐するすごろく**（作者の指示 2026-08-20）。
    ///
    /// 速度の合計で振れる回数が決まり、さいころで道を進む。分かれ道では
    /// 「攻撃が要る近道」と「防御が要る遠回り」を見比べて選ぶ。
    /// 振り切って卵に届かなければ親が帰ってくる。
    ///
    /// ⚠️ 弾いて飛ばす遊び（<see cref="StealScreen"/>）は**別物として残してある**。
    ///
    /// ⭐ 画面の作り:
    /// <list type="bullet">
    /// <item>盤は**縦にスクロール**する。1画面に収めない（作者の指示）</item>
    /// <item>入口が下、卵が上。分かれ道から**左右に膨らんで**、また合流する</item>
    /// <item>上の帯には**攻・HP・防**を並べる ── どの道が通れるかは、これと関門を見比べて読む</item>
    /// </list>
    ///
    /// ⭐ 色は**1色だけ**（<see cref="Ui.Accent"/>）。いま居るマスと、
    /// **いま通れる道**にだけ点く。⚠️ 関門の種類は色でなく字（攻/HP/防）で示す。</summary>
    public static class TrailScreen
    {
        // ── 寸法 ────────────────────────────────────
        private const float HeaderHeight = 224f;
        private const float GroupGap = 14f;
        /// <summary>下の操作帯。⭐ **状態が変わっても高さを変えない。**</summary>
        private const float DockHeight = 392f;

        /// <summary>マス1つ。⚠️ 押しどころではないので <see cref="Ui.Tap"/> 未満でよい。</summary>
        private const float CellW = 176f;
        private const float CellH = 96f;
        /// <summary>段の高さ（マス＋あいだ）。⚠️ 広いと縦に伸びすぎてスクロールが長くなる。</summary>
        private const float RowStep = 122f;
        /// <summary>左右に膨らむ幅。</summary>
        private const float Bulge = 252f;
        /// <summary>関門の札の高さ。⭐ マスの上端に帯として重ねる。</summary>
        private const float GateHigh = 34f;
        private const float GoalHeight = 104f;

        private static readonly Color Board = new Color(0.04f, 0.06f, 0.10f, 0.55f);
        private static readonly Color Plate = new Color(1f, 1f, 1f, 0.88f);
        private static readonly Color PlateGone = new Color(1f, 1f, 1f, 0.30f);
        /// <summary>マスとマスを繋ぐ線。⚠️ 薄いと分岐の形が読めない。</summary>
        private static readonly Color Road = new Color(1f, 1f, 1f, 0.42f);
        private static readonly Color RoadShut = new Color(1f, 1f, 1f, 0.14f);

        /// <summary>告知を出して次へ渡している最中。⚠️ 組み直すたびに作らないための札。</summary>
        private static bool _handing;
        /// <summary>さいころを回している最中。⚠️ 二重に振らせない。</summary>
        private static bool _rolling;
        /// <summary>その札を立てたときの潜入。⚠️ 潜入が差し替わったら札を下ろす。</summary>
        private static Raid _flagged;
        /// <summary>盤のどこを見ていたか（0＝入口側）。⚠️ 組み直しで先頭へ戻らないよう覚える。</summary>
        private static float _look = -1f;

        /// <summary>巣を選んで潜入へ。
        ///
        /// ⚠️ 道は <see cref="Trails.OfNest"/> ＝ **巣ごとに固定**。
        /// ⭐ 固定だからこそ「壁の多い巣には攻撃に寄せた編成で行く」が成り立つ。</summary>
        public static void Enter(App app, Nest nest)
        {
            app.CurrentNest = nest;
            app.CurrentIsBoss = false;
            _handing = false;
            _rolling = false;
            _flagged = null;
            _look = -1f;

            int raids = Games.RaidsOn(app.Game, nest);
            // ⚠️ 守りが最大の巣は**入れないのではなく、入れば戦闘**（[巣の寿命]）
            if (Steal.IsSealed(raids))
            {
                app.Raid = null;
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
            if (!ReferenceEquals(_flagged, raid)) { _handing = false; _rolling = false; }

            float boardTop = HeaderHeight + GroupGap;
            float boardHeight = Ui.H - Ui.TopBarHeight - boardTop - DockHeight;

            Header(app, body, raid);
            BoardOf(app, body, raid, boardTop, boardHeight);
            Dock(app, body, raid);

            // ⚠️ ここに押しどころを置かない。届いたなら持ち帰るしかないし、
            //    見つかったなら戦うしかない。選択肢でないものを押させない。
            if (_handing || _rolling) return;
            if (raid.Step == RaidStep.Met) { Meet(app, raid); return; }
            if (raid.Result != null) Finish(app, raid);
        }

        // ── 上の帯 ──────────────────────────────────

        /// <summary>判断に要る数。⭐ **回数・3つのステ・見込み**。
        ///
        /// ⚠️ ステを1本にまとめない。⭐ **どの道が通れるか**は種類ごとに決まるので、
        /// 3本並べて関門の要求と見比べられるようにする（作者の指示 2026-08-20）。</summary>
        private static void Header(App app, RectTransform body, Raid raid)
        {
            var strip = Ui.Block(body, "Header", Board, 0f, 0f, Ui.W, HeaderHeight);
            var faint = new Color(1f, 1f, 1f, 0.62f);

            // ⚠️ 段は**字の実寸**で取る。器の高さで詰めると重なる（2026-08-20 の実測）
            const float CapTop = 10f, CapHigh = 24f;
            const float BigTop = 44f, BigHigh = 72f;        // 44〜116
            const float PillTop = 132f, PillHigh = 84f;     // 132〜216

            // ── あと何回振れるか（左・主役） ──────────
            Ui.Label(strip, "RollsCap", "のこり", 24, faint,
                TextAnchor.LowerLeft, Ui.Margin, CapTop, 200f, CapHigh);
            var rolls = Ui.Label(strip, "Rolls", raid.Rolls.ToString(), 60,
                raid.Rolls <= 1 ? Ui.Accent : Color.white,
                TextAnchor.UpperLeft, Ui.Margin, BigTop, 100f, BigHigh);
            rolls.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Label(strip, "RollsUnit", "回", 28, faint,
                TextAnchor.LowerLeft, Ui.Margin + 106f, BigTop, 70f, BigHigh);

            // ── 残りマスと届く見込み（右） ────────────
            int carried = raid.Step == RaidStep.AtJunction ? raid.Pending : 0;
            int left = Trails.Left(raid);
            int odds = Trails.Odds(raid, carried);
            const float Wide = 300f;
            float right = Ui.W - Ui.Margin - Wide;
            Ui.Label(strip, "LeftCap",
                left < 0 ? "行き止まり" : $"とどく／卵まで {left} マス", 24, faint,
                TextAnchor.LowerRight, right, CapTop, Wide, CapHigh);
            var odd = Ui.Label(strip, "Odds", $"{odds}%", 52,
                odds < 40 ? Ui.Accent : Color.white,
                TextAnchor.UpperRight, right, BigTop, Wide, BigHigh);
            odd.horizontalOverflow = HorizontalWrapMode.Overflow;

            // ── 攻・HP・防（どの道が通れるかの元） ──────
            float pillWide = (Ui.W - Ui.Margin * 2f - 24f) / 3f;
            var gates = new[] { GimmickKind.Wall, GimmickKind.Damage, GimmickKind.Pressure };
            for (int i = 0; i < gates.Length; i++)
            {
                var key = Trails.StatOf(gates[i]);
                int now = Trails.Usable(raid, key);
                bool lifted = raid.TempLeft[key] > 0 && raid.Temp[key] > 0;
                bool sunk = raid.TempLeft[key] > 0 && raid.Temp[key] < 0;
                float left2 = Ui.Margin + (pillWide + 12f) * i;
                var pill = Ui.Block(strip, $"Stat {i}",
                    new Color(1f, 1f, 1f, lifted ? 0.20f : 0.09f), left2, PillTop, pillWide, PillHigh);
                // ⚠️ 段を重ねない。⭐ 見出しは上の段、数はその下の段
                Ui.Label(pill, "Cap", $"{GateName(gates[i])} {StatName(key)}", 21,
                    new Color(1f, 1f, 1f, 0.62f), TextAnchor.UpperCenter, 0f, 3f, pillWide, 30f);
                Ui.Label(pill, "Num", Ui.Digits(now), 28,
                    lifted ? Ui.Accent : sunk ? Ui.Danger : Color.white,
                    TextAnchor.UpperCenter, 0f, 38f, pillWide, 40f);
                if (raid.TempLeft[key] > 0)
                    Ui.Label(pill, "Temp", $"{raid.Temp[key]:+0;-0}%", 19,
                        lifted ? Ui.Accent : Ui.Danger,
                        TextAnchor.UpperRight, 0f, 3f, pillWide - 8f, 30f);
            }
        }

        // ── 盤 ──────────────────────────────────────

        /// <summary>マスの置き場所（盤の中の座標）。</summary>
        private struct Spot { public float X, Y; }

        /// <summary>マスを縦に並べる。⭐ **入口が下、卵が上。分かれ道で左右に膨らむ。**
        ///
        /// ⚠️ 1画面に収めない（作者の指示）。⭐ 縦にスクロールし、駒が見える所へ寄せる。</summary>
        private static void BoardOf(App app, RectTransform body, Raid raid,
            float top, float height)
        {
            var trail = raid.Trail;
            var spots = Layout(trail, out float tall);
            float content = tall + GoalHeight + RowStep;

            var view = Ui.Scroller(body, "Board", 0f, top, Ui.W, height, content);
            var back = Ui.Block(view, "Ground", Board, 0f, 0f, Ui.W, content);
            back.SetAsFirstSibling();

            // ⭐ 卵は**横いっぱいの帯**。⚠️ マスとして並べると1つだけ浮いて行き先に見えない
            var goal = Ui.Block(view, "Goal", Ui.Accent,
                Ui.Margin, 8f, Ui.W - Ui.Margin * 2f, GoalHeight);
            Ui.Label(goal, "T", "卵", 46, Ui.OnLead, TextAnchor.MiddleCenter,
                0f, 0f, Ui.W - Ui.Margin * 2f, GoalHeight);

            // ── 道の線（マスより先に敷く） ────────────
            for (int i = 0; i < trail.Count; i++)
            {
                var sq = trail.Squares[i];
                for (int w = 0; w < sq.Ways.Count; w++)
                {
                    var way = sq.Ways[w];
                    bool open = !sq.IsJunction || Trails.CanPass(raid, way);
                    bool chosen = sq.IsJunction && raid.Took.TryGetValue(i, out int took) && took == w;
                    Link(view, spots[i], spots[way.To], $"L{i}-{w}",
                        chosen ? Ui.Accent : open ? Road : RoadShut);
                }
            }

            // ── マス ────────────────────────────────
            var cells = new RectTransform[trail.Count];
            for (int i = 0; i < trail.Count; i++)
                cells[i] = Cell(view, raid, i, spots[i]);

            // ⭐ 関門の札は**入る先のマスの上端**に重ねる（どちらの道の関門か迷わない）
            for (int i = 0; i < trail.Count; i++)
            {
                var sq = trail.Squares[i];
                if (!sq.IsJunction) continue;
                foreach (var way in sq.Ways)
                    if (way.IsGated && cells[way.To] != null)
                        Gate(cells[way.To], way, Trails.CanPass(raid, way));
            }

            // ⭐ 分かれ道に立っているなら、行き先に印を付ける
            if (raid.Step == RaidStep.AtJunction)
            {
                var ways = trail.Squares[raid.At].Ways;
                for (int w = 0; w < ways.Count; w++)
                    if (Trails.CanPass(raid, ways[w])) Ring(view, spots[ways[w].To]);
            }

            Piece(cells[raid.At], raid);

            // ⭐ 駒が見える所へ寄せる。⚠️ 組み直すたびに先頭へ戻さない
            float want = 1f - Mathf.Clamp01((spots[raid.At].Y - height * 0.45f)
                / Mathf.Max(1f, content - height));
            var scroll = view.GetComponentInParent<ScrollRect>();
            if (scroll != null)
            {
                _look = Mathf.Clamp01(want);
                scroll.verticalNormalizedPosition = _look;
            }
        }

        /// <summary>マスの置き場所。⭐ **Core が持っている段と左右をそのまま読む。**
        /// ⚠️ 画面側で道を辿って割り出さない（重なって線が交差した。2026-08-20 の実機）。</summary>
        private static Spot[] Layout(Trail trail, out float tall)
        {
            int deep = trail.Depth;
            tall = (deep + 1) * RowStep;
            float mid = Ui.W / 2f - CellW / 2f;

            var spots = new Spot[trail.Count];
            for (int i = 0; i < trail.Count; i++)
            {
                var sq = trail.Squares[i];
                spots[i] = new Spot
                {
                    X = mid + sq.Lane * (Bulge / 2f),
                    // ⭐ 深いほど上（卵に近い）。卵の帯のぶんだけ下げる
                    Y = GoalHeight + 28f + (deep - sq.Row) * RowStep,
                };
            }
            return spots;
        }

        /// <summary>1マス。⭐ 中身は**進むか止まるかに関わるものだけ**。</summary>
        private static RectTransform Cell(RectTransform ground, Raid raid, int index, Spot at)
        {
            var trail = raid.Trail;
            var sq = trail.Squares[index];
            if (sq.IsGoal) return ground;                 // 卵は帯で描いてある

            bool behind = raid.Took.ContainsKey(index) || index < raid.At;
            var cell = Ui.Rect($"Cell {index}", ground);
            Ui.Place(cell, at.X, at.Y, CellW, CellH);
            var face = cell.gameObject.AddComponent<Image>();
            face.color = behind ? PlateGone : Plate;

            // ⚠️ **関門の札のぶん、中身を下げる。**同じ場所に重ねると字が読めない
            float pad = GatedInto(trail, index) ? GateHigh : 0f;
            float high = CellH - pad;

            switch (sq.Kind)
            {
                case SquareKind.Mob:
                    bool beaten = raid.Beaten.Contains(index);
                    face.color = beaten ? PlateGone : new Color(0.10f, 0.12f, 0.18f, 0.92f);
                    Ui.Label(cell, "T", beaten ? "×" : "敵", 30,
                        beaten ? new Color(1f, 1f, 1f, 0.45f) : Color.white,
                        TextAnchor.MiddleCenter, 0f, pad, CellW, high);
                    break;

                case SquareKind.Boon:
                case SquareKind.Bane:
                    bool up = sq.Kind == SquareKind.Boon;
                    var ink = behind ? Ui.InkFaint : up ? Ui.GoodInk : Ui.DangerInk;
                    Ui.Label(cell, "T",
                        $"{(up ? "▲" : "▼")}{StatName(sq.Stat)}{sq.Percent:+0;-0}%", 24, ink,
                        TextAnchor.MiddleCenter, 0f, pad, CellW, high);
                    break;

                default:
                    if (sq.IsJunction)
                    {
                        // ⭐ 分かれ道は**丸い節**。⚠️ 何も書かない（読むのは道の側の札）
                        Ui.Round(cell, "Hub", CellW / 2f - 22f, pad + high / 2f - 22f, 44f,
                            behind ? Ui.InkFaint : Ui.Ink);
                        break;
                    }
                    Ui.Round(cell, "Dot", CellW / 2f - 7f, pad + high / 2f - 7f, 14f,
                        new Color(0f, 0f, 0f, behind ? 0.10f : 0.20f));
                    break;
            }
            return cell;
        }

        /// <summary>そのマスに、関門つきの道で入ってくるか。⭐ 札を置く場所を空けるため。</summary>
        private static bool GatedInto(Trail trail, int index)
        {
            for (int i = 0; i < index; i++)
            {
                var sq = trail.Squares[i];
                if (!sq.IsJunction) continue;
                foreach (var way in sq.Ways)
                    if (way.To == index && way.IsGated) return true;
            }
            return false;
        }

        /// <summary>マスとマスを繋ぐ線。⭐ **関門があれば、その札を線の上に置く。**
        ///
        /// ⚠️ 関門をマスに書くと「どっちの道の関門か」が読めない。
        /// ⭐ 線の真ん中に置けば、どちらへ続く関門かが一目で分かる。</summary>
        private static void Link(RectTransform ground, Spot a, Spot b, string name, Color color)
        {
            float ax = a.X + CellW / 2f, ay = a.Y + CellH / 2f;
            float bx = b.X + CellW / 2f, by = b.Y + CellH / 2f;
            var line = Ui.Rect(name, ground);
            float dx = bx - ax, dy = by - ay;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            Ui.Place(line, ax, ay - 5f, len, 10f);
            line.pivot = new Vector2(0f, 0.5f);
            line.anchoredPosition = new Vector2(ax, -ay);
            line.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
            var image = line.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        /// <summary>関門の札。⭐ **入る先のマスの上端に、帯として重ねる。**
        ///
        /// ⚠️ 線の真ん中に置くと、2本の線が分かれ道の近くで寄るので札どうしが重なった。
        /// ⚠️ マスの外（真上）に出すと、1つ上のマスに重なった。
        /// ⭐ マスの中なら、段の高さがどうであれ絶対に他とぶつからない（2026-08-20 の実機）。</summary>
        private static void Gate(RectTransform cell, Way way, bool open)
        {
            var tag = Ui.Block(cell, "Gate",
                open ? Ui.Accent : new Color(0.10f, 0.12f, 0.18f, 0.88f), 0f, 0f, CellW, GateHigh);
            Ui.Label(tag, "T", $"{GateName(way.Gate)} {Ui.Digits(way.Requires)}", 22,
                open ? Ui.OnLead : new Color(1f, 1f, 1f, 0.62f),
                TextAnchor.MiddleCenter, 0f, 0f, CellW, GateHigh);
        }

        /// <summary>いま居るマスに置く駒。⭐ **3体で1つ**（作者の決定）。</summary>
        private static void Piece(RectTransform cell, Raid raid)
        {
            if (cell == null) return;
            const float Size = 56f;
            // ⚠️ 左下へ。上端は関門の札が使う
            var disc = Ui.Round(cell, "Piece", 4f, CellH - Size - 4f, Size, Ui.Accent);
            if (raid.Party.Count > 0)
                Ui.PixelOf(disc, "Art", raid.Party[0], Size * 0.14f, Size * 0.14f, Size * 0.72f);
            Jolt.Play(disc, new Vector2(0f, 14f), 0.20f);
        }

        /// <summary>行き先の印。⚠️ マスより一回り大きく、後ろに敷いて縁だけ見せる。</summary>
        private static void Ring(RectTransform ground, Spot at)
        {
            const float Halo = 6f;
            var ring = Ui.Ring(ground, "Landing",
                at.X - Halo, at.Y - Halo, CellW + Halo * 2f, CellH + Halo * 2f);
            ring.SetAsFirstSibling();
        }

        // ── 下の操作帯 ──────────────────────────────

        private static void Dock(App app, RectTransform body, Raid raid)
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

            if (raid.Step == RaidStep.AtJunction) { Fork(app, dock, raid, w); return; }

            Ui.Label(dock, "Left", $"あと {Trails.Left(raid)} マス", 30,
                new Color(1f, 1f, 1f, 0.78f), TextAnchor.MiddleCenter, Ui.Margin, 40f, w, 40f);
            Ui.Tappable(dock, "Roll", "さいころを振る", () => RollNow(app, raid),
                Ui.Margin, 108f, w, 132f, lead: true, enabled: raid.Rolls > 0);
            Ui.Label(dock, "Warn", raid.Rolls <= 1
                    ? "これで最後。届かなければ親が帰ってくる"
                    : "振り切って届かなければ親が帰ってくる",
                26, raid.Rolls <= 1 ? Ui.Accent : new Color(1f, 1f, 1f, 0.55f),
                TextAnchor.MiddleCenter, Ui.Margin, 262f, w, 40f);
        }

        /// <summary>分かれ道での選択。⭐ **2本を並べて、要る物と長さと見込みを対で出す。**
        ///
        /// ⚠️ ここが遊びの芯なので、材料を隠さない。
        /// ⭐ 「こっちは攻撃が足りない、あっちは防御なら通れる」を読ませる（作者の指示）。</summary>
        private static void Fork(App app, RectTransform dock, Raid raid, float w)
        {
            var ways = raid.Trail.Squares[raid.At].Ways;
            Ui.Label(dock, "Cap", "どちらの道を行く？", 30, new Color(1f, 1f, 1f, 0.86f),
                TextAnchor.MiddleCenter, Ui.Margin, 16f, w, 36f);

            float half = (w - 20f) / 2f;
            for (int i = 0; i < ways.Count && i < 2; i++)
            {
                int pick = i;
                var way = ways[i];
                bool open = Trails.CanPass(raid, way);
                var key = Trails.StatOf(way.Gate);
                int have = Trails.Usable(raid, key);
                float left = Ui.Margin + (half + 20f) * i;

                Ui.Tappable(dock, i == 0 ? "Near" : "Far",
                    $"{way.Length} マス",
                    () => { Trails.Take(raid, pick); app.Refresh(); },
                    left, 56f, half, 132f, lead: i == 0, enabled: open);

                // ⭐ 何がいくら要って、いまいくら持っているか
                Ui.Label(dock, $"Need{i}", $"{GateName(way.Gate)} {Ui.Digits(way.Requires)}", 28,
                    open ? Ui.Accent : new Color(1f, 1f, 1f, 0.45f),
                    TextAnchor.UpperCenter, left, 196f, half, 36f);
                Ui.Label(dock, $"Have{i}",
                    open ? $"{StatName(key)} {Ui.Digits(have)}"
                         : $"{StatName(key)}が {Ui.Digits(way.Requires - have)} 足りない",
                    24, new Color(1f, 1f, 1f, open ? 0.62f : 0.45f),
                    TextAnchor.UpperCenter, left, 238f, half, 32f);
                Ui.Label(dock, $"Odds{i}",
                    open ? $"とどく {Trails.OddsIfTake(raid, i)}%" : "通れない", 28,
                    open ? Color.white : new Color(1f, 1f, 1f, 0.40f),
                    TextAnchor.UpperCenter, left, 276f, half, 38f);

                // ⭐ その道に何が乗っているか（敵は回数が戻る／▲は先の関門を開ける）
                Ui.Label(dock, $"Has{i}", Contents(raid, way), 24,
                    new Color(1f, 1f, 1f, open ? 0.72f : 0.35f),
                    TextAnchor.UpperCenter, left, 318f, half, 32f);
            }
        }

        /// <summary>その道に乗っている物の要約。⚠️ 無ければ空。</summary>
        private static string Contents(Raid raid, Way way)
        {
            int mobs = 0, boons = 0, banes = 0;
            int at = way.To;
            for (int n = 0; n < way.Length - 1; n++)
            {
                var sq = raid.Trail.Squares[at];
                if (sq.Kind == SquareKind.Mob && !raid.Beaten.Contains(at)) mobs++;
                else if (sq.Kind == SquareKind.Boon) boons++;
                else if (sq.Kind == SquareKind.Bane) banes++;
                if (sq.Ways.Count == 0) break;
                at = sq.Ways[0].To;
            }
            var text = "";
            if (mobs > 0) text += $"敵×{mobs} ";
            if (boons > 0) text += $"▲×{boons} ";
            if (banes > 0) text += $"▼×{banes}";
            return text.Length == 0 ? "なにもない" : text.TrimEnd();
        }

        // ── 進行 ────────────────────────────────────

        private static void RollNow(App app, Raid raid)
        {
            if (_rolling || raid.Rolls <= 0) return;
            var nest = app.CurrentNest;
            if (nest == null) { app.Show(Screen.Nests); return; }
            _rolling = true;

            // ⚠️ 種は巣と進み具合から作る。その場で引くと、
            //    画面を出入りするだけで出目を選び直せてしまう
            var rng = new Rng(0).Stream(
                $"trail:{nest.Id}:{Games.RaidsOn(app.Game, nest)}"
                + $":{raid.Rolls}:{raid.At}:{raid.Took.Count}:{raid.Beaten.Count}");
            Trails.Roll(rng, raid);
            int face = raid.LastRoll;
            _flagged = raid;
            // ⚠️ ここで組み直さない。⭐ **さいころが止まってから**動かす
            TrailDice.Show(app.Overlay, face, () => { _rolling = false; app.Refresh(); });
        }

        /// <summary>雑魚に出会った。⚠️ **潜入の決着ではない** ── 勝てば続きへ戻る。</summary>
        private static void Meet(App app, Raid raid)
        {
            _handing = true;
            _flagged = raid;
            var nest = app.CurrentNest;
            int square = raid.At;
            BannerView.Show(app.Overlay, "雑魚に囲まれた！", () =>
            {
                _handing = false;
                if (!ReferenceEquals(app.Raid, raid) || app.Showing != Screen.Trail) return;
                app.EnterTrailMobBattle(nest, square);
            });
        }

        /// <summary>決着。⭐ 届いたら盗み、尽きたら親と戦う。</summary>
        private static void Finish(App app, Raid raid)
        {
            _handing = true;
            _flagged = raid;
            bool won = raid.Result == StealOutcome.Success;
            // ⚠️ 「振り切った」と「どの道も通れない」で言い方を変える。直す先が違う
            bool stuck = raid.Result == StealOutcome.Blocked
                && raid.Trail.Squares[raid.At].IsJunction;
            var nest = app.CurrentNest;
            BannerView.Show(app.Overlay,
                won ? "GET!" : stuck ? "どの道も通れない！" : "親に見つかった！", () =>
            {
                _handing = false;
                if (!ReferenceEquals(app.Raid, raid) || app.Showing != Screen.Trail) return;
                app.Raid = null;
                if (won)
                {
                    Games.GrowParty(Games.PartyOf(app.Game));
                    // ⚠️ 盗んだ卵は素質が落ちる。⭐ 盗んだ巣は**残る**
                    app.GainEgg(nest, EggOrigin.Stolen, closeNest: false);
                    Games.RecordRaid(app.Game, nest);
                }
                else
                {
                    // ⚠️ 潜入で負った傷と CT を持ち込む
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
