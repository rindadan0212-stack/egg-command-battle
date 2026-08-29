using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EggCommand.Core;
using EggCommand.Web;
using Xunit;

namespace EggCommand.Tests;

/// <summary>骨組みエディタ P4 ── `when=` の候補（<see cref="WhenNames.Of"/>）が
/// **実物の骨組みから**集められていることを固定する。
///
/// 🔴 これが「決め打ちの一覧を持たない」の証明 ── 骨組みを増減させた**仮のデータ**で
/// 候補が増減することを見る。手で書いた一覧を返す実装に差し替えたら、この検査が落ちる。</summary>
public class WhenNamesTests
{
    private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "layouts");

    private static Layout Parse(string src) => Layouts.Parse("t", src);

    /// <summary>🔴 骨組みを足すと候補が増える（＝実物を数えている）。</summary>
    [Fact]
    public void 骨組みを足すと候補が増える()
    {
        var one = Parse("a label 0 0 100 40 when=有る");
        Assert.Equal(new[] { "有る" }, WhenNames.Of(new[] { one }));

        var two = Parse("b label 0 0 100 40 when=無い");
        Assert.Equal(new[] { "有る", "無い" }, WhenNames.Of(new[] { one, two }));

        // ⚠️ 渡さなければ出ない ── 一覧を内側に隠し持っていないことの裏取り
        Assert.Equal(new[] { "無い" }, WhenNames.Of(new[] { two }));
    }

    /// <summary>⚠️ `!`（偽のとき出す）は名前から落とす ── エディタでは反転を別の切替が
    /// 持つので、候補に `!有る` と `有る` の2つを並べない。</summary>
    [Fact]
    public void 反転の印は候補に混ぜない()
    {
        var layout = Parse("a label 0 0 100 40 when=!有る\nb label 0 60 100 40 when=有る");
        Assert.Equal(new[] { "有る" }, WhenNames.Of(new[] { layout }));
    }

    /// <summary>⚠️ 子・孫まで数える（Inspector で選ぶのは深い節点のほうが多い）。</summary>
    [Fact]
    public void 入れ子の中も数える()
    {
        var layout = Parse("a box 0 0 100 100\n  b box 0 0 50 50 when=中\n    c label 0 0 20 20 when=奥");
        Assert.Equal(new[] { "中", "奥" }, WhenNames.Of(new[] { layout }));
    }

    /// <summary>⚠️ 空の名前（`when=` だけ・`when=!` だけ ── `FaultKind.EmptyWhenName` の
    /// 不備）は候補に入れない。⭐ 不備を勧めない。</summary>
    [Fact]
    public void 空の条件名は候補に入れない()
    {
        var layout = Parse("a label 0 0 100 40 when=!\nb label 0 60 100 40 when=有る");
        Assert.Equal(new[] { "有る" }, WhenNames.Of(new[] { layout }));
    }

    /// <summary>⚠️ 重複は畳み、あいうえお順（序数）で返す ── 同じ名前が2度出ない。</summary>
    [Fact]
    public void 重複は畳んで並べ替える()
    {
        var layout = Parse("a label 0 0 100 40 when=b\nb label 0 60 100 40 when=a\nc label 0 120 100 40 when=b");
        Assert.Equal(new[] { "a", "b" }, WhenNames.Of(new[] { layout }));
    }

    /// <summary>⭐ 実物でも効くこと ── `battle.txt` の技札は `when=s0`〜`s2` を持つ。
    /// ⚠️ 数を固定しない（骨組みは育つ）── 「実物から拾えている」ことだけを見る。</summary>
    [Fact]
    public void 実物の骨組みからも拾える()
    {
        var battle = Layouts.Parse("battle", File.ReadAllText(Path.Combine(Dir, "battle.txt")));
        var names = WhenNames.Of(new[] { battle });
        Assert.Contains("s0", names);
        Assert.Contains("s1", names);
        Assert.Contains("s2", names);
        // ⚠️ 反転（`when=!foe` 等）が混ざっていても、頭の `!` は落ちている
        Assert.DoesNotContain(names, n => n.StartsWith("!", StringComparison.Ordinal));
    }
}
