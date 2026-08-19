using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>保管庫。⭐ 並びは Assets/Resources/Prefabs/BoxScreen.prefab が持つ。</summary>
    public sealed class BoxView : MonoBehaviour
    {
        [SerializeField] private GameObject _detail;
        /// <summary>1体を見せる札。⭐ 配合の親札と**同じ部品**（並びも同じ）。</summary>
        [SerializeField] private CreaturePanel _panel;
        [SerializeField] private Button _party;
        [SerializeField] private Text _partyLabel;
        [SerializeField] private Button _release;
        [SerializeField] private Button[] _spend;       // 4枚。⚠️ ステとは無関係（Repurpose が札にする）
        [SerializeField] private Button[] _sortTabs;    // 7枚
        [SerializeField] private RectTransform _grid;
        [SerializeField] private CreatureCell _cell;

        /// <summary><paramref name="food"/> は餌に選んである個体（無ければ null）。
        /// ⭐ 一覧を押すのは「見る」だけ。餌にするかどうかは詳細の札で決める
        /// （押すたびに意味が変わると、何が起きるか分からない画面になる）。</summary>
        /// <param name="onTrain">技を鍛える札を押した。⭐ 卵を素材にする画面を開く。</param>
        /// <summary>一覧の元の位置と高さ。
        /// ⚠️ **覚えておかないと累積する** ── 開閉のたびに引き算して
        /// いくと、何度か開いただけで一覧が画面外へ落ちる。</summary>
        private float _gridTop;
        private float _gridHeight;
        private bool _gridSaved;

        /// <summary>出ている押しどころを、器の幅いっぱいに並べ直す。
        ///
        /// ⚠️ 位置を書くのは本来 Prefab の仕事だが、**何枚出るかは中身しだい**なので
        /// ここでしか決められない（出撃はパーティ編成へ、逃がすは合成へ移して2枚になった）。
        /// ⭐ 器の左右の余白と隙間は Prefab の1枚目・最後の1枚から読む（数を書かない）。</summary>
        private void Spread()
        {
            var shown = new List<RectTransform>();
            foreach (var button in Buttons())
            {
                if (button != null && button.gameObject.activeSelf)
                {
                    shown.Add((RectTransform)button.transform);
                }
            }
            if (shown.Count == 0) return;
            var box = shown[0].parent as RectTransform;
            if (box == null) return;

            // ⚠️ 余白は**1枚目の左**から読む。Prefab で動かしたらそれに従う
            float pad = shown[0].anchoredPosition.x;
            float gap = 12f;
            float room = box.rect.width - pad * 2f;
            float width = (room - gap * (shown.Count - 1)) / shown.Count;
            for (int i = 0; i < shown.Count; i++)
            {
                var rect = shown[i];
                rect.anchoredPosition = new Vector2(pad + (width + gap) * i, rect.anchoredPosition.y);
                rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
                // ⚠️ 中の字も一緒に伸ばす（伸ばさないと中央揃えが崩れる）
                foreach (RectTransform child in rect)
                {
                    child.sizeDelta = new Vector2(width, child.sizeDelta.y);
                    child.anchoredPosition = new Vector2(0f, child.anchoredPosition.y);
                }
            }
        }

        /// <summary>並び順そのままの押しどころ。⚠️ Prefab の並びと同じ順で返す。</summary>
        private IEnumerable<Button> Buttons()
        {
            yield return _party;
            if (_spend != null)
            {
                foreach (var button in _spend) yield return button;
            }
            yield return _release;
        }

        public void Bind(Game game, Creature creature, SortKey sort, IReadOnlyList<Creature> sorted,
            Action<SortKey> onSort, Action<string> onPick, Action onFuse, Action onGrow,
            SortBasis basis, Action<SortBasis> onBasis,
            Action<FilterKey> onFilter, FilterKey filter, Action repaint)
        {
            bool has = creature != null;
            if (_detail != null) _detail.SetActive(has);

            // ⚠️ **動かすのは器のほう。**_grid は中身（Content）で、
            //    こちらを動かしてもスクロール枠は動かない（実測で気づいた）。
            var box = _grid == null ? null : _grid.parent as RectTransform;
            if (!_gridSaved && box != null)
            {
                _gridTop = box.anchoredPosition.y;
                _gridHeight = box.sizeDelta.y;
                _gridSaved = true;
            }
            // ⚠️ **並べ替えの札7枚は隠す。**横に並べるだけで1行を使い切り、
            //    しかも**絞る手段が無かった**。⭐ 代わりに▼で開く1行を置く。
            //    ⚠️ 部品は消さずに隠す（Prefab を作り直さずに戻せる）。
            foreach (var tab in _sortTabs)
            {
                if (tab != null) tab.gameObject.SetActive(false);
            }
            if (_sortTabs.Length > 0 && _sortTabs[0] != null)
            {
                var at = (RectTransform)_sortTabs[0].transform;
                float used = SortBar.Build((RectTransform)at.parent, at.anchoredPosition.x,
                    -at.anchoredPosition.y, Ui.W - Ui.Margin * 2f,
                    filter, sort, onFilter, onSort, repaint, basis, onBasis);
                // ⚠️ **開いたぶんだけ一覧を下げる。**下げないと、
                //    開いた札の下に升が潜り込んで押せなくなる。
                if (box != null)
                {
                    float extra = used - SortBar.ClosedHeight;
                    box.anchoredPosition = new Vector2(box.anchoredPosition.x, _gridTop - extra);
                    box.sizeDelta = new Vector2(box.sizeDelta.x, _gridHeight - extra);
                }
            }

            // ⭐ 印を付けるのは「いま見ている個体」だけ。
            //    ⚠️ 餌の印はここから消えた（餌は合成の画面の中で選ぶようになったため）。
            CellGrid.Fill(_grid, _cell, sorted,
                id => creature != null && id == creature.Id,
                onPick);

            if (!has) return;

            // ⭐ 絵・見出し・ステ表・特性・技はすべて札が持つ。ここは押しどころだけ見る
            if (_panel != null) _panel.Bind(creature);

            // ⭐ **押しどころは2つだけ。**（2026-08-18・作者判断）
            //    ⚠️ 以前は 出撃／餌にする／合成／そだてる／技を鍛える／逃がす の6つが並び、
            //    どれを押せば何が起きるのか読めなかった。
            //    ⭐ 「分解」（個体を EXP に還す・たまごで技を鍛える）と、
            //    EXP で1レベル上げる「レベルアップ」の2つ。
            //    ⚠️ 出撃はパーティ編成へ、逃がすは分解へ移した。
            // ⚠️ **主役は1つ。**両方を塗っていた頃は、どちらを押せばいいのか読めなかった。
            //    分解は白い札（枠だけ）、レベルアップだけ塗る。
            // ⚠️ **分解はいつでも押せる。**⭐ 上限に達した個体でも EXP には還せる
            //    （2026-08-19 に合成から置き換えたので、上限は入口の条件でなくなった）。
            Repurpose(0, "分解", true, false, onFuse);

            // ⭐ 放置で溜めた EXP で育てる。1回で1レベル
            // ⚠️ **値段はその個体のいまの Lv で変わる**（作者の指示 2026-08-19）。
            //    一律の定数を出していた頃は、上げるほど重くなることが画面から読めなかった。
            int cost = Levels.ExpToNext(creature);
            bool canGrow = !Levels.IsMaxed(creature) && game.Idle.Exp >= cost;
            // ⚠️ 「EXP が足りません」と書かない。⭐ 要る数を出せば足りる
            // ⭐ **何レベル上がるかを出す**（作者の指示 2026-08-19）。
            Repurpose(1, $"レベルアップ ＋1  EXP {Ui.Digits(cost)}", canGrow, canGrow, onGrow);

            for (int i = 2; i < _spend.Length; i++)
            {
                if (_spend[i] != null) _spend[i].gameObject.SetActive(false);
            }

            // ⚠️ 出撃は「パーティ編成」へ、逃がすは合成へ移したので、この画面には出さない。
            //    ⭐ 部品は消さずに隠す（Prefab を作り直さずに戻せる）。
            if (_party != null) _party.gameObject.SetActive(false);
            if (_release != null) _release.gameObject.SetActive(false);

            // ⭐ **出ている押しどころだけで、幅いっぱいに並べ直す。**
            // ⚠️ **どれを出すか決めたあとで呼ぶ。**先に呼ぶと、隠す前の6枚で割ってしまう。
            // ⚠️ 器（Prefab）は6枚ぶんの幅で割ってあるので、2枚に減らした日から
            //    右半分が丸ごと空いていた（実測: 486〜958 が空白）。
            Spread();
        }

        /// <summary>まだ鍛えられる枠が1つでもあるか。⚠️ 全部上限なら入口を閉じる。</summary>
        private static bool HasRoom(Creature creature)
        {
            var skills = Creatures.SkillsOf(creature);
            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i] != null && !SkillCosts.IsMaxed(creature.SkillPoints[i])) return true;
            }
            return false;
        }

        /// <summary>ステ振りだった枠を別の押しどころに使い回す。
        /// ⚠️ ステ振りは外した — 上限も対価も無い ＋1 は選択になっていなかった。
        /// 育てた分は得意の方向へ自動で乗る（<see cref="Creatures.Grow"/>）。</summary>
        private void Repurpose(int index, string labelText, bool usable, bool lead, Action onTap)
        {
            if (index >= _spend.Length || _spend[index] == null) return;
            var button = _spend[index];
            button.gameObject.SetActive(true);
            var text = button.GetComponentInChildren<Text>();
            if (text != null) text.text = labelText;
            var plate = button.GetComponent<Image>();
            // ⚠️ 色を掛けず絵を差し替える（掛けると押せない札と見分けが付かない）
            // ⚠️ **押せない札は灰（button-off）。**ここで button のままにしていたので、
            //    餌を選んでいないのに「合成」が押せるように見えていた（Ui.Tappable と食い違い）
            if (plate != null)
                plate.sprite = Ui.SkinSprite(!usable ? "button-off" : lead ? "button-lead" : "panel");
            button.interactable = usable;
            var label = button.transform.Find("Label");
            if (label != null)
            {
                var ink = label.GetComponent<Text>();
                if (ink != null) ink.color = usable ? Ui.Ink : Ui.InkFaint;
            }
            button.onClick.RemoveAllListeners();
            if (usable && onTap != null) button.onClick.AddListener(() => onTap());
        }
    }
}
