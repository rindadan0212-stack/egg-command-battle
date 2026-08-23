using System;
using System.Collections.Generic;
using System.Text;

namespace EggCommand.Core
{
    /// <summary>節点の1行にあった、空白区切りの欄1つぶん。
    ///
    /// ⭐ **「欄の字」と「開始桁」の対。**⚠️ `text=` は行末までで1つの欄として持つ
    /// （中に空白があっても割らない ── 割ると書き出しで元の桁に戻せない）。
    ///
    /// ⭐ **これが桁揃えの唯一の出所。**この骨組みは手で桁を揃えてあるので、
    /// 値の桁数が変わっても後ろの欄をこの桁へ合わせて詰め直せる
    /// （<see cref="LayoutNode.RenderLine"/> が使う）。</summary>
    public sealed class LayoutField
    {
        /// <summary>元の綴りそのまま。⚠️ 数の `0` と `0.0` の違いや、
        /// `text=` の `\n` 展開前の字は、ここにしか残っていない。</summary>
        public readonly string Text;
        /// <summary>行頭からの開始桁（0始まり）。</summary>
        public readonly int Column;

        public LayoutField(string text, int column)
        {
            Text = text;
            Column = column;
        }
    }

    /// <summary>画面の骨組み1つ。⭐ **座標はここにしか無い。**
    ///
    /// ⚠️ コードに座標を書かせないための型です（2026-08-22・作者の指示
    /// 「コードでボタンを作るからいけないのでは？すべてアセットを使用することを
    /// 厳格に守れば」）。⭐ コードがするのは **bind に値を差すこと**と
    /// **button に手を繋ぐこと**だけ。
    ///
    /// ⚠️ **Core に置く理由**は検査です。座標がデータなら、重なりもはみ出しも
    /// **エンジンを起動せずに**数えられます（実測 2026-08-22: Unity の往復は
    /// 無変更でも19秒、コンパイル確認だけなら1.2秒）。</summary>
    public sealed class LayoutNode
    {
        /// <summary>部品の名前。⚠️ 同じ親の中で重ねない（<see cref="Layouts.Faults"/> が落とす）。</summary>
        public readonly string Name;
        /// <summary>何を出すか。⚠️ 知らない種類は読み込みで落とす（黙って素通りさせない）。</summary>
        public readonly string Kind;
        /// <summary>親の左上からのずれと大きさ。</summary>
        public readonly float Left, Top, Width, Height;
        /// <summary>`key=value` の付け足し。⚠️ 中身の解釈は描く側の仕事。</summary>
        public readonly IReadOnlyDictionary<string, string> Options;
        public readonly IReadOnlyList<LayoutNode> Children;

        /// <summary>元の行の番号（0始まり）。⚠️ <see cref="Layouts.Parse"/> を通さずに
        /// 組み立てた節点は -1。
        /// ⭐ <see cref="Layouts.Write"/> はこれで「原文のどの行を置き換えるか」を知る。
        ///
        /// ⚠️ <see cref="Layouts.Resolve"/>（`use=` の差し替え）を通ったあとも、
        /// **この骨組み自身の行から来た節点は値を保つ**（`Layouts.Splice` が運ぶ）。
        /// -1 になるのは、**差し込まれた側**（`use=` で差した部品の中身。
        /// <see cref="Layouts.Rename"/> を通る）だけ ── 別ファイルの行番号を
        /// この骨組みの選択に使うと指し示す先を取り違えるので、そこだけ意図的に捨てる。
        /// ⭐ これがエディタの `data-line`（節点を選ぶ土台）の出所そのもの。</summary>
        public readonly int LineNumber;
        /// <summary>行頭の空白の数（字下げ）。</summary>
        public readonly int Indent;
        /// <summary>元の行にあった欄の並び（名前・種類・左上幅高・付け足し）。
        /// ⚠️ 空なら「元の行を知らない」節点 ── <see cref="RenderLine"/> は詰め直さず
        /// 素直な1個空白区切りで書く。</summary>
        public readonly IReadOnlyList<LayoutField> Fields;
        /// <summary>最後の欄のあとに残っていた空白（あれば・普通は空）。</summary>
        public readonly string Trailing;
        /// <summary>この行の終端文字。`"\r\n"` / `"\n"` / `""`（最終行で改行が無い）。</summary>
        public readonly string Terminator;

        public LayoutNode(string name, string kind, float left, float top, float width, float height,
            IReadOnlyDictionary<string, string> options, IReadOnlyList<LayoutNode> children)
            : this(name, kind, left, top, width, height, options, children, -1, 0, null, "", "\n")
        {
        }

        /// <summary>⚠️ <see cref="Layouts.Parse"/> 専用。行の情報まで持つ節点を組み立てる。
        /// ⭐ 上の（短い）コンストラクタは、行を知らない節点用 ── 呼び分ける理由が無い限り
        /// 使わない（コードでは座標を作らない）。</summary>
        public LayoutNode(string name, string kind, float left, float top, float width, float height,
            IReadOnlyDictionary<string, string> options, IReadOnlyList<LayoutNode> children,
            int lineNumber, int indent, IReadOnlyList<LayoutField> fields, string trailing, string terminator)
        {
            Name = name;
            Kind = kind;
            Left = left;
            Top = top;
            Width = width;
            Height = height;
            Options = options ?? new Dictionary<string, string>();
            Children = children ?? new List<LayoutNode>();
            LineNumber = lineNumber;
            Indent = indent;
            Fields = fields ?? new List<LayoutField>();
            Trailing = trailing ?? "";
            Terminator = terminator ?? "";
        }

        public string Option(string key)
        {
            string found;
            return Options.TryGetValue(key, out found) ? found : null;
        }

        public bool Flag(string key) => Option(key) != null;

        public int Number(string key, int fallback)
        {
            var text = Option(key);
            int value;
            return text != null && int.TryParse(text, out value) ? value : fallback;
        }

        public override string ToString() =>
            $"{Name}({Kind}) {Left},{Top} {Width}x{Height}";

        // ── 書き出し（⭐ Layouts.Write の中核）────────────

        /// <summary>元の綴りの「text=」の頭。⚠️ <see cref="Layouts.TextMark"/>（先頭に区切りの
        /// 空白を持つ）と1つの出所を保つため、そこから削って作る。</summary>
        private static readonly string TextFieldPrefix = Layouts.TextMark.Substring(1);

        /// <summary>組み直す欄1つぶん。⚠️ 元の行に無かった（今の <see cref="Options"/> にだけ
        /// 在る）欄は桁の記録が無いので、<see cref="HasColumn"/> を false にして区別する
        /// （<see cref="RenderLine"/> はこれを見て、詰め直さず空白1つで足す）。</summary>
        private sealed class Slot
        {
            public readonly string Text;
            public readonly bool HasColumn;
            public readonly int Column;

            public Slot(string text, bool hasColumn, int column)
            {
                Text = text;
                HasColumn = hasColumn;
                Column = column;
            }
        }

