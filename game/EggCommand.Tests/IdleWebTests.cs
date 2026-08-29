using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace EggCommand.Tests;

/// <summary>放置の帯（ホームの画面）の見張り。⚠️ `Idle.cs`/`AppPage.razor`/`tap.js`/`stage.css`
/// はどれも Web 専用の依存（`Shell`/`Sheets`/ブラウザの DOM）を持つのでコンパイルには
/// 持ち込めない ── `StageCssTests`/`BattleWordTests` と同じ「**ソースをテキストとして
/// 読むだけ**」の形（`websrc\*` は csproj が写す）。
///
/// ⭐ 直前の担当交代（2026-08-28）で壊れていた3つを見張る:
/// 1. `Core.Idle.FoeAt`/`PaletteAt` の削除で建たなくなっていたビルド、
/// 2. `Core.Idle.Advance` の戻り値を捨てていて卵が棚に入らなかった穴、
/// 3. 放置の帯が編成（`PartyKind.Idle`）でなく巣の編成（既定 `PartyKind.Nest`）を
///    描いていた食い違い。</summary>
public class IdleWebTests
{
    private static readonly string WebSrc = Path.Combine(AppContext.BaseDirectory, "websrc");
    private static string IdleCs() => File.ReadAllText(Path.Combine(WebSrc, "Idle.cs"));
    private static string AppPage() => File.ReadAllText(Path.Combine(WebSrc, "AppPage.razor"));
    private static string StageCss() => File.ReadAllText(Path.Combine(WebSrc, "stage.css"));
    private static string TapJs() => File.ReadAllText(Path.Combine(WebSrc, "tap.js"));

    /// <summary>註と空行を落とした、実際に走る字だけ（`BattleWordTests.Code` と同じ形）。</summary>
    private static string Code(string text) => string.Join("\n",
        text.Split('\n').Where(l => !l.TrimStart().StartsWith("//")
                                  && !l.TrimStart().StartsWith("*")));

    [Fact]
    public void 検査するものが在る()
    {
        Assert.Contains("class Idle", IdleCs());
        Assert.Contains("BeatIdle", AppPage());
        Assert.Contains("idle-come", StageCss());
        Assert.Contains("eggTap", TapJs());
    }

    /// <summary>🔴 放置の帯は `Games.PartyOf(game, PartyKind.Idle)` を描く。
    /// ⚠️ 引数なしの既定は `PartyKind.Nest`（巣へ連れて行く編成）── 既定のまま呼ぶと、
    /// 画面には「編成したものと違う3体」が並ぶ（2026-08-28・作者の報告）。</summary>
    [Fact]
    public void 放置の帯はIdle編成を描く()
    {
        string src = Code(IdleCs());
        Assert.Contains("PartyOf(game, PartyKind.Idle)", src);
        // ⚠️ 引数なしの既定（＝ Nest）を呼んでいないことも直に確かめる
        Assert.DoesNotContain("PartyOf(game)", src);
    }

    /// <summary>🔴 `Idle.cs` はもう `Core.Idle.FoeAt`/`PaletteAt` を呼ばない
    /// （2026-08-28・前任者が `Core.Idle` を作り直した際に削除済み ── 呼び続けているなら
    /// ビルドが通らない）。⭐ 代わりに `IdleRun.FoeSpecies`/`FoePalette` を直に読む。</summary>
    [Fact]
    public void 削除された抽選関数を呼ばない()
    {
        string src = IdleCs();
        Assert.DoesNotContain("Idle.FoeAt(", src);
        Assert.DoesNotContain("Idle.PaletteAt(", src);
        Assert.Contains("FoeSpecies", src);
        Assert.Contains("FoePalette", src);
    }

