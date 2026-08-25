using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace EggCommand.Tests;

/// <summary>`Scenes.All`（`/edit` が触れる骨組みの唯一の出所）と、実物の
/// `assets/layouts/*.txt` が過不足なく1対1かを見張る。
///
/// ⚠️ 2026-08-25 監査で発覚: `fanfare.txt` は実在するのに `Scenes.All` に無く、
/// `/edit` から一生開けなかった（`?of=fanfare` は黙って `box` にフォールバックしていた）。
/// この検査が無かったので、`dotnet test` は緑のまま気づけなかった。
///
/// ⭐ `Scenes.cs` は `Shell`/`Sheets`/`Demo` など Web 専用の依存を多く持つので
/// コンパイルには持ち込めない（`TapCatalogTests` が `Shell.cs` を読むのと同じ理由）。
/// テキストとして読み、`S("id", ...)` の呼び出しを正規表現で数える。</summary>
public class ScenesTests
{
    private static readonly string WebSrcDir = Path.Combine(AppContext.BaseDirectory, "websrc");
    private static readonly string LayoutsDir = Path.Combine(AppContext.BaseDirectory, "layouts");

    private static HashSet<string> SceneIds()
    {
        string text = File.ReadAllText(Path.Combine(WebSrcDir, "Scenes.cs"));
        var ids = new HashSet<string>();
        foreach (Match m in Regex.Matches(text, "S\\(\"([a-z]+)\""))
            ids.Add(m.Groups[1].Value);
        return ids;
    }

    private static HashSet<string> LayoutFileIds()
    {
        var ids = new HashSet<string>();
        foreach (var path in Directory.GetFiles(LayoutsDir, "*.txt"))
            ids.Add(Path.GetFileNameWithoutExtension(path));
        return ids;
    }

    [Fact]
    public void Scenesは実物のlayoutsと過不足なく1対1()
    {
        var scenes = SceneIds();
        var files = LayoutFileIds();

        var missingFromScenes = new List<string>();
        foreach (var id in files) if (!scenes.Contains(id)) missingFromScenes.Add(id);
        Assert.True(missingFromScenes.Count == 0,
            "Scenes.All に無い骨組み（/edit から開けない）: " + string.Join(", ", missingFromScenes));

        var missingFromDisk = new List<string>();
        foreach (var id in scenes) if (!files.Contains(id)) missingFromDisk.Add(id);
        Assert.True(missingFromDisk.Count == 0,
            "Scenes.All にあるのに実物が無い骨組み: " + string.Join(", ", missingFromDisk));
    }
}
