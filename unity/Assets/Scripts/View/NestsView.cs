using System;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>探索の画面。⭐ 並びは Assets/Resources/Prefabs/NestsScreen.prefab が持つ。
    /// 札は3枚固定。⚠️ 増やすと「全部見てから決める」になり選択が薄まる。</summary>
    public sealed class NestsView : MonoBehaviour
    {
        [SerializeField] private EncounterCard[] _cards;
        [SerializeField] private Image _bossArt;
        [SerializeField] private Button _boss;

        public void Bind(Game game, Action<Encounter> onGo, Action onBoss)
        {
            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] == null) continue;
                bool has = i < game.Encounters.Count;
                _cards[i].gameObject.SetActive(has);
                if (!has) continue;
                var encounter = game.Encounters[i];
                _cards[i].Bind(encounter, () => onGo(encounter));
            }

            if (_bossArt != null)
            {
                var species = SpeciesTable.ById("nushi");
                _bossArt.sprite = PixelSpriteTexture.ToSprite(species.Sprite, species.Palettes[0]);
                _bossArt.preserveAspect = true;
            }
            if (_boss != null)
            {
                _boss.onClick.RemoveAllListeners();
                if (onBoss != null) _boss.onClick.AddListener(() => onBoss());
            }
        }
    }
}
