using System;
using System.Collections.Generic;
using System.Text;
using EggCommand.Core;

namespace EggCommand.Web
{
    /// <summary>骨組みに値を差すための口。
    ///
    /// ⚠️ **Unity 版の `LayoutFill` と同じ形にしてある。**⭐ 画面を書く側のコードが
    /// そのまま生きるようにするため（`BookScreen` は `Ui` を1度も呼んでいない）。
    ///
    /// ⚠️ 違うのは色の型だけ ── Unity は `Color`、ここは CSS の名前。</summary>
    public sealed class DomFill
    {
        /// <summary>`bind=` → 出す字。</summary>
        public Func<string, string> Text;
        /// <summary>`bind=` → 出すドット絵。</summary>
        public Func<string, PixelSprite> Sprite;
        /// <summary>`bind=` → その絵の色。</summary>
        public Func<string, Palette> Palette;
        /// <summary>`bind=` → 掛ける色（CSS）。⚠️ null なら骨組みの `ink=` のまま。</summary>
        public Func<string, string> Tint;
        /// <summary>`bind=` → 薄くするか（0〜1）。⚠️ 伏せてあるものを沈めるのに使う。</summary>
        public Func<string, double?> Fade;
        /// <summary>`bind=` → 主役に立てるか。⭐ **入切の札のため**
        /// （Unity 版 `Ui.Tappable(..., lead: 入っているか)` と同じ役）。
        /// ⚠️ 骨組みの `lead=yes` は消せない ── **足すだけ**。</summary>
        public Func<string, bool> Lead;
        /// <summary>`tap=` → 押しどころにするか。</summary>
        public Func<string, bool> Tappable;
        /// <summary>`repeat=` → 何個あるか。</summary>
        public Func<string, int> Count;
        /// <summary>⭐ 繰り返しの1件を組む直前に呼ばれる（どの繰り返しの、何番目か）。
        /// ⚠️ 入れ子があるので、どの繰り返しかを渡す。</summary>
        public Action<string, int> At;
        /// <summary>`when=` → 出すか。⚠️ null なら常に出す。</summary>
        public Func<string, bool> When;
        /// <summary>`bar` の伸び具（0〜1）。⚠️ null なら 0。</summary>
        public Func<string, double> Ratio;
        /// <summary>⭐ `icon` の絵の名前。⚠️ null なら骨組みの `pic=` のまま。</summary>
        public Func<string, string> Pic;
        /// <summary>⭐ **`host` の中身**（名前 → そのまま差す HTML）。
        ///
        /// ⚠️ 骨組みが知らないと宣言した枠なので、描く側が全部決める。
        /// ⭐ Unity 版の `LayoutFill.Mount`（器を渡す）に当たる。</summary>
        public Func<string, string> Inside;
    }

    /// <summary>骨組みを HTML に変える。⭐ **ここが唯一「座標を読む」場所。**
    ///
    /// ⚠️ Unity 版（`LayoutView.cs`）と1対1で対応する。⭐ 差し替えたのはここだけで、
    /// 骨組みファイル（`Layouts/*.txt`）は**1文字も変えていない**。
    ///
    /// ⚠️ **中身の判断はしない。**何をどこに置くかは骨組みが持つ。</summary>
    public static class LayoutDom
    {
        /// <param name="suffix">⭐ **同じ骨組みを何枚も出すときの番号**（`"#2"` など）。
        ///
        /// ⚠️ 繰り返し（`repeat=`）は自分で番号を付けるが、
        /// **描く側が何度も呼ぶ**場合（戦闘の立ち位置）はここで渡す。
        /// ⭐ 渡さないと id が重なる（実測 2026-08-22: 5体分が全部同じ id）。</param>
        /// <param name="crown">⭐ **1枚の画面に骨組みを重ねるときの冠**（`"-card"` など）。
        ///
        /// ⚠️ `suffix` とは**別のもの**。⭐ 冠は id にだけ付き、
        /// 押しどころの番号（`data-at`）には入らない ── 入れると番号が読めなくなる
        /// （実測 2026-08-22: `-card#0` を番号として読もうとして -1 になった）。
        /// ⚠️ `use=` の冠（`Layouts.Rename`）と同じ役目を、描くときにする。</param>
        public static string Render(EggCommand.Core.Layout layout, DomFill fill,
            string suffix = "", string crown = "")
        {
            var sb = new StringBuilder();
            if (layout == null) return "<!-- 骨組みが無い -->";
            foreach (var node in layout.Roots) One(sb, node, fill, node.Top, suffix, crown);
            return sb.ToString();
        }

