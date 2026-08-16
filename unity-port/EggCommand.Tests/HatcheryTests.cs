using System;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>孵化器・希少さ・探索。
/// ⚠️ ここは移植元に無い新しい規則なので、較正値（goldens）ではなく規則そのものを検査する。</summary>
public class HatcheryTests
{
    private const long T0 = 1_700_000_000;

    private static Game Fresh()
    {
        var game = Games.NewGame(2026_08_16);
        return game;
    }

    [Fact]
    public void 希少さが高いほど孵るのに時間がかかる()
    {
        for (int r = 1; r < Rarities.Max; r++)
        {
            Assert.True(Rarities.SecondsOf(r) < Rarities.SecondsOf(r + 1),
                $"★{r} が ★{r + 1} 以上の時間になっている");
        }
    }

    [Fact]
    public void 盗んだ卵は希少さが下がる()
    {
        var rng = new Rng(7);
        int defeated = 0, stolen = 0;
        for (int i = 0; i < 400; i++)
        {
            defeated += Rarities.Roll(rng, 3, EggOrigin.Defeated);
            stolen += Rarities.Roll(rng, 3, EggOrigin.Stolen);
        }
        Assert.True(stolen < defeated, $"倒す {defeated} / 盗む {stolen}");
    }

    [Fact]
    public void 孵化器は五枠まで()
    {
        var game = Fresh();
        for (int i = 0; i < Hatchery.Slots + 2; i++)
        {
            Games.GainEgg(game, Nests.ById("shallow-scale"), EggOrigin.Defeated);
        }

        for (int i = 0; i < Hatchery.Slots; i++)
        {
            Hatchery.Begin(game, game.Eggs[0].Id, T0);
        }
        Assert.False(Hatchery.HasRoom(game));
        Assert.Throws<InvalidOperationException>(() => Hatchery.Begin(game, game.Eggs[0].Id, T0));
    }

    [Fact]
    public void 時間が来るまで取り出せない()
    {
        var game = Fresh();
        var egg = Games.GainEgg(game, Nests.ById("shallow-scale"), EggOrigin.Defeated);
        var slot = Hatchery.Begin(game, egg.Id, T0);
        int need = Rarities.SecondsOf(egg.Rarity);

        Assert.Null(Hatchery.Collect(game, egg.Id, T0));
        Assert.Null(Hatchery.Collect(game, egg.Id, T0 + need - 1));

        var born = Hatchery.Collect(game, egg.Id, slot.ReadyUnix);
        Assert.NotNull(born);
        Assert.Equal(egg.SpeciesId, born!.SpeciesId);
        Assert.Empty(game.Incubating);
    }

    [Fact]
    public void テスト用の短縮で即取り出せる()
    {
        var game = Fresh();
        var egg = Games.GainEgg(game, Nests.ById("peak-fang"), EggOrigin.Defeated);
        var slot = Hatchery.Begin(game, egg.Id, T0);
        Hatchery.Rush(slot, T0);
        Assert.NotNull(Hatchery.Collect(game, egg.Id, T0));
    }

    [Fact]
    public void 戻すと棚に帰り経過は消える()
    {
        var game = Fresh();
        var egg = Games.GainEgg(game, Nests.ById("shallow-scale"), EggOrigin.Defeated);
        Hatchery.Begin(game, egg.Id, T0);
        Hatchery.Cancel(game, egg.Id);

        Assert.Empty(game.Incubating);
        Assert.Contains(game.Eggs, e => e.Id == egg.Id);

        var again = Hatchery.Begin(game, egg.Id, T0 + 999);
        Assert.Equal(T0 + 999, again.StartUnix);
    }

    [Fact]
    public void 探索は常に三件出ている()
    {
        var game = Fresh();
        Assert.Equal(Encounters.Shown, game.Encounters.Count);

        var first = game.Encounters[0].Nest;
        Encounters.Replace(game, first);
        Assert.Equal(Encounters.Shown, game.Encounters.Count);
        Assert.DoesNotContain(game.Encounters, e => e.Nest.Id == first.Id);
    }

    [Fact]
    public void 巣のレベルは段階どおりに並ぶ()
    {
        // ⭐ 振れ幅が段階の間隔を越えないこと。越えると「数が大きい＝手強い」が嘘になる
        var rng = new Rng(11);
        int lowMax = int.MinValue, highMin = int.MaxValue;
        for (int i = 0; i < 500; i++)
        {
            var e = Encounters.Make(rng, i);
            if (e.Nest.Tier == 1) lowMax = Math.Max(lowMax, e.Level);
            if (e.Nest.Tier == 2) highMin = Math.Min(highMin, e.Level);
        }
        Assert.True(lowMax < highMin, $"段階1の最大 {lowMax} が段階2の最小 {highMin} を越えている");
    }

    [Fact]
    public void 探索には巣と野良が必ず並ぶ()
    {
        // ⭐ 3件とも同じ種類だと「卵が獲れない回」「育てられない回」ができてしまう
        for (int seed = 0; seed < 60; seed++)
        {
            var game = Games.NewGame(seed);
            int nests = 0, wild = 0;
            foreach (var e in game.Encounters)
            {
                if (e.Kind == EncounterKind.Nest) nests++; else wild++;
            }
            Assert.True(nests > 0 && wild > 0, $"seed={seed}: 巣{nests} 野良{wild}");

            // 引き直しても崩れない
            for (int i = 0; i < 20; i++)
            {
                Encounters.Replace(game, game.Encounters[i % Encounters.Shown].Nest);
                nests = 0; wild = 0;
                foreach (var e in game.Encounters)
                {
                    if (e.Kind == EncounterKind.Nest) nests++; else wild++;
                }
                Assert.True(nests > 0 && wild > 0, $"seed={seed} 引き直し{i}: 巣{nests} 野良{wild}");
            }
        }
    }

    [Fact]
    public void 野良のほうが厚く伸びる()
    {
        Assert.True(Encounters.WildReward > 1, "野良の見返りが巣の戦闘と同じでは選ぶ理由が無い");
    }

    [Fact]
    public void 探索の巣に居ない種族は出さない()
    {
        var rng = new Rng(3);
        for (int i = 0; i < 200; i++)
        {
            Assert.NotEqual("nushi", Encounters.Make(rng, i).Nest.SpeciesId);
        }
    }

    [Fact]
    public void 配合の卵は世代が深いほど時間がかかる()
    {
        var game = Fresh();
        var ids = new System.Collections.Generic.List<string>();
        foreach (var c in game.Storage.Creatures) ids.Add(c.Id);

        var first = Games.BreedPair(game, ids[0], ids[1]);
        Assert.True(first.Egg.Rarity >= 2, $"1回目の配合で ★{first.Egg.Rarity}");
    }
}
