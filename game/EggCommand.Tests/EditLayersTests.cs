using System.Collections.Generic;
using EggCommand.Core;
using EggCommand.Web;
using Xunit;

namespace EggCommand.Tests;

/// <summary>骨組みエディタ E2「層」（<see cref="EditLayers"/>）── 計画 §11-2 の見分け方
/// どおりに節点を4つへ振り分けているかを固定する。
///
/// ⚠️ `EditLayers.cs` は `EggCommand.Web` プロジェクトに置いてあるが、`EditAttrs.cs`/
/// `EditAlign.cs` と同じ理由（`dotnet test` が Web を建てない約束）で、
/// `EggCommand.Tests.csproj` の `&lt;Compile Include&gt;` で直接コンパイルしている。</summary>
public class EditLayersTests
{
    private static LayoutNode Node(string kind, params (string Key, string Value)[] options)
    {
        var dict = new Dictionary<string, string>();
        foreach (var (k, v) in options) dict[k] = v;
        return new LayoutNode("t", kind, 0, 0, 10, 10, dict, new List<LayoutNode>());
    }

    [Fact]
    public void paint種類は絵層()
    {
        Assert.Equal(EditLayer.Paint, EditLayers.Of(Node("paint")));
    }

    [Theory]
    [InlineData("pixel")]
    [InlineData("icon")]
    [InlineData("bar")]
    [InlineData("label")]
    public void 種類だけで動く物になるもの(string kind)
    {
        Assert.Equal(EditLayer.Dynamic, EditLayers.Of(Node(kind)));
    }

    [Fact]
    public void bindを持つcardは動く物()
    {
        Assert.Equal(EditLayer.Dynamic, EditLayers.Of(Node("card", ("bind", "art"))));
    }

    [Fact]
    public void repeatを持つcardは動く物()
    {
        Assert.Equal(EditLayer.Dynamic, EditLayers.Of(Node("card", ("repeat", "box"))));
    }

    [Fact]
    public void tapを持つ節点は押しどころ()
    {
        Assert.Equal(EditLayer.Tap, EditLayers.Of(Node("card", ("tap", "one"))));
    }

    [Fact]
    public void holdを持つ節点は押しどころ()
    {
        Assert.Equal(EditLayer.Tap, EditLayers.Of(Node("button", ("hold", "skill1"))));
    }

    /// <summary>⭐ 実物にある組合せ（`bgrow button ... tap=grow lead=yes bind=grow`）。
    /// ⚠️ **`tap` を `bind` より先に見る**（このプロジェクトの判断・報告に明記の優先順位）
    /// ── 押しどころの層（囲んで作る・機能の付け替え）が実際のボタンを取りこぼさない。</summary>
    [Fact]
    public void tapとbindの両方を持つ釦は押しどころが勝つ()
    {
        Assert.Equal(EditLayer.Tap, EditLayers.Of(Node("button", ("tap", "grow"), ("bind", "grow"))));
    }

    /// <summary>⭐ 実物にある組合せ（`slot paint ... repeat=slots tap=slot use=slot pic=slot-frame`）。
    /// ⚠️ **`paint` を最優先に見る**（絵の配置作業を層の切替で妨げないため）。</summary>
    [Fact]
    public void paintとtapの両方を持つ節点は絵が勝つ()
    {
        Assert.Equal(EditLayer.Paint, EditLayers.Of(Node("paint", ("tap", "slot"), ("repeat", "slots"))));
    }

    [Theory]
    [InlineData("box")]
    [InlineData("card")]
    [InlineData("line")]
    [InlineData("round")]
    [InlineData("scroll")]
    [InlineData("veil")]
    [InlineData("host")]
    public void 何も持たない節点は入れ物(string kind)
    {
        Assert.Equal(EditLayer.Container, EditLayers.Of(Node(kind)));
    }

    [Fact]
    public void 層の帯はすべてを含めて5つ()
    {
        Assert.Equal(5, EditLayers.Switcher.Length);
        Assert.Null(EditLayers.Switcher[0]);
    }

    [Fact]
    public void トークンは往復する()
    {
        foreach (var layer in EditLayers.Switcher)
        {
            string token = EditLayers.Token(layer);
            if (layer is null) Assert.Equal("", token);
            else Assert.NotEqual("", token);
        }
    }

    [Theory]
    [InlineData("paint", EditLayer.Paint)]
    [InlineData("label", EditLayer.Dynamic)]
    [InlineData("icon", EditLayer.Dynamic)]
    [InlineData("box", EditLayer.Container)]
    [InlineData("card", EditLayer.Container)]
    [InlineData("line", EditLayer.Container)]
    [InlineData("round", EditLayer.Container)]
    public void 道具箱の並びは種類だけで決まる(string kind, EditLayer expect)
    {
        Assert.Equal(expect, EditLayers.PaletteLayerOf(kind));
    }

    /// <summary>⭐ `button`（押しどころ）は道具箱に出さない ── 「囲んで作る」だけが道具
    /// （計画 §11-6）。</summary>
    [Fact]
    public void buttonは道具箱に出さない()
    {
        Assert.Null(EditLayers.PaletteLayerOf("button"));
    }
}
