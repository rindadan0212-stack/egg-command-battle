using System;
using System.Collections.Generic;
using System.IO;
using EggCommand.Core;
using EggCommand.Web;
using Xunit;

namespace EggCommand.Tests;

/// <summary>骨組みエディタ E2「冠の逆算」（<see cref="TapCrowns"/>）── 実物の骨組み
/// （`Assets/Resources/Layouts/*.txt`）を解決して、`use=` の冠が正しく拾えているかを
/// 固定する。⚠️ `LayoutAssetTests.Read` と同じ読み方（`layouts\*.txt` は
/// `EggCommand.Tests.csproj` が既にコピー済み）。</summary>
public class TapCrownsTests
{
    private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "layouts");

    private static Layout Read(string id)
    {
        var raw = Layouts.Parse(id, File.ReadAllText(Path.Combine(Dir, id + ".txt")));
        return Layouts.Resolve(raw, name =>
        {
            var path = Path.Combine(Dir, name + ".txt");
            return File.Exists(path) ? Layouts.Parse(name, File.ReadAllText(path)) : null;
        });
    }

    private static List<Layout> AllResolved()
    {
        var list = new List<Layout>();
        foreach (var path in Directory.GetFiles(Dir, "*.txt"))
            list.Add(Read(Path.GetFileNameWithoutExtension(path)));
        return list;
    }

    /// <summary>⭐ 計画・作業指示の例そのもの: `cell` は `box`/`breed`（cellA/cellB）・
    /// `fuse`（cell）・`party`/`partyidle`（cellA/cellB）で差されている。⚠️ 出現順
    /// （ファイル名のアルファベット順: box→breed→fuse→party→partyidle）で
    /// `cellA`,`cellB`,`cell` の3つ・重複無し。</summary>
    [Fact]
    public void cellの冠はcellA_cellB_cellの3つ()
    {
        var crowns = TapCrowns.Crowns(AllResolved(), "cell");
        Assert.Equal(new[] { "cellA", "cellB", "cell" }, crowns);
    }

    /// <summary>⭐ 作業指示の例そのもの: `sortbar` は `box`/`breed`/`party`/`partyidle` の
    /// どこでも同じ名前「bar」で差されている ── 冠は1つだけ。</summary>
    [Fact]
    public void sortbarの冠はbarの1つ()
    {
        var crowns = TapCrowns.Crowns(AllResolved(), "sortbar");
        Assert.Equal(new[] { "bar" }, crowns);
    }

    /// <summary>⭐ `unit`/`square`/`walker`/`frame` はコードから直接描かれる4枚
    /// （`use=` を一度も通らない）── 冠は逆算できない（空）。</summary>
    [Theory]
    [InlineData("unit")]
    [InlineData("square")]
    [InlineData("walker")]
    [InlineData("frame")]
    public void useを一度も通らない部品は冠が空(string partId)
    {
        Assert.Empty(TapCrowns.Crowns(AllResolved(), partId));
    }

    /// <summary>⚠️ 存在しない partId でも例外を投げず、空を返す。</summary>
    [Fact]
    public void 存在しないpartIdは空()
    {
        Assert.Empty(TapCrowns.Crowns(AllResolved(), "no-such-part"));
    }
}
