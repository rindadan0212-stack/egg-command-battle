using System;
using System.IO;
using EggCommand.Core;
using EggCommand.Web;
using Xunit;

namespace EggCommand.Tests;

/// <summary>家系図（BOX の4つ目の釦「家系図」）の画面側の配線を見張る。
///
/// ⚠️ `Shell.cs`/`Sheets.cs` は `LayoutDom`/`Deeds`/`Filters` 等 Web 専用の依存が多く
/// コンパイルには持ち込めない（`TapCatalogTests`/`GrowSpendTests` と同じ理由）ので、
/// ソースをテキストとして読み直して確かめる。⭐ `TapCatalog.cs`/`Face.cs` は
/// Core にしか依存しないので直接コンパイルされている（csproj 参照） ── そちらは
/// 型として直に使う。</summary>
public class LineageWebTests
{
    private static readonly string WebSrc = Path.Combine(AppContext.BaseDirectory, "websrc");
    private static readonly string LayoutDir = Path.Combine(AppContext.BaseDirectory, "layouts");

    private static string ShellSource => File.ReadAllText(Path.Combine(WebSrc, "Shell.cs"));
    private static string SheetsSource => File.ReadAllText(Path.Combine(WebSrc, "Sheets.cs"));
    private static string BoxLayout => File.ReadAllText(Path.Combine(LayoutDir, "box.txt"));
    private static string TreeLayout => File.ReadAllText(Path.Combine(LayoutDir, "tree.txt"));

    [Fact]
    public void 元になるファイルが読める()
    {
        Assert.True(Directory.Exists(WebSrc), $"{WebSrc} が無い（csproj のコピー設定を見る）");
        Assert.True(File.Exists(Path.Combine(WebSrc, "Shell.cs")), "Shell.cs が無い");
        Assert.True(File.Exists(Path.Combine(WebSrc, "Sheets.cs")), "Sheets.cs が無い");
        Assert.True(File.Exists(Path.Combine(LayoutDir, "box.txt")), "box.txt が無い");
        Assert.True(File.Exists(Path.Combine(LayoutDir, "tree.txt")), "tree.txt が無い");
    }

    // ── TapCatalog（直にコンパイルされている型を使う） ─────────

    [Fact]
    public void TapCatalogにtreeが載っている()
    {
        Assert.Contains("tree", TapCatalog.Names);
    }

    // ── Shell.cs の配線 ──────────────────────────────

    [Fact]
    public void PanelにTreeがある()
    {
        Assert.Contains("public enum Panel { None, Party, Species, Skill, Eggs, Fuse, Train, Ask, Keep, Grow, Tree }",
            ShellSource.Replace("\r\n", "\n"));
    }

    /// <summary>🔴 `tap=tree` を押すと `Panel.Tree` が開く。</summary>
    [Fact]
    public void tapのtreeはPanelTreeを開く()
    {
        int at = ShellSource.IndexOf("case \"tree\":", StringComparison.Ordinal);
        Assert.True(at >= 0, "Shell.cs: case \"tree\": が見つからない");
        string tap = ShellSource.Substring(at, Math.Min(120, ShellSource.Length - at));
        Assert.Contains("Open = Panel.Tree", tap);
    }

    // ── Sheets.cs の Box() ── 2世代未満は押せない ─────────────

    /// <summary>⚠️ **`Generation < 2` の個体では家系図の釦が押せない**
    /// （作者の指示どおり ── 2世代未満は墓標を辿っても親が居ないので、押しても
    /// 「不明」しか出ない）。⭐ `Sheets.Box` の `Tappable` が唯一の判断口。</summary>
    [Fact]
    public void BoxのtreeはGeneration2未満で押せない()
    {
        int at = SheetsSource.IndexOf("public static string Box(Shell s)", StringComparison.Ordinal);
        Assert.True(at >= 0, "Sheets.cs: Box(Shell s) が見つからない");
        // ⚠️ 次の画面関数（配合）の手前までを Box() の中身とみなす
        int end = SheetsSource.IndexOf("public static string Breed(Shell s)", at, StringComparison.Ordinal);
        Assert.True(end > at, "Sheets.cs: Breed(Shell s) が見つからない（探索範囲の終端が決められない）");
        string body = SheetsSource.Substring(at, end - at);

        Assert.Contains("\"tree\" => picked.Generation >= 2", body);
    }

    /// <summary>⭐ `Sheets.Tree` 自体が居ること（本体）。</summary>
    [Fact]
    public void SheetsにTree関数がある()
    {
        Assert.Contains("public static string Tree(Shell s, string crown = \"\")", SheetsSource);
    }

    // ── box.txt ── 4つ目の釦 ─────────────────────────

    [Fact]
    public void boxtxtに家系図の釦がある()
    {
        Assert.Contains("tap=tree", BoxLayout);
        Assert.Contains("text=家系図", BoxLayout);
    }

