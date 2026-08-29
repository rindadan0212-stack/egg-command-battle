using System;
using System.Collections.Generic;
using System.Globalization;
using EggCommand.Core;

namespace EggCommand.Web
{
    /// <summary>木の行を掴んで落としたときの「落とし方」。⚠️ `None` は「そこへは落とせない」
    /// ではなく「まだ決まっていない」。⭐ どれになるかは**当たった要素**が決める
    /// （行の上端・下端の帯＝兄弟として並べ替え、行そのもの＝子にする）── 座標から数えない。
    ///
    /// 🔴 **ここ（Web の名前空間の直下）に在る理由。**`EditPage.razor` の中に持つと、
    /// テストプロジェクトは `.razor` を1枚も取り込まないので
    /// <see cref="LayoutSplice.PlanMove"/> をテストから呼べない（監査 A-5 と同じ穴）。</summary>
    public enum DropSpot { None, Before, Into, After }

    /// <summary>⭐ 「どの行を、どこへ、どれだけ字下げを変えて動かすか」の答え。
    /// ⚠️ <see cref="LayoutSplice.MoveLines"/> に渡す4つの数を1つに束ねただけ ──
    /// 🔴 **本番（`EditPage.MoveNode`）とテストが同じ組み立てを通るようにするための型**
    /// （2026-08-29 監査: この4つの組み立てだけがテストの外に残っていて、
    /// 部分木を忘れる・`+2` を忘れるといった壊し方が実物1,369件に及ぶのに緑のままだった）。</summary>
    public readonly struct MovePlan
    {
        /// <summary>動かす塊の先頭の行（説明コメントを連れて行くならその先頭）。</summary>
        public readonly int First;
        /// <summary>動かす塊の最後の行（部分木の末尾）。</summary>
        public readonly int Last;
        /// <summary>元の行番号でいう「この行の直前」へ挿す。</summary>
        public readonly int Before;
        /// <summary>塊の各行の字下げの増減（親が変わったぶん）。</summary>
        public readonly int IndentDelta;

        public MovePlan(int first, int last, int before, int indentDelta)
        {
            First = first;
            Last = last;
            Before = before;
            IndentDelta = indentDelta;
        }
    }

    /// <summary>骨組みエディタの「構造を直す」ための純関数（段階4a ── 入れ物へ落として
    /// 子として挿す、の土台）。⭐ <see cref="EditAttrs"/> と同じ作法で、**Web に置くが
    /// Core にしか依存しない**（画面の状態も JS も触らない）。
    ///
    /// 🔴 **ここに在る理由（2026-08-29 監査 A-5）。**⚠️ もともとこの5つは
    /// `EditPage.razor` の private として書かれていたが、`EggCommand.Tests.csproj` は
    /// `.razor` を1枚も取り込んでいないため、テスト側が**同じ処理を写して**検査していた。
    /// つまり11本の検査はどれも「写しが正しいこと」しか言えず、**本番の側を壊しても緑のまま**
    /// だった（比較演算子を1つ変えるだけで骨組みが壊れるのに、それを誰も見ていない）。
    /// ⭐ ここへ出して `&lt;Compile Include&gt;` で本番の実体をテストに読ませる ──
    /// `EditAttrs` / `TapCrowns` / `WhenNames` が既に通っている道と同じ。
    ///
    /// ⚠️ 🔴 **Core は1行も触らない。**このエディタは盤も保存も取り消しも
    /// <see cref="Layouts.Write"/> の全文文字列を通るので、「子として挿す」は
    /// 「Write が吐いた正典テキストへ1行スプライスして <see cref="Layouts.Parse"/> し直す」
    /// だけで足りる ── 行番号は Parse が振り直し、取り消しの控えは今までどおり全文なので
    /// 特別扱いが1つも要らない。</summary>
    public static class LayoutSplice
    {
        /// <summary>🔴 **その木は、いま開いている文書のものか。**（2026-08-29 監査 A-1）
        ///
        /// ⚠️ エディタは「読めない字になったら直前の姿へ戻す」控え（`_lastGoodRaw`）を持つが、
        /// これは**文書を切り替えても生き残る**。`box` を直したあと `battle` へ移り、
        /// `battle` の読み込みが失敗すると、控えの `box` の木が `battle` の中身として据わる
        /// ── 盤には box が出るのに利用者は battle だと思い、保存すると
        /// **`battle.txt` が box の中身で丸ごと上書きされる**（作品のデータが消える）。
        ///
        /// 🔴 **この食い違いは字としては正しいので、往復の確かめ（`Write(Parse(x))==x`）でも
        /// ディスクの照合でも捕まらない。**素性そのものを見るしかない ── だから名前を付けて
        /// 1つに置き、戻すときと保存するときの両方が同じ物を通す。</summary>
        public static bool SameDocument(Layout raw, string documentId) =>
            raw != null && raw.Id == documentId;

