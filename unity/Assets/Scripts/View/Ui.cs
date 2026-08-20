using System;
using System.Collections.Generic;
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
        // ⚠️ 器が**白**になったので、字は濃い側。以前の淡い字だと全部消える。
        //    色はモック（参考/モック_タマゴハンター/）の濃紺に合わせてある。
        public static readonly Color Ink = new Color32(0x2b, 0x33, 0x50, 0xff);

        // ⚠️ **淡い2色は読めていなかった**（2026-08-18・実測して差し替え）。
        //    コントラスト比（白い札 / 空の水色）:
        //      直す前   InkDim 3.0 / 2.3     InkFaint 1.9 / 1.4   ← 目安 4.5 を大きく割る
        //      直した後 InkDim 7.1 / 5.4     InkFaint 5.4 / 4.1
        //    ⭐ 「うすい＝控えめ」を色の薄さでやると、控えめを通り越して消える。
        //    濃さは保ったまま**彩度を落として**主役から下げる。
        //    ⚠️ 空（水色）の上は白い札より条件が悪い。淡いほうを基準に決めること。
        public static readonly Color InkDim = new Color32(0x4c, 0x53, 0x6b, 0xff);
        public static readonly Color InkFaint = new Color32(0x63, 0x69, 0x80, 0xff);
        /// <summary>色つきの札の上に置く字。⭐ どの色の上でも濃紺で通す（読み方を変えない）。</summary>
        public static readonly Color OnLead = new Color32(0x2b, 0x33, 0x50, 0xff);
        /// <summary>差し色。⭐ 主導線1つと「今ここ」にしか使わない。
        /// ⚠️ **面（押しどころ・印・帯）専用。**字には <see cref="AccentInk"/> を使う。</summary>
        public static readonly Color Accent = new Color32(0xf5, 0x9e, 0x0b, 0xff);

        /// <summary>差し色の**字**。⭐ 白い札の上でも読める濃さ（明暗比 5.0）。
        ///
        /// ⚠️ <see cref="Accent"/> をそのまま字に使うと、白い札の上で **2.1** しかない
        /// （実測 2026-08-19・「強化」の数字 +11 で発覚）。
        /// ⭐ 面は明るいままでよい ── 字だけ濃い側へ寄せる。同じ橙なので意味は繋がる。</summary>
        public static readonly Color AccentInk = new Color32(0xb4, 0x53, 0x09, 0xff);
        // ⚠️ 札の中央が鋼色（#647685）なので、沈んだ赤／緑は読めない。明るい側へ寄せた
        // ⚠️ **面（帯・丸・演出の数字）専用。**字には下の …Ink を使う。
        public static readonly Color Danger = new Color32(0xe0, 0x4f, 0x5f, 0xff);
        public static readonly Color Good = new Color32(0x2f, 0xa8, 0x4a, 0xff);

        /// <summary>良い側／危ない側の**字**。⭐ 白い札の上でも読める濃さ。
        ///
        /// ⚠️ <see cref="Good"/> <see cref="Danger"/> をそのまま字に使うと、
        /// 白い札の上で **3.1 / 3.9** しかない（実測 2026-08-19・得意▲と不得意▼で発覚）。
        /// ⭐ 器が鋼色から白へ変わった日に、面の色だけ残って字が取り残されていた。</summary>
        public static readonly Color GoodInk = new Color32(0x1e, 0x7a, 0x38, 0xff);
        public static readonly Color DangerInk = new Color32(0xc0, 0x30, 0x3f, 0xff);
        // WARN: containers are drawn with panel.png; there is no colour-filled container

        /// <summary>画面ごとの地。⚠️ 器が白なので**淡くしすぎない**（輪郭が無いので消える）。
        /// モックの空色〜暖色に寄せた中間の明るさを取る。</summary>
        public static Color SkyOf(Sky sky)
        {
            switch (sky)
            {
                case Sky.Home: return new Color32(0x8f, 0xd8, 0xf7, 0xff);
                case Sky.Nest: return new Color32(0xbd, 0xeb, 0xff, 0xff);
                case Sky.Battle: return new Color32(0xa7, 0xdc, 0xf0, 0xff);
                case Sky.Hatch: return new Color32(0xd7, 0xe7, 0xff, 0xff);
                case Sky.Breed: return new Color32(0xff, 0xdf, 0xe8, 0xff);
                default: return new Color32(0xdc, 0xef, 0xff, 0xff);
            }
        }

        /// <summary>画面ごとの地の絵（空→砂の縦グラデーション）。
        /// ⭐ 中身は Assets/Editor/BuildSky.cs が書き出した PNG。移植元の CSS と同じ停止位置。
        /// ⚠️ 見つからないときは null を返す。呼び側は <see cref="SkyOf"/> の単色へ落ちること。</summary>
        public static Sprite SkySpriteOf(Sky sky)
        {
            switch (sky)
            {
                case Sky.Home: return SkinSprite("sky-home");
                case Sky.Nest: return SkinSprite("sky-nest");
                case Sky.Battle: return SkinSprite("sky-battle");
                case Sky.Hatch: return SkinSprite("sky-hatch");
                case Sky.Breed: return SkinSprite("sky-breed");
                default: return SkinSprite("sky-box");
            }
        }

        // ── フォント ────────────────────────────────────
        private static Font _font;

        /// <summary>⭐ Mochiy Pop One（SIL OFL 1.1）。丸くて太いポップ体。
        ///
        /// ⚠️ ドットフォント（DotGothic16）から替えた。
        /// 器がカジュアルな丸角になったので、字だけドットだと様式が2つ同居する。
        /// モックが使っているのもこれ。
        ///
        /// ⭐ 太いので**白抜き**が効く（<see cref="Knockout"/>）。
        /// OFL なので APK に埋め込んで販売してよい。詳細は Assets/Resources/Fonts/NOTICE.md。
        ///
        /// ⚠️ OS のフォントを借りない。Editor では出ても Android では出ない。</summary>
        public static Font TheFont
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.Load<Font>("Fonts/MochiyPopOne-Regular");
                    if (_font == null)
                    {
                        // ⚠️ 黙って別の字で描かない。無いことに気づけないほうが困る
                        Debug.LogError("Mochiy Pop One が読めない。Assets/Resources/Fonts/ にあるか確認する");
                        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    }
                }
                return _font;
            }
        }

        /// <summary>白抜き。⭐ 白い字に濃紺の縁を付ける。
        ///
        /// 絵や色の上に置く字はこれにする。地の色が何であっても読めるので、
        /// 「背景ごとに字の色を選び直す」が要らなくなる。
        /// ⚠️ 白い器の上では使わない（縁だけが見えて濁る）。そこは濃紺の字。</summary>
        public static Text Knockout(Text text, int thickness = 4)
        {
            text.color = Color.white;
            // ⚠️ 足す前に探す。Prefab で既に付いている字へ実行時にもう1枚足すと、
            //    縁が二重になって太る（毎回 Bind で呼ぶので、放置すると増え続ける）
            var outline = text.GetComponent<Outline>();
            if (outline == null) outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(0x2b, 0x33, 0x50, 0xff);
            outline.effectDistance = new Vector2(thickness, thickness);
            outline.useGraphicAlpha = false;
            return text;
        }

        // ── Kenney の意匠（CC0） ────────────────────────
        // ⚠️ 素材そのものは Assets/Resources/UI/。出所は同フォルダの NOTICE.md。
        // ⭐ 色を掛けない。ドット絵に色を掛けると、作者が組んだ配色が濁る。
        //    使い分けは「どの絵を貼るか」で行う。

        private static readonly Dictionary<string, Sprite> Skin = new Dictionary<string, Sprite>();

        /// <summary>意匠の絵を名前で引く。⭐ View 側が絵を差し替えるときに使う。</summary>
        public static Sprite SkinSprite(string name) => SkinOf(name);

        private static Sprite SkinOf(string name)
        {
            Sprite sprite;
            if (Skin.TryGetValue(name, out sprite)) return sprite;
            sprite = Resources.Load<Sprite>("UI/" + name);
            if (sprite == null)
            {
                // ⚠️ 黙って無地に落とさない。無いことに気づけないほうが困る
                Debug.LogError($"UI の絵が無い: Resources/UI/{name}");
            }
            Skin[name] = sprite;
            return sprite;
        }

        /// <summary>9スライスで貼る。⚠️ 引き伸ばすのは中央だけで、枠の太さは変わらない。</summary>
        private static Image Sliced(GameObject go, string skin)
        {
            var image = go.AddComponent<Image>();
            image.sprite = SkinOf(skin);
            image.type = Image.Type.Sliced;
            // ⚠️ ここを 0.25（4倍）にしたら枠が 75 単位になり、112 の押しどころが枠だけになった。
            //    描かれる枠 = 6px × (100 / 32) ÷ この値。1.0 で約 19 単位。
            image.pixelsPerUnitMultiplier = 1f;
            return image;
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

        /// <summary>ただの面。⚠️ 帯・印・地など「枠を持たないもの」だけに使う。
        /// 中身を持つ器は <see cref="Card"/>。</summary>
        public static RectTransform Block(Transform parent, string name, Color color,
            float left, float top, float width, float height)
        {
            var rect = Rect(name, parent);
            Place(rect, left, top, width, height);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        /// <summary>中身を入れる器。⭐ 枠のある札として置く。
        ///
        /// ⚠️ 器はこれ1種類。⭐ **明るい札を「選択中」に使わない。**
        /// 明るい鋼色の上では字の明暗が逆になり、行ごとに読み方が変わってしまう
        /// （実際、光らせた行だけ字が飛んだ）。目立たせるのは差し色の一辺で足りる。
        /// <paramref name="highlighted"/> は呼ぶ側の意図を残すためだけに受ける。</summary>
        public static RectTransform Card(Transform parent, string name,
            float left, float top, float width, float height, bool highlighted = false)
        {
            var rect = Rect(name, parent);
            Place(rect, left, top, width, height);
            Sliced(rect.gameObject, "panel");
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

        /// <summary>その幅で折り返したときに要る高さ。
        ///
        /// ⭐ **器の高さを決め打ちにしないため。**⚠️ 決め打ちにすると、
        /// 語が1つ増えた日に字が枠から出る（覆いは中身の量が技ごとに違う）。
        ///
        /// ⚠️ 仮の部品を作って測って捨てる。<c>Text.preferredHeight</c> は
        /// **rect の幅が入っていないと折り返しを勘定しない**ので、
        /// 幅を入れてから読む（入れないと1行ぶんしか返らない）。</summary>
        /// <summary>桁区切り。⭐ **大きい数を出す所は全部ここを通す。**
        ///
        /// ⚠️ ステの桁を上げた（2026-08-19）ので、HP は6桁まで伸びる。
        /// 目で桁を数えさせないために区切る。
        /// ⚠️ 端末の地域設定で区切り文字が変わらないよう、文化を固定する。</summary>
        public static string Digits(int value) =>
            value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        public static float Height(string content, int size, float width)
        {
            var go = new GameObject("Measure", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, 10f);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = TheFont;
            text.fontSize = size;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            float height = text.preferredHeight;
            // ⚠️ Destroy はフレームの終わりまで効かない。⭐ 測ったら即その場で消す
            UnityEngine.Object.DestroyImmediate(go);
            return height;
        }

        /// <summary>選んでいることを示す**角丸の輪**。
        ///
        /// ⚠️ 四角い輪郭の絵は素材に無い（`Resources/UI/NOTICE.md`）。
        /// 4本の帯で組むと角が直角になり、丸い札の上で浮く（作者の指摘 2026-08-19）。
        /// ⭐ **角丸の札を2枚重ねる** ── 外を差し色、内を白にすると輪になる。
        ///
        /// ⚠️ 中身より**後ろ**に置くこと（前に出すと絵を隠す）。呼ぶ側で並び順を決める。</summary>
        public static RectTransform Ring(Transform parent, string name,
            float left, float top, float width, float height, float thick = 8f)
        {
            var ring = Rect(name, parent);
            Place(ring, left, top, width, height);
            var outer = ring.gameObject.AddComponent<Image>();
            outer.sprite = SkinOf("panel");
            outer.type = Image.Type.Sliced;
            outer.color = Accent;
            outer.raycastTarget = false;

            var inner = Rect("Inner", ring);
            Place(inner, thick, thick, width - thick * 2f, height - thick * 2f);
            var face = inner.gameObject.AddComponent<Image>();
            face.sprite = SkinOf("panel");
            face.type = Image.Type.Sliced;
            face.color = Color.white;
            face.raycastTarget = false;
            return ring;
        }

        /// <summary>卵1個の升。⭐ **どの画面でも同じ形**（絵・★・一言）。
        ///
        /// ⚠️ 揃える前は3画面で全部違った ── 合成のたまごは**卵の絵が無く**、
        /// 技を鍛えるはシアンの押しどころ、卵を選ぶだけが絵つきだった
        /// （レビュー指摘 2026-08-19）。
        ///
        /// ⚠️ 押しどころは呼ぶ側が足す（押せない場面があるので、ここでは決めない）。</summary>
        public static RectTransform EggCell(Transform parent, string name, Egg egg,
            string note, Color noteInk, float left, float top, float width, float height,
            bool dim = false)
        {
            var cell = Rect(name, parent);
            Place(cell, left, top, width, height);
            var plate = cell.gameObject.AddComponent<Image>();
            plate.sprite = SkinOf("panel");
            plate.type = Image.Type.Sliced;
            plate.color = dim ? new Color(1f, 1f, 1f, 0.55f) : Color.white;

            float art = Mathf.Min(width - 48f, height - 96f);
            Pixel(cell, "Art", EggArt.Sprite, EggArt.Shell,
                (width - art) / 2f, 14f, art);
            Label(cell, "Stars", Rarities.StarsOf(egg.Rarity), 24,
                dim ? InkFaint : AccentInk, TextAnchor.MiddleCenter,
                0f, height - 76f, width, 32f);
            Label(cell, "Note", note, 26, dim ? InkFaint : noteInk,
                TextAnchor.MiddleCenter, 0f, height - 44f, width, 36f);
            return cell;
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

            // ⭐ 主導線は黄、通常は青、押せないものは灰。⚠️ 色を掛けず、絵を差し替える
            var image = Sliced(rect.gameObject, !enabled ? "button-off" : lead ? "button-lead" : "button");

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = enabled;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            // ⚠️ 明るい札の上に明るい字を置かない。札ごとに読める側を選ぶ
            var text = Label(rect, "Label", label, 34,
                !enabled ? InkFaint : lead ? OnLead : Ink,
                TextAnchor.MiddleCenter, 0f, 0f, width, height);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            return button;
        }

        /// <summary>見えない押しどころ。⭐ 札そのものを押させたいときに重ねる。
        /// ⚠️ <see cref="Tappable"/> を使うと木の札が描かれてしまい、
        /// 中身の上に茶色い面が乗る（実際グリッドでそうなった）。</summary>
        /// <summary>円。⭐ モックのアバターはすべて円。⚠️ 9スライスしない（伸ばすと歪む）。
        /// <paramref name="outline"/> を立てると、地ではなく縁だけを描く。</summary>
        public static RectTransform Round(Transform parent, string name,
            float left, float top, float size, Color color, bool outline = false)
        {
            var rect = Rect(name, parent);
            Place(rect, left, top, size, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = SkinOf(outline ? "circle-outline" : "circle");
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        public static Button HitArea(Transform parent, string name, Action onClick,
            float left, float top, float width, float height)
        {
            var rect = Rect(name, parent);
            Place(rect, left, top, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        /// <summary>押しどころの字を小さくする。⚠️ 語が枠から出るときだけ使う。</summary>
        public static void Shrink(Button button, int size)
        {
            var label = button.transform.Find("Label");
            if (label != null) label.GetComponent<Text>().fontSize = size;
        }

        /// <summary>9スライスの面を1枚置く。⭐ **素材の器をそのまま使うための入口。**
        ///
        /// ⚠️ 素の <see cref="Image"/> に色を塗ると、角が立った「手で描いた四角」になる。
        /// ⭐ ここを通せば、丸角と影の付いた素材（Hyper Casual UI / CC0）の面になる。</summary>
        public static RectTransform Plate(Transform parent, string name, string skin, Color tint,
            float left, float top, float width, float height)
        {
            var rect = Rect(name, parent);
            Place(rect, left, top, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = SkinSprite(skin);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = tint;
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>絵の札を1つ置く（`Resources/UI/icon/*`）。
        ///
        /// ⭐ **字の代わりに絵を置くための唯一の入口。**素材（Kenney Board Game Icons / CC0）は
        /// **白の抜き**なので、色を掛けて出す ── 明るい器の上では濃く、暗い器の上では白く。
        ///
        /// ⚠️ 同じものには**必ず同じ絵**を使う。上の帯の「攻撃」と盤の「壁」が同じ剣なら、
        /// 字で結び付けを説明する必要が無くなる。違う絵にした瞬間、説明が要る。</summary>
        /// <param name="turn">回す角度。⭐ 上向きの矢印は「右向き」を 90 度回して作る
        /// （⚠️ 上下の矢印の素材には字が入っていて、字を消す方針に反する）。</param>
        public static Image Icon(Transform parent, string name, string icon, Color tint,
            float left, float top, float size, float turn = 0f)
        {
            var rect = Rect(name, parent);
            Place(rect, left, top, size, size);
            if (turn != 0f) rect.localRotation = Quaternion.Euler(0f, 0f, turn);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = SkinSprite("icon/" + icon);
            image.color = tint;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
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
            // ⚠️ 下地を黒にしない。地が明るくなったので、黒い帯だけが浮いて見える
            Block(parent, name + " Track", new Color32(0xff, 0xff, 0xff, 0xcc), left, top, width, height);
            float filled = Mathf.Clamp01(ratio) * width;
            var rect = Rect(name, parent);
            Place(rect, left, top, Mathf.Max(0f, filled), height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>キャラの足元に置く短いゲージ。⭐ 左に丸い数字、右に帯。
        ///
        /// 実際の対戦ゲームの並び（参考のスクショ）に合わせた形。
        /// ⚠️ 列幅いっぱいの帯にしない。**誰の量なのか**が離れると読めなくなる。</summary>
        public static void GaugePill(Transform parent, string name, string badge, float ratio,
            Color fill, float left, float top, float width, float height = 46f)
        {
            var rect = Rect(name, parent);
            Place(rect, left, top, width, height);

            // 地。⚠️ ピルの絵を敷いて丸みを出す（角丸を自分で描かない）
            var track = rect.gameObject.AddComponent<Image>();
            track.sprite = SkinOf("pill");
            track.type = Image.Type.Sliced;
            track.color = Color.white;
            track.raycastTarget = false;

            // 帯は丸い数字の右から
            float badgeSize = height;
            float barLeft = badgeSize + 6f;
            float barWidth = width - barLeft - 10f;
            Block(rect, "Fill", fill, barLeft, 9f, Mathf.Clamp01(ratio) * barWidth, height - 18f);

            Round(rect, "Badge", 0f, 0f, badgeSize, fill);
            // ⚠️ 丸の幅で折り返させない。3桁が「10/5」に割れて読めなくなる。
            //    桁数で字を縮め、はみ出しは許して中央に置く
            int size = badge.Length >= 4 ? 16 : badge.Length == 3 ? 19 : 23;
            var num = Label(rect, "Num", badge, size, Ink, TextAnchor.MiddleCenter, 0f, 0f, badgeSize, height);
            num.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        /// <summary>ボタンの中に置く小さなピル（CT など）。⭐ 参考画面の `CT 6` の形。</summary>
        public static void MiniPill(Transform parent, string name, string text,
            float left, float top, float width, float height = 40f)
        {
            var rect = Rect(name, parent);
            Place(rect, left, top, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = SkinOf("pill");
            image.type = Image.Type.Sliced;
            image.color = new Color32(0x2b, 0x33, 0x50, 0xff);
            image.raycastTarget = false;
            var label = Label(rect, "T", text, 22, Color.white, TextAnchor.MiddleCenter, 0f, 0f, width, height);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
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
