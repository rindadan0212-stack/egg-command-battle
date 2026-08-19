using System;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>一覧の上に置く「絞る・並べる」の1行。⭐ **押すと開く**。
    ///
    /// ⚠️ 札を7枚横に並べていた頃は、それだけで1行を丸ごと使い、
    /// しかも**絞る手段が無かった**（並べ替えは順を変えるだけで数を減らさない）。
    ///
    /// ⭐ 畳んでいるときは「いま何で絞って、何で並べているか」だけを出す。
    /// ⚠️ 開いた状態を覚えない ── 一覧へ戻るたびに開いていると、
    /// 見たいのは一覧なのに毎回畳む操作が要る。
    ///
    /// ⭐ BOX と配合の**両方**が使う（同じ一覧に別の操作を生やさない）。
    /// </summary>
    public static class SortBar
    {
        /// <summary>畳んだときの高さ。⚠️ 指で押せる下限（<see cref="Ui.Tap"/>）を下回らせない。</summary>
        public const float ClosedHeight = Ui.Tap;

        private const float Gap = 12f;
        // ⚠️ **Ui.Tappable は Ui.Tap(112) を下回る高さを勝手に引き上げる。**
        //    72 を渡しても 112 で置かれるので、最初からその値で組む
        //    （合成の札で同じ罠を踏み、タブと案内が重なった）。
        private const float RowH = Ui.Tap;

        /// <summary>いま開いているか。⚠️ 画面を組み直しても覚えておく（選ぶ途中で畳まない）。</summary>
        private static bool _open;

        /// <summary>閉じる。⭐ 画面を離れるときに呼ぶ。</summary>
        public static void Close() => _open = false;

        /// <summary>1行を置いて、使った高さを返す。
        /// ⚠️ 戻り値で次の位置を決めること（開閉で高さが変わる）。</summary>
        public static float Build(RectTransform parent, float left, float top, float width,
            FilterKey filter, SortKey sort, Action<FilterKey> onFilter, Action<SortKey> onSort,
            Action repaint, SortBasis basis = SortBasis.Born, Action<SortBasis> onBasis = null)
        {
            // ── 畳んだ見出し ──
            // ⭐ **ここは状態を出す1行で、主役ではない。**⚠️ 塗ると画面で一番目立ってしまう
            //    （実測レビュー: 「シアンが地の色になってアクセントが機能していない」）。
            var head = Ui.Tappable(parent, "SortBar", "", () => { _open = !_open; repaint(); },
                left, top, width, ClosedHeight);
            var plate = head.GetComponent<Image>();
            if (plate != null) plate.sprite = Ui.SkinSprite("panel");

            // ⭐ **どの数で並べているかまで畳んだ行に出す**（作者の指示 2026-08-19）。
            //    ⚠️ 「素質合計 順」だけだと、育成を含む数なのか生まれつきなのか読めなかった。
            Ui.Label(head.transform, "What",
                $"{Filters.LabelOf(filter)}　／　{Storages.LabelOf(sort)} 順"
                + $"（{Storages.LabelOf(basis)}）",
                28, Ui.Ink, TextAnchor.MiddleLeft, 28f, 0f, width - 100f, ClosedHeight);
            // ⭐ 開くと向きが変わる。⚠️ 字で「開く/閉じる」と書かない（印で足りる）
            Ui.Label(head.transform, "Arrow", _open ? "▲" : "▼",
                30, Ui.InkDim, TextAnchor.MiddleRight, width - 76f, 0f, 48f, ClosedHeight);

            if (!_open) return ClosedHeight;

            // ── 開いた中身 ──
            float y = top + ClosedHeight + Gap;
            y += Row(parent, "絞る", left, y, width, Filters.Keys.Length,
                i => Filters.LabelOf(Filters.Keys[i]),
                i => Filters.Keys[i].Equals(filter),
                i => { onFilter(Filters.Keys[i]); repaint(); }, "F");
            y += Gap;
            y += Row(parent, "並べる", left, y, width, Storages.SortKeys.Length,
                i => Storages.LabelOf(Storages.SortKeys[i]),
                i => Storages.SortKeys[i].Equals(sort),
                i => { onSort(Storages.SortKeys[i]); repaint(); }, "S");

            // ⭐ **何の数で並べるか**（作者の指示 2026-08-19）。
            // ⚠️ 育てた個体は合計で上に来る。生まれつきの良し悪しを見たいときは「素質だけ」
            if (onBasis != null)
            {
                y += Gap;
                y += Row(parent, "何の数で", left, y, width, Storages.Bases.Length,
                    i => Storages.LabelOf(Storages.Bases[i]),
                    i => Storages.Bases[i].Equals(basis),
                    i => { onBasis(Storages.Bases[i]); repaint(); }, "B");
            }
            return y - top;
        }

        /// <summary>見出し＋札の並び。⭐ 4枚ずつ折り返す（横に7枚並べると1枚が細くなりすぎる）。</summary>
        private static float Row(RectTransform parent, string title, float left, float top,
            float width, int count, Func<int, string> label, Func<int, bool> picked,
            Action<int> onTap, string tag)
        {
            Ui.Label(parent, $"{tag} Head", title, 24, Ui.InkDim, TextAnchor.UpperLeft,
                left, top, width, 30f);
            float y = top + 34f;

            const int PerRow = 4;
            float w = (width - Gap * (PerRow - 1)) / PerRow;
            int rows = Mathf.CeilToInt(count / (float)PerRow);
            for (int i = 0; i < count; i++)
            {
                int at = i;
                var b = Ui.Tappable(parent, $"{tag}{i}", label(i), () => onTap(at),
                    left + (w + Gap) * (i % PerRow), y + (RowH + Gap) * (i / PerRow), w, RowH);
                // ⭐ **選んでいるものだけ塗る。**選んでいないものは白い札に字だけ。
                // ⚠️ 全部を青く塗っていた頃は、10枚以上が一面に並んで
                //    「どれが選ばれているか」より先に「青い」しか読めなかった。
                var plate = b.GetComponent<Image>();
                if (plate != null) plate.sprite = Ui.SkinSprite(picked(i) ? "button-lead" : "panel");
                var ink = b.GetComponentInChildren<Text>();
                if (ink != null) ink.color = picked(i) ? Ui.OnLead : Ui.Ink;
            }
            return 34f + rows * RowH + (rows - 1) * Gap;
        }
    }
}
