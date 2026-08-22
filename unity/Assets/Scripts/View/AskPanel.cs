using System;
using UnityEngine;
using UnityEngine.UI;

namespace EggCommand.View
{
    /// <summary>「本当にやりますか」を一度だけ聞く札。
    ///
    /// ⭐ **取り返しがつかない操作にだけ挟む。**⚠️ 何にでも挟むと、
    /// 読まずに押す癖が付いて、肝心なときに効かなくなる。
    ///
    /// ⚠️ 分解・配合には**挟まない**（前からの決まり ── 取り返しがつかないほうが
    /// 判断に重みが出る）。⭐ ここを使うのは、押し間違いが**戦いの負け**になるような、
    /// 誤爆の代償が大きいものだけ。
    /// </summary>
    public static class AskPanel
    {
        private const float PanelLeft = 96f;
        private const float PanelWidth = 888f;
        private const float PanelHeight = 460f;
        private const float Pad = 32f;
        private static float Inner => PanelWidth - Pad * 2f;

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

        /// <param name="go">やる側の札の字。⚠️ 「はい」と書かない ── ⭐ **何が起きるか**を書く
        /// （「あきらめる」「逃がす」）。押す前に読み返せる言葉にしておく。</param>
        public static void Show(App app, string title, string body, string go, Action onGo)
        {
            Close();
            var root = Ui.Rect("AskPanel", app.Overlay);
            Ui.Stretch(root);
            _open = root.gameObject;

            // ⭐ 地を暗くして後ろを押させない。⚠️ ここを押しても閉じない
            //    （聞いている最中に、触っただけで消えるのは危ない）
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.62f);
            var block = root.gameObject.AddComponent<Button>();
            block.transition = Selectable.Transition.None;
            block.targetGraphic = dim;

            float top = (Ui.H - PanelHeight) / 2f - 120f;
            var panel = Ui.Card(root, "Panel", PanelLeft, top, PanelWidth, PanelHeight);

            Ui.Label(panel, "Title", title, 40, Ui.Ink, TextAnchor.UpperLeft,
                Pad, Pad, Inner, 56f);
            Ui.Label(panel, "Body", body, 28, Ui.InkDim, TextAnchor.UpperLeft,
                Pad, Pad + 72f, Inner, 140f);

            const float Gap = 16f;
            float wide = (Inner - Gap) / 2f;
            float row = PanelHeight - Ui.Tap - Pad;

            // ⚠️ **やめる側を左（先）に置く。**⭐ 指は左から探すので、
            //    危ないほうを先に置くと勢いで押される
            Ui.Tappable(panel, "Stop", "やめる", Close, Pad, row, wide, Ui.Tap);
            var yes = Ui.Tappable(panel, "Go", go,
                () => { Close(); onGo?.Invoke(); },
                Pad + wide + Gap, row, wide, Ui.Tap);
            // ⭐ 危ないほうは赤。⚠️ 主導線の黄にしない（勧めているように見える）
            var face = yes.GetComponent<Image>();
            if (face != null) face.sprite = Ui.SkinSprite("button-danger");
        }
    }
}
