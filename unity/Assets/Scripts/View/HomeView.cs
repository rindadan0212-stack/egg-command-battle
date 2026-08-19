using System;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>ホーム。⭐ 上半分が放置、下半分が孵化器。
    ///
    /// ⭐ 並びは Assets/Resources/Prefabs/HomeScreen.prefab が持つ。ここに座標は無い。
    /// ⚠️ 孵化は独立した画面をやめてここへ移した。空いている枠を押すと、
    /// そのとき初めて卵の在庫が開く（棚を常に出しておかない）。
    /// </summary>
    public sealed class HomeView : MonoBehaviour
    {
        [SerializeField] private Text _materials;
        [SerializeField] private IdleStrip _idle;
        [SerializeField] private IncubatorSlot[] _slots;

        // 在庫。⭐ 空き枠を押したときだけ開く
        [SerializeField] private GameObject _picker;
        [SerializeField] private RectTransform _shelf;
        [SerializeField] private EggCard _eggCard;
        [SerializeField] private Button _pickerClose;

        private App _app;
        /// <summary>在庫を開いたときに押した枠。⭐ **その枠へ入れる**。
        /// ⚠️ 手前から詰めると、取り出すたびに残りが左上へ動く。</summary>
        private int _openedSlot;

        public void Bind(App app, Action<int, Egg> onBegin, Action<Incubation> onCollect)
        {
            _app = app;
            var game = app.Game;
            long now = app.Now();

            if (_idle != null) _idle.Bind(game, app.Now, Retime);
            Retime();

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null) continue;
                // ⭐ 枠は動かない。空けたままにもできる
                var slot = Hatchery.At(game, i);
                bool ready = slot != null && Hatchery.IsReady(slot, now);
                var captured = slot;
                int at = i;
                // ⭐ 入っていれば「取り出す」、空いていれば「在庫を開く」
                // ⚠️ **孵ったときの手を必ず渡す**（いま孵っていなくても）。
                //    ⭐ ホームを開いたまま時間が 0 になっても、その場で押せるようにするため
                _slots[i].Bind(slot, now, app.Now,
                    slot == null ? () => OpenPicker(at)
                        : ready ? () => onCollect(captured) : (Action)null,
                    captured == null ? null : () => onCollect(captured));
            }

            // ⚠️ Body の中の在庫は使わない（上段と下段を覆えないため）。
            //    ⭐ 覆いは Overlay に出す（EggPickerPanel）。型だけ借りる
            if (_picker != null) _picker.SetActive(false);
            _onBegin = onBegin;
        }

        /// <summary>EXP の数だけ描き直す。⚠️ 画面は組み直さない（毎秒作り直すと触れない）。</summary>
        private void Retime()
        {
            if (_materials == null || _app == null) return;
            // ⭐ **EXP と書く。**⚠️ 数だけ出していた頃は、丸い印の隣の数が
            //    何の数なのか画面のどこにも書いていなかった。
            _materials.text = $"EXP {Ui.Digits(_app.Game.Idle.Exp)}";
        }

        private Action<int, Egg> _onBegin;

        private void OpenPicker(int slot)
        {
            _openedSlot = slot;
            EggPickerPanel.Show(_app, _eggCard, egg => _onBegin(_openedSlot, egg));
        }

        /// <summary>画面を離れるときに覆いを畳む。
        /// ⚠️ Overlay に出しているので、この器が消えても覆いは残ってしまう。</summary>
        private void OnDisable() => EggPickerPanel.Close();

    }
}
