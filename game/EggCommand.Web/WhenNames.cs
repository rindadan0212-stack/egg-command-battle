using System;
using System.Collections.Generic;
using EggCommand.Core;

namespace EggCommand.Web
{
    /// <summary>骨組みエディタ P4（案7の A）── `when=`（出す／出さないの条件）の候補を
    /// **実物の骨組みから**集める。
    ///
    /// 🔴 **決め打ちの一覧を持たない**（`anchor` の6値を実物の grep で導いたのと同じ作法）。
    /// ⚠️ 条件の名前は骨組みの語彙ではなく**画面ごとのコード**（`Sheets.When`）が決めるので、
    /// Core に一覧を置く場所が無い ── 「いま実際に使われている名前」を数えるのが唯一の
    /// 正しい出所になる。
    ///
    /// ⚠️ 集める範囲を決めるのは呼ぶ側（`EditPage.razor`）── いま直している骨組みと
    /// その土台の2枚だけを渡す。35枚ぶん全部を渡すと、**その画面では効かない名前**まで
    /// 勧めることになる（条件の真偽は画面ごとに別の `When` が答えるため）。
    ///
    /// ⚠️ **Core にしか依存しない**（<see cref="TapCrowns"/>/<see cref="EditAttrs"/> と同じ形で
    /// `EggCommand.Tests` へ直接コンパイルできる純関数）。`Scenes`/`LayoutStore` を使う
    /// Web 専用の配線は `EditPage.razor` に置く。</summary>
    public static class WhenNames
    {
        /// <summary>純関数: 木の集まりから `when=` の**名前**を、あいうえお順・重複無しで集める。
        ///
        /// ⚠️ `!`（偽のとき出す）は落とす（<see cref="Layouts.WhenOf"/> が外す）── エディタでは
        /// 反転を別の切替が持つので、候補に `!有る` と `有る` の2つを並べない。
        /// ⚠️ 空の名前（`when=` だけ・`when=!` だけ ── `FaultKind.EmptyWhenName` の不備）は
        /// 候補に入れない（不備を勧めない）。</summary>
        public static List<string> Of(IEnumerable<Layout> layouts)
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var layout in layouts)
                if (layout != null) Walk(layout.Roots, names);
            return new List<string>(names);
        }

        private static void Walk(IReadOnlyList<LayoutNode> nodes, SortedSet<string> into)
        {
            if (nodes == null) return;
            foreach (var node in nodes)
            {
                var name = Layouts.WhenOf(node);
                if (!string.IsNullOrEmpty(name)) into.Add(name);
                Walk(node.Children, into);
            }
        }
    }
}
