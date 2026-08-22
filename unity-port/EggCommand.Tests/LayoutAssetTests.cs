using System;
using System.Collections.Generic;
using System.IO;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>実物の骨組み（`Assets/Resources/Layouts/*.txt`）を全部検査する。
///
/// ⭐ **これがエンジン往復の置き換えです。**⚠️ 今日まで、重なり・はみ出し・
/// 押しどころの大きさは **Unity を起動して Play にして測る**しかありませんでした
/// （無変更でも19秒）。座標がデータに在るなら、ここで数えられます。
///
/// ⚠️ **実物の字幅までは見られません**（描かないと分からない）。
/// ⭐ 見られるのは枠どうしの関係 ── それが不具合の大半でした。</summary>
public class LayoutAssetTests
{
    private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "layouts");

    public static IEnumerable<object[]> All()
    {
        foreach (var path in Directory.GetFiles(Dir, "*.txt"))
            yield return new object[] { Path.GetFileNameWithoutExtension(path) };
    }

    /// <summary>⚠️ 1枚も見つからなければ、**検査が空回りしている**。
    /// ⭐ 「不備 0 件」が「調べていない」を意味しないようにする。</summary>
    [Fact]
    public void 骨組みが見つかる()
    {
        Assert.True(Directory.Exists(Dir), $"{Dir} が無い（csproj のコピー設定を見る）");
        Assert.NotEmpty(Directory.GetFiles(Dir, "*.txt"));
    }

    [Theory]
    [MemberData(nameof(All))]
    public void 不備がない(string id)
    {
        var layout = Layouts.Parse(id, File.ReadAllText(Path.Combine(Dir, id + ".txt")));
        Assert.Equal(new List<string>(), Layouts.Faults(layout));
    }

    /// <summary>⚠️ **下の帯（232）に潜っていないか。**
    ///
    /// ⭐ 今日 BOX で実際に起きた不具合がこれです（一覧が帯の下へ入り、重なり11件）。
    /// ⚠️ 帯を出す画面かどうかは骨組みからは分からないので、
    /// `dock=no` と書いてある画面だけ除きます。</summary>
    [Theory]
    [MemberData(nameof(All))]
    public void 下の帯へ潜っていない(string id)
    {
        const float DockHeight = 232f;
        const float TopBarHeight = 132f;
        float floor = Layouts.ScreenHeight - TopBarHeight - DockHeight;

        var layout = Layouts.Parse(id, File.ReadAllText(Path.Combine(Dir, id + ".txt")));
        foreach (var node in layout.Roots)
        {
            if (node.Option("dock") == "no") continue;
            Assert.True(node.Top + node.Height <= floor + 0.5f,
                $"{id}/{node.Name} が下の帯へ潜っている"
                + $"（下端 {node.Top + node.Height} / 帯の上端 {floor}）");
        }
    }
}
