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
                if (slot.Wild != null) slot.Wild.text = Creatures.WildTotalOf(creature).ToString();
            }

            bool ready = a != null && b != null && Fusion.CanFuse(a, b);
            if (_result != null) _result.SetActive(ready);
            if (ready)
            {
                List<string> speciesNames, skillPool;
                bool mutable;
                Breeding.PreviewOf(a, b, out speciesNames, out skillPool, out mutable);
                if (_resultEgg != null)
                {
                    _resultEgg.sprite = PixelSpriteTexture.ToSprite(EggArt.Sprite, EggArt.Shell);
                    _resultEgg.preserveAspect = true;
                }
                // ⭐ 生まれる子の Lv を**先に**見せる。育てていない2体を並べたら小さい数が出る。
                // ⚠️ 「先に育ててください」と字で書かない。数が言えば足りる
                if (_resultSpecies != null)
                {
                    _resultSpecies.text =
                        $"Lv {Fusion.PreviewBirthLevel(a, b)}　{string.Join(" / ", speciesNames)}";
                }
                if (_resultSkills != null) _resultSkills.text = string.Join("・", skillPool);
                // ⭐ 変異が出うるかは印1つ。⚠️ 確率を字で説明しない
                if (_resultMutable != null) _resultMutable.SetActive(mutable);
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
