using System;
using EggCommand.Core;

namespace EggCommand.Web
{
    /// <summary>骨組みエディタ E2 ── 「層」（計画 §11-2）。⭐ 骨組みは1本の木のまま、
    /// 層は「節点の性質」から見なすだけ（ファイル形式も Core も変えない・保存されない）。
    ///
    /// ⚠️ **Core にしか依存しない**（`EggCommand.Tests` へ `EditAttrs.cs`/`EditAlign.cs` と
    /// 同じ形で直接コンパイルできるように ── `dotnet test` が Web を建てない約束を守るため）。</summary>
    public enum EditLayer
    {
        /// <summary>種類が `paint`。</summary>
        Paint,
        /// <summary>`bind=`/`repeat=` を持つ、または種類が `pixel`/`icon`/`bar`/`label`。</summary>
        Dynamic,
        /// <summary>`tap=`/`hold=` を持つ。</summary>
        Tap,
        /// <summary>上のどれでもない（`box`/`card`/`scroll` 等）。</summary>
        Container,
    }

    public static class EditLayers
    {
        /// <summary>その節点がいまの層でどれに見なされるか。
        ///
        /// ⚠️ **優先順位（作者の設計に無い部分の判断・報告に明記）**: `paint` → `tap`/`hold`
        /// → `bind`/`repeat`/`pixel`/`icon`/`bar`/`label` → それ以外、の順で最初に当たったもの。
        /// 実物の骨組みには複数の条件を同時に満たす節点がある
        /// （例: `bgrow button tap=grow bind=grow` ── 押しどころでも動く物でもある、実測12件）。
        /// 計画の表は4つの見分け方を並べているだけで優先順位を書いていないので、
        /// 「押しどころ」の道具（囲んで作る・機能の付け替え）が実際のボタンを取りこぼさない
        /// よう、`tap`/`hold` を `bind`/`repeat` より先に見る。
        ///
        /// ⚠️ **`label` を動く物の見分け方に足した（計画の文面に無い追加）**: 表の文面は
        /// 「`bind=`/`repeat=` を持つ、または種類が `pixel`/`icon`/`bar`」だが、名詞の並び
        /// （「字・数・キャラ・アイコン・帯・繰り返し」）には「字」が入っている。実物には
        /// `bind=` を持たない動かない字（`text=` だけの label）が148件中28件（19%）あり、
        /// 見分け方の文面どおりに実装すると、これらだけが「入れ物」層へ落ちてしまう
        /// （字を置く・動かす、という段1の作業そのものが割れる）。名詞の並びに合わせて
        /// `label` を種類の並びへ足した。</summary>
        public static EditLayer Of(LayoutNode node)
        {
            if (node.Kind == "paint") return EditLayer.Paint;
            if (node.Option("tap") != null || node.Option("hold") != null) return EditLayer.Tap;
            if (node.Option("bind") != null || node.Option("repeat") != null
                || node.Kind == "pixel" || node.Kind == "icon" || node.Kind == "bar" || node.Kind == "label")
                return EditLayer.Dynamic;
            return EditLayer.Container;
        }

        /// <summary>JS 側（`edit.js`）と HTML 属性（`data-layer`）で使う短い英字トークン。
        /// ⚠️ null（「すべて」）は空文字 ── `#edstage[data-layerfilter=""]` は「掛けない」の意味。</summary>
        public static string Token(EditLayer? layer) => layer switch
        {
            EditLayer.Paint => "paint",
            EditLayer.Dynamic => "dynamic",
            EditLayer.Tap => "tap",
            EditLayer.Container => "container",
            _ => "",
        };

        /// <summary>日本語ラベル（層の帯の釦・「すべて」も含む）。</summary>
        public static string Label(EditLayer? layer) => layer switch
        {
            EditLayer.Paint => "絵",
            EditLayer.Dynamic => "動く物",
            EditLayer.Tap => "押しどころ",
            EditLayer.Container => "入れ物",
            _ => "すべて",
        };

        /// <summary>層の帯に並べる5つ（「すべて」を先頭に）。⚠️ 唯一の出所 ──
        /// `EditPage.razor` はこれを foreach するだけで釦を作る。</summary>
        public static readonly EditLayer?[] Switcher =
        {
            null, EditLayer.Paint, EditLayer.Dynamic, EditLayer.Tap, EditLayer.Container,
        };

        /// <summary>道具箱「足す」の並び替え用 ── **節点の中身でなく、種類そのものから決まる層**
        /// （まだ作っていない節点には `bind=`/`tap=` が無いので、<see cref="Of"/> は使えない）。
        ///
        /// ⚠️ `button`（押しどころ）はここに出さない（null を返す）── 押しどころの層では
        /// 「囲んで作る」だけが道具になる（計画 §11-6「道具箱から掴んで落とすのは
        /// 絵の上に印を置くには向かない」）。`pixel`/`bar`/`scroll`/`veil`/`host` も
        /// 元から道具箱に無い（`EditPage.AddKindPalette` の除外理由と同じ）。</summary>
        public static EditLayer? PaletteLayerOf(string kind) => kind switch
        {
            "paint" => EditLayer.Paint,
            "label" or "icon" => EditLayer.Dynamic,
            "box" or "card" or "line" or "round" => EditLayer.Container,
            _ => null,
        };
    }
}
