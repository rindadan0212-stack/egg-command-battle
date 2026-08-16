using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>保存と復元。⚠️ ここが崩れると、遊んだ結果が黙って消える。</summary>
public class SnapshotTests
{
    private const long T0 = 1_700_000_000;

    /// <summary>ひととおり触った状態を作る。⭐ 空の状態だけ通しても検査にならない。</summary>
    private static Game Played()
    {
        var game = Games.NewGame(2026_08_16);
        var ids = new List<string>();
        foreach (var c in game.Storage.Creatures) ids.Add(c.Id);

        Games.GrowParty(Games.PartyOf(game), 3);
        Games.TogglePartyMember(game, ids[2]);
        Games.GainEgg(game, game.Encounters[0].Nest, EggOrigin.Stolen);
        Games.GainEgg(game, game.Encounters[1].Nest, EggOrigin.Defeated);
        Hatchery.Begin(game, game.Eggs[0].Id, T0);
        Core.Idle.Advance(game.Idle, Games.PartyOf(game), T0);
        Core.Idle.Advance(game.Idle, Games.PartyOf(game), T0 + 120);
        return game;
    }

    private static void Same(Game a, Game b)
    {
        Assert.Equal(a.Seed, b.Seed);
        Assert.Equal(a.Serial, b.Serial);
        Assert.Equal(a.EncounterSerial, b.EncounterSerial);
        Assert.Equal(a.Storage.Slots, b.Storage.Slots);
        Assert.Equal(a.Storage.Creatures.Count, b.Storage.Creatures.Count);
        for (int i = 0; i < a.Storage.Creatures.Count; i++)
        {
            var x = a.Storage.Creatures[i];
            var y = b.Storage.Creatures[i];
            Assert.Equal(x.Id, y.Id);
            Assert.Equal(x.SpeciesId, y.SpeciesId);
            Assert.True(x.Wild.Equals(y.Wild), $"{x.Id}: 素質が違う");
            Assert.True(x.Trained.Equals(y.Trained), $"{x.Id}: 育てた分が違う");
            Assert.Equal(x.Earned, y.Earned);
            Assert.Equal(x.Skill2, y.Skill2);
            Assert.Equal(x.Skill3, y.Skill3);
            Assert.Equal(x.Strong, y.Strong);
            Assert.Equal(x.Weak, y.Weak);
            Assert.Equal(x.Generation, y.Generation);
        }
        Assert.Equal(a.Eggs.Count, b.Eggs.Count);
        for (int i = 0; i < a.Eggs.Count; i++)
        {
            Assert.Equal(a.Eggs[i].Id, b.Eggs[i].Id);
            Assert.Equal(a.Eggs[i].Rarity, b.Eggs[i].Rarity);
            Assert.True(a.Eggs[i].Wild.Equals(b.Eggs[i].Wild));
            Assert.Equal(a.Eggs[i].How, b.Eggs[i].How);
        }
        Assert.Equal(a.Incubating.Count, b.Incubating.Count);
        for (int i = 0; i < a.Incubating.Count; i++)
        {
            Assert.Equal(a.Incubating[i].Egg.Id, b.Incubating[i].Egg.Id);
            Assert.Equal(a.Incubating[i].ReadyUnix, b.Incubating[i].ReadyUnix);
        }
        Assert.Equal(a.Encounters.Count, b.Encounters.Count);
        for (int i = 0; i < a.Encounters.Count; i++)
        {
            Assert.Equal(a.Encounters[i].Nest.Id, b.Encounters[i].Nest.Id);
            Assert.Equal(a.Encounters[i].Nest.SpeciesId, b.Encounters[i].Nest.SpeciesId);
            Assert.Equal(a.Encounters[i].Nest.Tier, b.Encounters[i].Nest.Tier);
            Assert.Equal(a.Encounters[i].Level, b.Encounters[i].Level);
        }
        Assert.Equal(a.Party, b.Party);
        Assert.Equal(a.Idle.Materials, b.Idle.Materials);
        Assert.Equal(a.Idle.Defeated, b.Idle.Defeated);
        Assert.Equal(a.Idle.LastUnix, b.Idle.LastUnix);
    }

    [Fact]
    public void 遊んだ状態がそのまま戻る()
    {
        var game = Played();
        var back = Snapshots.Load(Snapshots.Save(game));
        Assert.NotNull(back);
        Same(game, back!);
    }

    [Fact]
    public void 二度通しても変わらない()
    {
        // ⭐ 保存 → 復元 → 保存 が同じ形になること。抜け落ちる欄があると崩れる
        var game = Played();
        var once = Snapshots.Load(Snapshots.Save(game))!;
        var twice = Snapshots.Load(Snapshots.Save(once))!;
        Same(once, twice);
    }

    [Fact]
    public void 乱数の続きから引ける()
    {
        // ⚠️ ここを保存しないと、遊び直すたびに同じ卵と同じ巣が出る
        var game = Played();
        var back = Snapshots.Load(Snapshots.Save(game))!;

        var a = new List<uint>();
        var b = new List<uint>();
        for (int i = 0; i < 8; i++)
        {
            a.Add(game.RngEgg.U32Value());
            b.Add(back.RngEgg.U32Value());
        }
        Assert.Equal(a, b);
    }

    [Fact]
    public void 保存しないと同じ卵が出てしまう()
    {
        // ⭐ 上の検査が本当に効いていることの裏取り（乱数を戻さなければ食い違う）
        var game = Played();
        var fresh = new Game(game.Seed);
        Assert.NotEqual(game.RngEgg.U32Value(), fresh.RngEgg.U32Value());
    }

    [Fact]
    public void 版が違う保存は読まない()
    {
        var save = Snapshots.Save(Played());
        save.Version = Snapshots.Version + 1;
        Assert.Null(Snapshots.Load(save));
        Assert.Null(Snapshots.Load(null));
    }

    [Fact]
    public void 倒れている者も戻る()
    {
        var game = Games.NewGame(5);
        game.Idle.DownUntil["c001"] = T0 + 20;
        var back = Snapshots.Load(Snapshots.Save(game))!;
        Assert.Equal(T0 + 20, back.Idle.DownUntil["c001"]);
    }

    [Fact]
    public void 復元したあと孵化の残り時間が続く()
    {
        var game = Played();
        var back = Snapshots.Load(Snapshots.Save(game))!;
        var slot = back.Incubating[0];
        Assert.Null(Hatchery.Collect(back, slot.Egg.Id, slot.ReadyUnix - 1));
        Assert.NotNull(Hatchery.Collect(back, slot.Egg.Id, slot.ReadyUnix));
    }
}
