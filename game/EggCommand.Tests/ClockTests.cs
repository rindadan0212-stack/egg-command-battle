using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>時間の見せ方（<see cref="Rarities.Clock"/>）。⚠️ ここが唯一の出所 ──
/// 呼び手（孵化器の卵・巣の残り時間・控えの古さ・棚の卵の待ち時間）は4か所とも
/// これを呼ぶだけで書式を持たない。⭐ 表は作者の指示（2026-08-28
/// 「〇h〇m〇s にし、h 表記があるときは s 表記を省略」）をそのまま検査にした。</summary>
public class ClockTests
{
    [Theory]
    [InlineData(3 * 3600, "3h")]                          // 3時間0分0秒
    [InlineData(2 * 3600 + 30 * 60 + 15, "2h30m")]         // 2時間30分15秒 ⭐ h があるので s は出さない
    [InlineData(59 * 60 + 59, "59m59s")]                   // 59分59秒
    [InlineData(5 * 60, "5m")]                             // 5分0秒
    [InlineData(45, "45s")]                                // 45秒
    [InlineData(0, "0s")]                                  // 0秒
    public void 時間の書式は0の単位を出さない(int seconds, string expected)
    {
        Assert.Equal(expected, Rarities.Clock(seconds));
    }

    /// <summary>⚠️ 負の秒（時計のずれ等）でも壊れない ── 既存の安全弁（`seconds < 0` の丸め）はそのまま。</summary>
    [Fact]
    public void 負の秒は0秒として出す()
    {
        Assert.Equal("0s", Rarities.Clock(-5));
    }
}