        /// <summary>この節点の行を、保持した欄から組み直す。
        ///
        /// ⚠️ 🔴 **原文の行を丸ごと返さない。**ここが echo だと、「値を直したのに
        /// 書き出しへ反映されない」を検査が見つけられなくなる（空回り）。
        /// ⭐ 欄ごとに「値が変わっていないか」を見て、変わっていなければ元の綴り
        /// （`0` と `0.0` の違い等）を、変わっていれば今の値を書く。
        ///
        /// ⭐ **後ろの欄は元の桁に合わせて詰め直す。**⚠️ 既にその桁を過ぎていたら、
        /// 欄どうしがくっつかないよう空白1つへ縮退する（壊さないため）。</summary>
        public string RenderLine()
        {
            var sb = new StringBuilder();
            if (Fields.Count == 0)
            {
                // ⚠️ 元の行を知らない節点（Parse を通していない）。
                //    ⭐ 詰め直す基準が無いので、素直な1個空白区切りで書く。
                sb.Append(' ', Math.Max(0, Indent));
                AppendPlain(sb);
                return sb.ToString() + Terminator;
            }

            var slots = BuildSlots();
            int pos = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                int pad;
                if (!slot.HasColumn)
                {
                    // ⚠️ 元の行に無かった欄（付け足しの新顔）── 桁の記録が無いので空白1つで足す。
                    pad = 1;
                }
                else
                {
                    pad = slot.Column - pos;
                    // ⚠️ 最初の欄だけ、桁 0（字下げ無し）を空白 0 で許す。
                    //    ⭐ 2番目以降は、欄どうしが**くっつかない**よう最低1個は空ける。
                    if (i == 0) pad = Math.Max(0, pad);
                    else if (pad < 1) pad = 1;
                }
                sb.Append(' ', pad);
                sb.Append(slot.Text);
                pos += pad + slot.Text.Length;
            }
            sb.Append(Trailing);
            return sb.ToString() + Terminator;
        }

        /// <summary>組み直す欄の並びを作る。⭐ 3つを順に並べるだけ:
        /// ① 名前・種類・左上幅高（常に6欄、消えることは無い）
        /// ② 元の行にあった付け足し（`text=` を除く。<see cref="Options"/> から
        ///    消えていたら欄ごと省く）
        /// ③ 今の <see cref="Options"/> にだけある付け足し（新顔。並び順は Options の
        ///    列挙順に従う ── 元に無かった欄には、他に基準にできる並びが無い）
        /// そして最後に `text=`（元に在れば更新、無くて今だけ在れば新規、両方無ければ無し）。
        ///
        /// ⚠️ 🔴 **`text=` を先に処理しない。**ここの並び順がそのまま書き出しの並びになる
        /// ── 先に置くと、新しく足した付け足しが `text=` の後ろに来て事故る
        /// （実際に釦へ「あきらめる when=!done」と字が出た罠と同じ形）。</summary>
        private List<Slot> BuildSlots()
        {
            var slots = new List<Slot>(Fields.Count)
            {
                new Slot(Name, true, Fields[0].Column),
                new Slot(Kind, true, Fields[1].Column),
                new Slot(FormatNumber(Fields[2].Text, Left), true, Fields[2].Column),
                new Slot(FormatNumber(Fields[3].Text, Top), true, Fields[3].Column),
                new Slot(FormatNumber(Fields[4].Text, Width), true, Fields[4].Column),
                new Slot(FormatNumber(Fields[5].Text, Height), true, Fields[5].Column),
            };

            var kept = new HashSet<string>();
            string textRaw = null;
            int textColumn = -1;
            for (int i = 6; i < Fields.Count; i++)
            {
                string raw = Fields[i].Text;
                if (raw.StartsWith(TextFieldPrefix, StringComparison.Ordinal))
                {
                    textRaw = raw;
                    textColumn = Fields[i].Column;
                    continue;
                }
                string key = KeyOf(raw);
                if (Option(key) == null) continue;   // ⚠️ Options から消えた ── 欄ごと無くなる
                kept.Add(key);
                slots.Add(new Slot(CurrentOptionText(raw), true, Fields[i].Column));
            }

            // ⭐ 新顔（元の行に無かった付け足し）。⚠️ 桁の記録が無いので空白1つで足す。
            foreach (var pair in Options)
            {
                if (pair.Key == "text" || kept.Contains(pair.Key)) continue;
                slots.Add(new Slot(pair.Key + "=" + pair.Value, false, -1));
            }

            // ⚠️ `text=` は必ず最後（規約）。
            if (textRaw != null)
            {
                if (Option("text") != null) slots.Add(new Slot(CurrentOptionText(textRaw), true, textColumn));
                // else: 消された ── 欄ごと無くなる
            }
            else if (Option("text") != null)
            {
                slots.Add(new Slot(TextFieldPrefix + Option("text").Replace("\n", "\\n"), false, -1));
            }

            return slots;
        }

        /// <summary>`key=value` の欄から `key` だけを取り出す。</summary>
        private static string KeyOf(string keyValue) => keyValue.Substring(0, keyValue.IndexOf('='));

