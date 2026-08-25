namespace EggCommand.Web
{
    /// <summary>`tap=` に選べる名前の一覧（骨組みエディタ E2・計画 §11-8・作者の決定6）。
    ///
    /// 🔴 **唯一の出所は `Shell.cs` の `public void Tap(string what, string at)` の
    /// `switch (what)`**（45個）。⚠️ ここは「並べただけ」の写し ── 手で写した瞬間に
    /// 2つに割れるので、`EggCommand.Tests` の `TapCatalogTests` が `Shell.cs` の
    /// ソースをテキストとして読み直し、`case "..."` を正規表現で抜き出して
    /// この配列と過不足なく一致するかを検査する（ずれたら test が落ちる ── 計画の
    /// (b) 案「switch と同じ場所で並べ、ずれたらテストで落とす」を選んだ）。
    ///
    /// ⚠️ **例外2つ（`out`/`in`）**: `Shell.cs` の switch の外、`AppPage.razor:152`
    /// の `if (what is "out" or "in")` が先取りして処理する（配合の親を差し替える
    /// トレード）。骨組みエディタは「実際に押して意味がある `tap=` の全集合」を
    /// 出したいので、この2つも一覧に含める（`TapCatalogTests` がこの2つを
    /// 例外として突き合わせる）。</summary>
    public static class TapCatalog
    {
        public static readonly string[] Names =
        {
            // ── Shell.cs Tap() の switch（43個・出現順そのまま） ──────
            "tab", "back", "close", "cheer", "extra", "bar-toggle",
            "chips-filter", "chips-sort", "chips-basis", "one",
            "nest", "boss", "roll", "square", "pay", "skip",
            "s0", "s1", "s2", "pick", "give", "stop", "go",
            "slot", "egg", "fuse", "melt", "train", "row", "chip", "feed", "grow",
            "pa", "pb", "breed",
            "party", "set", "seat", "done",
            "keep",
            "trials", "trial", "species",
            // ── switch の外（AppPage.razor:152 の例外・2個） ──────────
            "out", "in",
        };
    }
}