    /// <summary>⚠️ 3本 → 4本に割り直した後も、はみ出し・重なりは無い
    /// （`LayoutAssetTests.不備がない` が実物で見るので、ここは「4本ある」ことだけ見る）。</summary>
    [Fact]
    public void boxtxtの詳細札は釦4本()
    {
        int count = 0;
        foreach (var line in BoxLayout.Split('\n'))
            if (line.Contains(" button ") && line.Contains("tap=")) count++;
        Assert.Equal(4, count);
    }

    // ── tree.txt ── 骨組みの形そのもの ─────────────────────

    /// <summary>⭐ 骨組み（`Core.Layouts`）は直にコンパイルされているので、
    /// 実際に `Parse` して構造を確かめられる（テキスト一致より確実）。
    /// ⚠️ 7枚（自分1・親2・祖父母4）すべてに name/sub/gen の3つの bind があること、
    /// 「不明」の札も同じ大きさで出す設計どおり `when=` で隠していないことを見る。</summary>
    [Fact]
    public void treetxtは7枚ぶんの節点をwhenで隠さず持つ()
    {
        var layout = Layouts.Parse("tree", TreeLayout);
        // ⚠️ `dim`（veil）と `panel` は**兄弟**（どちらも字下げ0の根）── 親子ではない
        LayoutNode? panel = null;
        foreach (var root in layout.Roots) if (root.Name == "panel") panel = root;
        Assert.True(panel != null, "tree.txt に panel が無い");

        for (int i = 0; i < 7; i++)
        {
            string name = "n" + i;
            LayoutNode? node = null;
            foreach (var child in panel!.Children) if (child.Name == name) node = child;
            Assert.True(node != null, $"{name} が tree.txt に無い");

            // 🔴 「不明」の札も同じ大きさで出す ── when= で条件つき表示にしない
            Assert.Null(node!.Option("when"));

            var binds = new System.Collections.Generic.HashSet<string>();
            foreach (var child in node.Children)
            {
                var bind = child.Option("bind");
                if (bind != null) binds.Add(bind);
            }
            Assert.Contains(name + "name", binds);
            Assert.Contains(name + "sub", binds);
            Assert.Contains(name + "gen", binds);
        }
    }

    /// <summary>⚠️ 押しどころは「閉じる」だけ ── 家系図は読む場所であって選ぶ場所ではない。</summary>
    [Fact]
    public void treetxtの押しどころは閉じるだけ()
    {
        var layout = Layouts.Parse("tree", TreeLayout);
        int taps = 0;
        foreach (var root in layout.Roots) CountTaps(root, ref taps);
        Assert.Equal(1, taps);
        Assert.Contains("tap=close", TreeLayout);
    }

    private static void CountTaps(LayoutNode node, ref int count)
    {
        if (node.Option("tap") != null) count++;
        foreach (var child in node.Children) CountTaps(child, ref count);
    }

    /// <summary>🔴 **開く札には、必ず中身がある。**
    ///
    /// ⚠️ 2026-08-29 に実際に踏んだ: `Panel.Tree` を足して釦も繋いだのに、
    /// `AppPage.Card(Panel, string)` の表に足し忘れていて、**押せるのに何も出なかった**
    /// （札を作った側が `AppPage.razor` を触れず、繋ぎが片側だけ残った）。
    /// ⭐ 個別に見張るのではなく、**`Panel` の全部**を機械的に突き合わせる ──
    /// 次に札を増やす人も、足し忘れたらここで落ちる。</summary>
    [Fact]
    public void どのPanelにも中身が繋がっている()
    {
        // ⚠️ `Panel` は `Shell.cs` に在り、検査からはコンパイルできない（このファイルの
        //    冒頭に書いてある理由）── ⭐ 一覧も**ソースの字から**読む。
        var m = System.Text.RegularExpressions.Regex.Match(ShellSource,
            @"enum Panel\s*\{([^}]*)\}");
        Assert.True(m.Success, "Shell.cs: enum Panel が見つからない（検査の前提が崩れた）");
        var 札 = m.Groups[1].Value.Split(',');

        string app = File.ReadAllText(Path.Combine(WebSrc, "AppPage.razor"));
        var 抜け = new System.Collections.Generic.List<string>();
        foreach (var raw in 札)
        {
            string which = raw.Trim();
            if (which.Length == 0 || which == "None") continue;   // ⚠️ 「開いていない」は札ではない
            if (!app.Contains($"Panel.{which} =>", StringComparison.Ordinal)) 抜け.Add(which);
        }
        Assert.True(札.Length > 3, "札の一覧が読めていない（検査が空回り）");
        Assert.True(抜け.Count == 0,
            "AppPage.Card の表に中身が無い札: " + string.Join(", ", 抜け)
            + " ── 押せるのに何も出ない（2026-08-29 に Tree で踏んだ形）");
    }
}
