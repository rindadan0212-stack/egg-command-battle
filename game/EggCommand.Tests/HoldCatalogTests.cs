using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EggCommand.Web;
using Xunit;

namespace EggCommand.Tests;

/// <summary>骨組みエディタ P4「長押し（`hold=`）の付け替え」── <see cref="HoldCatalog.Names"/>
/// （写し）が `Shell.cs` の `Hold(string what, string at)` の `switch (what)`（唯一の出所）と
/// 過不足なく一致するかを固定する。
///
/// ⚠️ <see cref="TapCatalogTests"/> とまったく同じ型 ── 「手で写した一覧を作らない」の指示
/// どおり写しなので、`Shell.cs` をソースのままテキストとして読み直して突き合わせる。
///
/// 🔴 `Tap` の switch を混ぜない ── 同じ綴り（`s0`〜`s2`）が両方に居るので、範囲を
/// 間違えると「`tap=` の名前を `hold=` の候補に出す」道具になる。</summary>
public class HoldCatalogTests
{
    private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "websrc");

    private static string ShellSource => File.ReadAllText(Path.Combine(Dir, "Shell.cs"));

    /// <summary>⚠️ `Hold(string what, string at)` の中身だけを切り出す。終端は次の
    /// メソッド（`private void Choose(int i)`）── `TapEntranceTests` と同じ切り出し方。</summary>
    private static string HoldSwitchBody(string src)
    {
        int start = src.IndexOf("public void Hold(string what, string at)", StringComparison.Ordinal);
        Assert.True(start >= 0, "Shell.cs: Hold(string what, string at) が見つからない（検査の前提が崩れた）");
        int end = src.IndexOf("private void Choose(int i)", start, StringComparison.Ordinal);
        Assert.True(end > start, "Shell.cs: Choose(int i) が見つからない（探索範囲の終端が決められない）");
        return src.Substring(start, end - start);
    }

    private static List<string> CaseNamesOf(string body)
    {
        var found = new List<string>();
        foreach (Match m in Regex.Matches(body, "case \"([^\"]+)\":"))
            found.Add(m.Groups[1].Value);
        return found;
    }

    [Fact]
    public void 元になるファイルが読める()
    {
        Assert.True(Directory.Exists(Dir), $"{Dir} が無い（csproj のコピー設定を見る）");
        Assert.True(File.Exists(Path.Combine(Dir, "Shell.cs")), "Shell.cs が無い");
    }

    /// <summary>🔴 これが「ずれない検査」の本体。</summary>
    [Fact]
    public void ShellのswitchにあってHoldCatalogに無い名前が無い()
    {
        var found = CaseNamesOf(HoldSwitchBody(ShellSource));
        var missing = found.Where(n => !HoldCatalog.Names.Contains(n)).ToList();
        Assert.True(missing.Count == 0,
            "Shell.cs の Hold の switch にあるが HoldCatalog.Names に無い: " + string.Join(", ", missing));
    }

    /// <summary>⚠️ 逆向き ── 架空の候補（`Shell.Hold` の switch に無い名前）を出さない。</summary>
    [Fact]
    public void HoldCatalogにあってShellに無い名前が無い()
    {
        var found = new HashSet<string>(CaseNamesOf(HoldSwitchBody(ShellSource)), StringComparer.Ordinal);
        var extra = HoldCatalog.Names.Where(n => !found.Contains(n)).ToList();
        Assert.True(extra.Count == 0,
            "HoldCatalog.Names にあるが Shell.cs の Hold の switch に無い: " + string.Join(", ", extra));
    }

    [Fact]
    public void HoldCatalogは重複しない()
    {
        Assert.Equal(HoldCatalog.Names.Length, HoldCatalog.Names.Distinct().Count());
    }

    /// <summary>⚠️ 実測値をそのまま固定する（増減したら、この数もどこかを直し忘れている合図）。</summary>
    [Fact]
    public void 全部で15個()
    {
        // ⭐ BOX の札の技3 ＋ 戦闘の手札3 ＋ 配合の親札6 ＋ 種族の札の抽選3
        //    （2026-08-29 に戦闘と配合の計9個を配線したときの数）
        Assert.Equal(15, HoldCatalog.Names.Length);
    }

    /// <summary>🔴 `tap=` の一覧と混ざっていないことの杭 ── `HoldCatalog` にしか無い名前
    /// （`detail-s0` 等）が `TapCatalog` に紛れ込んでいないか。⚠️ `s0`〜`s2` は**両方に居るのが
    /// 正しい**（戦闘の技札は短押しで技を出し、長押しで詳細を開く）ので、そこは除く。</summary>
    [Fact]
    public void 長押し専用の名前がtapの一覧に紛れていない()
    {
        var shared = new HashSet<string>(new[] { "s0", "s1", "s2" }, StringComparer.Ordinal);
        foreach (var name in HoldCatalog.Names)
        {
            if (shared.Contains(name)) continue;
            Assert.DoesNotContain(name, TapCatalog.Names);
        }
    }
}
