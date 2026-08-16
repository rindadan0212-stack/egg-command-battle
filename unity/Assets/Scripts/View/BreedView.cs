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
            Action onBreed, Action<string> onPick)
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

                var species = Creatures.SpeciesOf(creature);
                if (slot.Art != null)
                {
                    slot.Art.sprite = PixelSpriteTexture.ToSprite(species.Sprite, Creatures.PaletteOf(creature));
                    slot.Art.preserveAspect = true;
                }
                if (slot.Element != null) slot.Element.color = ElementMark.ColorOf(species.Element);
                if (slot.Name != null) slot.Name.text = species.Name;
                if (slot.Wild != null)
                {
                    slot.Wild.text = $"Lv {Levels.Of(creature)} / {Levels.MaxOf(creature)}";
                }

                // ⭐ 実値4本。得意は緑、不得意は赤（BOX と同じ読み方にする）
                var stats = Creatures.StatsOf(creature);
                for (int k = 0; slot.Stats != null && k < slot.Stats.Length && k < Stats.Keys.Length; k++)
                {
                    var key = Stats.Keys[k];
                    if (slot.Stats[k] == null) continue;
                    slot.Stats[k].text = $"{Stats.LabelOf(key)} {stats[key]}";
                    slot.Stats[k].color = key == creature.Strong ? Ui.Good
                        : key == creature.Weak ? Ui.Danger : Ui.Ink;
                }

                if (slot.Skills != null)
                {
                    var names = new List<string>();
                    foreach (var skill in Creatures.SkillsOf(creature))
                    {
                        if (skill != null) names.Add(skill.Name);
                    }
                    slot.Skills.text = string.Join("・", names);
                }
            }

            bool ready = a != null && b != null && Fusion.CanFuse(a, b);
            if (_result != null) _result.SetActive(ready);
            if (ready)
            {
                if (_resultEgg != null)
                {
                    _resultEgg.sprite = PixelSpriteTexture.ToSprite(EggArt.Sprite, EggArt.Shell);
                    _resultEgg.preserveAspect = true;
                }
                // ⭐ 卵に出すのは**推定レベルと希少さだけ**。
                // ⚠️ 種族も技の候補も出さない。まだ決まっていないものを見せると、
                //    出た結果が「約束と違う」に見える。孵してからのお楽しみにする。
                // ⚠️ 「先に育ててください」と字で書かない。数が言えば足りる
                if (_resultSpecies != null)
                {
                    _resultSpecies.text = $"Lv {Fusion.PreviewBirthLevel(a, b)}";
                }
                if (_resultSkills != null)
                {
                    _resultSkills.text = Rarities.StarsOf(Fusion.PreviewRarity(a, b));
                    _resultSkills.color = Ui.Accent;
                }
                // ⚠️ 変異の印は外した（希少さの★と役割がぶつかる）
                if (_resultMutable != null) _resultMutable.SetActive(false);
            }

            if (_breed != null)
            {
                _breed.interactable = ready;
                _breed.onClick.RemoveAllListeners();
                if (ready) _breed.onClick.AddListener(() => onBreed());
            }

            CellGrid.Fill(_grid, _cell, all,
                id => (a != null && id == a.Id) || (b != null && id == b.Id), onPick);
        }
    }
}