        /// <summary>⭐ 「子として入れてよい入れ物」の判定 ── **規則の唯一の出所**。
        /// ⚠️ `box`/`card`/`scroll` の3種だけ:
        ///   - `host`（中を知らないと宣言した枠）は、子を書いた瞬間 `HostWithChildren` の不備になる
        ///   - `use=`（部品を差した節点）は、部品の中身と自前の子が混ざる未定義域
        /// ⚠️ JS 側はこの判定を持たない（光らせる先は `EditPage.DropTargetCsv` が配る）。</summary>
        public static bool IsContainer(LayoutNode node) =>
            node != null
            && (node.Kind == "box" || node.Kind == "card" || node.Kind == "scroll")
            && node.Option("use") is null;

        /// <summary>その行の節点までの道（根 → … → その節点）。⚠️ 原文の生の木だけを見る
        /// （差し替え済みの木を見ると、部品の中身に迷い込んで原文に無い節点を親に選ぶ）。</summary>
        public static bool TryPath(IReadOnlyList<LayoutNode> list, int line, List<LayoutNode> path)
        {
            if (list == null || path == null) return false;
            foreach (var n in list)
            {
                path.Add(n);
                if (n.LineNumber == line) return true;
                if (TryPath(n.Children, line, path)) return true;
                path.RemoveAt(path.Count - 1);
            }
            return false;
        }

        /// <summary>部分木（自分と全子孫）の中でいちばん後ろの行。⭐ ここの**直後**が
        /// 「最後の子」として挿す場所 ── 末尾追記と同じ重ね順になるので驚きが無い。
        /// ⚠️ 返すのは必ず**節点の行**なので、部分木の後ろに続くコメント（実物の慣習では
        /// 「次の節点の説明」）の**手前**に入る。</summary>
        public static int SubtreeLastLine(LayoutNode node)
        {
            int max = node.LineNumber;
            foreach (var child in node.Children)
            {
                int deep = SubtreeLastLine(child);
                if (deep > max) max = deep;
            }
            return max;
        }

