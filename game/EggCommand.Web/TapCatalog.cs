namespace EggCommand.Web
{
    /// <summary>`tap=` に選べる名前の一覧。<see cref="UiCommands"/> の登録表をそのまま返す。
    /// `out`/`in`（ブラウザへ委譲する保存の出し入れ）も同じ境界で検証する。</summary>
    public static class TapCatalog
    {
        public static string[] Names => UiCommands.TapNames;
    }
}