        /// <param name="suffix">繰り返しの中なら「#N」。⚠️ **子まで伝える。**
        /// ⭐ DOM の id は一意でなければならない ── 伝えないと、11枚の札の子が
        /// 全部同じ id になり、検査も指し示しも効かなくなる（2026-08-22 に実測）。</param>
        private static void One(StringBuilder sb, LayoutNode node, DomFill fill,
            float top, string suffix = "", string crown = "")
        {
            // ⭐ **条件で出さない。**⚠️ 隠すのでなく作らない
            if (!Shows(node, fill)) return;

            string repeat = node.Option("repeat");
            if (repeat == null) { Single(sb, node, fill, node.Left, top, -1, suffix, crown); return; }

            int count = fill?.Count != null ? fill.Count(repeat) : 0;
            int cols = Math.Max(1, node.Number("cols", 1));
            float gap = node.Number("gap", 0);
            // ⭐ 段の高さは `Layouts.StepOf` が唯一の出所（Unity 版と同じ）
            float step = Layouts.StepOf(node);

            for (int i = 0; i < count; i++)
            {
                fill?.At?.Invoke(repeat, i);
                float left = node.Left + (i % cols) * (node.Width + gap);
                // ⭐ 外側の番号も引き継ぐ（入れ子で id が衝突する）
                Single(sb, node, fill, left, top + (i / cols) * step, i, suffix, crown);
            }
        }

        private static void Single(StringBuilder sb, LayoutNode node, DomFill fill,
            float left, float top, int index, string suffix, string crown)
        {
            // ⚠️ 空白で繋がない（Unity 版と同じ理由 ── 読み戻せなくなる）
            // ⚠️ **外側の番号を捨てない。**⭐ 入れ子の繰り返しでは
            //    `card#2` の中の `face#0` が5枚できて id が衝突する（実測 2026-08-22）。
            string mine = index < 0 ? suffix : suffix + "#" + index;
            string name = node.Name + crown + mine;
            string tag = node.Kind == "button" ? "button" : "div";

            var style = new StringBuilder();
            style.Append("left:").Append(Px(left))
                 .Append(";top:").Append(Px(top))
                 .Append(";width:").Append(Px(node.Width))
                 .Append(";height:").Append(Px(node.Height));

            var cls = new StringBuilder("n ").Append(node.Kind);
            // ⚠️ **差し口が無ければ一度も呼ばない。**⭐ 値を差す側は
            //    「自分が書いた名前」だけを受け取ればよい（null を見張らせない）。
            string bind = node.Option("bind");
            bool has = bind != null;

            // ⚠️ **釦も字を出す。**⭐ ここを札だけにしていたので、釦の字がすべて
            //    ブラウザの既定（16px）で出ていた（実測 2026-08-22）。
            //    ⚠️ 既定は Unity の `Ui.Tappable` に合わせて 34。
            if (node.Kind == "label" || node.Kind == "button")
                style.Append(";font-size:")
                     .Append(Px(node.Number("size", node.Kind == "button" ? 34 : 26)));

            if (node.Kind == "label")
            {
                cls.Append(" a-").Append(node.Option("anchor") ?? "left");
                // ⭐ 折り返す字（説明文）。⚠️ 既定は折り返さない ── 1行の見出しが
                //    枠の都合で勝手に2行になると、上下の間隔が崩れる。
                if (node.Option("wrap") == "yes") cls.Append(" wrapped");
                string ink = node.Option("ink");
                if (ink != null) cls.Append(" ink-").Append(ink);
                string tint = has && fill?.Tint != null ? fill.Tint(bind) : null;
                if (tint != null) style.Append(";color:").Append(tint);
            }
            else
            {
                // ⭐ 字でないものは、色を**地**に掛ける（丸＝属性の色・線＝薄墨）。
                // ⚠️ Unity 版は `InkOf` が同じ役をしている（`Ui.Round` の色）。
                // ⚠️ **帯だけは別**── 色が付くのは「伸びた分」であって、地ではない。
                string ink = node.Option("ink");
                if (ink != null) cls.Append(" ink-").Append(ink);
                string tint = has && node.Kind != "bar" && fill?.Tint != null ? fill.Tint(bind) : null;
                // ⚠️ **絵の印だけは `color`**── 地ではなく、抱き合わせを染める側
                if (tint != null) style.Append(node.Kind == "icon" ? ";color:" : ";background:").Append(tint);
                // ⭐ 絵を回す（矢印の ±90）。⚠️ 中心のまわり
                int turn = node.Number("turn", 0);
                if (turn != 0) style.Append(";transform:rotate(").Append(turn).Append("deg)");
            }
            if (node.Option("lead") == "yes"
                || (has && fill?.Lead != null && fill.Lead(bind))) cls.Append(" lead");

            double? fade = has && fill?.Fade != null ? fill.Fade(bind) : null;
            if (fade.HasValue) style.Append(";opacity:")
                .Append(fade.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));

