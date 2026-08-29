using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EggCommand.Core;
using EggCommand.Sim;
using Xunit;
using Xunit.Abstractions;

namespace EggCommand.Tests;

/// <summary>骨組みの `icon`/`paint` の枠が、実寸目録（`icon-manifest.txt`/`paint-manifest.txt`、
/// ドット数×4）と合っているかを見る（ドット絵化計画 段取り4「1ドット=4px」統一・2026-08-29）。
///
/// ⚠️ 2種類の検査に分けてある（`ArtTests` と同じ「落とす／落とさない」の考え方）:
/// - ⚠️ **静的**（<see cref="骨組みのpic直書きノードの不一致を数える"/>）: 骨組みの `pic=` が
///   **直に**書いてある節点だけを見る。⚠️ `assets/layouts/*.txt` の中には、この作業の対象外
///   （`square.txt`/`trail.txt` など。触ってはいけないファイル）にも古くからの不一致が
///   ある ── ここを `Assert` で落とすと、直せない箇所のせいで `dotnet test` が
///   永久に赤くなる。⭐ だから**数えるだけ**（`ArtTests.死蔵の絵を数える` と同じ流儀）。
/// - 🔴 **動的相当**（<see cref="status欄は状態異常16種類のどれでも枠と合う"/>）: `unit.txt` の
///   `sicon` は `bind=` で実行時にしか絵の名前が決まらない（静的走査では見えない）。
///   ⚠️ `LayoutDom.Render`/`DrawnMismatches` は Blazor 専用の依存を持つ `EggCommand.Web` に
///   在り、`EggCommand.Tests.csproj`（コンパイル対象の追加）は今回の作業範囲の外なので、
///   直接は呼べない。⭐ 代わりに、`LayoutDom.FitDotsStyle` と**同じ式**（実ドット数×4 が
///   節点の幅高と一致するか）を、`Core.Art.StatusIcon` が返す**実物の名前 16種類全部**で
///   直に確かめる ── 判定の中身は同じで、経路だけが違う。</summary>
public class PicFrameSizeTests
{
    private readonly ITestOutputHelper _out;
    public PicFrameSizeTests(ITestOutputHelper output) => _out = output;

    private static readonly string LayoutsDir = Path.Combine(AppContext.BaseDirectory, "layouts");
    private static readonly string IconDir = Path.Combine(AppContext.BaseDirectory, "icon");

    private static Layout Read(string id)
    {
        var raw = Layouts.Parse(id, File.ReadAllText(Path.Combine(LayoutsDir, id + ".txt")));
        return Layouts.Resolve(raw, name =>
        {
            var path = Path.Combine(LayoutsDir, name + ".txt");
            return File.Exists(path) ? Layouts.Parse(name, File.ReadAllText(path)) : null;
        });
    }

