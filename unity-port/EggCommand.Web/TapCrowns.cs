using System;
using System.Collections.Generic;
using EggCommand.Core;

namespace EggCommand.Web
{
    /// <summary>骨組みエディタ E2 ── 「冠」の逆算（計画 §11-8・作者の決定6・
    /// `Layout.cs` の `Layouts.Rename`）。
    ///
    /// `use=` で部品を差すと、差し込まれた側の `tap=`/`hold=`/`bind=`/`repeat=`/`when=` の
    /// 値の頭に「差した枠の名前 + "-"」が冠として付く。部品ファイル自身（例: `cell.txt`）を
    /// 編集しているときに `Shell.Tap` の生の名前をそのまま候補として出すと、実際には
    /// 存在しない `tap=` を提示してしまう（例: `box.txt` の `cellA`/`cellB`、`fuse.txt` の
    /// `cell` ── 差し込み先ごとに冠が変わる）。
    ///
    /// ここでは「その部品ファイルが、実際にどんな名前（冠）で差されているか」を、
    /// 解決済みの木を歩いて逆算する。⚠️ **`use=` を持つ節点自身の `Name` は
    /// `Layouts.Splice` を通っても変わらない**（`Rename` は差し込んだ側の**子**にだけ
    /// 冠を付ける ── だから解決済みの木を歩いて `Option("use") == partId` の節点を
    /// 見つければ、その `Name` がそのまま冠になる。入れ子（部品がさらに他の部品を差す）が
    /// あっても、外側の `Rename` が子孫すべての `Name` に冠を重ねて付けるので、
    /// この歩き方は多段の入れ子でも壊れない）。
    ///
    /// ⚠️ **Core にしか依存しない**（`Crowns` 本体は `EditAttrs.cs`/`EditAlign.cs` と同じ形で
    /// `EggCommand.Tests` へ直接コンパイルできる純関数）。`Scenes`/`LayoutStore` を使う
    /// Web 側の呼び出し口は `EditPage.razor` に置く（そこだけが Web 専用の配線を持つ）。</summary>
    public static class TapCrowns
    {
        /// <summary>純関数: 解決済みの木の集まりから、`partId` が `use=` されている節点の
        /// 名前（＝冠。末尾の `"-"` は付けない）を、出現順・重複無しで集める。</summary>
        public static List<string> Crowns(IEnumerable<Layout> resolvedLayouts, string partId)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var found = new List<string>();
            foreach (var layout in resolvedLayouts)
                if (layout != null) Walk(layout.Roots, partId, seen, found);
            return found;
        }

        private static void Walk(IReadOnlyList<LayoutNode> nodes, string partId,
            HashSet<string> seen, List<string> into)
        {
            foreach (var node in nodes)
            {
                if (node.Option("use") == partId && seen.Add(node.Name)) into.Add(node.Name);
                Walk(node.Children, partId, seen, into);
            }
        }
    }
}
