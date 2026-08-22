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

        // ⭐ **マスを2倍にして、あいだを詰めた**（2026-08-20・作者の指示
        //    「マスの大きさを2倍にしてもっと余白を少なく」）。
        // ⚠️ あいだ（RowStep − CellH）は 26 のまま据え置き ── マスだけ大きくすることで、
        //    見た目の余白の割合が半分以下になる。
        // ⚠️ **横は 4列ぶんで割る。**⭐ マスを縦に2倍のまま、横は 4×248 + 隙間 で
        //    画面の幅（1080）に収める（2026-08-20 の4列化）。
        //    ⚠️ 352（縦と同じ2倍）だと 4列で 1408 になり、入らない。
        private const float CellW = 248f;
        private const float CellH = 192f;
        /// <summary>段の高さ（マス＋あいだ）。⚠️ あいだは 26 のまま。</summary>
        private const float RowStep = 218f;
        /// <summary>一番外の車線までの横のずれ。⭐ 車線 ±<see cref="Trail.LaneEdge"/> がここ。
        /// ⚠️ 本数は毎回変わる（2〜4）ので、**端を決めて割る**形にしてある。</summary>
        private const float LaneStep = 396f;
        private const float GoalHeight = 176f;

        /// <summary>関門を踏んだあと、余分に置く間（秒）。
        /// ⭐ **重い所で一拍おく**と、そこが重く感じる（2026-08-21 の手ざわりの調べ）。
        /// ⚠️ 長いと「詰まった」に見える。</summary>
        private const float GateBeat = 0.26f;

        /// <summary>卵まであと何マスから、行き先が脈打ちはじめるか。
        /// ⚠️ 「あと少し！」と字で書かない（作者の指示・この画面に説明の文は置かない）。</summary>
        private const int GoalNear = 6;

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

        /// <summary>⭐ **いま駒を描く場所。**-1 なら本当の居場所（<c>raid.At</c>）。
        /// ⚠️ 振ったあと、タップされるまで**前のマス**に留めておくために要る。</summary>
        private static int _shownAt = -1;

        /// <summary>⭐ **出目で行ける先の道筋。**空なら待っていない。
        /// ⚠️ 末尾のマスを光らせて、**押されたら**その道筋を歩く
        /// （2026-08-20・作者の指示「止まるマスを光らせてそこをタップして移動」）。
        /// ⭐ 関門で通れない道は Core が外してあるので、**光っていない ＝ 行けない**。
        /// だから鍵の絵も要らない（同・作者の指摘）。</summary>
        private static System.Collections.Generic.List<System.Collections.Generic.List<int>> _open;

        /// <summary>歩いている最中。⚠️ この間は押させない。</summary>
        private static bool _walking;

        /// <summary>⭐ **歩いている最中の、残りの歩数。**-1 なら歩いていない。
        ///
        /// ⚠️ 出目と進むマス数が合っているかを、**数えさせずに見せる**ための数
        /// （2026-08-21）。以前この食い違いを疑われたとき、盤を見ても確かめようが無かった。</summary>
        private static int _walkLeft = -1;

        /// <summary>巣を選んで潜入へ。⚠️ 道は <see cref="Trails.OfNest"/> ＝ **巣ごとに固定**。</summary>
        public static void Enter(App app, Nest nest)
        {
            app.CurrentNest = nest;
            app.CurrentIsBoss = false;
            _handing = false;
            _rolling = false;
            _flagged = null;
            _shownAt = -1;
            _walkLeft = -1;
            _open = null;
            _walking = false;

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
            if (!ReferenceEquals(_flagged, raid))
            {
                _handing = false; _rolling = false; _walking = false;
                _shownAt = -1; _walkLeft = -1; _open = null;
                // ⚠️ **覚え直す。**⭐ 直さないと毎回ここへ落ちて、
                //    この後で組んだ `_open` を次の組み直しが消し続ける（2026-08-21 監査）。
                _flagged = raid;
            }

            // ⭐ **行ける先を並べるのは、ここの仕事。**
            //
            // ⚠️ <see cref="RaidStep.Choosing"/> に入る道は**1つではない**。
            // 振ったあと（<see cref="RollNow"/>）だけでなく、
            // ⭐ **関門で「N マス進む」を買ったときも** Choosing になる。
            // ⚠️ 並べるのを RollNow の中だけでやっていたので、買った直後は
            //    **光るマスが0・さいころの釦は例外**で、そこから何も押せなくなった
            //    （2026-08-21 に実機で再現）。
            // ⭐ 画面側の1か所に寄せておけば、Core が Choosing に入る道を増やしても壊れない。
            if (!_handing && !_rolling && !_walking && _open == null
                && raid.Result == null && raid.Step == RaidStep.Choosing)
            {
                var reach = Trails.Reach(raid, raid.Pending);
                if (reach.Count == 0) Trails.Stuck(raid);
                // ⚠️ **ここで return しない。**⭐ 返すと Header も盤も下の帯も描かれず、
                //    次の組み直し（0.13秒後）まで**画面が空になる**（2026-08-21 監査）。
                //    `Walk` が `_walking` を立てるので、この下の見張りは正しく働く。
                else if (reach.Count == 1) Walk(app, raid, reach[0]);
                else _open = reach;
            }

            float boardTop = HeaderHeight + GroupGap;
            float boardHeight = Ui.H - Ui.TopBarHeight - boardTop - DockHeight;

            Header(body, raid);
            BoardOf(app, body, raid, boardTop, boardHeight);
            Dock(app, body, raid);

            // ⚠️ 歩いている間・行き先を待っている間は、次の場面へ進めない
            //    （雑魚の戦闘が駒の到着前に始まってしまう）
            if (_handing || _rolling || _walking || _open != null) return;
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

            // ── 卵までの残り ─────────────────────────
            // ⭐ **最短マス数で出す**（2026-08-20・作者の指示
            //    「％で表示するのではなく最短マス数を表示するように」）。
            // ⚠️ %は「たぶん届く」という当てにならない数で、何をすれば良いか分からなかった。
            //    ⭐ マス数なら、さいころの残りと直に見比べられる。
            // ⚠️ **見せかけの居場所で数える。**⭐ `Trails.Go` は歩き始めに `raid.At` を
            //    終点へ動かすので、本当の居場所で数えると**帯の数字だけが先に飛ぶ**
            //    ── 残り歩数の札と真っ向から矛盾する（2026-08-21 監査）。
            int left2 = Trails.LeftFrom(raid.Trail, _shownAt >= 0 ? _shownAt : raid.At);
            const float Wide = 300f;
            float right = Ui.W - Ui.Margin - Wide;
            Ui.Icon(strip, "GoalIcon", "goal", Faint, Ui.W - Ui.Margin - 42f, 20f, 42f);
            Ui.Label(strip, "LeftUnit", "マス", 26, Faint,
                TextAnchor.UpperRight, right, 22f, Wide - 52f, 40f);
            Ui.Label(strip, "LeftNum", left2 < 0 ? "—" : left2.ToString(), 50,
                left2 < 0 ? Faint : Color.white,
                TextAnchor.UpperRight, right, 56f, Wide - 52f, 62f);

            Purse(strip, raid);
        }

        /// <summary>⭐ **いくら払えるか。**（2026-08-21 に戻した）
        ///
        /// ⚠️ **関門を消費にした当日に、残高の表示だけが画面から消えていた。**
        /// ⭐ ステが払う物になった以上、手持ちが見えないと「払うか見送るか」を
        /// 選びようがない ── 判断させる仕組みだけ作って、判断の材料を消していた。
        ///
        /// ⚠️ **字を置かない。**盤の関門と**同じ絵**（剣・心・盾）を使うので、
        /// 「この数と、あの関門の数を見比べる」ことは説明しなくても分かる。
        /// ⭐ 一時増減が効いている間は色が変わる ── ▲▼ のマスと同じ色。</summary>
        private static void Purse(RectTransform strip, Raid raid)
        {
            const float Art = 48f, Row = 64f, Top = 132f, Gap = 8f;
            var keys = new[] { StatKey.Atk, StatKey.Hp, StatKey.Def };
            float wide = (Ui.W - Ui.Margin * 2f) / keys.Length;
            for (int i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                float left = Ui.Margin + wide * i;
                // ⚠️ **`Usable` で出す。**⭐ 払ったぶんを引き、一時増減を掛けた
                //    「いま実際に出せる額」でないと、関門の数と見比べられない。
                int have = Trails.Usable(raid, key);
                int pct = raid.TempLeft[key] > 0 ? raid.Temp[key] : 0;
                var ink = pct > 0 ? Ui.GoodInk : pct < 0 ? Ui.DangerInk : Color.white;
                Ui.Icon(strip, $"Purse {i}", IconOf(key), ink, left, Top + (Row - Art) / 2f, Art);
                Ui.Label(strip, $"PurseN {i}", Ui.Digits(Shown(key, have)), 36, ink,
                    TextAnchor.MiddleLeft, left + Art + Gap, Top, wide - Art - Gap * 2f, Row);
            }
        }

        /// <summary>⭐ **画面に出す数。**⚠️ HP だけ桁が違う。
        ///
        /// ⚠️ 内側の HP は素の値で、⭐ 画面に出ている HP は **×<see cref="Battle.HpScale"/>**
        /// （2026-08-19 の桁上げ）。素のまま出すと、HP を要求する関門だけが
        /// **手持ちの 1/105 の数**に見え、「安い関門」だと誤解する。
        /// ⚠️ 旧画面（<see cref="StealStage"/>）には同じ補正が入っていたのに、
        /// すごろくへ載せ替えたときに落とした（2026-08-21 の討論で発覚）。</summary>
        private static int Shown(StatKey key, int value) =>
            key == StatKey.Hp ? value * Battle.HpScale : value;

        /// <summary>関門が要求する量を、画面の単位で。</summary>
        private static int Shown(GimmickKind kind, int price) =>
            Shown(Trails.StatOf(kind), price);

        // ── 盤 ──────────────────────────────────────

        private struct Spot { public float X, Y; }

        /// <summary>マスを縦に並べる。⭐ **入口が下、卵が上。分かれ道で左右に膨らむ。**
        /// ⚠️ 1画面に収めない（作者の指示）。縦にスクロールし、駒が見える所へ寄せる。</summary>
        private static void BoardOf(App app, RectTransform body, Raid raid, float top, float height)
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
                (Ui.W - Ui.Margin * 2f - 110f) / 2f, (GoalHeight - 110f) / 2f, 110f);
            // ⭐ **近づくと脈打つ。**⚠️ 「あと少し！」と字で書かない
            int far = Trails.LeftFrom(trail, _shownAt >= 0 ? _shownAt : raid.At);
            if (far >= 0 && far <= GoalNear) Throb.On(goal, 0.05f);

            // ── 道の線（マスより先に敷く） ────────────
            for (int i = 0; i < trail.Count; i++)
            {
                var sq = trail.Squares[i];
                for (int w = 0; w < sq.Ways.Count; w++)
                {
                    var way = sq.Ways[w];
                    // ⚠️ **通れない道はもう無い**（2026-08-21・関門は只で入れる）
                    bool took = sq.IsJunction && raid.Took.TryGetValue(i, out int t) && t == w;
                    Link(view, spots[i], spots[way.To], $"L{i}-{w}", took ? Ui.Accent : Road);
                }
            }

            var cells = new RectTransform[trail.Count];
            for (int i = 0; i < trail.Count; i++) cells[i] = Cell(view, raid, i, spots[i]);

            // ⭐ **行ける先を全部光らせ、そこを押させる。**
            // ⚠️ 押しどころはマスそのもの ── 別に釦を出すと、どこへ行くのか分からない。
            // ⭐ 通れない道は Core が外してあるので、光っていなければ行けない。
            int here = _shownAt >= 0 ? _shownAt : raid.At;
            if (_open != null)
            {
                var raidNow = raid;
                foreach (var path in _open)
                {
                    int end = path[path.Count - 1];
                    if (cells[end] == null) continue;
                    Ring(view, spots[end]);
                    var go = cells[end].gameObject.AddComponent<Button>();
                    go.transition = Selectable.Transition.None;
                    // ⚠️ **押した瞬間の出目で引き直す。**⭐ `_open` は組んだ時点の
                    //    `Pending` で作った道筋なので、間に関門の「N マス進む」などが
                    //    挟まると**古い長さのまま光り続ける**（作者の報告 2026-08-22
                    //    「出た目に関わらず1マスしか進めないときがある」）。
                    // ⚠️ 捕まえた `path` をそのまま渡すと、その古い長さで動いてしまう。
                    int want = end;
                    go.onClick.AddListener(() => Choose(app, raidNow, want));
                    Throb.On(cells[end], 0.06f);
                }
            }

            Piece(cells[here], raid);

            // ⭐ 駒が見える所へ寄せる
            var scroll = view.GetComponentInParent<ScrollRect>();
            if (scroll != null)
                scroll.verticalNormalizedPosition = Mathf.Clamp01(
                    1f - (spots[here].Y - height * 0.45f)
                    / Mathf.Max(1f, content - height));
        }

        /// <summary>マスの置き場所。⭐ **Core が持っている段と左右をそのまま読む。**
        ///
        /// ⚠️ **揺らさない**（2026-08-21 に外した）。⭐ 段は「1段＝1歩」を運んでいる
        /// 唯一の手がかりなので、段の揃いを崩すと**歩数が目で数えられなくなる**。
        /// ⚠️ 横の揺らぎは、マスがほぼ接している（248px に対し隙間 16px）ので
        /// 2〜10px しか動かず、狙った「列の不揃い」は起きなかった。
        /// ⭐ 守るべきものを壊して、狙ったものは達成できていなかった。</summary>
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
                    // ⭐ 車線は -3 〜 +3。⚠️ 一番外（±LaneEdge）が画面の端に来るよう割る
                    X = mid + sq.Lane * (LaneStep / (float)Trail.LaneEdge),
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
            if (sq.IsGoal)
            {
                // ⚠️ **`ground` を返してはいけない。**⭐ `Ui.Scroller` が返すのは
                //    巻物の**中身そのもの**（盤ぜんぶ）。返すと、卵が行ける先に入った瞬間に
                //    **盤全体に押しどころが付き、盤全体が拍動**した
                //    （＝どこを押しても勝てる。成功する潜入では毎回通る道・2026-08-21 監査）。
                // ⭐ 卵にも他と同じ大きさの当たり判定を1枚置く。絵は帯のほうが持っている。
                var mark = Ui.Rect($"Cell {index}", ground);
                Ui.Place(mark, at.X, at.Y, CellW, CellH);
                var clear = mark.gameObject.AddComponent<Image>();
                clear.color = new Color(0f, 0f, 0f, 0f);
                return mark;
            }

            // ⚠️ **添字で「通り過ぎた」を決めない。**⭐ 添字は（段, 列）の順なので、
            //    同じ段で自分より左にあるマスまで暗く落ちていた（2026-08-21 監査）。
            bool behind = raid.Took.ContainsKey(index)
                || trail.Squares[index].Row < trail.Squares[raid.At].Row;
            var cell = Ui.Rect($"Cell {index}", ground);
            Ui.Place(cell, at.X, at.Y, CellW, CellH);
            // ⚠️ 素の四角を塗らない。⭐ 素材の器（丸角＋影）を敷く
            var face = cell.gameObject.AddComponent<Image>();
            face.sprite = Ui.SkinSprite("panel");
            face.type = Image.Type.Sliced;
            face.pixelsPerUnitMultiplier = 1f;
            face.color = behind ? PlateGone : Plate;

            // ⚠️ 関門も**1マス**なので、他のマスと同じ大きさ・同じ中身の置き方をする
            //    （2026-08-20・作者の指示「関門は1マスとしてカウントするので他のマスと
            //    被らないように」）。以前はマスの上端に帯を重ねていた。
            const float pad = 0f;
            const float high = CellH;
            const float midY = CellH / 2f;

            switch (sq.Kind)
            {
                case SquareKind.Gate:
                    Gate(cell, sq, face, behind);
                    break;

                case SquareKind.Mob:
                    bool beaten = raid.Beaten.Contains(index);
                    face.color = beaten ? PlateGone : Dark;
                    Ui.Icon(cell, "I", "mob",
                        beaten ? new Color(1f, 1f, 1f, 0.30f) : Color.white,
                        CellW / 2f - 48f, midY - 48f, 96f);
                    break;

                case SquareKind.Boon:
                case SquareKind.Bane:
                {
                    var gift = sq.Face;
                    if (gift == null) break;
                    bool up = gift.Amount >= 0;
                    var ink = behind ? Ui.InkFaint : up ? Ui.GoodInk : Ui.DangerInk;
                    // ⭐ 矢印＋ステの絵＋数。⚠️ 「▲防+60%」の記号を字で書かない
                    // ⚠️ 数の枠は**要る幅より広く**取る。⭐ 「30%」で 109 要るのに 102 しか
                    //    無く、字が枠からはみ出していた（2026-08-20 に実測）
                    Ui.Icon(cell, "A", "arrow", ink, 10f, midY - 26f, 52f, up ? 90f : -90f);
                    Ui.Icon(cell, "S", IconOf(gift.Stat), ink, 66f, midY - 26f, 52f);
                    int shown = gift.Amount < 0 ? -gift.Amount : gift.Amount;
                    Ui.Label(cell, "N", $"{shown}%", 44, ink,
                        TextAnchor.MiddleLeft, 124f, pad, CellW - 132f, high);
                    break;
                }

                case SquareKind.Plain:
                    // ⚠️ **分かれ道に印を付けない**（2026-08-21・作者の指摘
                    //    「黒丸のマスの役割もよくわからない」）。
                    // ⭐ 分かれ道は**遊びに現れない**。マスを直接押す形にした 2026-08-20 から、
                    //    プレイヤーが分かれ道で止まって道を選ぶ場面は無い。
                    //    ⚠️ 印を残していたので、盤の4枚に1枚が**意味のない黒丸**になっていた。
                    Ui.Icon(cell, "I", "plain",
                        new Color(0f, 0f, 0f, behind ? 0.12f : 0.26f),
                        CellW / 2f - 32f, midY - 32f, 64f);
                    break;

                default:
                    // ⚠️ **知らない顔つきを黙って素通りにしない。**
                    // ⭐ `default` が素通りを描いていた頃は、マスの種類を足した瞬間に
                    //    **盤の上では素通りに化ける**（＝足したことに気づけない）。
                    //    ⚠️ この画面は「絵で分からせる」規約なので、
                    //    絵が無いマスは**仕様の穴**であって既定値ではない（2026-08-21 の討論）。
                    Debug.LogError($"知らないマスの顔つき: {sq.Kind}（絵が決まっていません）");
                    Ui.Label(cell, "N", "?", 64, Ui.DangerInk,
                        TextAnchor.MiddleCenter, 0f, pad, CellW, high);
                    break;
            }
            return cell;
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

        /// <summary>関門のマス。⭐ **ステの絵＋要る量＋段の粒。**
        ///
        /// ⭐ **1マスとして描く**（2026-08-20・作者の指示「関門は1マスとしてカウントする
        /// ので他のマスと被らないように」）。
        /// ⚠️ 以前はマスの上に帯として重ねていたので、他のマスと重なる問題が付いて回った。
        /// ⚠️ 「壁」「通れない」と書かない ── 絵が同じなら結び付けは説明が要らない。
        /// ⚠️ 鍵の絵も出さない ── 行ける先は光るので、光らなければ行けない。</summary>
        private static void Gate(RectTransform cell, Square sq, Image face, bool behind)
        {
            var toll = sq.Toll;
            if (toll == null) return;
            face.color = behind ? PlateGone : Ui.Accent;
            var ink = behind ? new Color(1f, 1f, 1f, 0.45f) : Ui.OnLead;

            // ⭐ **上が払う量、下がもらえる物。**（2026-08-21）
            // ⚠️ 横に並べると数の枠が足りない（実測: 「+4」に 54 要るのに 40 しか無かった）。
            // ⭐ 上下に分ければ、どちらも枠いっぱいまで使える。
            const float Band = 66f, Art = 50f;
            Ui.Icon(cell, "I", IconOf(toll.Kind), ink, 10f, 10f + (Band - Art) / 2f, Art);
            // ⚠️ **`Shown` を通す。**素の値を出すと HP の関門だけ桁がずれる
            Ui.Label(cell, "N", Ui.Digits(Shown(toll.Kind, toll.Price)), 34, ink,
                TextAnchor.MiddleLeft, 68f, 10f, CellW - 78f, Band);

            // ⚠️ 「払うと回数+1」と字で書かない ── さいころの絵か矢印と、数だけ。
            var gift = sq.Face;
            if (gift != null) Reward(cell, gift, ink, 10f, 84f, Band, CellW - 20f);

            // ⭐ 段の粒。⚠️ 満たない段は薄い粒で残す（何段中いくつかが読める）
            const float Pip = 11f, PipGap = 4f;
            float pips = Trail.GateGrades * (Pip + PipGap);
            float from = (CellW - pips) / 2f;
            for (int g = 0; g < Trail.GateGrades; g++)
            {
                Ui.Round(cell, $"Grade {g}", from + g * (Pip + PipGap), CellH - 22f, Pip,
                    g < toll.Grade ? ink : new Color(ink.r, ink.g, ink.b, 0.24f));
            }
        }

        /// <summary>⭐ **払うともらえる物。**⚠️ 字で「回数+1」と書かない。
        ///
        /// ⭐ 回数は**さいころを個数だけ並べる** ── 上の帯（残り回数）と**まったく同じ文法**
        /// なので、説明が要らない。⚠️ さいころの絵は無地なので、
        /// 1つだけ置いても「四角」にしか見えなかった（実機で確認 2026-08-21）。
        /// ⭐ 距離は**卵の絵＋数** ── これも上の帯（卵まで N マス）と同じ組み合わせ。</summary>
        private static void Reward(Transform where, Gift gift, Color ink,
            float left, float top, float high, float wide)
        {
            const float Art = 46f, Gap = 8f;
            // ⚠️ **「回数でなければ距離」と決め打ちしない。**⭐ 前は `!= Rolls` で
            //    振り分けていたので、もらえる物を1種類足した日から
            //    **卵の絵で描かれる**（＝別物なのに同じ見た目）。2026-08-21 の討論の
            //    「既定値で黙って通す」と同じ形なので、種類ごとに書き出す。
            switch (gift.Kind)
            {
                case GiftKind.Rolls:
                    // ⚠️ **無地の `die` を使わない。**単独で置くと「四角」にしか見えなかった
                    //    （実機で確認 2026-08-21）。⭐ 目のある `die-3` なら1つでもさいころに見える。
                    //    ⚠️ 上の帯は何個も並ぶので無地でも通じるが、ここは1〜3個しか出ない。
                    int show = gift.Amount > 4 ? 4 : gift.Amount;
                    for (int i = 0; i < show; i++)
                        Ui.Icon(where, $"G{i}", "die-3", ink,
                            left + i * (Art + Gap), top + (high - Art) / 2f, Art);
                    return;

                case GiftKind.Hop:
                    Ui.Icon(where, "G", "goal", ink, left, top + (high - Art) / 2f, Art);
                    Ui.Label(where, "GN", "+" + gift.Amount, 34, ink,
                        TextAnchor.MiddleLeft, left + Art + Gap, top, wide - Art - Gap, high);
                    return;

                case GiftKind.Stat:
                    Ui.Icon(where, "G", IconOf(gift.Stat), ink, left, top + (high - Art) / 2f, Art);
                    Ui.Label(where, "GN", (gift.Amount >= 0 ? "+" : "") + gift.Amount + "%", 34, ink,
                        TextAnchor.MiddleLeft, left + Art + Gap, top, wide - Art - Gap, high);
                    return;

                default:
                    // ⚠️ `Fight` はここへ来ない（`Square.Gate` が弾く）。
                    //    ⭐ 来たなら、絵が決まっていない新しい物。
                    Debug.LogError($"絵の決まっていないもらい物: {gift.Kind}");
                    Ui.Label(where, "GN", "?", 40, Ui.DangerInk,
                        TextAnchor.MiddleLeft, left, top, wide, high);
                    return;
            }
        }

        /// <summary>いま居るマスに置く駒。⭐ **編成ぜんぶで1つ**（作者の決定）。</summary>
        private static void Piece(RectTransform cell, Raid raid)
        {
            if (cell == null) return;
            // ⭐ **左下の隅に小さく置く**（2026-08-21）。
            // ⚠️ 112 で置いていた頃は、立っているマスの絵と数を半分隠していた。
            // ⭐ ここなら**払う量（上の段）は必ず読める**。
            //    もらう物（下の段）は隠れるが、そのときは下の札が同じものを出している。
            // ⚠️ **関門と同じ橙にしない**（作者の指摘 2026-08-21「色が同じで見づらい」）。
            //    ⭐ 駒は `Ui.Accent` で、関門のマスも `Ui.Accent` ── 同じ色なので、
            //    関門の上に立った瞬間に駒が地に溶けていた。
            // ⭐ **濃紺の縁 ＋ 白い地**にする。マスは 橙（関門）／濃紺（雑魚）／白（素通り）の
            //    3種類しか無いので、この2色なら**どの上でも必ず浮く**。
            const float Size = 84f, Edge = 5f;
            var rim = Ui.Round(cell, "PieceRim", 6f - Edge, CellH - Size - 6f - Edge,
                Size + Edge * 2f, Ui.Ink);
            var disc = Ui.Round(rim, "Piece", Edge, Edge, Size, Color.white);
            if (raid.Party.Count > 0)
                Ui.PixelOf(disc, "Art", raid.Party[0], Size * 0.14f, Size * 0.14f, Size * 0.72f);
            Jolt.Play(rim, new Vector2(0f, 14f), 0.20f);

            // ⭐ **踏むたびに潰れて伸びる。**⚠️ 座標を動かすだけだと「滑って」いる
            Squash.Play(rim, 0.22f);

            // ⭐ **歩いている最中だけ、残りの歩数を出す。**
            // ⚠️ 「出目のぶん進んでいない」と疑われたとき、盤を見ても確かめようが無かった
            //    （2026-08-20 の指摘）。⭐ 6→5→4… と減れば、数えなくても合っていると分かる。
            if (_walkLeft <= 0) return;
            const float Tag = 54f;
            var tag = Ui.Round(cell, "Left", Size - 10f, CellH - Size - 26f, Tag, Ui.Ink);
            Ui.Label(tag, "N", _walkLeft.ToString(), 32, Color.white,
                TextAnchor.MiddleCenter, 0f, 0f, Tag, Tag);
        }

        /// <summary>行き先の印。⚠️ マスより一回り大きく、後ろに敷いて縁だけ見せる。</summary>
        private static void Ring(RectTransform ground, Spot at)
        {
            const float Halo = 12f;
            var ring = Ui.Ring(ground, "Landing",
                at.X - Halo, at.Y - Halo, CellW + Halo * 2f, CellH + Halo * 2f);
            // ⚠️ **一番下にしない。**⭐ 一番下にすると地（Ground）が前へ出て、
            //    印が暗い面の**下**に沈む（2026-08-21 監査）。地の1つ上に置く。
            ring.SetSiblingIndex(1);
        }

        // ── 下の操作帯 ──────────────────────────────

        private static void Dock(App app, RectTransform body, Raid raid)
        {
            float top = Ui.H - Ui.TopBarHeight - DockHeight;
            var dock = Ui.Block(body, "Dock", Board, 0f, top, Ui.W, DockHeight);
            float w = Ui.W - Ui.Margin * 2f;

            // ⚠️ **歩いている最中は何も出さない。**⭐ Core の段は歩き終わる前に
            //    Moved へ戻っているので、見張らないと**駒が動いている最中に振れて**しまう。
            if (raid.Result != null || raid.Step == RaidStep.Met || _rolling || _walking) return;
            // ⚠️ **道を選ぶ札はもう出さない**（2026-08-20・作者の指摘
            //    「マスを直接押すようになったので下の道を選ぶボタンはいらない」）。

            if (raid.Step == RaidStep.Offered) { Till(app, dock, raid, w); return; }
            // ⚠️ **振れるのは Moved のときだけ。**⭐ 他の段で釦を出すと、
            //    押した瞬間に <see cref="Trails.Roll"/> が撥ねて進行不能に見える。
            if (raid.Step != RaidStep.Moved) return;

            // ⭐ 押しどころはさいころの絵だけ。⚠️ 「さいころを振る」と書かない
            var button = Ui.Tappable(dock, "Roll", "", () => RollNow(app, raid),
                Ui.Margin, 108f, w, 150f, lead: true, enabled: raid.Rolls > 0);
            Ui.Icon(button.transform, "I", "die",
                raid.Rolls > 0 ? Ui.OnLead : Ui.InkFaint, w / 2f - 44f, 31f, 88f);
        }

        /// <summary>⭐ **払うか、払わないか。**（2026-08-21・作者の指示
        /// 「対価を払えば有利になる」）
        ///
        /// ⚠️ **字で説明しない。**左が「払う量」、右が「もらえる物」。
        /// ⭐ 払う側には**払うステの絵と数**、もらう側には**さいころか矢印と数**を置くだけで、
        /// 交換だと分かる。⚠️ 「〇〇を払って△△を得ますか？」とは書かない。
        ///
        /// ⚠️ 押さない選択も**同じ大きさで**置く ── 小さくすると
        /// 「押すのが正解」に見えて、判断そのものが消える。</summary>
        private static void Till(App app, RectTransform dock, Raid raid, float w)
        {
            var sq = raid.Trail.Squares[raid.At];
            var toll = sq.Toll;
            var gift = sq.Face;
            if (toll == null || gift == null) { Trails.Pass(raid); app.Refresh(); return; }

            const float High = 150f, Gap = 16f;
            float half = (w - Gap) / 2f;

            // ── 払う ──────────────────────────────
            var pay = Ui.Tappable(dock, "Pay", "", () =>
            {
                if (raid.Step != RaidStep.Offered) return;
                Trails.Pay(raid);
                Paid(app, raid);
                app.Refresh();
            }, Ui.Margin, 108f, half, High, lead: true);
            // ⭐ 払う量（左）→ もらう物（右）。⚠️ 矢印は「交換」の合図。
            // ⚠️ 数の枠は**要る幅より広く**取ること（「+2」に 54 要る）。
            // ⚠️ **数を折り返させない。**⭐ 104 では「42,000」（font32 で約120要る）が
            //    2行に折れていた（作者の指摘 2026-08-21）。HP の関門は ×105 されるので
            //    6桁になりうる ── 180 まで広げ、さらに折り返しを切っておく。
            const float Art = 52f, Num = 180f;
            float step = (half - 40f - Art * 2f - Num) / 1f;   // 矢印のぶん
            Ui.Icon(pay.transform, "S", IconOf(toll.Kind), Ui.OnLead, 18f, High / 2f - 26f, Art);
            var price = Ui.Label(pay.transform, "N", Ui.Digits(Shown(toll.Kind, toll.Price)), 32,
                Ui.OnLead, TextAnchor.MiddleLeft, 18f + Art + 4f, 0f, Num, High);
            // ⚠️ **折り返しを切る。**⭐ 枠を広げても、桁が伸びれば同じことが起きる。
            //    数は1行で読めることが先（はみ出すほうがまだ読める）。
            price.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Icon(pay.transform, "A", "arrow", new Color(1f, 1f, 1f, 0.55f),
                18f + Art + Num + 8f, High / 2f - 18f, step > 36f ? 36f : step);
            Reward(pay.transform, gift, Ui.OnLead,
                half - Art - Num - 14f, 0f, High, Art + Num + 4f);

            // ── 払わない ───────────────────────────
            var skip = Ui.Tappable(dock, "Skip", "", () =>
            {
                if (raid.Step != RaidStep.Offered) return;
                Trails.Pass(raid);
                app.Refresh();
            }, Ui.Margin + half + Gap, 108f, half, High);
            Ui.Icon(skip.transform, "I", "arrow", Ui.Ink,
                half / 2f - 30f, High / 2f - 30f, 60f, 0f);
        }

        // ── 進行 ────────────────────────────────────

        /// <summary>⭐ **押されたマスへ、いまの出目で行く道筋を引き直して歩く。**
        ///
        /// ⚠️ 光らせた時点の道筋を覚えて渡さない ── 覚えると、
        /// 間に出目が変わる出来事（関門の「N マス進む」）が挟まったときに
        /// **古い長さで動く**（2026-08-22）。⭐ 行き先だけ覚えて、道は毎回引き直す。</summary>
        private static void Choose(App app, Raid raid, int goal)
        {
            if (_walking || raid.Step != RaidStep.Choosing) return;
            foreach (var path in Trails.Reach(raid, raid.Pending))
            {
                if (path[path.Count - 1] != goal) continue;
                Walk(app, raid, path);
                return;
            }
            // ⚠️ **黙って何もしないをしない。**⭐ 光っていたのに行けないのは、
            //    盤か出目が押す前と変わったということ ── 組み直して光らせ直す。
            Debug.LogError($"すごろく: マス {goal} へ行く道が無い（出目 {raid.Pending} / 居るマス {raid.At}）");
            _open = null;
            app.Refresh();
        }

        /// <summary>駒を1マスずつ歩かせる。⭐ **行き先は既に決まっている**（Core が動かした）。
        ///
        /// ⚠️ 前は振った瞬間に飛んでいたので、何マス進んだのか目で追えなかった
        /// （2026-08-20・作者の指示）。</summary>
        private static void Walk(App app, Raid raid, System.Collections.Generic.List<int> path)
        {
            if (_walking || path == null || path.Count < 2) return;
            _open = null;
            _walking = true;
            _shownAt = path[0];
            // ⭐ **出目そのものから始める**（2026-08-21）。⚠️ 1歩目で減らしてから描くと、
            //    6 を振っても 5,4,3,2,1 としか出ず、**1つ足りなく見える**。
            _walkLeft = path.Count;
            _flagged = raid;

            var board = raid.Trail;
            // ⚠️ **先に動かしてから歩かせる。**⭐ Core が跡（通った道）を記録するので、
            //    歩きの見せ方と本当の居場所がずれない
            // ⚠️ **食い違いを黙って通さない。**⭐ Core が出目と歩数を突き合わせて投げるので、
            //    ここで捕まえて**声を上げてから引き直す**（作者の報告 2026-08-22）。
            //    ⚠️ 投げっぱなしにすると歩きが始まらず、潜入がそこで固まる。
            try
            {
                Trails.Go(raid, path);
            }
            catch (System.InvalidOperationException error)
            {
                Debug.LogError($"すごろく: {error.Message}（居るマス {raid.At} / 出目 {raid.Pending}）");
                // ⚠️ **Console だけに書かない。**⭐ 遊んでいる人の目に入らないと、
                //    「1マスしか進まなかった」としか報告できない（2026-08-22）。
                BannerView.Show(app.Overlay, $"進みがずれた（{error.Message}）", null);
                var again = Trails.Reach(raid, raid.Pending);
                if (again.Count == 0) { _walking = false; Trails.Stuck(raid); app.Refresh(); return; }
                path = again[0];
                _walkLeft = path.Count;
                Trails.Go(raid, path);
            }
            TrailWalk.Show(app.Overlay, path,
                at =>
                {
                    // ⚠️ 別の潜入に切り替わっていたら触らない
                    if (!ReferenceEquals(app.Raid, raid) || app.Showing != Screen.Trail) return;
                    _shownAt = at;
                    _walkLeft--;
                    app.Refresh();
                    // ⚠️ **`<= 1` で見る。**⭐ 札の数を出目から始めるために
                    //    `_walkLeft` を1つ増やしたとき、ここの境目を直し忘れて
                    //    **▲▼ の数字も雑魚の揺れも一度も出ていなかった**（2026-08-21 監査）。
                    //    `onStep` は path.Count-1 回しか呼ばれないので、0 には落ちない。
                    Landed(app, raid, at, _walkLeft <= 1);
                },
                () =>
                {
                    _walking = false;
                    _shownAt = -1;
                    _walkLeft = -1;
                    if (!ReferenceEquals(app.Raid, raid) || app.Showing != Screen.Trail) return;
                    app.Refresh();
                },
                // ⭐ **関門では一拍おく。**⚠️ ここが一番「払った甲斐」を感じるべき所なのに、
                //    素通りのマスと同じ速さで抜けていた（2026-08-21 の手ざわりの調べ）。
                at => board.Squares[at].IsGate ? GateBeat : 0f);
        }

        /// <summary>1マス踏んだ瞬間。⭐ **何が起きたかを、字でなく動きと数で出す。**
        ///
        /// ⚠️ 盤は1マスごとに組み直されるので、演出は<see cref="App.Overlay"/> と
        /// 同じ「組み直されない層」（<see cref="Fx"/>）へ出すこと。
        /// 盤の中に出すと、次の一歩で一緒に消える。</summary>
        private static void Landed(App app, Raid raid, int index, bool last)
        {
            var sq = raid.Trail.Squares[index];
            var cell = Find(app, $"Cell {index}");
            if (cell == null) return;
            var fx = Fx.Get(app.transform);
            var at = fx.PointOf(cell, new Vector2(CellW / 2f, CellH / 2f));

            switch (sq.Kind)
            {
                case SquareKind.Gate:
                    // ⚠️ `last` を見ないと、**通り抜けただけの関門まで光る**（2026-08-21 監査）
                    if (!last) break;
                    // ⭐ 払える関門に**着いた**合図。⚠️ 「払えます」と書かない
                    if (Trails.CanPay(raid, index))
                    {
                        fx.Ring(at, Ui.Accent, CellW * 0.5f, CellW * 1.25f, 0.34f);
                        break;
                    }
                    // ⭐ 払い済みなら静かでよい ── もう用が無いマス
                    if (raid.Paid.Contains(index)) break;
                    // ⚠️ **足りないときも必ず何か返す。**⭐ 止まったのに何も起きないと、
                    //    「壊れている」と読まれる ── 実際、討論で真っ先に挙がった
                    //    （2026-08-21）。⭐ 縮む輪＋要る量が跳ねる ＝ 「届かなかった」。
                    fx.Ring(at, Ui.DangerInk, CellW * 0.75f, CellW * 0.42f, 0.24f);
                    var price = cell.Find("N") as RectTransform;
                    if (price != null) Jolt.Play(price, new Vector2(14f, 0f), 0.20f);
                    break;

                case SquareKind.Boon:
                case SquareKind.Bane:
                {
                    // ⚠️ 止まったマスだけが効く。⭐ 通り抜けたぶんは出さない
                    var gift = sq.Face;
                    if (!last || gift == null) break;
                    bool up = gift.Amount >= 0;
                    fx.Number(at, (up ? "+" : "") + gift.Amount + "%",
                        up ? Ui.GoodInk : Ui.DangerInk, 58f);
                    fx.Ring(at, up ? Ui.GoodInk : Ui.DangerInk, CellW * 0.4f, CellW, 0.30f);
                    break;
                }

                case SquareKind.Mob:
                    if (!last || raid.Beaten.Contains(index)) break;
                    Shake.Play(app.Stage, 20f);
                    break;

                case SquareKind.Plain:
                    // ⭐ 素通りは**静かでよい**。⚠️ ここに何か足すと、
                    //    盤の3分の1で毎回鳴ることになる。
                    break;

                default:
                    // ⚠️ **知らない顔つきを黙って素通り扱いにしない。**
                    //    ⭐ `Cell` と同じ規則（2026-08-21）── 絵も演出も無いマスは
                    //    既定値ではなく**仕様の穴**。
                    Debug.LogError($"踏んだときの演出が決まっていないマス: {sq.Kind}");
                    break;
            }
        }

        /// <summary>いま出ている画面から、名前で1つ拾う。⚠️ 見つからなければ null。</summary>
        private static RectTransform Find(App app, string name)
        {
            foreach (var rect in app.GetComponentsInChildren<RectTransform>(false))
                if (rect.name == name) return rect;
            return null;
        }

        /// <summary>払った瞬間。⭐ **払った甲斐を、字ではなく動きで出す。**</summary>
        private static void Paid(App app, Raid raid)
        {
            var cell = Find(app, $"Cell {raid.At}");
            if (cell == null) return;
            var fx = Fx.Get(app.transform);
            var at = fx.PointOf(cell, new Vector2(CellW / 2f, CellH / 2f));
            fx.Ring(at, Ui.Accent, CellW * 0.4f, CellW * 1.4f, 0.36f);
            fx.Impact(at, Ui.Accent);
            Shake.Play(app.Stage, 16f);
        }

        private static void RollNow(App app, Raid raid)
        {
            // ⚠️ 段を見ずに振ると <see cref="Trails.Roll"/> が撥ねる（最後の砦）
            if (_rolling || raid.Rolls <= 0 || raid.Step != RaidStep.Moved) return;
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
            TrailDice.Show(app.Overlay, face, () =>
            {
                _rolling = false;
                if (!ReferenceEquals(app.Raid, raid) || app.Showing != Screen.Trail) return;

                var open = Trails.Reach(raid, raid.Pending);
                if (open.Count == 0)
                {
                    // ⚠️ 1マスも動けない ── そこで見つかる
                    Trails.Stuck(raid);
                    app.Refresh();
                    return;
                }
                // ⭐ **行ける先が1つだけなら、押させずに進む**（作者の指示 2026-08-20）
                if (open.Count == 1) { Walk(app, raid, open[0]); return; }
                _open = open;
                app.Refresh();
            });
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
            // ⚠️ **どちらの負け方かは、居るマスで分ける。**⭐ 敵に負けたなら、
            //    そこは敵のマス。⚠️ 以前は「分かれ道に居るか」で見ていたが、
            //    分かれ道かどうかは負け方と何の関係も無かった（2026-08-21 に直した）。
            // ⚠️ 「どの道も通れない」はいまや**起きない**（どのマスからも関門でない
            //    1段先がある）。安全網として残してあるだけ。
            bool stuck = raid.Result == StealOutcome.Blocked
                && raid.Trail.Squares[raid.At].Kind != SquareKind.Mob;
            if (!won) Shake.Play(app.Stage, 34f);
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

        /// <summary>⚠️ **知らない関門を黙って「防」にしない。**
        /// ⭐ 既定値で通すと、関門の種類を足したとき**盾の絵で出てしまう**
        /// ── 払う先が違うのに見た目が同じ、が一番たちが悪い（2026-08-21 の討論）。</summary>
        private static string IconOf(GimmickKind gate)
        {
            switch (gate)
            {
                case GimmickKind.Wall: return "stat-atk";
                case GimmickKind.Damage: return "stat-hp";
                case GimmickKind.Pressure: return "stat-def";
                default:
                    Debug.LogError($"知らない関門: {gate}（絵が決まっていません）");
                    return "stat-def";
            }
        }

        private static string IconOf(StatKey key)
        {
            switch (key)
            {
                case StatKey.Atk: return "stat-atk";
                case StatKey.Hp: return "stat-hp";
                case StatKey.Def: return "stat-def";
                default:
                    Debug.LogError($"関門にできないステ: {key}");
                    return "stat-def";
            }
        }
    }
}