    /// <summary>🔴 `Core.Idle.Advance` の戻り値は、どちらの呼び出し口でも捨てない。
    /// ⚠️ 捨てると、乱数（`RngIdle`）を渡して見た目が動いていても、
    /// `Games.GainIdleEggs` を誰も呼ばないので**増えた卵が棚に一切入らない**
    /// （前任者からの申し送りで発覚した2件のうちの片方）。</summary>
    [Fact]
    public void AppPageは放置の戻り値を捨てずに卵を棚へ入れる()
    {
        string src = Code(AppPage());
        int advanceCalls = Regex.Matches(src, @"Core\.Idle\.Advance\(").Count;
        Assert.True(advanceCalls >= 2,
            $"Core.Idle.Advance の呼び出しが {advanceCalls} 箇所 ── OnAfterRenderAsync と BeatIdle の両方にあるはず");

        int gainCalls = Regex.Matches(src, @"Games\.GainIdleEggs\(").Count;
        Assert.True(gainCalls >= advanceCalls,
            $"Games.GainIdleEggs の呼び出しが {gainCalls} 箇所 ── Advance と同じ回数だけ要る（清算のたびに棚へ入れる）");

        // ⚠️ 「戻り値を捨てて呼ぶ」旧い壊れた形が、直った後にまた紛れ込んでいないか直に確かめる
        Assert.DoesNotContain(
            "Core.Idle.Advance(_shell.Game.Idle, Games.PartyOf(_shell.Game, PartyKind.Idle), _shell.Now);",
            src);
    }

