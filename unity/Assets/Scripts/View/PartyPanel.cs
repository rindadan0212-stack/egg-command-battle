using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>パーティ編成。⭐ **放置と巣で、別の編成を選ぶ。**
    ///
    /// ⚠️ 編成が1本しか無かった頃は、巣に合わせて組み替えると
    /// 放置で溜めていた側も入れ替わり、放置が止まっていた。
    ///
    /// ⭐ **巣の編成は3つ登録できる。**巣ごとに相性が違うので、
    /// 潜るたびに組み直すのではなく、作っておいたものを選ぶ。
    ///
    /// ⚠️ 入口はホームと、巣を選ぶ前の2か所。BOX からは開かない
    /// （BOX は「1体を見る」画面で、編成は「並べる」画面なので混ぜない）。
    /// </summary>
    public static class PartyPanel
    {
        private const float PanelLeft = 48f;
        private const float PanelTop = 180f;
        private const float PanelWidth = 984f;
        private const float PanelHeight = 1560f;
        private const float Pad = 24f;
        private const float Inner = PanelWidth - Pad * 2f;

        private const float KindTop = 150f;
        private const float SlotTop = KindTop + Ui.Tap + 12f;
        /// <summary>「編成1/2/3」の段の高さ。⭐ 上段より**小さい**（下位だと形で示す）。
        /// ⚠️ Ui.Tappable は Ui.Tap を下回る高さを引き上げるので、ここは自前で組む。</summary>
        private const float SlotH = 72f;
        /// ⚠️ **見出しは段のぶんだけ下へ。**高さを見落として
        /// 「編成1/2/3」の札に重ねてしまい、実測で3件の重なりが出た。
        private const float PickedH = 220f;

        private const float CellW = 228f;
        private const float CellH = 200f;
        private const int PerRow = 4;

        private static GameObject _open;
        private static PartyKind _kind = PartyKind.Nest;

        /// <param name="kind">最初に見せる側。⭐ ホームからは放置、巣からは巣。</param>
        public static void Show(App app, PartyKind kind)
        {
            _kind = kind;
            Rebuild(app);
        }

        private static void Rebuild(App app)
        {
            Close();
            Build(app);
        }

        public static void Close()
        {
            if (_open == null) return;
            // ⚠️ Destroy はフレームの終わりまで効かない。残すと覆いが指を吸う
            _open.SetActive(false);
            _open.transform.SetParent(null, false);
            Object.Destroy(_open);
            _open = null;
        }

        private static void Build(App app)
        {
            var root = Ui.Rect("PartyPanel", app.Overlay);
            Ui.Stretch(root);
            _open = root.gameObject;

            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            var close = root.gameObject.AddComponent<Button>();
            close.targetGraphic = dim;
            close.onClick.AddListener(() => { Close(); app.Refresh(); });

            var panel = Ui.Card(root, "Panel", PanelLeft, PanelTop, PanelWidth, PanelHeight);
            Ui.Label(panel, "Title", "パーティ編成", 40, Ui.Ink, TextAnchor.UpperLeft,
                Pad, Pad, Inner, 56f);
            Ui.Label(panel, "Note",
                _kind == PartyKind.Idle
                    ? $"放置で戦い続ける{Games.PartySize}体です。巣へ潜る編成とは別です。"
                    : $"巣へ潜る{Games.PartySize}体です。3つまで登録できます。",
                24, Ui.InkDim, TextAnchor.UpperLeft, Pad, 84f, Inner, 40f);

            Kinds(app, panel);
            // ⚠️ **空白を残さない。**放置側で段を消したとき、その高さぶんの空白が
            //    そのまま残っていた（レビュー指摘 2026-08-19）。⭐ 下を詰める。
            float y = SlotTop;
            if (_kind == PartyKind.Nest) { Slots(app, panel); y += SlotH + 14f; }
            y = Picked(app, panel, y);
            Pool(app, panel, y);

            Ui.Tappable(panel, "Done", "決定", () => { Close(); app.Refresh(); },
                Pad, PanelHeight - Ui.Tap - Pad, Inner, Ui.Tap);
        }

        /// <summary>放置／巣 の切り替え。</summary>
        private static void Kinds(App app, RectTransform panel)
        {
            float half = (Inner - 12f) / 2f;
            Kind(app, panel, "KindIdle", $"放置の{Games.PartySize}体", PartyKind.Idle, Pad, half);
            Kind(app, panel, "KindNest", $"巣へ潜る{Games.PartySize}体", PartyKind.Nest,
                Pad + half + 12f, half);
        }

        private static void Kind(App app, RectTransform panel, string name, string label,
            PartyKind kind, float left, float width)
        {
            var b = Ui.Tappable(panel, name, label, () => { _kind = kind; Rebuild(app); },
                left, KindTop, width, Ui.Tap);
            // ⭐ **選んでいる側だけ塗る。**選んでいない側は白い札に字だけ
            var plate = b.GetComponent<Image>();
            if (plate != null) plate.sprite = Ui.SkinSprite(_kind == kind ? "button-lead" : "panel");
            var ink = b.GetComponentInChildren<Text>();
            if (ink != null) ink.color = _kind == kind ? Ui.OnLead : Ui.Ink;
        }

        /// <summary>巣の編成 3つ。⭐ 押すと、その番号の編成に切り替わる。</summary>
        private static void Slots(App app, RectTransform panel)
        {
            // ⭐ **上段より小さく組む。**⚠️ 同じ大きさ・同じ塗りだと、
            //    どちらが上位の切り替えか読めなかった（レビュー指摘 2026-08-19）。
            float w = Wide(Games.NestPartySlots);
            for (int i = 0; i < Games.NestPartySlots; i++)
            {
                int slot = i;
                int count = app.Game.NestParties[i].Count;
                bool on = Games.Slot(app.Game) == i;
                var box = Ui.Rect($"Set {i}", panel);
                Ui.Place(box, Pad + (w + Gap) * i, SlotTop, w, SlotH);
                var plate = box.gameObject.AddComponent<Image>();
                plate.sprite = Ui.SkinSprite("panel");
                plate.type = Image.Type.Sliced;
                plate.color = on ? Ui.Accent : Color.white;
                Ui.Label(box, "Label", $"編成{i + 1}  {count}体", 24,
                    on ? Ui.OnLead : Ui.InkDim, TextAnchor.MiddleCenter, 0f, 0f, w, SlotH);
                var tap = box.gameObject.AddComponent<Button>();
                tap.targetGraphic = plate;
                tap.onClick.AddListener(() => { app.Game.NestParty = slot; Rebuild(app); });
            }
        }

        /// <summary>いま選んでいる編成。⭐ **押すと外れる。**</summary>
        /// <returns>使い終わった下端。⭐ 次の塊はここから置く。</returns>
        /// <summary>横に <paramref name="many"/> 個並べるときの1つぶんの幅。
        ///
        /// ⚠️ **隙間の数は「個数−1」。**`(Inner - 24) / 個数` と書いてあった頃は
        /// 3個（隙間2つ）でだけ正しく、4個にすると **12px はみ出していた**
        /// （2026-08-20 の4体化で発覚）。⭐ 個数から隙間の数を出す。</summary>
        private static float Wide(int many) =>
            many <= 0 ? Inner : (Inner - Gap * (many - 1)) / many;

        /// <summary>横に並べるときの隙間。</summary>
        private const float Gap = 12f;

        private static float Picked(App app, RectTransform panel, float top)
        {
            var roster = Games.RosterOf(app.Game, _kind);
            float HeadTop = top;
            float PickedTop = HeadTop + 40f;
            Ui.Label(panel, "Head", $"選んでいる {roster.Count}/{Games.PartySize} 体",
                26, Ui.Ink, TextAnchor.UpperLeft, Pad, HeadTop, Inner, 32f);

            float w = Wide(Games.PartySize);
            for (int i = 0; i < Games.PartySize; i++)
            {
                var box = Ui.Rect($"Picked {i}", panel);
                Ui.Place(box, Pad + (w + Gap) * i, PickedTop, w, PickedH);
                var plate = box.gameObject.AddComponent<Image>();
                plate.sprite = Ui.SkinSprite("panel");
                plate.type = Image.Type.Sliced;

                if (i >= roster.Count)
                {
                    // ⚠️ 空き枠は「素質の高い順」で自動的に埋まる。黙って埋まると
                    //    選んだつもりの3体と違うので、空であることを出す
                    plate.color = new Color(1f, 1f, 1f, 0.5f);
                    Ui.Label(box, "Empty", "空き\n（自動で埋まる）", 22, Ui.InkDim,
                        TextAnchor.MiddleCenter, 0f, 0f, w, PickedH);
                    continue;
                }
                var c = FindById(app, roster[i]);
                if (c == null) continue;
                // ⭐ **選択は「角丸の黄色い輪」に揃える**（一覧の升と同じ約束）。
                // ⚠️ 中身より後ろへ入れる（前に出すと絵を隠す）
                var ring = Ui.Ring(box, "Ring", 0f, 0f, w, PickedH);
                ring.SetAsFirstSibling();
                Ui.PixelOf(box, "Art", c, (w - 110f) / 2f, 16f, 110f);
                Ui.Label(box, "Lv", $"Lv {Levels.Of(c)}", 24, Ui.Ink, TextAnchor.MiddleCenter,
                    0f, PickedH - 54f, w, 32f);

                string id = c.Id;
                var tap = box.gameObject.AddComponent<Button>();
                tap.targetGraphic = plate;
                tap.onClick.AddListener(() =>
                {
                    Games.TogglePartyMember(app.Game, id, _kind);
                    Rebuild(app);
                });
            }
            return PickedTop + PickedH + 16f;
        }

        /// <summary>手持ちの一覧。⭐ 押すと入る／外れる。</summary>
        private static void Pool(App app, RectTransform panel, float ListTop)
        {
            var roster = Games.RosterOf(app.Game, _kind);
            var all = app.Game.Storage.Creatures;
            // ⭐ **BOX・配合と同じ升**（作者の指示「すべて揃えたい」）。
            // ⚠️ もう一方の編成に入っているなら一言で出す（同じ個体を両方に入れられるが、
            //    知らずに入れると放置か潜入のどちらかが手薄になる）
            var other = _kind == PartyKind.Idle ? PartyKind.Nest : PartyKind.Idle;
            string mark = other == PartyKind.Idle ? "放置中" : "巣に登録";
            CellGrid.Scroll(panel, "Pool", Pad, ListTop, Inner,
                PanelHeight - ListTop - Ui.Tap - Pad * 2f,
                CellGrid.Template(), all,
                id => roster.Contains(id),
                id =>
                {
                    Games.TogglePartyMember(app.Game, id, _kind);
                    Rebuild(app);
                },
                c => Games.IsInParty(app.Game, c.Id, other) ? mark : $"Lv {Levels.Of(c)}",
                c => Games.IsInParty(app.Game, c.Id, other) ? Ui.AccentInk : Ui.InkDim);
        }

        private static Creature FindById(App app, string id)
        {
            foreach (var c in app.Game.Storage.Creatures) if (c.Id == id) return c;
            return null;
        }
    }
}