        /// <summary>数の欄。⚠️ 値が変わっていなければ元の綴りを守る
        /// （float に落とすと `0` と `0.0` の違いが消えるため）。
        /// ⭐ 変わっていれば今の値を書く ── そこだけは元の綴りを再現できない。</summary>
        private static string FormatNumber(string raw, float current)
        {
            float parsed;
            if (float.TryParse(raw, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out parsed)
                && parsed == current)
                return raw;
            return current.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>`key=value` か `text=...`（行末までの1欄）を、今の値で書き直す。</summary>
        private string CurrentOptionText(string raw)
        {
            if (raw.StartsWith(TextFieldPrefix, StringComparison.Ordinal))
            {
                // ⭐ `text=` は「展開前の字」で持っている。⚠️ 変わっていなければそのまま、
                //    変わっていれば今の値を `\n` → `\\n` へ戻して書く（読み込みの逆）。
                string rawLiteral = raw.Substring(TextFieldPrefix.Length);
                string originalExpanded = rawLiteral.Replace("\\n", "\n");
                string current = Option("text") ?? "";
                return current == originalExpanded
                    ? raw
                    : TextFieldPrefix + current.Replace("\n", "\\n");
            }

            int eq = raw.IndexOf('=');
            string key = raw.Substring(0, eq);
            string rawValue = raw.Substring(eq + 1);
            string value = Option(key);
            return value == rawValue ? raw : key + "=" + (value ?? "");
        }

        /// <summary>欄の位置を知らないときの、素直な書き方（1個空白区切り）。</summary>
        private void AppendPlain(StringBuilder sb)
        {
            sb.Append(Name).Append(' ').Append(Kind).Append(' ')
              .Append(Left.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(' ')
              .Append(Top.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(' ')
              .Append(Width.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(' ')
              .Append(Height.ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach (var pair in Options)
            {
                if (pair.Key == "text") continue;   // ⭐ text= は行末へ回すので後で足す
                sb.Append(' ').Append(pair.Key).Append('=').Append(pair.Value);
            }
            string text = Option("text");
            if (text != null) sb.Append(' ').Append(TextFieldPrefix).Append(text.Replace("\n", "\\n"));
        }
    }

    /// <summary>原文の行1つぶん（節点の行かどうかは問わない）。⭐ 終端文字ごと持つ。
    /// <see cref="Layouts.Write"/> はこれを土台にして、節点の行だけ組み直した文字を差し込む。</summary>
    public sealed class RawLine
    {
        /// <summary>終端文字を含まない中身。</summary>
        public readonly string Text;
        /// <summary>この行の終端文字。`"\r\n"` / `"\n"` / `""`（最終行で改行が無い）。</summary>
        public readonly string Terminator;

        public RawLine(string text, string terminator)
        {
            Text = text;
            Terminator = terminator;
        }
    }

    /// <summary>1画面ぶんの骨組み。</summary>
    public sealed class Layout
    {
        public readonly string Id;
        public readonly IReadOnlyList<LayoutNode> Roots;
        /// <summary>原文の行、全部。⭐ コメント・空行を <see cref="Layouts.Write"/> が
        /// そのまま通す元 ── 節点の行はここでなく <see cref="LayoutNode"/> 自身が持つ。</summary>
        public readonly IReadOnlyList<RawLine> Lines;
        /// <summary>⭐ <see cref="Layouts.Resolve"/> を通した（`use=` を差し替え済みの）木か。
        ///
        /// ⚠️ 差し替えは <see cref="Layouts.Splice"/> / <see cref="Layouts.Rename"/> が
        /// **毎回新しい節点を作り直す**ので、差し替え後の木は部品がインライン展開されている。
        /// ⚠️ **差し込まれた側**（`use=` で差した部品の中身）は行番号
        /// （<see cref="LayoutNode.LineNumber"/>）も失われる ── 別ファイルの行を
        /// この骨組みのものとして書き出さないため。⭐ この骨組み自身の行から来た節点は、
        /// この旗が立っていても行番号を保ったまま（エディタの選択はここを読む）。
        /// ⚠️ **それでも書き戻しは断る** ── 部品がインライン展開された時点で、
        /// 原文には無い行が並ぶことに変わりはない。この旗を見て <see cref="Layouts.Write"/>
        /// が断る ── 黙って原文に無いものを書き出すのが一番困る。</summary>
        public readonly bool Resolved;

        public Layout(string id, IReadOnlyList<LayoutNode> roots)
            : this(id, roots, null, false)
        {
        }

        /// <summary>⚠️ <see cref="Layouts.Parse"/> 専用。原文の行まで持つ骨組みを組み立てる。</summary>
        public Layout(string id, IReadOnlyList<LayoutNode> roots, IReadOnlyList<RawLine> lines)
            : this(id, roots, lines, false)
        {
        }

        /// <summary>⚠️ <see cref="Layouts.Resolve"/> 専用。解決済みの旗を立てて組み立てる。</summary>
        public Layout(string id, IReadOnlyList<LayoutNode> roots, IReadOnlyList<RawLine> lines, bool resolved)
        {
            Id = id;
            Roots = roots;
            Lines = lines ?? new List<RawLine>();
            Resolved = resolved;
        }
    }

    /// <summary>骨組みの読み込みと検査。
    ///
    /// ⭐ **形式は行ベース。**⚠️ JSON にしません ── Unity と .NET で読む道具が違い、
    /// Core に依存を足すことになるためです。⭐ 行ベースなら
    /// <see cref="PixelSprite.Parse"/> と同じく `Split` だけで読めて、
    /// **差分が読める**（エディタが吐いたものを目で確かめられる）。
    ///
    /// 書き方:
    /// <code>
    /// head   label  48 16 984 44   size=28 ink=dim anchor=left text=手に入れた種族
    /// grid   scroll 48 76 984 1432 content=1280
    ///   cell card   0 0 317 304    repeat=species
    ///     art pixel 78 24 160 160  bind=art
    /// </code>
    /// ⚠️ 字下げが親子。⭐ **空白2つで1段**（タブは混ぜない ── 落とします）。</summary>
    public static class Layouts
    {
        /// <summary>画面の大きさ。⚠️ <c>Ui.W</c> / <c>Ui.H</c> と同じ数。
        /// ⭐ ここが検査の基準なので、View 側と食い違わせない。</summary>
        public const float ScreenWidth = 1080f;
        public const float ScreenHeight = 1920f;

        /// <summary>指で押せる最小の高さ。⚠️ View の <c>Ui.Tap</c> と同じ数。</summary>
        public const float TapHeight = 112f;

        /// <summary>知っている種類。⚠️ **ここに無いものは落とす。**
        /// ⭐ 黙って素通りさせると、綴り違いが「何も出ない」として通ります。</summary>
        public static readonly string[] Kinds =
        {
            "box",      // 何も描かない入れ物（まとめるだけ）
            "card",     // 面（明度差）で区切る札
            "label",    // 字
            "pixel",    // ドット絵
            "button",   // 押しどころ
            "scroll",   // 巻物（content= で中身の高さ）
            "round",    // 丸
            "veil",     // ⭐ 覆い（地を暗くし、後ろを押させない）
            "line",     // ⭐ 区切りの1本（⚠️ 一辺だけ・面と二重に使わない）
            "bar",      // ⭐ 割合で伸びる帯（孵化の残り・HP）
            "host",     // ⭐ **ここの中は骨組みが知らない**と宣言する枠
            "icon",     // ⭐ 絵の印（`pic=` で名前。色は `ink=` か `bind=`）
        };

        /// <summary>⭐ **骨組みが中を知らない枠か。**
        ///
        /// ⚠️ 放置の帯・盤・戦闘の場は、位置を**体数や時間から逆算**しているので、
        /// 座標を書き出せない。⭐ それを「まだ移していない」と混ぜないための印。
        ///
        /// ⭐ **枠そのものは検査する**（大きさ・場所・重なり）。
        /// ⚠️ 中に子を書いたら落とす ── 書けるなら host ではない。</summary>
        public static bool IsHost(LayoutNode node) => node.Kind == "host";

        /// <summary>押しどころとして指が触れる種類。⚠️ 高さの検査はこれだけに掛ける。</summary>
        private static bool IsTappable(string kind) => kind == "button";

        /// <summary>知っている付け足し。⚠️ **ここに無い名前は落とす。**
        /// ⭐ `anchr=left` のような綴り違いが黙って無視されると、
        /// 「直したのに効かない」を延々と追うことになる。</summary>
        public static readonly string[] Options =
        {
            "size",     // 字の大きさ
            "ink",      // 字の色（名前で指す。色そのものは書かない）
            "anchor",   // 字の寄せ
            "bind",     // 値の差し込み口
            "tap",      // 押しどころ（⭐ 指で押す── 高さ 112 以上）
            "hold",     // ⭐ **長押しで開く札**（⚠️ 押しどころではない）
            "lead",     // 主導線の見た目にするか
            "repeat",   // 繰り返す元（データの名前）
            "cols",     // 繰り返しの列数
            "gap",      // 繰り返しの隙間
            "rows",     // 繰り返しの1段ぶんの高さ
            "max",      // 繰り返しの上限（⚠️ 巻物の外で繰り返すときは必須）
            "when",     // ⭐ 条件で出す／出さない（`when=有る` / `when=!有る`）
            "foe",      // ⭐ 左右反転して出す（敵はすべて反転・2026-08-21 の指示）
            "use",      // ⭐ 別の骨組みを部品として差す（使い回し）
            "text",     // ⭐ **動かない字**（⚠️ 必ず行の最後。以降は全部その字）
            "flow",     // ⭐ 兄弟を上から詰める（`flow=down`）
            "wrap",     // ⭐ 枠の幅で折り返す（`wrap=yes`）
            "dock",     // ⭐ 下の帯を跨いでよい（`dock=no`）── 帯そのものだけ
            "pic",      // ⭐ 絵の名前（`Resources/UI/icon/<名前>.png`）
            "turn",     // ⭐ 絵を回す度数（矢印を ±90 するのに使う）
        };

        /// <summary>⭐ **兄弟を上から詰めるか。**
        ///
        /// ⚠️ 詰める親の中では、子の `上` は**絶対位置でなく「その上に空ける隙間」**に変わる。
        /// ⭐ こうしないと、出ない子（`when=`）の高さぶんの空白がそのまま残る
        /// ── パーティ編成で実際に起きて、レビューで指摘された（2026-08-19）。</summary>
        public static bool Flows(LayoutNode node) => node.Option("flow") == "down";

        /// <summary>詰めたときの、子それぞれの上端。⭐ **ここが唯一の出所。**
        ///
        /// ⚠️ 検査（<see cref="Faults"/>）と描く側が別々に数えると、必ずずれる。
        /// ⭐ 検査は「全部出る・繰り返しは `max=` まで」の**いちばん深い場合**で見る
        /// （実際はそれより浅くなるので、安全側）。</summary>
        /// <param name="shows">その子を出すか。⚠️ null なら全部出るものとして数える。</param>
        /// <param name="countOf">繰り返しの個数。⚠️ null なら `max=`。</param>
        public static float[] TopsOf(LayoutNode parent,
            Func<LayoutNode, bool> shows, Func<LayoutNode, int> countOf)
        {
            var tops = new float[parent.Children.Count];
            if (!Flows(parent))
            {
                for (int i = 0; i < tops.Length; i++) tops[i] = parent.Children[i].Top;
                return tops;
            }
            float y = 0f;
            for (int i = 0; i < parent.Children.Count; i++)
            {
                var child = parent.Children[i];
                tops[i] = y + child.Top;
                // ⭐ 出さない子は場所を取らない（これが `flow=down` の目的そのもの）
                if (shows != null && !shows(child)) { tops[i] = y; continue; }
                y = tops[i] + DeepOf(child, countOf);
            }
            return tops;
        }

        /// <summary>その子が縦に使う高さ。⭐ 繰り返しなら段数ぶん。</summary>
        private static float DeepOf(LayoutNode node, Func<LayoutNode, int> countOf)
        {
            if (node.Option("repeat") == null) return node.Height;
            int count = countOf != null ? countOf(node) : node.Number("max", 0);
            int cols = Math.Max(1, node.Number("cols", 1));
            int rows = (count + cols - 1) / cols;
            return rows <= 0 ? 0f : (rows - 1) * StepOf(node) + node.Height;
        }

        /// <summary>⭐ **動かない字は骨組みに、動く字だけ `bind`。**
        ///
        /// ⚠️ `bind=` 一本槍にすると、「特性」「決定」「空き」のような**変わらない字**まで
        /// コードの `switch` へ戻ります。⭐ しかも switch 側には検査が1つも掛からない
        /// （綴り違いは `?? ""` で素通りする）。
        ///
        /// ⚠️ **`text=` は必ず行の最後。**⭐ 規約はこれ1つだけで、
        /// 引用符もエスケープも要らない（空白も「　」もそのまま書ける）。</summary>
        public const string TextMark = " text=";

        /// <summary>その部品を出す条件の名前。⚠️ null なら常に出す。
        ///
        /// ⭐ **式は書けない。名前だけ。**⚠️ 骨組みに `and` や比較を入れ始めると、
        /// そこが第二のプログラムになり、検査も編集も追えなくなる。
        /// 真偽を決めるのは呼ぶ側（`When(key)`）。</summary>
        public static string WhenOf(LayoutNode node)
        {
            var text = node.Option("when");
            if (text == null) return null;
            return text.Length > 0 && text[0] == '!' ? text.Substring(1) : text;
        }

        /// <summary>その条件が「偽のとき出す」ものか（`when=!有る`）。</summary>
        public static bool WhenNot(LayoutNode node)
        {
            var text = node.Option("when");
            return text != null && text.Length > 0 && text[0] == '!';
        }

        /// <summary>⭐ **2つが同時には出ないか。**`when=x` と `when=!x` は排他。
        ///
        /// ⚠️ これが無いと、条件で入れ替わる2つを**重なっている**と誤って落とす
        /// （どちらか片方しか出ないのに）。</summary>
        public static bool Exclusive(LayoutNode a, LayoutNode b)
        {
            var ka = WhenOf(a);
            var kb = WhenOf(b);
            if (ka == null || kb == null || ka != kb) return false;
            return WhenNot(a) != WhenNot(b);
        }

        /// <summary>繰り返しの1段ぶんの高さ。⭐ **ここが唯一の出所。**
        ///
        /// ⚠️ 2026-08-22 の初版は、置く側が `高さ+隙間`・数える側が `高さ` と
        /// **別々に決めていた**ので、`rows=` を書かない画面で
        /// **巻物の中身が1段あたり隙間のぶん足りなくなる**穴があった。</summary>
        public static float StepOf(LayoutNode node) =>
            node.Number("rows", (int)(node.Height + node.Number("gap", 0)));

        /// <summary>字を出す種類。⚠️ 重なりの検査はこれだけに掛ける
        /// （札の上に字が乗るのは当たり前なので、面どうしは見ない）。</summary>
        private static bool IsText(string kind) => kind == "label";

        // ── 読み込み ────────────────────────────────────

        /// <summary>その行が節点を持たない行か（空行・コメント）。
        /// ⚠️ <see cref="Parse"/> の読み飛ばしと <see cref="Write"/> の素通しは、
        /// **同じ規則**でなければ食い違う（節点が消えた行を空行と間違えて残す等）。
        /// ⭐ だから1か所にまとめる。</summary>
        private static bool IsSkippable(string raw)
        {
            string body = raw.Trim();
            return body.Length == 0 || body[0] == '#';
        }

        /// <summary>原文を行ごとに割る。⚠️ `Split('\n')` は終端文字を捨ててしまうので、
        /// ここでは終端文字（`"\r\n"` / `"\n"` / `""`）を1行ごとに別で持たせる
        /// ── <see cref="Write"/> がバイト単位で元に戻すために要る。
        ///
        /// ⭐ 中身の割り方は今までと同じ（`\r\n` / `\n` / 裸の `\r` を区切りとみなす）。
        /// 最後の1行は、ファイルが改行で終わっていれば空、終わっていなければ
        /// 残りの字そのもの ── どちらも終端文字は `""`。</summary>
        private static List<RawLine> SplitLines(string text)
        {
            var result = new List<RawLine>();
            int start = 0;
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c != '\r' && c != '\n') { i++; continue; }
                string content = text.Substring(start, i - start);
                string terminator;
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    terminator = "\r\n";
                    i += 2;
                }
                else
                {
                    terminator = c.ToString();
                    i += 1;
                }
                result.Add(new RawLine(content, terminator));
                start = i;
            }
            result.Add(new RawLine(text.Substring(start), ""));
            return result;
        }

        /// <summary>空白区切りの欄へ割る。⚠️ `Split(' ', RemoveEmptyEntries)` と
        /// 同じ結果（連続する空白は1つの区切りとみなす）だが、**各欄の開始桁も残す**
        /// ── 桁揃えを保った書き出しに要る。</summary>
        /// <param name="offset">`body` が原文の何桁目から始まっているか。</param>
        private static List<LayoutField> Tokenize(string body, int offset)
        {
            var fields = new List<LayoutField>();
            int i = 0;
            while (i < body.Length)
            {
                if (body[i] == ' ') { i++; continue; }
                int start = i;
                while (i < body.Length && body[i] != ' ') i++;
                fields.Add(new LayoutField(body.Substring(start, i - start), offset + start));
            }
            return fields;
        }

        public static Layout Parse(string id, string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var rawLines = SplitLines(text);

            var roots = new List<LayoutNode>();
            var pending = new List<object[]>();   // [depth, name, kind, l, t, w, h, options, line, indent, fields, trailing, terminator]

            for (int i = 0; i < rawLines.Count; i++)
            {
                string raw = rawLines[i].Text;
                if (raw.IndexOf('\t') >= 0)
                    throw new ArgumentException($"{id}: {i + 1}行目にタブがある（空白2つで1段）");
                if (IsSkippable(raw)) continue;

                int spaces = 0;
                while (spaces < raw.Length && raw[spaces] == ' ') spaces++;
                if (spaces % 2 != 0)
                    throw new ArgumentException($"{id}: {i + 1}行目の字下げが奇数（空白2つで1段）");
                int depth = spaces / 2;

                // ⚠️ **`.Trim()` は行末の余りも削る。**⭐ ここで差を取り出しておかないと
                //    `Write` が元の行末の空白を再現できない（普通は空だが、念のため）。
                string trimmedBody = raw.Trim();
                string trailing = raw.Substring(spaces + trimmedBody.Length);

                // ⭐ **`text=` は行の最後まで全部。**⚠️ 空白で切る前に外す
                //    ── 切ってから繋ぎ直すと、二重空白や全角空白が失われる。
                string literal = null;
                string fieldsBody = trimmedBody;
                int textColumn = -1;
                int mark = trimmedBody.IndexOf(TextMark, StringComparison.Ordinal);
                if (mark >= 0)
                {
                    literal = trimmedBody.Substring(mark + TextMark.Length);
                    fieldsBody = trimmedBody.Substring(0, mark);
                    textColumn = spaces + mark + 1;   // ⚠️ TextMark 先頭の区切り空白を飛ばす
                }

                var fields = Tokenize(fieldsBody, spaces);
                if (fields.Count < 6)
                    throw new ArgumentException(
                        $"{id}: {i + 1}行目「{fieldsBody}」── 名前 種類 左 上 幅 高 が要る");

                // ⚠️ **名前に `#` を使わせない。**⭐ 繰り返しの複製が `名前#0` を作るので、
                //    元の名前に `#` があると「読む→書く→読む」が同じ木に戻らない
                //    ── エディタは往復が閉じている形式の上にしか載らない。
                if (fields[0].Text.IndexOf('#') >= 0)
                    throw new ArgumentException($"{id}: {i + 1}行目 名前に # は使えない（繰り返しが使う）");

                var options = new Dictionary<string, string>();
                for (int p = 6; p < fields.Count; p++)
                {
                    int eq = fields[p].Text.IndexOf('=');
                    if (eq <= 0)
                        throw new ArgumentException($"{id}: {i + 1}行目「{fields[p].Text}」は key=value でない");
                    // ⚠️ **後勝ちで黙って通さない。**⭐ 名前の重複は落とすのに
                    //    付け足しの重複を見逃すと、直したつもりの値が効かない
                    string key = fields[p].Text.Substring(0, eq);
                    if (options.ContainsKey(key))
                        throw new ArgumentException($"{id}: {i + 1}行目「{key}=」が2つある");
                    options[key] = fields[p].Text.Substring(eq + 1);
                }
                // ⚠️ 空の `text=` は「書いたのに何も出ない」になる。⭐ 落とす
                if (literal != null)
                {
                    if (literal.Length == 0)
                        throw new ArgumentException($"{id}: {i + 1}行目 text= が空");
                    // ⚠️ 🔴 **`text=` は行末まで飲む。**⭐ 後ろに付け足しを書くと、
                    //    それが**字として画面に出る**（実測 2026-08-22:
                    //    釦に「あきらめる when=!done」と出ていた）。
                    //    ⚠️ 静かに壊れる形なので、ここで落とす。⭐ 直しは `when=` を前へ。
                    foreach (var known in Options)
                    {
                        if (known == "text") continue;
                        if (literal.IndexOf(" " + known + "=", StringComparison.Ordinal) < 0) continue;
                        throw new ArgumentException(
                            $"{id}: {i + 1}行目 text= の後ろに「{known}=」がある"
                            + "（text= は行末まで全部・付け足しは text= より前へ）");
                    }
                    // ⭐ **`\n` だけは行替えとして読む。**⚠️ 骨組みは1部品1行なので、
                    //    これが無いと2行の字（「空き／（自動で埋まる）」）が書けない。
                    //    ⭐ 規約はこれ1つだけ ── 他のエスケープは作らない。
                    options["text"] = literal.Replace("\\n", "\n");
                    // ⚠️ 欄としては「text=」から行末までを**1つ**で持つ（展開前の字のまま）
                    //    ── ここで割ってしまうと、書き出しで元の綴りへ戻せない。
                    fields.Add(new LayoutField(TextMark.Substring(1) + literal, textColumn));
                }

                pending.Add(new object[]
                {
                    depth, fields[0].Text, fields[1].Text,
                    Num(id, i, fields[2].Text), Num(id, i, fields[3].Text),
                    Num(id, i, fields[4].Text), Num(id, i, fields[5].Text),
                    options,
                    i, spaces, fields, trailing, rawLines[i].Terminator,
                });
            }

            // ⭐ 後ろから組む（子が先に要る）。⚠️ 前から作ると子を後付けすることになり、
            //    LayoutNode を書き換え可能にせざるを得なくなる。
            var built = new LayoutNode[pending.Count];
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                int depth = (int)pending[i][0];
                var kids = new List<LayoutNode>();
                for (int j = i + 1; j < pending.Count; j++)
                {
                    int at = (int)pending[j][0];
                    if (at <= depth) break;
                    if (at == depth + 1) kids.Add(built[j]);
                }
                built[i] = new LayoutNode(
                    (string)pending[i][1], (string)pending[i][2],
                    (float)pending[i][3], (float)pending[i][4],
                    (float)pending[i][5], (float)pending[i][6],
                    (Dictionary<string, string>)pending[i][7], kids,
                    (int)pending[i][8], (int)pending[i][9],
                    (List<LayoutField>)pending[i][10], (string)pending[i][11], (string)pending[i][12]);
            }
            for (int i = 0; i < pending.Count; i++)
                if ((int)pending[i][0] == 0) roots.Add(built[i]);

            return new Layout(id, roots, rawLines);
        }

        private static float Num(string id, int line, string text)
        {
            float value;
            if (!float.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out value))
                throw new ArgumentException($"{id}: {line + 1}行目「{text}」が数でない");
            return value;
        }

