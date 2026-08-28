using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>種族ごとの卵（<see cref="EggSkins"/>）。
///
/// ⭐ 卵は**輪郭1つ＋種族ごとの模様**で組む。⚠️ 見張るのは2つ:
/// 「種族ぶん焼けているか」と「**どれも別の柄に見えるか**」。
/// 後者が無いと、色だけ違って模様が同じ2種が黙って並ぶ ── 画面では見分けられない。</summary>
public class EggSkinTests
{
    private static readonly string Manifest =
        Path.Combine(AppContext.BaseDirectory, "paint", "paint-manifest.txt");

    /// <summary>⚠️ 1種も無ければ、検査が空回りしている。</summary>
    [Fact]
    public void 検査するものが在る()
    {
        Assert.NotEmpty(SpeciesTable.All);
        Assert.True(File.Exists(Manifest), "paint-manifest.txt が写されていない");
    }

    /// <summary>⭐ **どの種族にも意匠が決めてある。**
    /// ⚠️ 表に無い種族は既定（斑・生成り）へ落ちる ── 落ちること自体は安全だが、
    /// 種族を足したのに卵を決め忘れた、を見逃さない。</summary>
    [Fact]
    public void 全種族に意匠が決めてある()
    {
        var fallback = EggSkins.Of("この種族はいない");
        var missed = SpeciesTable.All
            .Where(s => EggSkins.Of(s.Id).Look == fallback.Look
                     && EggSkins.Of(s.Id).Ground == fallback.Ground
                     && EggSkins.Of(s.Id).Ink == fallback.Ink)
            .Select(s => s.Id).ToList();
        Assert.True(missed.Count == 0,
            "卵の意匠が決まっていない種族（`EggSkins.Table` に足す）: " + string.Join(", ", missed));
    }

