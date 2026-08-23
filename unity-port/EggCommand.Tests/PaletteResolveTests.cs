using System;
using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>パレットの「指定しない」(null) が0番から正しく受け継がれるかを検査する。
///
/// ⭐ **土台は固定値。**⚠️ `GoldenTests` の「パレットが一致する」は `tamaru` を
/// 検査から外している（作者が描き直した意匠なので、移植元の golden に無い ──
/// `SpeciesGoldenTests.Redrawn` 参照）。tamaru こそ null を使った当人なので、
/// **ここで別枠の固定値を持って直に確かめる**（2026-08-23・null 対応の作業で追加）。
///
/// ⚠️ **この固定値は null 対応より前の `Species.cs` から1文字も変えずに書き写した。**
/// null を入れて色の値が1つでもずれたら、ここが真っ先に落ちる。</summary>
public class PaletteResolveTests
{
    /// <summary>⭐ null 対応の**前**に `Species.cs` から書き写した、全38パレットの色。
    /// ⚠️ 手で直さない ── ここが「変わっていないこと」の証拠そのもの。</summary>
    private static readonly Dictionary<string, string[][]> ExpectedColors = new Dictionary<string, string[][]>
    {
        ["tamaru"] = new[]
        {
            new[] { "#00fe01", "#fefc01", "#00c862", "#8c8504", "#c3bc01", "#ff7e00", "#7f807f", "#553e00", "#b31a00", "#000000", "#fefeff" },
            new[] { "#b300fe", "#014ffe", "#c800a2", "#04348c", "#0142c3", "#00cdff", "#7f807f", "#003155", "#00b397", "#000000", "#fffffe" },
            new[] { "#fe5200", "#fe01af", "#c8a200", "#8c0467", "#c3018c", "#d000ff", "#7f807f", "#550051", "#5300b3", "#000000", "#fefffe" },
            new[] { "#00fec7", "#3bfe01", "#0092c8", "#298c04", "#33c301", "#b9ff00", "#7f807f", "#2a5500", "#b3a600", "#000000", "#fffeff" },
        },
        ["tsunoga"] = new[]
        {
            new[] { "#2a1a14", "#c97a52", "#eab48c", "#160e0a" },
            new[] { "#141a2a", "#5273c9", "#8c9eea", "#0a0e16" },
            new[] { "#2a1420", "#c95293", "#ea8cc4", "#160a12" },
            new[] { "#1a2a18", "#63c952", "#98ea8c", "#0e160a" },
        },
        ["haneru"] = new[]
        {
            new[] { "#241c2e", "#a98fc9", "#ded0ea", "#141018" },
            new[] { "#1c2e2a", "#8fc9bd", "#d0eae4", "#101816" },
            new[] { "#2e2418", "#c9b48f", "#eae0d0", "#181410" },
            new[] { "#2e1c1c", "#c98f8f", "#ead0d0", "#181010" },
        },
        ["nobiru"] = new[]
        {
            new[] { "#1c2e24", "#6ec99a", "#a8eac8", "#101a14" },
            new[] { "#2e1c24", "#c96e9a", "#eaa8c8", "#1a1014" },
            new[] { "#2a2e18", "#b4c96e", "#dceaa8", "#181a10" },
        },
        ["hirabe"] = new[]
        {
            new[] { "#182a2e", "#6eb4c9", "#a8dcea", "#101a1c" },
            new[] { "#2e2818", "#c9b06e", "#eadaa8", "#1a1810" },
            new[] { "#241c2e", "#9a6ec9", "#c8a8ea", "#141018" },
        },
        ["togeru"] = new[]
        {
            new[] { "#2e1818", "#c96e6e", "#eaa8a8", "#1a1010" },
            new[] { "#18182e", "#6e6ec9", "#a8a8ea", "#10101a" },
            new[] { "#1c2e18", "#7ec96e", "#b4eaa8", "#101a10" },
        },
        ["marumi"] = new[]
        {
            new[] { "#2e2a20", "#e0d0a8", "#f4ecd0", "#1a1810" },
            new[] { "#202a2e", "#a8d0e0", "#d0ecf4", "#10181a" },
            new[] { "#2e2028", "#e0a8c4", "#f4d0e4", "#1a1014" },
        },
        ["kibane"] = new[]
        {
            new[] { "#241a2e", "#9a7ec9", "#c6b0ea", "#140f1a" },
            new[] { "#1c2436", "#6e9ec9", "#a8cbea", "#101418" },
            new[] { "#361c22", "#c96e7f", "#eaa8b4", "#181012" },
            new[] { "#2e2a18", "#c9bd6e", "#eae0a8", "#1a1810" },
        },
        ["iwao"] = new[]
        {
            new[] { "#22201c", "#8f8a7e", "#c2bdb0", "#141310" },
            new[] { "#1c2436", "#6e9ec9", "#a8cbea", "#101418" },
            new[] { "#361c22", "#c96e7f", "#eaa8b4", "#181012" },
            new[] { "#2e2a18", "#c9bd6e", "#eae0a8", "#1a1810" },
        },
        ["homura"] = new[]
        {
            new[] { "#2e1a14", "#e08a4e", "#f5c48c", "#1a0f0a" },
            new[] { "#1c2436", "#6e9ec9", "#a8cbea", "#101418" },
            new[] { "#361c22", "#c96e7f", "#eaa8b4", "#181012" },
            new[] { "#2e2a18", "#c9bd6e", "#eae0a8", "#1a1810" },
        },
        ["nushi"] = new[]
        {
            new[] { "#14100c", "#6b5a3e", "#9c8759", "#e8d48a" },
            new[] { "#0c1014", "#3e556b", "#59839c", "#8ac8e8" },
        },
    };

