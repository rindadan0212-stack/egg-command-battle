using System;
using System.Linq;
using System.Text;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>色数の上限（<see cref="PixelSprite.Digits"/>）そのものの検査。
///
/// ⭐ **2026-08-25 に 15 → 35（9 + a〜z）へ広げた**（段取り3・作者の絵が18〜27色あったため）。
/// ⚠️ ここは `Species.cs` の実物ではなく、`PixelSprite`/`Palette` 単体を作り物のデータで叩く
/// ── 実物（種族の絵）が何色使っていても、上限そのものの往復は崩れないことを保証する。</summary>
public class PixelSpriteTests
{
    /// <summary>1行ぶんの絵を作る。⭐ 幅・高さは検査の主眼ではないので 1×N の帯でよい。</summary>
    private static string RowOf(int count)
    {
        var sb = new StringBuilder(count);
        for (int i = 1; i <= count; i++) sb.Append(PixelSprite.Digits[i - 1]);
        return sb.ToString();
    }

    [Fact]
    public void 上限は35()
    {
        Assert.Equal(35, PixelSprite.Digits.Length);
        Assert.Equal(35, PixelSprite.MaxIndex);
        Assert.Equal("123456789abcdefghijklmnopqrstuvwxyz", PixelSprite.Digits);
    }

    /// <summary>⚠️ 旧上限（15色）では通らなかった 16色以上が、いまは普通に往復すること。</summary>
    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(26)]
    [InlineData(30)]
    public void 十六色以上が往復する(int colors)
    {
        string row = RowOf(colors);
        var sprite = PixelSprite.Parse(new[] { row });

        var back = new StringBuilder(colors);
        for (int x = 0; x < colors; x++) back.Append(PixelSprite.CharOf(sprite.At(x, 0)));
        Assert.Equal(row, back.ToString());
    }

    /// <summary>⭐ **35色ちょうど**（上限いっぱい）が通ること。</summary>
    [Fact]
    public void 三十五色ちょうどが通る()
    {
        string row = RowOf(35);
        var sprite = PixelSprite.Parse(new[] { row });

        Assert.Equal(35, sprite.Width);
        for (int x = 0; x < 35; x++)
        {
            Assert.Equal(x + 1, sprite.At(x, 0));
            Assert.Equal(PixelSprite.Digits[x], PixelSprite.CharOf(sprite.At(x, 0)));
        }

        // 添字 → 文字 → 添字 の往復も確かめる
        for (int i = 1; i <= 35; i++)
        {
            char ch = PixelSprite.CharOf((byte)i);
            Assert.Equal(i, PixelSprite.IndexOf(ch));
        }
    }

    /// <summary>⚠️ **36色目**（<see cref="PixelSprite.Digits"/> の外の文字）は落ちること。
    /// ⭐ 上限を超えた帳面を黙って通さない、が壊れていないかの検査。</summary>
    [Fact]
    public void 三十六色目は落ちる()
    {
        // Digits の外の文字（英数字だが Digits に無いもの）を1つ混ぜる
        Assert.DoesNotContain('A', PixelSprite.Digits);
        var ex = Assert.Throws<ArgumentException>(() => PixelSprite.Parse(new[] { "A" }));
        Assert.Contains("A", ex.Message);
    }

    /// <summary>⚠️ 添字が Digits の長さを超えたら <see cref="PixelSprite.CharOf"/> が落ちること
    /// （36番地を直接引こうとした場合）。</summary>
    [Fact]
    public void CharOfは上限を超えた添字を投げる()
    {
        Assert.Throws<ArgumentException>(() => PixelSprite.CharOf(36));
        // ⭐ 35番（上限ちょうど）は通る
        Assert.Equal('z', PixelSprite.CharOf(35));
    }

    /// <summary>⚠️ 読めない文字は IndexOf が -1 を返す（Parse 側で場所つきに変換される）。</summary>
    [Fact]
    public void IndexOfは読めない文字でマイナス1()
    {
        Assert.Equal(-1, PixelSprite.IndexOf('A'));
        Assert.Equal(-1, PixelSprite.IndexOf('!'));
        Assert.Equal(0, PixelSprite.IndexOf('.'));
    }

    /// <summary>⭐ 35色ぶんの Palette も普通に組み立てられ、<see cref="Palette.ColorOf"/> が
    /// 全添字を引けること（Species の Audit と同じ形の検査を単体でも押さえる）。</summary>
    [Fact]
    public void 三十五色のPaletteが全添字を引ける()
    {
        var colors = Enumerable.Range(0, 35).Select(i => $"#{i:x2}{i:x2}{i:x2}").ToArray();
        var palette = new Palette(colors!);
        Assert.Equal(35, palette.Count);
        for (byte i = 1; i <= 35; i++)
        {
            Assert.Equal(colors[i - 1], palette.ColorOf(i));
        }
    }
}