        // ── 書き出し ────────────────────────────────────

        /// <summary>骨組みを、原文の書式へ組み直す。⭐ これから作る GUI 編集の「往復」の
        /// 出口 ── `Write(Parse(t)) == t`（実物32枚すべて）が閉じていることで保証する。
        ///
        /// ⚠️ 🔴 **原文を丸ごと返す echo ではない。**節点の行は、必ず節点が持つ
        /// <see cref="LayoutNode.RenderLine"/> から組み直す。コメント・空行だけ、
        /// <see cref="Layout.Lines"/> をそのまま通す。
        ///
        /// ⭐ **節点が消えていたら、その行も消える。**⚠️ 逆に増えた節点（`LineNumber` が
        /// -1 ── 原文の行を持たない）は、末尾へ足す。差し込み先を木の形から言い当てる
        /// ことはしない ── それは GUI 編集ツール（このコミットでは作らない）の仕事。
        ///
        /// ⚠️ 🔴 **書き戻してよいのは `Parse` 直後の生の木だけ。**<see cref="Resolve"/> を
        /// 通した木（<see cref="Layout.Resolved"/>）は落とす ── 部品がインライン展開され、
        /// 節点の行番号も失われているので、黙って渡すと原文に無いものが並ぶ。</summary>
        public static string Write(Layout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (layout.Resolved)
                throw new InvalidOperationException(
                    $"{layout.Id}: 解決済みの木は書き戻せない（Resolve は use= を展開し、"
                    + "節点の行番号も失う ── Parse 直後の生の木を渡すこと）");

            var claimed = new Dictionary<int, LayoutNode>();
            var appended = new List<LayoutNode>();
            foreach (var root in layout.Roots) Collect(root, claimed, appended);

            var sb = new StringBuilder();
            for (int i = 0; i < layout.Lines.Count; i++)
            {
                LayoutNode node;
                if (claimed.TryGetValue(i, out node))
                {
                    sb.Append(node.RenderLine());
                }
                else if (IsSkippable(layout.Lines[i].Text))
                {
                    sb.Append(layout.Lines[i].Text).Append(layout.Lines[i].Terminator);
                }
                // ⚠️ else: 元は節点の行だったのに、今の木には無い ── 消えた節点として省く
                //    （コメント・空行でないのに手元の木に居ないなら、消された節点の行）。
            }
            foreach (var node in appended) sb.Append(node.RenderLine());

            return sb.ToString();
        }