    /// <summary>⭐ **本体**: 全38パレット・1文字も変わっていないこと。
    /// ⚠️ `species.Palettes[p].Colors` を直に読む ── null が解決されないまま残っていたら、
    /// ここで `null != "#..."` として即座に落ちる（サイレントに壊れない）。</summary>
    [Fact]
    public void null対応の前後で全パレットの色が一致する()
    {
        Assert.Equal(38, Count());
        foreach (var species in SpeciesTable.All)
        {
            Assert.True(ExpectedColors.TryGetValue(species.Id, out var expected),
                $"{species.Id} の期待値が無い（ExpectedColors に足し忘れ）");
            Assert.Equal(expected!.Length, species.Palettes.Count);
            for (int p = 0; p < expected.Length; p++)
            {
                Assert.Equal(expected[p], species.Palettes[p].Colors);
            }
        }
    }

    private static int Count()
    {
        int total = 0;
        foreach (var species in SpeciesTable.All) total += species.Palettes.Count;
        return total;
    }

    /// <summary>⭐ **tamaru で実際に null が使われていること**を確かめる。
    /// ⚠️ 「色が変わっていない」だけでは、null を1個も書かずに済ませても通ってしまう ──
    /// 機能そのものを使っているかは別に確かめる必要がある。</summary>
    [Fact]
    public void tamaruの変異パレットは通常の刃と目をnullで受け継いでいる()
    {
        // ⭐ 添字7=刃・添字a=目 → Colors 配列では 6番目・9番目（0始まり）。
        var tamaru = SpeciesTable.ById("tamaru");
        for (int p = 1; p < tamaru.Palettes.Count; p++)
        {
            Assert.Equal(tamaru.Palettes[0].Colors[6], tamaru.Palettes[p].Colors[6]);
            Assert.Equal(tamaru.Palettes[0].Colors[9], tamaru.Palettes[p].Colors[9]);
        }
    }

    // ── Palette.ResolveGroup 単体 ── ─────────────────────────

    [Fact]
    public void nullは0番の同じ位置の色を受け継ぐ()
    {
        var raw = new[]
        {
            new Palette("#111111", "#222222", "#333333"),
            new Palette("#aaaaaa", null, "#cccccc"),
        };
        var resolved = Palette.ResolveGroup(raw);
        Assert.Equal(new[] { "#aaaaaa", "#222222", "#cccccc" }, resolved[1].Colors);
        // ⚠️ 0番自身は触れられていない
        Assert.Equal(new[] { "#111111", "#222222", "#333333" }, resolved[0].Colors);
    }

    [Fact]
    public void 解決したPaletteにnullが残らない()
    {
        var raw = new[]
        {
            new Palette("#111111", "#222222"),
            new Palette(null, null),
        };
        var resolved = Palette.ResolveGroup(raw);
        foreach (var palette in resolved)
            foreach (var color in palette.Colors)
                Assert.NotNull(color);
    }

    [Fact]
    public void ゼロ番自身がnullなら投げる()
    {
        var raw = new[]
        {
            new Palette("#111111", null),
            new Palette("#aaaaaa", "#bbbbbb"),
        };
        Assert.Throws<ArgumentException>(() => Palette.ResolveGroup(raw));
    }

    [Fact]
    public void 色数が0番と違えば投げる()
    {
        var raw = new[]
        {
            new Palette("#111111", "#222222"),
            new Palette("#aaaaaa"),
        };
        Assert.Throws<ArgumentException>(() => Palette.ResolveGroup(raw));
    }
}
