using System;
using System.IO;
using System.Linq;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>生まれたその場の「分解」「くわしく見る」（作者の指示 2026-08-29）の検査。
///
/// ⭐ `fanfare.txt` は Core の `Layouts.Parse` で直に実物を読める（`LayoutAssetTests` と
/// 同じ手段）。⚠️ `Shell.cs`（`Cheer`/`ClaimBorn` の配線）は `Shell`/`Deeds`/`LayoutDom`
/// 等 Web 専用の依存が多くコンパイルには持ち込めないので、`TapCatalogTests` と同じ
/// 「`websrc\Shell.cs` をテキストとして読み直す」形にする。</summary>
public class FanfareChoiceTests
{
    private static readonly string LayoutDir = Path.Combine(AppContext.BaseDirectory, "layouts");
    private static readonly string WebSrc = Path.Combine(AppContext.BaseDirectory, "websrc");
    private static string ShellSource => File.ReadAllText(Path.Combine(WebSrc, "Shell.cs"));

    private static Layout Fanfare() =>
        Layouts.Parse("fanfare", File.ReadAllText(Path.Combine(LayoutDir, "fanfare.txt")));

    /// <summary>🔴 覆い（`dim`）より**後**に書かれていること。
    /// ⚠️ `LayoutDom` は後に書いた節点が上に乗るので、先に書くと押しどころが
    /// 覆い（`tap=cheer`）に食われ、押しても閉じるだけになる。</summary>
    [Fact]
    public void 釦は覆いより後に書かれている()
    {
        var layout = Fanfare();
        var dim = layout.Roots.First(n => n.Kind == "veil");
        var choice = layout.Roots.First(n => n.Name == "choice");
        Assert.True(choice.LineNumber > dim.LineNumber,
            $"choice（{choice.LineNumber}行目）が dim（{dim.LineNumber}行目）より前に書かれている");
    }

    /// <summary>⚠️ **卵を得たとき（`Cheer.EggGot`）には出さない** ── 2つの釦とも
    /// `when=creature` で出し分けている。</summary>
    [Fact]
    public void 分解とくわしく見るはcreatureのときだけ出る()
    {
        var choice = Fanfare().Roots.First(n => n.Name == "choice");
        var fuse = choice.Children.First(n => n.Name == "fuse");
        var detail = choice.Children.First(n => n.Name == "detail");
        Assert.Equal("creature", fuse.Option("when"));
        Assert.Equal("creature", detail.Option("when"));
    }

    /// <summary>⚠️ 分解は既にある口（fuse）へ。くわしく見るは **BOX の詳細札へ着地**
    /// ── grow（全行が不可逆の EXP 消費）へは繋がない（2026-08-29 付け替え）。
    /// ⭐ tap 名の存在だけでなく**意味的な着地先**まで見る ── 前の版はここが
    /// 「grow へ繋がっていること」を固定して、誤着地のバグを保護していた。</summary>
    [Fact]
    public void 分解とくわしく見るの着地先()
    {
        var choice = Fanfare().Roots.First(n => n.Name == "choice");
        Assert.Equal("fuse", choice.Children.First(n => n.Name == "fuse").Option("tap"));
        Assert.Equal("detail", choice.Children.First(n => n.Name == "detail").Option("tap"));
        string block = Block(ShellSource, "case \"detail\":");
        Assert.Contains("Now_Sheet = Sheet.Box", block);
        Assert.DoesNotContain("Panel.Grow", block);
    }

    /// <summary>`Cheer.EggGot`（卵を得た）は個体でない・`Cheer.Born`（生まれた）は
    /// 個体 ── 卵か個体かを見分ける唯一の出所（`IsCreature`）が、2つの工場で
    /// 正しく逆になっていること。</summary>
    [Fact]
    public void CheerのEggGotとBornはIsCreatureが逆になっている()
    {
        string src = ShellSource;
        int eggGot = src.IndexOf("public static Cheer EggGot(Egg egg)", StringComparison.Ordinal);
        int born = src.IndexOf("public static Cheer Born(Creature creature)", StringComparison.Ordinal);
        Assert.True(eggGot >= 0 && born >= 0, "Shell.cs: Cheer.EggGot/Cheer.Born が見つからない");
        Assert.True(born > eggGot, "Shell.cs: Born が EggGot より前に出てきた（探索範囲の前提が崩れた）");

        string eggGotBody = src.Substring(eggGot, born - eggGot);
        string bornBody = src.Substring(born, Math.Min(400, src.Length - born));

        Assert.Contains("IsCreature: false", eggGotBody);
        Assert.Contains("IsCreature: true", bornBody);
    }

    /// <summary>🔴 開く前に祝いを閉じ、生まれた個体を「いま選んでいる個体」にする
    /// （`ClaimBorn()`）を、fuse（分解）・detail（くわしく見る）の**両方**が呼んでいること。
    /// ⭐ さらに fuse は本人を分解候補へ**事前選択**する（`Melts.Add` ── 2026-08-29）。</summary>
    [Fact]
    public void fuseとdetailはClaimBornを呼びfuseは本人を事前選択する()
    {
        string fuse = Block(ShellSource, "case \"fuse\":");
        Assert.Contains("ClaimBorn()", fuse);
        Assert.Contains("Melts.Add", fuse);
        Assert.Contains("ClaimBorn()", Block(ShellSource, "case \"detail\":"));
    }

    /// <summary>🔴 事前選択された本人が候補一覧から消えないこと（`Deeds.Food` の除外緩和
    /// ── 「見ている本人は外す」の規則のままだと、fuse の事前選択が一覧に出ない）。</summary>
    [Fact]
    public void 分解候補はMeltsに居る本人を外さない()
    {
        string src = DeedsSource;
        int at = src.IndexOf("public static IReadOnlyList<Creature> Food(Shell s)", StringComparison.Ordinal);
        Assert.True(at >= 0, "Deeds.cs: Food が見つからない");
        int end = src.IndexOf("public static void Mark", at, StringComparison.Ordinal);
        Assert.True(end > at, "Deeds.cs: Mark が見つからない（Food の終端が決められない）");
        Assert.Contains("Melts.Contains", src.Substring(at, end - at));
    }

    /// <summary>`case "x":` の行頭から最初の `break;` まで ── 複数行に育った case を
    /// まとめて検査するため（1行だけ読む形だと、複数行化した瞬間に検査が空振りする）。</summary>
    private static string Block(string src, string mark)
    {
        int at = src.IndexOf(mark, StringComparison.Ordinal);
        Assert.True(at >= 0, $"Shell.cs: {mark} が見つからない");
        int end = src.IndexOf("break;", at, StringComparison.Ordinal);
        return end < 0 ? src.Substring(at) : src.Substring(at, end - at);
    }

    private static string DeedsSource => File.ReadAllText(Path.Combine(WebSrc, "Deeds.cs"));
}