        /// <summary>木を辿って、節点を「元の行番号」と「新顔（末尾へ足す）」に仕分ける。</summary>
        private static void Collect(LayoutNode node, Dictionary<int, LayoutNode> claimed, List<LayoutNode> appended)
        {
            if (node.LineNumber >= 0) claimed[node.LineNumber] = node;
            else appended.Add(node);
            foreach (var child in node.Children) Collect(child, claimed, appended);
        }

        // ── 検査（⭐ エンジン不要）────────────────────────

        /// <summary>骨組みの不備を数える。⭐ **`InspectScreens` の静的版。**
        ///
        /// ⚠️ 実物の字幅までは見られません（それは描いてからでないと分からない）。
        /// ⭐ ただし**枠どうしの関係**── 重なり・はみ出し・画面の外・押しどころの大きさ
        /// ── はここで全部落ちます。</summary>
        public static List<string> Faults(Layout layout)
        {
            var problems = new List<string>();
            if (layout == null) { problems.Add("骨組みが無い"); return problems; }

            // ⚠️ **根っこ同士も兄弟。**⭐ ここを見ていなくて、わざと重ねた2つの字が
            //    素通りした（2026-08-22・道具を壊して確かめたときに発覚）。
            Siblings(layout.Id, layout.Id, layout.Roots, TopsOf2(layout.Roots), problems);
            foreach (var root in layout.Roots)
                Walk(layout.Id, root, 0f, 0f, ScreenWidth, ScreenHeight, false, false, problems);

            return problems;
        }

