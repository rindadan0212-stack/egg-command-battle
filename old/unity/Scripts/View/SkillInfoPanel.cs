using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>技1つの詳細を覆いに出す。⭐ **長押しで開く。**
    ///
    /// ⚠️ 札の上に全部書かない。戦闘中の札は3枚並ぶので、
    /// 説明文まで載せると名前が読めなくなる（実測で札の高さは 130）。
    /// ⭐ 札には**名前・Lv・CT** だけ。残りはここで読ませる。
    ///
    /// ⚠️ 言い回しは <see cref="SkillText"/> から取る。ここで書き直さない
    /// （同じ効果が画面ごとに別の言い方になる ── 実際3通りに割れていた）。
    /// </summary>
    public static class SkillInfoPanel
    {
        private const float Pad = 32f;
        private const float Width = 936f;

        private static GameObject _open;

        /// <param name="slot">どの枠に入っているか。⚠️ **枠1（0）を渡すと CT を 0 で出す。**
        /// ⭐ 枠1 の CT は常に 0 なので、技の表の数をそのまま出すと**画面が嘘をつく**
        /// （実測 2026-08-22: BOX の札は「CT0」、長押しの詳細は「CT 3」と出ていた）。
        /// ⚠️ -1 なら枠を問わない（図鑑）── そのときは技の表の数をそのまま出す。</param>
        public static void Show(App app, Skill skill, int level, int slot = -1)
        {
            if (skill == null) return;
            Close();

            var root = Ui.Rect("SkillInfoPanel", app.Overlay);
            Ui.Stretch(root);
            _open = root.gameObject;

            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            var dimButton = root.gameObject.AddComponent<Button>();
            dimButton.targetGraphic = dim;
            dimButton.onClick.AddListener(Close);

            string power = SkillText.PowerOf(skill);
            string growth = SkillText.StepsOf(skill, slot);
            string body = SkillText.Describe(skill);

            // ⭐ 高さは中身から出す。⚠️ 決め打ちにすると、効果が増えた技で字がはみ出す
            float inner = Width - Pad * 2f;
            float bodyHeight = Mathf.Max(96f, Ui.Height(body, 30, inner));
            // ⚠️ 1行に収まらない技がある（成長は最大4段）。⭐ 高さも実測で出す
            float growthHeight = growth.Length == 0 ? 0f : Mathf.Max(40f, Ui.Height(growth, 24, inner));
            float height = Pad + 56f + 44f + 16f + bodyHeight + 16f + growthHeight + Pad + Ui.Tap + Pad;

            var card = Ui.Card(root, "Card", (Ui.W - Width) / 2f, (Ui.H - height) / 2f,
                Width, height);

            float y = Pad;
            Ui.Label(card, "Name", skill.Name, 40, Ui.Ink, TextAnchor.UpperLeft,
                Pad, y, inner, 56f);
            y += 56f;

            // ⭐ Lv・CT・威力を1行に。⚠️ 3行に割ると札より縦に長い覆いになる
            var meta = new System.Text.StringBuilder();
            meta.Append("Lv ").Append(level).Append(" / ").Append(Skills.MaxLevel);
            // ⚠️ **枠1 は CT 0。**⭐ 技の表の数ではなく、その枠での実際を出す
            meta.Append("　CT ").Append(slot == 0 ? 0 : skill.Ct);
            if (power.Length > 0) meta.Append("　威力 ").Append(power);
            Ui.Label(card, "Meta", meta.ToString(), 26, Ui.InkDim, TextAnchor.UpperLeft,
                Pad, y, inner, 44f);
            y += 44f + 16f;

            var text = Ui.Label(card, "Body", body, 30, Ui.Ink, TextAnchor.UpperLeft,
                Pad, y, inner, bodyHeight);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            y += bodyHeight + 16f;

            if (growth.Length > 0)
            {
                // ⚠️ 「上げると強くなる」と書かない。⭐ Lv2→Lv5 の実数を並べる
                var line = Ui.Label(card, "Growth", growth, 24, Ui.InkFaint,
                    TextAnchor.UpperLeft, Pad, y, inner, growthHeight);
                line.horizontalOverflow = HorizontalWrapMode.Wrap;
                y += growthHeight;
            }

            Ui.Tappable(card, "Close", "閉じる", Close,
                (Width - 520f) / 2f, height - Ui.Tap - Pad, 520f, Ui.Tap);
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
