using System;
using System.IO;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>レベルアップの二度手間解消（作者の指示 2026-08-29「点を振る前に点を
/// 獲得するのが二度手間」）の検査。
///
/// ⭐ `Shell.cs` は `LayoutDom`/`Deeds`/`Filters` 等 Web 専用の依存が多く
/// コンパイルには持ち込めない（`TapCatalogTests`/`BattleWordTests` と同じ理由）ので、
/// 「押した拍にまとまる」の**効き目**（EXP→1点→そのステへが実際につながるか）は
/// Core だけで直に測り、「実際にその2つの口を呼んでいるか」は
/// `websrc\Shell.cs` をテキストとして読み直して確かめる（2段構え）。</summary>
public class GrowSpendTests
{
    private static readonly string WebSrc = Path.Combine(AppContext.BaseDirectory, "websrc");
    private static readonly string LayoutDir = Path.Combine(AppContext.BaseDirectory, "layouts");
    private static string ShellSource => File.ReadAllText(Path.Combine(WebSrc, "Shell.cs"));

    private static Creature Make(int earned = 0) =>
        new Creature("t", "tamaru", new StatBlock(20, 20, 20, 20), new StatBlock(0, 0, 0, 0),
            earned, 0, null, null, 0, null, null, 1);

    /// <summary>Core 側の効き目 ── 振れる点が無いところから、`Shell` の
    /// `case "spend"` と同じ手順（`Creatures.UnspentOf` が 0 なら先に
    /// `Core.Idle.Spend`、そのあと `Creatures.Spend`）を直に踏むと、
    /// 1回のやり取りで EXP が減り・Lv が上がり・そのステに1点入る。</summary>
    [Fact]
    public void EXPから1点を作ってそのままステへ振れる()
    {
        var one = Make();
        var run = new IdleRun { Exp = Levels.ExpToNext(one) };
        int levelBefore = Levels.Of(one);

        if (Creatures.UnspentOf(one) <= 0) Idle.Spend(run, one);
        int spent = Creatures.Spend(one, StatKey.Atk, 1);

        Assert.Equal(1, spent);
        Assert.Equal(0, Creatures.UnspentOf(one));
        Assert.Equal(1, one.Points[StatKey.Atk]);
        Assert.Equal(levelBefore + 1, Levels.Of(one));
        Assert.Equal(0, run.Exp);
    }

    /// <summary>⚠️ **EXP が足りないときは、点も増えずステも動かない。**
    /// 「黙って何も起きない」を避けるのは `Shell`/`Sheets.Grow` の `Tappable` の
    /// 役目（押させない）だが、Core の口自体も安全に 0 を返すことをここで裏取りする。</summary>
    [Fact]
    public void EXPが足りないときは振れない()
    {
        var one = Make();
        var run = new IdleRun { Exp = Levels.ExpToNext(one) - 1 };

        if (Creatures.UnspentOf(one) <= 0) Idle.Spend(run, one);
        int spent = Creatures.Spend(one, StatKey.Atk, 1);

        Assert.Equal(0, spent);
        Assert.Equal(0, one.Earned);
        Assert.Equal(0, Creatures.UnspentOf(one));
    }

    /// <summary>🔴 上限（`Levels.GrowMax`）は1ミリも変えていないことの裏取り
    /// ── 振れる点も使い切って上限に達した個体は、EXP がいくらあっても振れない。
    /// ⚠️ 「稼いだ分（`Earned`）＝上限」だけでは足りない ── 振っていない分は
    /// `UnspentOf` に残るので、そこも `Points` で使い切らせる。</summary>
    [Fact]
    public void 上限に達したら振れない()
    {
        var maxedPoints = new StatBlock(0, 0, 0, 0).With(StatKey.Atk, Levels.GrowMax);
        var one = new Creature("t", "tamaru", new StatBlock(20, 20, 20, 20), new StatBlock(0, 0, 0, 0),
            Levels.GrowMax, 0, null, null, 0, null, null, 1, points: maxedPoints);
        Assert.True(Levels.IsMaxed(one));
        Assert.Equal(0, Creatures.UnspentOf(one));

        var run = new IdleRun { Exp = 999_999 };
        if (Creatures.UnspentOf(one) <= 0) Idle.Spend(run, one);
        int spent = Creatures.Spend(one, StatKey.Def, 1);

        Assert.Equal(0, spent);
        Assert.Equal(999_999, run.Exp);
    }

    [Fact]
    public void grow_txtにlevelup釦がもう無くspend釦は残っている()
    {
        string text = File.ReadAllText(Path.Combine(LayoutDir, "grow.txt"));
        Assert.DoesNotContain("tap=levelup", text);
        Assert.Contains("tap=spend", text);
    }

    /// <summary>🔴 **EXP→点→ステの判断は `Deeds.SpendPoint` に1つだけ在る。**
    ///
    /// ⚠️ 2026-08-29 に、この中身が `Shell.Tap` の `case "spend"` へ**丸写し**されかけた
    /// （`Deeds.SpendPoint` は残ったまま呼ばれなくなり、同じ判断が2か所になっていた）。
    /// ⭐ このリポジトリで一番よく壊れる形なので、**押す側は呼ぶだけ**を検査で釘づける。</summary>
    [Fact]
    public void 点を振る判断はDeedsに1つだけ()
    {
        string deeds = File.ReadAllText(Path.Combine(WebSrc, "Deeds.cs"));
        int start = deeds.IndexOf("public static void SpendPoint(", StringComparison.Ordinal);
        Assert.True(start >= 0, "Deeds.cs: SpendPoint が見つからない（検査の前提が崩れた）");
        string body = deeds.Substring(start, Math.Min(1400, deeds.Length - start));
        Assert.Contains("Core.Idle.Spend(", body);      // ⭐ EXP → 1点
        Assert.Contains("Creatures.Spend(", body);      // ⭐ 1点 → ステ

        // ⚠️ 押す側は「呼ぶだけ」── 中身を持たないこと
        int at = ShellSource.IndexOf("case UiActionKind.Spend:", StringComparison.Ordinal);
        Assert.True(at >= 0, "Shell.cs: case UiActionKind.Spend が見つからない");
        string tap = ShellSource.Substring(at, Math.Min(200, ShellSource.Length - at));
        Assert.Contains("Deeds.SpendPoint(", tap);
        Assert.DoesNotContain("Creatures.Spend(", tap);
    }

    /// <summary>🔴 **死んだ口を残さない**（2026-08-29）。⚠️ 釦を無くしたのに
    /// `case "levelup"` と `Deeds.Grow` を残していたので消した ── 呼ばれない分岐が
    /// 在ると、次の人がそちらを直して「直したのに変わらない」を踏む。
    /// ⭐ `TapCatalog` の写しからも同時に外してある（`TapCatalogTests` が対で見張る）。</summary>
    [Fact]
    public void levelupの残骸がどこにも無い()
    {
        Assert.DoesNotContain("UiActionKind.Levelup", ShellSource);
        Assert.DoesNotContain("Deeds.Grow(", ShellSource);
    }
}
