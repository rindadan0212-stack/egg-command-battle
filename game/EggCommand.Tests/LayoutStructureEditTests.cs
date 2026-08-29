using System.Collections.Generic;
using System.IO;
using System.Linq;
using EggCommand.Core;
using EggCommand.Web;
using Xunit;

namespace EggCommand.Tests;

/// <summary>骨組みエディタ 段階3（削除・追加・複製）の往復の安全網。
///
/// ⭐ **Core だけで完結する純テスト**（`dotnet test` は Web（Blazor WASM）を建てない、
/// という既存の約束を守る ── `EditAttrsTests`/`LayoutWriteTests` と同じ作法）。
/// `EditPage.razor` の `RemoveLines`/`NewNode`/`EnsureTrailingTerminator`/
/// `CloneAsNewRoot`/`CloneDescendant` は、すべて「`LayoutNode` を作り直して
/// `Layouts.Write`/`Layouts.Parse` に渡すだけ」という同じ土台に乗っている ──
/// ここではその土台を、実レイアウトでなくテスト内の小さな骨組み文字列で直接組み立てて確かめる。
///
/// ⚠️ どのケースも最後に `Write(Parse(x))==x`（保存ガードと同じ判定）を1回は掛ける。</summary>
public class LayoutStructureEditTests
{
    // ── 1) 削除 ──────────────────────────────────────

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 削除で中央のルートと子孫だけが消え他は1バイトも変わらない(string nl)
    {
        string original =
            $"a box 0 0 100 100{nl}" +
            $"b box 0 120 100 100{nl}" +
            $"  c label 0 0 80 20 text=子{nl}" +
            $"d box 0 240 100 100{nl}";
        var layout = Layouts.Parse("t", original);
        Assert.Equal(3, layout.Roots.Count);
        var b = layout.Roots[1];
        Assert.Single(b.Children);

        // ⭐ `EditPage.RemoveLines` と同じ形: b（と子孫 c）だけを木から外す
        //    （`_raw.Lines` そのものには触らない ── Write が claim されない行を落とす）。
        var kept = new List<LayoutNode> { layout.Roots[0], layout.Roots[2] };
        var edited = new Layout(layout.Id, kept, layout.Lines);
        string written = Layouts.Write(edited);

        Assert.DoesNotContain("b box", written);
        Assert.DoesNotContain("子", written);
        Assert.Contains("a box", written);
        Assert.Contains("d box", written);

        // ⚠️ 消えた行以外は1バイトも変わらない。
        string[] originalLines = original.Replace("\r\n", "\n").Split('\n');
        string[] writtenLines = written.Replace("\r\n", "\n").Split('\n');
        Assert.Equal(originalLines[0], writtenLines[0]);   // a はそのまま
        Assert.Equal(originalLines[3], writtenLines[1]);   // d の行がそのまま2行目に来る

        // 🔴 往復が閉じる（保存ガードと同じ判定）。
        Assert.Equal(written, Layouts.Write(Layouts.Parse("t", written)));
    }

    // ── 2) 追加 ──────────────────────────────────────

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 追加で末尾に1行増え元の行は不変で往復が閉じる(string nl)
    {
        string original = $"a box 0 0 100 100{nl}b box 0 120 100 100{nl}";
        var layout = Layouts.Parse("t", original);

        // ⭐ `EditPage.NewNode` と同じ形: LineNumber=-1・Indent=0・Terminator はファイルに合わせる
        //    （フル引数コンストラクタ ── 短い13引数版は Terminator="\n" 固定なので使わない）。
        var extra = new LayoutNode("c", "box", 390f, 900f, 300f, 120f,
            new Dictionary<string, string>(), new List<LayoutNode>(),
            lineNumber: -1, indent: 0, fields: null, trailing: "", terminator: nl,
            partId: null, partLine: -1);
        var roots = new List<LayoutNode>(layout.Roots) { extra };
        var edited = new Layout(layout.Id, roots, layout.Lines);
        string written = Layouts.Write(edited);

        string[] originalLines = original.Replace("\r\n", "\n").Split('\n');
        string[] writtenLines = written.Replace("\r\n", "\n").Split('\n');
        // ⚠️ 元の行は1つも動かない（増やした節点は原文の行を持たないので末尾へ足す）。
        //    最後の要素（改行の後ろの空要素）は比べない ── 増やした行はその後ろに来る。
        for (int i = 0; i < originalLines.Length - 1; i++)
            Assert.Equal(originalLines[i], writtenLines[i]);
        Assert.Contains("c box 390 900 300 120", written);

        // 🔴 往復が閉じる。
        Assert.Equal(written, Layouts.Write(Layouts.Parse("t", written)));

        var reparsed = Layouts.Parse("t", written);
        Assert.Equal(3, reparsed.Roots.Count);
        Assert.Equal("c", reparsed.Roots[2].Name);
    }

