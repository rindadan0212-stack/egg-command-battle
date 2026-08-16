using System;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>画面ごとの地の色。⭐ どの画面にいるかが色で分かる。</summary>
    public enum Sky
    {
        Home,
        Nest,
        Battle,
        Hatch,
        Breed,
        Box,
    }

    /// <summary>画面を組む道具。
    ///
    /// ⭐ 位置は「左上を原点に、右と下へ」で指定する。
    /// Unity の既定（中心原点・上が正）のまま書くと、画面の上下と符号がずれて読み違える。
    /// ここを1箇所に閉じ込めておけば、各画面は素直な座標だけを書けばよい。
    ///
    /// ⚠️ 角丸は押せるものだけ。区切りは余白で作り、線を引くときは一辺だけ。
    /// ⚠️ 押せるものは高さ 44 以上（指で押せる下限）。
    /// </summary>
    public static class Ui
    {
        // 縦持ち前提の設計座標
        public const float W = 1080f;
        public const float H = 1920f;

        /// <summary>押せるものの最小の高さ。⚠️ ここを下回らせない。</summary>
        public const float Tap = 112f;

        public const float Margin = 48f;
        public const float TopBarHeight = 132f;
        public const float DockHeight = 232f;

        // ── 色 ──────────────────────────────────────────
        // 無彩色を支配的に、差し色は1つ。画面ごとに地の色だけを変える。
        public static readonly Color Ink = new Color32(0xef, 0xe9, 0xdc, 0xff);
        public static readonly Color InkDim = new Color32(0x93, 0x8b, 0x7c, 0xff);
        public static readonly Color InkFaint = new Color32(0x60, 0x5a, 0x50, 0xff);
        /// <summary>差し色。⭐ 主導線1つと「今ここ」にしか使わない。</summary>
        public static readonly Color Accent = new Color32(0xd8, 0xb4, 0x5c, 0xff);
        public static readonly Color Danger = new Color32(0xc9, 0x6e, 0x6e, 0xff);
        public static readonly Color Good = new Color32(0x8f, 0xc9, 0x6e, 0xff);
        public static readonly Color Panel = new Color32(0x24, 0x20, 0x1a, 0xff);
        public static readonly Color PanelHi = new Color32(0x2f, 0x2a, 0x22, 0xff);

        public static Color SkyOf(Sky sky)
        {
            switch (sky)
            {
                case Sky.Home: return new Color32(0x16, 0x1c, 0x22, 0xff);
                case Sky.Nest: return new Color32(0x1a, 0x1c, 0x14, 0xff);
                case Sky.Battle: return new Color32(0x20, 0x15, 0x14, 0xff);
                case Sky.Hatch: return new Color32(0x1c, 0x1a, 0x22, 0xff);
                case Sky.Breed: return new Color32(0x22, 0x1a, 0x1e, 0xff);
                default: return new Color32(0x1a, 0x18, 0x16, 0xff);
            }
        }

        // ── フォント ────────────────────────────────────
        private static Font _font;

        /// <summary>⚠️ OS のフォントを借りている。Editor（Windows）では日本語が出るが、
        /// Android ビルドでは出ない。日本語のフォントアセットを入れるまでの仮。
        /// ⚠️ 配布物に Windows 同梱フォントを埋め込むのはライセンス上できないので、
        /// 自由に使えるフォント（Noto Sans JP など）を選ぶ判断が要る。</summary>
        public static Font TheFont
        {
            get
            {
                if (_font == null)
                {
                    _font = Font.CreateDynamicFontFromOSFont(
                        new[] { "Yu Gothic UI", "Meiryo", "MS Gothic", "Noto Sans JP" }, 32);
                    if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return _font;
            }
        }

        // ── 部品 ────────────────────────────────────────

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        /// <summary>左上を原点に置く。</summary>
        public static RectTransform Place(Component target, float left, float top, float width, float height)
        {
            var rect = target.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(left, -top);
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>面で区切る。⚠️ 線と二重に使わない。</summary>
        public static RectTransform Block(Transform parent, string name, Color color,
            float left, float top, float width, float height)
        {
            var rect = Rect(name, parent);
            Place(rect, left, top, width, height);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        public static Text Label(Transform parent, string name, string content, int size, Color color,
            TextAnchor anchor, float left, float top, float width, float height)
        {
            var rect = Rect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = content;
            text.font = TheFont;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            Place(rect, left, top, width, height);
            return text;
        }

        /// <summary>押せるもの。⭐ 角丸を使えるのはここだけ（今は面で表す）。
        /// ⚠️ 高さは <see cref="Tap"/> を下回らせない。</summary>
        public static Button Tappable(Transform parent, string name, string label, Action onClick,
            float left, float top, float width, float height,
            bool lead = false, bool enabled = true)
        {
            // ⚠️ 黙って高さを引き上げない。呼ぶ側は渡した高さで次の位置を決めているので、
            //    ここで勝手に伸ばすと親の枠からはみ出す（実際 BOX の行で起きた）。
            //    下限は守らせるが、直すのは呼ぶ側。
            if (height < Tap)
            {
                Debug.LogWarning($"押しどころ '{name}' の高さが {height}。{Tap} 以上にする（指で押せない）");
                height = Tap;
            }
            var rect = Rect(name, parent);
            Place(rect, left, top, width, height);

            var image = rect.gameObject.AddComponent<Image>();
            // ⭐ 主導線だけを塗る。他は面をひとつ持ち上げるだけで済ませる
            image.color = !enabled ? new Color32(0x1e, 0x1b, 0x17, 0xff)
                : lead ? Accent
                : PanelHi;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = enabled;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            var text = Label(rect, "Label", label, 34,
                !enabled ? InkFaint : lead ? new Color32(0x1a, 0x16, 0x12, 0xff) : Ink,
                TextAnchor.MiddleCenter, 0f, 0f, width, height);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            return button;
        }

        /// <summary>ドット絵を貼る。⚠️ 補間しない（<see cref="PixelSpriteTexture"/> が保証する）。</summary>
        public static Image Pixel(Transform parent, string name, PixelSprite sprite, Palette palette,
            float left, float top, float size)
        {
            var rect = Rect(name, parent);
            Place(rect, left, top, size, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = PixelSpriteTexture.ToSprite(sprite, palette);
            image.preserveAspect = true;
            return image;
        }

        public static Image PixelOf(Transform parent, string name, Creature creature,
            float left, float top, float size)
        {
            return Pixel(parent, name, Creatures.SpeciesOf(creature).Sprite,
                Creatures.PaletteOf(creature), left, top, size);
        }

        /// <summary>横に伸びる細い帯。⭐ 量を1本の線で見せる（HP・ゲージ）。</summary>
        public static Image Bar(Transform parent, string name, float ratio, Color color,
            float left, float top, float width, float height)
        {
            Block(parent, name + " Track", new Color32(0x14, 0x12, 0x10, 0xff), left, top, width, height);
            float filled = Mathf.Clamp01(ratio) * width;
            var rect = Rect(name, parent);
            Place(rect, left, top, Mathf.Max(0f, filled), height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>縦に伸びる中身をスクロールさせる器。返るのは中身を入れる場所。</summary>
        public static RectTransform Scroller(Transform parent, string name,
            float left, float top, float width, float height, float contentHeight)
        {
            var viewport = Rect(name, parent);
            Place(viewport, left, top, width, height);
            // ⚠️ Mask は「下に敷いた画像の不透明なところだけ見せる」仕組みなので、
            //    透明な画像を敷くと中身が丸ごと消える（実際それで巣一覧が真っ黒になった）。
            //    切り取りたいだけなら RectMask2D。画像は指が触れる面としてだけ置く。
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, -Mathf.Max(contentHeight, height));
            content.offsetMax = Vector2.zero;

            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }
    }
}