            // ⚠️ 押しどころは `tap=` があるときだけ。⭐ 無ければただの札
            // ⚠️ **長押し（`hold=`）は押しどころではない。**
            //    ⭐ 設計が「押しても何も起きない、長押しで読む」と言い切っているので、
            //    指の大きさ（112）の規則を掛けると**偽の警報**になる。
            string hold = node.Option("hold");
            string tap = node.Option("tap");
            bool live = tap != null && fill?.Tappable != null && fill.Tappable(tap);
            if (tap != null && !live && node.Kind != "button") cls.Append(" quiet");

            sb.Append('<').Append(tag)
              .Append(" id=\"").Append(Esc(name)).Append('"')
              .Append(" class=\"").Append(cls).Append('"')
              .Append(" style=\"").Append(style).Append('"');
            // ⭐ **骨組みエディタの掴みどころ**（`data-line` ＝ 出所の行番号）。
            //
            // ⚠️ `use=` で差した部品の節点（`Layouts.Rename` を通ったもの）は
            //    `LineNumber` が -1 ── **その場合は出さない**（誤った行を選べるより、
            //    選べないほうがまし。別ファイルの行を、いま編集中の骨組みの行として
            //    書き戻すと事故る）。⭐ この骨組み自身の行から来た節点は、
            //    `Resolve` を通したあとも `LineNumber` を保っている（`Layouts.Splice`）。
            // ⭐ **繰り返し（`repeat=`）の複製は全部同じ節点 `node` を指すので、
            //    自動的に同じ `data-line` を持つ。**何番目の複製を押しても
            //    「1本の元の行」に行き着く ── `closest('[data-line]')` で終わる。
            if (node.LineNumber >= 0)
                sb.Append(" data-line=\"").Append(node.LineNumber).Append('"');
            // ⭐ **押しどころの名前と番号を、部品そのものに書いておく。**
            //
            // ⚠️ ここは字を組み立てて `MarkupString` で出しているので、Blazor の
            // `@onclick` は付けられない。⭐ 代わりに `#stage` で拾って、この2つを読む。
            // ⚠️ 番号は**繰り返しの連なり**（`2#1` のような入れ子もある）。
            if (live || hold != null)
            {
                if (live) sb.Append(" data-tap=\"").Append(Esc(tap)).Append('"');
                if (hold != null) sb.Append(" data-hold=\"").Append(Esc(hold)).Append('"');
                if (mine.Length > 0) sb.Append(" data-at=\"").Append(Esc(mine.Substring(1))).Append('"');
            }
            if (tag == "button" && !live && tap != null) sb.Append(" disabled");
            sb.Append('>');

