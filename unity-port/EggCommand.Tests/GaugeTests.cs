using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>ゲージを刻んで進めても勝敗が変わらないこと。
///
/// ⚠️ これは「見せるためのコード」が勝敗を変えていないかの検査。
/// ここが崩れると、画面のためにゲームの結果が動くという最悪の形になる。</summary>
public class GaugeTests
{
    private static BattleState Fresh(int seed)
    {
        var game = Games.NewGame(seed);
        var enemies = Nests.MakeDefenders(game.RngNest, Nests.ById("thicket-fang"));
        return Battle.CreateBattle(Games.PartyOf(game), enemies);
    }

    /// <summary>刻んで進めても、動く順番も勝敗も1つも変わらない。</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2026_08_16)]
    [InlineData(777)]
    public void 刻んで進めても手番の順が変わらない(int seed)
    {
        var plain = Play(Fresh(seed), 0);
        // ⚠️ 列そのものが自明でないことを先に確かめる。⭐ Play が即 break する形に
        //    退化しても「両方とも短い列」で一致してしまう
        Assert.True(plain.Count > 5, $"手番の列が短すぎる（{plain.Count}）");
        foreach (int ticks in new[] { 1, 3, 7 })
        {
            var stepped = Play(Fresh(seed), ticks);
            Assert.Equal(plain, stepped);
        }
    }

    /// <summary>1手ずつ進めて「誰が動いたか」を並べる。
    /// <paramref name="ticks"/> が 0 なら刻まない（従来どおり）。</summary>
    private static List<string> Play(BattleState state, int ticks)
    {
        var order = new List<string>();
        for (int guard = 0; guard < 400; guard++)
        {
            if (ticks > 0)
            {
                // 満ちるまで刻む。⚠️ 0 が返ったら誰かが満ちている
                while (Battle.AdvanceGauges(state, ticks) > 0) { }
            }
            var actor = Battle.NextActor(state);
            if (actor == null) break;
            order.Add(actor.Key);
            Battle.PerformAction(state, actor, Ai.ChooseAction(state, actor));
        }
        order.Add("結果=" + state.Result);
        return order;
    }

    [Fact]
    public void 誰かが満ちていたら刻まない()
    {
        var state = Fresh(5);
        while (Battle.AdvanceGauges(state, 1) > 0) { }
        Assert.Equal(0, Battle.AdvanceGauges(state, 99));
    }

    [Fact]
    public void 決着後は刻まない()
    {
        var state = Fresh(9);
        for (int i = 0; i < 400 && state.Result == null; i++)
        {
            var actor = Battle.NextActor(state);
            if (actor == null) break;
            Battle.PerformAction(state, actor, Ai.ChooseAction(state, actor));
        }
        Assert.NotNull(state.Result);
        Assert.Equal(0, Battle.AdvanceGauges(state, 10));
    }
}
