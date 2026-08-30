using System;
using System.Linq;
using EggCommand.Web;
using Xunit;

namespace EggCommand.Tests;

/// <summary>候補と入力検証は同じ <see cref="UiCommands"/> の登録表から出る。</summary>
public class TapCatalogTests
{
    [Fact]
    public void tap候補は重複せず全て解析できる()
    {
        Assert.Equal(TapCatalog.Names.Length, TapCatalog.Names.Distinct().Count());
        foreach (var name in TapCatalog.Names)
            Assert.True(UiCommands.TryParseTap(name, "0#1", out var command), name + " を解析できない");
    }

    [Fact]
    public void tap入力は一度だけ型と添字へ変換される()
    {
        Assert.True(UiCommands.TryParseTap("nest", "2#1", out var command));
        Assert.Equal(UiActionKind.Nest, command.Kind);
        Assert.Equal("nest", command.Name);
        Assert.Equal("2#1", command.At);
        Assert.Equal(2, command.Index);
    }

    [Fact]
    public void hold専用名と未知のtapは境界で拒否する()
    {
        Assert.False(UiCommands.TryParseTap("detail-s0", "", out _));
        Assert.False(UiCommands.TryParseTap("not-a-command", "", out _));
    }

    [Fact]
    public void 添字を使うtapは負数と非数を入口で拒否する()
    {
        foreach (var name in UiCommands.IndexedTapNames)
        {
            Assert.False(UiCommands.TryParseTap(name, "-1", out _), name + " が負数を受けた");
            Assert.False(UiCommands.TryParseTap(name, "nope", out _), name + " が非数を受けた");
        }
    }

    [Fact]
    public void 固定長tapは巨大な添字を入口で拒否する()
    {
        foreach (var name in UiCommands.BoundedTapNames)
            Assert.False(UiCommands.TryParseTap(name, "999999#1", out _), name + " が巨大な添字を受けた");
    }

    [Fact]
    public void 動的tapは巨大な添字を実行時範囲で拒否できる()
    {
        foreach (var name in UiCommands.DynamicTapNames)
        {
            Assert.True(UiCommands.TryParseTap(name, "999999#1", out var command), name + " の形式まで拒否した");
            Assert.False(UiCommands.IsWithinRange(command, 3), name + " が動的一覧の範囲を越えた");
        }
    }
}
