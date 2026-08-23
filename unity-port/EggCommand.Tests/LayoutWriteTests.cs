using System;
using System.Collections.Generic;
using System.IO;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>骨組みの書き出し（`Layouts.Write`）。
///
/// ⭐ **これが「往復」を閉じる検査です。** GUI 編集ツール（このコミットでは作らない）は、
/// `Parse` で読んで、直して、`Write` で書き戻す。⚠️ 書き戻しでコメントが1文字でも
/// 変われば、779行中412行（53%）を占める「なぜその数か」の記録が消える。
///
/// ⚠️ **道具はわざと壊して効きを確かめる。**「元に戻る」は、
/// 実は原文を丸ごと echo しているだけかもしれない ── それを見抜く試験を対で置く。</summary>
public class LayoutWriteTests
{
    // ── #1 実物32枚すべてで往復が閉じる ─────────────────

    private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "layouts");

    public static IEnumerable<object[]> All()
    {
        foreach (var path in Directory.GetFiles(Dir, "*.txt"))
            yield return new object[] { Path.GetFileNameWithoutExtension(path) };
    }

    /// <summary>⚠️ 1枚も見つからなければ、下の Theory は「調べていない」のに緑になる。
    /// ⭐ <see cref="LayoutAssetTests"/> と同じ理由で置く。</summary>
    [Fact]
    public void 骨組みが見つかる()
    {
        Assert.True(Directory.Exists(Dir), $"{Dir} が無い（csproj のコピー設定を見る）");
        Assert.NotEmpty(Directory.GetFiles(Dir, "*.txt"));
    }

    /// <summary>🔴 **`Write(Parse(t)) == t` が実物32枚すべてでバイト単位に成り立つ。**
    ///
    /// ⚠️ `use=` を差し替える前の、`Parse` の生の出力にだけ掛ける
    /// （`Resolve` は別の骨組みの中身を差し込むので、往復の対象が変わってしまう）。</summary>
    [Theory]
    [MemberData(nameof(All))]
    public void 書き出すと原文に戻る(string id)
    {
        string original = File.ReadAllText(Path.Combine(Dir, id + ".txt"));
        string written = Layouts.Write(Layouts.Parse(id, original));
        Assert.Equal(original, written);
    }

    // ── #4 コメント・空行がそのまま残る（dice.txt: 19行中17行がコメント）──

    [Fact]
    public void diceのコメントは17行とも1バイトも変わらない()
    {
        string path = Path.Combine(Dir, "dice.txt");
        string original = File.ReadAllText(path);
        var layout = Layouts.Parse("dice", original);
        string written = Layouts.Write(layout);

        // ⭐ 全体が戻ることは #1 で見ている。ここは「コメントである」ことを
        //    明示的に数えて確かめる ── 17行という数そのものが、この検査の的。
        string[] originalLines = original.Replace("\r\n", "\n").Split('\n');
        string[] writtenLines = written.Replace("\r\n", "\n").Split('\n');
        Assert.Equal(originalLines.Length, writtenLines.Length);

        int comments = 0;
        for (int i = 0; i < originalLines.Length; i++)
        {
            if (originalLines[i].TrimStart().StartsWith("#"))
            {
                comments++;
                Assert.Equal(originalLines[i], writtenLines[i]);
            }
        }
        Assert.Equal(17, comments);
    }

    // ── #2 空回りしていないことの証明（節点の数を1つ変える）────

    private const string Multi = "# 見出し\na label 0 0 100 40 text=Alpha\nb label 0 60 100 40 text=Beta\n";

    /// <summary>⭐ **1つの節点の値を変えると、その行だけが変わる。**
    /// ⚠️ 原文を丸ごと echo していたら、この検査は絶対に緑にならない
    /// （どこも変わらないか、全部が置き換わるかのどちらかになる）。</summary>
    [Fact]
    public void 値を変えるとその行だけが変わる()
    {
        var layout = Layouts.Parse("t", Multi);
        var a = layout.Roots[0];
        var b = layout.Roots[1];

        // ⭐ a の Left だけ 0 → 10 に変える。他の欄・他の節点は無傷のまま。
        var changedA = new LayoutNode(a.Name, a.Kind, 10f, a.Top, a.Width, a.Height,
            a.Options, a.Children, a.LineNumber, a.Indent, a.Fields, a.Trailing, a.Terminator);
        var edited = new Layout(layout.Id, new List<LayoutNode> { changedA, b }, layout.Lines);

        string written = Layouts.Write(edited);
        string[] originalLines = Multi.Replace("\r\n", "\n").Split('\n');
        string[] writtenLines = written.Replace("\r\n", "\n").Split('\n');

        Assert.Equal(originalLines.Length, writtenLines.Length);
        Assert.Equal(originalLines[0], writtenLines[0]);              // 見出し（コメント）は無傷
        Assert.NotEqual(originalLines[1], writtenLines[1]);           // a の行だけ変わる
        // ⚠️ 「10」は "100" の部分文字列でもあるので、Contains では弱い。厳密に見る。
        Assert.Equal("a label 10 0 100 40 text=Alpha", writtenLines[1]);
        Assert.Equal(originalLines[2], writtenLines[2]);              // b の行は無傷
    }

    /// <summary>⚠️ **節点を減らすと、その行は消える**（コメントとして生き残らない）。
    /// ⭐ 「原文をそのまま通す」経路（コメント・空行用）と「節点の行」経路が
    /// 混同されていないことの裏取り。</summary>
    [Fact]
    public void 節点を減らすとその行が消える()
    {
        var layout = Layouts.Parse("t", Multi);
        var b = layout.Roots[1];
        var edited = new Layout(layout.Id, new List<LayoutNode> { b }, layout.Lines);

        string written = Layouts.Write(edited);

        Assert.DoesNotContain("Alpha", written);   // 消した a の行はどこにも残らない
        Assert.Contains("見出し", written);         // コメントは無傷
        Assert.Contains("Beta", written);           // 残した b は無傷
    }

    /// <summary>⭐ **節点を増やすと、新しい行が足される。**⚠️ 元の行は1つも動かない
    /// （増やした節点は原文の行を持たないので、末尾へ足す）。</summary>
    [Fact]
    public void 節点を増やすと行が足される()
    {
        var layout = Layouts.Parse("t", Multi);
        var extra = new LayoutNode("c", "label", 0, 120, 100, 40,
            new Dictionary<string, string> { { "text", "Gamma" } }, new List<LayoutNode>());
        var edited = new Layout(layout.Id,
            new List<LayoutNode> { layout.Roots[0], layout.Roots[1], extra }, layout.Lines);

        string written = Layouts.Write(edited);
        string[] originalLines = Multi.Replace("\r\n", "\n").Split('\n');
        string[] writtenLines = written.Replace("\r\n", "\n").Split('\n');

        // ⭐ 元の3行（見出し・a・b）はそのまま先頭に残る。
        //    ⚠️ `originalLines` の最後は「改行の後ろ」を表す空の1個 ── 増やした行は
        //    その**後ろ**に足されるので、ここは比べない（比べると新しい行と競合する）。
        for (int i = 0; i < originalLines.Length - 1; i++)
            Assert.Equal(originalLines[i], writtenLines[i]);

        Assert.Contains("Gamma", written);
    }

    // ── #3 桁揃えが保たれる ────────────────────────────

    /// <summary>⭐ **値の桁数が増えても、後ろの欄は同じ列に残る**（詰め直す余地がある時）。
    /// ⚠️ この骨組みは手で桁を揃えてあるので、これが崩れると編集のたびに
    /// 後ろの欄がガタつく画面になる。</summary>
    [Fact]
    public void 桁数が増えても後ろの欄は同じ列に残る()
    {
        const string original = "a label 0 0 180          40          bind=name\n";
        var layout = Layouts.Parse("t", original);
        var node = layout.Roots[0];

        // ⭐ 幅だけ 180 → 1800（3桁→4桁）に変える。余裕を持たせた行なので詰め直せる。
        var changed = new LayoutNode(node.Name, node.Kind, node.Left, node.Top, 1800f, node.Height,
            node.Options, node.Children, node.LineNumber, node.Indent, node.Fields,
            node.Trailing, node.Terminator);
        string written = changed.RenderLine();

        int originalColumn = original.IndexOf("bind=name", StringComparison.Ordinal);
        int writtenColumn = written.IndexOf("bind=name", StringComparison.Ordinal);
        Assert.Equal(originalColumn, writtenColumn);
        Assert.Contains("1800", written);
    }

    /// <summary>⚠️ **わざと壊す対。**詰め直す余地が無い（空白1つしか無い）行で
    /// 同じことをすると、桁は守れない ── その代わり欄がくっつきもしない
    /// （空白1つへ縮退する）。⭐ 「列を守る」を無条件にやると、ここで欄が
    /// くっついて壊れた行になる。</summary>
    [Fact]
    public void 詰める余地が無ければ空白1つへ縮退する()
    {
        const string original = "a label 0 0 180 40 bind=name\n";   // 幅の後ろは空白1つだけ
        var layout = Layouts.Parse("t", original);
        var node = layout.Roots[0];

        var changed = new LayoutNode(node.Name, node.Kind, node.Left, node.Top, 1800f, node.Height,
            node.Options, node.Children, node.LineNumber, node.Indent, node.Fields,
            node.Trailing, node.Terminator);
        string written = changed.RenderLine();

        Assert.Contains("1800 40", written);      // 欄はくっつかない（空白1つで区切られる）
        Assert.DoesNotContain("180040", written); // ⚠️ これが起きたら壊れた行
    }

    // ── #5 わざと壊して落ちることを確かめる（対） ────────

    [Fact]
    public void nullを書き出そうとしたら落ちる()
    {
        Assert.Throws<ArgumentNullException>(() => Layouts.Write(null));
    }

    /// <summary>⭐ 上と対 ── 普通の骨組みは落ちずに書ける。</summary>
    [Fact]
    public void 普通の骨組みは落ちずに書ける()
    {
        var layout = Layouts.Parse("t", Multi);
        var written = Layouts.Write(layout);
        Assert.Equal(Multi, written);
    }

    /// <summary>⚠️ **数の綴りは、値が変わっていなければ守られる。**
    /// ⭐ `0` と `0.0` を float へ落とすと区別が消えるので、綴りをそのまま
    /// 欄に持っておかないとこの検査は必ず落ちる。</summary>
    [Fact]
    public void 変えていない数はゼロの綴りも守られる()
    {
        const string original = "a label 0.0 0 100 40\n";
        var layout = Layouts.Parse("t", original);
        string written = layout.Roots[0].RenderLine();
        Assert.Equal(original, written);
        Assert.Contains("0.0", written);
    }

    /// <summary>⚠️ 上と対 ── 値を変えれば、当然その綴りは失われて今の値になる。</summary>
    [Fact]
    public void 変えた数は今の値になる()
    {
        const string original = "a label 0.0 0 100 40\n";
        var layout = Layouts.Parse("t", original);
        var node = layout.Roots[0];
        var changed = new LayoutNode(node.Name, node.Kind, 5f, node.Top, node.Width, node.Height,
            node.Options, node.Children, node.LineNumber, node.Indent, node.Fields,
            node.Trailing, node.Terminator);
        string written = changed.RenderLine();
        Assert.DoesNotContain("0.0", written);
        Assert.Contains("5", written);
    }

    // ── #6 難所を作った字で試験する ──────────────────────
    //
    // ⚠️ 実物32枚は「CRLF・行末空白なし・小数の綴りなし・最終行に改行あり」で
    //    均質だった（2026-08-23・実測で判明）。⭐ だから #1（実物での往復）が
    //    最初から緑だったのは「保持の仕組みが正しいから」ではなく
    //    「難所が題材に無かったから」でしかない。ここでは難所を**自分で作って**試す。

    /// <summary>LF だけの骨組みが原文に戻る。</summary>
    [Fact]
    public void LFだけの骨組みが原文に戻る()
    {
        const string lfOnly = "# 見出し\na label 0 0 100 40\nb label 0 60 100 40\n";
        var layout = Layouts.Parse("t", lfOnly);
        string written = Layouts.Write(layout);
        Assert.Equal(lfOnly, written);
        Assert.DoesNotContain("\r", written);
    }

    /// <summary>⚠️ わざと壊す対 ── 終端を `\r\n` に取り違えると、LF だけの原文には戻らない
    /// （<see cref="LayoutNode.Terminator"/> が本当に効いていることの裏取り）。</summary>
    [Fact]
    public void LFの行の終端を取り違えると元に戻らない()
    {
        const string lfOnly = "a label 0 0 100 40\n";
        var layout = Layouts.Parse("t", lfOnly);
        var a = layout.Roots[0];
        Assert.Equal("\n", a.Terminator);   // 前提: LF で終わっていた

        var wrong = new LayoutNode(a.Name, a.Kind, a.Left, a.Top, a.Width, a.Height,
            a.Options, a.Children, a.LineNumber, a.Indent, a.Fields, a.Trailing, "\r\n");
        Assert.NotEqual(lfOnly, wrong.RenderLine());
        Assert.Equal("a label 0 0 100 40\r\n", wrong.RenderLine());
    }

    /// <summary>🔴 **CRLF と LF が混ざった骨組みが、行ごとの終端そのままに戻る。**
    /// ⚠️ 実物32枚はどれも単一の終端で統一されていたので、この形は実物では試せない。</summary>
    [Fact]
    public void CRLFとLFが混ざった骨組みが行ごとの終端そのままに戻る()
    {
        const string mixed = "a label 0 0 100 40\r\nb label 0 60 100 40\n";
        var layout = Layouts.Parse("t", mixed);

        // ⭐ 1行目は \r\n、2行目は \n ── 節点ごとに終端が違うことを先に確かめる。
        Assert.Equal("\r\n", layout.Roots[0].Terminator);
        Assert.Equal("\n", layout.Roots[1].Terminator);

        string written = Layouts.Write(layout);
        Assert.Equal(mixed, written);
    }

    /// <summary>⚠️ わざと壊す対 ── 終端を1本の値に丸めて（2行目にも \r\n を使って）
    /// しまうと、混ざった原文には戻らない。⭐ 行ごとに終端を持つ設計が
    /// 効いていることの裏取り（1本の終端しか持たない実装だとここで壊れる）。</summary>
    [Fact]
    public void 終端を1本に丸めると混ざった原文には戻らない()
    {
        const string mixed = "a label 0 0 100 40\r\nb label 0 60 100 40\n";
        var layout = Layouts.Parse("t", mixed);
        var a = layout.Roots[0];
        var b = layout.Roots[1];

        // ⚠️ 2行目にも（本当は \n のところ）\r\n を使わせる ── 「1本に丸めた」の再現。
        var wrongB = new LayoutNode(b.Name, b.Kind, b.Left, b.Top, b.Width, b.Height,
            b.Options, b.Children, b.LineNumber, b.Indent, b.Fields, b.Trailing, "\r\n");
        string written = a.RenderLine() + wrongB.RenderLine();
        Assert.NotEqual(mixed, written);
    }

    /// <summary>🔴 **行末に空白がある行がそのまま戻る。**
    /// ⚠️ いまの `Parse` は `raw.Trim()` を通す（<see cref="Layouts.Parse"/> 参照）ので、
    /// これが緑になって初めて `Trailing` が本当に効いていると言える。</summary>
    [Fact]
    public void 行末に空白がある行がそのまま戻る()
    {
        const string original = "a label 0 0 100 40   \nb label 0 60 100 40\n";   // 1行目の末尾に空白3つ
        var layout = Layouts.Parse("t", original);
        Assert.Equal("   ", layout.Roots[0].Trailing);   // ⭐ 本当に捉えていることを直接見る

        string written = Layouts.Write(layout);
        Assert.Equal(original, written);
    }

    /// <summary>⚠️ わざと壊す対 ── `Trailing` を捨てると（`.Trim()` だけを信じた実装が
    /// やってしまう形）、行末の空白は戻らない。</summary>
    [Fact]
    public void 行末の空白を捨てると元の行に戻らない()
    {
        const string original = "a label 0 0 100 40   \n";
        var layout = Layouts.Parse("t", original);
        var a = layout.Roots[0];

        var wrong = new LayoutNode(a.Name, a.Kind, a.Left, a.Top, a.Width, a.Height,
            a.Options, a.Children, a.LineNumber, a.Indent, a.Fields, "", a.Terminator);
        Assert.NotEqual(original, wrong.RenderLine());
        Assert.Equal("a label 0 0 100 40\n", wrong.RenderLine());
    }

    /// <summary>🔴 **最終行に改行が無い骨組みがそのまま戻る**（末尾に改行を足さない）。</summary>
    [Fact]
    public void 最終行に改行が無い骨組みがそのまま戻る()
    {
        const string original = "a label 0 0 100 40\nb label 0 60 100 40";   // 末尾に \n 無し
        var layout = Layouts.Parse("t", original);
        Assert.Equal("", layout.Roots[1].Terminator);   // ⭐ 本当に捉えていることを直接見る

        string written = Layouts.Write(layout);
        Assert.Equal(original, written);
        Assert.False(written.EndsWith("\n", StringComparison.Ordinal));
    }

    /// <summary>⚠️ わざと壊す対 ── 終端を「改行が在る」と取り違えると、
    /// 無かった改行が足されてしまう。</summary>
    [Fact]
    public void 終端の判定を誤ると無かった改行が足される()
    {
        const string original = "a label 0 0 100 40";   // 改行が全く無い1行だけの骨組み
        var layout = Layouts.Parse("t", original);
        var a = layout.Roots[0];
        Assert.Equal("", a.Terminator);   // 前提

        var wrong = new LayoutNode(a.Name, a.Kind, a.Left, a.Top, a.Width, a.Height,
            a.Options, a.Children, a.LineNumber, a.Indent, a.Fields, a.Trailing, "\n");
        Assert.NotEqual(original, wrong.RenderLine());
        Assert.Equal(original + "\n", wrong.RenderLine());
    }

    /// <summary>`text=` の中の `\n`（展開して本当の改行になる）がそのまま戻る。
    /// ⚠️ 実物32枚のうち、これを含む画面はあるが、値を変えない往復では
    /// 「展開してから \n → \\n へ戻す」経路そのものは通らない（元の綴りを
    /// そのまま使う経路しか通らないため）。ここは値を変えない場合の裏取り。</summary>
    [Fact]
    public void textの中の改行がそのまま戻る()
    {
        const string original = "a label 0 0 400 80 text=空き\\n（自動で埋まる）\n";
        var layout = Layouts.Parse("t", original);
        Assert.Equal("空き\n（自動で埋まる）", layout.Roots[0].Option("text"));   // 展開後は本当の改行

        string written = Layouts.Write(layout);
        Assert.Equal(original, written);
    }

    /// <summary>⚠️ 上と対 ── `text=` の値を変えても（展開後の値を差し替えても）、
    /// 書き出しは `\n` → `\\n` へ戻して1行のまま保つ。⭐ ここを逃さないと、
    /// 本当の改行が1行の途中に紛れ込んで行ベース形式が壊れる。</summary>
    [Fact]
    public void 変えたtextの改行も書き出しで1行に逃がされる()
    {
        const string original = "a label 0 0 400 80 text=A\\nB\n";
        var layout = Layouts.Parse("t", original);
        var node = layout.Roots[0];

        var options = new Dictionary<string, string>(node.Options) { ["text"] = "X\nY" };
        var changed = new LayoutNode(node.Name, node.Kind, node.Left, node.Top, node.Width, node.Height,
            options, node.Children, node.LineNumber, node.Indent, node.Fields,
            node.Trailing, node.Terminator);

        string written = changed.RenderLine();
        // ⚠️ 終端の \n を除けば、本当の改行は1つも無い（1行に収まっている）
        Assert.DoesNotContain("\n", written.Substring(0, written.Length - 1));
        Assert.Equal("a label 0 0 400 80 text=X\\nY\n", written);
    }

    // ── #7 付け足しの追加・削除 ──────────────────────────
    //
    // ⭐ これを塞がないと、GUI 編集ツールは1歩目で詰まる（札に `lead=yes` を足す・
    //    `gap=` を書き換える、が全部できない）。⚠️ `Options` の型はそのまま
    //    （`IReadOnlyDictionary<string, string>`）── 既存の欄の並びは `Fields` が、
    //    新顔の並びは Options 自身の列挙順（.NET の Dictionary は削除が無ければ
    //    足した順を保つ）が受け持つので、これで足りると判断した。

    /// <summary>元の行に無かった `key=value` を足したら、行末（付け足しの最後）に書かれる。</summary>
    [Fact]
    public void 付け足しを足すと行末に足される()
    {
        const string original = "a label 0 0 100 40 bind=name\n";
        var layout = Layouts.Parse("t", original);
        var node = layout.Roots[0];

        var options = new Dictionary<string, string>(node.Options) { ["lead"] = "yes" };
        var changed = new LayoutNode(node.Name, node.Kind, node.Left, node.Top, node.Width, node.Height,
            options, node.Children, node.LineNumber, node.Indent, node.Fields,
            node.Trailing, node.Terminator);

        string written = changed.RenderLine();
        Assert.Equal("a label 0 0 100 40 bind=name lead=yes\n", written);
    }

    /// <summary>元にあった `key=` を消したら、その欄は行から消える
    /// （⚠️ `key=` だけ空にする、ではない ── 欄ごと無くなる）。</summary>
    [Fact]
    public void 付け足しを消すと欄ごと消える()
    {
        const string original = "a label 0 0 100 40 bind=name lead=yes\n";
        var layout = Layouts.Parse("t", original);
        var node = layout.Roots[0];

        var options = new Dictionary<string, string>(node.Options);
        options.Remove("lead");
        var changed = new LayoutNode(node.Name, node.Kind, node.Left, node.Top, node.Width, node.Height,
            options, node.Children, node.LineNumber, node.Indent, node.Fields,
            node.Trailing, node.Terminator);

        string written = changed.RenderLine();
        Assert.Equal("a label 0 0 100 40 bind=name\n", written);
        Assert.DoesNotContain("lead", written);
    }

    /// <summary>複数足した新顔は、`Options` に足した順に並ぶ。</summary>
    [Fact]
    public void 複数の新顔はOptionsに足した順に並ぶ()
    {
        const string original = "a label 0 0 100 40\n";
        var layout = Layouts.Parse("t", original);
        var node = layout.Roots[0];

        var options = new Dictionary<string, string>(node.Options) { ["lead"] = "yes", ["dock"] = "no" };
        var changed = new LayoutNode(node.Name, node.Kind, node.Left, node.Top, node.Width, node.Height,
            options, node.Children, node.LineNumber, node.Indent, node.Fields,
            node.Trailing, node.Terminator);

        string written = changed.RenderLine();
        Assert.Equal("a label 0 0 100 40 lead=yes dock=no\n", written);
    }

    /// <summary>⚠️ 🔴 **`text=` は必ず行末。**足した付け足しが `text=` の後ろへ回ると、
    /// それが字として画面に出る事故（実測: 釦に「あきらめる when=!done」）と同じ形になる。
    /// ⭐ 新顔は必ず `text=` より前に入ることを確かめる。</summary>
    [Fact]
    public void 付け足しを足してもtextより前に入る()
    {
        const string original = "a label 0 0 400 40 tap=open text=あきらめる\n";
        var layout = Layouts.Parse("t", original);
        var node = layout.Roots[0];

        var options = new Dictionary<string, string>(node.Options) { ["when"] = "!done" };
        var changed = new LayoutNode(node.Name, node.Kind, node.Left, node.Top, node.Width, node.Height,
            options, node.Children, node.LineNumber, node.Indent, node.Fields,
            node.Trailing, node.Terminator);

        string written = changed.RenderLine();
        int whenIndex = written.IndexOf("when=", StringComparison.Ordinal);
        int textIndex = written.IndexOf("text=", StringComparison.Ordinal);
        Assert.True(whenIndex >= 0 && textIndex >= 0 && whenIndex < textIndex);
        Assert.EndsWith("text=あきらめる\n", written, StringComparison.Ordinal);
    }

    /// <summary>⭐ 上の裏取り ── 書いた字を <see cref="Layouts.Parse"/> でもう一度
    /// 読み直せる（＝ `text=` の後ろに付け足しが無い、という Parse 自身の罠に
    /// 引っかからない）。⚠️ 引っかかっていたらここで例外が飛ぶ。</summary>
    [Fact]
    public void 足した付け足しは書いたものを読み直しても同じ値になる()
    {
        const string original = "a label 0 0 400 40 tap=open text=あきらめる\n";
        var layout = Layouts.Parse("t", original);
        var node = layout.Roots[0];

        var options = new Dictionary<string, string>(node.Options) { ["when"] = "!done" };
        var changed = new LayoutNode(node.Name, node.Kind, node.Left, node.Top, node.Width, node.Height,
            options, node.Children, node.LineNumber, node.Indent, node.Fields,
            node.Trailing, node.Terminator);

        string written = changed.RenderLine();
        var reparsed = Layouts.Parse("t", written);
        Assert.Equal("あきらめる", reparsed.Roots[0].Option("text"));
        Assert.Equal("!done", reparsed.Roots[0].Option("when"));
    }

    /// <summary>⭐ 桁揃え: 足した付け足しの後ろにある元の欄は、詰め直す余地があれば
    /// 元の桁を保つ。</summary>
    [Fact]
    public void 足した付け足しの後ろは詰める余地があれば桁を保つ()
    {
        const string original = "a label 0 0 400 40 tap=open                    text=あきらめる\n";
        var layout = Layouts.Parse("t", original);
        var node = layout.Roots[0];

        var options = new Dictionary<string, string>(node.Options) { ["when"] = "!done" };
        var changed = new LayoutNode(node.Name, node.Kind, node.Left, node.Top, node.Width, node.Height,
            options, node.Children, node.LineNumber, node.Indent, node.Fields,
            node.Trailing, node.Terminator);

        string written = changed.RenderLine();
        int originalColumn = original.IndexOf("text=", StringComparison.Ordinal);
        int writtenColumn = written.IndexOf("text=", StringComparison.Ordinal);
        Assert.Equal(originalColumn, writtenColumn);
    }

    /// <summary>⚠️ わざと壊す対 ── 詰め直す余地が無ければ、桁は守れない代わりに
    /// 欄はくっつかない（空白1つへ縮退。#3 の作法のまま）。</summary>
    [Fact]
    public void 足した付け足しの後ろは詰める余地が無ければ空白1つへ縮退する()
    {
        const string original = "a label 0 0 400 40 tap=open text=あきらめる\n";   // 空白1つだけ
        var layout = Layouts.Parse("t", original);
        var node = layout.Roots[0];

        var options = new Dictionary<string, string>(node.Options) { ["when"] = "!done" };
        var changed = new LayoutNode(node.Name, node.Kind, node.Left, node.Top, node.Width, node.Height,
            options, node.Children, node.LineNumber, node.Indent, node.Fields,
            node.Trailing, node.Terminator);

        string written = changed.RenderLine();
        Assert.Contains("when=!done text=あきらめる", written);
    }

    // ── #8 解決済みの木は書き戻せない ────────────────────
    //
    // ⚠️ `Layouts.Resolve` / `Splice` / `Rename` を通した木は、部品が展開済み・冠付きで、
    // 節点の `LineNumber` もすべて -1 になる（差し替えは毎回新しい節点を作り直すため）。
    // ⭐ これを `Write` に渡すと原文に無いものが並ぶので、`Layout.Resolved` を見て断る。

    private static Layout Find(string name, params (string id, string text)[] parts)
    {
        foreach (var p in parts) if (p.id == name) return Layouts.Parse(p.id, p.text);
        return null;
    }

    /// <summary>⭐ 生の木（`Parse` 直後）は解決済みでないので、そのまま書き戻せる。</summary>
    [Fact]
    public void 生の木は解決済みでないので書き戻せる()
    {
        var layout = Layouts.Parse("t", Multi);
        Assert.False(layout.Resolved);
        var written = Layouts.Write(layout);   // 落ちないこと自体が確認
        Assert.Equal(Multi, written);
    }

    /// <summary>⚠️ わざと壊す対 ── `Resolve` を通した木を書き戻そうとすると落ちる。</summary>
    [Fact]
    public void 解決済みの木を書き戻そうとすると落ちる()
    {
        var main = Layouts.Parse("main", "slot box 100 200 400 300 use=part\n");
        var resolved = Layouts.Resolve(main, n => Find(n, ("part", "a label 0 0 400 40\n")));

        Assert.True(resolved.Resolved);
        var ex = Assert.Throws<InvalidOperationException>(() => Layouts.Write(resolved));
        Assert.Contains("解決済み", ex.Message);
    }
}
