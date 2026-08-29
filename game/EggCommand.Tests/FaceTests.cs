using System;
using System.Collections.Generic;
using EggCommand.Core;
using EggCommand.Web;
using Xunit;

namespace EggCommand.Tests;

/// <summary>技のラベルの色（作者の指示 2026-08-29「技のラベルはその個体の属性の色に」）
/// の検査。⭐ `Face.cs` は Core にしか依存しないので、`EditAttrs.cs` と同じ形で
/// `EggCommand.Tests.csproj` に直接コンパイルして持ち込んである（写しではない）。
///
/// ⚠️ 見た目そのもの（実物のフォントでどう見えるか）は Playwright の担当。
/// ここで見るのは「色の出所」と「数の上でのコントラスト」の2つだけ。</summary>
public class FaceTests
{
    private static readonly Element[] Elements = { Element.Fire, Element.Wood, Element.Water };

    public static IEnumerable<object[]> AllElements()
    {
        foreach (var e in Elements) yield return new object[] { e };
    }

    private static Creature Make(Element element) =>
        new Creature("t", "tamaru", new StatBlock(20, 20, 20, 20), new StatBlock(0, 0, 0, 0),
            0, 0, null, null, 0, null, null, 1, element: element);

    /// <summary>🔴 **新しい色を作っていない**── `ElementInk` は `ElementCss` の
    /// RGB をそのまま暗くしただけ（46%）であることを、同じ式で作った期待値と
    /// 突き合わせて確かめる。⚠️ ここで別の switch を書いて色を並べると、
    /// 本番側が万一 `ElementCss` から離れて独立した色を持っても検査が気づけない
    /// ── 期待値も `ElementCss` の戻り値から**その場で**計算する。</summary>
    [Theory]
    [MemberData(nameof(AllElements))]
    public void ElementInkはElementCssを暗くしただけ(Element element)
    {
        var (r, g, b) = Rgb(Face.ElementCss(element));
        string expected = $"#{Shade(r):x2}{Shade(g):x2}{Shade(b):x2}";
        Assert.Equal(expected, Face.ElementInk(element));

        static int Shade(int channel) => (int)Math.Round(channel * 0.46);
    }

    /// <summary>⚠️ **読めなくならないこと**を数で確かめる。白い札（戦闘の手札）でも、
    /// 属性色そのままの札（BOX の s0/s1/s2 の地）でも、WCAG のコントラスト比
    /// 3:1（大きい字の下限）を上回ることを見る。</summary>
    [Theory]
    [MemberData(nameof(AllElements))]
    public void ElementInkは白地でも属性色の地でも読める(Element element)
    {
        var ink = Rgb(Face.ElementInk(element));
        var white = (r: 255, g: 255, b: 255);
        var card = Rgb(Face.ElementCss(element));

        double onWhite = Contrast(ink, white);
        double onCard = Contrast(ink, card);
        Assert.True(onWhite >= 4.5, $"{element}: 白地でのコントラストが低い（{onWhite:0.00}:1）");
        Assert.True(onCard >= 3.0, $"{element}: 属性色の地でのコントラストが低い（{onCard:0.00}:1）");
    }

    /// <summary>BOX の札（`panel.txt` の s0name/s1name/s2name）は `Face.Tint` 経由で
    /// `ElementInk` から塗られ、地（s0/s1/s2 のカード）は今までどおり生の
    /// `ElementCss` のまま ── 2つを混同していないことの配線チェック。</summary>
    [Theory]
    [MemberData(nameof(AllElements))]
    public void BOXの技ラベルはElementInkから地はElementCssから塗る(Element element)
    {
        var face = new Face(Make(element));
        Assert.Equal(Face.ElementInk(element), face.Tint("s0name"));
        Assert.Equal(Face.ElementInk(element), face.Tint("s1name"));
        Assert.Equal(Face.ElementInk(element), face.Tint("s2name"));
        Assert.Equal(Face.ElementCss(element), face.Tint("s0"));
        Assert.Equal(Face.ElementCss(element), face.Tint("elem"));
    }

    private static (int r, int g, int b) Rgb(string hex)
    {
        hex = hex.TrimStart('#');
        return (
            Convert.ToInt32(hex.Substring(0, 2), 16),
            Convert.ToInt32(hex.Substring(2, 2), 16),
            Convert.ToInt32(hex.Substring(4, 2), 16));
    }

    /// <summary>WCAG のコントラスト比。⚠️ 検査専用の実装 ── 本番コードには置かない
    /// （本番は塗るだけでよい。「読めるか」を測るのはここだけの仕事）。</summary>
    private static double Contrast((int r, int g, int b) a, (int r, int g, int b) b)
    {
        double la = Luminance(a) + 0.05, lb = Luminance(b) + 0.05;
        return Math.Max(la, lb) / Math.Min(la, lb);
    }

    private static double Luminance((int r, int g, int b) c)
    {
        double Lin(int v)
        {
            double x = v / 255.0;
            return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Lin(c.r) + 0.7152 * Lin(c.g) + 0.0722 * Lin(c.b);
    }
}
