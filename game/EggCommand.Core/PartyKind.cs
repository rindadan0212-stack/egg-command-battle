namespace EggCommand.Core
{
    /// <summary>編成の用途。⭐ **放置と巣で別の3体を選べる**。
    ///
    /// ⚠️ 分けていなかった頃は1本しか無く、巣に合わせて組み替えると
    /// 放置で溜めていた側も入れ替わってしまった。</summary>
    public enum PartyKind
    {
        /// <summary>放置に出している3体。</summary>
        Idle,
        /// <summary>巣へ潜る3体。⭐ 3つ登録できる。</summary>
        Nest,
    }
}
