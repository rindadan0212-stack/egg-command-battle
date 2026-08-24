using System.Collections.Generic;
using System.Linq;
using EggCommand.Core;
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
}
