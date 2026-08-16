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

        public void Bind(Game game, Creature creature, SortKey sort, IReadOnlyList<Creature> sorted,
            Action<SortKey> onSort, Action<string> onPick, Action onParty, Action onRelease,
            Action<StatKey> onSpend)
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

            CellGrid.Fill(_grid, _cell, sorted,
                id => creature != null && id == creature.Id, onPick);

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

            int unspent = Creatures.UnspentOf(creature);
            if (_point != null)
            {
                _point.text = "＋" + unspent;
                _point.gameObject.SetActive(unspent > 0);
            }

            var stats = Creatures.StatsOf(creature);
            for (int i = 0; i < _stats.Length && i < Stats.Keys.Length; i++)
            {
                var key = Stats.Keys[i];
                var row = _stats[i];
                if (row == null) continue;
                if (row.Label != null) row.Label.text = Stats.LabelOf(key);
                if (row.Value != null) row.Value.text = stats[key].ToString();
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

            for (int i = 0; i < _spend.Length && i < Stats.Keys.Length; i++)
            {
                var key = Stats.Keys[i];
                if (_spend[i] == null) continue;
                var label = _spend[i].GetComponentInChildren<Text>();
                if (label != null) label.text = $"{Stats.LabelOf(key)}＋";
                _spend[i].interactable = unspent > 0;
                _spend[i].onClick.RemoveAllListeners();
                _spend[i].onClick.AddListener(() => onSpend(key));
            }
        }
    }
}
