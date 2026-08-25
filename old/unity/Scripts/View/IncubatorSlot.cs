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

        /// <summary>種族の名前。⭐ **器（Prefab）には無いので、ここで足す。**
        /// ⚠️ 温めている間も、卵の絵はどれも同じ ── ★と時計は「どれくらい」しか
        /// 言わないので、**何が孵るのか**を言う字が1つも無かった（2026-08-22）。</summary>
        private Text _who;

        private float _fullWidth = -1f;
        private Incubation _slot;
        private Func<long> _clockSource;

        /// <summary>孵ったときの押しどころ。⚠️ **Bind のあとに孵る**ことがあるので覚えておく。</summary>
        private Action _onCollect;
        private bool _wasReady;

        public void Bind(Incubation slot, long nowUnix, Func<long> clockSource, Action onTap) =>
            Bind(slot, nowUnix, clockSource, onTap, null);

        /// <param name="onCollect">孵ったときに押せるようにする手。
        /// ⚠️ **Bind の時点で孵っていなくても渡すこと。**
        /// ⭐ 前は Bind した瞬間の状態で押しどころを決めていたので、
        /// ホームを開いたまま時間が 0 になっても押せず、
        /// 一度ほかの画面を挟まないと孵せなかった（作者の指摘 2026-08-19）。</param>
        public void Bind(Incubation slot, long nowUnix, Func<long> clockSource, Action onTap,
            Action onCollect)
        {
            _onCollect = onCollect;
            _wasReady = slot != null && Hatchery.IsReady(slot, nowUnix);
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

            // ⭐ **何を温めているか。**⚠️ 器の一番下の空きに置く（実測: 380 のうち
            //    時計が 356 で終わる）── 上へ割り込ませると帯と時計が動く。
            if (_who == null && _filled != null)
            {
                var frame = (RectTransform)transform;
                _who = Ui.Label(_filled.transform, "Who", "", 22, Ui.InkDim,
                    TextAnchor.MiddleCenter, 0f, 354f, frame.rect.width, 26f);
            }
            if (_who != null) _who.text = SpeciesTable.ById(slot.Egg.SpeciesId).Name;

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
                // ⭐ 孵ったら「孵った」と出す。⚠️ 帯の色（橙→緑）だけでは、
                //    取り出せるようになったことに気づけなかった
                _clock.text = ready ? "孵った" : Rarities.Clock(Hatchery.LeftOf(_slot, nowUnix));
                _clock.color = ready ? Ui.GoodInk : Ui.Ink;
                _clock.gameObject.SetActive(true);
            }
            if (_ready != null) _ready.SetActive(ready);
        }

        private void Update()
        {
            if (_slot == null || _clockSource == null) return;
            long now = _clockSource();
            Retime(now);

            // ⭐ **見ている最中に孵ったら、その場で押せるようにする。**
            // ⚠️ 状態が変わった瞬間だけ差し替える（毎フレーム付け替えると押しどころが飛ぶ）
            bool ready = Hatchery.IsReady(_slot, now);
            if (ready == _wasReady) return;
            _wasReady = ready;
            if (_button == null || _onCollect == null) return;
            _button.onClick.RemoveAllListeners();
            _button.interactable = ready;
            if (ready) _button.onClick.AddListener(() => _onCollect());
        }
    }
}
