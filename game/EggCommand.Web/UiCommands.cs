using EggCommand.Core;

namespace EggCommand.Web;

/// <summary>骨組み/JavaScript から届く操作名を、境界で一度だけ型へ直す。
/// 名前、候補一覧、受け手の有無をこの登録表から派生させる。</summary>
public enum UiActionKind
{
    Tab, Close, Cheer, Menu, Book, BarToggle, ChipsFilter, ChipsSort, ChipsBasis, One,
    Nest, Boss, Roll, Square, Pay, Skip, S0, S1, S2, Aim, Pick, Give, Feat, Stop, Go,
    Slot, Egg, EggStar, EggNew, Fuse, Melt, Train, Row, Chip, Feed, Grow, Detail, Tree,
    Spend, Pa, Pb, Breed, Party, Set, Seat, Done, Keep, Trial, Species, Out, In,
    DetailS0, DetailS1, DetailS2, PfillS0, PfillS1, PfillS2, QfillS0, QfillS1, QfillS2,
    SheetpS0, SheetpS1, SheetpS2, Skill1, Skill2, Skill3,
}

/// <summary>外部入力を読んだ結果。<see cref="Index"/> は入れ子の番号も境界で解いた値。</summary>
public readonly record struct UiCommand(UiActionKind Kind, string Name, string At, int Index);

/// <summary>操作名の唯一の登録表。<c>tap=</c>/<c>hold=</c> の文字列互換はここで保つ。</summary>
public static class UiCommands
{
    // -1 は添字を使わない、0 は動的な一覧なので非負だけを境界で保証する。
    // 正の値は固定長の一覧で、境界で範囲まで検証する。
    private sealed record Definition(string Name, UiActionKind Kind, bool Hold = false, int IndexCount = -1);

    private static readonly Definition[] Definitions =
    {
        new("tab", UiActionKind.Tab, IndexCount: 5), new("close", UiActionKind.Close), new("cheer", UiActionKind.Cheer),
        new("menu", UiActionKind.Menu), new("book", UiActionKind.Book), new("bar-toggle", UiActionKind.BarToggle),
        new("chips-filter", UiActionKind.ChipsFilter, IndexCount: Filters.Keys.Length), new("chips-sort", UiActionKind.ChipsSort, IndexCount: Storages.SortKeys.Length), new("chips-basis", UiActionKind.ChipsBasis, IndexCount: Storages.Bases.Length), new("one", UiActionKind.One, IndexCount: 0),
        new("nest", UiActionKind.Nest, IndexCount: 0), new("boss", UiActionKind.Boss), new("roll", UiActionKind.Roll), new("square", UiActionKind.Square, IndexCount: 0), new("pay", UiActionKind.Pay), new("skip", UiActionKind.Skip),
        new("s0", UiActionKind.S0), new("s1", UiActionKind.S1), new("s2", UiActionKind.S2), new("aim", UiActionKind.Aim), new("pick", UiActionKind.Pick), new("give", UiActionKind.Give), new("feat", UiActionKind.Feat), new("stop", UiActionKind.Stop), new("go", UiActionKind.Go),
        new("slot", UiActionKind.Slot, IndexCount: Hatchery.Slots), new("egg", UiActionKind.Egg, IndexCount: 0), new("eggstar", UiActionKind.EggStar), new("eggnew", UiActionKind.EggNew),
        new("fuse", UiActionKind.Fuse), new("melt", UiActionKind.Melt), new("train", UiActionKind.Train), new("row", UiActionKind.Row, IndexCount: 0), new("chip", UiActionKind.Chip, IndexCount: 0), new("feed", UiActionKind.Feed), new("grow", UiActionKind.Grow), new("detail", UiActionKind.Detail), new("tree", UiActionKind.Tree), new("spend", UiActionKind.Spend, IndexCount: Stats.Keys.Length),
        new("pa", UiActionKind.Pa), new("pb", UiActionKind.Pb), new("breed", UiActionKind.Breed), new("party", UiActionKind.Party), new("set", UiActionKind.Set, IndexCount: Games.NestPartySlots), new("seat", UiActionKind.Seat, IndexCount: 0), new("done", UiActionKind.Done), new("keep", UiActionKind.Keep), new("trial", UiActionKind.Trial, IndexCount: 0), new("species", UiActionKind.Species, IndexCount: SpeciesTable.All.Count),
        new("out", UiActionKind.Out), new("in", UiActionKind.In),
        new("detail-s0", UiActionKind.DetailS0, true), new("detail-s1", UiActionKind.DetailS1, true), new("detail-s2", UiActionKind.DetailS2, true),
        new("s0", UiActionKind.S0, true), new("s1", UiActionKind.S1, true), new("s2", UiActionKind.S2, true),
        new("pfill-s0", UiActionKind.PfillS0, true), new("pfill-s1", UiActionKind.PfillS1, true), new("pfill-s2", UiActionKind.PfillS2, true),
        new("qfill-s0", UiActionKind.QfillS0, true), new("qfill-s1", UiActionKind.QfillS1, true), new("qfill-s2", UiActionKind.QfillS2, true),
        new("sheetp-s0", UiActionKind.SheetpS0, true), new("sheetp-s1", UiActionKind.SheetpS1, true), new("sheetp-s2", UiActionKind.SheetpS2, true),
        new("skill1", UiActionKind.Skill1, true, 0), new("skill2", UiActionKind.Skill2, true, 0), new("skill3", UiActionKind.Skill3, true, 0),
    };

