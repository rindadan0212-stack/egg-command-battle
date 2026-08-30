using System;
using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>パレットの「指定しない」(null) が0番から正しく受け継がれるかを検査する。
///
/// ⭐ **土台は固定値。**⚠️ `GoldenTests` の「パレットが一致する」は `tamaru`/`tsunoga`/
/// `haneru`/`nobiru` を検査から外している（作者が描き直した意匠なので、移植元の golden に無い ──
/// `SpeciesGoldenTests.Redrawn` 参照）。**ここで別枠の固定値を持って直に確かめる**
/// （2026-08-23・null 対応の作業で追加。2026-08-25・段取り3で4種の絵を取り込み、
/// その4種ぶんの固定値を実物の値へ更新した）。
///
/// ⚠️ **この固定値は、対象の絵が変わるたびに Species.cs から書き写し直す。**
/// null を入れて色の値が1つでもずれたら、ここが真っ先に落ちる。</summary>
public class PaletteResolveTests
{
    /// <summary>⭐ 全38パレットの色。⚠️ tamaru/tsunoga/haneru/nobiru の4種は
    /// 2026-08-25（段取り3）に、`SpriteImport` が取り込んだ実物へ書き写し直した
    /// （それ以前は null 対応より前の `Species.cs` の写しだった）。手で直さない ──
    /// ここが「Species.cs と1文字も違わないこと」の証拠そのもの。</summary>
    private static readonly Dictionary<string, string[][]> ExpectedColors = new Dictionary<string, string[][]>
    {
        ["tamaru"] = new[]
        {
            new[] { "#474671", "#040406", "#f7f5ea", "#1a1d49", "#30345a", "#afb5e2", "#55507d", "#3e3f54", "#d6d1db", "#ea9d1e", "#2d2e3e", "#1d1f2f", "#08132e", "#2a3155", "#9999aa", "#fbd34a", "#ca7416", "#675c8d", "#78768e", "#8570a4", "#5d5e7e", "#15203e" },
            new[] { "#715546", "#060504", "#eaf5f7", "#49271a", "#5a3a30", "#e2baaf", "#7d6450", "#54443e", "#dbd9d1", "#1eeae1", "#3e322d", "#2f211d", "#2e0a08", "#55312a", "#aa9f99", "#4ae8fb", "#16cab0", "#8d775c", "#8e8076", "#a49670", "#7e675d", "#3e1815" },
            new[] { "#467155", "#040605", "#f7eaf5", "#1a4927", "#305a3a", "#afe2ba", "#507d64", "#3e5444", "#d1dbd9", "#e11eea", "#2d3e32", "#1d2f21", "#082e0a", "#2a5531", "#99aa9f", "#fb4ae8", "#b016ca", "#5c8d77", "#768e80", "#70a496", "#5d7e67", "#153e18" },
        },
        ["tsunoga"] = new[]
        {
            new[] { "#fbf6e5", "#141414", "#21d2c5", "#2fb5ba", "#23e1d9", "#f9e1d9", "#ffccf1", "#22efd4", "#efc2d1", "#46ffe6", "#dac4c0", "#fff0e3", "#00b1b4", "#a784b0", "#fdf4e2", "#dad9ca", "#06082d", "#48f3d2" },
            new[] { "#e5f9fb", "#141414", "#d221a4", "#ba2f87", "#e123aa", "#d9f9ec", "#e0ffcc", "#ef22c6", "#c2efc2", "#ff46da", "#c0dacd", "#e3fff9", "#b40075", "#aab084", "#e2fdfd", "#cad6da", "#2d1106", "#f348db" },
            new[] { "#fbe5f9", "#141414", "#a4d221", "#87ba2f", "#aae123", "#ecd9f9", "#cce0ff", "#c6ef22", "#c2c2ef", "#daff46", "#cdc0da", "#f9e3ff", "#75b400", "#84aab0", "#fde2fd", "#dacad6", "#062d11", "#dbf348" },
        },
        ["haneru"] = new[]
        {
            new[] { "#87ac5d", "#050506", "#faf1c9", "#43644d", "#619255", "#1a2122", "#cbb08a", "#b1c664", "#607e5f", "#bc906e", "#294b3b", "#cf4949", "#a23536", "#f8736f", "#ff8d6d", "#ffffff", "#4b6d4b", "#3b1816", "#d05c6c", "#c0c0c0", "#a83348", "#f56342", "#87a26a", "#e5e49b", "#c1d185", "#111211" },
            new[] { "#5d6dac", "#060505", "#c9f3fa", "#584364", "#5d5592", "#221a1e", "#8acbc6", "#6490c6", "#685f7e", "#6ebcaa", "#46294b", "#49cf76", "#35a258", "#6ff8a1", "#6dffbe", "#ffffff", "#564b6d", "#163b24", "#5cd073", "#c0c0c0", "#33a845", "#42f59f", "#6a74a2", "#9bcde5", "#85a8d1", "#111211" },
            new[] { "#ac5d6d", "#050605", "#fac9f3", "#645843", "#925d55", "#1e221a", "#c68acb", "#c66490", "#7e685f", "#aa6ebc", "#4b4629", "#7649cf", "#5835a2", "#a16ff8", "#be6dff", "#ffffff", "#6d564b", "#24163b", "#735cd0", "#c0c0c0", "#4533a8", "#9f42f5", "#a26a74", "#e59bcd", "#d185a8", "#111211" },
        },
        ["nobiru"] = new[]
        {
            new[] { "#fe3b40", "#fcbd8a", "#000000", "#d29673", "#312c2a", "#fd4b50", "#fd5d62", "#fc7a4d", "#d39c88", "#fa6237", "#f14d50", "#9f292b", "#bc3c3c", "#f06038", "#db484c", "#ecf2ea", "#96402c", "#504038", "#060305", "#dd6b4e", "#8d7264", "#c1fce5", "#a88a7d", "#e3d5cb", "#ffffff" },
            new[] { "#3bfe77", "#8afce3", "#000000", "#73d2b6", "#312c2a", "#4bfd81", "#5dfd8d", "#4dfcb4", "#88d3b5", "#37faa3", "#4df181", "#299f4e", "#3cbc67", "#38f09d", "#48db75", "#ebeaf2", "#2c9663", "#385048", "#040603", "#4edd9b", "#648d80", "#f9c1fc", "#7da898", "#cbe3dd", "#ffffff" },
            new[] { "#773bfe", "#e38afc", "#000000", "#b673d2", "#312c2a", "#814bfd", "#8d5dfd", "#b44dfc", "#b588d3", "#a337fa", "#814df1", "#4e299f", "#673cbc", "#9d38f0", "#7548db", "#f2ebea", "#632c96", "#483850", "#030406", "#9b4edd", "#80648d", "#fcf9c1", "#987da8", "#ddcbe3", "#ffffff" },
        },
        ["hirabe"] = new[]
        {
            new[] { "#2ab5be", "#221a2a", "#22162a", "#7fe0d0", "#efd58e" },
            new[] { "#be2a84", "#2a271a", "#2a2916", "#e07fd0", "#8ee9ef" },
            new[] { "#6bbe2a", "#1a2a2a", "#16282a", "#bfe07f", "#ef8ed9" },
        },
        ["togeru"] = new[]
        {
            new[] { "#fe5b3f", "#f23a42", "#231a2a", "#22162a", "#fed983" },
            new[] { "#3ffe9b", "#3af26f", "#2a281a", "#2a2916", "#83fafe" },
            new[] { "#bb3ffe", "#8e3af2", "#1a292a", "#16282a", "#fe83e5" },
        },
        ["marumi"] = new[]
        {
            new[] { "#deeae2", "#231b2b", "#9bcac2", "#22162a", "#fcf2c3" },
            new[] { "#e6deea", "#2b281b", "#ca9bc2", "#2a2916", "#c3f3fc" },
            new[] { "#eae8de", "#1b2b2b", "#bbca9b", "#16282a", "#fcc3ea" },
        },
        ["kibane"] = new[]
        {
            new[] { "#4a32b1", "#221a2a", "#9458dc", "#221a26", "#cbabf5", "#fac159" },
            new[] { "#b17432", "#2a271a", "#dcc058", "#26261a", "#f5e4ab", "#59faf7" },
            new[] { "#32b189", "#1a2a2a", "#58dcd6", "#1a2426", "#abf5f0", "#fa59e2" },
            new[] { "#b032b1", "#2a1a25", "#dc58ba", "#261a20", "#f5abe4", "#b2fa59" },
        },
        ["iwao"] = new[]
        {
            new[] { "#7b7a76", "#221a2a", "#56565d", "#22162a", "#ada58a", "#e7cd76" },
            new[] { "#7b7a76", "#2a271a", "#56565d", "#2a2916", "#8aa9ad", "#76dbe7" },
            new[] { "#7b7a76", "#1a2a2a", "#56565d", "#16282a", "#ad8aa4", "#e776c8" },
            new[] { "#7b7a76", "#2a1a25", "#56565d", "#2a1622", "#99ad8a", "#a7e776" },
        },
        ["homura"] = new[]
        {
            new[] { "#fd360e", "#231a2a", "#fe9005", "#22162a", "#fede3c" },
            new[] { "#0efd86", "#2a281a", "#05fee3", "#2a2916", "#3cddfe" },
            new[] { "#ae0efd", "#1a292a", "#fe05f0", "#16282a", "#fe3cbd" },
            new[] { "#fdf50e", "#2a1a24", "#a5fe05", "#2a1622", "#83fe3c" },
        },
        ["nushi"] = new[]
        {
            new[] { "#241b2b", "#b54575", "#7e2e66", "#e5931e", "#221629", "#eeddbb", "#fc4a46" },
            new[] { "#2b291b", "#50b545", "#4b7e2e", "#1ee5d5", "#292816", "#bbeeee", "#46fc87" },
        },
    };

