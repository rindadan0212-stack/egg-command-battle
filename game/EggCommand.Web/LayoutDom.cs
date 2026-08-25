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
        /// <summary>⭐ `icon`/`paint` の絵の名前。⚠️ null なら骨組みの `pic=` のまま。</summary>
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
        /// <summary>⭐ 今どの画面を描いているか。⚠️ 「絵が枠に合わない」警告の出所を言うためだけに使う
        /// （<see cref="Render"/> の呼び出しごとに更新される。Blazor WASM は単一スレッドなので、
        /// ふつうの static フィールドで足りる）。</summary>
        private static string _layoutId = "";

        /// <summary>⭐ 「絵が枠に合わない」警告を、同じ組み合わせに1回しか出さないための帳面
        /// （ドット絵化計画 段取り4・第1部「黙って縮めない・黙ってはみ出させない」）。</summary>
        private static readonly HashSet<string> _mismatchWarned = new HashSet<string>();

        /// <summary>⭐ **直前に描いたときに見つかった「枠と絵が合わない」節点**（エディタ用）。
        /// ⚠️ `FindPicMismatches`（骨組みだけを見る）では `pixel` を判定できない ──
        /// どの種族の絵が入るかは `bind=` 越しに実行時に決まるため。⭐ 描く側は実物を持っているので、
        /// **判定を二重に書かずここへ控える**。エディタは描く前に <see cref="ClearDrawnMismatches"/> を
        /// 呼び、描いたあとに <see cref="DrawnMismatches"/> を読む。</summary>
        private static readonly List<PicMismatch> _drawn = new List<PicMismatch>();
        private static readonly HashSet<string> _drawnKeys = new HashSet<string>();

        /// <summary>⚠️ エディタが盤を組み直す**直前**に呼ぶ（溜め込みっぱなしにしない）。</summary>
        public static void ClearDrawnMismatches() { _drawn.Clear(); _drawnKeys.Clear(); }

        /// <summary>直前の描画で見つかった不一致（`icon`/`paint`/`pixel` すべて）。</summary>
        public static IReadOnlyList<PicMismatch> DrawnMismatches => _drawn;

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
            _layoutId = layout.Id;
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
            //    🔴 既定は 40（ドット絵化計画 §6・2026-08-25）── PixelMplus10 が
            //    「絵と同じドットの太さ」で出る唯一の大きさ。以前は Unity の
            //    `Ui.Tappable` に合わせて button=34・label=26 だった（Mochiy Pop One 時代の値）。
            //    ⚠️ 骨組み側の明示 `size=` はこの段では触らない（段取り2で丸める）。
            if (node.Kind == "label" || node.Kind == "button")
                style.Append(";font-size:")
                     .Append(Px(node.Number("size", 40)));

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
            // ⭐ E2: **層（レイヤ）の見なし**（計画 §11-2）。⚠️ 骨組みの字には書かない
            //    （保存されない・編集中の見なしだけ）ので、ここは HTML の属性としてのみ出す
            //    ── `stage.css`（薄くする・二重縁）と `edit.js`（触れなくする）の
            //    両方がこの1つの属性を読む（唯一の出所は `EditLayers.Of`）。
            //    ⚠️ 遊ぶ画面（`/app`）にも同じ属性が付くが、`data-line`/`data-tap` と同じく
            //    そちらの JS（`tap.js`）は読まないので無害（既存の作法どおり）。
            sb.Append(" data-layer=\"").Append(EditLayers.Token(EditLayers.Of(node))).Append('"');
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
                    // ⚠️ **「引き伸ばさない」規則の対象外**（骨組みエディタのプレビュー専用）。
                    //    アップロードした絵の実ドット数を知らない（任意サイズの data URL）ので、
                    //    ここだけは今までどおり枠いっぱいに出す。
                    sb.Append("<div class=\"n icon-art\" style=\"left:0;top:0;width:100%;height:100%;--pic:url(")
                      .Append(Esc(overridePic)).Append(")\"></div>");
                }
                else if (pic != null && !IconManifest.Exists(pic))
                {
                    // 🔴 **黙って空の四角にしない。**⚠️ 表や骨組みが指す名前でも、
                    //    実体（`Resources/UI/icon/<名前>.png`）が無ければここへ落ちる
                    //    ── 埋め込んだ一覧（`IconManifest`）と突き合わせて分かる。
                    string iconStyle = FitDotsStyle(node, "icon", IconDots, IconDots);
                    sb.Append("<div class=\"n icon-missing\" style=\"").Append(iconStyle)
                      .Append("\" title=\"絵が無い: ").Append(Esc(pic)).Append(".png\">？</div>");
                }
                else if (pic != null)
                {
                    // ⭐ 絵の場所は1回だけ言う（`--pic`）。⚠️ 素の絵と、
                    //    その形に切った色の2枚が同じ絵を見るので、二重に書かない。
                    // 🔴 **引き伸ばさない**（段取り4・第1部）── 100%/100% で枠いっぱいに
                    //    伸縮させていたのをやめ、実ドット数×4 で中央に置く。
                    string iconStyle = FitDotsStyle(node, "icon", IconDots, IconDots);
                    sb.Append("<div class=\"n icon-art\" style=\"").Append(iconStyle).Append(";--pic:url(icon/")
                      .Append(Esc(pic)).Append(".png)\"></div>");
                }
            }
            else if (node.Kind == "paint")
            {
                // ⭐ **絵をそのまま出す（色を掛け合わせない）**── icon と違い、
                //    抱き合わせ（mask）の層を持たない（ドット絵化計画 決定10）。
                string pic = (has && fill?.Pic != null ? fill.Pic(bind) : null) ?? node.Option("pic");
                Paint(sb, pic, node);
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
            // 🔴 **引き伸ばさない**（段取り4・第1部）── 「短い辺で正方形」に縮めていたのをやめ、
            //    `sprite.Width * 4` × `sprite.Height * 4` で中央に置く（合わなければ警告）。
            string style = FitDotsStyle(node, "pixel", sprite.Width, sprite.Height);
            string foeClass = node.Option("foe") == "yes" ? " foe" : "";

            string? stem = SpriteManifest.StemOf(sprite, palette);
            if (stem != null)
            {
                if (!SpriteManifest.Exists(stem))
                {
                    // 🔴 **黙って空の四角にしない。**⚠️ `IconManifest` の `icon-missing` と同じ扱い
                    //    ── 種族やパレットを増やしたのに `sim sprites` を走らせ忘れると、
                    //    ここへ落ちて画面の上で気づける（テストでも `SpritePngTests` が落ちる）。
                    sb.Append("<div class=\"n icon-missing\" style=\"").Append(style)
                      .Append("\" title=\"絵が無い: sprite/").Append(Esc(stem))
                      .Append(".png\">？</div>");
                    return;
                }
                // ⚠️ `class="n pixel"` と `foe` の付き方は SVG 版から変えていない
                //    ── `stage.css` の `.n.pixel`（crisp）／`.n.pixel.foe`（左右反転）が
                //    タグの種類を問わずそのまま乗る。
                sb.Append("<img class=\"n pixel").Append(foeClass)
                  .Append("\" src=\"sprite/").Append(Esc(stem)).Append(".png\" alt=\"\"")
                  .Append(" style=\"").Append(style).Append("\" />");
                return;
            }

            // ⚠️ 種族表に無い絵（卵など）は、まだ PNG を焼いていないので SVG のまま描く
            //    （`EggCommand.Sim/Book.cs` と同じやり方 ── 添字色 → `Palette.ColorOf` の
            //    "#rrggbb" をそのまま矩形に）。⭐ ここも同じ「引き伸ばさない」規則（`style`）。
            sb.Append("<svg class=\"n pixel").Append(foeClass)
              .Append("\" viewBox=\"0 0 ")
              .Append(sprite.Width).Append(' ').Append(sprite.Height)
              .Append("\" style=\"").Append(style).Append("\">");
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

        /// <summary>絵をそのまま出す（色を掛け合わせない・ドット絵化計画 決定10）。
        /// ⚠️ icon と違い、色の抱き合わせ層（`::after` の mask）を持たない。
        /// ⭐ 「引き伸ばさない」規則は icon/pixel と同じ（<see cref="FitDotsStyle"/>）、
        /// 絵の実ドット数は <see cref="PaintManifest"/> から引く。</summary>
        private static void Paint(StringBuilder sb, string pic, LayoutNode node)
        {
            if (pic == null)
            {
                // 🔴 黙って空にしない。⚠️ Core の検査（`IconMissingSource`）は icon にしか
                //    掛けていない（paint は枠だけの部品もありうるため）── ここで拾う。
                sb.Append("<div class=\"n paint-missing\" style=\"left:0;top:0;width:100%;height:100%\""
                    + " title=\"paint に pic= が無い\">？</div>");
                return;
            }

            var size = PaintManifest.SizeOf(pic);
            if (size == null)
            {
                // ⚠️ 実ドット数が分からない（実体が無い）。⭐ 枠なりの大きさを仮に使い
                //    （4の倍数の骨組みなら誤差は出ない）、？を出す（icon-missing と同じ扱い）。
                string style = FitDotsStyle(node, "paint", DotsOf(node.Width), DotsOf(node.Height));
                sb.Append("<div class=\"n paint-missing\" style=\"").Append(style)
                  .Append("\" title=\"絵が無い: paint/").Append(Esc(pic)).Append(".png\">？</div>");
                return;
            }

            string ok = FitDotsStyle(node, "paint", size.Value.Width, size.Value.Height);
            sb.Append("<img class=\"n paint\" src=\"paint/").Append(Esc(pic))
              .Append(".png\" alt=\"\" style=\"").Append(ok).Append("\" />");
        }

        /// <summary>⭐ 今ある28枚のアイコンは全部 128×128 実ピクセル（2026-08-25 実測・
        /// `assets/ui/icon/*.png`）── 1ドット=4px の規則に当てはめると
        /// 32×32 ドット。⚠️ アイコンをまだ 8/12/16ドットで描き直していない
        /// （計画 §8-3・未着手）ので、いまはこの実測値を定数で持つ。将来描き直したら、
        /// `IconManifest` を `PaintManifest` と同じ「大きさ付き一覧」に差し替えて、
        /// この定数を消すこと。</summary>
        private const int IconDots = 32;

        /// <summary>絵を「ドット数×4px」で節点の中央に置くスタイル文字列を作る。
        /// 🔴 **引き伸ばさない**（段取り4・第1部）── 枠に合わなければ位置だけ中央寄せし、
        /// 大きさはそのまま（縮めない・はみ出しても隠さない）。
        /// ⭐ 合っていなければ console に1回だけ警告する（<see cref="WarnMismatch"/>）。</summary>
        private static string FitDotsStyle(LayoutNode node, string kind, int dotsW, int dotsH)
        {
            float w = dotsW * 4f;
            float h = dotsH * 4f;
            float left = (node.Width - w) / 2f;
            float top = (node.Height - h) / 2f;
            if (Math.Abs(node.Width - w) > 0.5f || Math.Abs(node.Height - h) > 0.5f)
                WarnMismatch(node, kind, dotsW, dotsH, w, h);
            return "left:" + Px(left) + ";top:" + Px(top) + ";width:" + Px(w) + ";height:" + Px(h);
        }

        /// <summary>⚠️ **黙って縮めない・黙ってはみ出させない。**どこがどう合っていないかを言う。
        /// ⭐ 同じ「画面/部品 節点 種類」の組には1回しか出さない（連打しない）。</summary>
        private static void WarnMismatch(LayoutNode node, string kind, int dotsW, int dotsH, float w, float h)
        {
            // ⭐ `use=` で差し込まれた側は `PartId` を持つ（`data-part` と同じ規約）。
            //    それを冠のように前に付けて、どの部品ファイルの節点かが分かるようにする
            //    （例: "home/slot art" ── home 画面の、slot 部品の、art 節点）。
            string place = node.PartId != null ? node.PartId + " " + node.Name : node.Name;
            string key = _layoutId + "/" + place + " " + kind;

            // ⭐ **描いたときに分かった不一致を、そのまま控える**（2026-08-25）。
            //    🔴 これが無いと、エディタは `pixel`（種族の絵）の不一致を**一件も出せない**
            //    ── `FindPicMismatches` は骨組みだけを見るので、`bind=` で実行時に決まる絵の
            //    実寸を知りようがない（実測: `box` はコンソールに2件出るのにエディタは0件だった）。
            //    ⭐ 描く側は実物の絵を持っている ── **判定を二重に書かず、ここの結果を配る**。
            //    ⚠️ 溜め込みっぱなしにしない（エディタが描く前に `ClearDrawnMismatches`）。
            if (_drawnKeys.Add(key))
                _drawn.Add(new PicMismatch(node.LineNumber, node.PartId, node.PartLine,
                    node.Name, kind, node.Option("pic") ?? "", w, h, node.Width, node.Height));

            if (!_mismatchWarned.Add(key)) return;
            // 🔴 **`Console.Error` を使わない。**（2026-08-25・実測して判明）
            //    ⚠️ `dotnet run`（Development）の WASM ホストは .NET の stderr 書き込みを
            //    `dotNetCriticalError` として扱い、**「何かが壊れました」の赤い帯**を出す
            //    （`web移行計画.md` の「踏んだ罠: Console.Error.WriteLine が Blazor の赤い帯を出す」）。
            //    ⭐ ここは**移行の途中で必ず鳴る**知らせ（32画面ぶん・計45箇所）なので、
            //    赤い帯を出すと**本物のクラッシュを覆い隠す**。だから普通の書き出しにする。
            //    ⚠️ 「黙って縮めない・黙ってはみ出させない」の約束は守られている
            //    （console に必ず出る。ただの色が違うだけ）。
            Console.WriteLine(
                "絵が枠に合わない: " + _layoutId + "/" + place + " " + kind + " "
                + dotsW + "x" + dotsH + "ドット=" + Px(w) + "x" + Px(h)
                + " なのに枠は" + Px(node.Width) + "x" + Px(node.Height));
        }

        /// <summary>絵の実ドット数が分からないとき、枠の大きさから逆算する。
        /// ⚠️ 4の倍数の骨組みなら誤差は出ない（計画 §2・§8-1 の升目直しが前提）。</summary>
        private static int DotsOf(float px) => Math.Max(1, (int)Math.Round(px / 4f));

        // ── ⭐ E1: 骨組みエディタ向け「枠と絵の実寸」問い合わせ ──────────
        //
        // ⚠️ 遊ぶ画面の描画（`FitDotsStyle`/`Paint`/`Dots`）とは**あえて別の物差し**にする。
        //    描く側は「分からなければ枠を信じる」（黙って壊さない）側へ倒すが、エディタは
        //    「分からなければ何も言わない」側へ倒したい（分からないのに合っていると
        //    誤って報告しない）── 目的が違うので、同じ関数を無理に共用しない。

        /// <summary>その絵の「引き伸ばさない」実寸（設計px）── 分かるときだけ。
        /// ⭐ 骨組みエディタ（E1-4 絵を選んだら枠を自動で合わせる／E1-5 不一致の検出）が
        /// 使う唯一の出所。⚠️ icon はまだ実物の大きさを持っていない（計画 §8-3・未着手）
        /// ので <see cref="IconDots"/>（実測32ドット・全アイコン共通）で代用する。paint は
        /// <see cref="PaintManifest.SizeOf"/> を引く。⚠️ 名前が一覧に無ければ null
        /// （「分からない」を「合っている」と偽らない）。</summary>
        public static (float Width, float Height)? ExpectedPicSize(string kind, string? pic)
        {
            if (string.IsNullOrEmpty(pic)) return null;
            if (kind == "icon")
                return IconManifest.Exists(pic) ? (IconDots * 4f, IconDots * 4f) : ((float, float)?)null;
            if (kind == "paint")
            {
                var size = PaintManifest.SizeOf(pic);
                return size != null ? (size.Value.Width * 4f, size.Value.Height * 4f) : ((float, float)?)null;
            }
            return null;
        }

        /// <summary>⭐ E1-5: 「枠と絵が合わない」節点1件ぶん。⚠️ <see cref="LayoutNode.LineNumber"/>/
        /// <see cref="LayoutNode.PartId"/>/<see cref="LayoutNode.PartLine"/> と同じ規約
        /// （<see cref="Fault"/> に倣う ── 自前の行は <c>PartId==null &amp;&amp; LineNumber&gt;=0</c>、
        /// 部品から来た側は <c>PartId!=null &amp;&amp; LineNumber==-1</c>）。</summary>
        public readonly struct PicMismatch
        {
            public readonly int LineNumber;
            public readonly string? PartId;
            public readonly int PartLine;
            public readonly string Name;
            public readonly string Kind;
            public readonly string Pic;
            /// <summary>絵の実寸（設計px）。</summary>
            public readonly float ExpectW, ExpectH;
            /// <summary>いまの枠（設計px）。</summary>
            public readonly float FrameW, FrameH;

            public PicMismatch(int lineNumber, string? partId, int partLine, string name, string kind, string pic,
                float expectW, float expectH, float frameW, float frameH)
            {
                LineNumber = lineNumber; PartId = partId; PartLine = partLine;
                Name = name; Kind = kind; Pic = pic;
                ExpectW = expectW; ExpectH = expectH; FrameW = frameW; FrameH = frameH;
            }
        }

        /// <summary>⭐ E1-5: 骨組みエディタが「枠と絵が合わない」節点を洗い出す唯一の出所。
        /// ⚠️ `bind=` で絵が決まる節点（実行時にしか分からない）は対象にしない ── 骨組みの
        /// `pic=` が直に書いてある節点だけを見る（<see cref="ExpectedPicSize"/> と同じ理由）。</summary>
        public static List<PicMismatch> FindPicMismatches(EggCommand.Core.Layout? layout)
        {
            var result = new List<PicMismatch>();
            if (layout == null) return result;
            foreach (var root in layout.Roots) WalkPicMismatch(root, result);
            return result;
        }

        private static void WalkPicMismatch(LayoutNode node, List<PicMismatch> into)
        {
            if (node.Kind == "icon" || node.Kind == "paint")
            {
                string? pic = node.Option("pic");
                var expect = ExpectedPicSize(node.Kind, pic);
                if (expect is { } size
                    && (Math.Abs(node.Width - size.Width) > 0.5f || Math.Abs(node.Height - size.Height) > 0.5f))
                {
                    into.Add(new PicMismatch(node.LineNumber, node.PartId, node.PartLine,
                        node.Name, node.Kind, pic!, size.Width, size.Height, node.Width, node.Height));
                }
            }
            foreach (var child in node.Children) WalkPicMismatch(child, into);
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
