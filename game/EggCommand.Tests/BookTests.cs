using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>図鑑（<see cref="Game.SpeciesSeen"/>）。
///
/// ⚠️ **goldens では守れない**（移植元に無い）。ここが唯一の見張り。
///
/// ⭐ 守りたいのは1つ: **「手に入れたことがある」は減らない。**
/// ⚠️ 「いま持っている」で作ると、分解して枠を空けるたびに図鑑が減る。</summary>
public class BookTests
{
    private const long T0 = 1_700_000_000;

    private static Creature Made(string id, string speciesId) =>
        new Creature(id, speciesId,
            new StatBlock(20, 20, 20, 20, 20, 20),
            new StatBlock(0, 0, 0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1,
            StatKey.Spd, StatKey.Res);

    [Fact]
    public void 手に入れると図鑑に載る()
    {
        var game = Games.NewGame(1, T0);
        game.SpeciesSeen.Clear();

        Games.Keep(game, Made("a", "tamaru"));
        Assert.True(Games.HasSeen(game, "tamaru"));
        Assert.False(Games.HasSeen(game, "iwao"));
        Assert.Equal(1, Games.SeenCount(game));
    }

    /// <summary>⭐ **同じ種族を何体持っても1件。**</summary>
    [Fact]
    public void 同じ種族は二重に載らない()
    {
        var game = Games.NewGame(1, T0);
        game.SpeciesSeen.Clear();

        Games.Keep(game, Made("a", "tamaru"));
        Games.Keep(game, Made("b", "tamaru"));
        Assert.Equal(1, Games.SeenCount(game));
    }

    /// <summary>⚠️ **これが図鑑の芯。**⭐ 分解しても消えない。</summary>
    [Fact]
    public void 分解しても図鑑から消えない()
    {
        var game = Games.NewGame(1, T0);
        game.SpeciesSeen.Clear();

        Games.Keep(game, Made("a", "tsunoga"));
        Games.ReleaseCreature(game, "a");

        // ⚠️ 新しい遊びは最初から個体を持っているので、空にはならない。
        //    ⭐ 見たいのは「手放した個体が保管庫から消えたか」だけ
        foreach (var c in game.Storage.Creatures) Assert.NotEqual("a", c.Id);
        Assert.True(Games.HasSeen(game, "tsunoga"));
    }

    /// <summary>⚠️ **表に無い id を書き込まない。**⭐ 書くと、種族を消したときに
    /// 図鑑が「知らない何か」を1枠抱えたまま残る。</summary>
    [Fact]
    public void 知らない種族は載らない()
    {
        var game = Games.NewGame(1, T0);
        game.SpeciesSeen.Clear();

        Games.See(game, "そんな種族はいない");
        Games.See(game, null);
        Games.See(game, "");
        Assert.Equal(0, Games.SeenCount(game));
    }

    [Fact]
    public void 保存して読み直しても残る()
    {
        var game = Games.NewGame(1, T0);
        game.SpeciesSeen.Clear();

        Games.Keep(game, Made("a", "haneru"));
        Games.ReleaseCreature(game, "a");   // ⭐ 手放してから保存する

        var back = Snapshots.Load(Snapshots.Save(game));
        Assert.True(Games.HasSeen(back, "haneru"));
    }

    /// <summary>⭐ **古い保存（この欄が無い頃のもの）でも、持っている個体は載る。**
    ///
    /// ⚠️ 手元に居るのに「まだ見ていない」と出るほうが嘘になる。
    /// ⭐ 口を通さずに入った個体を拾い直す（self-heal）にもなっている。</summary>
    [Fact]
    public void 古い保存は保管庫から継ぎ足される()
    {
        var game = Games.NewGame(1, T0);
        // ⚠️ **口を通さず**に入れる（図鑑が無かった頃の形）
        game.Storage = Storages.Accept(game.Storage, Made("a", "nobiru"));
        game.SpeciesSeen.Clear();

        var save = Snapshots.Save(game);
        Assert.DoesNotContain("nobiru", save.Seen);

        var back = Snapshots.Load(save);
        Assert.True(Games.HasSeen(back, "nobiru"));
    }

    /// <summary>⚠️ **保管庫へ入る道が増えたら、ここも増やすこと。**
    /// ⭐ 孵化器から出てきた個体も図鑑に載る。</summary>
    [Fact]
    public void 孵化器から出た個体も載る()
    {
        var game = Games.NewGame(1, T0);
        game.SpeciesSeen.Clear();

        var egg = Games.GainEgg(game, Nests.ById("shallow-scale"), EggOrigin.Defeated);
        var slot = Hatchery.Begin(game, egg.Id, T0);
        Hatchery.Rush(slot, T0);
        var born = Hatchery.Collect(game, egg.Id, T0);

        Assert.NotNull(born);
        Assert.True(Games.HasSeen(game, born.SpeciesId));
    }
}
