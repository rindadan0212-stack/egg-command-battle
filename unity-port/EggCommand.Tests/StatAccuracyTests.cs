using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>弱化命中・弱化耐性（素質の5本目・6本目）が**実際に効く**か。
///
/// ⚠️ **この2本は挙動として1件も検査されていなかった。**
/// 既存のテストはどれも 4引数の <c>new StatBlock(hp, atk, def, spd)</c> で個体を作るので、
/// acc/res は既定の 0 のまま ── つまり <c>gap = (acc - res) / 2</c> は常に 0 で、
/// 中核の式が「動いていない状態」だけを通していた。
///
/// ⭐ ここは 6引数で作り、差が通る率に乗ることを直に測る。
/// </summary>
public class StatAccuracyTests
{
    /// <param name="acc">弱化命中。<param name="res">弱化耐性。</param></param>
    private static Creature Make(string id, int acc, int res) =>
        new Creature(id, "tamaru", new StatBlock(20, 20, 20, 20, acc, res),
            new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1);

    /// <summary>属性を揃えて、命中と抵抗の差だけを残した盤。</summary>
    private static (BattleState State, Unit Me, Unit Foe) Pair(
        int myAcc, int myRes, int foeAcc, int foeRes)
    {
        var a = Make("a", myAcc, myRes);
        var b = Make("b", foeAcc, foeRes);
        var s = Battle.CreateBattle(new List<Creature> { a }, new List<Creature> { b });
        return (s, s.Units.Find(u => u.Side == Side.Ally)!, s.Units.Find(u => u.Side == Side.Enemy)!);
    }

    /// <summary>実値の差の半分。⚠️ **素質ではなく実値**で測る
    /// ── 種族の基礎に弱化命中・弱化耐性が入ったので、素質 0 でも差がある。</summary>
    public static int GapOf(Unit me, Unit foe) =>
        (Creatures.StatsOf(me.Creature).Acc - Creatures.StatsOf(foe.Creature).Res)
        / Battle.LandStatDivisor;

    /// <summary>⭐ **差の半分が%ポイントとして乗る。**⚠️ 速度は関係しない（2026-08-18 に外した）。
    ///
    /// ⚠️ 期待値を直書きしない。⭐ 種族の基礎に弱化命中・弱化耐性を配った日（2026-08-19）に
    /// 「素質 0 なら差 0」が成り立たなくなり、直書きの 50/65/35/56 が全部落ちた。
    /// 検査したい性質は**差の半分が乗ること**なので、差から出せば基礎値を動かしても壊れない。</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(30, 0)]
    [InlineData(0, 30)]
    [InlineData(20, 8)]
    public void 命中と抵抗の差が通る率に乗る(int acc, int foeRes)
    {
        var (_, me, foe) = Pair(acc, 0, 0, foeRes);
        var weak = Effect.Buff(StatKey.Atk, -1, 3, chance: 50);
        Assert.Equal(50 + GapOf(me, foe), Battle.LandChanceOf(weak, me, foe));
    }

    /// <summary>⭐ 素質を積んだぶんは、そのまま差に乗る（動かないと育てる意味が無い）。</summary>
    [Fact]
    public void 命中を積むほど通りやすくなる()
    {
        var (_, low, foeLow) = Pair(0, 0, 0, 0);
        var (_, high, foeHigh) = Pair(30, 0, 0, 0);
        var weak = Effect.Buff(StatKey.Atk, -1, 3, chance: 50);
        Assert.True(Battle.LandChanceOf(weak, high, foeHigh)
            > Battle.LandChanceOf(weak, low, foeLow));
    }

    /// <summary>⚠️ **攻撃力・防御力の強化弱化に引きずられない。**
    ///
    /// ⭐ 引きずられていた頃は、相手に「防御力DOWN」を当てると弱化耐性まで 30% 下がり、
    /// **弱化で弱化の通る率を操れた**。それでは「先に弱化を通したほうが勝つ」の
    /// 一手勝負に戻るので、ここは育てて決める軸のまま置く。</summary>
    [Fact]
    public void 攻撃力と防御力の上下は通る率を動かさない()
    {
        var (state, me, foe) = Pair(30, 0, 0, 30);
        var weak = Effect.Buff(StatKey.Atk, -1, 3, chance: 50);
        int before = Battle.LandChanceOf(weak, me, foe);

        // 掛ける側の攻撃力を上げ、受ける側の防御力を下げても、通る率は動かない
        Battle.ApplyOne(state, me, me, Effect.Buff(StatKey.Atk, 1, 5));
        Battle.ApplyOne(state, foe, foe, Effect.Buff(StatKey.Def, -1, 5));
        Assert.True(me.Status.Atk.Turns > 0, "攻撃力UP が乗っていない（前提が崩れている）");
        Assert.True(foe.Status.Def.Turns > 0, "防御力DOWN が乗っていない（前提が崩れている）");

        Assert.Equal(before, Battle.LandChanceOf(weak, me, foe));
    }

    /// <summary>⚠️ 差がどれだけ開いても床 25 / 天井 95 を越えない。</summary>
    [Fact]
    public void 差が極端でも床と天井を越えない()
    {
        var (_, me, foe) = Pair(Stats.WildStatMax, 0, 0, Stats.WildStatMax);
        var sure = Effect.Buff(StatKey.Atk, -1, 3, chance: 95);
        var faint = Effect.Buff(StatKey.Atk, -1, 3, chance: 30);

        Assert.True(Battle.LandChanceOf(sure, me, foe) <= Battle.LandCeil);
        Assert.True(Battle.LandChanceOf(faint, foe, me) >= Battle.LandFloor);
    }

    /// <summary>⭐ **6本すべてが合計上限で削られる。**
    ///
    /// ⚠️ ゴールデン由来の個体は4本ぶんしか持たないので、この経路は一度も通っていなかった。
    /// 本番では acc/res が非ゼロで合計ちょうどの個体が普通に出る。</summary>
    [Fact]
    public void 合計上限は六本すべてから削る()
    {
        int max = Stats.WildStatMax;
        var over = new StatBlock(max, max, max, max, max, max);   // 合計 240
        var capped = Stats.CapTo(over, max, Stats.WildTotalMax);  // 上限 120

        Assert.Equal(Stats.WildTotalMax, Stats.TotalOf(capped));
        foreach (var key in Stats.Keys)
        {
            Assert.True(capped[key] > 0, $"{Stats.LabelOf(key)} が 0 まで削られた");
            Assert.True(capped[key] <= max, $"{Stats.LabelOf(key)} が1本の上限を越えた");
        }
    }

    /// <summary>⚠️ 1ステの上限 × 3 が合計の上限。⭐ この比が「何本伸ばせるか」を決めている。</summary>
    [Fact]
    public void 合計上限は一本の上限の三倍()
    {
        Assert.Equal(Stats.WildStatMax * 3, Stats.WildTotalMax);
        Assert.Equal(6, Stats.Keys.Length);
    }
}
