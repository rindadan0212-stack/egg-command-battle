using System;
using System.Collections.Generic;
using System.IO;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>実物の骨組み（`Assets/Resources/Layouts/*.txt`）を全部検査する。
///
/// ⭐ **これがエンジン往復の置き換えです。**⚠️ 今日まで、重なり・はみ出し・
/// 押しどころの大きさは **Unity を起動して Play にして測る**しかありませんでした
/// （無変更でも19秒）。座標がデータに在るなら、ここで数えられます。
///
/// ⚠️ **実物の字幅までは見られません**（描かないと分からない）。
/// ⭐ 見られるのは枠どうしの関係 ── それが不具合の大半でした。</summary>
public class LayoutAssetTests
{
    private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "layouts");

    /// <summary>⭐ **`use=` を差し替えてから読む。**
    /// ⚠️ 差し替える前の木を検査しても、実際に出るものを見ていない。</summary>
    private static Layout Read(string id)
    {
        var raw = Layouts.Parse(id, File.ReadAllText(Path.Combine(Dir, id + ".txt")));
        return Layouts.Resolve(raw, name =>
        {
            var path = Path.Combine(Dir, name + ".txt");
            return File.Exists(path) ? Layouts.Parse(name, File.ReadAllText(path)) : null;
        });
    }

    public static IEnumerable<object[]> All()
    {
        foreach (var path in Directory.GetFiles(Dir, "*.txt"))
            yield return new object[] { Path.GetFileNameWithoutExtension(path) };
    }

    /// <summary>⚠️ 1枚も見つからなければ、**検査が空回りしている**。
    /// ⭐ 「不備 0 件」が「調べていない」を意味しないようにする。</summary>
    [Fact]
    public void 骨組みが見つかる()
    {
        Assert.True(Directory.Exists(Dir), $"{Dir} が無い（csproj のコピー設定を見る）");
        Assert.NotEmpty(Directory.GetFiles(Dir, "*.txt"));
    }

    [Theory]
    [MemberData(nameof(All))]
    public void 不備がない(string id)
    {
        Assert.Equal(new List<string>(), Layouts.Faults(Read(id)));
    }

    /// <summary>⚠️ **下の帯（232）に潜っていないか。**
    ///
    /// ⭐ 今日 BOX で実際に起きた不具合がこれです（一覧が帯の下へ入り、重なり11件）。
    /// 🔴 規則の本体は Core（<see cref="Layouts.DockFaults"/> ── 定数も
    /// <see cref="Layouts.TopBarHeight"/> / <see cref="Layouts.DockHeight"/>）。
    /// エディタの不備バンドも同じ物を呼ぶ ── 規則を2か所に書かない（2026-08-29。
    /// テストだけが知る規則だと、エディタで直しても後からここで落ちる）。</summary>
    [Theory]
    [MemberData(nameof(All))]
    public void 下の帯へ潜っていない(string id)
    {
        Assert.Equal(new List<string>(), Layouts.DockFaults(Read(id)).ConvertAll(f => f.Text));
    }

    /// <summary>⭐ 定数と実物（frame.txt）の一致を固定 ── 実物が動いたら定数側でも気づく形に。</summary>
    [Fact]
    public void 外枠の実物と定数が一致する()
    {
        LayoutNode? top = null, dock = null;
        foreach (var n in Read("frame").Roots) { if (n.Name == "top") top = n; if (n.Name == "dock") dock = n; }
        Assert.NotNull(top);
        Assert.NotNull(dock);
        Assert.Equal(Layouts.TopBarHeight, top!.Height);
        Assert.Equal(Layouts.DockHeight, dock!.Height);
        Assert.Equal(Layouts.ScreenHeight - Layouts.DockHeight, dock!.Top);
    }

    // ── PartId / PartLine ── ⭐ 「掴めるようにする」でなく「どこの行か言える」──

    /// <summary>🔴 **画面の全部が出所を言える、の証明。**
    ///
    /// ⚠️ 実物32枚すべての、`Resolve` した木の**全節点**を辿り、
    /// `LineNumber >= 0`（自前の行）か `PartId != null && PartLine >= 0`
    /// （差し込まれた部品の行）の**どちらか一方**を必ず満たすことを確かめる。
    /// ⭐ 片方も満たさない節点が1つでもあれば落ちる（黙って出所不明を許さない）。</summary>
    [Theory]
    [MemberData(nameof(All))]
    public void 全ての節点が出所を言える(string id)
    {
        foreach (var root in Read(id).Roots) AssertHasOrigin(id, root);
    }

    private static void AssertHasOrigin(string id, LayoutNode node)
    {
        bool own = node.LineNumber >= 0;
        bool part = node.PartId != null && node.PartLine >= 0;
        Assert.True(own || part,
            $"{id}/{node.Name}: 出所が無い"
            + $"（LineNumber={node.LineNumber} PartId={node.PartId ?? "null"} PartLine={node.PartLine}）");
        foreach (var child in node.Children) AssertHasOrigin(id, child);
    }

    /// <summary>⭐ **数で桁を合わせる。**⚠️ 実測（コメント・空行を除く）:
    /// box は自前12行・差し込み47行 ── `panel`(28) + `sortbar`(3) + `sortchips`(6)
    /// + `cell`(5)×2（`cellA`/`cellB`）= 47。⭐ ここが崩れたら、
    /// 出所の付け方（`Rename`）のどこかが二重に数えたか、取りこぼしている。
    /// ⚠️ 自前が 11→12 になったのは 2026-08-29「家系図」釦（`btree`）を足したため
    /// （差し込みは変わらない ── `btree` は `use=panel` の中身ではなく box.txt 自前の行）。</summary>
    [Fact]
    public void boxの自前と差し込みの数()
    {
        int own = 0, part = 0;
        foreach (var root in Read("box").Roots) CountOrigins(root, ref own, ref part);
        Assert.Equal(12, own);
        Assert.Equal(47, part);
    }

    /// <summary>⭐ breed は自前15行・差し込み75行 ──
    /// `panelmini`(28)×2（`pfill`/`qfill`）+ `sortbar`(3) + `sortchips`(6)
    /// + `cell`(5)×2（`cellA`/`cellB`）= 75。</summary>
    [Fact]
    public void breedの自前と差し込みの数()
    {
        int own = 0, part = 0;
        foreach (var root in Read("breed").Roots) CountOrigins(root, ref own, ref part);
        Assert.Equal(15, own);
        Assert.Equal(75, part);
    }

    private static void CountOrigins(LayoutNode node, ref int own, ref int part)
    {
        if (node.LineNumber >= 0) own++;
        else if (node.PartId != null) part++;
        foreach (var child in node.Children) CountOrigins(child, ref own, ref part);
    }
}
