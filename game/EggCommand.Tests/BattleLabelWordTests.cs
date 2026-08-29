using System;
using System.IO;
using Xunit;

namespace EggCommand.Tests;

/// <summary>戦闘の手札の技名（`s0name`/`s1name`/`s2name`）が、手札の主（`hand`）の
/// 属性色で塗られているかの見張り（作者の指示 2026-08-29「技のラベルはその個体の
/// 属性の色に」）。BOX 側（`panel.txt`）は `FaceTests` が実物を動かして検査するが、
/// `Sheets.cs` は `Shell`/`Face`/`LayoutDom` 等 Web 専用の依存が多くコンパイルには
/// 持ち込めない（`BattleWordTests` と同じ理由）ので、こちらは
/// `websrc\Sheets.cs` をテキストとして読み直す。</summary>
public class BattleLabelWordTests
{
    private static readonly string WebSrc = Path.Combine(AppContext.BaseDirectory, "websrc");
    private static string Sheets() => File.ReadAllText(Path.Combine(WebSrc, "Sheets.cs"));

    [Fact]
    public void 検査するものが在る()
    {
        Assert.Contains("public static string Fight(Shell s)", Sheets());
    }

    /// <summary>🔴 `Fight` の中の `Tint` が、技名の字（`...name` で終わる bind）を
    /// `Face.ElementInk(hand.Creature.Element)` から塗っていること。
    /// ⚠️ 生の `Face.ElementCss` を字にそのまま使っていないこと（白い札の上で
    /// 薄い色は読めない ── `FaceTests.ElementInkは白地でも属性色の地でも読める` の
    /// 実測が裏付ける）。</summary>
    [Fact]
    public void 戦闘の技ラベルはFaceのElementInkから塗る()
    {
        string src = Sheets();
        int start = src.IndexOf("public static string Fight(Shell s)", StringComparison.Ordinal);
        Assert.True(start >= 0, "Sheets.cs: Fight(Shell s) が見つからない");
        int end = src.IndexOf("private static string Column(", start, StringComparison.Ordinal);
        Assert.True(end > start, "Sheets.cs: Column( が見つからない（探索範囲の終端が決められない）");
        string body = src.Substring(start, end - start);

        Assert.Contains("Face.ElementInk(hand.Creature.Element)", body);
        Assert.DoesNotContain("Face.ElementCss(hand.Creature.Element)", body);
    }
}
