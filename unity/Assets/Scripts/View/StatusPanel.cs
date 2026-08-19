using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>1体のステを覆いに出す。⭐ **札そのものは BOX と同じもの**を使う。
    ///
    /// ⚠️ ここで並びを書き直さない。同じ個体が画面によって違う顔になり、
    /// 「BOX では見えるのに潜入では見えない」欄が生まれる（配合で実際に起きた）。
    /// ⭐ <c>Prefabs/CreaturePanel</c> を読んで貼るだけにしてある。
    /// </summary>
    public static class StatusPanel
    {
        private const string Path = "Prefabs/CreaturePanel";

        private const float Pad = 24f;

        private static GameObject _open;

        public static void Show(App app, Creature creature)
        {
            if (creature == null) return;
            Close();

            var prefab = Resources.Load<CreaturePanel>(Path);
            if (prefab == null)
            {
                // ⚠️ 黙って何も出さない、をしない。⭐ 出ない理由が分からないと直せない
                Debug.LogWarning($"{Path} が無い。「画面を Prefab に書き出す」を1度走らせること");
                return;
            }

            var root = Ui.Rect("StatusPanel", app.Overlay);
            Ui.Stretch(root);
            _open = root.gameObject;

            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            var dimButton = root.gameObject.AddComponent<Button>();
            dimButton.targetGraphic = dim;
            dimButton.onClick.AddListener(Close);

            // ⚠️ **地を敷いてから貼る。**Prefab の札は器を持たない
            //    （BOX では画面の側が器を出している）。そのまま置くと後ろの盤が
            //    透けて、字がまったく読めなかった（実測）。
            float w = ((RectTransform)prefab.transform).sizeDelta.x;
            float h = ((RectTransform)prefab.transform).sizeDelta.y;
            float boxH = h + Pad * 2f + Ui.Tap + Pad;
            var card = Ui.Card(root, "Card", (Ui.W - w - Pad * 2f) / 2f,
                (Ui.H - boxH) / 2f, w + Pad * 2f, boxH);

            var panel = Object.Instantiate(prefab, card);
            Ui.Place(panel, Pad, Pad, w, h);
            panel.Bind(creature);

            Ui.Tappable(card, "Close", "閉じる", Close,
                (w + Pad * 2f - 520f) / 2f, Pad + h + Pad, 520f, Ui.Tap);
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
    }
}
