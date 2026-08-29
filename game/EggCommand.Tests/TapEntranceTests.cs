using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using EggCommand.Core;
using EggCommand.Web;
using Xunit;

namespace EggCommand.Tests;

/// <summary>「受け側は生きているが UI から入れない」事故を二度と起こさないための検査
/// （保存の控え `keep` の入口消失 ── 2026-08-29 発覚 ── が原型。ホーム改修で釦の行だけが
/// 消え、セーブ書き出しに一度も触れない状態が誰にも気づかれず続いていた）。
///
/// ⭐ `TapCatalog.Names`（Shell の switch と一致することは `TapCatalogTests` が別に保証）を
/// 1つずつ、「どれかの骨組みに `tap=` がある」か「コードが data-tap を直に出す
/// （<see cref="CodeEntrances"/>）」かで突き合わせる。逆向き（骨組みに在るのに受け手が
/// 居ない＝綴り違い）も同じ材料で見る。`hold=` も対で守る。</summary>
public class TapEntranceTests
{
    private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "layouts");
    private static readonly string WebSrc = Path.Combine(AppContext.BaseDirectory, "websrc");

    /// <summary>⚠️ `use=` を差し替えてから読む（`LayoutAssetTests.Read` と同じ）──
    /// 差し込まれた側の `tap=`/`hold=` は冠付きの名前（`bar-toggle` 等）に変わるのが実物で、
    /// 生の値（`toggle`）のままでは Shell に届かない。</summary>
    private static Layout Read(string id)
    {
        var raw = Layouts.Parse(id, File.ReadAllText(Path.Combine(Dir, id + ".txt")));
        return Layouts.Resolve(raw, name =>
        {
            var path = Path.Combine(Dir, name + ".txt");
            return File.Exists(path) ? Layouts.Parse(name, File.ReadAllText(path)) : null;
        });
    }

    /// <summary>⭐ 部品（`use=` で差される側）は**単独では数えない** ── 差された側で
    /// 冠付きの名前として数える。⚠️ どのファイルが部品かの一覧を手で書かない ──
    /// `use=` の実物から集める（一覧を手で持つと、それ自体が今回と同じ「ずれの温床」）。</summary>
    private static (HashSet<string> Taps, HashSet<string> Holds) Entrances()
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var all = new List<string>();
        foreach (var path in Directory.GetFiles(Dir, "*.txt"))
            all.Add(Path.GetFileNameWithoutExtension(path));
        foreach (var id in all)
            Collect(Layouts.Parse(id, File.ReadAllText(Path.Combine(Dir, id + ".txt"))).Roots,
                n => { if (n.Option("use") is string u) used.Add(u); });

        var taps = new HashSet<string>(StringComparer.Ordinal);
        var holds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in all)
        {
            if (used.Contains(id)) continue;   // 部品は土台（Resolve 済み）側で数える
            Collect(Read(id).Roots, n =>
            {
                if (n.Option("tap") is string t) taps.Add(t);
                if (n.Option("hold") is string h) holds.Add(h);
            });
        }
        return (taps, holds);
    }

    private static void Collect(IReadOnlyList<LayoutNode> nodes, Action<LayoutNode> see)
    {
        foreach (var n in nodes) { see(n); Collect(n.Children, see); }
    }

    /// <summary>コードが `data-tap` を直に出す入口（骨組みに `tap=` が無いのが正しいもの）。
    /// ⚠️ 増やすときは出所（ファイルと理由）を必ず書く ── 書けない名前は入口消失。</summary>
    private static readonly Dictionary<string, string> CodeEntrances = new(StringComparer.Ordinal)
    {
        ["square"] = "Board.cs が data-tap を直に出す（マスの位置は実行時に決まる）",
        ["slot"] = "Incubator.cs が data-tap を直に出す（巣5つの位置は Spots が持つ）",
    };

    [Fact]
    public void 全てのtapに入口がある()
    {
        var (taps, _) = Entrances();
        var lost = new List<string>();
        foreach (var name in TapCatalog.Names)
            if (!taps.Contains(name) && !CodeEntrances.ContainsKey(name)) lost.Add(name);
        Assert.True(lost.Count == 0,
            "受け側は生きているのに、どの骨組みからも押せない tap: " + string.Join(", ", lost)
            + "（コードから出す正当な入口なら CodeEntrances に出所つきで足すこと）");
    }

    /// <summary>⭐ 逆向き ── 綴り違いや消し忘れの押しどころを落とす。</summary>
    [Fact]
    public void 骨組みのtapは全て受け手が居る()
    {
        var (taps, _) = Entrances();
        var ghost = new List<string>();
        foreach (var t in taps) if (Array.IndexOf(TapCatalog.Names, t) < 0) ghost.Add(t);
        Assert.True(ghost.Count == 0, "骨組みに在るが誰も受けない tap: " + string.Join(", ", ghost));
    }

    // ── hold も同じ守り ──────────────────────────────

    /// <summary>`Shell.Hold` の case を実物から抜き出す（`TapCatalogTests` と同じ読み方
    /// ── Web はコンパイルに持ち込めないので `websrc\Shell.cs` をテキストで読む）。</summary>
    private static List<string> HoldCases()
    {
        string src = File.ReadAllText(Path.Combine(WebSrc, "Shell.cs"));
        int start = src.IndexOf("public void Hold(string what, string at)", StringComparison.Ordinal);
        Assert.True(start >= 0, "Shell.cs: Hold が見つからない");
        int end = src.IndexOf("private void Choose(int i)", start, StringComparison.Ordinal);
        Assert.True(end > start, "Shell.cs: Choose が見つからない（Hold の終端が決められない）");
        var found = new List<string>();
        foreach (Match m in Regex.Matches(src.Substring(start, end - start), "case \"([^\"]+)\":"))
            found.Add(m.Groups[1].Value);
        return found;
    }

    [Fact]
    public void 全てのholdに入口がある()
    {
        var (_, holds) = Entrances();
        var lost = new List<string>();
        foreach (var name in HoldCases()) if (!holds.Contains(name)) lost.Add(name);
        Assert.True(lost.Count == 0, "受け側は生きているのに、どの骨組みからも長押しできない hold: "
            + string.Join(", ", lost));
    }

    [Fact]
    public void 骨組みのholdは全て受け手が居る()
    {
        var (_, holds) = Entrances();
        var cases = new HashSet<string>(HoldCases(), StringComparer.Ordinal);
        var ghost = new List<string>();
        foreach (var h in holds) if (!cases.Contains(h)) ghost.Add(h);
        Assert.True(ghost.Count == 0, "骨組みに在るが誰も受けない hold: " + string.Join(", ", ghost));
    }
}
