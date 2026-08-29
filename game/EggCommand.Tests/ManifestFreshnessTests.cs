using System;
using System.Collections.Generic;
using System.IO;
using EggCommand.Sim;
using Xunit;

namespace EggCommand.Tests;

/// <summary>「名前 幅 高」の実寸目録（`icon-manifest.txt` / `paint-manifest.txt`）が、
/// 実物の PNG と1件残らず一致しているかを見る。
///
/// 🔴 **これが無いと、人が道具（`sim icon-manifest` / `sim paint-placeholder`）を
/// 走らせ忘れたまま `dotnet test` が緑のままになる**（2026-08-25・`paint` で実際に
/// 踏んだ誤診と同じ穴 ── `PaintManifest.cs` の doc 参照。忘れると `LayoutDom` が
/// 絵の実寸を取り違え、黙って引き伸ばす／潰れる）。
///
/// ⚠️ **icon** は `EggCommand.Tests.csproj` の既存のコピー設定（`icon\*.png`）で
/// 実物へ直接届くので、ここで素直に読み直す。
/// ⚠️ **paint** は実物 PNG のコピー設定が無い（`EggCommand.Tests.csproj` の変更は
/// 今回の作業範囲の外）ので、`EggCommand.Sim`（`CheckedInAssets`）に埋め込んだ実物
/// 越しに比べる ── 埋め込みは `EggCommand.Sim` のビルドごとに実物から作り直されるので、
/// 「チェックイン済みの中身」が古いまま固定される心配は無い。</summary>
public class ManifestFreshnessTests
{
    private static readonly string IconDir = Path.Combine(AppContext.BaseDirectory, "icon");

    [Fact]
    public void iconの実物が見つかる()
    {
        // ⚠️ 1枚も無ければ、以下の検査は「揃っている」ではなく「見ていない」で通ってしまう。
        Assert.True(Directory.Exists(IconDir), $"{IconDir} が無い（csproj のコピー設定を見る）");
        Assert.NotEmpty(Directory.GetFiles(IconDir, "*.png"));
    }

    /// <summary>🔴 **icon の目録は、実物の PNG からその場で読み直した値と1件残らず一致する。**
    /// ⚠️ ずれたら（新しい icon を足した／大きさが変わった／`sim icon-manifest` を
    /// 走らせ忘れた）ここが落ちる。</summary>
    [Fact]
    public void iconの目録は実物と1件残らず一致する()
    {
        var fresh = IconManifestTool.ComputeManifestLines(IconDir);
        var checkedIn = CheckedInAssets.IconManifestLines();
        AssertSameLines(fresh, checkedIn, "assets/ui/icon/icon-manifest.txt",
            "sim icon-manifest");
    }

    /// <summary>🔴 paint 版（<see cref="iconの目録は実物と1件残らず一致する"/> と同じ形）。</summary>
    [Fact]
    public void paintの目録は実物と1件残らず一致する()
    {
        var fresh = CheckedInAssets.FreshPaintManifestLines();
        var checkedIn = CheckedInAssets.PaintManifestLines();
        AssertSameLines(fresh, checkedIn, "assets/ui/paint/paint-manifest.txt",
            "sim paint-placeholder");
    }

    /// <summary>⚠️ **落ちたときは原因ではなく直し方を言う**（`SpritePngTests` と同じ流儀）。</summary>
    private static void AssertSameLines(List<string> fresh, List<string> checkedIn,
        string manifestPath, string howToFix)
    {
        var freshSet = new HashSet<string>(fresh, StringComparer.Ordinal);
        var checkedInSet = new HashSet<string>(checkedIn, StringComparer.Ordinal);

        var missing = new List<string>();   // 実物にはあるのに目録に無い
        foreach (var line in fresh) if (!checkedInSet.Contains(line)) missing.Add(line);
        var stale = new List<string>();     // 目録にはあるのに実物と食い違う（削除／大きさ違い）
        foreach (var line in checkedIn) if (!freshSet.Contains(line)) stale.Add(line);

        bool ok = missing.Count == 0 && stale.Count == 0;
        Assert.True(ok,
            $"{manifestPath} が実物と食い違っている。`{howToFix}` を走らせてください。"
            + (missing.Count > 0 ? $"\n  実物にあるのに目録に無い/違う: {string.Join(", ", missing)}" : "")
            + (stale.Count > 0 ? $"\n  目録にあるのに実物に無い/違う: {string.Join(", ", stale)}" : ""));
    }
}
