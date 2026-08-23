using System;
using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>状態異常を**絵で出す**ときの出口（<see cref="Battle.ActiveStatusBadges"/>）。
///
/// 🔴 ここが空だったせいで「挑発が緑（良い側）で出る」を持ち込んでいた（2026-08-23）。
/// ⭐ 色は**その札を持っている個体にとって**の得失。掛けた側の得失ではない。
/// ⚠️ だから「敵に付けた弱化」は、敵の列に**赤**で出るのが正しい。</summary>
public class StatusBadgeTests
{
    private static Creature Make(string id) =>
        new Creature(id, "tamaru", new StatBlock(30, 30, 30, 30),
            new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1);

    private static Unit One()
    {
        var s = Battle.CreateBattle(new List<Creature> { Make("a0") }, new List<Creature> { Make("e0") });
        return s.Units.Find(u => u.Key == "ally-0")!;
    }

    /// <summary>その種類を1つだけ乗せた札を引く。</summary>
    private static StatusBadge Only(Action<UnitStatus> put)
    {
        var u = One();
        put(u.Status);
        var badges = Battle.ActiveStatusBadges(u);
        Assert.Single(badges);
        return badges[0];
    }

    /// <summary>⭐ 弱化は全部「悪い側」。
    /// 🔴 挑発がここに居るのが要点 ── `効果の種類.md` は「相手に付ける**弱化**」と書いており、
    /// `UnitStatus.TauntBy` も「挑発を掛けてきた相手」を持つ（＝札は掛けられた側に乗る）。</summary>
    [Theory]
    [InlineData("atk")]
    [InlineData("def")]
    [InlineData("spd")]
    [InlineData("poison")]
    [InlineData("stun")]
    [InlineData("sleep")]
    [InlineData("taunt")]
    [InlineData("block")]
    public void 弱化は悪い側で出る(string which)
    {
        var badge = Only(st =>
        {
            switch (which)
            {
                case "atk": st.Atk = new Modifier { Percent = -30, Turns = 3 }; break;
                case "def": st.Def = new Modifier { Percent = -30, Turns = 3 }; break;
                case "spd": st.Spd = new Modifier { Percent = -30, Turns = 3 }; break;
                case "poison": st.Poison = new Stacking { Stacks = 2, Turns = 3 }; break;
                case "stun": st.Stun = 2; break;
                case "sleep": st.Sleep = 2; break;
                case "taunt": st.Taunt = 2; break;
                case "block": st.Block = 2; break;
            }
        });
        Assert.False(badge.Good, $"{which} は弱化なので悪い側（赤）で出るはず");
    }

    /// <summary>⭐ 強化は全部「良い側」。</summary>
    [Theory]
    [InlineData("atk")]
    [InlineData("def")]
    [InlineData("spd")]
    [InlineData("regen")]
    [InlineData("shield")]
    [InlineData("guts")]
    [InlineData("immune")]
    public void 強化は良い側で出る(string which)
    {
        var badge = Only(st =>
        {
            switch (which)
            {
                case "atk": st.Atk = new Modifier { Percent = 30, Turns = 3 }; break;
                case "def": st.Def = new Modifier { Percent = 30, Turns = 3 }; break;
                case "spd": st.Spd = new Modifier { Percent = 30, Turns = 3 }; break;
                case "regen": st.Regen = new Stacking { Stacks = 2, Turns = 3 }; break;
                case "shield": st.Shield = 2; break;
                case "guts": st.Guts = 2; break;
                case "immune": st.Immune = 2; break;
            }
        });
        Assert.True(badge.Good, $"{which} は強化なので良い側（緑）で出るはず");
    }

    /// <summary>⚠️ 種類が増えたのに絵を割り当て忘れる、を止める。
    /// ⭐ `Art` 側の表を舐めるのではなく **enum を舐める** ── 表に無い種類を見つけたい。</summary>
    [Fact]
    public void すべての種類に絵がある()
    {
        foreach (StatusKind kind in Enum.GetValues(typeof(StatusKind)))
            Assert.False(string.IsNullOrEmpty(Art.StatusIcon(kind)), $"{kind} の絵が無い");
    }

    /// <summary>⚠️ 絵は種類ごとに**別物**でなければ意味がない（使い回すと見分けが付かない）。</summary>
    [Fact]
    public void 絵は種類ごとに違う()
    {
        var seen = new Dictionary<string, StatusKind>();
        foreach (StatusKind kind in Enum.GetValues(typeof(StatusKind)))
        {
            string name = Art.StatusIcon(kind);
            Assert.False(seen.ContainsKey(name), $"{kind} と {seen.GetValueOrDefault(name)} が同じ絵 {name} を使っている");
            seen[name] = kind;
        }
    }

    /// <summary>⭐ 絵の並びと字の並びは**同じ順**（`ActiveStatuses` と揃えてある）。
    /// ⚠️ Unity 側はまだ字を読むので、片方だけ並べ替えると2つの画面がずれる。</summary>
    [Fact]
    public void 絵の並びは字の並びと揃っている()
    {
        var u = One();
        u.Status.Atk = new Modifier { Percent = 30, Turns = 3 };
        u.Status.Poison = new Stacking { Stacks = 2, Turns = 3 };
        u.Status.Shield = 1;
        u.Status.Taunt = 1;
        u.Status.Block = 1;

        var badges = Battle.ActiveStatusBadges(u);
        var words = Battle.ActiveStatuses(u);
        Assert.Equal(words.Count, badges.Count);

        // ⭐ 「同じ順か」だけを見る ── 字の書式そのものは別の関心事
        var order = new List<StatusKind>
        {
            StatusKind.Atk, StatusKind.Poison, StatusKind.Shield, StatusKind.Taunt, StatusKind.Block,
        };
        Assert.Equal(order, badges.ConvertAll(b => b.Kind));
    }

    /// <summary>⚠️ 数が空だと絵の下に何も出ず「1回だけ」と見分けが付かない。</summary>
    [Fact]
    public void どの札にも数が添う()
    {
        var u = One();
        u.Status.Atk = new Modifier { Percent = -30, Turns = 3 };
        u.Status.Def = new Modifier { Percent = 30, Turns = -1 };
        u.Status.Regen = new Stacking { Stacks = 2, Turns = 3 };
        u.Status.Guts = 5;
        foreach (var b in Battle.ActiveStatusBadges(u))
            Assert.False(string.IsNullOrWhiteSpace(b.Text), $"{b.Kind} に数が無い");
    }
}
