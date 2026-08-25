using System.Collections.Generic;
using EggCommand.Web;
using Xunit;

namespace EggCommand.Tests;

/// <summary>骨組みエディタ 段階2 Pass A「揃える・等間隔」（<see cref="EditAlign"/>）の
/// 固定入力での検査。
///
/// ⚠️ `EditAlign.cs` は `EggCommand.Web` プロジェクトに置いてあるが、`EditAttrsTests` と
/// 同じ理由（`dotnet test` が Web を建てない約束）で `EggCommand.Tests.csproj` の
/// `&lt;Compile Include&gt;` で直接コンパイルしている（ProjectReference は張らない）。</summary>
public class EditAlignTests
{
    private static List<(int Line, float Left, float Top, float Width, float Height)> Nodes(
        params (int Line, float Left, float Top, float Width, float Height)[] items) =>
        new(items);

    [Fact]
    public void 左揃えは最小Leftへ集める()
    {
        var nodes = Nodes((1, 10f, 0f, 20f, 20f), (2, 50f, 0f, 20f, 20f), (3, 30f, 0f, 20f, 20f));
        var result = EditAlign.AlignLeft(nodes);

        Assert.Equal(2, result.Count);
        Assert.False(result.ContainsKey(1));   // ⚠️ 既に最小（10）── 変わらないので含めなくてよい
        Assert.Equal((10f, 0f), result[2]);
        Assert.Equal((10f, 0f), result[3]);
    }

    [Fact]
    public void 右揃えは最大Rightへ右端を集める()
    {
        var nodes = Nodes((1, 0f, 0f, 10f, 10f), (2, 0f, 0f, 30f, 10f), (3, 0f, 0f, 20f, 10f));
        var result = EditAlign.AlignRight(nodes);

        Assert.Equal(2, result.Count);
        Assert.False(result.ContainsKey(2));   // ⚠️ 右端が既に30（最大）── 変わらない
        Assert.Equal((20f, 0f), result[1]);   // 30 - 10
        Assert.Equal((10f, 0f), result[3]);   // 30 - 20
    }

    [Fact]
    public void 上揃えは最小Topへ集める()
    {
        var nodes = Nodes((1, 0f, 10f, 10f, 20f), (2, 0f, 50f, 10f, 20f), (3, 0f, 30f, 10f, 20f));
        var result = EditAlign.AlignTop(nodes);

        Assert.Equal(2, result.Count);
        Assert.False(result.ContainsKey(1));
        Assert.Equal((0f, 10f), result[2]);
        Assert.Equal((0f, 10f), result[3]);
    }

    [Fact]
    public void 下揃えは最大Bottomへ下端を集める()
    {
        var nodes = Nodes((1, 0f, 0f, 10f, 10f), (2, 0f, 0f, 10f, 30f), (3, 0f, 0f, 10f, 20f));
        var result = EditAlign.AlignBottom(nodes);

        Assert.Equal(2, result.Count);
        Assert.False(result.ContainsKey(2));   // ⚠️ 下端が既に30（最大）── 変わらない
        Assert.Equal((0f, 20f), result[1]);   // 30 - 10
        Assert.Equal((0f, 10f), result[3]);   // 30 - 20
    }

    [Fact]
    public void 左右中央はbbox中心xへ集める()
    {
        var nodes = Nodes((1, 0f, 0f, 20f, 10f), (2, 100f, 0f, 20f, 10f));
        var result = EditAlign.AlignCenterX(nodes);

        // bbox: minLeft=0, maxRight=120 → centerX=60 → 各 Left = 60 - width/2 = 50
        Assert.Equal(2, result.Count);
        Assert.Equal((50f, 0f), result[1]);
        Assert.Equal((50f, 0f), result[2]);
    }

    [Fact]
    public void 上下中央はbbox中心yへ集める()
    {
        var nodes = Nodes((1, 0f, 0f, 10f, 20f), (2, 0f, 100f, 10f, 20f));
        var result = EditAlign.AlignCenterY(nodes);

        // bbox: minTop=0, maxBottom=120 → centerY=60 → 各 Top = 60 - height/2 = 50
        Assert.Equal(2, result.Count);
        Assert.Equal((0f, 50f), result[1]);
        Assert.Equal((0f, 50f), result[2]);
    }

    [Fact]
    public void 横の等間隔は両端を固定して間を均等に詰め直す()
    {
        var nodes = Nodes((1, 0f, 0f, 10f, 10f), (2, 15f, 0f, 10f, 10f), (3, 100f, 0f, 10f, 10f));
        var result = EditAlign.DistributeH(nodes);

        // span = 110 - 0 = 110、Σwidth = 30、gap = 80 / 2 = 40
        // x: 1→0(不変) 2→50(変わる) 3→100(不変)
        Assert.Single(result);
        Assert.Equal((50f, 0f), result[2]);
    }

    [Fact]
    public void 縦の等間隔は両端を固定して間を均等に詰め直す()
    {
        var nodes = Nodes((1, 0f, 0f, 10f, 10f), (2, 0f, 15f, 10f, 10f), (3, 0f, 100f, 10f, 10f));
        var result = EditAlign.DistributeV(nodes);

        // span = 110 - 0 = 110、Σheight = 30、gap = 80 / 2 = 40
        Assert.Single(result);
        Assert.Equal((0f, 50f), result[2]);
    }

    [Fact]
    public void 横の等間隔は2節点では空を返す()
    {
        var nodes = Nodes((1, 0f, 0f, 10f, 10f), (2, 50f, 0f, 10f, 10f));
        Assert.Empty(EditAlign.DistributeH(nodes));
    }

    [Fact]
    public void 縦の等間隔は2節点では空を返す()
    {
        var nodes = Nodes((1, 0f, 0f, 10f, 10f), (2, 0f, 50f, 10f, 10f));
        Assert.Empty(EditAlign.DistributeV(nodes));
    }

    [Fact]
    public void 既に揃っている入力は空を返す()
    {
        var nodes = Nodes((1, 5f, 0f, 10f, 10f), (2, 5f, 0f, 20f, 10f), (3, 5f, 0f, 30f, 10f));
        Assert.Empty(EditAlign.AlignLeft(nodes));
    }

    [Fact]
    public void 既に等間隔の入力は空を返す()
    {
        // 0, 20, 40（幅10・gap10）── 詰め直しても同じ位置に戻る
        var nodes = Nodes((1, 0f, 0f, 10f, 10f), (2, 20f, 0f, 10f, 10f), (3, 40f, 0f, 10f, 10f));
        Assert.Empty(EditAlign.DistributeH(nodes));
    }
}
