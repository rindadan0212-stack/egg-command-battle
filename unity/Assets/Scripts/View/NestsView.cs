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

        /// <param name="app">⭐ 時計の出所。⚠️ 札は残り時間を秒ごとに描き直すので要る。</param>
        public void Bind(App app, Action<Encounter> onGo, Action onBoss)
        {
            var game = app.Game;
            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] == null) continue;
                bool has = i < game.Encounters.Count;
                _cards[i].gameObject.SetActive(has);
                if (!has) continue;
                var encounter = game.Encounters[i];
                // ⭐ 居座る時間が切れたら組み直す（Expire → Refill で次の巣が出る）
                _cards[i].Bind(encounter, () => onGo(encounter), app.Now, () => app.Refresh(),
                    Games.RaidsOn(game, encounter.Nest));
            }

            if (_bossArt != null)
            {
                var species = SpeciesTable.ById("nushi");
                _bossArt.sprite = PixelSpriteTexture.ToSprite(species.Sprite, species.Palettes[0]);
                _bossArt.preserveAspect = true;
                Ui.Face(_bossArt.rectTransform, true);
            }
            if (_boss != null)
            {
                _boss.onClick.RemoveAllListeners();
                if (onBoss != null) _boss.onClick.AddListener(() => onBoss());
            }
        }
    }
}
