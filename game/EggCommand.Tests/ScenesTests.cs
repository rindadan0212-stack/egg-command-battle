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

    /// <summary>⭐ `S(...)` の1行ぶんを、id と付け足しの旗に分けて読む。</summary>
    private static Dictionary<string, string> SceneFlags()
    {
        string text = File.ReadAllText(Path.Combine(WebSrcDir, "Scenes.cs"));
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        // ⚠️ 行末までを見る（`crowned: true` は `byPart: true` の後ろに来る）。
        foreach (Match m in Regex.Matches(text, "S\\(\"([a-z]+)\"[^\\r\\n]*"))
            map[m.Groups[1].Value] = m.Value;
        return map;
    }

    /// <summary>🔴 **`crowned` は「`use=` で差し込まれる部品」と1対1**（2026-08-29）。
    ///
    /// ⚠️ もとは `byPart` 1つが「盤で選ぶときの探し方」と「値に冠が付くか」を兼ねていた。
    /// コードから描く4枚（`slot`/`unit`/`square`/`walker`）を `byPart: true` にした途端、
    /// 「機能を選ぶ」の候補が**0件**になった ── この4枚はどの骨組みからも `use=` されて
    /// いないので冠が逆算できず、`TapCandidates` が空を返していたため。
    ///
    /// ⭐ 実物の `use=` を数えて突き合わせるので、部品を増減しても勝手に追随する
    /// （手で書いた一覧を持たない）。</summary>
    [Fact]
    public void 冠が付く部品は実物のuseと1対1()
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.GetFiles(LayoutsDir, "*.txt"))
            foreach (Match m in Regex.Matches(File.ReadAllText(path), @"\buse=([a-z]+)"))
                used.Add(m.Groups[1].Value);

        var flags = SceneFlags();
        var crowned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in flags)
            if (pair.Value.Contains("crowned: true", StringComparison.Ordinal)) crowned.Add(pair.Key);

        var missing = new List<string>();
        foreach (var id in used) if (!crowned.Contains(id)) missing.Add(id);
        Assert.True(missing.Count == 0,
            "`use=` で差されているのに crowned: true が無い（機能を選ぶで冠が逆算されない）: "
            + string.Join(", ", missing));

        var extra = new List<string>();
        foreach (var id in crowned) if (!used.Contains(id)) extra.Add(id);
        Assert.True(extra.Count == 0,
            "crowned: true なのに `use=` されていない（機能を選ぶの候補が0件になる）: "
            + string.Join(", ", extra));
    }

    /// <summary>⭐ コードから描く4枚は `byPart`（探し方）は true・`crowned`（冠）は false。
    /// ⚠️ この2つを再び1つに戻さないための杭 ── 兼ねると候補0件の不具合が再発する。</summary>
    [Fact]
    public void コードから描く4枚は探し方だけがdata_part()
    {
        var flags = SceneFlags();
        foreach (var id in new[] { "slot", "unit", "square", "walker" })
        {
            Assert.True(flags.ContainsKey(id), $"Scenes.All に {id} が無い");
            string line = flags[id];
            Assert.True(line.Contains("byPart: true", StringComparison.Ordinal),
                $"{id}: コードから描く4枚は data-part で選ぶので byPart: true が要る");
            Assert.False(line.Contains("crowned: true", StringComparison.Ordinal),
                $"{id}: `use=` されないので冠は無い ── crowned: true にすると候補が0件になる");
        }
    }
}
