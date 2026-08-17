using System;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>孵化器の空き枠に入れる卵を選ぶ覆い。
    ///
    /// ⚠️ **以前はホームの本体（Body）の中に置いていた。**Body は上段の見出しと
    /// 下段の 探索/配合/BOX を含まないので、覆いを出しても**そこだけ明るいまま押せた**
    /// （在庫を開いたまま探索へ行けた）。⭐ だから覆いは Overlay に出す。
    ///
    /// ⚠️ 閉じる押しどころを**目に見える形で置く**。
    /// 以前は「地を押せば閉じる」だけで、閉じ方が画面のどこにも書いていなかった。
    /// </summary>
    public static class EggPickerPanel
    {
        private const float PanelLeft = 48f;
        private const float PanelTop = 240f;
        private const float PanelWidth = 984f;
        private const float PanelHeight = 1440f;
        private const float Pad = 24f;
        private const float Inner = PanelWidth - Pad * 2f;

        /// <summary>卵の札1枚ぶんの場所。⚠️ EggCard.prefab の実寸に合わせる。</summary>
        private const float CellW = 232f;
        private const float CellH = 300f;
        private const int PerRow = 4;

        private static GameObject _open;

        public static void Close()
        {
            if (_open == null) return;
            // ⚠️ Destroy はフレームの終わりまで効かない。残すと覆いが指を吸う
            _open.SetActive(false);
            _open.transform.SetParent(null, false);
            UnityEngine.Object.Destroy(_open);
            _open = null;
        }

        /// <param name="template">札の型（HomeScreen.prefab が持つ EggCard）。</param>
        /// <param name="onPick">卵を選んだ。⭐ 呼び側が孵化器へ入れる。</param>
        public static void Show(App app, EggCard template, Action<Egg> onPick)
        {
            Close();
            if (template == null) return;

            var root = Ui.Rect("EggPickerPanel", app.Overlay);
            Ui.Stretch(root);
            _open = root.gameObject;

            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            var dimButton = root.gameObject.AddComponent<Button>();
            dimButton.targetGraphic = dim;
            dimButton.onClick.AddListener(Close);

            var panel = Ui.Card(root, "Panel", PanelLeft, PanelTop, PanelWidth, PanelHeight);
            var eggs = app.Game.Eggs;

            Ui.Label(panel, "Title", "どの卵を温めるか", 40, Ui.Ink, TextAnchor.UpperLeft,
                Pad, Pad, Inner, 56f);
            // ⚠️ 「卵がありません」と書かない。⭐ 数が言えば足りる
            Ui.Label(panel, "Count", $"棚の卵 {eggs.Count}", 26, Ui.InkDim,
                TextAnchor.UpperLeft, Pad, 86f, Inner, 40f);

            float areaTop = 140f;
            float areaHeight = PanelHeight - Ui.Tap - Pad * 2f - areaTop;
            int rows = (eggs.Count + PerRow - 1) / PerRow;
            var content = Ui.Scroller(panel, "Eggs", Pad, areaTop, Inner, areaHeight, rows * CellH);

            for (int i = 0; i < eggs.Count; i++)
            {
                var egg = eggs[i];
                var card = UnityEngine.Object.Instantiate(template, content);
                card.gameObject.SetActive(true);
                card.name = $"Egg {i}";
                var rect = (RectTransform)card.transform;
                Ui.Place(rect, (i % PerRow) * CellW, (i / PerRow) * CellH, CellW - 8f, CellH - 8f);
                card.Bind(egg, true, app.HatchSpeed, () => { Close(); onPick(egg); });
            }

            Ui.Tappable(panel, "Close", "やめる", Close,
                Pad, PanelHeight - Ui.Tap - Pad, Inner, Ui.Tap);
        }
    }
}
