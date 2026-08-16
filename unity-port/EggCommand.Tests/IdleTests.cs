using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>ホームの放置。⚠️ 移植元に無い規則なので、規則そのものを検査する。</summary>
public class IdleTests
{
    private const long T0 = 1_700_000_000;

    private static List<Creature> Party(int hp, int atk, int def, int spd, int n = 3)
    {
        var party = new List<Creature>();
        for (int i = 0; i < n; i++)
        {
            party.Add(new Creature($"c{i}", "tamaru", new StatBlock(hp, atk, def, spd),
                new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1));
        }
        return party;
    }

    private static IdleRun Started(long now = T0)
    {
        var run = new IdleRun();
        Idle.Advance(run, Party(20, 20, 20, 20), now);   // 1回目は時計を合わせるだけ
        return run;
    }

    [Fact]
    public void 初回は時計を合わせるだけで素材は入らない()
    {
        var run = new IdleRun();
        Assert.Equal(0, Idle.Advance(run, Party(20, 20, 20, 20), T0));
        Assert.Equal(T0, run.LastUnix);
    }

    [Fact]
    public void 時間が進むと素材が溜まる()
    {
        var run = Started();
        int gained = Idle.Advance(run, Party(20, 20, 20, 20), T0 + 60);
        Assert.True(gained > 0, "1分で素材が1つも入らない");
        Assert.Equal(gained, run.Materials);
        Assert.Equal(gained, run.Defeated);
    }

    [Fact]
    public void 十分でおよそ一体ぶんの素材が溜まる()
    {
        // ⭐ 「10分回せば最初の個体は MAX」が狙い。GrowMax ぶんの素材が要る
        // ⚠️ 手で作った編成ではなく**遊び始めの実物**で測る。
        //    強すぎる編成で較正して、実物が毎回倒れていたことがある
        var game = Games.NewGame(2026_08_16);
        var real = Games.PartyOf(game);
        var run = new IdleRun();
        Idle.Advance(run, real, T0);
        Idle.Advance(run, real, T0 + 600);
        Assert.Empty(run.DownUntil);
        int levels = run.Materials / Idle.MaterialPerLevel;
        Assert.True(levels >= Levels.GrowMax * 0.7 && levels <= Levels.GrowMax * 1.6,
            $"10分で {run.Materials} 素材 = {levels}Lv（狙いは {Levels.GrowMax}Lv 前後）");
    }

    [Fact]
    public void 強い編成のほうが速い()
    {
        var weak = Started();
        Idle.Advance(weak, Party(10, 8, 8, 8), T0 + 300);
        var strong = Started();
        Idle.Advance(strong, Party(30, 30, 30, 30), T0 + 300);
        Assert.True(strong.Materials > weak.Materials,
            $"弱 {weak.Materials} / 強 {strong.Materials}");
    }

    [Fact]
    public void 弱いと倒れる_強いと倒れない()
    {
        // ⚠️ 「弱い」は1体だけ残った状態のこと。3体そろっていれば遊び始めでも間に合う
        var lone = Party(2, 1, 1, 1, 1);
        var weak = new IdleRun();
        Idle.Advance(weak, lone, T0);
        Idle.Advance(weak, lone, T0 + 300);
        Assert.NotEmpty(weak.DownUntil);

        var strong = Started();
        Idle.Advance(strong, Party(30, 60, 30, 60), T0 + 300);
        Assert.Empty(strong.DownUntil);
    }

    [Fact]
    public void 倒れる者は防御が一番低い者()
    {
        var party = new List<Creature>
        {
            new Creature("tough", "tamaru", new StatBlock(10, 5, 30, 5),
                new StatBlock(0,0,0,0), 0, 0, null, null, 0, null, null, 1),
            new Creature("soft", "tamaru", new StatBlock(10, 5, 2, 5),
                new StatBlock(0,0,0,0), 0, 0, null, null, 0, null, null, 1),
        };
        var run = new IdleRun();
        Idle.Advance(run, party, T0);
        // ⚠️ 一撃ぶんだけ進める。長く回すと残った者も遅くなって順に倒れる（それは正しい）
        Idle.Advance(run, party, T0 + (long)Idle.ChargeSeconds + 1);
        Assert.Contains("soft", run.DownUntil.Keys);
        Assert.DoesNotContain("tough", run.DownUntil.Keys);
    }

    [Fact]
    public void 倒れた者は時間で起き上がる()
    {
        var party = Party(2, 1, 1, 1, 1);
        var run = new IdleRun();
        Idle.Advance(run, party, T0);
        Idle.Advance(run, party, T0 + 10);
        Assert.NotEmpty(run.DownUntil);

        long until = 0;
        foreach (var v in run.DownUntil.Values) until = v;
        Assert.True(Idle.IsDown(run, party[0], until - 1));
        foreach (var c in party) Assert.False(Idle.IsDown(run, c, until + Idle.ReviveSeconds));
    }

    [Fact]
    public void 何日でも一度に流し込まない()
    {
        var capped = Started();
        Idle.Advance(capped, Party(24, 22, 18, 20), T0 + Idle.CatchUpMax * 10);
        var exact = Started();
        Idle.Advance(exact, Party(24, 22, 18, 20), T0 + Idle.CatchUpMax);
        Assert.Equal(exact.Materials, capped.Materials);
    }

    [Fact]
    public void 巻き戻しても素材は増えない()
    {
        var run = Started();
        Assert.Equal(0, Idle.Advance(run, Party(20, 20, 20, 20), T0 - 100));
        Assert.Equal(0, run.Materials);
    }

    // ── 素材を使う ──────────────────────────────────

    [Fact]
    public void 素材は一度で上限まで入る()
    {
        var run = new IdleRun { Materials = Levels.GrowMax * Idle.MaterialPerLevel };
        var creature = Party(20, 20, 20, 20, 1)[0];
        Assert.Equal(Levels.GrowMax, Idle.Spend(run, creature));
        Assert.Equal(Levels.GrowMax, creature.Earned);
        Assert.Equal(0, run.Materials);
    }

    [Fact]
    public void 足りないぶんだけ入る()
    {
        var run = new IdleRun { Materials = Idle.MaterialPerLevel * 3 + 7 };
        var creature = Party(20, 20, 20, 20, 1)[0];
        Assert.Equal(3, Idle.Spend(run, creature));
        Assert.Equal(7, run.Materials);   // ⚠️ 端数は捨てない
    }

    [Fact]
    public void 上限に達していたら素材を使わない()
    {
        var run = new IdleRun { Materials = 999 };
        var creature = Party(20, 20, 20, 20, 1)[0];
        Creatures.Grow(creature, Levels.GrowMax);
        Assert.Equal(0, Idle.Spend(run, creature));
        Assert.Equal(999, run.Materials);
    }

    [Fact]
    public void 同じ編成と同じ経過なら必ず同じ結果()
    {
        // ⭐ 乱数を使っていないことの検査。放置は「見ていない間」に進むので、
        //    結果が揺れると何が起きたのか説明できなくなる
        var a = Started();
        Idle.Advance(a, Party(24, 22, 18, 20), T0 + 500);
        var b = Started();
        Idle.Advance(b, Party(24, 22, 18, 20), T0 + 500);
        Assert.Equal(a.Materials, b.Materials);
        Assert.Equal(a.Defeated, b.Defeated);
    }
}