            if (node.Kind == "icon")
            {
                // ⭐ **絵は抱き合わせ（mask）で出して、色は地で与える。**
                //    ⚠️ Unity は `Image.color` で染めているので、同じ振る舞いに合わせる。
                string pic = (has && fill?.Pic != null ? fill.Pic(bind) : null) ?? node.Option("pic");
                if (pic != null)
                {
                    // ⭐ 絵の場所は1回だけ言う（`--pic`）。⚠️ 素の絵と、
                    //    その形に切った色の2枚が同じ絵を見るので、二重に書かない。
                    sb.Append("<div class=\"n icon-art\" style=\"left:0;top:0;width:100%;height:100%;--pic:url(icon/")
                      .Append(Esc(pic)).Append(".png)\"></div>");
                }
            }
            else if (node.Kind == "host")
            {
                // ⭐ **中は骨組みが知らない。**⚠️ ここだけは描く側の字をそのまま流す
                sb.Append(fill?.Inside != null ? fill.Inside(node.Name) ?? "" : "");
            }
            else if (node.Kind == "bar")
            {
                // ⭐ **帯は「地」と「伸びた分」の2枚。**⚠️ 幅だけが割合で変わるので
                //    骨組みには書けない（だから種類にしてある）。
                double at = has && fill?.Ratio != null ? fill.Ratio(bind) : 0;
                if (at < 0) at = 0;
                if (at > 1) at = 1;
                string paint = has && fill?.Tint != null ? fill.Tint(bind) : null;
                sb.Append("<div class=\"n bar-fill\" style=\"left:0;top:0;height:100%;width:")
                  .Append((at * 100).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))
                  .Append('%');
                if (paint != null) sb.Append(";background:").Append(paint);
                sb.Append("\"></div>");
            }
            else if (node.Kind == "pixel")
            {
                var sprite = has && fill?.Sprite != null ? fill.Sprite(bind) : null;
                var palette = has && fill?.Palette != null ? fill.Palette(bind) : null;
                if (sprite != null && palette != null) Dots(sb, sprite, palette, node);
            }
            else if (node.Kind == "label" || node.Kind == "button")
            {
                // ⭐ **動かない字は骨組みから直に**（Unity 版 `TextOf` と同じ順）
                string literal = node.Option("text");
                sb.Append(Esc(literal ?? (has && fill?.Text != null ? fill.Text(bind) ?? "" : "")));
            }

            // ⭐ 子にも同じ番号を伝える（id を一意に保つ）
            // ⭐ 詰める親なら、ここで子の上端を出す（`TopsOf` が唯一の出所）
            var tops = Layouts.TopsOf(node, child => Shows(child, fill),
                child => fill?.Count != null ? fill.Count(child.Option("repeat")) : 0);
            for (int i = 0; i < node.Children.Count; i++)
                One(sb, node.Children[i], fill, tops[i], mine, crown);
            sb.Append("</").Append(tag).Append('>');
        }

        /// <summary>ドット絵を SVG で。⭐ **`EggCommand.Sim/Book.cs` と同じやり方**
        /// （添字色 → `Palette.ColorOf` の "#rrggbb" をそのまま矩形に）。
        ///
        /// ⚠️ 画像ファイルにしない ── 変異＝パレットスワップなので、
        /// **同じ絵に別の色を掛ける**のがこの作品の仕組み。</summary>
        private static void Dots(StringBuilder sb, PixelSprite sprite, Palette palette, LayoutNode node)
        {
            // ⭐ 正方形で描く（検査が「絵は正方形」を要求している）
            float size = Math.Min(node.Width, node.Height);
            sb.Append("<svg class=\"n pixel").Append(node.Option("foe") == "yes" ? " foe" : "")
              .Append("\" viewBox=\"0 0 ")
              .Append(sprite.Width).Append(' ').Append(sprite.Height)
              .Append("\" style=\"left:0;top:0;width:").Append(Px(size))
              .Append(";height:").Append(Px(size)).Append("\">");
            for (int y = 0; y < sprite.Height; y++)
            {
                for (int x = 0; x < sprite.Width; x++)
                {
                    byte at = sprite.At(x, y);
                    if (at == 0) continue;   // ⚠️ 添字0は透明
                    sb.Append("<rect x=\"").Append(x).Append("\" y=\"").Append(y)
                      .Append("\" width=\"1\" height=\"1\" fill=\"")
                      .Append(palette.ColorOf(at)).Append("\"/>");
                }
            }
            sb.Append("</svg>");
        }

        /// <summary>⭐ **設計 px を CSS へ。**⚠️ `--u` を掛けない ── 外枠を丸ごと
        /// 拡大縮小するので、中は設計の数のままでよい。</summary>
        /// <summary>その部品を出すか。⚠️ `when=` が無ければ常に出す。</summary>
        private static bool Shows(LayoutNode node, DomFill fill)
        {
            string key = Layouts.WhenOf(node);
            if (key == null) return true;
            bool yes = fill?.When != null && fill.When(key);
            return Layouts.WhenNot(node) ? !yes : yes;
        }

        private static string Px(float value) =>
            value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "px";

        private static string Esc(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}
