using System;
using System.Linq;
using EggCommand.Web;
using Xunit;

namespace EggCommand.Tests;

/// <summary>長押し候補と入力検証は <see cref="UiCommands"/> の同じ登録表を使う。</summary>
public class HoldCatalogTests
{
    [Fact]
    public void hold候補は重複せず全て解析できる()
    {
        Assert.Equal(HoldCatalog.Names.Length, HoldCatalog.Names.Distinct().Count());
        foreach (var name in HoldCatalog.Names)
            Assert.True(UiCommands.TryParseHold(name, "1#2", out var command), name + " を解析できない");
    }

    [Fact]
    public void hold入力は一度だけ型と添字へ変換される()
    {
        Assert.True(UiCommands.TryParseHold("detail-s1", "1#2", out var command));
        Assert.Equal(UiActionKind.DetailS1, command.Kind);
        Assert.Equal(1, command.Index);
    }

    [Fact]
    public void tap専用名と未知のholdは境界で拒否する()
    {
        Assert.False(UiCommands.TryParseHold("nest", "", out _));
        Assert.False(UiCommands.TryParseHold("not-a-command", "", out _));
    }

    [Fact]
    public void 添字を使うholdは負数と非数を入口で拒否する()
    {
        foreach (var name in UiCommands.IndexedHoldNames)
        {
            Assert.False(UiCommands.TryParseHold(name, "-1", out _), name + " が負数を受けた");
            Assert.False(UiCommands.TryParseHold(name, "nope", out _), name + " が非数を受けた");
        }
    }

    [Fact]
    public void 動的holdは巨大な添字を実行時範囲で拒否できる()
    {
        foreach (var name in UiCommands.DynamicHoldNames)
        {
            Assert.True(UiCommands.TryParseHold(name, "999999#1", out var command), name + " の形式まで拒否した");
            Assert.False(UiCommands.IsWithinRange(command, 3), name + " が動的一覧の範囲を越えた");
        }
    }
}
