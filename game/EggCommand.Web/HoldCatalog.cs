namespace EggCommand.Web
{
    /// <summary>`hold=`（長押しで開く札）に選べる名前の一覧。
    /// <see cref="UiCommands"/> の登録表を使うため、短押し候補とは混ざらない。</summary>
    public static class HoldCatalog
    {
        public static string[] Names => UiCommands.HoldNames;
    }
}
