using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EggCommand.Core;
using Xunit.Abstractions;

namespace EggCommand.Tests;

/// <summary>絵の割り当て表（`Core.Art`）を検査する。
///
/// ⭐ **「絵が無いことを黙って通さない」の唯一の出所**（作者の指示 2026-08-23）。
/// ⚠️ 2種類の検査を分けてある:
/// - 🔴 **表が指す名前に実体が無い** ── 落ちる（`Art` の綴り間違いは即バグ）
/// - ⚠️ **実体はあるのに、表からも骨組み（`pic=`）からも指されていない（死蔵）**
///   ── 数えるだけ（動いてはいないので、落とすと直すまで `dotnet test` が塞がる）</summary>
public class ArtTests
{
    private readonly ITestOutputHelper _out;
    public ArtTests(ITestOutputHelper output) => _out = output;

    private static readonly string IconDir = Path.Combine(AppContext.BaseDirectory, "icon");
    private static readonly string LayoutsDir = Path.Combine(AppContext.BaseDirectory, "layouts");

    [Fact]
    public void 絵の実物が見つかる()
    {
        // ⚠️ 1枚も無ければ、以下の検査は「無い」ではなく「見ていない」で通ってしまう。
        Assert.True(Directory.Exists(IconDir), $"{IconDir} が無い（csproj のコピー設定を見る）");
        Assert.NotEmpty(Directory.GetFiles(IconDir, "*.png"));
    }

    /// <summary>🔴 `Art` の表が指す名前は、全部ファイルが実在すること。</summary>
    [Fact]
    public void 表が指す絵は全部ある()
    {
        var missing = new List<string>();
        foreach (var r in Art.All())
        {
            var path = Path.Combine(AppContext.BaseDirectory, r.Folder, r.Name + ".png");
            if (!File.Exists(path)) missing.Add($"{r.Concept} → {r.Folder}/{r.Name}.png（{path}）");
        }
        Assert.Equal(new List<string>(), missing);
    }

    /// <summary>⚠️ **死蔵を数える**（落とさない）。
    ///
    /// ⭐ 「指されている」の判定源は2つ: ① `Art.All()`（表） ② 骨組みの `pic=` リテラル
    /// （`repeat=`/`bind=` でコードが名前を選ぶものは、ここでは追えない ── 主なものは
    /// 下の `DynamicallyChosen` に明記して除外してある）。
    ///
    /// ⚠️ **過大に「死蔵」と数えることはあっても、実際に死蔵なのに見逃すことは無い**
    /// （静的な文字列一致だけを見ているので、判定は甘めに倒してある）。</summary>
    [Fact]
    public void 死蔵の絵を数える()
    {
        Assert.True(Directory.Exists(LayoutsDir), $"{LayoutsDir} が無い（csproj のコピー設定を見る）");

        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in Art.All()) referenced.Add(r.Name);

        var picPattern = new Regex(@"\bpic=([A-Za-z0-9_-]+)", RegexOptions.Compiled);
        foreach (var path in Directory.GetFiles(LayoutsDir, "*.txt"))
            foreach (Match m in picPattern.Matches(File.ReadAllText(path)))
                referenced.Add(m.Groups[1].Value);

        // ⚠️ コードが番号や場面で名前を組み立てる／選ぶもの（骨組みの文字列にも表にも出てこない）。
        //    見つけた場所（2026-08-23 に手で確かめた）:
        //    - die-1〜6:  Unity `DieCube.cs`/`TrailDice.cs`、Web `Sheets.cs`（raid.Dice）
        //    - die-spent: Unity `TrailScreen.cs`、Web `Sheets.cs`（`die < raid.Rolls` の分岐）
        //    - stat-atk/def/hp: Unity `TrailScreen.cs`、Web `Board.cs`（`StatKey` → 絵の対応）
        foreach (var n in new[]
        {
            "die-1", "die-2", "die-3", "die-4", "die-5", "die-6", "die-spent",
            "stat-atk", "stat-def", "stat-hp",
        })
            referenced.Add(n);

        var files = Directory.GetFiles(IconDir, "*.png")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n != null)
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var orphans = files.Where(f => !referenced.Contains(f)).ToList();
        _out.WriteLine(orphans.Count == 0
            ? "死蔵の絵: 0 枚"
            : $"死蔵の絵: {orphans.Count} 枚 ── {string.Join(", ", orphans)}"
              + "（骨組みの pic= にも Art の表にも見当たらない。使っているなら bind= 経由 ── "
              + "見逃しなら、この一覧に足す）");
    }

    /// <summary>⚠️ **仮絵の残り枚数を見える形にする**（作者が順次差し替える前提のため）。
    /// ⭐ 落とさない ── 仮絵が残っていること自体は不具合ではない。</summary>
    [Fact]
    public void 仮絵の残り枚数を出す()
    {
        _out.WriteLine($"仮絵（差し替え予定）: {Art.Placeholder.Count} 枚 ── {string.Join(", ", Art.Placeholder)}");
        foreach (var name in Art.Placeholder)
            Assert.True(File.Exists(Path.Combine(IconDir, name + ".png")), $"仮絵として登録されている {name} の実体が無い");
    }
}
