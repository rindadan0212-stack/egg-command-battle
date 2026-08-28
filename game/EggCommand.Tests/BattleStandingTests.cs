using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>🔴 **「誰が立っているか」を聞くだけで、戦いが進んではいけない。**
///
/// ⚠️ <see cref="Battle.NextActor"/> は名前に反して**進める**関数（毒が入り、
/// 強化の残りが減り、スタンなら手番を捨てる）。⭐ ところが画面を描く側
/// （`Sheets.Fight`）と押した側（`Deeds.Strike`）が「いま誰の番か」を知りたくて
/// それを呼んでいたので、**1手のあいだに毒が3〜4回入り、3ターンの強化が1手で切れて**いた
/// （2026-08-28 に発見。作者の報告「敵の技が表示される不具合」を追っていて見つかった）。
///
/// ⭐ 直し方は2枚重ね:
/// <list type="number">
/// <item>聞くだけの入口を用意する（<see cref="Battle.Standing"/>／<see cref="Battle.StandingAlly"/>）</item>
/// <item>それでも誰かが呼び過ぎたときのために、**進める側で1手番1回に釘づける**
///   （<see cref="Unit.TickedAt"/>）── 呼び手の作法だけで守ると、また誰かが呼ぶ</item>
/// </list></summary>
public class BattleStandingTests
{
    private static BattleState Fresh(int seed)
    {
        var game = Games.NewGame(seed);
        var enemies = Nests.MakeDefenders(game.RngNest, Nests.ById("thicket-fang"));
        return Battle.CreateBattle(Games.PartyOf(game), enemies);
    }

    /// <summary>誰かが立つところまで進める。</summary>
    private static Unit Stand(BattleState state)
    {
        while (Battle.AdvanceGauges(state, 3) > 0) { }
        var actor = Battle.NextActor(state);
        Assert.NotNull(actor);
        return actor!;
    }

    /// <summary>⭐ 何度聞いても、HP も毒も1つも動かない。</summary>
    [Fact]
    public void 立っている者を聞くだけでは何も進まない()
    {
        var state = Fresh(2026_08_28);
        var actor = Stand(state);
        Battle.ApplyOne(state, actor, actor, Effect.Poison(3, 5));
        // ⚠️ **空回りしていないことを先に確かめる**（毒が入らなければ、以下は何も見ていない）
        Assert.Equal(5, actor.Status.Poison.Turns);

        int hp = actor.Hp, turns = actor.Status.Poison.Turns, log = state.Log.Count;
        for (int i = 0; i < 5; i++) Assert.Same(actor, Battle.Standing(state));
        Assert.Equal(hp, actor.Hp);
        Assert.Equal(turns, actor.Status.Poison.Turns);
        Assert.Equal(log, state.Log.Count);
    }

    /// <summary>🔴 **同じ手番では、何度呼ばれても1回しか進まない。**
    /// ⚠️ この検査は <see cref="Unit.TickedAt"/> の釘を外すと落ちる
    /// （毒が4回入り、HP が4段減る）。</summary>
    [Fact]
    public void 同じ手番で二度は進まない()
    {
        var state = Fresh(2026_08_28);
        var actor = Stand(state);
        Battle.ApplyOne(state, actor, actor, Effect.Poison(3, 5));
        Assert.Equal(5, actor.Status.Poison.Turns);

        int hp = actor.Hp;
        for (int i = 0; i < 4; i++) Battle.NextActor(state);
        Assert.Equal(hp, actor.Hp);
        Assert.Equal(5, actor.Status.Poison.Turns);
    }

    /// <summary>⚠️ **釘が「永久に進まない」になっていないことの証明。**
    /// ⭐ 手番が1つ進めば、次に立ったときはきちんと毒が入る。</summary>
    [Fact]
    public void 手番が変われば毒はまた入る()
    {
        var state = Fresh(2026_08_28);
        var actor = Stand(state);
        Battle.ApplyOne(state, actor, actor, Effect.Poison(3, 5));
        int hp = actor.Hp;

        Battle.PerformAction(state, actor, Ai.ChooseAction(state, actor));
        // ⭐ もう一度この体だけを立たせる（誰が動くかは、この検査の的ではない）
        foreach (var u in state.Units) u.Gauge = ReferenceEquals(u, actor) ? Battle.GaugeMax : 0;

        Assert.Same(actor, Battle.NextActor(state));
        Assert.Equal(4, actor.Status.Poison.Turns);
        Assert.True(actor.Hp < hp, "毒でHPが減っていない");
    }

    /// <summary>🔴 **手札の主はいつでも味方**（作者の報告「敵の技が表示される」）。
    /// ⚠️ 敵の手番を一度も通らないと何も見ていないので、そこも数える。</summary>
    [Fact]
    public void 手札の主はいつでも味方()
    {
        var state = Fresh(2026_08_28);
        bool sawFoe = false;
        for (int guard = 0; guard < 300 && state.Result == null; guard++)
        {
            var hand = Battle.StandingAlly(state);
            Assert.NotNull(hand);
            Assert.Equal(Side.Ally, hand!.Side);
            if (Battle.Standing(state)?.Side == Side.Enemy) sawFoe = true;

            var next = Battle.NextActor(state);
            if (next == null) break;
            Battle.PerformAction(state, next, Ai.ChooseAction(state, next));
        }
        Assert.True(sawFoe, "敵が立っている場面を一度も通っていない（検査が空回り）");
    }

    /// <summary>⭐ 立っているのが味方なら、手札の主は**その体そのもの**。
    /// ⚠️ 別の味方を指すと、押した技が思っていない体から出る。</summary>
    [Fact]
    public void 味方が立っているならその体が手札の主()
    {
        var state = Fresh(777);
        for (int guard = 0; guard < 300 && state.Result == null; guard++)
        {
            var now = Battle.Standing(state);
            if (now != null && now.Side == Side.Ally)
                Assert.Same(now, Battle.StandingAlly(state));
            var next = Battle.NextActor(state);
            if (next == null) break;
            Battle.PerformAction(state, next, Ai.ChooseAction(state, next));
        }
    }
}