    /// <summary>⭐ **種族ぶん焼けている。**⚠️ `sim egg-art` を走らせ忘れると、
    /// 画面は赤い「？」を出す（`LayoutDom.Paint`）── そこで気づくのは開いた人だけなので、
    /// 一覧（`paint-manifest.txt`）と種族表を突き合わせてここで止める。</summary>
    [Fact]
    public void 種族ぶんの卵が焼いてある()
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(Manifest))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3) known.Add(parts[0]);
        }
        var missing = SpeciesTable.All
            .Select(s => EggSkins.NameOf(s.Id))
            .Where(name => !known.Contains(name)).ToList();
        Assert.True(missing.Count == 0,
            "卵の絵が焼かれていない（`dotnet run --project EggCommand.Sim -- egg-art`）: "
            + string.Join(", ", missing));
    }

    /// <summary>⭐ 焼いた大きさは、形（<see cref="EggSkins.Shape"/>）と同じ。
    /// ⚠️ ここがずれると `slot.txt` の枠（176x220 ＝ 44x55 ドット×4）に合わず、
    /// 「引き伸ばさない」規則でコンソールに警告が出続ける。</summary>
    [Fact]
    public void 焼いた大きさは形と同じ()
    {
        var size = new Dictionary<string, (int W, int H)>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(Manifest))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3 && int.TryParse(parts[1], out int w) && int.TryParse(parts[2], out int h))
                size[parts[0]] = (w, h);
        }
        foreach (var species in SpeciesTable.All)
        {
            var got = size[EggSkins.NameOf(species.Id)];
            Assert.Equal((EggSkins.Shape.Width, EggSkins.Shape.Height), got);
        }
    }

    /// <summary>🔴 **どの2種も、同じ見た目にならない。**
    ///
    /// ⚠️ 「色だけ違って模様が同じ」も、「模様だけ違って色が同じ」も通してしまうと、
    /// 画面で見分けられない卵が並ぶ。⭐ **模様（塗り分け）と色の組**を1つの札にして比べる。</summary>
    [Fact]
    public void どの2種も同じ卵にならない()
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var species in SpeciesTable.All)
        {
            var sprite = EggSkins.Build(species.Id);
            var skin = EggSkins.Of(species.Id);
            // ⭐ 塗り分けそのもの＋色 ── どちらが違っても別の卵とみなす
            var mark = string.Concat(Enumerable.Range(0, sprite.Width * sprite.Height)
                .Select(i => (char)('0' + sprite.At(i % sprite.Width, i / sprite.Width))));
            string key = mark + "|" + skin.Ground + "|" + skin.Ink;
            Assert.False(seen.ContainsKey(key),
                $"{species.Id} と {(seen.TryGetValue(key, out var other) ? other : "?")} の卵が同じ見た目");
            seen[key] = species.Id;
        }
    }

    /// <summary>⭐ **どの2つの模様も、塗り分けが違う。**
    ///
    /// ⚠️ **これは「似て見えないこと」の検査ではない。**⭐ 見張るのは
    /// 「式を複製して直し忘れ、2つの模様が**まったく同じ塗り分け**になった」だけ。
    ///
    /// 🔴 **似て見えるかは、ここでは測れない。**画素の違う割合を数える案を実際に試して
    /// 捨てた（2026-08-27）── その数は「似ているか」ではなく**模様の量**を測ってしまう。
    /// 実測: 見た目が全く別物の `Crack`×`Plain` が 18%（一番近い判定）になる一方、
    /// 実際に見分けのつかなかった `Stars`×`Cross`（格子の間隔だけ違う版）は 35% と
    /// 「遠い」判定になった。⭐ **似ているかは目で見る** ── そのための
    /// `sim egg-try`（全模様を1枚に並べる）。</summary>
    [Fact]
    public void どの2つの模様も塗り分けが違う()
    {
        var seen = new Dictionary<string, EggSkins.Mode>(StringComparer.Ordinal);
        foreach (EggSkins.Mode look in Enum.GetValues(typeof(EggSkins.Mode)))
        {
            var sprite = EggSkins.BuildLook(look);
            var key = string.Concat(Enumerable.Range(0, sprite.Width * sprite.Height)
                .Select(i => (char)('0' + sprite.At(i % sprite.Width, i / sprite.Width))));
            Assert.False(seen.ContainsKey(key),
                $"{look} と {(seen.TryGetValue(key, out var other) ? other.ToString() : "?")} "
                + "の塗り分けが完全に同じ（式を直し忘れていないか）");
            seen[key] = look;
        }
    }

    /// <summary>⭐ 輪郭は種族で変わらない。⚠️ 模様の式が縁まで塗ると、
    /// 卵の形そのものが種族ごとに変わって見える（同じ卵の別の柄、が崩れる）。</summary>
    [Fact]
    public void 輪郭は種族で変わらない()
    {
        foreach (var species in SpeciesTable.All)
        {
            var sprite = EggSkins.Build(species.Id);
            for (int y = 0; y < sprite.Height; y++)
            {
                for (int x = 0; x < sprite.Width; x++)
                {
                    byte want = EggSkins.Shape.At(x, y);
                    // ⭐ 見るのは「透明」と「輪郭」だけ ── 中身は模様で変わってよい
                    if (want == 0 || want == EggSkins.Edge)
                        Assert.Equal(want, sprite.At(x, y));
                }
            }
        }
    }

    /// <summary>⭐ 模様は**中身からはみ出さない**。⚠️ 式は形を知らないので、
    /// 塗る側（<see cref="EggSkins.Build"/>）が中身だけに限っていることを固定する。</summary>
    [Fact]
    public void 模様は中身の外へ出ない()
    {
        foreach (var species in SpeciesTable.All)
        {
            var sprite = EggSkins.Build(species.Id);
            for (int y = 0; y < sprite.Height; y++)
            {
                for (int x = 0; x < sprite.Width; x++)
                {
                    if (sprite.At(x, y) == EggSkins.Mark)
                        Assert.Equal(EggSkins.Shell, EggSkins.Shape.At(x, y));
                }
            }
        }
    }

    /// <summary>⭐ どの卵も、地と模様が**どちらも見える量**ある。
    /// ⚠️ 模様が 0% だと「無地が2種」になり、100% 近いと地の色が消える
    /// ── どちらも見分けにくい卵になる。⚠️ 無地（`Plain`）だけは例外。</summary>
    [Fact]
    public void 模様は多すぎず少なすぎない()
    {
        foreach (var species in SpeciesTable.All)
        {
            var sprite = EggSkins.Build(species.Id);
            int shell = 0, mark = 0;
            for (int y = 0; y < sprite.Height; y++)
                for (int x = 0; x < sprite.Width; x++)
                {
                    if (sprite.At(x, y) == EggSkins.Shell) shell++;
                    else if (sprite.At(x, y) == EggSkins.Mark) mark++;
                }
            int inside = shell + mark;
            double share = (double)mark / inside;
            // ⚠️ 無地は「艶だけ」なので下限を外す（それが狙いの意匠）
            double floor_ = EggSkins.Of(species.Id).Look == EggSkins.Mode.Plain ? 0.0 : 0.04;
            Assert.True(share >= floor_ && share <= 0.75,
                $"{species.Id} の模様が {share:P0}（地 {shell} / 模様 {mark}）── 見分けにくい");
        }
    }
}
