using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>盤に出る字と、演出を**順番に**出す仕掛けの見張り。
///
/// ⚠️ `Deeds.cs` は Web 専用の依存（`Shell`/`Sheets`/`Face`）が多くコンパイルには
/// 持ち込めないので、`ScenesTests`/`TapCatalogTests` と同じ「**テキストとして読むだけ**」の形。</summary>
public class BattleWordTests
{
    private static readonly string WebSrc = Path.Combine(AppContext.BaseDirectory, "websrc");
    private static string Deeds() => File.ReadAllText(Path.Combine(WebSrc, "Deeds.cs"));
    private static string Fx() => File.ReadAllText(Path.Combine(WebSrc, "fx.js"));

    [Fact]
    public void 検査するものが在る()
    {
        Assert.Contains("BattleEventKind.Missed", Deeds());
        Assert.Contains("eggFx", Fx());
    }

    /// <summary>⭐ **効き目が付かなかったときは「MISS」**（2026-08-28・作者の指示）。
    /// ⚠️ 免疫（「免疫」）・ブロック（「通らない」）とは**原因が違う**ので別の字のまま。</summary>
    [Fact]
    public void 通らなかった弱化はMISSと出る()
    {
        var line = Deeds().Split('\n')
            .FirstOrDefault(l => l.Contains("case BattleEventKind.Missed:"));
        Assert.NotNull(line);
        Assert.Contains("\"MISS\"", line!);
    }

    /// <summary>⚠️ **この字が出る道が実際に在ることの証明。**
    /// ⭐ 字だけ直しても、そもそも <see cref="BattleEventKind.Missed"/> が起きなければ
    /// 画面には一生出ない ── 実際に戦わせて、起きることを確かめる。</summary>
    [Fact]
    public void 外れは実際に起きる()
    {
        int missed = 0;
        for (int seed = 1; seed <= 12 && missed == 0; seed++)
        {
            var game = Games.NewGame(seed);
            var state = Battle.CreateBattle(Games.PartyOf(game),
                Nests.MakeDefenders(game.RngNest, Nests.ById("thicket-fang")));
            for (int guard = 0; guard < 400; guard++)
            {
                var actor = Battle.NextActor(state);
                if (actor == null) break;
                Battle.PerformAction(state, actor, Ai.ChooseAction(state, actor));
            }
            missed += state.Log.Count(e => e.Kind == BattleEventKind.Missed);
        }
        Assert.True(missed > 0, "12戦してもどの弱化も外れなかった ── MISS が画面に出る道が無い");
    }

    /// <summary>🔴 **`animation-delay` は `cssText` より後に書く。**
    ///
    /// ⚠️ `fx.js` は出す物の位置を `el.style.cssText = …` で入れるが、これは
    /// **inline style を丸ごと差し替える**。先に書いた `animation-delay` は消える。
    /// ⭐ 実際に踏んだ（2026-08-28）── C# は正しい秒を送っていたのに、DOM には
    /// 一つも残っていなかった。⚠️ 送った値のログは正しかったので、**送信側だけ見ていると気づけない**。</summary>
    [Fact]
    public void 出す間はcssTextより後に書く()
    {
        string fx = Fx();
        int css = fx.LastIndexOf("style.cssText", StringComparison.Ordinal);
        int delay = fx.IndexOf("animationDelay", StringComparison.Ordinal);
        Assert.True(css >= 0, "cssText への差し替えが見つからない（検査が空回り）");
        Assert.True(delay >= 0, "animationDelay の書き込みが見つからない");
        Assert.True(delay > css,
            "animation-delay を cssText より先に書いている ── 丸ごと消えるので順番に出なくなる");
    }

    /// <summary>⭐ 順番に出す間は `Core.Beats` が唯一の出所。
    /// ⚠️ JS 側で数え直すと、待たせる秒（`Deeds.Beat` が伸ばす `Wait`）と2つに割れる。</summary>
    [Fact]
    public void 出す間の数はCoreが持つ()
    {
        Assert.Contains("Beats.PopStep", Deeds());
        Assert.Contains("Beats.PopMost", Deeds());
        Assert.True(Beats.PopStep > 0 && Beats.PopStep < Beats.Settle,
            "1つ出すごとの間は、着弾のあとの間より短いこと");
        Assert.True(Beats.PopMost > Beats.PopStep, "出し切る上限は、1つぶんの間より長いこと");
    }

    /// <summary>🔴 **描く側は `NextActor` を呼ばない**（2026-08-28 の不具合の釘）。
    /// ⚠️ あれは進める関数なので、描くたび・押すたびに毒が入る。
    /// ⭐ 聞くだけの入口（`Battle.Standing` / `Battle.StandingAlly`）を使う。</summary>
    [Fact]
    public void 描く側と押す側は進める関数を呼ばない()
    {
        // ⚠️ 註（`//` の行）は数えない ── **なぜ呼ばないか**を書いた註にも同じ語が出る
        string deeds = Code(Deeds()), sheets = Code(File.ReadAllText(Path.Combine(WebSrc, "Sheets.cs")));

        // ⭐ `Deeds.Beat` の中の1回だけが正しい呼び出し
        int calls = Regex.Matches(deeds, @"Battle\.NextActor\(").Count;
        Assert.True(calls == 1,
            $"Deeds.cs の NextActor 呼び出しが {calls} 箇所 ── 進めてよいのは Beat の中の1回だけ");
        Assert.Contains("Battle.Standing(state)", deeds);

        Assert.DoesNotContain("NextActor", sheets);
        Assert.Contains("Battle.StandingAlly(state)", sheets);
    }

    /// <summary>註と空行を落とした、実際に走る字だけ。</summary>
    private static string Code(string text) => string.Join("\n",
        text.Split('\n').Where(l => !l.TrimStart().StartsWith("//")
                                  && !l.TrimStart().StartsWith("*")));
}
