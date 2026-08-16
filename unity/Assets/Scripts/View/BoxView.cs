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
        [SerializeField] private Image _art;
        [SerializeField] private Image _element;
        [SerializeField] private Text _name;
        [SerializeField] private Text _id;
        [SerializeField] private Text _point;
        [SerializeField] private Text _level;
        [SerializeField] private Text _slant;
        [SerializeField] private StatRow[] _stats;      // 4本。HP/ATK/DEF/SPD の順
        [SerializeField] private Text[] _skills;        // 3枠
        [SerializeField] private Text[] _skillCts;
        [SerializeField] private Button _party;
        [SerializeField] private Text _partyLabel;
        [SerializeField] private Button _release;
        [SerializeField] private Button[] _spend;       // 4本。ステと同じ順
        [SerializeField] private Button[] _sortTabs;    // 7枚
        [SerializeField] private RectTransform _grid;
        [SerializeField] private CreatureCell _cell;

        /// <summary><paramref name="food"/> は餌に選んである個体（無ければ null）。
        /// ⭐ 一覧を押すのは「見る」だけ。餌にするかどうかは詳細の札で決める
        /// （押すたびに意味が変わると、何が起きるか分からない画面になる）。</summary>
        public void Bind(Game game, Creature creature, SortKey sort, IReadOnlyList<Creature> sorted,
            Action<SortKey> onSort, Action<string> onPick, Action onParty, Action onRelease,
            Creature food, Action onMarkFood, Action onFeed)
        {
            bool has = creature != null;
            if (_detail != null) _detail.SetActive(has);

            for (int i = 0; i < _sortTabs.Length && i < Storages.SortKeys.Length; i++)
            {
                var key = Storages.SortKeys[i];
                var tab = _sortTabs[i];
                if (tab == null) continue;
                var label = tab.GetComponentInChildren<Text>();
                if (label != null) label.text = Storages.LabelOf(key);
                // ⚠️ 色を掛けず絵を差し替える（掛けると押せない札と見分けが付かない）
                var plate = tab.GetComponent<Image>();
                if (plate != null) plate.sprite = Ui.SkinSprite(sort == key ? "button-lead" : "button");
                tab.onClick.RemoveAllListeners();
                tab.onClick.AddListener(() => onSort(key));
            }

            // ⭐ 選んでいる個体と、食わせる相手の両方に印を付ける
            CellGrid.Fill(_grid, _cell, sorted,
                id => (creature != null && id == creature.Id) || (food != null && id == food.Id),
                onPick);

            if (!has) return;

            var species = Creatures.SpeciesOf(creature);
            if (_art != null)
            {
                _art.sprite = PixelSpriteTexture.ToSprite(species.Sprite, Creatures.PaletteOf(creature));
                _art.preserveAspect = true;
            }
            if (_element != null) _element.color = ElementMark.ColorOf(species.Element);
            if (_name != null) _name.text = species.Name;
            if (_id != null) _id.text = creature.Id;

            // ⚠️ Lv を主役にしない。ARK と同じで、同じ Lv でも中身はまるで別物。
            //    見るべきは下の4本。ここは添え物として小さく置く
            if (_level != null) _level.text = $"Lv {Levels.Of(creature)} / {Levels.MaxOf(creature)}";
            if (_slant != null)
            {
                _slant.text = creature.Strong == null || creature.Weak == null
                    ? ""
                    : $"▲{Stats.LabelOf(creature.Strong.Value)}  ▼{Stats.LabelOf(creature.Weak.Value)}";
            }
            if (_point != null) _point.gameObject.SetActive(false);

            // ⭐ ステ振りの4枠を「餌にする」「合成」の2枠に置き換える。
            //    Prefab の位置はそのまま使えるので、器を作り直さない
            bool isFood = food != null && food.Id == creature.Id;
            bool canFeed = food != null && !isFood && !Levels.IsMaxed(creature);
            Repurpose(0, isFood ? "餌を外す" : "餌にする", true, isFood, onMarkFood);
            // ⚠️ 「個体を選んでください」と書かない。⭐ 伸びる数だけ出す
            Repurpose(1, canFeed ? $"合成 ＋{Levels.FeedValueOf(food)}" : "合成", canFeed, false, onFeed);
            for (int i = 2; i < _spend.Length; i++)
            {
                if (_spend[i] != null) _spend[i].gameObject.SetActive(false);
            }

            var stats = Creatures.StatsOf(creature);
            for (int i = 0; i < _stats.Length && i < Stats.Keys.Length; i++)
            {
                var key = Stats.Keys[i];
                var row = _stats[i];
                if (row == null) continue;
                if (row.Label != null) row.Label.text = Stats.LabelOf(key);
                if (row.Value != null)
                {
                    // ⭐ この画面の主役。素質＋育てた分の内訳を出す
                    int trained = creature.Trained[key];
                    row.Value.text = trained > 0
                        ? $"{stats[key]}  ({creature.Wild[key]}+{trained})"
                        : $"{stats[key]}  ({creature.Wild[key]})";
                    row.Value.color = key == creature.Strong ? Ui.Good
                        : key == creature.Weak ? Ui.Danger : Ui.Ink;
                }
                if (row.Bar != null) row.Bar.fillAmount = Mathf.Clamp01(creature.Wild[key] / 60f);
            }

            var skills = Creatures.SkillsOf(creature);
            for (int i = 0; i < _skills.Length; i++)
            {
                var skill = i < skills.Length ? skills[i] : null;
                if (_skills[i] != null)
                {
                    _skills[i].text = skill == null ? "—" : skill.Name;
                    _skills[i].color = skill == null ? Ui.InkFaint : Ui.Ink;
                }
                if (i < _skillCts.Length && _skillCts[i] != null)
                {
                    _skillCts[i].text = skill != null && i > 0 ? skill.Ct.ToString() : "";
                }
            }

            bool inParty = Games.IsInParty(game, creature.Id);
            if (_partyLabel != null) _partyLabel.text = inParty ? "出撃中" : "出撃";
            if (_party != null)
            {
                var plate = _party.GetComponent<Image>();
                if (plate != null) plate.sprite = Ui.SkinSprite(inParty ? "button-lead" : "button");
                _party.onClick.RemoveAllListeners();
                _party.onClick.AddListener(() => onParty());
            }
            if (_release != null)
            {
                _release.onClick.RemoveAllListeners();
                _release.onClick.AddListener(() => onRelease());
            }

        }

        /// <summary>ステ振りだった枠を別の押しどころに使い回す。
        /// ⚠️ ステ振りは外した — 上限も対価も無い ＋1 は選択になっていなかった。
        /// 育てた分は得意の方向へ自動で乗る（<see cref="Creatures.Grow"/>）。</summary>
        private void Repurpose(int index, string label, bool usable, bool lead, Action onTap)
        {
            if (index >= _spend.Length || _spend[index] == null) return;
            var button = _spend[index];
            button.gameObject.SetActive(true);
            var text = button.GetComponentInChildren<Text>();
            if (text != null) text.text = label;
            var plate = button.GetComponent<Image>();
            // ⚠️ 色を掛けず絵を差し替える（掛けると押せない札と見分けが付かない）
            if (plate != null) plate.sprite = Ui.SkinSprite(lead ? "button-lead" : "button");
            button.interactable = usable;
            button.onClick.RemoveAllListeners();
            if (usable && onTap != null) button.onClick.AddListener(() => onTap());
        }
    }
}
