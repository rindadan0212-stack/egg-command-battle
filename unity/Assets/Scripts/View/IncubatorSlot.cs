using System;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>孵化器の1枠。⭐ 配置は Prefab（IncubatorSlot）が持つ。</summary>
    public sealed class IncubatorSlot : MonoBehaviour
    {
        [SerializeField] private GameObject _filled;   // 卵が入っているときだけ出す一式
        [SerializeField] private GameObject _empty;    // 空の台座
        [SerializeField] private Image _art;
        [SerializeField] private Text _stars;
        [SerializeField] private Image _fill;          // 残り時間の帯
        [SerializeField] private Text _clock;
        [SerializeField] private GameObject _ready;    // 孵る合図
        [SerializeField] private Button _button;

        private float _fullWidth = -1f;
        private Incubation _slot;
        private Func<long> _clockSource;

        public void Bind(Incubation slot, long nowUnix, Func<long> clockSource, Action onTap)
        {
            if (_fullWidth < 0f && _fill != null) _fullWidth = _fill.rectTransform.sizeDelta.x;
            _slot = slot;
            _clockSource = clockSource;

            if (_filled != null) _filled.SetActive(slot != null);
            if (_empty != null) _empty.SetActive(slot == null);

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.interactable = onTap != null;
                if (onTap != null) _button.onClick.AddListener(() => onTap());
            }

            if (slot == null) return;

            if (_art != null)
            {
                _art.sprite = PixelSpriteTexture.ToSprite(EggArt.Sprite, EggArt.Shell);
                _art.preserveAspect = true;
            }
            if (_stars != null) _stars.text = Rarities.StarsOf(slot.Egg.Rarity);
            Retime(nowUnix);
        }

        /// <summary>時計だけ進める。⚠️ 毎フレーム画面を組み直さない（作り直すと押しどころが飛ぶ）。</summary>
        public void Retime(long nowUnix)
        {
            if (_slot == null) return;
            bool ready = Hatchery.IsReady(_slot, nowUnix);

            if (_fill != null)
            {
                var size = _fill.rectTransform.sizeDelta;
                size.x = Mathf.Max(0f, _fullWidth) * (float)Hatchery.ProgressOf(_slot, nowUnix);
                _fill.rectTransform.sizeDelta = size;
                _fill.color = ready ? Ui.Good : Ui.Accent;
            }
            if (_clock != null)
            {
                _clock.text = ready ? "" : Rarities.Clock(Hatchery.LeftOf(_slot, nowUnix));
                _clock.gameObject.SetActive(!ready);
            }
            if (_ready != null) _ready.SetActive(ready);
        }

        private void Update()
        {
            if (_slot != null && _clockSource != null) Retime(_clockSource());
        }
    }
}
