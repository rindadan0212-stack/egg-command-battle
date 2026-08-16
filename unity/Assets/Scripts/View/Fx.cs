using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>演出。⭐ **説明しないで見せる**ための層。
    ///
    /// ⚠️ ここは画面の組み直しで消えない場所に置く（App Canvas の子にしない）。
    /// 数字が飛んでいる最中に画面が組み直されるので、一緒に消えると何も見えない。
    /// </summary>
    public sealed class Fx : MonoBehaviour
    {
        private static Fx _instance;
        private RectTransform _root;

        public static Fx Get(Transform parent)
        {
            if (_instance != null) return _instance;

            var go = new GameObject("Fx", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 9f; // ⭐ 画面より手前。数字は必ず上に出る
            canvas.sortingOrder = 100;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Ui.W, Ui.H);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            _instance = go.AddComponent<Fx>();
            _instance._root = go.GetComponent<RectTransform>();
            return _instance;
        }

        /// <summary>数字を浮かせる。⭐ 何が起きたかを言葉でなく数と色で出す。</summary>
        public void Number(Vector2 screenPoint, string text, Color color, float size = 46f)
        {
            var rect = Ui.Rect("Number", _root);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(360f, 70f);
            rect.anchoredPosition = screenPoint;

            var label = rect.gameObject.AddComponent<Text>();
            label.text = text;
            label.font = Ui.TheFont;
            label.fontSize = Mathf.RoundToInt(size);
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            rect.gameObject.AddComponent<FloatingNumber>().Begin(label);
        }

        /// <summary>技の名前を頭の上に出す。⭐ 数字より先に、長く、低く出す。
        /// ⚠️ 数字と同じ速さで飛ばすと、読む前に消える（技名は読ませたい字）。</summary>
        public void Shout(Vector2 screenPoint, string text, Color color)
        {
            var rect = Ui.Rect("Shout", _root);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(600f, 80f);
            rect.anchoredPosition = screenPoint;

            var label = rect.gameObject.AddComponent<Text>();
            label.text = text;
            label.font = Ui.TheFont;
            label.fontSize = 42;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            // ⭐ 地の色が何であっても読めるように白抜きにする
            Ui.Knockout(label, 4);

            rect.gameObject.AddComponent<FloatingNumber>().Begin(label, life: 1.0f, rise: 34f);
        }

        /// <summary>広がる丸。⭐ 構えにも被弾にも同じ形を使う（見るべき場所が1つで済む）。</summary>
        public void Ring(Vector2 screenPoint, Color color, float from, float to, float life = 0.4f)
        {
            var rect = Ui.Rect("Ring", _root);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(from, from);
            rect.anchoredPosition = screenPoint;

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Ui.SkinSprite("circle-outline");
            // ⚠️ 濃く出すと体を塗り潰す。輪は「そこ」を指すだけで、見せたいのは中の絵
            image.color = new Color(color.r, color.g, color.b, 0.34f);
            image.raycastTarget = false;

            rect.gameObject.AddComponent<Pulse>().Begin(image, from, to, life);
        }

        /// <summary>当たった瞬間の光。⚠️ 輪より短く・小さく。長いと「まだ効いている」に見える。</summary>
        public void Impact(Vector2 screenPoint, Color color)
        {
            var rect = Ui.Rect("Impact", _root);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(60f, 60f);
            rect.anchoredPosition = screenPoint;

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Ui.SkinSprite("circle");
            image.color = new Color(color.r, color.g, color.b, 0.75f);
            image.raycastTarget = false;

            rect.gameObject.AddComponent<Pulse>().Begin(image, 60f, 260f, 0.26f);
        }

        /// <summary>その場の画面座標を、この層の座標に直す。</summary>
        public Vector2 PointOf(RectTransform target, Vector2 offset)
        {
            var world = target.TransformPoint(new Vector3(
                target.rect.center.x + offset.x, target.rect.center.y + offset.y, 0f));
            var local = _root.InverseTransformPoint(world);
            return new Vector2(local.x, local.y);
        }
    }

    /// <summary>属性の印。⭐ 3すくみを**色**で覚えさせる（説明文を置かない）。
    ///
    /// ⚠️ 字で「鱗に有利なのは羽」と書くのをやめた。
    /// 色が同じ規則で毎回出ていれば、勝った負けたの経験のほうが早く教える。
    /// </summary>
    public static class ElementMark
    {
        public static Color ColorOf(Element element)
        {
            switch (element)
            {
                case Element.Fang: return new Color32(0xe8, 0x7a, 0x5c, 0xff);
                case Element.Plume: return new Color32(0xa8, 0xd8, 0x6e, 0xff);
                default: return new Color32(0x6e, 0xa8, 0xd8, 0xff);
            }
        }

        /// <summary>小さな印を置く。⚠️ 大きさは変えない。同じ形が同じ意味であることが手掛かりになる。</summary>
        public static void Put(Transform parent, Element element, float left, float top)
        {
            Ui.Block(parent, "Elem", ColorOf(element), left, top, 22f, 22f);
            // 有利を取る相手の色を、細い帯で下に添える（矢印の代わり）
            Ui.Block(parent, "ElemBeats", ColorOf(SpeciesTable.Beats(element)), left, top + 26f, 22f, 6f);
        }
    }
}
