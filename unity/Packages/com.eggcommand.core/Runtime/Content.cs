#nullable enable

namespace EggCommand.Core
{
    /// <summary>中身（種族・技・巣）の検査を通す**唯一の入口**。
    ///
    /// ⭐ 表が3つに分かれているので、足す人が「どれを呼ぶか」を覚えなくて済むようにする。
    /// 中身を1つ足したら、ここを1回呼べば繋がっているかが分かる、という状態を保つ。
    ///
    /// ⚠️ **件数は数えない。** 照合データ（golden）は移植が正しいことの証明で、
    /// 中身の量とは関係が無い。数える検査を golden に置くと、
    /// 種族や技を1つ足すたびに落ちて、作り直したくなる。
    /// ⚠️ golden は TS 実装との一致の記録で、TS 側はもう触っているので**二度と作れない**。
    ///
    /// | 何を守るか | どこが見るか |
    /// |---|---|
    /// | 移植が正しいこと（変えてはいけない値） | golden（id 引きで照合。足しても落ちない） |
    /// | 中身が規則を守っていること | ここ |
    /// </summary>
    public static class Content
    {
        public static void Audit()
        {
            SpeciesTable.Audit();
            Skills.Audit();
            Nests.Audit();
            Encounters.Audit();
        }
    }
}
