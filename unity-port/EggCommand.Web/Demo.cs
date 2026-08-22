using EggCommand.Core;

namespace EggCommand.Web;

/// <summary>画面を確かめるための、決まった中身のゲーム。
///
/// ⚠️ **本番の入口ではありません。**⭐ 骨組みが実物の数で描けることと、
/// 検査（`scripts/audit.mjs`）が通ることを見るための土台です。
///
/// ⚠️ 種と時刻を固定する ── 毎回違う画面を撮ると、比べたときの差が
/// 「直したから」なのか「引きが違うから」なのか分からなくなる。</summary>
public static class Demo
{
    public const int Seed = 20260822;
    public const long Now = 1_700_000_000;

    /// <summary>⭐ 14体。一覧が4列で3段以上になる数（巻物が効くことを見るため）。</summary>
    public static Game Game() => Games.NewGame(Seed, Now, startWith: 14);

    /// <summary>絞って並べた一覧。⭐ **絞ってから並べる**（BOX・配合・編成で同じ順）。</summary>
    public static IReadOnlyList<Creature> Sorted(Game game,
        FilterKey filter = FilterKey.All,
        SortKey sort = SortKey.WildTotal,
        SortBasis basis = SortBasis.Born)
    {
        var pool = Filters.Apply(game, game.Storage.Creatures, filter);
        return Storages.Sorted(new Storage(game.Storage.Slots, pool), sort, basis);
    }

    /// <summary>卵をいくつか温めている状態を作る。
    /// ⚠️ 入れる前に**棚へ載せる** ── `Hatchery.Begin` は棚から取る作り。
    /// ⭐ 始めた時刻をずらして、進み具合の違う枠を並べる（帯を見るため）。</summary>
    public static void Incubate(Game game, long now, int howMany)
    {
        var nest = Nests.ById("shallow-scale");
        int want = Math.Clamp(howMany, 0, Hatchery.Slots);
        for (int i = 0; i < want; i++)
        {
            var egg = Nests.MakeEgg(game.RngEgg, nest, EggOrigin.Defeated, ++game.Serial,
                element: SpeciesTable.Roll(game.RngElement));
            game.Eggs.Add(egg);
            Hatchery.Begin(game, egg.Id, now - i * 1200);
        }
    }

    /// <summary>決まった戦いを1つ組む。⚠️ 遊びの入口ではない ── 画面を見るための土台。
    /// ⭐ 巣の守り手を相手にする（本番と同じ作り方）。</summary>
    public static BattleState Fight(Game game)
    {
        var nest = Nests.ById("shallow-scale");
        var mine = new List<Creature>();
        foreach (var id in Games.RosterOf(game, PartyKind.Nest))
            foreach (var c in game.Storage.Creatures) if (c.Id == id) mine.Add(c);
        if (mine.Count == 0)
            for (int i = 0; i < Games.PartySize && i < game.Storage.Creatures.Count; i++)
                mine.Add(game.Storage.Creatures[i]);

        var state = EggCommand.Core.Battle.CreateBattle(mine, Games.DefendersOf(game, nest));
        // ⭐ ゲージを少し進めておく（帯が動いていることが見える形にする）
        for (int i = 0; i < state.Units.Count; i++)
            state.Units[i].Gauge = EggCommand.Core.Battle.GaugeMax / (i + 2);
        return state;
    }

    /// <summary>決まった潜入を1つ組む。⚠️ 遊びの入口ではない ── 盤を見るための土台。</summary>
    public static Raid Raid(Game game, int raids = 0)
    {
        var nest = Nests.ById("shallow-scale");
        return Trails.Begin(Trails.OfNest(nest), Games.PartyOf(game), raids);
    }

    /// <summary>棚に卵を積む（孵化器には入れない）。</summary>
    public static void Shelve(Game game, int howMany)
    {
        var nest = Nests.ById("shallow-scale");
        for (int i = 0; i < Math.Max(0, howMany); i++)
        {
            game.Eggs.Add(Nests.MakeEgg(game.RngEgg, nest, EggOrigin.Defeated, ++game.Serial,
                element: SpeciesTable.Roll(game.RngElement)));
        }
    }
}