        /// <param name="parentScrolls">**直近の親**が巻物か。⭐ 「親の枠から縦へはみ出し」を
        /// 見逃す唯一の場合。</param>
        /// <param name="insideScroll">**祖先のどこか**に巻物があるか。⭐ 「画面の外」を
        /// 見逃す唯一の場合。
        ///
        /// ⚠️ **この2つを1本の旗で兼ねてはいけない**（2026-08-22 の初版はそうしていた）。
        /// 巻物 → 箱 → 字 の3段になると、箱は巻物ではないので孫の旗が下りてしまい、
        /// **巻物の中なのに「画面の外」と嘘をつく**（実測: `t/a: 画面の外（0,1950 400x40）`）。</param>
        /// <param name="flowTop">親が `flow=down` のとき、詰めた結果の上端。
        /// ⚠️ null なら骨組みに書いてある `上` をそのまま使う。</param>
        private static void Walk(string id, LayoutNode node,
            float parentX, float parentY, float parentW, float parentH,
            bool parentScrolls, bool insideScroll, List<string> problems,
            float? flowTop = null)
        {
            // ⭐ **効く上端はここ1か所で決める。**⚠️ 以降で node.Top を直に読まない
            //    ── 読んだ場所だけ詰める前の数を見て、検査が嘘になる。
            float top = flowTop ?? node.Top;
            bool known = false;
            for (int i = 0; i < Kinds.Length; i++) if (Kinds[i] == node.Kind) { known = true; break; }
            if (!known) problems.Add($"{id}/{node.Name}: 知らない種類「{node.Kind}」");

            float x = parentX + node.Left;
            float y = parentY + top;

            if (node.Width <= 0f || node.Height <= 0f)
                problems.Add($"{id}/{node.Name}: 大きさが 0 以下（{node.Width}x{node.Height}）");

            // ⚠️ 条件の名前が空だと、何で出し分けるのか誰にも分からない
            if (node.Option("when") != null && string.IsNullOrEmpty(WhenOf(node)))
                problems.Add($"{id}/{node.Name}: when= の名前が空");

            // ⚠️ **同じ場所に2つの出所を置かない。**⭐ どちらが勝つかを
            //    描く側の順序が決めることになり、直したのに効かないが生まれる。
            if (node.Option("text") != null && node.Option("bind") != null)
                problems.Add($"{id}/{node.Name}: text= と bind= の両方がある（字の出所は1つ）");

            // ⚠️ 字を出さない種類に text= を書いても**どこにも出ない**
            if (node.Option("text") != null && !IsText(node.Kind) && !IsTappable(node.Kind))
                problems.Add($"{id}/{node.Name}: 「{node.Kind}」は字を出さないのに text= がある");

            // ⚠️ **中を知らないと宣言した枠に、子を書かせない。**
            //    ⭐ 書けるなら host ではなく、普通の入れ物（box）ですむ。
            if (IsHost(node) && node.Children.Count > 0)
                problems.Add($"{id}/{node.Name}: host の中に子がある"
                    + $"（{node.Children.Count}個）── 書けるなら box にする");

            // ⚠️ **絵の印には名前が要る。**⭐ `pic=` か `bind=`（描く側が選ぶ）。
            //    ⚠️ どちらも無いと、**何も出ない四角**が黙って置かれる。
            if (node.Kind == "icon" && node.Option("pic") == null && node.Option("bind") == null)
                problems.Add($"{id}/{node.Name}: icon に pic= も bind= も無い（何の絵か言えていない）");

            // ⚠️ **`flow=` は down しか無い。**⭐ 綴り違いが黙って
            //    「詰めない」に落ちると、重なった画面がそのまま出る。
            if (node.Option("flow") != null && !Flows(node))
                problems.Add($"{id}/{node.Name}: flow=「{node.Option("flow")}」は知らない（down だけ）");

            // ⚠️ 知らない付け足しを黙って無視しない（#5）
            foreach (var pair in node.Options)
            {
                bool listed = false;
                for (int i = 0; i < Options.Length; i++) if (Options[i] == pair.Key) { listed = true; break; }
                if (!listed) problems.Add($"{id}/{node.Name}: 知らない付け足し「{pair.Key}=」");
            }

            // ⚠️ **覆いは画面いっぱいでなければ意味がない。**
            //    ⭐ 隙間があると、そこから後ろが押せる（覆いの目的が消える）。
            if (node.Kind == "veil"
                && (Math.Abs(node.Width - ScreenWidth) > 0.5f
                    || Math.Abs(node.Height - ScreenHeight) > 0.5f
                    || Math.Abs(node.Left) > 0.5f || Math.Abs(node.Top) > 0.5f))
            {
                problems.Add($"{id}/{node.Name}: 覆いが画面いっぱいでない"
                    + $"（{node.Left},{node.Top} {node.Width}x{node.Height}）── 隙間から後ろが押せる");
            }

            // ⚠️ **検査する枠と、実際に描かれる枠を食い違わせない**（#3）。
            //    ⭐ 絵と丸は短いほうの辺で正方形に描かれるので、
            //    「幅984・高40」と書くと 40x40 が描かれるのに検査は 984x40 を見てしまう。
            if ((node.Kind == "pixel" || node.Kind == "round" || node.Kind == "icon")
                && Math.Abs(node.Width - node.Height) > 0.5f)
            {
                problems.Add($"{id}/{node.Name}: {node.Kind} は正方形で描かれる"
                    + $"（{node.Width}x{node.Height} と書いても {Math.Min(node.Width, node.Height)} 角になる）");
            }

            // ⚠️ 親の外へ出ていないか。
            // ⭐ **巻物の中は縦を見ない** ── 縦に溢れることが巻物の役目。
            //    ⚠️ 逆に**横は必ず見る**。横に溢れたものは指が届かない
            //    （巻物は縦にしか動かない）。
            if (parentW > 0f)
            {
                if (node.Left < -0.5f || node.Left + node.Width > parentW + 0.5f)
                {
                    problems.Add($"{id}/{node.Name}: 親の枠から横へはみ出し"
                        + $"（子 左{node.Left} 幅{node.Width} / 親 幅{parentW}）");
                }
            }
            if (parentH > 0f && !parentScrolls)
            {
                if (top < -0.5f || top + node.Height > parentH + 0.5f)
                {
                    problems.Add($"{id}/{node.Name}: 親の枠から縦へはみ出し"
                        + $"（子 上{top} 高{node.Height} / 親 高{parentH}）");
                }
            }

            // ⚠️ 巻物の中は画面より下に在ってよい（動かせば見える）
            bool offScreen = x < -0.5f || x + node.Width > ScreenWidth + 0.5f;
            // ⚠️ ここは `insideScroll`。⭐ 巻物の中なら、何段目でも下に在ってよい
            if (!insideScroll && (y < -0.5f || y + node.Height > ScreenHeight + 0.5f)) offScreen = true;
            if (offScreen)
            {
                problems.Add($"{id}/{node.Name}: 画面の外（{x},{y} {node.Width}x{node.Height}）");
            }

            if (IsTappable(node.Kind) && node.Height < TapHeight - 0.5f)
            {
                problems.Add($"{id}/{node.Name}: 押しどころの高さが {node.Height}"
                    + $"。{TapHeight} 以上にする（指で押せない）");
            }

            Siblings(id, node.Name, node.Children, TopsOf(node, null, null), problems);

            // ⭐ **並びの検査。**`repeat=` を持つ札は、`cols=` 枚が親の幅に収まるか。
            // ⚠️ ここを見ないと「3列で置いたら右端が切れる」が実機まで分からない。
            if (node.Option("repeat") != null)
            {
                int cols = node.Number("cols", 1);
                float gap = node.Number("gap", 0);
                // ⚠️ **左のする分を足す。**⭐ 左を 26 へ寄せて中央に見せている
                //    一覧が多いので、左を無視すると**右端の1列が黙ってはみ出る**
                //    （実測 2026-08-22: 分解の一覧で 4列目が 22px 出ていた）。
                float need = node.Left + cols * node.Width + (cols - 1) * gap;
                if (cols < 1)
                    problems.Add($"{id}/{node.Name}: cols= が {cols}（1以上）");
                else if (parentW > 0f && need > parentW + 0.5f)
                    problems.Add($"{id}/{node.Name}: {cols}列が親の幅に収まらない"
                        + $"（左{node.Left} + 要る {need - node.Left} = {need} / 親 {parentW}）");

                // ⚠️ **巻物の外で繰り返すなら、何段までかを宣言させる**（#7）。
                //    ⭐ 繰り返しの数はデータ次第なので、検査は「何個来るか」を知らない。
                //    宣言が無ければ、増えた日に黙って親からはみ出す。
                if (!parentScrolls)
                {
                    int max = node.Number("max", 0);
                    if (max <= 0)
                    {
                        problems.Add($"{id}/{node.Name}: 巻物の外の繰り返しには max=（上限の個数）が要る");
                    }
                    else if (parentH > 0f)
                    {
                        int rows = (max + cols - 1) / cols;
                        float deep = top + rows * StepOf(node) - gap;
                        if (deep > parentH + 0.5f)
                            problems.Add($"{id}/{node.Name}: max={max} だと親の枠から縦へはみ出す"
                                + $"（要る {deep} / 親 高{parentH}）");
                    }
                }
            }

            // ⚠️ 🔴 **詰める親の中に「入れ替わる2つ」を置かない。**
            //
            // ⭐ 検査は「全部出る」いちばん深い場合で数えます。入れ替わる2つ
            //    （`when=x` と `when=!x`）は**同時には出ない**ので、そこだけ
            //    数えすぎになり、⚠️ **通るはずの画面が落ちる**か、逆に
            //    位置がずれたまま通ります。
            // ⭐ 入れ替わる2つは「変わり種」なので、詰める中でなく
            //    **決め打ちの位置**か**別の骨組み**に置くこと。
            if (Flows(node))
            {
                for (int i = 0; i < node.Children.Count; i++)
                    for (int j = i + 1; j < node.Children.Count; j++)
                        if (Exclusive(node.Children[i], node.Children[j]))
                            problems.Add($"{id}/{node.Name}: 詰める中に入れ替わる2つ"
                                + $"「{node.Children[i].Name}」×「{node.Children[j].Name}」"
                                + "── 決め打ちの位置か、別の骨組みに置く");
            }

            bool scrolls = node.Kind == "scroll";
            // ⭐ **詰めた結果の上端で降りる。**⚠️ `TopsOf` が唯一の出所
            //    ── 描く側と別々に数えたら、検査は別の画面を見ていることになる。
            var tops = TopsOf(node, null, null);
            for (int i = 0; i < node.Children.Count; i++)
                Walk(id, node.Children[i], x, y, node.Width, node.Height,
                    scrolls, insideScroll || scrolls, problems, tops[i]);
        }