    /// <summary>指定したメソッドの本体（`{` から対応する `}` まで、入れ子ごと数えて）だけを取り出す。</summary>
    private static string MethodBody(string src, string signature)
    {
        int start = src.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, signature + " が見つからない（検査が空回りしている）");
        int brace = src.IndexOf('{', start);
        int depth = 0, i = brace;
        for (; i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            else if (src[i] == '}') { depth--; if (depth == 0) break; }
        }
        return src.Substring(brace, i - brace + 1);
    }

    /// <summary>⚠️ 放置は押さずに卵が増える遊び。⭐ `BeatIdle`（1秒ごと）が増やした分も
    /// `Keep()` の経路に乗せないと、**溜めた卵が閉じた瞬間に消える**
    /// （`Beat`＝戦闘・すごろく用タイマーが「オートで勝った育ちが消えていた」で
    /// 踏んだのと同じ罠。`Vault.Keep` は中身が変わらなければ書かないので、
    /// 毎秒呼んでも無駄打ちにはならない）。</summary>
    [Fact]
    public void BeatIdleは増えた分を保存する()
    {
        string body = Code(MethodBody(AppPage(), "private async Task BeatIdle()"));
        Assert.Contains("Keep()", body);
    }

    /// <summary>⚠️ `Core.Idle.Advance` は `rng` を渡さないと、見た目の引き直しと卵の抽選を
    /// 行わない（`Core.Idle` の doc 参照）。⭐ 遊びの中からは必ず `RngIdle` を渡す。</summary>
    [Fact]
    public void 放置の進行に乱数を渡す()
    {
        string src = Code(AppPage());
        int rngUses = Regex.Matches(src, @"RngIdle\b").Count;
        Assert.True(rngUses >= 2,
            $"_shell.Game.RngIdle への言及が {rngUses} 箇所 ── Advance の2つの呼び出し口それぞれで渡すはず");
    }

    /// <summary>🔴 5拍に1回の全面組み直しはやめた（背景のアニメが巻き戻るため）。
    /// ⚠️ 戻すと、`#idle` の中身が作り直されて `.roll-sky`/`.roll-hill` の
    /// アニメーションが最初からやり直しになる（背景が「倒すたびに巻き戻る」ように見えた
    /// 不具合の原因そのもの）。
    ///
    /// ⚠️ 🔴 **2026-08-28（仕事2）に `_idleBeats` という名前の札を戻した**が、役目は別物
    /// （字と保存を1秒に1回へ**間引く**ためだけの数え ── 画面ぜんたいの組み直しではない）。
    ///
    /// 🔴 **2026-08-29（仕事1）で唯一の例外ができた** ── 期限切れの巣を埋め直したとき
    /// だけ、探索の画面（`Sheet.Nests`）を見ていれば組み直す（<see cref="Encounters"/> の
    /// доも参照）。⚠️ だから「`Draw()` が無いこと」はもう守りたい不変条件ではない ──
    /// **その1箇所だけ**に限られていること・毎拍かならず走る経路（打撃演出・帯の差し替え・
    /// 字/保存の間引き）には紛れ込んでいないことを直に数える。</summary>
    [Fact]
    public void 放置は画面を組み直さず毎秒差し替える()
    {
        string src = Code(AppPage());
        Assert.Contains("eggTap.idle", src);
        Assert.Contains("Idle.Peek(", src);

        string beatIdle = Code(MethodBody(AppPage(), "private async Task BeatIdle()"));
        // ⭐ 唯一許された組み直しは、巣が入れ替わって・かつ探索の画面を見ているときだけ
        Assert.Matches(new Regex(@"Now_Sheet == Sheet\.Nests\)\s*\{\s*Draw\(\);\s*StateHasChanged\(\);"), beatIdle);
        Assert.Equal(1, Regex.Matches(beatIdle, @"Draw\(\)").Count);
        Assert.Equal(1, Regex.Matches(beatIdle, @"StateHasChanged\(\)").Count);
    }

    /// <summary>🔴 仕事1（2026-08-29・作者の報告「巣がタイムオーバーになっても自動で
    /// 消えない」）: `BeatIdle` から `Encounters.Expire`→`Encounters.Refill` を呼ぶ。
    /// ⚠️ 呼んでいたのは検査（<c>HatcheryTests</c>）だけで、遊ぶ道からは
    /// `Games.NewGame` の開幕1回しか `Refill` が呼ばれておらず、残り0秒の巣が
    /// 並んだまま消えなかった。</summary>
    [Fact]
    public void BeatIdleは期限切れの巣を消して埋め直す()
    {
        string body = Code(MethodBody(AppPage(), "private async Task BeatIdle()"));
        Assert.Contains("Encounters.Expire(", body);
        Assert.Contains("Encounters.Refill(", body);

        // ⭐ 消えた件数が 0 のときは Refill も画面の組み直しも呼ばない（空振りで
        //    毎秒 Refill しない ── 意図をコードの形でも表す）。
        Assert.Matches(
            new Regex(@"Encounters\.Expire\([^;]+;\s*if\s*\(\w+ > 0\)\s*\{\s*Encounters\.Refill\("),
            body);
    }

    /// <summary>⚠️ `tap.js` は camelCase で読む（`FoeArt` ではなく `foeArt`）
    /// ── このリポジトリで何度も踏んでいる罠（`fx.js` の冒頭註と同じ）。</summary>
    [Fact]
    public void tapJsは小文字のcamelCaseで読む()
    {
        string js = TapJs();
        Assert.Contains("idle(view)", js);
        Assert.Contains("view.foeArt", js);
        Assert.Contains("view.foeKey", js);
        Assert.Contains("view.eggs", js);
        Assert.DoesNotContain("view.FoeArt", js);
        Assert.DoesNotContain("view.FoeKey", js);
    }

    /// <summary>指定した選び手（`.idle-come {` や `@keyframes idle-enter`）の
    /// 波括弧の中身だけを、入れ子ごと数えて取り出す。</summary>
    private static string Block(string css, string marker)
    {
        int start = css.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, marker + " が見つからない（検査が空回りしている）");
        int brace = css.IndexOf('{', start);
        int depth = 0, i = brace;
        for (; i < css.Length; i++)
        {
            if (css[i] == '{') depth++;
            else if (css[i] == '}') { depth--; if (depth == 0) break; }
        }
        return css.Substring(brace, i - brace + 1);
    }

    /// <summary>🔴 飛び込みの演出（相手・卵とも共通の `.idle-come`）に回転が入っていない
    /// （2026-08-28・作者の指示「回転してくる演出から、放射線状に飛び込んでくる演出に」）。</summary>
    [Fact]
    public void 飛び込みの演出に回転が無い()
    {
        string css = StageCss();
        Assert.DoesNotContain("rotate", Block(css, ".idle-come {"));
        Assert.DoesNotContain("rotate", Block(css, "@keyframes idle-enter"));
    }

    /// <summary>⚠️ 動きを望まない人には出さない一覧（`prefers-reduced-motion`）に
    /// `.idle-come` が残っていること ── 相手も卵もこの級を使うので、ここが抜けると
    /// 卵の飛び込みだけ止められなくなる。</summary>
    [Fact]
    public void 縮小モーションの一覧に飛び込みが載っている()
    {
        string css = StageCss();
        var m = Regex.Match(css, @"prefers-reduced-motion[\s\S]*", RegexOptions.Singleline);
        Assert.True(m.Success);
        Assert.Contains(".idle-come", m.Value);
    }

    // ── 2026-08-28・拍の作り直しに合わせた作り直し（仕事1〜5）の見張り ──────────

    /// <summary>🔴 仕事1: `Idle.cs`（Web）はもう `EnemyHp`/`FoeFresh` を読まない
    /// ── `Core.Idle` の拍の作り直しで、`Advance` が二度と書かない「亡骸」になった
    /// （`IdleRun.EnemyHp` の doc 参照）。⭐ 代わりに拍（`IdlePhase`）を直に読む。</summary>
    [Fact]
    public void Web版Idleはもう亡骸のEnemyHpを読まない()
    {
        // ⚠️ 註（doc）はこの変更の経緯そのものを語るので `EnemyHp`/`FoeFresh` に触れて
        //    当然 ── 見るのは**実際に走る字**だけ（`Code()` で註を落とす）。
        string src = Code(IdleCs());
        Assert.DoesNotContain("EnemyHp", src);
        Assert.DoesNotContain("FoeFresh", src);
        Assert.Contains("IdlePhase.Rest", src);
        Assert.Contains("IdlePhase.Come", src);
    }

    /// <summary>🔴 仕事2: 拍を見に行く間隔は 250ms（1秒に4回）。⚠️ `IdlePhase.Face`（0.5秒）・
    /// `Finish`（0.4秒）は 1000ms より短いので、1秒に1回しか覗かないと拍をまたいで
    /// 見落とす。</summary>
    [Fact]
    public void IdleEveryは250ミリ秒()
    {
        Assert.Contains("private const int IdleEvery = 250;", Code(AppPage()));
    }

    /// <summary>🔴 仕事2: 見に行く回数を4倍にしても、重い処理（字の差し替え・保存）まで
    /// 4倍にはしない ── どちらも間引きの条件式（`if (...)`）の中にあること。
    /// ⚠️ 帯の差し替え（`eggTap.idle`）と `Idle.Advance` 自体は間引かない（軽いので毎拍）
    /// ── ここでは重い2つだけを直に確かめる。</summary>
    [Fact]
    public void BeatIdleは字と保存を間引く()
    {
        string body = Code(MethodBody(AppPage(), "private async Task BeatIdle()"));

        Assert.Matches(new Regex(@"if\s*\([^)]*\)\s*await Keep\(\);"), body);
        Assert.Matches(new Regex(@"if\s*\([^)]*\)\s*await Js\.InvokeVoidAsync\(""eggTap\.words"""), body);
    }

    /// <summary>🔴 2026-08-28（本物の手番制への作り直し）: 打撃の演出は `IdleGain.Blows` から
    /// 作る ── 拍（`Phase`）のその瞬間の値からは作らない（1回の呼び出しの「間に」起きたことなので、
    /// 覗いた瞬間にはもう消えている）。⚠️ 旧 `Strikes`/`FirstStriker`（「誰から何回、順繰りに
    /// 殴ったか」しか言えない数え方）を組み立て直していないことも直に確かめる ── 手番はゲージの
    /// 競り合いで決まるので、誰が動いたかは `Blows[].Who`/`Target` にしか残っていない。</summary>
    [Fact]
    public void 打撃の演出はBlowsから作る()
    {
        string body = Code(MethodBody(AppPage(), "private async Task StrikeFx(Core.Idle.IdleGain gain)"));
        Assert.Contains("gain.Blows", body);
        Assert.Contains("eggFx.play", body);
        // ⚠️ 削除された欄を読んでいないか・`Phase` の瞬間値から組み立てていないか直に確かめる
        Assert.DoesNotContain("gain.Strikes", body);
        Assert.DoesNotContain("gain.FirstStriker", body);
        Assert.DoesNotContain(".Phase", body);
    }

    /// <summary>🔴 仕事2: 画面に飛ぶ数字は `blow.Damage`（実際に与えたダメージ）から作る ──
    /// 「力（Atk+Spd）」という代役の数はもう出さない（前任者からの申し送り）。</summary>
    [Fact]
    public void 打撃の数字はblowのダメージから作る()
    {
        string body = Code(MethodBody(AppPage(), "private async Task StrikeFx(Core.Idle.IdleGain gain)"));
        Assert.Contains("blow.Damage", body);
        Assert.DoesNotContain("stats.Atk", body);
    }

    /// <summary>🔴 仕事4: 倒れた味方を描く。⚠️ 直前まで `Web/Idle.cs` は
    /// `Core.Idle.IsDown` を一度も呼んでおらず、倒れても画面に一切出ていなかった
    /// （稼ぎが落ちる理由が見えない、という前任者からの申し送り）。</summary>
    [Fact]
    public void WebのIdleは倒れをIsDownで判定する()
    {
        Assert.Contains("Idle.IsDown(", Code(IdleCs()));
    }

    /// <summary>🔴 仕事4: `.idle-down` は `.idle-walk` の揺れ（`idle-bob`）を必ず止める ──
    /// 倒れているのに歩幅で上下していると壊れて見える。</summary>
    [Fact]
    public void 倒れた味方は歩く揺れを止める()
    {
        string block = Block(StageCss(), ".idle-walk.idle-down {");
        Assert.Contains("animation: none", block);
    }

    /// <summary>🔴 仕事4: 放置の帯（`.idle-drain`）は0.3秒で縮む。⚠️ 打撃は0.5秒ごとに来るので、
    /// 旧 .95s のままだと次の段が来る前に前の段の途中にしか着かない。</summary>
    [Fact]
    public void 放置の帯は0_3秒で縮む()
    {
        Assert.Contains(".idle-drain { transition: width .3s linear; }", StageCss());
    }

    /// <summary>🔴 仕事5: 卵の種族は `Core.Idle.Advance` を呼ぶ「前」に控えたものを
    /// `Games.GainIdleEggs` へ渡す。⚠️ `Advance` は周期が終わると同じ呼び出しの中で
    /// 次の相手へ引き直すので、呼んだ「後」の `FoeSpecies` を渡すと**次に出る相手**の
    /// 種族で卵が生まれてしまう（前任者からの申し送り）。</summary>
    [Fact]
    public void 卵の種族はAdvanceを呼ぶ前に控える()
    {
        string src = Code(AppPage());

        // 🔴 旧い壊れた形（Advance の「後」に FoeSpecies を読む）が戻っていないか直に確かめる
        Assert.DoesNotContain("Games.GainIdleEggs(_shell.Game, caughtUp, _shell.Game.Idle.FoeSpecies)", src);
        Assert.DoesNotContain("Games.GainIdleEggs(_shell.Game, gain, _shell.Game.Idle.FoeSpecies)", src);

        int advanceCalls = Regex.Matches(src, @"Core\.Idle\.Advance\(").Count;
        int capturedBeforeAdvance = Regex.Matches(src,
            @"int idleFoeSpecies = _shell\.Game\.Idle\.FoeSpecies;\s+var \w+ = Core\.Idle\.Advance\(").Count;
        Assert.True(advanceCalls >= 2, $"Core.Idle.Advance の呼び出しが {advanceCalls} 箇所");
        Assert.Equal(advanceCalls, capturedBeforeAdvance);

        Assert.Contains("Games.GainIdleEggs(_shell.Game, caughtUp, idleFoeSpecies)", src);
        Assert.Contains("Games.GainIdleEggs(_shell.Game, gain, idleFoeSpecies)", src);
    }
}
