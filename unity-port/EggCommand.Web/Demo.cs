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

    /// <summary>畳んだ帯に出す1行。⚠️ どの数で並べているかまで出す
    /// （「素質合計 順」だけだと、育成を含む数なのか読めなかった）。</summary>
    public static string SortLine(FilterKey filter, SortKey sort, SortBasis basis) =>
        $"{Filters.LabelOf(filter)}　／　{Storages.LabelOf(sort)} 順（{Storages.LabelOf(basis)}）";
}
