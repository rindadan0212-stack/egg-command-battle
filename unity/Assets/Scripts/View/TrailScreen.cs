using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>卵強奪。⭐ **分岐するすごろく**（作者の指示 2026-08-20）。
    ///
    /// ⭐ **字で説明せず、置き方と動きで分からせる**（作者の指示 2026-08-20）。
    /// この画面に説明の文は1つも無い。読ませるのは**絵と数**だけ:
    /// <list type="bullet">
    /// <item>残りの回数は「のこり6回」ではなく、**さいころの絵を6つ並べて、使うと空にする**</item>
    /// <item>ステの種類は「壁 攻」ではなく、**剣・心・盾の絵**。⭐ 上の帯と盤で**同じ絵**を使う</item>
    /// <item>通れないことは「HPが270足りない」ではなく、**錠前の絵と、暗く落とした道**</item>
    /// <item>道に何があるかは「敵×2」ではなく、**髑髏の絵を2つ**</item>
    /// <item>どちらを選ぶかは「どちらの道を行く？」ではなく、**2つ並べて置くこと**そのもの</item>
    /// </list>
    ///
    /// ⚠️ 素材は Kenney Board Game Icons（CC0・`Resources/UI/icon/`）。
    /// 白の抜きなので色を掛けて使う（`Resources/UI/NOTICE.md`）。
    ///
    /// ⚠️ 弾いて飛ばす遊び（<see cref="StealScreen"/>）は**別物として残してある**。</summary>
    public static class TrailScreen
    {
        // ── 寸法 ────────────────────────────────────
        private const float HeaderHeight = 214f;
        private const float GroupGap = 14f;
        /// <summary>下の操作帯。⭐ **状態が変わっても高さを変えない。**</summary>
        private const float DockHeight = 392f;

        private const float CellW = 176f;
        private const float CellH = 96f;
        /// <summary>段の高さ（マス＋あいだ）。</summary>
        private const float RowStep = 122f;
        /// <summary>左右に膨らむ幅。</summary>
        private const float Bulge = 252f;
        /// <summary>関門の札の高さ。⭐ マスの上端に帯として重ねる。</summary>
        private const float GateHigh = 36f;
        private const float GoalHeight = 104f;

        private static readonly Color Board = new Color(0.04f, 0.06f, 0.10f, 0.55f);
        private static readonly Color Plate = new Color(1f, 1f, 1f, 0.88f);
        private static readonly Color PlateGone = new Color(1f, 1f, 1f, 0.30f);
        /// <summary>マスとマスを繋ぐ線。⚠️ 薄いと分岐の形が読めない。</summary>
        private static readonly Color Road = new Color(1f, 1f, 1f, 0.42f);
        /// <summary>通れない道。⭐ **暗く落とす**（「通れない」と書かない）。</summary>
        private static readonly Color RoadShut = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color Faint = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color Dark = new Color(0.10f, 0.12f, 0.18f, 0.92f);

        private static bool _handing;
        private static bool _rolling;
        private static Raid _flagged;

        /// <summary>巣を選んで潜入へ。⚠️ 道は <see cref="Trails.OfNest"/> ＝ **巣ごとに固定**。</summary>
        public static void Enter(App app, Nest nest)
        {
            app.CurrentNest = nest;
            app.CurrentIsBoss = false;
            _handing = false;
            _rolling = false;
            _flagged = null;

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

            Header(body, raid);
            BoardOf(body, raid, boardTop, boardHeight);
            Dock(app, body, raid);

            if (_handing || _rolling) return;
            if (raid.Step == RaidStep.Met) { Meet(app, raid); return; }
            if (raid.Result != null) Finish(app, raid);
        }

        // ── 上の帯 ──────────────────────────────────

        /// <summary>手持ちのすべて。⭐ **字は1つも無い。**
        ///
        /// ⭐ さいころの絵の数 ＝ あと何回振れるか。使ったぶんは空のさいころに変わる。
        /// ⭐ 剣・心・盾 ＝ 攻撃・HP・防御。**盤の関門と同じ絵**なので、
        /// 「壁は攻撃で通る」を字で言う必要が無い。</summary>
        private static void Header(RectTransform body, Raid raid)
        {
            var strip = Ui.Block(body, "Header", Board, 0f, 0f, Ui.W, HeaderHeight);

            // ── 残りの回数 ＝ さいころの絵の数 ──────────
            // ⚠️ 「のこり N 回」と書かない。⭐ 並んでいる数がそのまま回数
            const float Die = 46f, DieGap = 8f;
            // ⚠️ 雑魚を倒すと回数が戻るので、最初の数より増えることがある
            int had = raid.Rolls > raid.Given ? raid.Rolls : raid.Given;
            int show = had > 12 ? 12 : had;                 // ⚠️ 並べきれない数は畳む
            for (int i = 0; i < show; i++)
            {
                bool left = i < raid.Rolls;
                var die = Ui.Icon(strip, $"Die {i}", left ? "die" : "die-spent",
                    left ? Color.white : new Color(1f, 1f, 1f, 0.26f),
                    Ui.Margin + (Die + DieGap) * i, 20f, Die);
                // ⭐ 最後の1つは脈打つ。⚠️ 「これで最後」と書かない
                if (left && raid.Rolls == 1) Throb.On(die.rectTransform, 0.12f);
            }
            if (had > show)
                Ui.Label(strip, "More", $"+{had - show}", 26, Faint,
                    TextAnchor.MiddleLeft, Ui.Margin + (Die + DieGap) * show, 20f, 90f, Die);

            // ── 卵までの残りと、届く見込み ──────────────
            int carried = raid.Step == RaidStep.AtJunction ? raid.Pending : 0;
            int left2 = Trails.Left(raid);
            int odds = Trails.Odds(raid, carried);
            const float Wide = 300f;
            float right = Ui.W - Ui.Margin - Wide;
            Ui.Icon(strip, "GoalIcon", "goal", Faint, Ui.W - Ui.Margin - 42f, 20f, 42f);
            Ui.Label(strip, "LeftNum", left2 < 0 ? "—" : left2.ToString(), 30, Faint,
                TextAnchor.UpperRight, right, 22f, Wide - 52f, 40f);
            var odd = Ui.Label(strip, "Odds", $"{odds}%", 50,
                odds < 40 ? Ui.Accent : Color.white,
                TextAnchor.UpperRight, right, 72f, Wide, 68f);
            odd.horizontalOverflow = HorizontalWrapMode.Overflow;

            // ── 攻・HP・防（盤の関門と同じ絵） ──────────
            float chip = (Ui.W - Ui.Margin * 2f - 24f) / 3f;
            var gates = new[] { GimmickKind.Wall, GimmickKind.Damage, GimmickKind.Pressure };
            for (int i = 0; i < gates.Length; i++)
            {
                var key = Trails.StatOf(gates[i]);
                int now = Trails.Usable(raid, key);
                bool lifted = raid.TempLeft[key] > 0 && raid.Temp[key] > 0;
                bool sunk = raid.TempLeft[key] > 0 && raid.Temp[key] < 0;
                float at = Ui.Margin + (chip + 12f) * i;
                var box = Ui.Plate(strip, $"Stat {i}", "pill",
                    new Color(1f, 1f, 1f, lifted ? 0.24f : 0.12f), at, 142f, chip, 58f);
                var tint = lifted ? Ui.Accent : sunk ? Ui.Danger : Color.white;
                Ui.Icon(box, "I", IconOf(gates[i]), tint, 14f, 11f, 36f);
                Ui.Label(box, "N", Ui.Digits(now), 28, tint,
                    TextAnchor.MiddleLeft, 60f, 0f, chip - 68f, 58f);
                // ⭐ 増減は矢印の絵で出す（「+60%」の符号を読ませない）
                if (raid.TempLeft[key] > 0)
                    Ui.Icon(box, "T", "arrow", tint, chip - 40f, 15f, 30f, lifted ? 90f : -90f);
            }
        }

        // ── 盤 ──────────────────────────────────────

        private struct Spot { public float X, Y; }

        /// <summary>マスを縦に並べる。⭐ **入口が下、卵が上。分かれ道で左右に膨らむ。**
        /// ⚠️ 1画面に収めない（作者の指示）。縦にスクロールし、駒が見える所へ寄せる。</summary>
        private static void BoardOf(RectTransform body, Raid raid, float top, float height)
        {
            var trail = raid.Trail;
            var spots = Layout(trail, out float tall);
            float content = tall + GoalHeight + RowStep;

            var view = Ui.Scroller(body, "Board", 0f, top, Ui.W, height, content);
            var back = Ui.Block(view, "Ground", Board, 0f, 0f, Ui.W, content);
            back.SetAsFirstSibling();

            // ⭐ 卵は横いっぱいの帯。⚠️ 字を置かない ── 旗の絵だけで行き先だと分かる
            var goal = Ui.Plate(view, "Goal", "panel", Ui.Accent,
                Ui.Margin, 8f, Ui.W - Ui.Margin * 2f, GoalHeight);
            Ui.Icon(goal, "I", "goal", Ui.OnLead,
                (Ui.W - Ui.Margin * 2f - 62f) / 2f, (GoalHeight - 62f) / 2f, 62f);

            // ── 道の線（マスより先に敷く） ────────────
            for (int i = 0; i < trail.Count; i++)
            {
                var sq = trail.Squares[i];
                for (int w = 0; w < sq.Ways.Count; w++)
                {
                    var way = sq.Ways[w];
                    bool open = !sq.IsJunction || Trails.CanPass(raid, way);
                    bool took = sq.IsJunction && raid.Took.TryGetValue(i, out int t) && t == w;
                    Link(view, spots[i], spots[way.To], $"L{i}-{w}",
                        took ? Ui.Accent : open ? Road : RoadShut);
                }
            }

            var cells = new RectTransform[trail.Count];
            for (int i = 0; i < trail.Count; i++) cells[i] = Cell(view, raid, i, spots[i]);

            // ⭐ 関門の札は**入る先のマスの上端**に重ねる（どちらの道の関門か迷わない）
            for (int i = 0; i < trail.Count; i++)
            {
                var sq = trail.Squares[i];
                if (!sq.IsJunction) continue;
                foreach (var way in sq.Ways)
                    if (way.IsGated && cells[way.To] != null)
                        Gate(cells[way.To], way, Trails.CanPass(raid, way));
            }

            // ⭐ 分かれ道に立っているなら、行ける先を光らせる
            if (raid.Step == RaidStep.AtJunction)
            {
                var ways = trail.Squares[raid.At].Ways;
                foreach (var way in ways)
                    if (Trails.CanPass(raid, way)) Ring(view, spots[way.To]);
            }

            Piece(cells[raid.At], raid);

            // ⭐ 駒が見える所へ寄せる
            var scroll = view.GetComponentInParent<ScrollRect>();
            if (scroll != null)
                scroll.verticalNormalizedPosition = Mathf.Clamp01(
                    1f - (spots[raid.At].Y - height * 0.45f)
                    / Mathf.Max(1f, content - height));
        }

        /// <summary>マスの置き場所。⭐ **Core が持っている段と左右をそのまま読む。**</summary>
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
                    Y = GoalHeight + 28f + (deep - sq.Row) * RowStep,
                };
            }
            return spots;
        }

        /// <summary>1マス。⭐ **中身は絵1つ。**字は数（%）だけ。</summary>
        private static RectTransform Cell(RectTransform ground, Raid raid, int index, Spot at)
        {
            var trail = raid.Trail;
            var sq = trail.Squares[index];
            if (sq.IsGoal) return ground;

            bool behind = raid.Took.ContainsKey(index) || index < raid.At;
            var cell = Ui.Rect($"Cell {index}", ground);
            Ui.Place(cell, at.X, at.Y, CellW, CellH);
            // ⚠️ 素の四角を塗らない。⭐ 素材の器（丸角＋影）を敷く
            var face = cell.gameObject.AddComponent<Image>();
            face.sprite = Ui.SkinSprite("panel");
            face.type = Image.Type.Sliced;
            face.pixelsPerUnitMultiplier = 1f;
            face.color = behind ? PlateGone : Plate;

            // ⚠️ 関門の札のぶん、中身を下げる
            float pad = GatedInto(trail, index) ? GateHigh : 0f;
            float high = CellH - pad;
            float midY = pad + high / 2f;

            switch (sq.Kind)
            {
                case SquareKind.Mob:
                    bool beaten = raid.Beaten.Contains(index);
                    face.color = beaten ? PlateGone : Dark;
                    Ui.Icon(cell, "I", "mob",
                        beaten ? new Color(1f, 1f, 1f, 0.30f) : Color.white,
                        CellW / 2f - 24f, midY - 24f, 48f);
                    break;

                case SquareKind.Boon:
                case SquareKind.Bane:
                    bool up = sq.Kind == SquareKind.Boon;
                    var ink = behind ? Ui.InkFaint : up ? Ui.GoodInk : Ui.DangerInk;
                    // ⭐ 矢印＋ステの絵＋数。⚠️ 「▲防+60%」の記号を字で書かない
                    Ui.Icon(cell, "A", "arrow", ink, 14f, midY - 17f, 34f, up ? 90f : -90f);
                    Ui.Icon(cell, "S", IconOf(sq.Stat), ink, 52f, midY - 17f, 34f);
                    Ui.Label(cell, "N", $"{(sq.Percent < 0 ? -sq.Percent : sq.Percent)}%", 26, ink,
                        TextAnchor.MiddleLeft, 92f, pad, CellW - 96f, high);
                    break;

                default:
                    if (sq.IsJunction)
                    {
                        // ⭐ 分かれ道は丸い節。⚠️ 何も書かない
                        Ui.Round(cell, "Hub", CellW / 2f - 22f, midY - 22f, 44f,
                            behind ? Ui.InkFaint : Ui.Ink);
                        break;
                    }
                    Ui.Icon(cell, "I", "plain",
                        new Color(0f, 0f, 0f, behind ? 0.12f : 0.26f),
                        CellW / 2f - 16f, midY - 16f, 32f);
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

        /// <summary>マスとマスを繋ぐ線。</summary>
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

        /// <summary>関門の札。⭐ **ステの絵＋数。通れないなら錠前。**
        /// ⚠️ 「壁」「通れない」と書かない ── 絵が同じなら結び付けは説明が要らない。</summary>
        private static void Gate(RectTransform cell, Way way, bool open)
        {
            var tag = Ui.Plate(cell, "Gate", "pill", open ? Ui.Accent : Dark,
                0f, 0f, CellW, GateHigh);
            var ink = open ? Ui.OnLead : new Color(1f, 1f, 1f, 0.62f);
            Ui.Icon(tag, "I", IconOf(way.Gate), ink, 10f, 4f, 28f);
            Ui.Label(tag, "N", Ui.Digits(way.Requires), 22, ink,
                TextAnchor.MiddleLeft, 44f, 0f, CellW - 84f, GateHigh);
            if (!open) Ui.Icon(tag, "L", "locked", ink, CellW - 34f, 4f, 28f);
        }

        /// <summary>いま居るマスに置く駒。⭐ **3体で1つ**（作者の決定）。</summary>
        private static void Piece(RectTransform cell, Raid raid)
        {
            if (cell == null) return;
            const float Size = 56f;
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

            if (raid.Result != null || raid.Step == RaidStep.Met || _rolling) return;
            if (raid.Step == RaidStep.AtJunction) { Fork(app, dock, raid, w); return; }

            // ⭐ 押しどころはさいころの絵だけ。⚠️ 「さいころを振る」と書かない
            var button = Ui.Tappable(dock, "Roll", "", () => RollNow(app, raid),
                Ui.Margin, 108f, w, 150f, lead: true, enabled: raid.Rolls > 0);
            Ui.Icon(button.transform, "I", "die",
                raid.Rolls > 0 ? Ui.OnLead : Ui.InkFaint, w / 2f - 44f, 31f, 88f);
        }

        /// <summary>分かれ道での選択。⭐ **2つ並べて置くことが、そのまま問いになる。**
        ///
        /// ⚠️ 「どちらの道を行く？」と書かない。
        /// ⚠️ 「HPが270足りない」と書かない ── 錠前の絵と、押せない札で分かる。
        /// ⚠️ 「敵×2」と書かない ── 髑髏を2つ置く。</summary>
        private static void Fork(App app, RectTransform dock, Raid raid, float w)
        {
            var ways = raid.Trail.Squares[raid.At].Ways;
            float half = (w - 20f) / 2f;

            for (int i = 0; i < ways.Count && i < 2; i++)
            {
                int pick = i;
                var way = ways[i];
                bool open = Trails.CanPass(raid, way);
                var key = Trails.StatOf(way.Gate);
                float left = Ui.Margin + (half + 20f) * i;

                // ── 押しどころ ── 何マスか（数だけ）
                var button = Ui.Tappable(dock, i == 0 ? "Near" : "Far", "",
                    () => { Trails.Take(raid, pick); app.Refresh(); },
                    left, 24f, half, 132f, lead: i == 0, enabled: open);
                var ink = !open ? Ui.InkFaint : i == 0 ? Ui.OnLead : Ui.Ink;
                Ui.Label(button.transform, "Steps", way.Length.ToString(), 56, ink,
                    TextAnchor.MiddleCenter, 0f, 0f, half, 132f);

                // ── 要るもの ── ステの絵＋数（＋通れないなら錠前）
                var need = Ui.Plate(dock, $"Need{i}", "pill", open ? Ui.Accent : Dark,
                    left + 12f, 168f, half - 24f, 48f);
                var needInk = open ? Ui.OnLead : new Color(1f, 1f, 1f, 0.62f);
                Ui.Icon(need, "I", IconOf(way.Gate), needInk, 12f, 8f, 32f);
                Ui.Label(need, "N", Ui.Digits(way.Requires), 24, needInk,
                    TextAnchor.MiddleLeft, 52f, 0f, half - 100f, 48f);
                if (!open) Ui.Icon(need, "L", "locked", needInk, half - 60f, 8f, 32f);

                // ── いま持っている量（同じ絵で並べる） ──
                Ui.Icon(dock, $"HaveI{i}", IconOf(way.Gate), Faint, left + 12f, 228f, 28f);
                Ui.Label(dock, $"HaveN{i}", Ui.Digits(Trails.Usable(raid, key)), 24,
                    open ? Color.white : new Color(1f, 1f, 1f, 0.45f),
                    TextAnchor.MiddleLeft, left + 48f, 228f, half - 60f, 28f);

                // ── その道に何が乗っているか（絵を並べるだけ） ──
                Contents(dock, raid, way, left + 12f, 272f, half - 24f, open);

                // ── 届く見込み ──
                Ui.Label(dock, $"Odds{i}", open ? $"{Trails.OddsIfTake(raid, i)}%" : "—", 30,
                    open ? Color.white : new Color(1f, 1f, 1f, 0.40f),
                    TextAnchor.MiddleLeft, left + 12f, 320f, half - 24f, 40f);
            }
        }

        /// <summary>その道に乗っている物を**絵で並べる**。⚠️ 数を字で書かない。</summary>
        private static void Contents(RectTransform dock, Raid raid, Way way,
            float left, float top, float wide, bool open)
        {
            const float Size = 34f, Gap = 6f;
            float at = left;
            int drawn = 0;
            int cursor = way.To;
            for (int n = 0; n < way.Length - 1 && drawn < 6; n++)
            {
                var sq = raid.Trail.Squares[cursor];
                string icon = null;
                Color tint = Color.white;
                if (sq.Kind == SquareKind.Mob && !raid.Beaten.Contains(cursor))
                { icon = "mob"; tint = Color.white; }
                else if (sq.Kind == SquareKind.Boon) { icon = "arrow"; tint = Ui.Good; }
                else if (sq.Kind == SquareKind.Bane) { icon = "arrow"; tint = Ui.Danger; }

                if (icon != null && at + Size <= left + wide)
                {
                    float turn = icon != "arrow" ? 0f : sq.Kind == SquareKind.Boon ? 90f : -90f;
                    Ui.Icon(dock, $"C{cursor}", icon,
                        open ? tint : new Color(tint.r, tint.g, tint.b, 0.35f), at, top, Size, turn);
                    at += Size + Gap;
                    drawn++;
                }
                if (sq.Ways.Count == 0) break;
                cursor = sq.Ways[0].To;
            }
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
            TrailDice.Show(app.Overlay, face, () => { _rolling = false; app.Refresh(); });
        }

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

        private static void Finish(App app, Raid raid)
        {
            _handing = true;
            _flagged = raid;
            bool won = raid.Result == StealOutcome.Success;
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
                    app.GainEgg(nest, EggOrigin.Stolen, closeNest: false);
                    Games.RecordRaid(app.Game, nest);
                }
                else app.EnterBattle(nest, false, raid.Hp, raid.Cooldowns);
            });
        }

        // ── 絵の割り当て ────────────────────────────
        // ⭐ **ここが唯一の対応表。**上の帯・盤の関門・下の札が全部これを通るので、
        //    同じものには必ず同じ絵が出る（＝字で結び付けを説明しなくてよい）。

        private static string IconOf(GimmickKind gate)
        {
            switch (gate)
            {
                case GimmickKind.Wall: return "stat-atk";
                case GimmickKind.Damage: return "stat-hp";
                default: return "stat-def";
            }
        }

        private static string IconOf(StatKey key)
        {
            switch (key)
            {
                case StatKey.Atk: return "stat-atk";
                case StatKey.Hp: return "stat-hp";
                default: return "stat-def";
            }
        }
    }
}