    private static Dictionary<string, (int W, int H)> ParseSizeLines(IEnumerable<string> lines)
    {
        var map = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3) continue;
            if (int.TryParse(parts[1], out var w) && int.TryParse(parts[2], out var h))
                map[parts[0]] = (w, h);
        }
        return map;
    }

    private static Dictionary<string, (int W, int H)> IconSizes() =>
        ParseSizeLines(IconManifestTool.ComputeManifestLines(IconDir));

    private static Dictionary<string, (int W, int H)> PaintSizes() =>
        ParseSizeLines(CheckedInAssets.PaintManifestLines());

    // ── 静的（報告のみ・落とさない）───────────────────────

    /// <summary>⚠️ **数えるだけ**（落とさない）。`square.txt`/`trail.txt` など
    /// このタスクの対象外ファイルに残る、既存の不一致を可視化するための欄。
    /// ⭐ `unit.txt`（このタスクで直した骨組み）に不一致が残っていたら、それは対象内 ──
    /// そちらは下の動的相当の検査が**落とす**側で拾う。</summary>
    [Fact]
    public void 骨組みのpic直書きノードの不一致を数える()
    {
        Assert.True(Directory.Exists(LayoutsDir), $"{LayoutsDir} が無い（csproj のコピー設定を見る）");
        var iconSizes = IconSizes();
        var paintSizes = PaintSizes();

        var mismatches = new List<string>();
        foreach (var path in Directory.GetFiles(LayoutsDir, "*.txt").OrderBy(p => p, StringComparer.Ordinal))
        {
            string id = Path.GetFileNameWithoutExtension(path);
            Walk(id, Read(id).Roots, iconSizes, paintSizes, mismatches);
        }

        _out.WriteLine(mismatches.Count == 0
            ? "枠と絵（pic= 直書き）の不一致: 0 件"
            : $"枠と絵（pic= 直書き）の不一致: {mismatches.Count} 件\n  " + string.Join("\n  ", mismatches));

        // 🔴 **数だけでなく、名前ごと固定する**（2026-08-29）。
        //    ⚠️ 「数えて出すだけ」だと、**新しい不一致が増えても気づけない**
        //    ── まさにそれで状態異常の破綻が見過ごされていた。
        // ⭐ **0件になった**（2026-08-29・作者の指示「①PNGを枠の大きさに描き直す」）。
        //    ⚠️ かつてはすごろくの盤（`square`/`trail`）に6件あり、絵（32ドット＝128px）が
        //    枠より大きいまま置かれていた ── さいころ12個は枠46px間隔に128pxが並び、
        //    **白い塊**にしか見えなかった（実測 2026-08-29）。
        //    ⭐ いまは `tools/icon-fit.mjs` が原画（`assets/ui/icon-src/`）から枠の大きさで
        //    焼き直し、骨組みの枠もその実寸に合わせてある。
        // ⭐ **1件でも増えたら、ここが落ちる。**絵か枠を直すこと（表に足して逃げない）。
        var 既知 = new string[0];
        var いま = mismatches
            .Select(m => m.Substring(0, m.IndexOf(' ')))
            .OrderBy(s => s, StringComparer.Ordinal).ToArray();
        Assert.Equal(既知.OrderBy(s => s, StringComparer.Ordinal).ToArray(), いま);
    }

    private static void Walk(string layoutId, IReadOnlyList<LayoutNode> nodes,
        Dictionary<string, (int W, int H)> iconSizes, Dictionary<string, (int W, int H)> paintSizes,
        List<string> into)
    {
        foreach (var node in nodes)
        {
            if (node.Kind == "icon" || node.Kind == "paint")
            {
                string pic = node.Option("pic");
                var sizes = node.Kind == "icon" ? iconSizes : paintSizes;
                if (pic != null && sizes.TryGetValue(pic, out var dots))
                {
                    float w = dots.W * 4f, h = dots.H * 4f;
                    if (Math.Abs(node.Width - w) > 0.5f || Math.Abs(node.Height - h) > 0.5f)
                        into.Add($"{layoutId}/{node.Name} {node.Kind} pic={pic}: "
                            + $"実寸{w}x{h} なのに枠は{node.Width}x{node.Height}");
                }
            }
            Walk(layoutId, node.Children, iconSizes, paintSizes, into);
        }
    }

    // ── 動的相当（unit.txt に限って落とす）─────────────────

    private static LayoutNode Find(IEnumerable<LayoutNode> nodes, string name)
    {
        foreach (var node in nodes)
        {
            if (node.Name == name) return node;
            var hit = Find(node.Children, name);
            if (hit != null) return hit;
        }
        return null;
    }

    /// <summary>🔴 **これが今回の本題。**16種類の状態異常のうち、どれが乗っても
    /// `sicon` の枠（`unit.txt`）と実寸目録が一致すること ── 一致しなければ、
    /// 戦闘中にその状態異常が付いた瞬間、絵が枠からはみ出す／潰れる（実測した現行バグ）。
    /// ⚠️ 例外表は作らない（1種類でも漏らすと同じ事故を繰り返す）。</summary>
    [Fact]
    public void status欄は状態異常16種類のどれでも枠と合う()
    {
        var sicon = Find(Read("unit").Roots, "sicon");
        Assert.NotNull(sicon);
        var iconSizes = IconSizes();

        var bad = new List<string>();
        foreach (StatusKind kind in Enum.GetValues(typeof(StatusKind)))
        {
            string pic = Art.StatusIcon(kind);
            Assert.True(iconSizes.TryGetValue(pic, out var dots),
                $"icon-manifest.txt に {pic}（{kind}）が無い");
            float w = dots.W * 4f, h = dots.H * 4f;
            if (Math.Abs(sicon.Width - w) > 0.5f || Math.Abs(sicon.Height - h) > 0.5f)
                bad.Add($"{kind}({pic}): 実寸{w}x{h} なのに枠は{sicon.Width}x{sicon.Height}");
        }
        Assert.True(bad.Count == 0, "sicon の枠と合わない状態異常: " + string.Join(", ", bad));
    }

    /// <summary>🔴 **`sicon` と `snum` が別の相手を指さない。**⚠️ `cols=`/`max=` がずれると、
    /// N番目の絵と N番目の数が食い違う（`unit.txt` の注記参照）。</summary>
    [Fact]
    public void siconとsnumは同じ数と同じ間隔()
    {
        var roots = Read("unit").Roots;
        var sicon = Find(roots, "sicon");
        var snum = Find(roots, "snum");
        Assert.NotNull(sicon);
        Assert.NotNull(snum);

        Assert.Equal(sicon.Option("cols"), snum.Option("cols"));
        Assert.Equal(sicon.Option("max"), snum.Option("max"));
        // ⭐ 同じ間隔＝同じ歩幅（幅＋隙間）で列が並ぶこと（中身が icon/label で
        //   幅・隙間の値そのものが違っても、歩幅が揃っていれば列は一致する）。
        float siconStep = sicon.Width + sicon.Number("gap", 0);
        float snumStep = snum.Width + snum.Number("gap", 0);
        Assert.Equal(siconStep, snumStep);
    }
}
