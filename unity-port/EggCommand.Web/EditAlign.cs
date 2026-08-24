using System;
using System.Collections.Generic;
using System.Linq;

namespace EggCommand.Web
{
    /// <summary>骨組みエディタ 段階2 Pass A ── 「揃える・等間隔」の純関数（`wiki/開発/web移行計画.md`
    /// §11-3・作者承認済み）。
    ///
    /// ⭐ **Core 非依存・純粋関数。**入力は選択中の節点の矩形（行番号・左上幅高）だけ、
    /// 出力は「変わる節点だけ」の新しい左上。⚠️ `LayoutNode` そのものは受け取らない ──
    /// `EditPage.razor` 側が `Find` で拾った矩形をタプルに詰めて渡し、返ってきた新座標を
    /// 既存の「作り直して Left/Top だけ差し替える」経路（`ReplaceLines`）へ渡す。
    /// この関数自身は木も往復のバイト忠実も一切知らない。
    ///
    /// ⚠️ **`EggCommand.Tests` へは `EditAttrs.cs` と同じ扱い**（`ProjectReference` でなく
    /// `&lt;Compile Include&gt;` で直接コンパイル ── `dotnet test` が Web（Blazor WASM）を
    /// 建てないという既存の約束を守るため）。この形を選べるのも、この型が Core は
    /// もちろん `EggCommand.Web` の他の型（`IconManifest` 等）にも一切依存しないから。</summary>
    public static class EditAlign
    {
        public static Dictionary<int, (float Left, float Top)> AlignLeft(
            IReadOnlyList<(int Line, float Left, float Top, float Width, float Height)> nodes)
        {
            var result = new Dictionary<int, (float Left, float Top)>();
            if (nodes.Count == 0) return result;
            float min = nodes.Min(n => n.Left);
            foreach (var n in nodes)
                if (n.Left != min) result[n.Line] = (min, n.Top);
            return result;
        }

        public static Dictionary<int, (float Left, float Top)> AlignRight(
            IReadOnlyList<(int Line, float Left, float Top, float Width, float Height)> nodes)
        {
            var result = new Dictionary<int, (float Left, float Top)>();
            if (nodes.Count == 0) return result;
            float maxRight = nodes.Max(n => n.Left + n.Width);
            foreach (var n in nodes)
            {
                float left = maxRight - n.Width;
                if (left != n.Left) result[n.Line] = (left, n.Top);
            }
            return result;
        }

        public static Dictionary<int, (float Left, float Top)> AlignTop(
            IReadOnlyList<(int Line, float Left, float Top, float Width, float Height)> nodes)
        {
            var result = new Dictionary<int, (float Left, float Top)>();
            if (nodes.Count == 0) return result;
            float min = nodes.Min(n => n.Top);
            foreach (var n in nodes)
                if (n.Top != min) result[n.Line] = (n.Left, min);
            return result;
        }

        public static Dictionary<int, (float Left, float Top)> AlignBottom(
            IReadOnlyList<(int Line, float Left, float Top, float Width, float Height)> nodes)
        {
            var result = new Dictionary<int, (float Left, float Top)>();
            if (nodes.Count == 0) return result;
            float maxBottom = nodes.Max(n => n.Top + n.Height);
            foreach (var n in nodes)
            {
                float top = maxBottom - n.Height;
                if (top != n.Top) result[n.Line] = (n.Left, top);
            }
            return result;
        }

        public static Dictionary<int, (float Left, float Top)> AlignCenterX(
            IReadOnlyList<(int Line, float Left, float Top, float Width, float Height)> nodes)
        {
            var result = new Dictionary<int, (float Left, float Top)>();
            if (nodes.Count == 0) return result;
            float minLeft = nodes.Min(n => n.Left);
            float maxRight = nodes.Max(n => n.Left + n.Width);
            float centerX = (minLeft + maxRight) / 2f;
            foreach (var n in nodes)
            {
                float left = centerX - n.Width / 2f;
                if (left != n.Left) result[n.Line] = (left, n.Top);
            }
            return result;
        }

        public static Dictionary<int, (float Left, float Top)> AlignCenterY(
            IReadOnlyList<(int Line, float Left, float Top, float Width, float Height)> nodes)
        {
            var result = new Dictionary<int, (float Left, float Top)>();
            if (nodes.Count == 0) return result;
            float minTop = nodes.Min(n => n.Top);
            float maxBottom = nodes.Max(n => n.Top + n.Height);
            float centerY = (minTop + maxBottom) / 2f;
            foreach (var n in nodes)
            {
                float top = centerY - n.Height / 2f;
                if (top != n.Top) result[n.Line] = (n.Left, top);
            }
            return result;
        }

        /// <summary>横に等間隔。⭐ 両端（最小Left・最大Right）を固定し、Left の昇順に
        /// 間を詰め直す（`gap = (span - Σwidth) / (n-1)`）。⚠️ 3個未満は「間」が定義できない
        /// （2個なら両端そのもの・1個以下は論外）ので、何もしない（空を返す）。</summary>
        public static Dictionary<int, (float Left, float Top)> DistributeH(
            IReadOnlyList<(int Line, float Left, float Top, float Width, float Height)> nodes)
        {
            var result = new Dictionary<int, (float Left, float Top)>();
            if (nodes.Count < 3) return result;
            var sorted = nodes.OrderBy(n => n.Left).ToList();
            float minLeft = sorted.Min(n => n.Left);
            float maxRight = sorted.Max(n => n.Left + n.Width);
            float span = maxRight - minLeft;
            float totalWidth = sorted.Sum(n => n.Width);
            float gap = (span - totalWidth) / (sorted.Count - 1);

            float x = minLeft;
            foreach (var n in sorted)
            {
                if (x != n.Left) result[n.Line] = (x, n.Top);
                x += n.Width + gap;
            }
            return result;
        }

        /// <summary>縦に等間隔。⚠️ <see cref="DistributeH"/> と同じ規則を Top/Height で。</summary>
        public static Dictionary<int, (float Left, float Top)> DistributeV(
            IReadOnlyList<(int Line, float Left, float Top, float Width, float Height)> nodes)
        {
            var result = new Dictionary<int, (float Left, float Top)>();
            if (nodes.Count < 3) return result;
            var sorted = nodes.OrderBy(n => n.Top).ToList();
            float minTop = sorted.Min(n => n.Top);
            float maxBottom = sorted.Max(n => n.Top + n.Height);
            float span = maxBottom - minTop;
            float totalHeight = sorted.Sum(n => n.Height);
            float gap = (span - totalHeight) / (sorted.Count - 1);

            float y = minTop;
            foreach (var n in sorted)
            {
                if (y != n.Top) result[n.Line] = (n.Left, y);
                y += n.Height + gap;
            }
            return result;
        }
    }
}
