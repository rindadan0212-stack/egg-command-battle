using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EggCommand.Web;
using Xunit;

namespace EggCommand.Tests;

/// <summary>骨組みエディタ E2「機能（`tap=`）の付け替え」── `TapCatalog.Names`（写し）が
/// `Shell.cs` の `switch (what)`（唯一の出所）と過不足なく一致するかを固定する。
///
/// ⚠️ 「手で写した一覧を作らない」の指示どおり `TapCatalog.Names` は写しなので、
/// ここで **`Shell.cs` をソースのままテキストとして読み直し**（`LayoutRuleTests` の
/// `view\*.cs` と同じ「読むだけ」の型 ── `EggCommand.Tests.csproj` の
/// `&lt;None Include&gt;` が `websrc\Shell.cs` へコピーする）、`case "..."` を正規表現で
/// 抜き出して突き合わせる。ずれたらこの検査が落ちる。</summary>
public class TapCatalogTests
{
    private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "websrc");

    private static string ShellSource => File.ReadAllText(Path.Combine(Dir, "Shell.cs"));
    private static string AppPageSource => File.ReadAllText(Path.Combine(Dir, "AppPage.razor"));

    /// <summary>⚠️ Shell.cs 側の `Tap(string what, string at)` の中身だけを切り出す ──
    /// `Hold(string what, string at)`（別の switch・`hold=` 用）の `case` を混ぜない。</summary>
    private static string TapSwitchBody(string src)
    {
        int start = src.IndexOf("public void Tap(string what, string at)", StringComparison.Ordinal);
        Assert.True(start >= 0, "Shell.cs: Tap(string what, string at) が見つからない（検査の前提が崩れた）");
        int end = src.IndexOf("public void Hold(string what, string at)", start, StringComparison.Ordinal);
        Assert.True(end > start, "Shell.cs: Hold(string what, string at) が見つからない（探索範囲の終端が決められない）");
        return src.Substring(start, end - start);
    }

    private static List<string> CaseNamesOf(string body)
    {
        var found = new List<string>();
        foreach (Match m in Regex.Matches(body, "case \"([^\"]+)\":"))
            found.Add(m.Groups[1].Value);
        return found;
    }

    /// <summary>⚠️ `Shell.cs` の switch の外にいる例外（`AppPage.razor:152` が先取りする）。
    /// ⭐ 増やすときはここと `TapCatalog.Names` の両方に足すこと（片方だけ増やすとこの
    /// テストが落ちる ── それが「ずれを見張る」の実体）。</summary>
    private static readonly HashSet<string> ExceptionsOutsideSwitch = new(StringComparer.Ordinal) { "out", "in" };

    [Fact]
    public void 元になるファイルが読める()
    {
        Assert.True(Directory.Exists(Dir), $"{Dir} が無い（csproj のコピー設定を見る）");
        Assert.True(File.Exists(Path.Combine(Dir, "Shell.cs")), "Shell.cs が無い");
        Assert.True(File.Exists(Path.Combine(Dir, "AppPage.razor")), "AppPage.razor が無い");
    }

    /// <summary>🔴 これが「ずれない検査」の本体。Shell.cs の switch にあって
    /// TapCatalog.Names に無い名前が無いこと。</summary>
    [Fact]
    public void ShellのswitchにあってTapCatalogに無い名前が無い()
    {
        var found = CaseNamesOf(TapSwitchBody(ShellSource));
        var missing = found.Where(n => !TapCatalog.Names.Contains(n)).ToList();
        Assert.True(missing.Count == 0,
            "Shell.cs の switch にあるが TapCatalog.Names に無い: " + string.Join(", ", missing));
    }

    /// <summary>⚠️ 逆向き ── TapCatalog.Names にあるが、Shell.cs の switch にも
    /// `out`/`in` の例外にも無い名前（架空の候補）が無いこと。</summary>
    [Fact]
    public void TapCatalogにあってShellにも例外にも無い名前が無い()
    {
        var found = new HashSet<string>(CaseNamesOf(TapSwitchBody(ShellSource)), StringComparer.Ordinal);
        var extra = TapCatalog.Names
            .Where(n => !found.Contains(n) && !ExceptionsOutsideSwitch.Contains(n))
            .ToList();
        Assert.True(extra.Count == 0,
            "TapCatalog.Names にあるが Shell.cs の switch にも out/in の例外にも無い: " + string.Join(", ", extra));
    }

    [Fact]
    public void TapCatalogは重複しない()
    {
        Assert.Equal(TapCatalog.Names.Length, TapCatalog.Names.Distinct().Count());
    }

    /// <summary>⚠️ 実測値をそのまま固定する（増減したら、この数もどこかを直し忘れている合図）。</summary>
    [Fact]
    public void 全部で51個()
    {
        // ⭐ 2026-08-26 に `levelup` と `spend` を足した（ARK式の自由配分）
        // ⚠️ 2026-08-29 に `levelup` を外した（作者の指示「点を振る前に点を獲得するのが
        //    二度手間」で釦が消え、`Shell.Tap` の分岐も死んだため）── 47 → 46。
        // ⭐ 同日、家系図の `tree` を足した（作者の指示「BOXで2世代以降の
        //    キャラクターの家系図を見られるように」）── 46 → 47。
        // ⭐ 同日、祝いの「くわしく見る」の `detail` を足した（grow への誤着地を
        //    BOX 詳細へ付け替え）── 47 → 48。
        // 🔴 同日、**上のバーを外した**（作者の指示「この帯は不要」）── `back` と `extra` が
        //    消え、代わりに右上のメニュー `menu` と、その中の `book`（図鑑）が入った。48 → 48。
        // ⭐ 同日、卵の棚に並べ替えを足した（作者の指示「星、入手順」）── `eggstar`/`eggnew`
        //    の2つで 48 → 50。
        // ⭐ 同日、戦闘の狙い先 `aim` を足した（作者の指示「ターゲットしていることが
        //    わかるように（敵味方両方）」）── 体を押して狙い、もう一度押して外す。50 → 51。
        Assert.Equal(51, TapCatalog.Names.Length);
    }

    /// <summary>⭐ `out`/`in` の例外そのものが、いまも `AppPage.razor` にあるか
    /// （前提が崩れていないかの裏取り）。</summary>
    [Fact]
    public void OutInの例外がAppPageに今もある()
    {
        Assert.Contains("is \"out\" or \"in\"", AppPageSource);
    }
}
