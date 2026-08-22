using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>骨組み（<see cref="Layout"/>）に値を差すための口。
    ///
    /// ⭐ **画面がするのはここを埋めることだけ。**⚠️ 座標には触れません。
    ///
    /// ⭐ 繰り返し（`repeat=`）は <see cref="At"/> で「いま何番目か」が先に届くので、
    /// <see cref="Text"/> などの中でそれを読めば済みます
    /// ── 口を「1件用」と「繰り返し用」に割らずに済みます。</summary>
    public sealed class LayoutFill
    {
        /// <summary>`bind=` → 出す字。⚠️ null を返したら空。</summary>
        public Func<string, string> Text;
        /// <summary>`bind=` → 出すドット絵。</summary>
        public Func<string, PixelSprite> Sprite;
        /// <summary>`bind=` → その絵の色。</summary>
        public Func<string, Palette> Palette;
        /// <summary>`bind=` → 字や絵に掛ける色。⚠️ null なら骨組みの `ink=` のまま。</summary>
        public Func<string, Color?> Tint;
        /// <summary>`tap=` → 押したときの手。⚠️ null を返したら押しどころを作らない。</summary>
        public Func<string, Action> Tap;
        /// <summary>`tap=` → 長押ししたときの手。</summary>
        public Func<string, Action> Hold;
        /// <summary>`repeat=` → 何個あるか。</summary>
        public Func<string, int> Count;
        /// <summary>⭐ 繰り返しの1件を組む直前に呼ばれる（どの繰り返しの、何番目か）。
        ///
        /// ⚠️ **入れ子があるので、どの繰り返しかを渡す。**⭐ 番号1本だと、
        /// 内側の繰り返しが外側の番号を潰す（段5枚 × 顔4体 が組めない）。</summary>
        public Action<string, int> At;
        /// <summary>`when=` → 出すか。⚠️ null なら常に出す。</summary>
        public Func<string, bool> When;
    }

    /// <summary>骨組みを実際の部品に変える。⭐ **ここが唯一「座標を読む」場所。**
    ///
    /// ⚠️ このファイルは Unity に触りますが、**中身の判断はしません**
    /// （何をどこに置くかは骨組みが持つ）。⭐ だから後日エンジンを替えるときに
    /// 差し替えるのはここだけで済みます。
    ///
    /// ⚠️ **画面から `Ui.Place` を呼ばない**というのが、この作りの約束です
    /// （2026-08-22・作者の指示「すべてアセットを使用することを厳格に守れば」）。</summary>
    public static class LayoutView
    {
        private static readonly Dictionary<string, Layout> Cache = new Dictionary<string, Layout>();

        /// <summary>骨組みを読む。⭐ 一度読んだら覚える（毎回の組み直しで読み直さない）。</summary>
        public static Layout Of(string id)
        {
            Layout found;
            if (Cache.TryGetValue(id, out found)) return found;

            var asset = Resources.Load<TextAsset>("Layouts/" + id);
            if (asset == null)
            {
                // ⚠️ 黙って空を返さない。⭐ 無いことに気づけないほうが困る
                Debug.LogError($"骨組みが読めない: Assets/Resources/Layouts/{id}.txt");
                return null;
            }
            // ⭐ `use=` を先に差し替える。⚠️ 検査も描画も、差し替えたあとの木を見る
            found = Layouts.Resolve(Layouts.Parse(id, asset.text), Of);
            // ⚠️ **読んだ場で検査する。**⭐ テストでも同じものを見ているが、
            //    ここで見ておくと「アセットだけ直してテストを回し忘れた」を拾える。
            var problems = Layouts.Faults(found);
            foreach (var line in problems) Debug.LogError("骨組み: " + line);

            Cache[id] = found;
            return found;
        }

        /// <summary>骨組みのとおりに組む。</summary>
        public static void Build(string id, RectTransform parent, LayoutFill fill)
        {
            var layout = Of(id);
            if (layout == null) return;
            foreach (var node in layout.Roots) One(node, parent, fill, node.Top);
        }

        /// <summary>子を組む。<paramref name="top"/> は**詰めた結果の上端**
        /// （`Layouts.TopsOf` が出す。⚠️ ここで数え直さない）。</summary>
        private static void One(LayoutNode node, RectTransform parent, LayoutFill fill, float top)
        {
            // ⭐ **条件で出さない。**⚠️ 隠すのでなく作らない
            //    （作って隠すと、検査が「在るのに見えない」と数えることになる）
            if (!Shows(node, fill)) return;

            // ⭐ 繰り返しは、その札を人数ぶん複製する
            string repeat = node.Option("repeat");
            if (repeat != null) { Many(node, parent, fill, repeat, top); return; }
            Single(node, parent, fill, node.Left, top);
        }

        private static void Many(LayoutNode node, RectTransform parent, LayoutFill fill,
            string repeat, float top)
        {
            int count = fill != null && fill.Count != null ? fill.Count(repeat) : 0;
            int cols = node.Number("cols", 1);
            if (cols < 1) cols = 1;
            float gap = node.Number("gap", 0);
            // ⭐ 段の高さは `Layouts.StepOf` が唯一の出所（ここで数え直さない）
            float step = Layouts.StepOf(node);

            for (int i = 0; i < count; i++)
            {
                if (fill != null && fill.At != null) fill.At(repeat, i);
                float left = node.Left + (i % cols) * (node.Width + gap);
                Single(node, parent, fill, left, top + (i / cols) * step, i);
            }
        }

        private static void Single(LayoutNode node, RectTransform parent, LayoutFill fill,
            float left, float top, int index = -1)
        {
            // ⚠️ **空白で繋がない。**⭐ 名前に空白が入ると、書き戻したものを
            //    読み直せない（`Split(' ')` が名前を割る）── 往復が閉じなくなる。
            string name = index < 0 ? node.Name : node.Name + "#" + index;
            RectTransform rect;

            switch (node.Kind)
            {
                case "card":
                    rect = Ui.Card(parent, name, left, top, node.Width, node.Height);
                    break;

                case "scroll":
                    // ⭐ 中身の高さは**並ぶ数**で決まるので、ここで数える
                    rect = Ui.Scroller(parent, name, left, top, node.Width, node.Height,
                        ContentOf(node, fill));
                    break;

                case "label":
                {
                    var text = Ui.Label(parent, name, TextOf(node, fill),
                        node.Number("size", 26), InkOf(node, fill),
                        AnchorOf(node.Option("anchor")),
                        left, top, node.Width, node.Height);
                    rect = (RectTransform)text.transform;
                    break;
                }

                case "pixel":
                {
                    // ⚠️ **差し口が無ければ一度も呼ばない**（web 版と同じ約束）
                    string art = node.Option("bind");
                    var sprite = art != null && fill != null && fill.Sprite != null
                        ? fill.Sprite(art) : null;
                    var palette = art != null && fill != null && fill.Palette != null
                        ? fill.Palette(art) : null;
                    if (sprite == null || palette == null)
                    {
                        rect = Ui.Rect(name, parent);
                        Ui.Place(rect, left, top, node.Width, node.Height);
                        break;
                    }
                    var image = Ui.Pixel(parent, name, sprite, palette,
                        left, top, Mathf.Min(node.Width, node.Height));
                    var tint = art != null && fill.Tint != null ? fill.Tint(art) : null;
                    if (tint.HasValue) image.color = tint.Value;
                    rect = (RectTransform)image.transform;
                    break;
                }

                case "button":
                {
                    // ⚠️ **字の大きさを渡す。**⭐ 渡さないと `Ui.Tappable` の既定 34 に
                    //    固定され、骨組みに書いた `size=` が黙って効かない。
                    var button = Ui.Tappable(parent, name, TextOf(node, fill),
                        HandOf(node, fill), left, top, node.Width, node.Height,
                        lead: node.Option("lead") == "yes", size: node.Number("size", 34));
                    rect = (RectTransform)button.transform;
                    break;
                }

                case "veil":
                {
                    // ⭐ 地を暗くし、指を吸う。⚠️ ここを押しても閉じない
                    var dim = Ui.Rect(name, parent);
                    Ui.Place(dim, left, top, node.Width, node.Height);
                    var paint = dim.gameObject.AddComponent<UnityEngine.UI.Image>();
                    paint.color = new Color(0f, 0f, 0f, 0.62f);
                    var block = dim.gameObject.AddComponent<Button>();
                    block.transition = Selectable.Transition.None;
                    block.targetGraphic = paint;
                    rect = dim;
                    break;
                }

                case "round":
                    rect = Ui.Round(parent, name, left, top,
                        Mathf.Min(node.Width, node.Height), InkOf(node, fill));
                    break;

                case "line":
                {
                    // ⭐ **区切りの1本。**⚠️ 色は薄墨（`ink=faint` でさらに薄く）。
                    //    表の見出しの下だけ濃く、行の間は消え入るくらいにする。
                    var bar = Ui.Rect(name, parent);
                    Ui.Place(bar, left, top, node.Width, node.Height);
                    var paint = bar.gameObject.AddComponent<UnityEngine.UI.Image>();
                    paint.raycastTarget = false;
                    paint.color = new Color32(0x2e, 0x26, 0x1c,
                        node.Option("ink") == "faint" ? (byte)0x24 : (byte)0x57);
                    rect = bar;
                    break;
                }

                default:
                    // ⚠️ 「box」と、知らない種類。⭐ 入れ物としてだけ作る
                    rect = Ui.Rect(name, parent);
                    Ui.Place(rect, left, top, node.Width, node.Height);
                    break;
            }

            // ⭐ 札そのものを押しどころにする（`tap=` があるとき）。
            // ⚠️ 中に釦を置かない ── どこを押すのか読めなくなる。
            string tap = node.Option("tap");
            if (tap != null && node.Kind != "button" && fill != null)
            {
                var hand = fill.Tap != null ? fill.Tap(tap) : null;
                var held = fill.Hold != null ? fill.Hold(tap) : null;
                if (hand != null || held != null) Touchable(rect, hand, held);
            }

            // ⭐ 詰める親なら、ここで子の上端を出す（`TopsOf` が唯一の出所）
            var tops = Layouts.TopsOf(node,
                child => Shows(child, fill),
                child => fill != null && fill.Count != null
                    ? fill.Count(child.Option("repeat")) : 0);
            for (int i = 0; i < node.Children.Count; i++)
                One(node.Children[i], rect, fill, tops[i]);
        }

        /// <summary>巻物の中身の高さ。⭐ **並ぶ数から出す**（骨組みには書かない ──
        /// 書くとデータと二重になり、必ずずれる）。</summary>
        private static float ContentOf(LayoutNode node, LayoutFill fill)
        {
            float deepest = node.Height;
            foreach (var child in node.Children)
            {
                string repeat = child.Option("repeat");
                if (repeat == null)
                {
                    deepest = Mathf.Max(deepest, child.Top + child.Height);
                    continue;
                }
                int count = fill != null && fill.Count != null ? fill.Count(repeat) : 0;
                int cols = Mathf.Max(1, child.Number("cols", 1));
                float step = Layouts.StepOf(child);
                int rows = Mathf.CeilToInt(count / (float)cols);
                deepest = Mathf.Max(deepest, child.Top + rows * step);
            }
            return deepest;
        }

        private static void Touchable(RectTransform rect, Action tap, Action hold)
        {
            if (rect == null) return;
            if (hold == null)
            {
                var button = rect.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                if (tap != null) button.onClick.AddListener(() => tap());
                return;
            }
            // ⚠️ 長押しが要るときは Button を使わない ── 指を離した拍子に押される
            var press = rect.gameObject.AddComponent<LongPress>();
            press.OnTap = tap;
            press.OnHold = hold;
        }

        /// <summary>その部品を出すか。⚠️ `when=` が無ければ常に出す。</summary>
        private static bool Shows(LayoutNode node, LayoutFill fill)
        {
            string key = Layouts.WhenOf(node);
            if (key == null) return true;
            bool yes = fill != null && fill.When != null && fill.When(key);
            return Layouts.WhenNot(node) ? !yes : yes;
        }

        /// <summary>出す字。⭐ **動かない字は骨組み（`text=`）から直に。**
        /// ⚠️ `bind=` と併記されていたら <see cref="Layouts.Faults"/> が落とすので、
        /// ここで順序を決める必要は無い。</summary>
        private static string TextOf(LayoutNode node, LayoutFill fill)
        {
            string literal = node.Option("text");
            if (literal != null) return literal;
            string bind = node.Option("bind");
            if (bind == null || fill == null || fill.Text == null) return "";
            return fill.Text(bind) ?? "";
        }

        private static Action HandOf(LayoutNode node, LayoutFill fill)
        {
            string tap = node.Option("tap") ?? node.Name;
            if (fill == null || fill.Tap == null) return null;
            return fill.Tap(tap);
        }

        private static Color InkOf(LayoutNode node, LayoutFill fill)
        {
            string bind = node.Option("bind");
            if (bind != null && fill != null && fill.Tint != null)
            {
                var chosen = fill.Tint(bind);
                if (chosen.HasValue) return chosen.Value;
            }
            switch (node.Option("ink"))
            {
                case "dim": return Ui.InkDim;
                case "faint": return Ui.InkFaint;
                case "accent": return Ui.AccentInk;
                case "danger": return Ui.DangerInk;
                case "good": return Ui.GoodInk;
                case "on-lead": return Ui.OnLead;
                case null: return Ui.Ink;
                default:
                    // ⚠️ 知らない色名を黙って既定にしない ── 綴り違いが通ってしまう
                    Debug.LogError($"骨組み: 知らない ink=「{node.Option("ink")}」（{node.Name}）");
                    return Ui.Ink;
            }
        }

        private static TextAnchor AnchorOf(string name)
        {
            switch (name)
            {
                case "left": return TextAnchor.MiddleLeft;
                case "right": return TextAnchor.MiddleRight;
                case "center": return TextAnchor.MiddleCenter;
                case "upper-left": return TextAnchor.UpperLeft;
                case "upper-center": return TextAnchor.UpperCenter;
                case null: return TextAnchor.MiddleLeft;
                default:
                    Debug.LogError($"骨組み: 知らない anchor=「{name}」");
                    return TextAnchor.MiddleLeft;
            }
        }
    }
}
