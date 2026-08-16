using System;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>棚に置いてある卵1個。⭐ 配置は Prefab（EggCard）が持つ。</summary>
    public sealed class EggCard : MonoBehaviour
    {
        [SerializeField] private Image _art;
        [SerializeField] private Text _stars;
        [SerializeField] private Text _wild;
        [SerializeField] private Image _element;
        [SerializeField] private Text _wait;
        [SerializeField] private Button _button;

        /// <summary><paramref name="speed"/> は所要時間の割る数。
        /// ⚠️ ここで割らないと、札の時計と実際の待ち時間が食い違う（画面が嘘をつく）。</summary>
        public void Bind(Egg egg, bool canBegin, int speed, Action onTap)
        {
            if (_art != null)
            {
                _art.sprite = PixelSpriteTexture.ToSprite(EggArt.Sprite, EggArt.Shell);
                _art.preserveAspect = true;
                _art.color = canBegin ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            }
            if (_stars != null) _stars.text = Rarities.StarsOf(egg.Rarity);
            // ⭐ 素質は伏せない。手元にある卵なので、どれを先に温めるかの材料になる
            if (_wild != null) _wild.text = Stats.TotalOf(egg.Wild).ToString();
            if (_wait != null)
            {
                int seconds = Rarities.SecondsOf(egg.Rarity);
                if (speed > 1) seconds = seconds / speed;
                _wait.text = Rarities.Clock(seconds < 1 ? 1 : seconds);
            }
            if (_element != null)
            {
                _element.color = ElementMark.ColorOf(egg.Element);
            }
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.interactable = canBegin;
                if (onTap != null) _button.onClick.AddListener(() => onTap());
            }
        }
    }
}
