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
