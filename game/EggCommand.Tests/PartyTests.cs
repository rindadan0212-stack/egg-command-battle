using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>編成が「放置1本＋巣3本」に分かれていること。
///
/// ⚠️ **保存の形が変わった変更**なので、往復と引き継ぎを必ず測る。
/// ⭐ 特に「古い保存（編成1本）を読んだとき、どちらにも入ること」──
/// 片方だけにすると、続きから始めた人の編成が黙って半分消える。
/// </summary>
public class PartyTests
{
    private const int T0 = 1_700_000_000;

    private static Game Fresh()
    {
        var game = Games.NewGame(7, T0);
        // 個体を増やしておく（編成を触るには手持ちが要る）
        for (int i = 0; i < 6; i++)
        {
            var nest = game.Encounters[0].Nest;
            var egg = Nests.MakeEgg(game.RngEgg, nest, EggOrigin.Defeated, 100 + i);
            game.Storage = Storages.Accept(game.Storage,
                Nests.Hatch(game.RngHatch, egg, $"p{i}"));
        }
        return game;
    }

    /// <summary>⭐ **放置と巣は別の3体。**片方を変えても、もう片方は動かない。</summary>
    [Fact]
    public void 放置と巣の編成は独立している()
    {
        var game = Fresh();
        var idle = Games.RosterOf(game, PartyKind.Idle);
        var nest = Games.RosterOf(game, PartyKind.Nest);
        idle.Clear(); nest.Clear();

        Games.TogglePartyMember(game, "p0", PartyKind.Idle);
        Games.TogglePartyMember(game, "p1", PartyKind.Nest);

        Assert.Contains("p0", Games.RosterOf(game, PartyKind.Idle));
        Assert.DoesNotContain("p1", Games.RosterOf(game, PartyKind.Idle));
        Assert.Contains("p1", Games.RosterOf(game, PartyKind.Nest));
        Assert.DoesNotContain("p0", Games.RosterOf(game, PartyKind.Nest));
    }

    /// <summary>⭐ **巣の編成は3つ登録できる。**番号を変えると別の3体になる。</summary>
    [Fact]
    public void 巣の編成は三つ登録できる()
    {
        var game = Fresh();
        Assert.Equal(3, Games.NestPartySlots);
        for (int i = 0; i < Games.NestPartySlots; i++)
        {
            game.NestParty = i;
            Games.RosterOf(game, PartyKind.Nest).Clear();
            Games.TogglePartyMember(game, $"p{i}", PartyKind.Nest);
        }
        for (int i = 0; i < Games.NestPartySlots; i++)
        {
            game.NestParty = i;
            Assert.Contains($"p{i}", Games.RosterOf(game, PartyKind.Nest));
            Assert.DoesNotContain($"p{(i + 1) % 3}", Games.RosterOf(game, PartyKind.Nest));
        }
    }

    /// <summary>⚠️ 番号が範囲外でも落ちない（古い保存・壊れた値のため）。</summary>
    [Fact]
    public void 番号が範囲外でも落ちない()
    {
        var game = Fresh();
        game.NestParty = 99;
        Assert.Equal(0, Games.Slot(game));
        Assert.NotNull(Games.RosterOf(game, PartyKind.Nest));
        game.NestParty = -3;
        Assert.Equal(0, Games.Slot(game));
        Assert.NotNull(Games.RosterOf(game, PartyKind.Nest));
    }

    /// <summary>⭐ 4本ぜんぶが保存され、読み戻せる。</summary>
    [Fact]
    public void 四本の編成が往復する()
    {
        var game = Fresh();
        Games.RosterOf(game, PartyKind.Idle).Clear();
        Games.TogglePartyMember(game, "p0", PartyKind.Idle);
        Games.TogglePartyMember(game, "p1", PartyKind.Idle);
        for (int i = 0; i < 3; i++)
        {
            game.NestParty = i;
            Games.RosterOf(game, PartyKind.Nest).Clear();
            Games.TogglePartyMember(game, $"p{i + 2}", PartyKind.Nest);
        }
        game.NestParty = 2;

        var back = Snapshots.Load(Snapshots.Save(game));
        Assert.NotNull(back);
        Assert.Equal(2, back!.NestParty);
        Assert.Equal(game.Party, back.Party);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(game.NestParties[i], back.NestParties[i]);
        }
    }

    /// <summary>⚠️ **古い保存（編成1本）は、放置と巣1の両方へ引き継ぐ。**
    /// ⭐ 片方だけにすると、続きから始めた人の編成が半分消える。</summary>
    [Fact]
    public void 古い保存の一本は放置と巣の両方へ入る()
    {
        var game = Fresh();
        Games.RosterOf(game, PartyKind.Idle).Clear();
        Games.TogglePartyMember(game, "p0", PartyKind.Idle);
        Games.TogglePartyMember(game, "p1", PartyKind.Idle);

        var save = Snapshots.Save(game);
        // ⭐ 巣の編成を持っていなかった頃の保存を作る
        save.NestParties.Clear();
        save.NestPartyCounts.Clear();
        save.NestParty = 0;

        var back = Snapshots.Load(save);
        Assert.NotNull(back);
        Assert.Equal(save.Party, back!.Party);
        Assert.Equal(save.Party, back.NestParties[0]);
        Assert.Empty(back.NestParties[1]);
        Assert.Empty(back.NestParties[2]);
    }

    /// <summary>⚠️ 個体が消えたら、**すべての**編成から外れる。
    /// ⭐ 残ると、その枠が永久に空のままになる。</summary>
    [Fact]
    public void 消えた個体は全部の編成から外れる()
    {
        var game = Fresh();
        Games.RosterOf(game, PartyKind.Idle).Clear();
        Games.TogglePartyMember(game, "p0", PartyKind.Idle);
        for (int i = 0; i < 3; i++)
        {
            game.NestParty = i;
            Games.RosterOf(game, PartyKind.Nest).Clear();
            Games.TogglePartyMember(game, "p0", PartyKind.Nest);
        }

        Games.ReleaseCreature(game, "p0");

        Assert.DoesNotContain("p0", game.Party);
        foreach (var roster in game.NestParties) Assert.DoesNotContain("p0", roster);
    }
}
