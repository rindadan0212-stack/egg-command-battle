using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>属性は**個体**が持つ（種族ではない）。2026-08-17 に移した。</summary>
public class ElementTests
{
    [Fact]
    public void 同じ種族から3属性とも生まれる()
    {
        var game = Games.NewGame(7);
        var nest = Nests.ById("shallow-scale");   // タマルの巣

        var seen = new HashSet<Element>();
        for (int i = 0; i < 60; i++)
        {
            seen.Add(Games.GainEgg(game, nest, EggOrigin.Defeated).Element);
        }

        // ⭐ 炎のタマルも水のタマルも出る
        Assert.Equal(SpeciesTable.Elements.Length, seen.Count);
    }

    [Fact]
    public void 巣の守り手は挑むたびに属性が変わりうる()
    {
        var game = Games.NewGame(11);
        var nest = Nests.ById("thicket-fang");

        var seen = new HashSet<Element>();
        for (int i = 0; i < 60; i++)
        {
            foreach (var defender in Games.DefendersOf(game, nest)) seen.Add(defender.Element);
        }

        // ⚠️ ここが1つに固まっていると、有利属性を揃えるだけで巣が確定で落ちる
        Assert.Equal(SpeciesTable.Elements.Length, seen.Count);
    }

    [Fact]
    public void 配合の子は親のどちらかの属性を継ぐ()
    {
        var game = Games.NewGame(23);
        var ids = new List<string>();
        foreach (var c in game.Storage.Creatures) ids.Add(c.Id);

        for (int i = 0; i + 1 < ids.Count; i += 2)
        {
            var a = Games.CreatureById(game, ids[i]);
            var b = Games.CreatureById(game, ids[i + 1]);
            var outcome = Games.FusePair(game, a.Id, b.Id);
            Assert.True(outcome.Egg.Element == a.Element || outcome.Egg.Element == b.Element);
        }
    }

    [Fact]
    public void 孵っても卵の属性のまま()
    {
        var game = Games.NewGame(31);
        var egg = Games.GainEgg(game, Nests.ById("cliff-plume"), EggOrigin.Stolen);
        var born = Games.HatchEgg(game, egg.Id);
        Assert.Equal(egg.Element, born.Element);
    }

    /// <summary>⚠️ 属性を個体へ移す前の保存は Element を持たない（-1）。
    /// その個体の見え方が黙って変わらないよう、種族が昔持っていた属性で埋める。</summary>
    [Fact]
    public void 属性を持たない古い保存は昔の属性で読める()
    {
        var game = Games.NewGame(41);
        var save = Snapshots.Save(game);
        foreach (var c in save.Creatures) c.Element = -1;

        var back = Snapshots.Load(save)!;
        foreach (var c in back.Storage.Creatures)
        {
            Assert.Equal(Migrations.ElementOf(c.SpeciesId), c.Element);
        }
    }

    [Fact]
    public void 属性は保存して戻しても変わらない()
    {
        var game = Games.NewGame(43);
        Games.GainEgg(game, Nests.ById("peak-fang"), EggOrigin.Defeated);

        var back = Snapshots.Load(Snapshots.Save(game))!;
        for (int i = 0; i < game.Storage.Creatures.Count; i++)
        {
            Assert.Equal(game.Storage.Creatures[i].Element, back.Storage.Creatures[i].Element);
        }
        Assert.Equal(game.Eggs[0].Element, back.Eggs[0].Element);
    }

    /// <summary>有利 ×1.5 / 不利 ×0.75。⚠️ 逆数ではない。</summary>
    [Fact]
    public void 三すくみの倍率が決めたとおり()
    {
        foreach (var element in SpeciesTable.Elements)
        {
            var weaker = SpeciesTable.Beats(element);
            Assert.Equal(1.5, Battle.ElementMultiplier(element, weaker));
            Assert.Equal(0.75, Battle.ElementMultiplier(weaker, element));
            Assert.Equal(1.0, Battle.ElementMultiplier(element, element));
        }
    }
}
