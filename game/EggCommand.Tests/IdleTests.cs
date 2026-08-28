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
    public void 時間が進むとEXPが溜まる()
    {
        var run = Started();
        int gained = Idle.Advance(run, Party(20, 20, 20, 20), T0 + 60);
        Assert.True(gained > 0, "1分で EXP が1つも入らない");
        Assert.Equal(gained, run.Exp);
        // ⚠️ 1体倒すと EXP は ExpPerKill 入る（1体 ＝ 1EXP ではない）
        Assert.Equal(gained / Idle.ExpPerKill, run.Defeated);
    }

    [Fact]
    public void 十分でおよそ一体ぶんのEXPが溜まる()
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
        // ⚠️ 1レベルの値段は**その個体の Lv**で変わるので、割り算では出ない。
        //    ⭐ 実物の1体で「この EXP なら何段上がるか」を数える
        int levels = Levels.LevelsFor(real[0], run.Exp);
        // 🔴 **育成の上限が 20 → 50 になった**（2026-08-26）ので、「10分で MAX」は
        //    もう成り立たない（EXP が約2.5倍要る）。⚠️ ここは**放置の配り方の較正**を
        //    見る検査なので、基準は旧上限の 20 段のまま置く。
        //    🚧 「何分で振り切れるべきか」は未決 ── 決まったら放置の量ごと直す。
        const int Pace = 20;
        Assert.True(levels >= Pace * 0.7 && levels <= Pace * 1.6,
            $"10分で {run.Exp} EXP = {levels}Lv（狙いは {Levels.GrowMax}Lv 前後）");
    }

    [Fact]
    public void 強い編成のほうが速い()
    {
        var weak = Started();
        Idle.Advance(weak, Party(10, 8, 8, 8), T0 + 300);
        var strong = Started();
        Idle.Advance(strong, Party(30, 30, 30, 30), T0 + 300);
        Assert.True(strong.Exp > weak.Exp,
            $"弱 {weak.Exp} / 強 {strong.Exp}");
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
        Assert.Equal(exact.Exp, capped.Exp);
    }

    [Fact]
    public void 巻き戻してもEXPは増えない()
    {
        var run = Started();
        Assert.Equal(0, Idle.Advance(run, Party(20, 20, 20, 20), T0 - 100));
        Assert.Equal(0, run.Exp);
    }

    // ── EXP を使う ──────────────────────────────────

    [Fact]
    public void EXPは一度に一レベルだけ入る()
    {
        // ⭐ 一気に上限まで入れない。どこで上げ止めるかは持ち主が決める
        var creature = Party(20, 20, 20, 20, 1)[0];
        int cost = Levels.ExpToNext(creature);
        var run = new IdleRun { Exp = cost * 10 };
        Assert.Equal(1, Idle.Spend(run, creature));
        Assert.Equal(1, creature.Earned);
        Assert.Equal(cost * 10 - cost, run.Exp);
    }

    /// <summary>⭐ **値段はレベルが高いほど上がる。**（作者の指示 2026-08-19）</summary>
    [Fact]
    public void 高いレベルほど一レベルが高くつく()
    {
        var creature = Party(20, 20, 20, 20, 1)[0];
        var run = new IdleRun
        {
            Exp = Levels.ExpBetween(Levels.Of(creature), Levels.Of(creature) + Levels.GrowMax),
        };
        int first = run.Exp;
        Idle.Spend(run, creature);
        int firstCost = first - run.Exp;

        // ⚠️ 上限手前まで上げてから、もう1段の値段を測る
        while (creature.Earned < Levels.GrowMax - 1) Idle.Spend(run, creature);
        int before = run.Exp;
        Idle.Spend(run, creature);
        int lastCost = before - run.Exp;

        Assert.True(lastCost > firstCost, $"最初 {firstCost} / 最後 {lastCost}");
    }

    [Fact]
    public void EXPが一レベルぶんに満たなければ入らない()
    {
        var creature = Party(20, 20, 20, 20, 1)[0];
        var run = new IdleRun { Exp = Levels.ExpToNext(creature) - 1 };
        Assert.Equal(0, Idle.Spend(run, creature));
        Assert.Equal(Levels.ExpToNext(creature) - 1, run.Exp);   // ⚠️ 端数は捨てない
        Assert.Equal(0, creature.Earned);
    }

    [Fact]
    public void 上限に達していたらEXPを使わない()
    {
        var run = new IdleRun { Exp = 999 };
        var creature = Party(20, 20, 20, 20, 1)[0];
        Creatures.Grow(creature, Levels.GrowMax);
        Assert.Equal(0, Idle.Spend(run, creature));
        Assert.Equal(999, run.Exp);
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
        Assert.Equal(a.Exp, b.Exp);
        Assert.Equal(a.Defeated, b.Defeated);
    }
}
