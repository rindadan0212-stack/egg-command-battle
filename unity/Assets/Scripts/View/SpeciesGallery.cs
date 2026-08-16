using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>段4 の偵察。Core のデータが Unity の絵になる経路が通っているかを、目で見るためだけの画面。
    ///
    /// ⭐ 見たいのは2つ:
    ///   1. Core が持つ添字色のドット絵が、そのままの形で出るか
    ///   2. 変異＝パレットスワップが Unity 上でも成立するか（同じ絵・色だけ違う）
    ///
    /// ⚠️ これは戦闘画面ではない。段4 で組むときに作り直す。
    /// </summary>
    public sealed class SpeciesGallery : MonoBehaviour
    {
        // 縦持ち前提。企画どおりスマホの縦画面で組む
        private const float DesignWidth = 1080f;
        private const float DesignHeight = 1920f;

        private const float Margin = 48f;
        private const float NameColumnWidth = 208f;
        private const float CellSize = 160f;
        private const float CellGap = 24f;
        private const float RowHeight = 240f;
        private const float RowGap = 40f;
        private const float TopOfRows = 320f; // 画面上端からの距離

        private static readonly Color Ink = new Color32(0xe8, 0xe0, 0xd0, 0xff);
        private static readonly Color InkDim = new Color32(0x8a, 0x81, 0x72, 0xff);
        // ⚠️ 差し色は1つだけ。ここでは「どれが通常色か」を示すためだけに使う
        private static readonly Color Accent = new Color32(0xc9, 0xbd, 0x6e, 0xff);

        private Font _font;

        private void Start()
        {
            _font = LoadFont();
            var root = BuildCanvas();
            BuildTitle(root);

            var species = SpeciesTable.All;
            for (int i = 0; i < species.Count; i++)
            {
                BuildRow(root, species[i], i);
            }
        }

        /// <summary>⚠️ OS のフォントを借りている。Editor（Windows）でしか日本語が出ない。
        /// Android へ持っていくには日本語のフォントアセットが要る。段4 までの仮。</summary>
        private static Font LoadFont()
        {
            var font = Font.CreateDynamicFontFromOSFont(
                new[] { "Yu Gothic UI", "Meiryo", "MS Gothic", "Noto Sans JP" }, 32);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private RectTransform BuildCanvas()
        {
            var canvasGo = new GameObject("Gallery Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            // ⚠️ Overlay にしない。Overlay の Canvas は**カメラの描画に入らない**ので、
            //    カメラ経由の撮影に一切写らない（真っ白の画像が出る）。
            //    戦闘画面ではキャラを world space に置いて UI を上に載せるので、
            //    どのみちカメラ経由のほうが素直。
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 10f;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(DesignWidth, DesignHeight);
            // 縦持ちなので幅に合わせる。高さで合わせると横長の画面で中身がはみ出す
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            var background = NewRect("Background", canvasGo.transform);
            Stretch(background);
            var image = background.gameObject.AddComponent<Image>();
            image.color = new Color32(0x1a, 0x16, 0x12, 0xff);

            return canvasGo.GetComponent<RectTransform>();
        }

        private void BuildTitle(RectTransform root)
        {
            var title = NewText(root, "Title", "Core のドット絵と変異色", 40, Ink, TextAnchor.LowerLeft);
            Place(title, Margin, 120f, DesignWidth - Margin * 2f, 56f);

            var note = NewText(root, "Note", "左端が通常色。右は同じ絵でパレットだけ差し替えたもの", 26, InkDim, TextAnchor.UpperLeft);
            Place(note, Margin, 184f, DesignWidth - Margin * 2f, 40f);
        }

        private void BuildRow(RectTransform root, Species species, int rowIndex)
        {
            float top = TopOfRows + rowIndex * (RowHeight + RowGap);

            var row = NewRect($"Row {species.Id}", root);
            Place(row, Margin, top, DesignWidth - Margin * 2f, RowHeight);

            // ── 名前と属性 ──────────────────────────────
            var name = NewText(row, "Name", species.Name, 36, Ink, TextAnchor.UpperLeft);
            Place(name, 0f, 24f, NameColumnWidth, 48f);

            string element = SpeciesTable.LabelOf(species.Element);
            string skill = Skills.ById(species.Skill1).Name;
            var meta = NewText(row, "Meta", $"{element} / {skill}", 24, InkDim, TextAnchor.UpperLeft);
            Place(meta, 0f, 76f, NameColumnWidth, 40f);

            var stats = NewText(row, "Stats",
                $"HP{species.Base.Hp} 攻{species.Base.Atk}\n防{species.Base.Def} 速{species.Base.Spd}",
                22, InkDim, TextAnchor.UpperLeft);
            Place(stats, 0f, 124f, NameColumnWidth, 72f);

            // ── ドット絵（通常＋変異色） ────────────────
            for (int p = 0; p < species.Palettes.Count; p++)
            {
                float x = NameColumnWidth + p * (CellSize + CellGap);

                var cell = NewRect($"Palette {p}", row);
                Place(cell, x, 0f, CellSize, CellSize);

                var image = cell.gameObject.AddComponent<Image>();
                image.sprite = PixelSpriteTexture.ToSprite(species.Sprite, species.Palettes[p]);
                image.preserveAspect = true;

                if (p == 0)
                {
                    // 主役は1つだけ立てる。「これが通常色」を線1本で言う
                    var bar = NewRect("NormalMark", row);
                    Place(bar, x, CellSize + 8f, CellSize, 3f);
                    bar.gameObject.AddComponent<Image>().color = Accent;
                }

                var caption = NewText(row, $"Caption {p}", p == 0 ? "通常" : $"変異 {p}",
                    22, p == 0 ? Ink : InkDim, TextAnchor.UpperCenter);
                Place(caption, x, CellSize + 20f, CellSize, 32f);
            }
        }

        // ── 部品づくり ──────────────────────────────────
        // ⚠️ 位置は「左上を原点に、右と下へ」で指定する。
        //    Unity の既定（中心原点・上が正）のまま書くと、画面の上下と符号がずれて読み違える。

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private Text NewText(Transform parent, string name, string content, int size, Color color, TextAnchor anchor)
        {
            var rect = NewRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = content;
            text.font = _font;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Place(Component target, float left, float top, float width, float height)
        {
            var rect = target.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(left, -top);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
