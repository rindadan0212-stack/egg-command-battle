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
    /// ⚠️ 生の `Face.ElementCss` を**字に**そのまま使っていないこと（白い札の上でも
    /// 属性色の地の上でも薄い色は読めない ──
    /// `FaceTests.ElementInkは白地でも属性色の地でも読める` の実測が裏付ける）。
    ///
    /// 🔴 **2026-08-29 に「字だけ」へ絞り込んだ。**⚠️ 前は `Face.ElementCss(...)` を
    /// `Fight` の中で**一度でも**使ったら落ちる形だったが、作者の指示「（技の札を）
    /// 属性の色に」で**札の地**がその色になった ── 禁じたかったのは「字を薄い色で塗る」
    /// ことだけで、地に使うのは BOX の札（`panel.txt` の s0/s1/s2・`Face.Tint`）と
    /// 同じ約束のほうが正しい。⭐ だから枝ごとに見る。</summary>
    [Fact]
    public void 戦闘の技ラベルはFaceのElementInkから塗る()
    {
        string src = Sheets();
        int start = src.IndexOf("public static string Fight(Shell s)", StringComparison.Ordinal);
        Assert.True(start >= 0, "Sheets.cs: Fight(Shell s) が見つからない");
        int end = src.IndexOf("private static string Column(", start, StringComparison.Ordinal);
        Assert.True(end > start, "Sheets.cs: Column( が見つからない（探索範囲の終端が決められない）");
        string body = src.Substring(start, end - start);

        // ⭐ 字（`...name`）は読める濃さのほう ── 枝と色を1つの綴りで固定する
        Assert.Contains("key.EndsWith(\"name\") && hand != null ? Face.ElementInk(hand.Creature.Element)", body);
        // ⚠️ 字を生の（薄い）属性色で塗る枝が現れていないこと
        Assert.DoesNotContain("key.EndsWith(\"name\") && hand != null ? Face.ElementCss", body);
    }

    /// <summary>🔴 技の札の**地**が属性の色（生の `Face.ElementCss`）であること
    /// （2026-08-29・作者の指示「属性の色に」）。
    /// ⭐ BOX の札（`panel.txt` の s0/s1/s2）は前からそう塗っていて、
    /// 「属性の丸と技の札の地は同じ色」という約束の**戦闘側だけが抜けていた**。
    /// ⚠️ `Slot(key)` が `(番号, "")` を返すのは札そのもの（`s0`/`s1`/`s2`）だけ
    /// ── `s0name`/`s0ct`/`s0lv` は第2要素が空でないので、ここには来ない。</summary>
    [Fact]
    public void 戦闘の技の札の地は属性の色()
    {
        string src = Sheets();
        int start = src.IndexOf("public static string Fight(Shell s)", StringComparison.Ordinal);
        Assert.True(start >= 0, "Sheets.cs: Fight(Shell s) が見つからない");
        int end = src.IndexOf("private static string Column(", start, StringComparison.Ordinal);
        Assert.True(end > start, "Sheets.cs: Column( が見つからない（探索範囲の終端が決められない）");
        string body = src.Substring(start, end - start);

        Assert.Contains("Slot(key) is (int, \"\") && hand != null ? Face.ElementCss(hand.Creature.Element)", body);
    }
}
