using System;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>探索に出ている巣1件の札。⭐ 配置は Prefab（EncounterCard）が持つ。
    ///
    /// ⚠️ 出すのは**絵とレベルだけ**。名前も素質も届く距離も出さない。
    /// 中身が分かると「勝てる相手だけ選ぶ」になり、飛ばして確かめる意味が消える。
    /// </summary>
    public sealed class EncounterCard : MonoBehaviour
    {
        [SerializeField] private Image _art;
        [SerializeField] private Text _level;
        [SerializeField] private Button _button;

        public void Bind(Encounter encounter, Action onTap)
        {
            var species = SpeciesTable.ById(encounter.Nest.SpeciesId);
            if (_art != null)
            {
                _art.sprite = PixelSpriteTexture.ToSprite(species.Sprite, species.Palettes[0]);
                _art.preserveAspect = true;
            }
            if (_level != null) _level.text = encounter.Level.ToString();
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                if (onTap != null) _button.onClick.AddListener(() => onTap());
            }
        }
    }
}
