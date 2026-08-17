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
            // ⚠️ 段階の全域を出すために力量を高く渡す（低いと段1 ばかりになる）
            var e = Encounters.Make(rng, i, 80);
            if (e.Nest.Tier == 1) lowMax = Math.Max(lowMax, e.Level);
            if (e.Nest.Tier == 2) highMin = Math.Min(highMin, e.Level);
        }
        Assert.True(lowMax < highMin, $"段階1の最大 {lowMax} が段階2の最小 {highMin} を越えている");
    }

    [Fact]
    public void 探索の巣に居ない種族は出さない()
    {
        var rng = new Rng(3);
        for (int i = 0; i < 200; i++)
        {
            Assert.NotEqual("nushi", Encounters.Make(rng, i, 80).Nest.SpeciesId);
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

    // ── 巣の居座る時間 ──────────────────────────────

    /// <summary>⭐ **深い巣ほど早く消える。**
    ///
    /// ⚠️ 逆にすると良い巣が居続けて探索が止まります
    /// （1件を掘り尽くすまで他を見なくてよくなるため）。</summary>
    [Fact]
    public void 深い巣ほど居座る時間が短い()
    {
        int last = int.MaxValue;
        for (int tier = 1; tier <= 5; tier++)
        {
            int seconds = Encounters.SecondsFor(tier);
            Assert.True(seconds < last, $"段{tier}: {last}秒 → {seconds}秒 と短くなっていない");
            Assert.True(seconds > 0, $"段{tier}: 居座る時間が {seconds}秒");
            last = seconds;
        }
    }

    /// <summary>⭐ 時間が切れた巣は別の巣に入れ替わる。</summary>
    [Fact]
    public void 時間切れの巣は入れ替わる()
    {
        const long T0 = 1_700_000_000;
        var game = Games.NewGame(2026_08_17);
        // 期限を持たせ直す（NewGame は時刻を渡していない）
        game.Encounters.Clear();
        Encounters.Refill(game, T0);

        var before = new System.Collections.Generic.List<string>();
        foreach (var e in game.Encounters) before.Add(e.Nest.Id);
        Assert.Equal(Encounters.Shown, before.Count);

        // まだ誰も切れていない
        Assert.Equal(0, Encounters.Expire(game, T0 + 1));

        // 一番短い巣でも足りる時間を進める
        long far = T0 + Encounters.SecondsFor(1) + 1;
        int gone = Encounters.Expire(game, far);

        Assert.True(gone > 0, "時間を進めても1件も入れ替わらない");
        Assert.Equal(Encounters.Shown, game.Encounters.Count);
        foreach (var e in game.Encounters)
        {
            Assert.DoesNotContain(e.Nest.Id, before);
            Assert.False(Encounters.IsGone(e, far), "入れ替えた巣がもう切れている");
        }
    }

    /// <summary>⚠️ 期限は「いつ」で持つ。⭐ 画面を見ていない間も進む。</summary>
    [Fact]
    public void 巣の期限は保存して読み直しても残る()
    {
        const long T0 = 1_700_000_000;
        var game = Games.NewGame(5);
        game.Encounters.Clear();
        Encounters.Refill(game, T0);

        var back = Snapshots.Load(Snapshots.Save(game));
        Assert.NotNull(back);
        for (int i = 0; i < game.Encounters.Count; i++)
        {
            Assert.Equal(game.Encounters[i].UntilUnix, back!.Encounters[i].UntilUnix);
            Assert.True(back.Encounters[i].UntilUnix > T0, "期限が入っていない");
        }
    }

    /// <summary>⚠️ 巣の寿命より前の保存（期限 0）は消えない。</summary>
    [Fact]
    public void 期限を持たない巣は消えない()
    {
        var game = Games.NewGame(11);   // ⚠️ NewGame は時刻を渡していないので期限 0
        foreach (var e in game.Encounters) Assert.Equal(0, e.UntilUnix);
        Assert.Equal(0, Encounters.Expire(game, 9_999_999_999));
    }

    /// <summary>⭐ 期限を持たない巣は、**いまから数え直す**。
    /// ⚠️ 消さない ── 起動しただけで探索が丸ごと作り替わってしまう。</summary>
    [Fact]
    public void 期限を持たない巣にいまから期限を与える()
    {
        const long T0 = 1_700_000_000;
        var game = Games.NewGame(11);                 // ⚠️ 時刻を渡していないので期限 0
        var before = new List<string>();
        foreach (var e in game.Encounters) before.Add(e.Nest.Id);

        int stamped = Encounters.Stamp(game, T0);

        Assert.Equal(before.Count, stamped);
        for (int i = 0; i < game.Encounters.Count; i++)
        {
            var e = game.Encounters[i];
            Assert.Equal(before[i], e.Nest.Id);       // ⭐ 巣そのものは入れ替えない
            Assert.Equal(T0 + Encounters.SecondsFor(e.Nest.Tier), e.UntilUnix);
        }
    }

    /// <summary>⚠️ 既に期限を持っている巣には触らない（延命させない）。</summary>
    [Fact]
    public void 期限のある巣は数え直さない()
    {
        const long T0 = 1_700_000_000;
        var game = Games.NewGame(11, T0);
        var was = new List<long>();
        foreach (var e in game.Encounters) was.Add(e.UntilUnix);

        Assert.Equal(0, Encounters.Stamp(game, T0 + 999));

        for (int i = 0; i < game.Encounters.Count; i++)
            Assert.Equal(was[i], game.Encounters[i].UntilUnix);
    }

    /// <summary>⚠️ **開幕の3件も期限を持つこと。**
    /// 時刻を渡さないと期限0＝永久に居座り、「安全に稼げる場所」が全員に配られる。</summary>
    [Fact]
    public void 開幕の巣も時間で消える()
    {
        const long T0 = 1_700_000_000;
        var game = Games.NewGame(2026_08_17, T0);

        foreach (var e in game.Encounters)
        {
            Assert.True(e.UntilUnix > T0, "開幕の巣に期限が入っていない");
        }
        Assert.Equal(Encounters.Shown, Encounters.Expire(game, T0 + 100_000));
    }

    /// <summary>⚠️ 差し替えた巣の盗んだ回数を捨てること。⭐ 残すと保存が単調に膨らむ。</summary>
    [Fact]
    public void 差し替えた巣の記録は残らない()
    {
        var game = Games.NewGame(31);
        var nest = game.Encounters[0].Nest;

        Games.RecordRaid(game, nest);
        Assert.Single(game.Raids);

        Encounters.Replace(game, nest);
        Assert.Empty(game.Raids);
    }
}
