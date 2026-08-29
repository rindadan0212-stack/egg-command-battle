using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>家系図（作者の指示「BOXで2世代以降のキャラクターの家系図を見られるように」）。
///
/// ⚠️ 配合は両親を消す（<see cref="Games.FusePair"/>）ので、遡るための控えは
/// 「墓標」（<see cref="Tomb"/>）しかない。ここでは墓標が正しく積まれること
/// （<see cref="Tombs"/>）と、そこから3代ぶんを正しく組み立てられること
/// （<see cref="Lineage"/>）を検査する。</summary>
public class LineageTests
{
    // ── 組み立て道具 ────────────────────────────────

    /// <summary>保管庫に居る（＝まだ配合していない）個体を作って足す。</summary>
    private static Creature Live(Game game, string id, string speciesId, int wildTotal,
        string? parentA, string? parentB, int generation)
    {
        var c = new Creature(id, speciesId, new StatBlock(wildTotal, 0, 0, 0),
            new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, parentA, parentB, generation);
        game.Storage = Storages.Accept(game.Storage, c);
        return c;
    }

    /// <summary>墓標だけを手で作る（配合を経由せず、壊れた保存や特定の血統を再現するため）。</summary>
    private static Tomb Buried(string id, string speciesId, int wildTotal,
        string? parentA = null, string? parentB = null, int generation = 0) =>
        new Tomb(id, speciesId, Element.Fire, generation, parentA, parentB, wildTotal);

    // ── 配合で墓標が積まれる ────────────────────────────

    [Fact]
    public void 配合すると両親の墓標が2つ積まれ消える前の中身が入っている()
    {
        var game = Games.NewGame(2026_08_29);
        var ids = new List<string>();
        foreach (var c in game.Storage.Creatures) ids.Add(c.Id);

        var a = Games.CreatureById(game, ids[0]);
        var b = Games.CreatureById(game, ids[1]);
        // ⚠️ 消える前に控える ── 消したあとは game.Storage から同じ値を取れない
        string aSpecies = a.SpeciesId, bSpecies = b.SpeciesId;
        int aWild = Creatures.WildTotalOf(a), bWild = Creatures.WildTotalOf(b);
        int aGen = a.Generation, bGen = b.Generation;
        Element aElement = a.Element, bElement = b.Element;

        Assert.Empty(game.Tombs);
        Games.FusePair(game, ids[0], ids[1]);

        // 🔴 **2つ積まれる**
        Assert.Equal(2, game.Tombs.Count);
        var tombA = game.Tombs.Find(t => t.Id == ids[0]);
        var tombB = game.Tombs.Find(t => t.Id == ids[1]);
        Assert.NotNull(tombA);
        Assert.NotNull(tombB);

        // 🔴 **消える前の中身が入っている**（0 やデフォルト値ではない）
        Assert.Equal(aSpecies, tombA!.SpeciesId);
        Assert.Equal(aWild, tombA.WildTotal);
        Assert.Equal(aGen, tombA.Generation);
        Assert.Equal(aElement, tombA.Element);
        Assert.Equal(bSpecies, tombB!.SpeciesId);
        Assert.Equal(bWild, tombB.WildTotal);
        Assert.Equal(bGen, tombB.Generation);
        Assert.Equal(bElement, tombB.Element);

        // ⭐ 消えたことも裏取り（保管庫には残っていない）
        Assert.DoesNotContain(game.Storage.Creatures, c => c.Id == ids[0]);
        Assert.DoesNotContain(game.Storage.Creatures, c => c.Id == ids[1]);
    }

    // ── 並び ────────────────────────────────────────

    /// <summary>⭐ **並びは決め打ちの二分木**（自分=0／親=1,2／祖父母=3,4,5,6）。</summary>
    [Fact]
    public void 三代ぶんが正しい並びで返る()
    {
        var game = new Game(1);
        var gA1 = Buried("gA1", "tamaru", 10);
        var gA2 = Buried("gA2", "tsunoga", 20);
        var gB1 = Buried("gB1", "haneru", 30);
        var gB2 = Buried("gB2", "tamaru", 40);
        var pA = Buried("pA", "tsunoga", 50, gA1.Id, gA2.Id, generation: 1);
        var pB = Buried("pB", "haneru", 60, gB1.Id, gB2.Id, generation: 1);
        game.Tombs.AddRange(new[] { gA1, gA2, gB1, gB2, pA, pB });

        var self = Live(game, "self", "tamaru", 70, pA.Id, pB.Id, generation: 2);

        var nodes = Lineage.Of(game, self, 2);

        Assert.Equal(7, nodes.Length);
        Assert.Equal("self", nodes[0].Id);
        Assert.Equal("pA", nodes[1].Id);
        Assert.Equal("pB", nodes[2].Id);
        Assert.Equal("gA1", nodes[3].Id);
        Assert.Equal("gA2", nodes[4].Id);
        Assert.Equal("gB1", nodes[5].Id);
        Assert.Equal("gB2", nodes[6].Id);
        foreach (var n in nodes) Assert.True(n.Known, $"{n.Id} が不明のまま");

        // ⭐ 保管庫に居る個体（self）も、墓標に居る個体もどちらも辿れる
        Assert.Equal("tamaru", nodes[0].SpeciesId);
        Assert.Equal(2, nodes[0].Generation);
        Assert.Equal(60, nodes[2].WildTotal);
    }

