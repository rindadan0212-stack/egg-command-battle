using System;
using System.Collections.Generic;
using System.IO;
using EggCommand.Core;
using EggCommand.Sim;
using Xunit;

namespace EggCommand.Tests;

/// <summary>ドット絵の PNG 書き出し。
///
/// ⭐ **正典を PNG に戻す決定**（2026-08-22・作者）の土台です。
///
/// ⚠️ **「書けた」では足りません。**⭐ 書いたものを読み戻して、
/// **元の添字とパレットに戻る**ことを確かめて初めて「正典にしてよい」と言えます。
/// ⚠️ ここが閉じていないと、絵を PNG 側で直しても Core に戻らず、
/// **出所が2つに割れます**。</summary>
public class SpritePngTests
{
    public static IEnumerable<object[]> All()
    {
        foreach (var species in SpeciesTable.All)
            yield return new object[] { species.Id };
    }

    /// <summary>⭐ **往復が閉じる。**書いて読んだら、画素もパレットも元どおり。</summary>
    [Theory]
    [MemberData(nameof(All))]
    public void 書いて読むと元に戻る(string id)
    {
        var species = SpeciesTable.ById(id);
        var before = species.Sprite;
        var palette = species.Palettes[0];

        SpritePng.Decode(SpritePng.Encode(before, palette), out var after, out var backPalette);

        Assert.Equal(before.Width, after.Width);
        Assert.Equal(before.Height, after.Height);
        for (int y = 0; y < before.Height; y++)
        {
            for (int x = 0; x < before.Width; x++)
            {
                Assert.True(before.At(x, y) == after.At(x, y),
                    $"{id} の ({x},{y}) が {before.At(x, y)} → {after.At(x, y)}");
            }
        }

        Assert.Equal(palette.Count, backPalette.Count);
        for (int i = 1; i <= palette.Count; i++)
            Assert.Equal(palette.ColorOf((byte)i), backPalette.ColorOf((byte)i));
    }

    /// <summary>⚠️ **添字0は必ず透明。**⭐ tRNS が 0 番だけを透明にしていること。
    /// 透明が抜けると、絵が四角い板になります。</summary>
    [Fact]
    public void 添字0が透明になっている()
    {
        var species = SpeciesTable.All[0];
        var png = SpritePng.Encode(species.Sprite, species.Palettes[0]);

        int at = 8;
        bool found = false;
        while (at + 8 <= png.Length)
        {
            int len = (png[at] << 24) | (png[at + 1] << 16) | (png[at + 2] << 8) | png[at + 3];
            string kind = System.Text.Encoding.ASCII.GetString(png, at + 4, 4);
            if (kind == "tRNS")
            {
                found = true;
                Assert.Equal(1, len);          // ⭐ 0番だけ書けば残りは不透明
                Assert.Equal(0, png[at + 8]);  // ⚠️ その 0番の alpha は 0
            }
            at += 8 + len + 4;
        }
        Assert.True(found, "tRNS が無い（透明が失われる）");
    }

    /// <summary>⚠️ **道具をわざと壊して、効きを確かめる。**
    /// ⭐ 1画素でも違えば往復の検査が落ちること。</summary>
    [Fact]
    public void 一画素違えば落ちる()
    {
        var species = SpeciesTable.All[1];
        var png = SpritePng.Encode(species.Sprite, species.Palettes[0]);
        SpritePng.Decode(png, out var same, out _);

        // ⭐ まず素で一致することを確かめてから
        Assert.Equal(species.Sprite.At(0, 0), same.At(0, 0));

        // ⚠️ わざと1画素だけ違う絵を作って、比べ方が本当に効くか見る
        var rows = new string[species.Sprite.Height];
        for (int y = 0; y < species.Sprite.Height; y++)
        {
            var line = new System.Text.StringBuilder();
            for (int x = 0; x < species.Sprite.Width; x++)
                line.Append(PixelSprite.CharOf(species.Sprite.At(x, y)));
            rows[y] = line.ToString();
        }
        // 左上を、透明でなければ透明に・透明なら1番にする
        rows[0] = (rows[0][0] == '.' ? "1" : ".") + rows[0].Substring(1);
        var tampered = PixelSprite.Parse(rows);

        bool differs = false;
        for (int y = 0; y < tampered.Height && !differs; y++)
            for (int x = 0; x < tampered.Width && !differs; x++)
                if (tampered.At(x, y) != same.At(x, y)) differs = true;
        Assert.True(differs, "1画素変えたのに違いが出ない＝比べ方が効いていない");
    }

    // ── 表示用 PNG（`art/sprites/display/`）── ─────────────────

    public static IEnumerable<object[]> AllDisplayPalettes()
    {
        foreach (var species in SpeciesTable.All)
            for (int p = 0; p < species.Palettes.Count; p++)
                yield return new object[] { species.Id, p };
    }

    /// <summary>🔴 **「その場で焼き直したもの」と「art/sprites/display/ の実物」をバイト単位で比べる。**
    ///
    /// ⚠️ Core（種族の絵・パレット）を直して `sim sprites` を走らせ忘れると、
    /// ゲームは黙って古い PNG を出し続ける ── 出所が2つに割れて、誰も気づけない。
    /// ⭐ ここで焼き直しと実物を突き合わせておけば、忘れた瞬間に `dotnet test` が落ちる。
    /// ⚠️ 落ちたときは**原因ではなく直し方**を言う（「sim sprites を走らせてください」）。</summary>
    [Theory]
    [MemberData(nameof(AllDisplayPalettes))]
    public void 表示用PNGが最新である(string id, int paletteIndex)
    {
        var species = SpeciesTable.ById(id);
        var fresh = SpritePng.Encode(species.Sprite, species.Palettes[paletteIndex]);

        var path = Path.Combine(AppContext.BaseDirectory, "sprites-display",
            SpritePng.DisplayFileName(id, paletteIndex));
        Assert.True(File.Exists(path),
            $"{id}-{paletteIndex} の表示用 PNG が無い。sim sprites を走らせてください（art/sprites/display/ が唯一の出所）");

        var onDisk = File.ReadAllBytes(path);
        Assert.True(fresh.AsSpan().SequenceEqual(onDisk),
            $"{id}-{paletteIndex} の表示用 PNG が古い。sim sprites を走らせてください（Core の絵かパレットを直したのに焼き直していない）");
    }

    /// <summary>⚠️ **枚数のずれも古さの一種。**種族を消す／パレット数を変えたのに焼き直さないと、
    /// 死んだファイルが残ったり、逆に足りなかったりする ── 中身の比較だけでは拾えない。</summary>
    [Fact]
    public void 表示用PNGの枚数が種族表と一致する()
    {
        int expected = 0;
        foreach (var species in SpeciesTable.All) expected += species.Palettes.Count;

        var dir = Path.Combine(AppContext.BaseDirectory, "sprites-display");
        Assert.True(Directory.Exists(dir), $"{dir} が無い。sim sprites を走らせてください");
        Assert.Equal(expected, Directory.GetFiles(dir, "*.png").Length);
    }
}