    /// <summary>⭐ **本体**: 全35パレット・1文字も変わっていないこと。
    /// ⚠️ `species.Palettes[p].Colors` を直に読む ── null が解決されないまま残っていたら、
    /// ここで `null != "#..."` として即座に落ちる（サイレントに壊れない）。
    ///
    /// ⚠️ 2026-08-25・段取り3で 38 → 35 件に変わった（正当な変化）── tamaru/tsunoga/haneru の
    /// 3種が「通常＋変異3枚」（計4枚）から「通常＋変異2枚」（計3枚）へ変わったため
    /// （3種 × −1枚 = −3）。nobiru はもともと3枚のまま。</summary>
    [Fact]
    public void null対応の前後で全パレットの色が一致する()
    {
        Assert.Equal(35, Count());
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

    /// <summary>⭐ **取り込んだ4種で実際に null が使われていること**を確かめる
    /// （2026-08-25・段取り3・`SpriteImport` の彩度しきい値判定）。
    /// ⚠️ 「色が変わっていない」だけでは、null を1個も書かずに済ませても通ってしまう ──
    /// 機能そのものを使っているかは別に確かめる必要がある。
    ///
    /// ⚠️ **添字（0始まり）は Species.cs の実物から書き写した**（取り込み時に彩度が低いと
    /// 判定された色の位置）。tamaru は今回たまたま無彩色に判定された色が無かった（0件）ので、
    /// ここでは対象に含めない ── 0件を「null を使っている」の証拠にはできない。</summary>
    [Theory]
    [InlineData("tsunoga", new[] { 1 })]
    [InlineData("haneru", new[] { 15, 19, 25 })]
    [InlineData("nobiru", new[] { 2, 4, 24 })]
    public void 取り込んだ種族の無彩色は変異でもnullで受け継がれている(string speciesId, int[] indices)
    {
        var species = SpeciesTable.ById(speciesId);
        Assert.NotEmpty(indices);
        for (int p = 1; p < species.Palettes.Count; p++)
        {
            foreach (int i in indices)
            {
                Assert.Equal(species.Palettes[0].Colors[i], species.Palettes[p].Colors[i]);
            }
        }
    }

    /// <summary>⚠️ 上の対（裏取り）── tamaru は無彩色に判定された色が無いので null を使っていない。
    /// **黙って「0件」を見逃さない**ため、件数そのものをここで確かめておく。</summary>
    [Fact]
    public void tamaruは無彩色ゼロ件_nullを使っていない()
    {
        var tamaru = SpeciesTable.ById("tamaru");
        // 通常パレットと変異パレットが、22色すべてで違う値を持つ
        // （＝1色も null で受け継いでいない）ことを確かめる。
        for (int p = 1; p < tamaru.Palettes.Count; p++)
        {
            for (int i = 0; i < tamaru.Palettes[0].Colors.Length; i++)
            {
                Assert.NotEqual(tamaru.Palettes[0].Colors[i], tamaru.Palettes[p].Colors[i]);
            }
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