    /// <summary>⚠️ **墓標が無い先祖は「不明」で埋まり、木が途中で切れない**
    /// ── 片方の枝（親A）が不明でも、もう片方の枝（親B とその親）はそのまま出る。</summary>
    [Fact]
    public void 墓標が無い先祖は不明で埋まり木が途中で切れない()
    {
        var game = new Game(2);
        var gB1 = Buried("gB1", "tamaru", 10);
        // ⚠️ "missing-gB2" は墓標にも保管庫にも居ない ID
        var pB = Buried("pB", "haneru", 40, gB1.Id, "missing-gB2", generation: 1);
        game.Tombs.Add(gB1);
        game.Tombs.Add(pB);

        // ⭐ 親A は最初から居ない（野生から生まれた個体・ParentA=null）
        var self = Live(game, "self", "tamaru", 70, parentA: null, parentB: pB.Id, generation: 2);

        var nodes = Lineage.Of(game, self, 2);

        Assert.True(nodes[0].Known);
        Assert.False(nodes[1].Known, "親Aは持たないはずなのに何か出ている");
        Assert.False(nodes[3].Known, "居ない親の先に何か出ている");
        Assert.False(nodes[4].Known, "居ない親の先に何か出ている");

        // 🔴 木が途中で切れない ── 親Bの枝はちゃんと最後まで出る
        Assert.True(nodes[2].Known);
        Assert.Equal("pB", nodes[2].Id);
        Assert.True(nodes[5].Known);
        Assert.Equal("gB1", nodes[5].Id);
        // ⚠️ 墓標の無い祖先（missing-gB2）だけが「不明」
        Assert.False(nodes[6].Known);
        Assert.Null(nodes[6].Id);
        Assert.Null(nodes[6].SpeciesId);
    }

    /// <summary>🔴 **輪になっている保存（親が自分を指す）でも落ちない。**
    /// ⚠️ わざと壊した保存を模す（実際に起こりうる壊れ方 ── 手で書き換えた保存や、
    /// 将来のバグで親子関係が循環したもの）。</summary>
    [Fact]
    public void 輪になっている保存でも落ちない()
    {
        var game = new Game(3);
        // ⚠️ 墓標の親が「自分自身（self）」を指す ── 壊れた保存の典型
        var tombA = Buried("tombA", "tamaru", 10, parentA: "self", parentB: null, generation: 1);
        game.Tombs.Add(tombA);
        var self = Live(game, "self", "haneru", 50, parentA: tombA.Id, parentB: null, generation: 2);

        Lineage.Node[]? nodes = null;
        var ex = Record.Exception(() => nodes = Lineage.Of(game, self, 2));

        Assert.Null(ex);
        Assert.NotNull(nodes);
        Assert.True(nodes![1].Known);
        Assert.Equal("tombA", nodes[1].Id);
        // ⭐ 輪を踏んだ先（tombA の親 ＝ self）は「不明」で止まる。木を無限には辿らない
        Assert.False(nodes[3].Known);
    }

    // ── 上限 ────────────────────────────────────────

    /// <summary>⚠️ **際限なく増えない**（作者の指示）。古いものから捨てる。</summary>
    [Fact]
    public void 墓標が上限を超えたら古いものから捨てられる()
    {
        var game = new Game(4);
        int total = Tombs.Limit + 50;
        for (int i = 0; i < total; i++)
        {
            var c = new Creature($"c{i}", "tamaru", new StatBlock(i, 0, 0, 0),
                new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 0);
            Tombs.Bury(game, c);
        }

        Assert.Equal(Tombs.Limit, game.Tombs.Count);
        Assert.DoesNotContain(game.Tombs, t => t.Id == "c0");
        Assert.DoesNotContain(game.Tombs, t => t.Id == "c49");
        Assert.Contains(game.Tombs, t => t.Id == $"c{total - 1}");
    }
}