        /// <summary>親の子の中で重ならない名前。⚠️ 同じ親の中で名前が2つあると
        /// `FaultKind.DuplicateName` の不備になる。</summary>
        public static string UniqueChildName(LayoutNode parent, string baseName)
        {
            var used = new HashSet<string>();
            foreach (var c in parent.Children) used.Add(c.Name);
            if (!used.Contains(baseName)) return baseName;
            int i = 2;
            while (used.Contains(baseName + i.ToString(CultureInfo.InvariantCulture))) i++;
            return baseName + i.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>⭐ `text` の `afterIndex` 行目の**直後**へ `line`（終端込み）を挟んだ字を返す。
        /// ⚠️ 行の数え方は Core の `Layouts.SplitLines` と**同じ規則**（CRLF/CR/LF のどれも
        /// 1つの終端として数える）── private なので呼べず、ここでは「終端の後ろの桁」を
        /// 探すだけの形で写している。⭐ 挟むのは行の境目なので、**他の行は1バイトも動かない**。
        /// ⚠️ `afterIndex` が最終行より後ろなら末尾へ足す（＝今までの追記と同じ）。
        /// ⚠️ `afterIndex` が負なら末尾へ足す（監査 A-4）── 素直に数えると**先頭**に挟まり、
        /// 字下げのある子の行が根の位置に来て骨組みが壊れる。いまは呼び出し側
        /// （`ContainerAt`）が負の行の節点を親に選ばないので届かないが、
        /// **「届かないから安全」に頼らない**（1か所ゆるめば通る道になる）。</summary>
        public static string SpliceAfter(string text, int afterIndex, string line)
        {
            if (afterIndex < 0) return text + line;
            int at = 0, seen = 0;
            while (at < text.Length && seen <= afterIndex)
            {
                char c = text[at];
                if (c != '\r' && c != '\n') { at++; continue; }
                at += (c == '\r' && at + 1 < text.Length && text[at + 1] == '\n') ? 2 : 1;
                seen++;
            }
            return text.Substring(0, at) + line + text.Substring(at);
        }

        // ── 段階4b（2026-08-29）: 木の行を掴んで並べ替える・親を付け替える ──────
        //
        // 🔴 **盤（キャンバス）の掴みは今までどおり「動かすだけ」。**構造を変えるのは
        //    木パネルだけが持つ ── Godot の割り方に倣った。⚠️ Figma 式に「盤で重ねたら
        //    自動で子にする」は採らない: この道具では**位置を1ドット直すたびに構造が
        //    変わりうる**ことになり、直したいのは座標だけなのに親子が入れ替わる事故の
        //    方が、掴んで入れられる便利さより高くつく（骨組みは遊びの土台なので、
        //    気づかず親が変わると `when=`/`flow=`/はみ出し検査の意味まで変わる）。
        //
        // ⭐ 実装は段階4a（<see cref="SpliceAfter"/>）と同じ「正典テキストへの行操作」。
        //    切り取って字下げを付け替えて挿し直すだけなので、**Core は1行も要らない**し、
        //    取り消しの控え（全文）もそのまま効く。

        /// <summary>原文を「終端込みの行」へ割る。⚠️ 数え方は Core の `Layouts.SplitLines` と
        /// **同じ規則**（CRLF / 裸の CR / LF のどれも1つの終端）── private なので呼べず、
        /// ここでは同じ歩き方を写している（<see cref="SpliceAfter"/> と同じ理由）。
        /// ⭐ 各要素は終端まで含むので、そのまま繋ぎ直せば**1バイトも違わず元へ戻る**。
        /// ⚠️ 最後が終端で終わっていれば空の要素は足さない（繋ぎ直しの往復を優先する。
        /// Core の `SplitLines` は空の1行を足すが、こちらは節点の行番号でしか索かないので
        /// 食い違わない ── 行番号は「終端をいくつ跨いだか」で決まり、末尾の扱いに依らない）。</summary>
        public static List<string> SplitKeep(string text)
        {
            var lines = new List<string>();
            if (text == null) return lines;
            int start = 0, at = 0;
            while (at < text.Length)
            {
                char c = text[at];
                if (c != '\r' && c != '\n') { at++; continue; }
                at += (c == '\r' && at + 1 < text.Length && text[at + 1] == '\n') ? 2 : 1;
                lines.Add(text.Substring(start, at - start));
                start = at;
            }
            if (start < text.Length) lines.Add(text.Substring(start));
            return lines;
        }

        /// <summary>行頭の半角空白の数。⚠️ Core の読み込みと同じで**半角空白だけ**を数える
        /// （全角空白や NBSP を削ると、字下げの数と桁がずれる ── Core 側で実害が出た罠）。</summary>
        public static int IndentOf(string line)
        {
            int i = 0;
            while (i < line.Length && line[i] == ' ') i++;
            return i;
        }

        /// <summary>🔴 **その節点と一緒に動く「直前の説明」の始まりの行。**
        ///
        /// ⚠️ 実物の慣習では、節点の直上にあるコメントの塊はその節点の説明
        /// （`box.txt` の `btree` の上に3行ある）── 節点だけ動かすと説明が置き去りになり、
        /// **別の節点の説明として読める字**になってしまう。⭐ だから一緒に動かす。
        ///
        /// 🔴 **ただし機械的に遡ってはいけない。**ファイル冒頭の見出し（画面全体の説明）は
        /// 空行を挟まず最初のルートの直上まで続くので、素直に遡ると**見出しごと引っ越す**。
        ///
        /// ⭐ 規則（これが仕様）:
        ///   ① **字下げが節点と同じコメント行**だけを、上へ連続する限り連れて行く。
        ///   ② 空行・節点の行・字下げの違うコメントに当たったら、そこで打ち切る。
        ///   ③ 🔴 **打ち切らずにファイルの先頭まで遡ってしまったら、それは見出し**なので
        ///      1行も連れて行かない ── 上に節点が1つも無いコメントの塊は、その節点の
        ///      説明ではなく画面ぜんたいの説明。
        ///
        /// ⚠️ 直す前は③でなく「**ルート（字下げ 0）は一律で連れて行かない**」だった。
        /// 見出しは確かに守れるが、**2番目以降のルートは説明を置き去りにする** ──
        /// 置き去りの説明は次の節点にくっついて、その節点の説明として読める字になる
        /// （実物 `encounter.txt` で `level` を動かすと、`level` の「数字の枠を縮めた」が
        /// `track` の説明になる）。⭐ ③なら見出しだけを特別扱いできて、
        /// 挿す側の <see cref="BeforeSlot"/>（最初のルートだけ例外）と鏡になる。</summary>
        public static int LeadCommentStart(IReadOnlyList<string> lines, int nodeLine, int nodeIndent)
        {
            if (lines == null || nodeLine <= 0) return nodeLine;
            int start = nodeLine;
            for (int i = nodeLine - 1; i >= 0; i--)
            {
                string s = lines[i];
                int ind = IndentOf(s);
                if (ind >= s.Length) return start;               // ② 空白だけの行
                char c = s[ind];
                if (c == '\r' || c == '\n') return start;        // ② 空行
                if (c != '#') return start;                      // ② 節点の行 ── ここで確定
                if (ind != nodeIndent) return start;             // ② 字下げが違う
                start = i;                                       // ①
            }
            return nodeLine;                                     // ③ 見出しだった
        }

        /// <summary>その節点の部分木（自分＋全子孫）に、その行が入っているか。
        /// ⚠️ **自分の中へは動かせない**の判定に使う ── 親を自分の子孫にすると、
        /// 字下げの上では「自分の中に自分が居る」字になり、木として読めなくなる。</summary>
        public static bool IsInSubtree(LayoutNode node, int line)
        {
            if (node == null) return false;
            if (node.LineNumber == line) return true;
            foreach (var child in node.Children)
                if (IsInSubtree(child, line)) return true;
            return false;
        }

        /// <summary>🔴 **「X の手前」に挿す行。**⚠️ 素直に「X の説明コメントの先頭」にすると、
        /// **X の説明が、動かしてきた節点の説明として読める字**になる（X は説明を失う）。
        ///
        /// ⚠️ ルート（字下げ 0）は <see cref="LeadCommentStart"/> の規則①で説明を連れて行かない
        /// のに、挿し先だけは説明の**後ろ**（＝ルートの行そのもの）を指していた ── この非対称が
        /// 原因。実物 `home.txt` で `mats` を `ground` の上端へ落とすと、`ground` の説明
        /// （「地面は下の帯の裏まで敷く背景なので…」）が `mats` のものになる。背景5層の
        /// 重ね順を直すのは home.txt でいちばんやりたい操作なので当たりやすい
        /// （2026-08-29 監査 重大2 ── 実物35枚の総当たり12,191件のうち1,742件が該当）。
        ///
        /// ⭐ 規則: **「X の手前」＝「X の直前の兄弟の後ろ」の完全な鏡**にする。
        ///   - 直前の兄弟が居る → その部分木の直後（＝X の説明の**手前**）
        ///   - 居ない（＝最初の子）→ 親の行の直後
        ///   - 居ないうえに親も居ない（＝**最初のルート**）→ X の行そのもの
        ///     ⚠️ ここだけは例外 ── ファイル冒頭の見出しを守るための規則①が要るのはここだけで、
        ///     見出しと「その節点の説明」を字面から見分ける手立てが無い。</summary>
        public static int BeforeSlot(LayoutNode target, LayoutNode parent, IReadOnlyList<LayoutNode> roots)
        {
            var siblings = parent is null ? roots : parent.Children;
            if (siblings is null) return target.LineNumber;
            LayoutNode prev = null;
            bool found = false;
            foreach (var s in siblings)
            {
                if (s.LineNumber == target.LineNumber) { found = true; break; }
                prev = s;
            }
            // ⚠️ 兄弟の中に居ない＝道が食い違っている。今までどおりの位置へ倒す（壊さない側）。
            if (!found) return target.LineNumber;
            if (prev is not null) return SubtreeLastLine(prev) + 1;
            if (parent is not null) return parent.LineNumber + 1;
            return target.LineNumber;
        }

        /// <summary>🔴 **「どの行を、どこへ、どれだけ字下げを変えて動かすか」を決める。**
        /// ⭐ 本番（`EditPage.MoveNode`）とテストが**この1つ**を通る ── 4つの数の組み立てが
        /// ここにしか無いので、壊せば必ず検査が落ちる（2026-08-29 監査: この組み立てだけが
        /// テストの外に残っていて、実物1,369件が黙って壊れる変異も緑のままだった）。
        /// <param name="movedPath">動かす節点までの道（<see cref="TryPath"/> の結果）。</param>
        /// <param name="targetPath">落とし先までの道。</param>
        /// <param name="roots">原文の生の木の根（最初のルートの判定に要る）。</param></summary>
        public static MovePlan PlanMove(IReadOnlyList<string> lines,
            IReadOnlyList<LayoutNode> movedPath, IReadOnlyList<LayoutNode> targetPath,
            IReadOnlyList<LayoutNode> roots, DropSpot where)
        {
            var moved = movedPath[movedPath.Count - 1];
            var target = targetPath[targetPath.Count - 1];
            var targetParent = targetPath.Count >= 2 ? targetPath[targetPath.Count - 2] : null;

            int first = LeadCommentStart(lines, moved.LineNumber, moved.Indent);
            int last = SubtreeLastLine(moved);
            // ⚠️ `Into` だけ「子にする」ので1段深くなる。`Before`/`After` は落とし先と同じ深さ。
            int indent = where == DropSpot.Into ? target.Indent + 2 : target.Indent;
            int before = where == DropSpot.Before
                ? BeforeSlot(target, targetParent, roots)
                : SubtreeLastLine(target) + 1;   // ⭐ 部分木の**後ろ**（子ごと跨ぐ）
            return new MovePlan(first, last, before, indent - moved.Indent);
        }

        /// <summary>⭐ 2つの木の**形**（親の道つきの名前の並び）が同じか。
        ///
        /// ⚠️ 動かした結果これが同じなら、**骨組みは1つも変わっていない** ── 変わったのは
        /// 空行やコメントの位置だけ。⭐ そういう動きは「動かしました」と言わず、取り消しにも
        /// 積まない（2026-08-29 監査: 木の並びが1つも変わらないのに原文だけ変わる動きが
        /// 実物で372件あり、成功と表示され取り消しにも積まれていた）。
        ///
        /// ⚠️ 「後ろの兄弟の手前へ動かす」のように**今いる場所と同じ**指し方をすると、
        /// 塊が間の空行より上へ回り込んで字だけが変わる ── 見た目も遊びも何も変わらない。</summary>
        public static bool SameShape(Layout a, Layout b)
        {
            if (a == null || b == null) return false;
            var one = new List<string>();
            var two = new List<string>();
            Shape(a.Roots, "", one);
            Shape(b.Roots, "", two);
            if (one.Count != two.Count) return false;
            for (int i = 0; i < one.Count; i++)
                if (!string.Equals(one[i], two[i], StringComparison.Ordinal)) return false;
            return true;
        }

        private static void Shape(IReadOnlyList<LayoutNode> list, string parent, List<string> into)
        {
            foreach (var n in list)
            {
                string here = parent + "/" + n.Name;
                into.Add(here);
                Shape(n.Children, here, into);
            }
        }

        /// <summary>⭐ 動かしたあと、その塊の**先頭が何行目に来るか**。
        /// ⚠️ 抜いてから挿すので、挿し先が塊より後ろなら塊のぶんだけ手前へ寄る。
        /// ⭐ 呼び出し側はこれで「動かした節点」を選び直す（名前で探し直さない）。</summary>
        public static int MovedIndex(int first, int last, int before) =>
            before <= first ? before : before - (last - first + 1);

        /// <summary>🔴 **行の塊 `[first..last]` を、元の行番号でいう `before` 行の直前へ移す。**
        /// 各行の字下げは `indentDelta` ぶん増減する（親が変わったぶん）。
        ///
        /// ⭐ 塊は**連続した行**なので、途中に挟まるコメントや空行は黙って一緒に動く
        /// ── 部分木の中の説明が置き去りにならない。
        /// ⭐ 塊の外は1バイトも触らない（行の境目で切って繋ぐだけ）。
        ///
        /// ⚠️ 字下げの付け替えは**一律**なので、行の中の桁も一律にずれる ──
        /// Core の `RenderLine` は元の行の桁を覚えて詰め直すが、一律にずれた桁は
        /// そのまま再現されるので**往復（`Write(Parse(x))==x`）は閉じる**。
        /// 見た目の桁が揃わなくなるのは動かした行だけで、他の行は変わらない。
        ///
        /// ⚠️ 空行には字下げを足さない（空白だけの行を作らない）。
        /// ⚠️ `before` が塊の中を指したら**何もしない**（自分の中へは動かせない）。</summary>
        public static string MoveLines(string text, int first, int last, int before, int indentDelta)
        {
            var lines = SplitKeep(text);
            if (first < 0 || last < first || last >= lines.Count) return text;
            if (before < 0 || before > lines.Count) return text;
            if (before > first && before <= last) return text;   // 自分の中へは動かせない

            string terminator = DominantTerminator(lines);

            var block = new List<string>(last - first + 1);
            for (int i = first; i <= last; i++) block.Add(Reindent(lines[i], indentDelta));

            var rest = new List<string>(lines.Count);
            for (int i = 0; i < lines.Count; i++)
                if (i < first || i > last) rest.Add(lines[i]);

            rest.InsertRange(MovedIndex(first, last, before), block);

            // ⚠️ 終端の無い行が**途中**へ来ると、次の行と融合して節点が1つ黙って消える
            //    （段階3で実際に踏んだ罠 ── `EnsureTrailingTerminator` が同じ事故を防いでいる）。
            var sb = new System.Text.StringBuilder(text.Length + block.Count);
            for (int i = 0; i < rest.Count; i++)
            {
                string s = rest[i];
                // 🔴 **裸の CR で終わる行の直後に、LF で始まる行が来ると1本消える。**
                //    繋いだ字では `"a\r" + "\n"` が CRLF ひとつに読めてしまい、
                //    間にあった空行が無かったことになる（2026-08-29 監査 中2 ──
                //    ファズ 396,695件中 3,373件で消失。純 CRLF の 297,512件では 0件）。
                //    ⚠️ この並びは**原文には存在しえない**（原文で CR の次が LF なら
                //    そもそも CRLF ひとつと数えられる）── 並べ替えが作る組み合わせ。
                //    ⭐ CR を CRLF に閉じてから繋ぐ（中身は変えず、終端だけ揃える）。
                if (i < rest.Count - 1 && s.Length > 0 && s[s.Length - 1] == '\r'
                    && rest[i + 1].Length > 0 && rest[i + 1][0] == '\n')
                    s += "\n";
                sb.Append(s);
                if (i < rest.Count - 1 && !EndsWithTerminator(s)) sb.Append(terminator);
            }
            return sb.ToString();
        }

        /// <summary>字下げを増減した行。⚠️ 空行はそのまま（空白だけの行を作らない）。
        /// ⚠️ 減らしすぎて負にはしない ── 節点の行は必ず親より深いので届かないが、
        /// 塊の中に字下げの浅いコメントが混ざっていることはある。</summary>
        private static string Reindent(string line, int delta)
        {
            if (delta == 0) return line;
            int indent = IndentOf(line);
            if (indent >= line.Length) return line;                       // 空白だけの行
            char c = line[indent];
            if (c == '\r' || c == '\n') return line;                      // 空行
            return new string(' ', Math.Max(0, indent + delta)) + line.Substring(indent);
        }

        private static bool EndsWithTerminator(string s) =>
            s.Length > 0 && (s[s.Length - 1] == '\n' || s[s.Length - 1] == '\r');

        /// <summary>その原文が使っている終端。⚠️ 足す必要が出たときだけ使う
        /// （実物の骨組み35枚は全部 CRLF）。</summary>
        private static string DominantTerminator(List<string> lines)
        {
            foreach (var s in lines)
            {
                if (s.EndsWith("\r\n", StringComparison.Ordinal)) return "\r\n";
                if (s.EndsWith("\n", StringComparison.Ordinal)) return "\n";
                if (s.EndsWith("\r", StringComparison.Ordinal)) return "\r";
            }
            return "\n";
        }
    }
}
