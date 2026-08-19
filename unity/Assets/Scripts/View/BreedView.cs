using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>配合。⭐ 並びは Assets/Resources/Prefabs/BreedScreen.prefab が持つ。</summary>
    public sealed class BreedView : MonoBehaviour
    {
        [SerializeField] private ParentSlot[] _parents;   // 2枠
        [SerializeField] private GameObject _result;
        [SerializeField] private Image _resultEgg;
        [SerializeField] private Text _resultSpecies;
        [SerializeField] private Text _resultSkills;
        [SerializeField] private GameObject _resultMutable;
        [SerializeField] private Button _breed;
        [SerializeField] private RectTransform _grid;
        [SerializeField] private CreatureCell _cell;

        public void Bind(IReadOnlyList<Creature> all, Creature a, Creature b,
            Action onBreed, Action<string> onPick,
            FilterKey filter, SortKey sort, Action<FilterKey> onFilter, Action<SortKey> onSort,
            Action repaint)
        {
            var chosen = new[] { a, b };
            for (int i = 0; i < _parents.Length && i < chosen.Length; i++)
            {
                var slot = _parents[i];
                var creature = chosen[i];
                if (slot == null) continue;
                if (slot.Filled != null) slot.Filled.SetActive(creature != null);
                if (slot.Empty != null) slot.Empty.SetActive(creature == null);
                if (creature == null) continue;
                // ⭐ BOX の詳細とまったく同じ札。⚠️ ここで欄を選び直さない
                //    （選び直した結果、特性の働きが配合だけ出ていなかった）
                if (slot.Panel != null) slot.Panel.Bind(creature);
            }

            bool ready = a != null && b != null && Fusion.CanFuse(a, b);
            // ⚠️ **卵の予告は出さない**（2026-08-18・作者判断）。
            //    ⭐ 親2枚を見比べる画面なのに、真ん中に3枚目の札が入って主役が割れていた。
            //    推定レベルも★も、押したあとに卵として手に入るので先に言う必要がない。
            if (_result != null) _result.SetActive(false);

            if (_breed != null)
            {
                _breed.interactable = ready;
                // ⚠️ **押せないのに主導線の色（黄）のままだった。**
                //    2体そろっていないのに「配合する」が押せるように見えていた
                var plate = _breed.GetComponent<Image>();
                if (plate != null) plate.sprite = Ui.SkinSprite(ready ? "button-lead" : "button-off");
                var ink = _breed.GetComponentInChildren<Text>();
                if (ink != null) ink.color = ready ? Ui.OnLead : Ui.InkFaint;
                _breed.onClick.RemoveAllListeners();
                if (ready) _breed.onClick.AddListener(() => onBreed());
            }

            // ⭐ **BOX と同じ部品を使う。**同じ一覧に別の操作を生やさない。
            //
            // ⚠️ **器（Stack）は VerticalLayoutGroup で自動整列している。**
            //    直接ぶら下げると、SortBar が自分で置いた札の位置まで
            //    レイアウトに上書きされ、格子が崩れる（実測で
            //    並べ替えの札が画面外 2168 へ落ちた）。
            //    ⭐ **入れ物を1つ挟む** ── 入れ物はレイアウトが並べ、
            //    その中は SortBar が自由に置ける。
            var box = _grid == null ? null : _grid.parent as RectTransform;
            if (box != null && box.parent != null)
            {
                var stack = (RectTransform)box.parent;
                var host = Ui.Rect("Sort Host", stack);
                // ⚠️ 一覧の**すぐ上**へ。作っただけだと末尾に並ぶ
                host.SetSiblingIndex(box.GetSiblingIndex());
                float used = SortBar.Build(host, 0f, 0f, Ui.W - Ui.Margin * 2f,
                    filter, sort, onFilter, onSort, repaint);
                // ⭐ **入れ物の高さを直に設定する。**
                // ⚠️ LayoutElement では効かない ── この Stack は childControlHeight が
                //    false なので、レイアウトは LayoutElement を見ず、
                //    部品自身の高さ（sizeDelta）を使う（実測で高さ100のままだった）。
                host.sizeDelta = new Vector2(Ui.W - Ui.Margin * 2f, used);
            }

            CellGrid.Fill(_grid, _cell, all,
                id => (a != null && id == a.Id) || (b != null && id == b.Id), onPick);
        }
    }
}
