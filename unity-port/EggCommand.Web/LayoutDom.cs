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
            // ⭐ 自作の仮ドット絵など、補間せず出したい icon だけに効く
            //    （既存の Kenney 絵は滑らかな見た目のまま ── stage.css 参照）。
            if (node.Kind == "icon" && node.Option("crisp") == "yes") cls.Append(" crisp");

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
            // ⭐ **差し込まれた側は、代わりに出所（部品ファイル名・その中の行）を出す。**
            //
            // ⚠️ `data-line` は出さない（上と同じ理由 ── 別ファイルの行番号を
            //    いま編集中の骨組みの行として書き戻すと事故る）。⭐ その代わり、
            //    どの部品ファイルの何行目から来たかは言える ── これでエディタが
            //    「その部品ファイルへ切り替える」次の一手を作れる（今回はまだ作らない）。
            //    ⭐ こうして、画面のどの部品も `data-line` か
            //    `data-part`＋`data-part-line` の**どちらか一方**を必ず持つ。
            else if (node.PartId != null)
                sb.Append(" data-part=\"").Append(Esc(node.PartId)).Append('"')
                  .Append(" data-part-line=\"").Append(node.PartLine).Append('"');
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
                // ⭐ 段E: 骨組みエディタ専用の上書き（`IconOverrides`）を**先に**見る。
                //    ⚠️ 遊ぶ画面（`/app`）はこの表に一度も書き込まないので、`TryGet` は
                //    常に false ── 以下の既存2分岐（`IconManifest.Exists`→`icon-missing`／
                //    通常の `icon/<名前>.png`）は1バイトも変わらず、今までどおりのまま通る。
                if (pic != null && IconOverrides.TryGet(pic, out var overridePic))
                {
                    // ⭐ まだビルド（`CopyArt`/`IconManifest`）に入っていない絵。
                    //    絵そのものは在るので `icon-missing` にしない ── 出所はディスクから
                    //    読んだ data URL（骨組みエディタ側が登録した文字列をそのまま使う）。
                    sb.Append("<div class=\"n icon-art\" style=\"left:0;top:0;width:100%;height:100%;--pic:url(")
                      .Append(Esc(overridePic)).Append(")\"></div>");
                }
                else if (pic != null && !IconManifest.Exists(pic))
                {
                    // 🔴 **黙って空の四角にしない。**⚠️ 表や骨組みが指す名前でも、
                    //    実体（`Resources/UI/icon/<名前>.png`）が無ければここへ落ちる
                    //    ── 埋め込んだ一覧（`IconManifest`）と突き合わせて分かる。
                    sb.Append("<div class=\"n icon-missing\" style=\"left:0;top:0;width:100%;height:100%\" title=\"絵が無い: ")
                      .Append(Esc(pic)).Append(".png\">？</div>");
                }
                else if (pic != null)
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

        /// <summary>ドット絵を出す。⭐ **種族の絵は「あらかじめ差し替えた PNG」を `&lt;img&gt;` 1枚で**
        /// （2026-08-23・作者の指示 ──「色の差し替えを機械的に行い、あらかじめ差し替えた PNG を
        /// ゲームに表示させる」）。
        ///
        /// ⚠️ **以前ここは逆のことを書いていた**（「画像ファイルにしない、同じ絵に色を掛ける」）。
        /// SVG で1画素＝1つの `&lt;rect&gt;` を敷く方式は、64×64 の絵1枚が矩形1,085個になり、
        /// BOX 画面（絵30枚）だけで DOM が 32,663 個・骨組みエディタの1手（土台=panel）が
        /// 最大 359ms まで膨らんでいた（実測 2026-08-23）。
        /// ⭐ 色の差し替え（変異＝パレットスワップ）は**もう実行時にしない**。
        /// `SpritePng.RunDisplay`（Sim）がビルド前に (種族 × パレット) の全通りを焼き、
        /// ここは焼けた PNG のファイル名を引いて `&lt;img&gt;` を1つ置くだけ。
        ///
        /// ⚠️ **卵（`EggArt`）はまだ焼いていない**（種族表の外・View 側に絵が埋まっている、
        /// `SpritePng.Run` のコメント参照）。<see cref="SpriteManifest.StemOf"/> が
        /// 種族表に無いと判じたら、今までどおり SVG で描く ── 焼いていない絵まで
        /// `&lt;img&gt;` にすると、静かに空白へ落ちる。</summary>
        private static void Dots(StringBuilder sb, PixelSprite sprite, Palette palette, LayoutNode node)
        {
            // ⭐ 正方形で描く（検査が「絵は正方形」を要求している）
            float size = Math.Min(node.Width, node.Height);
            string foeClass = node.Option("foe") == "yes" ? " foe" : "";

            string? stem = SpriteManifest.StemOf(sprite, palette);
            if (stem != null)
            {
                if (!SpriteManifest.Exists(stem))
                {
                    // 🔴 **黙って空の四角にしない。**⚠️ `IconManifest` の `icon-missing` と同じ扱い
                    //    ── 種族やパレットを増やしたのに `sim sprites` を走らせ忘れると、
                    //    ここへ落ちて画面の上で気づける（テストでも `SpritePngTests` が落ちる）。
                    sb.Append("<div class=\"n icon-missing\" style=\"left:0;top:0;width:")
                      .Append(Px(size)).Append(";height:").Append(Px(size))
                      .Append("\" title=\"絵が無い: sprite/").Append(Esc(stem))
                      .Append(".png\">？</div>");
                    return;
                }
                // ⚠️ `class="n pixel"` と `foe` の付き方は SVG 版から変えていない
                //    ── `stage.css` の `.n.pixel`（crisp）／`.n.pixel.foe`（左右反転）が
                //    タグの種類を問わずそのまま乗る。
                sb.Append("<img class=\"n pixel").Append(foeClass)
                  .Append("\" src=\"sprite/").Append(Esc(stem)).Append(".png\" alt=\"\"")
                  .Append(" style=\"left:0;top:0;width:").Append(Px(size))
                  .Append(";height:").Append(Px(size)).Append("\" />");
                return;
            }

            // ⚠️ 種族表に無い絵（卵など）は、まだ PNG を焼いていないので SVG のまま描く
            //    （`EggCommand.Sim/Book.cs` と同じやり方 ── 添字色 → `Palette.ColorOf` の
            //    "#rrggbb" をそのまま矩形に）。
            sb.Append("<svg class=\"n pixel").Append(foeClass)
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