        /// <summary>同じ親を持つ部品どうしの見張り。
        /// ⚠️ **根っこの一覧にも掛ける** ── 掛け忘れると、画面の一番外側だけ
        /// 検査が素通りする（2026-08-22 に実際そうなっていた）。</summary>
        private static float[] TopsOf2(IReadOnlyList<LayoutNode> roots)
        {
            var tops = new float[roots.Count];
            for (int i = 0; i < roots.Count; i++) tops[i] = roots[i].Top;
            return tops;
        }

        private static void Siblings(string id, string owner,
            IReadOnlyList<LayoutNode> list, float[] tops, List<string> problems)
        {
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    if (list[i].Name == list[j].Name)
                        problems.Add($"{id}/{owner}: 「{list[i].Name}」が2つある");

                    if (!Overlaps(list[i], tops[i], list[j], tops[j])) continue;
                    // ⭐ 条件で入れ替わる2つは、同時には出ない
                    if (Exclusive(list[i], list[j])) continue;

                    // ⭐ 字どうし。⚠️ 面（card）と字が重なるのは当たり前なので見ない
                    if (IsText(list[i].Kind) && IsText(list[j].Kind))
                    {
                        problems.Add($"{id}/{owner}: 字の重なり"
                            + $"「{list[i].Name}」×「{list[j].Name}」");
                        continue;
                    }

                    // ⭐ **押しどころどうし**（#4）。⚠️ 重なると片方に指が届かない。
                    //    2026-08-22 の初版は字しか見ておらず、釦が2枚重なっても素通りした。
                    if (Tappable(list[i]) && Tappable(list[j]))
                    {
                        problems.Add($"{id}/{owner}: 押しどころの重なり"
                            + $"「{list[i].Name}」×「{list[j].Name}」── 片方に指が届かない");
                    }
                }
            }
        }

        /// <summary>指が触れる部品か。⭐ `button` と、`tap=` / `hold=` を持つ札。
        /// ⚠️ 重なりの検査では長押しも数える ── 重なれば片方に指が届かないのは同じ。</summary>
        private static bool Tappable(LayoutNode node) =>
            IsTappable(node.Kind) || node.Option("tap") != null || node.Option("hold") != null;

        /// <summary>⚠️ 上端は**詰めた結果**を渡すこと。⭐ 骨組みに書いてある `上` で
        /// 比べると、`flow=down` の中は全部が同じ位置に見えて偽の重なりが出る。</summary>
        private static bool Overlaps(LayoutNode a, float aTop, LayoutNode b, float bTop) =>
            !(a.Left + a.Width <= b.Left + 0.5f || b.Left + b.Width <= a.Left + 0.5f
              || aTop + a.Height <= bTop + 0.5f || bTop + b.Height <= aTop + 0.5f);

        /// <summary>⭐ **`use=` を実物に差し替える。**
        ///
        /// ⚠️ 検査も描画も、**差し替えたあとの木**を見なければ意味がない。
        /// ⭐ だから読み込みの直後に1度だけ通す（描く側で毎回やらない）。
        ///
        /// ⚠️ **輪を作らせない** ── `a` が `b` を、`b` が `a` を使うと止まらない。
        /// </summary>
        /// <param name="find">名前 → 骨組み。⚠️ 無ければ null を返すこと。</param>
        public static Layout Resolve(Layout layout, Func<string, Layout> find)
        {
            if (layout == null) return null;
            var seen = new List<string> { layout.Id };
            var roots = new List<LayoutNode>();
            foreach (var node in layout.Roots) roots.Add(Splice(layout.Id, node, find, seen));
            // ⚠️ `resolved: true` ── これで書き出そうとすると Write が断る。
            return new Layout(layout.Id, roots, layout.Lines, true);
        }

        private static LayoutNode Splice(string id, LayoutNode node,
            Func<string, Layout> find, List<string> seen)
        {
            var kids = new List<LayoutNode>();

            string use = node.Option("use");
            if (use != null)
            {
                if (seen.Contains(use))
                    throw new InvalidOperationException(
                        $"{id}/{node.Name}: use= が輪になっている（{string.Join(" → ", seen)} → {use}）");

                var part = find != null ? find(use) : null;
                if (part == null)
                    throw new InvalidOperationException($"{id}/{node.Name}: use=「{use}」が見つからない");

                // ⚠️ **差し込む側の子は、差し込まれる中身の後ろ**に置く。
                //    ⭐ 上に足したいものがあるとき、順番で言えるようにする。
                var deeper = new List<string>(seen) { use };
                foreach (var inner in part.Roots)
                    kids.Add(Rename(node.Name + "-", Splice(use, inner, find, deeper)));
            }

            foreach (var child in node.Children) kids.Add(Splice(id, child, find, seen));

            // ⭐ **この節点自身の出所は保つ。**⚠️ 差し替わるのは「子」（`use=` が差した中身・
            //    `Rename` を通るので下で LineNumber を落とす）だけで、node 自身は
            //    元の原文の行から来ている（Splice はこの Layout の Roots を辿っているだけ）。
            //    ⭐ ここを短いコンストラクタ（LineNumber 既定 -1）のままにすると、
            //    `use=` を1つも使っていない骨組みまで**丸ごと**選べなくなる
            //    （エディタの `data-line` の土台が全部 -1 になるため）。
            return new LayoutNode(node.Name, node.Kind, node.Left, node.Top,
                node.Width, node.Height, node.Options, kids,
                node.LineNumber, node.Indent, node.Fields, node.Trailing, node.Terminator);
        }

        /// <summary>⭐ **差した部品の名前に、差した枠の名前を冠する。**
        ///
        /// ⚠️ 同じ部品を1画面で2度差すと、名前がそのまま重なります
        /// （配合は親札を左右2つ差す）。⭐ web では名前が id になるので、
        /// 重なった時点で**どちらも指し示せなくなる**。
        ///
        /// ⚠️ 冠は**部品の中身すべて**に付ける ── 根だけだと孫が重なる。
        /// ⭐ 読むときの利も大きい（`pa-art` で「左の親の絵」と分かる）。</summary>
        private static LayoutNode Rename(string crown, LayoutNode node)
        {
            var kids = new List<LayoutNode>();
            foreach (var child in node.Children) kids.Add(Rename(crown, child));

            // ⭐ **差し込み口にも冠を付ける。**⚠️ 付けないと、配合の左右2枚が
            //    同じ `bind=art` を持ち、**どちらの親の絵か言えなくなる**。
            // ⭐ 付けると、値を差す側は「どの枠のどの欄か」を1つの名前で受け取れる:
            //    `pfill-name` → 左の親の名前。⚠️ 中身の出し方は1つの関数で済む。
            var options = new Dictionary<string, string>();
            foreach (var pair in node.Options)
            {
                string value = pair.Value;
                // ⚠️ **`hold` も冠を付ける**（2026-08-22 に抜けていた）。
                //    ⭐ `tap` と同じ「押されたら名前で呼ぶ」道なので、
                //    冠が無いと同じ部品を2度差した瞬間に**どちらの長押しか言えなくなる**。
                if (pair.Key == "bind" || pair.Key == "tap"
                    || pair.Key == "hold" || pair.Key == "repeat")
                    value = crown + value;
                // ⚠️ 条件は `!` が先頭に付く。⭐ 冠は名前のほうに付ける
                else if (pair.Key == "when")
                    value = value.Length > 0 && value[0] == '!'
                        ? "!" + crown + value.Substring(1) : crown + value;
                options[pair.Key] = value;
            }

            return new LayoutNode(crown + node.Name, node.Kind, node.Left, node.Top,
                node.Width, node.Height, options, kids);
        }

        /// <summary>不備があれば投げる。⚠️ 起動時に1度呼んで、**黙って壊れた画面を出さない**。</summary>
        public static void Audit(Layout layout)
        {
            var problems = Faults(layout);
            if (problems.Count == 0) return;
            var report = new StringBuilder("骨組みに不備:\n");
            foreach (var line in problems) report.Append("  ").Append(line).Append('\n');
            throw new InvalidOperationException(report.ToString());
        }
    }
}