    // ── 3) 追加（終端ガード）── 融合の再現とガードの効き（回帰防止） ─────

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 末尾に改行が無いとガード無しでは前の行に融合し節点が消える(string nl)
    {
        // ⚠️ わざと壊す: 最終行（コメント・改行なし）に、ガード無しで -1 節点を足す。
        string original = $"a box 0 0 100 100{nl}# trailing note";
        var layout = Layouts.Parse("t", original);
        Assert.Single(layout.Roots);
        int lastIndex = layout.Lines.Count - 1;
        Assert.Equal("", layout.Lines[lastIndex].Terminator);   // 前提: 末尾行に改行が無い

        var extra = new LayoutNode("c", "box", 0f, 0f, 10f, 10f,
            new Dictionary<string, string>(), new List<LayoutNode>(),
            lineNumber: -1, indent: 0, fields: null, trailing: "", terminator: nl,
            partId: null, partLine: -1);
        var edited = new Layout(layout.Id, new List<LayoutNode> { layout.Roots[0], extra }, layout.Lines);
        string written = Layouts.Write(edited);

        // 🔴 融合している：c はコメント行の続きとして飲み込まれ、独立した行として現れない。
        Assert.Contains("# trailing notec box 0 0 10 10", written);
        string[] writtenLines = written.Replace("\r\n", "\n").Split('\n');
        Assert.DoesNotContain("c box 0 0 10 10", writtenLines);

        // ⚠️ しかも往復判定（保存ガードと同じ判定）はすり抜ける ── Parse が融合行を
        //    1つのコメント行と読み、Write が同じ融合をそのまま再現する。c は消えたまま。
        var reparsed = Layouts.Parse("t", written);
        Assert.Single(reparsed.Roots);   // c がどこにも居ない（サイレント消失）
        Assert.Equal(written, Layouts.Write(reparsed));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void EnsureTrailingTerminator相当の手当てのあとは融合せず節点として残る(string nl)
    {
        string original = $"a box 0 0 100 100{nl}# trailing note";
        var layout = Layouts.Parse("t", original);
        int lastIndex = layout.Lines.Count - 1;
        Assert.Equal("", layout.Lines[lastIndex].Terminator);   // 前提: 末尾行に改行が無い

        // ⭐ `EditPage.EnsureTrailingTerminator` と同じ手当て: 最終行（ここでは skippable
        //    ── コメント）の Terminator を、文書の終端で補ってから追記する
        //    （節点行なら `ReplaceLine` で作り直す ── 下の #7 系のテストと同じ土台）。
        var lines = new List<RawLine>(layout.Lines);
        lines[lastIndex] = new RawLine(lines[lastIndex].Text, nl);
        var fixedLayout = new Layout(layout.Id, layout.Roots, lines);

        var extra = new LayoutNode("c", "box", 0f, 0f, 10f, 10f,
            new Dictionary<string, string>(), new List<LayoutNode>(),
            lineNumber: -1, indent: 0, fields: null, trailing: "", terminator: nl,
            partId: null, partLine: -1);
        var edited = new Layout(fixedLayout.Id,
            new List<LayoutNode> { fixedLayout.Roots[0], extra }, fixedLayout.Lines);
        string written = Layouts.Write(edited);

        // 🔴 融合しない（別の行になる）。
        string[] writtenLines = written.Replace("\r\n", "\n").Split('\n');
        Assert.Contains("# trailing note", writtenLines);
        Assert.Contains("c box 0 0 10 10", writtenLines);
        Assert.DoesNotContain("notec", written);

        // 🔴 往復が閉じ、しかも c は本物の節点として読み直せる（コメントに飲まれない）。
        var reparsed = Layouts.Parse("t", written);
        Assert.Equal(2, reparsed.Roots.Count);
        Assert.Equal("c", reparsed.Roots[1].Name);
        Assert.Equal(written, Layouts.Write(reparsed));
    }

    // ── 4) 複製（subtree） ────────────────────────────

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 複製した部分木は親子の入れ子として読め名前も衝突しない(string nl)
    {
        string original =
            $"a box 0 0 200 200{nl}" +
            $"  x label 10 10 80 20 text=一{nl}" +
            $"  y label 10 40 80 20 text=二{nl}";
        var layout = Layouts.Parse("t", original);
        var origin = layout.Roots[0];   // a（子 x, y を持つ）
        Assert.Equal(2, origin.Children.Count);

        // ⭐ `EditPage.CloneAsNewRoot`/`CloneDescendant` と同じ形。
        //    親（複製の根）: 名前を一意化(a2)・絶対座標(0,0)+オフセット(16,16)・indent0。
        //    子: 名前はそのまま（部分木内で既に一意）・left/top はそのまま（相対は不変）、
        //    indent は元の (子孫.Indent − 選択節点.Indent) で相対の深さを保つ。
        var cx = new LayoutNode(origin.Children[0].Name, origin.Children[0].Kind,
            origin.Children[0].Left, origin.Children[0].Top, origin.Children[0].Width, origin.Children[0].Height,
            new Dictionary<string, string>(origin.Children[0].Options), new List<LayoutNode>(),
            -1, origin.Children[0].Indent - origin.Indent, null, "", nl, null, -1);
        var cy = new LayoutNode(origin.Children[1].Name, origin.Children[1].Kind,
            origin.Children[1].Left, origin.Children[1].Top, origin.Children[1].Width, origin.Children[1].Height,
            new Dictionary<string, string>(origin.Children[1].Options), new List<LayoutNode>(),
            -1, origin.Children[1].Indent - origin.Indent, null, "", nl, null, -1);
        var cloneRoot = new LayoutNode("a2", origin.Kind, origin.Left + 16f, origin.Top + 16f,
            origin.Width, origin.Height,
            new Dictionary<string, string>(origin.Options), new List<LayoutNode> { cx, cy },
            -1, 0, null, "", nl, null, -1);

        var roots = new List<LayoutNode>(layout.Roots) { cloneRoot };
        var edited = new Layout(layout.Id, roots, layout.Lines);
        string written = Layouts.Write(edited);

        // 🔴 往復が閉じる。
        Assert.Equal(written, Layouts.Write(Layouts.Parse("t", written)));

        // 🔴 複製側も、読み直すと「親1・子2」の入れ子になる（子が親の下にぶら下がる）・
        //    名前が衝突しない（a と a2 は別の名前）。
        var reparsed = Layouts.Parse("t", written);
        Assert.Equal(2, reparsed.Roots.Count);   // a, a2
        var a2 = reparsed.Roots.Single(r => r.Name == "a2");
        Assert.Equal(2, a2.Children.Count);
        Assert.Equal("x", a2.Children[0].Name);
        Assert.Equal("y", a2.Children[1].Name);
        Assert.Equal(16f, a2.Left);
        Assert.Equal(16f, a2.Top);
        // ⚠️ 子は親からの相対座標のまま（絶対座標は自動でついてくる ── 描く側の仕事）。
        Assert.Equal(10f, a2.Children[0].Left);
        Assert.Equal(10f, a2.Children[0].Top);
        Assert.Equal("一", a2.Children[0].Option("text"));
        Assert.Equal("二", a2.Children[1].Option("text"));
        // ⚠️ 元の a は無傷のまま。
        var a = reparsed.Roots.Single(r => r.Name == "a");
        Assert.Equal(2, a.Children.Count);
    }

    // ── 5) 段階4a: 入れ物へ落として「子」として挿す ─────────────
    //
    // 🔴 **Core は1行も触らない。**⭐ `EditPage.InsertChildAt` は
    //    「`Layouts.Write` が吐いた正典テキストへ1行スプライスして `Parse` し直す」
    //    だけ ── 行番号は Parse が振り直し、取り消しの控えは今までどおり全文なので
    //    特別扱いが要らない。ここではその土台（スプライス＋往復）を直接確かめる。
    // 🔴 **呼ぶのは本番の実体**（`EggCommand.Web.LayoutSplice` ── csproj で直接
    //    コンパイルしている）。⚠️ 2026-08-29 の監査までは、`.razor` がこの csproj に
    //    載らないせいで**同じ処理の写し**をここに置いて検査していた ── つまり
    //    「写しが正しいこと」しか言えず、**本番側の比較演算子を1つ変えても緑のまま**
    //    だった。⭐ 骨組み（＝作品のデータ）を壊す種類なので、実体を呼ぶ形に直した。

    private static string SpliceAfter(string text, int afterIndex, string line) =>
        LayoutSplice.SpliceAfter(text, afterIndex, line);

    private static int SubtreeLastLine(LayoutNode node) => LayoutSplice.SubtreeLastLine(node);

    /// <summary>⭐ `EditPage.NewNode` と同じ形の1行を作る（`Fields` が空なので
    /// `RenderLine` は「字下げ＋1個空白区切り」で書く）。</summary>
    private static string ChildLine(string name, string kind, float left, float top,
        float width, float height, int indent, string nl,
        Dictionary<string, string>? options = null) =>
        new LayoutNode(name, kind, left, top, width, height,
            options ?? new Dictionary<string, string>(), new List<LayoutNode>(),
            lineNumber: -1, indent: indent, fields: null, trailing: "", terminator: nl,
            partId: null, partLine: -1).RenderLine();

    /// <summary>🔴 **行の数え方が `Layouts.Parse` と一致している**ことの杭。
    /// ⚠️ ここがずれると、挿す位置が1行ずれて別の親の子になる（静かに壊れる形）。</summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void スプライスの行の数え方はParseと一致する(string nl)
    {
        string original =
            $"# 説明の1行目{nl}" +
            $"a box 0 0 200 200{nl}" +
            $"  x label 10 10 80 20 text=一{nl}" +
            $"b box 0 240 100 100{nl}";
        var layout = Layouts.Parse("t", original);

        // ⚠️ Parse が言う行番号（0基準）と、スプライスの数え方が同じ物を指すこと。
        Assert.Equal(1, layout.Roots[0].LineNumber);          // a
        Assert.Equal(2, layout.Roots[0].Children[0].LineNumber);   // x
        Assert.Equal(3, layout.Roots[1].LineNumber);          // b

        // ⭐ 「a の部分木の最後（＝x の行 2）の直後」へ挟むと、b の行の手前に入る。
        string spliced = SpliceAfter(original, SubtreeLastLine(layout.Roots[0]),
            ChildLine("y", "label", 10f, 40f, 80f, 20f, 2, nl));
        string[] lines = spliced.Replace("\r\n", "\n").Split('\n');
        Assert.Equal("  y label 10 40 80 20", lines[3]);
        Assert.Equal("b box 0 240 100 100", lines[4]);
    }

    /// <summary>🔴 **別の文書の木を、いま開いている文書として扱わない**（監査 A-1）。
    ///
    /// ⚠️ この食い違いが起きると、保存で**別のファイルの中身に丸ごと上書きされる**。
    /// しかも字としては正しいので、往復の確かめ（`Write(Parse(x))==x`）もディスクの照合も
    /// 素通りする ── 素性を見る以外に捕まえ方が無い。
    /// ⚠️ ここで固定するのは**規則そのもの**（`EditPage` の巻き戻しと保存の両方が
    /// これを通す）── 画面の配線は `.razor` がこの csproj に載らないので検査できない。</summary>
    [Fact]
    public void 別の文書の木は同じ文書とみなさない()
    {
        var box = Layouts.Parse("box", "a box 0 0 100 100\n");
        var battle = Layouts.Parse("battle", "a box 0 0 100 100\n");

        Assert.True(LayoutSplice.SameDocument(box, "box"));
        // 🔴 中身が1バイトも違わなくても、素性が違えば違う（ここが要点）。
        Assert.False(LayoutSplice.SameDocument(box, "battle"));
        Assert.True(LayoutSplice.SameDocument(battle, "battle"));
        // ⚠️ 木が無いときも「同じ」と言わない（戻り先が無いのに戻さない）。
        Assert.False(LayoutSplice.SameDocument(null, "box"));
    }

    /// <summary>🔴 **行番号が負なら末尾へ足す**（2026-08-29 監査 A-4）。
    ///
    /// ⚠️ 素直に数えると `afterIndex = -1` は「0行目の手前」＝**ファイルの先頭**に挟まる。
    /// 子として挿す字は字下げを持つので、先頭に来ると「字下げが飛んでいる」で読めなくなり、
    /// 骨組みが壊れる。⭐ いまは呼び出し側（`ContainerAt`）が `LineNumber &lt; 0` の節点を
    /// 親に選ばないので届かないが、**「届かないから安全」に頼らない** ── 親の選び方が
    /// 1か所ゆるむだけで通る道になるので、`SpliceAfter` 自身が塞ぐ。</summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 行番号が負なら先頭でなく末尾へ足す(string nl)
    {
        string original = $"a box 0 0 200 200{nl}b box 0 240 100 100{nl}";
        string line = ChildLine("y", "label", 10f, 40f, 80f, 20f, 2, nl);

        string spliced = SpliceAfter(original, -1, line);

        // ⭐ 先頭ではなく末尾（元の字がそのまま頭に残っている）。
        Assert.StartsWith(original, spliced, System.StringComparison.Ordinal);
        Assert.Equal(original + line, spliced);
        // ⚠️ 先頭に挟まっていたら、字下げのある行が根の位置に来て読めない。
        Assert.NotEqual(line + original, spliced);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 子として挿しても他の行とコメントは1バイトも変わらない(string nl)
    {
        // ⚠️ 実物の作法どおり、節点の上・部分木の後ろ・行末にコメントを散らしておく。
        string original =
            $"# 画面の説明（ファイルの頭）{nl}" +
            $"a box 0 0 200 200{nl}" +
            $"  # ⭐ 中の字の説明{nl}" +
            $"  x label 10 10 80 20 text=一{nl}" +
            $"{nl}" +
            $"# ⚠️ 次の節点の説明（部分木の後ろ）{nl}" +
            $"b box 0 240 100 100{nl}";
        var layout = Layouts.Parse("t", original);
        var a = layout.Roots[0];

        string spliced = SpliceAfter(Layouts.Write(layout), SubtreeLastLine(a),
            ChildLine("y", "label", 10f, 40f, 80f, 20f, a.Indent + 2, nl));

        // 🔴 元の行は**1本残らず・同じ順で**残っている（コメント・空行を含む）。
        string[] before = original.Replace("\r\n", "\n").Split('\n');
        string[] after = spliced.Replace("\r\n", "\n").Split('\n');
        Assert.Equal(before.Length + 1, after.Length);
        var kept = new List<string>(after);
        kept.RemoveAt(4);   // ⭐ 挿した1行（x の直後）を抜くと、元と完全に一致する
        Assert.Equal(before, kept.ToArray());

        // 🔴 部分木の後ろのコメントは「次の節点の説明」── その**手前**に入る。
        Assert.Equal("  y label 10 40 80 20", after[4]);
        Assert.Equal("", after[5]);
        Assert.Equal("# ⚠️ 次の節点の説明（部分木の後ろ）", after[6]);

        // 🔴 往復が閉じる（保存ガードと同じ判定）。
        var reparsed = Layouts.Parse("t", spliced);
        Assert.Equal(spliced, Layouts.Write(reparsed));

        // 🔴 読み直すと a の子が2つ（x, y）── ルートは増えていない。
        Assert.Equal(2, reparsed.Roots.Count);
        var a2 = reparsed.Roots[0];
        Assert.Equal(2, a2.Children.Count);
        Assert.Equal("x", a2.Children[0].Name);
        Assert.Equal("y", a2.Children[1].Name);
        Assert.Equal(10f, a2.Children[1].Left);
        Assert.Equal(40f, a2.Children[1].Top);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 深い部分木の直後に入り後ろの行番号が繰り上がる(string nl)
    {
        string original =
            $"a box 0 0 200 200{nl}" +
            $"  p box 0 0 180 180{nl}" +
            $"    q box 0 0 160 160{nl}" +
            $"      r label 0 0 100 20 text=奥{nl}" +
            $"b box 0 240 100 100{nl}";
        var layout = Layouts.Parse("t", original);
        var a = layout.Roots[0];
        Assert.Equal(4, layout.Roots[1].LineNumber);   // b は挿す前は4行目

        // ⭐ a に落とす ＝ a の部分木の最後（r＝3行目）の直後へ。⚠️ 「最後の子」なので、
        //    深い子孫（q/r）より後ろに入る ── 末尾追記と同じ z 順。
        int after = SubtreeLastLine(a);
        Assert.Equal(3, after);
        string spliced = SpliceAfter(Layouts.Write(layout), after,
            ChildLine("y", "label", 8f, 8f, 80f, 20f, a.Indent + 2, nl));

        var reparsed = Layouts.Parse("t", spliced);
        // 🔴 挿した行は決定的に「部分木の最後＋1」── `SelectOnly(after + 1)` の根拠。
        var y = reparsed.Roots[0].Children[1];
        Assert.Equal("y", y.Name);
        Assert.Equal(after + 1, y.LineNumber);
        // 🔴 後ろの行番号は1つ繰り上がる（b は 4 → 5）。
        Assert.Equal(5, reparsed.Roots[1].LineNumber);
        Assert.Equal("b", reparsed.Roots[1].Name);
        // ⚠️ 深い子孫はそのまま（p の子 q、q の子 r）。
        var p = reparsed.Roots[0].Children[0];
        Assert.Equal("p", p.Name);
        Assert.Equal("q", p.Children[0].Name);
        Assert.Equal("r", p.Children[0].Children[0].Name);
        Assert.Equal(spliced, Layouts.Write(reparsed));
    }

    /// <summary>⚠️ `EditPage.IsContainer` と同じ規則の写し ── 入れ物は `box`/`card`/`scroll`
    /// で、`use=`（部品を差した節点）は除く。`host` は種類の時点で外れる。</summary>
    private static bool IsContainer(LayoutNode n) => LayoutSplice.IsContainer(n);

    [Fact]
    public void 入れ物はboxとcardと巻物だけでhostとuseは親にならない()
    {
        var layout = Layouts.Parse("t",
            "a box 0 0 100 100\n" +
            "b card 0 120 100 100\n" +
            "c scroll 0 240 100 100 content=400\n" +
            "d host 0 360 100 100\n" +
            "e card 0 480 100 100 use=cell\n" +
            "f label 0 600 100 40 text=字\n");
        var by = new Dictionary<string, LayoutNode>();
        foreach (var r in layout.Roots) by[r.Name] = r;

        Assert.True(IsContainer(by["a"]));    // box
        Assert.True(IsContainer(by["b"]));    // card
        Assert.True(IsContainer(by["c"]));    // scroll
        // 🔴 host は子を書いた瞬間 `HostWithChildren` の不備になる。
        Assert.False(IsContainer(by["d"]));
        // 🔴 use= は部品の中身と自前の子が混ざる未定義域。
        Assert.False(IsContainer(by["e"]));
        Assert.False(IsContainer(by["f"]));   // 入れ物ではない種類

        // ⚠️ 実際に host へ子を書くと不備になる（除外の根拠を実物で示す）。
        var bad = Layouts.Parse("t", "d host 0 0 100 100\n  z label 0 0 50 20 text=あ\n");
        Assert.Contains(Layouts.Inspect(bad), f => f.Kind == FaultKind.HostWithChildren);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 詰める枠の子は上が0で入る(string nl)
    {
        // ⚠️ `flow=down` の親では、子の「上」は**上に空ける隙間**の意味に変わる
        //    ── 落とした高さをそのまま書くと大きく空く。⭐ 0 に丸める。
        string original =
            $"a box 0 0 200 400 flow=down{nl}" +
            $"  x label 0 0 180 40 text=一{nl}";
        var layout = Layouts.Parse("t", original);
        var a = layout.Roots[0];
        Assert.Equal("down", a.Option("flow"));

        float droppedTop = 260f;   // 落とした位置（そのまま書くと 260 の隙間になる）
        float top = a.Option("flow") == "down" ? 0f : droppedTop;
        string spliced = SpliceAfter(Layouts.Write(layout), SubtreeLastLine(a),
            ChildLine("y", "label", 0f, top, 180f, 40f, a.Indent + 2, nl));

        var reparsed = Layouts.Parse("t", spliced);
        var y = reparsed.Roots[0].Children[1];
        Assert.Equal("y", y.Name);
        Assert.Equal(0f, y.Top);
        Assert.Equal(spliced, Layouts.Write(reparsed));
        // ⚠️ 詰めた結果として実際に下へ並ぶ（不備にならない）。
        Assert.Equal(new List<string>(), Layouts.Faults(reparsed));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 兄弟と名前が重ならない(string nl)
    {
        // ⚠️ `Faults.DuplicateName` は「同じ親の中」で見る ── 比べる相手は兄弟だけ
        //    （別の親に同じ名前が居ても不備ではない ── b の下にも `label` を置いてある）。
        string original =
            $"a box 0 0 200 200{nl}" +
            $"  label label 10 10 80 20 text=一{nl}" +
            $"b box 0 240 100 100{nl}" +
            $"  label label 0 0 80 20 text=別の親{nl}";
        var layout = Layouts.Parse("t", original);
        var a = layout.Roots[0];

        var used = new HashSet<string>();
        foreach (var c in a.Children) used.Add(c.Name);
        string name = "label";
        if (used.Contains(name)) { int i = 2; while (used.Contains("label" + i)) i++; name = "label" + i; }
        Assert.Equal("label2", name);   // ⭐ 兄弟に label が居るので label2

        string spliced = SpliceAfter(Layouts.Write(layout), SubtreeLastLine(a),
            ChildLine(name, "label", 10f, 40f, 80f, 20f, a.Indent + 2, nl,
                new Dictionary<string, string> { ["text"] = "字" }));
        var reparsed = Layouts.Parse("t", spliced);

        // 🔴 名前の重なりの不備が出ない（別の親の同名 `label` は元から不備でない）。
        Assert.DoesNotContain(Layouts.Inspect(reparsed), f => f.Kind == FaultKind.DuplicateName);
        Assert.Equal("label2", reparsed.Roots[0].Children[1].Name);
        Assert.Equal(spliced, Layouts.Write(reparsed));
    }

    // ── 4) 段階4b: 木の行を掴んで並べ替える・親を付け替える ──────────
    //
    // ⭐ 呼ぶのは**本番の実体**（`EggCommand.Web.LayoutSplice`）── 写しを検査しない
    //    （2026-08-29 監査 A-5: 写しを見ていたので、本番を壊してもここは緑のままだった）。

    /// <summary>⭐ `EditPage.MoveNode` と同じ手順 ──「正典を書き出す → 計画を立てる →
    /// 動かす」。🔴 **計画（塊の範囲・挿し先・字下げの増減）は本番の
    /// <see cref="LayoutSplice.PlanMove"/> がただ1つ持つ**ので、ここで組み立てるのは
    /// 「どの節点を、どこへ、どう」だけ。
    ///
    /// ⚠️ 2026-08-29 の監査までは、この4つの数を**ここで写して**組んでいた ── そのため
    /// 本番側で「部分木を跨がずに落とし先の次の行へ挿す」「`Into` の `+2` を忘れる」と
    /// いった壊し方をしても検査は緑のままだった（実物では1,369件／1,319件が黙って壊れる）。
    /// 往復（`Write(Parse(x))==x`）も行の集合も通ってしまうので、**組み立てを共有する
    /// 以外に捕まえる手が無い**。</summary>
    private static string MoveInto(Layout layout, string movedName, string targetName, string where)
    {
        var moved = FindByName(layout.Roots, movedName);
        var target = FindByName(layout.Roots, targetName);
        Assert.NotNull(moved);
        Assert.NotNull(target);

        // ⚠️ 道を辿るのも本番と同じ（`TryPath` ── 生の木だけを見る）。
        var movedPath = new List<LayoutNode>();
        Assert.True(LayoutSplice.TryPath(layout.Roots, moved!.LineNumber, movedPath));
        var targetPath = new List<LayoutNode>();
        Assert.True(LayoutSplice.TryPath(layout.Roots, target!.LineNumber, targetPath));

        var spot = where switch
        {
            "into" => DropSpot.Into,
            "before" => DropSpot.Before,
            _ => DropSpot.After,
        };

        string text = Layouts.Write(layout);
        var lines = LayoutSplice.SplitKeep(text);
        var plan = LayoutSplice.PlanMove(lines, movedPath, targetPath, layout.Roots, spot);
        return LayoutSplice.MoveLines(text, plan.First, plan.Last, plan.Before, plan.IndentDelta);
    }

    private static LayoutNode? FindByName(IReadOnlyList<LayoutNode> list, string name)
    {
        foreach (var n in list)
        {
            if (n.Name == name) return n;
            var deep = FindByName(n.Children, name);
            if (deep is not null) return deep;
        }
        return null;
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 兄弟の間へ並べ替えても中身は変わらず往復が閉じる(string nl)
    {
        string original =
            $"a box 0 0 100 100{nl}" +
            $"b box 0 120 100 100{nl}" +
            $"c box 0 240 100 100{nl}";
        var layout = Layouts.Parse("t", original);

        // ⭐ c を a の手前へ（＝いちばん上の根にする）。
        string moved = MoveInto(layout, "c", "a", "before");
        var reparsed = Layouts.Parse("t", moved);
        Assert.Equal(new[] { "c", "a", "b" }, reparsed.Roots.Select(n => n.Name).ToArray());
        // 🔴 往復が閉じる（保存ガードと同じ判定）。
        Assert.Equal(moved, Layouts.Write(reparsed));
        // ⚠️ 行の中身は1バイトも変わらない（並びが変わっただけ）。
        var beforeSet = new List<string>(original.Replace("\r\n", "\n").Split('\n'));
        var afterSet = new List<string>(moved.Replace("\r\n", "\n").Split('\n'));
        beforeSet.Sort(StringComparer.Ordinal);
        afterSet.Sort(StringComparer.Ordinal);
        Assert.Equal(beforeSet, afterSet);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 別の入れ物の子にすると字下げが付き替わり深い部分木ごと動く(string nl)
    {
        string original =
            $"a box 0 0 200 200{nl}" +
            $"  x label 10 10 80 20 text=元の子{nl}" +
            $"b box 0 240 200 200{nl}" +
            $"  p box 0 0 180 180{nl}" +
            $"    q label 0 0 100 20 text=奥{nl}";
        var layout = Layouts.Parse("t", original);

        // ⭐ 根の a（子 x を持つ）を、深い所の p の中へ ── 字下げが **0 → 4** へ増え、
        //    子の x も **2 → 6** へ一律に増える（増える向きの付け替え）。
        string moved = MoveInto(layout, "a", "p", "into");
        var reparsed = Layouts.Parse("t", moved);

        // ⚠️ 根は b だけになる（a が中へ入った）。
        Assert.Single(reparsed.Roots);
        Assert.Equal("b", reparsed.Roots[0].Name);
        var p2 = reparsed.Roots[0].Children[0];
        Assert.Equal(new[] { "q", "a" }, p2.Children.Select(n => n.Name).ToArray());
        // 🔴 深い子孫がそのまま付いてくる。
        Assert.Equal("x", p2.Children[1].Children[0].Name);
        Assert.Equal("元の子", p2.Children[1].Children[0].Option("text"));
        // 🔴 字下げが一律に増えている（本番の付け替えを見ている ── 写しではない）。
        Assert.Contains($"{nl}    a box 0 0 200 200{nl}", moved);
        Assert.Contains($"{nl}      x label 10 10 80 20 text=元の子", moved);
        Assert.Equal(moved, Layouts.Write(reparsed));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 深い所から根へ出すと字下げが減り往復が閉じる(string nl)
    {
        string original =
            $"a box 0 0 200 200{nl}" +
            $"  p box 0 0 180 180{nl}" +
            $"    q label 0 0 100 20 text=奥{nl}" +
            $"b box 0 240 100 100{nl}";
        var layout = Layouts.Parse("t", original);

        // ⭐ p を b の後ろ（＝根）へ。字下げは 2 → 0、子の q は 4 → 2 へ一律に減る。
        string moved = MoveInto(layout, "p", "b", "after");
        var reparsed = Layouts.Parse("t", moved);

        Assert.Equal(new[] { "a", "b", "p" }, reparsed.Roots.Select(n => n.Name).ToArray());
        Assert.Empty(reparsed.Roots[0].Children);
        Assert.Equal("q", reparsed.Roots[2].Children[0].Name);
        // 🔴 字下げが一律にずれても往復は閉じる（`RenderLine` は元の桁を覚えて詰め直すが、
        //    一律にずれた桁はそのまま再現される ── だから保存ガードを通る）。
        Assert.Equal(moved, Layouts.Write(reparsed));
        Assert.Contains($"{nl}p box 0 0 180 180{nl}", moved);
        Assert.Contains($"{nl}  q label 0 0 100 20 text=奥", moved);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 子の説明コメントは一緒に動きファイルの見出しは残る(string nl)
    {
        // ⚠️ 実物の作法（`box.txt` の `btree` の上に、同じ字下げの説明が3行）。
        string original =
            $"# 画面の説明（ファイルの頭）{nl}" +
            $"# ⚠️ 見出しは空行を挟まず最初の根まで続く{nl}" +
            $"a box 0 0 200 200{nl}" +
            $"  x label 10 10 80 20 text=一{nl}" +
            $"  # ⭐ y の説明その1{nl}" +
            $"  # ⭐ y の説明その2{nl}" +
            $"  y label 10 40 80 20 text=二{nl}" +
            $"b box 0 240 200 200{nl}";
        var layout = Layouts.Parse("t", original);

        // ⭐ y を b の中へ ── 直上の**同じ字下げのコメント2行**が一緒に動く。
        string moved = MoveInto(layout, "y", "b", "into");
        string[] after = moved.Replace("\r\n", "\n").Split('\n');

        // 🔴 ファイルの見出しは動かない（先頭2行のまま）。
        Assert.Equal("# 画面の説明（ファイルの頭）", after[0]);
        Assert.Equal("# ⚠️ 見出しは空行を挟まず最初の根まで続く", after[1]);
        // 🔴 説明2行が y に付いて b の中へ移った（字下げもそのまま2＝b の子）。
        Assert.Equal("a box 0 0 200 200", after[2]);
        Assert.Equal("  x label 10 10 80 20 text=一", after[3]);
        Assert.Equal("b box 0 240 200 200", after[4]);
        Assert.Equal("  # ⭐ y の説明その1", after[5]);
        Assert.Equal("  # ⭐ y の説明その2", after[6]);
        Assert.Equal("  y label 10 40 80 20 text=二", after[7]);

        var reparsed = Layouts.Parse("t", moved);
        Assert.Equal("y", reparsed.Roots[1].Children[0].Name);
        Assert.Equal(moved, Layouts.Write(reparsed));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 根を動かしても直前のコメントは連れて行かない(string nl)
    {
        // 🔴 規則①: 根（字下げ0）は直前コメントを動かさない ── 見出しと「その節点の
        //    説明」を字面から見分ける手立てが無いので、動かさない側に倒す。
        string original =
            $"# 画面の説明（ファイルの頭）{nl}" +
            $"a box 0 0 100 100{nl}" +
            $"b box 0 120 100 100{nl}";
        var layout = Layouts.Parse("t", original);

        string moved = MoveInto(layout, "a", "b", "after");
        string[] after = moved.Replace("\r\n", "\n").Split('\n');
        Assert.Equal("# 画面の説明（ファイルの頭）", after[0]);   // ⭐ 見出しは頭に残る
        Assert.Equal("b box 0 120 100 100", after[1]);
        Assert.Equal("a box 0 0 100 100", after[2]);
        Assert.Equal(moved, Layouts.Write(Layouts.Parse("t", moved)));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 部分木の中に挟まるコメントと空行は一緒に動く(string nl)
    {
        string original =
            $"a box 0 0 200 200{nl}" +
            $"  p box 0 0 180 180{nl}" +
            $"    # ⭐ 中に挟まる説明{nl}" +
            $"    q label 0 0 100 20 text=奥{nl}" +
            $"b box 0 240 100 100{nl}";
        var layout = Layouts.Parse("t", original);

        string moved = MoveInto(layout, "p", "b", "into");
        string[] after = moved.Replace("\r\n", "\n").Split('\n');

        // ⭐ 塊は**連続した行**なので、中のコメントは黙って一緒に動く（字下げも付け替わる）。
        Assert.Equal("a box 0 0 200 200", after[0]);
        Assert.Equal("b box 0 240 100 100", after[1]);
        Assert.Equal("  p box 0 0 180 180", after[2]);
        Assert.Equal("    # ⭐ 中に挟まる説明", after[3]);
        Assert.Equal("    q label 0 0 100 20 text=奥", after[4]);
        Assert.Equal(moved, Layouts.Write(Layouts.Parse("t", moved)));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 動かした行以外は1バイトも変わらない(string nl)
    {
        string original =
            $"# 頭{nl}" +
            $"a box 0 0 200 200{nl}" +
            $"  x label 10 10 80 20 text=一{nl}" +
            $"{nl}" +
            $"# ⚠️ b の説明{nl}" +
            $"b box 0 240 100 100{nl}";
        var layout = Layouts.Parse("t", original);

        string moved = MoveInto(layout, "x", "b", "after");
        var beforeLines = new List<string>(original.Replace("\r\n", "\n").Split('\n'));
        var afterLines = new List<string>(moved.Replace("\r\n", "\n").Split('\n'));

        // ⭐ 動いた x の行は字下げが 2 → 0 になる。それ以外は綴りも並びもそのまま。
        beforeLines.Remove("  x label 10 10 80 20 text=一");
        afterLines.Remove("x label 10 10 80 20 text=一");
        Assert.Equal(beforeLines, afterLines);
        Assert.Equal(moved, Layouts.Write(Layouts.Parse("t", moved)));
    }

    /// <summary>🔴 **実物（`box.txt`）で確かめる。**⚠️ 作り物の骨組みだけで固めると、
    /// 実際の書き方（`btree` の上に同じ字下げの説明が3行・ファイルの頭に見出し）と
    /// ずれていても気づけない ── 規則の出所は実物の慣習なので、実物で1本打つ。</summary>
    [Fact]
    public void 実物のbox骨組みでも説明は付いてきて見出しは残る()
    {
        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "layouts", "box.txt");
        Assert.True(System.IO.File.Exists(path), $"{path} が無い（csproj のコピー設定を見る）");
        string original = System.IO.File.ReadAllText(path);
        var layout = Layouts.Parse("box", original);

        // ⭐ btree（`detail` の子）を、同じ親の中で bfuse の手前へ。
        string moved = MoveInto(layout, "btree", "bfuse", "before");
        var before = original.Replace("\r\n", "\n").Split('\n');
        var after = moved.Replace("\r\n", "\n").Split('\n');

        // 🔴 ファイルの見出し（頭の行）は動かない。
        Assert.Equal(before[0], after[0]);
        Assert.StartsWith("#", after[0]);

        // 🔴 btree の直上にあった**同じ字下げの説明3行**が、そのまま付いてきている。
        int at = Array.FindIndex(after, s => s.TrimStart().StartsWith("btree", StringComparison.Ordinal));
        Assert.True(at >= 3, "btree が見つからない（実物の書き方が変わった？）");
        Assert.StartsWith("  #", after[at - 1]);
        Assert.StartsWith("  #", after[at - 2]);
        Assert.StartsWith("  #", after[at - 3]);
        // ⚠️ そのすぐ後ろが bfuse ＝「手前へ」が効いている。
        Assert.StartsWith("  bfuse", after[at + 1]);
        // ⚠️ 説明の上は親の detail ── btree は「最初の子」へ来たので、間に何も挟まらない
        //    （説明が元の位置に置き去りになっていない）。
        Assert.StartsWith("detail", after[at - 4]);

        // 🔴 行の集合そのものは変わらない（並びが変わっただけ・1行も欠けない）。
        var sortedBefore = new List<string>(before);
        var sortedAfter = new List<string>(after);
        sortedBefore.Sort(StringComparer.Ordinal);
        sortedAfter.Sort(StringComparer.Ordinal);
        Assert.Equal(sortedBefore, sortedAfter);

        // 🔴 往復が閉じる（保存ガードと同じ判定）。
        Assert.Equal(moved, Layouts.Write(Layouts.Parse("box", moved)));
    }

    [Fact]
    public void 自分の子孫へは動かせない()
    {
        var layout = Layouts.Parse("t",
            "a box 0 0 200 200\n" +
            "  p box 0 0 180 180\n" +
            "    q box 0 0 160 160\n");
        var a = layout.Roots[0];
        var p = a.Children[0];
        var q = p.Children[0];

        // 🔴 自分自身・子・孫のどれも「自分の中」── 動かすと木として読めなくなる。
        Assert.True(LayoutSplice.IsInSubtree(a, a.LineNumber));
        Assert.True(LayoutSplice.IsInSubtree(a, p.LineNumber));
        Assert.True(LayoutSplice.IsInSubtree(a, q.LineNumber));
        // ⚠️ 逆向き（子から見た親）は「自分の中」ではない ── 親の外へ出すのは正しい操作。
        Assert.False(LayoutSplice.IsInSubtree(q, a.LineNumber));

        // ⭐ 万一そこまで届いても、行を動かす側が自分で撥ねる（何も変えない）。
        string text = Layouts.Write(layout);
        int first = a.LineNumber, last = LayoutSplice.SubtreeLastLine(a);
        Assert.Equal(text, LayoutSplice.MoveLines(text, first, last, q.LineNumber, 2));
    }

    [Fact]
    public void 動かしたあとの行番号は塊の抜き差しで決まる()
    {
        // ⭐ `SelectOnly` の根拠 ── 名前で探し直さず、抜いた位置と挿し先だけで決める。
        //    後ろへ動かすと、抜いた塊のぶんだけ手前へ寄る。
        Assert.Equal(0, LayoutSplice.MovedIndex(2, 3, 0));    // 前へ: そのまま
        Assert.Equal(3, LayoutSplice.MovedIndex(0, 1, 5));    // 後ろへ: 5 − 2 行
        Assert.Equal(2, LayoutSplice.MovedIndex(2, 4, 2));    // 動かない位置

        var layout = Layouts.Parse("t",
            "a box 0 0 100 100\n" +
            "b box 0 120 100 100\n" +
            "c box 0 240 100 100\n");
        string moved = MoveInto(layout, "a", "c", "after");
        var reparsed = Layouts.Parse("t", moved);
        // ⚠️ 実際に読み直した行番号と一致する（数え方が机上とずれていない）。
        Assert.Equal(2, reparsed.Roots[2].LineNumber);
        Assert.Equal("a", reparsed.Roots[2].Name);
    }

    [Fact]
    public void 入れ物でない節点は並べ替えの親にもならない()
    {
        // ⭐ 判定は段階4a と**同じ関数**（規則を2つ持たない）。
        var layout = Layouts.Parse("t",
            "d host 0 0 100 100\n" +
            "e card 0 120 100 100 use=cell\n" +
            "f label 0 240 100 40 text=字\n" +
            "g scroll 0 360 100 100 content=400\n");
        var by = new Dictionary<string, LayoutNode>();
        foreach (var n in layout.Roots) by[n.Name] = n;

        Assert.False(LayoutSplice.IsContainer(by["d"]));   // host は中身をコードが持つ
        Assert.False(LayoutSplice.IsContainer(by["e"]));   // use= は部品の中身と混ざる
        Assert.False(LayoutSplice.IsContainer(by["f"]));   // label は入れ物でない
        Assert.True(LayoutSplice.IsContainer(by["g"]));    // 巻物は入れ物
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 終端の無い最後の行を動かしても前の行に融合しない(string nl)
    {
        // ⚠️ 段階3で踏んだ罠と同じ形 ── 終端の無い行が**途中**へ来ると次と融合し、
        //    節点が1つ黙って消える。`MoveLines` は繋ぎ直す拍で終端を補う。
        string original =
            $"a box 0 0 100 100{nl}" +
            $"b box 0 120 100 100";   // ⚠️ 末尾に終端が無い
        var layout = Layouts.Parse("t", original);
        var a = layout.Roots[0];
        var b = layout.Roots[1];

        string text = Layouts.Write(layout);
        string moved = LayoutSplice.MoveLines(text, b.LineNumber,
            LayoutSplice.SubtreeLastLine(b), a.LineNumber, 0);

        var reparsed = Layouts.Parse("t", moved);
        Assert.Equal(2, reparsed.Roots.Count);   // 🔴 融合して消えていない
        Assert.Equal(new[] { "b", "a" }, reparsed.Roots.Select(n => n.Name).ToArray());
    }

    // ── 5) 段階4b の追検査（2026-08-29 監査）──────────────────

    /// <summary>🔴 **「X の手前」に挿しても、X の説明コメントは X に残る。**
    ///
    /// ⚠️ 直す前は「X の説明の**後ろ**（＝X の行そのもの）」へ挿していたので、
    /// X の説明が動かしてきた節点のものとして読める字になり、X は説明を失った
    /// （実物35枚の総当たり12,191件のうち1,742件が該当）。
    /// ⭐ 「X の手前」＝「X の直前の兄弟の後ろ」の鏡にすると、説明の**手前**へ入る。</summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 手前へ挿しても落とし先の説明は落とし先に残る(string nl)
    {
        string original =
            $"# ファイルの見出し（画面ぜんたいの説明）{nl}" +
            $"a box 0 0 100 100{nl}" +
            $"# b の説明{nl}" +
            $"b box 0 120 100 100{nl}" +
            $"c box 0 240 100 100{nl}";
        var layout = Layouts.Parse("t", original);

        // ⭐ c を b の手前へ。
        string moved = MoveInto(layout, "c", "b", "before");
        var lines = LayoutSplice.SplitKeep(moved);
        int at = lines.FindIndex(s => s.Contains("# b の説明"));
        Assert.True(at >= 0);
        // 🔴 説明の**次**の行が b であること（間に c が割り込んでいない）。
        Assert.Contains("b box", lines[at + 1]);
        // ⭐ c は説明より手前（＝a の直後）へ入っている。
        Assert.Contains("c box", lines[at - 1]);
        Assert.Equal(new[] { "a", "c", "b" },
            Layouts.Parse("t", moved).Roots.Select(n => n.Name).ToArray());
        Assert.Equal(moved, Layouts.Write(Layouts.Parse("t", moved)));
    }

    /// <summary>⚠️ **最初のルートだけは例外** ── その手前はファイルの見出しなので、
    /// 見出しと「その節点の説明」を字面から見分ける手立てが無い。⭐ 見出しを守る側に倒し、
    /// 塊は見出しの**後ろ**（＝最初のルートの直前）へ入れる。</summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 最初のルートの手前へ挿してもファイルの見出しは頭に残る(string nl)
    {
        string original =
            $"# ファイルの見出し{nl}" +
            $"# 2行目の見出し{nl}" +
            $"a box 0 0 100 100{nl}" +
            $"b box 0 120 100 100{nl}";
        var layout = Layouts.Parse("t", original);

        string moved = MoveInto(layout, "b", "a", "before");
        var lines = LayoutSplice.SplitKeep(moved);
        Assert.Contains("# ファイルの見出し", lines[0]);   // 🔴 見出しは動かない
        Assert.Contains("# 2行目の見出し", lines[1]);
        Assert.Contains("b box", lines[2]);
        Assert.Equal(new[] { "b", "a" },
            Layouts.Parse("t", moved).Roots.Select(n => n.Name).ToArray());
    }

    /// <summary>🔴 **子を持つ節点の「下端」へ落としたら、その子ごと跨ぐ。**
    /// ⚠️ 部分木を忘れて「落とし先の次の行」へ挿すと、**その子の兄弟**として入ってしまう
    /// ── 実物では1,369件で他の節点が黙って付け替わる壊し方（監査の変異注入）。
    /// ⭐ 往復も行の集合も通ってしまうので、親を数えるここでしか捕まらない。</summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 子を持つ節点の下端へ落とすとその子ごと跨ぐ(string nl)
    {
        string original =
            $"a box 0 0 100 100{nl}" +
            $"  a1 label 0 0 50 20{nl}" +
            $"  a2 label 0 24 50 20{nl}" +
            $"b box 0 120 100 100{nl}";
        var layout = Layouts.Parse("t", original);

        // ⭐ b を a の下端へ（＝a の部分木のあと・ルートのまま）。
        string moved = MoveInto(layout, "b", "a", "after");
        var reparsed = Layouts.Parse("t", moved);
        // 🔴 b はルートのまま（a の子になっていない）。
        Assert.Equal(new[] { "a", "b" }, reparsed.Roots.Select(n => n.Name).ToArray());
        Assert.Equal(2, reparsed.Roots[0].Children.Count);   // a1/a2 は a の子のまま
        Assert.Empty(reparsed.Roots[1].Children);
        Assert.Equal(moved, Layouts.Write(reparsed));
    }

    /// <summary>⚠️ `Into` は1段深くする（`+2`）。⭐ 忘れると兄弟のまま入り、
    /// 実物では1,319件で親が違う ── 往復では捕まらない（監査の変異注入）。</summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 入れ物の中へ落とすと一段深くなる(string nl)
    {
        string original =
            $"a box 0 0 100 100{nl}" +
            $"  a1 label 0 0 50 20{nl}" +
            $"b label 0 120 50 20{nl}";
        var layout = Layouts.Parse("t", original);

        string moved = MoveInto(layout, "b", "a", "into");
        var reparsed = Layouts.Parse("t", moved);
        Assert.Single(reparsed.Roots);
        Assert.Equal(new[] { "a1", "b" },
            reparsed.Roots[0].Children.Select(n => n.Name).ToArray());
        Assert.Equal(2, reparsed.Roots[0].Children[1].Indent);
        Assert.Equal(moved, Layouts.Write(reparsed));
    }

    /// <summary>⭐ 動かしたあとの選択（`MovedIndex`）が、本当にその節点を指すか。
    /// ⚠️ ここを `before` で代用すると実物6,311件で別の節点を選ぶ（監査の変異注入）。</summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 動かしたあとの行番号が動かした節点を指す(string nl)
    {
        string original =
            $"a box 0 0 100 100{nl}" +
            $"  a1 label 0 0 50 20{nl}" +
            $"  a2 label 0 24 50 20{nl}" +
            $"b box 0 120 100 100{nl}" +
            $"c box 0 240 100 100{nl}";
        var layout = Layouts.Parse("t", original);

        var moved = FindByName(layout.Roots, "c")!;
        var target = FindByName(layout.Roots, "a")!;
        var movedPath = new List<LayoutNode>();
        LayoutSplice.TryPath(layout.Roots, moved.LineNumber, movedPath);
        var targetPath = new List<LayoutNode>();
        LayoutSplice.TryPath(layout.Roots, target.LineNumber, targetPath);

        string text = Layouts.Write(layout);
        var lines = LayoutSplice.SplitKeep(text);
        var plan = LayoutSplice.PlanMove(lines, movedPath, targetPath, layout.Roots, DropSpot.Before);
        string spliced = LayoutSplice.MoveLines(text, plan.First, plan.Last, plan.Before, plan.IndentDelta);

        int at = LayoutSplice.MovedIndex(plan.First, plan.Last, plan.Before)
            + (moved.LineNumber - plan.First);
        var reparsed = Layouts.Parse("t", spliced);
        var landed = FindByLine(reparsed.Roots, at);
        Assert.NotNull(landed);
        Assert.Equal("c", landed!.Name);
    }

    private static LayoutNode? FindByLine(IReadOnlyList<LayoutNode> list, int line)
    {
        foreach (var n in list)
        {
            if (n.LineNumber == line) return n;
            var deep = FindByLine(n.Children, line);
            if (deep is not null) return deep;
        }
        return null;
    }

    /// <summary>🔴 **CR と LF が混ざった原文でも、行が1本も消えない。**
    /// ⚠️ 裸の CR で終わる行の直後に LF 始まりの空行が来ると、繋いだ字では CRLF ひとつに
    /// 読めてしまい空行が消える ── この並びは**原文には存在しえず、並べ替えが作る**
    /// （ファズ 396,695件中 3,373件。純 CRLF では0件）。実物35枚は全部 CRLF なので
    /// 今は届かないが、扉は閉じておく。</summary>
    [Fact]
    public void CRとLFが混ざっていても行が消えない()
    {
        // ⚠️ `a` は裸の CR で終わる。空行の終端は LF。
        string original = "a box 0 0 100 100\r\nb box 0 120 100 100\r\nc box 0 240 100 100\n";
        var layout = Layouts.Parse("t", original);
        string text = Layouts.Write(layout);

        // ⭐ 行数（終端の数）が動かないことを、動かし方を変えて総当たりで確かめる。
        int lineCount = LayoutSplice.SplitKeep(text).Count;
        for (int first = 0; first < lineCount; first++)
            for (int before = 0; before <= lineCount; before++)
            {
                string moved = LayoutSplice.MoveLines(text, first, first, before, 0);
                Assert.Equal(lineCount, LayoutSplice.SplitKeep(moved).Count);
            }
    }

    /// <summary>⭐ 「後ろの兄弟の手前へ」のように**今いる場所と同じ**指し方をすると、
    /// 塊が間の空行より上へ回り込んで**字だけ**が変わる ── 木の形は1つも変わらない。
    /// ⚠️ これを「動かしました」と言って取り消しに積むと、押しても何も起きない取り消しが
    /// 溜まる（監査: 実物で372件）。⭐ <see cref="LayoutSplice.SameShape"/> で見分ける。</summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void 木の形が変わらない動きは見分けられる(string nl)
    {
        string original =
            $"a box 0 0 100 100{nl}" +
            $"{nl}" +
            $"# b の説明{nl}" +
            $"b box 0 120 100 100{nl}";
        var layout = Layouts.Parse("t", original);

        // ⭐ b を a の下端へ ＝ いま居る場所。字は変わる（空行が下へ回る）が、形は同じ。
        string moved = MoveInto(layout, "b", "a", "after");
        var reparsed = Layouts.Parse("t", moved);
        Assert.NotEqual(original, moved);                       // 字は変わった
        Assert.True(LayoutSplice.SameShape(layout, reparsed));  // 🔴 でも形は同じ

        // ⚠️ 本当に並べ替えたときは、ちゃんと「違う形」と言う。
        string real = MoveInto(layout, "b", "a", "before");
        Assert.False(LayoutSplice.SameShape(layout, Layouts.Parse("t", real)));
    }

    /// <summary>🔴 **実物35枚を総当たりで動かして、壊れないことを確かめる。**
    ///
    /// ⭐ 検査する不変量（どれも往復や行の集合だけでは捕まらないものを含む）:
    ///   ① 読み直せる ② 往復が閉じる ③ 字下げを除いた行の多重集合が不変
    ///   ④ 節点の数が不変 ⑤ **動かしていない節点の親が1つも変わらない**
    ///   ⑥ **動かしていない節点の「直前の説明」が1つも変わらない**（監査 重大2 の網）
    ///
    /// ⚠️ 全ての組み合わせ（12,000件超）は遅いので、**落とし先を間引いて**回す ──
    /// 壊れ方は「どの落とし先か」でなく「どの形の落とし先か」で決まるので、
    /// 各文書の全ての節点を1回は動かす限り、網の目は粗くならない。</summary>
    [Fact]
    public void 実物を総当たりで動かしても他の節点は巻き添えにならない()
    {
        int done = 0;
        foreach (var path in Directory.GetFiles(LayoutDir, "*.txt").OrderBy(p => p, StringComparer.Ordinal))
        {
            string id = Path.GetFileNameWithoutExtension(path);
            var layout = Layouts.Parse(id, File.ReadAllText(path));
            string text = Layouts.Write(layout);
            var lines = LayoutSplice.SplitKeep(text);
            var all = new List<LayoutNode>();
            Collect(layout.Roots, all);
            var parentBefore = ParentMap(layout.Roots);
            var leadBefore = LeadMap(all, lines);

            foreach (var moved in all)
            {
                // ⚠️ 落とし先は間引く（全部やると12,000件超で遅い）── 動かす側は全部通す。
                for (int t = 0; t < all.Count; t += 3)
                {
                    var target = all[t];
                    foreach (var spot in new[] { DropSpot.Before, DropSpot.Into, DropSpot.After })
                    {
                        if (moved.LineNumber == target.LineNumber) continue;
                        if (LayoutSplice.IsInSubtree(moved, target.LineNumber)) continue;

                        var movedPath = new List<LayoutNode>();
                        if (!LayoutSplice.TryPath(layout.Roots, moved.LineNumber, movedPath)) continue;
                        var targetPath = new List<LayoutNode>();
                        if (!LayoutSplice.TryPath(layout.Roots, target.LineNumber, targetPath)) continue;
                        if (spot == DropSpot.Into && !LayoutSplice.IsContainer(target)) continue;

                        var plan = LayoutSplice.PlanMove(lines, movedPath, targetPath, layout.Roots, spot);
                        string spliced = LayoutSplice.MoveLines(text, plan.First, plan.Last, plan.Before, plan.IndentDelta);
                        if (spliced == text) continue;

                        string what = $"{id}: 「{moved.Name}」を「{target.Name}」の {spot} へ";
                        var re = Layouts.Parse(id, spliced);                       // ①
                        Assert.Equal(spliced, Layouts.Write(re));                  // ②
                        Assert.Equal(Bag(lines), Bag(LayoutSplice.SplitKeep(spliced)));   // ③

                        var reAll = new List<LayoutNode>();
                        Collect(re.Roots, reAll);
                        Assert.True(all.Count == reAll.Count, what + " ── 節点の数が変わった");  // ④

                        var parentAfter = ParentMap(re.Roots);
                        var leadAfter = LeadMap(reAll, LayoutSplice.SplitKeep(spliced));
                        var movedNames = new HashSet<string>();
                        Names(moved, movedNames);
                        // ⚠️ **最初のルートの手前へ落とす場合だけは、説明の検査から外す。**
                        //    ⭐ 最初のルートの「直前の説明」は**ファイルの見出しそのもの**で、
                        //    字面から見分ける手立てが無い（`LeadCommentStart` の規則①）。
                        //    見出しを頭に残す（＝塊は見出しの後ろへ入れる）と決めた以上、
                        //    最初のルートが見出しを手放すのは避けられない ── 見出しごと
                        //    引っ越すよりずっとまし、という取り引き。
                        string? exempt = spot == DropSpot.Before
                            && target.LineNumber == layout.Roots[0].LineNumber
                            ? target.Name : null;
                        foreach (var n in reAll)
                        {
                            if (movedNames.Contains(n.Name)) continue;
                            Assert.True(parentBefore.TryGetValue(n.Name, out var p0)
                                && parentAfter.TryGetValue(n.Name, out var p1) && p0 == p1,
                                what + $" ── 「{n.Name}」の親が変わった");             // ⑤
                            // ⑥ ⚠️ 見るのは「**元の説明が、まだ自分の直上に付いているか**」
                            //    （`c1` が `c0` で終わっているか）── 説明を新しく**得る**のは
                            //    避けようがない。上に居た節点が動いて退くと、その上のコメントが
                            //    降りてくるだけで、自分の説明は失われていない（実物
                            //    `panelmini.txt` で `elem` を動かすと、見出しが `art` の
                            //    説明の上に降りてくる ── 骨組みが本当にそう変わったので、
                            //    直しようが無いし、間違いでもない）。
                            //    🔴 監査 重大2（説明を**取られる**＝直上から剥がされる）は
                            //    この向きでちょうど捕まる。
                            if (n.Name == exempt) continue;
                            Assert.True(leadBefore.TryGetValue(n.Name, out var c0)
                                && leadAfter.TryGetValue(n.Name, out var c1)
                                && c1.EndsWith(c0, System.StringComparison.Ordinal),
                                what + $" ── 「{n.Name}」が直前の説明を失った");     // ⑥
                        }
                        done++;
                    }
                }
            }
        }
        Assert.True(done > 500, $"実物を動かせた組み合わせが {done} 件しかない（検査が空回りしている）");
    }

    private static readonly string LayoutDir =
        Path.Combine(System.AppContext.BaseDirectory, "layouts");

    private static void Collect(IReadOnlyList<LayoutNode> list, List<LayoutNode> into)
    {
        foreach (var n in list) { into.Add(n); Collect(n.Children, into); }
    }

    private static void Names(LayoutNode node, HashSet<string> into)
    {
        into.Add(node.Name);
        foreach (var c in node.Children) Names(c, into);
    }

    /// <summary>名前 → 親の名前（根は空）。⚠️ 行番号は動くので名前で照合する。</summary>
    private static Dictionary<string, string> ParentMap(IReadOnlyList<LayoutNode> roots)
    {
        var map = new Dictionary<string, string>();
        void Walk(IReadOnlyList<LayoutNode> list, string parent)
        {
            foreach (var n in list) { map[n.Name] = parent; Walk(n.Children, n.Name); }
        }
        Walk(roots, "");
        return map;
    }

    /// <summary>名前 → その節点の直前に続くコメントの字（無ければ空）。
    /// ⭐ 監査 重大2（説明の付け替え）を捕まえる網。</summary>
    private static Dictionary<string, string> LeadMap(List<LayoutNode> all, List<string> lines)
    {
        var map = new Dictionary<string, string>();
        foreach (var n in all)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = n.LineNumber - 1; i >= 0; i--)
            {
                string s = lines[i];
                int ind = LayoutSplice.IndentOf(s);
                if (ind >= s.Length) break;
                char c = s[ind];
                if (c == '\r' || c == '\n' || c != '#') break;
                sb.Insert(0, s.Trim());
            }
            map[n.Name] = sb.ToString();
        }
        return map;
    }

    private static List<string> Bag(List<string> lines)
    {
        var bag = new List<string>(lines.Count);
        foreach (var s in lines) bag.Add(s.Trim());
        bag.Sort(StringComparer.Ordinal);
        return bag;
    }
}