    private static readonly Dictionary<string, Definition> Taps = Definitions.Where(d => !d.Hold)
        .ToDictionary(d => d.Name, StringComparer.Ordinal);
    private static readonly Dictionary<string, Definition> Holds = Definitions.Where(d => d.Hold)
        .ToDictionary(d => d.Name, StringComparer.Ordinal);

    public static string[] TapNames { get; } = Definitions.Where(d => !d.Hold).Select(d => d.Name).ToArray();
    public static string[] HoldNames { get; } = Definitions.Where(d => d.Hold).Select(d => d.Name).ToArray();
    public static string[] IndexedTapNames { get; } = Definitions.Where(d => !d.Hold && d.IndexCount >= 0).Select(d => d.Name).ToArray();
    public static string[] IndexedHoldNames { get; } = Definitions.Where(d => d.Hold && d.IndexCount >= 0).Select(d => d.Name).ToArray();
    public static string[] BoundedTapNames { get; } = Definitions.Where(d => !d.Hold && d.IndexCount > 0).Select(d => d.Name).ToArray();
    public static string[] BoundedHoldNames { get; } = Definitions.Where(d => d.Hold && d.IndexCount > 0).Select(d => d.Name).ToArray();
    public static string[] DynamicTapNames { get; } = Definitions.Where(d => !d.Hold && d.IndexCount == 0).Select(d => d.Name).ToArray();
    public static string[] DynamicHoldNames { get; } = Definitions.Where(d => d.Hold && d.IndexCount == 0).Select(d => d.Name).ToArray();

    public static bool TryParseTap(string name, string at, out UiCommand command) => TryParse(Taps, name, at, out command);
    public static bool TryParseHold(string name, string at, out UiCommand command) => TryParse(Holds, name, at, out command);

    private static bool TryParse(IReadOnlyDictionary<string, Definition> definitions, string name, string at, out UiCommand command)
    {
        if (definitions.TryGetValue(name, out var definition))
        {
            int index = IndexOf(at);
            if (IsValid(definition, index))
            {
                command = new UiCommand(definition.Kind, definition.Name, at, index);
                return true;
            }
        }
        command = default;
        return false;
    }

    public static bool IsValidTap(UiCommand command) => IsValidCommand(Taps, command);
    public static bool IsValidHold(UiCommand command) => IsValidCommand(Holds, command);

    /// <summary>現在の動的一覧に対する添字の安全な範囲。</summary>
    public static bool IsWithinRange(UiCommand command, int count) => command.Index >= 0 && command.Index < count;

    private static bool IsValidCommand(IReadOnlyDictionary<string, Definition> definitions, UiCommand command) =>
        definitions.TryGetValue(command.Name, out var definition)
        && definition.Kind == command.Kind
        && IsValid(definition, command.Index);

    private static bool IsValid(Definition definition, int index) =>
        definition.IndexCount < 0 || (index >= 0 && (definition.IndexCount == 0 || index < definition.IndexCount));

    private static int IndexOf(string at)
    {
        if (string.IsNullOrEmpty(at)) return -1;
        int cut = at.IndexOf('#');
        return int.TryParse(cut < 0 ? at : at[..cut], out int index) ? index : -1;
    }
}
