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

        public void Bind(App app, Action<Egg> onBegin, Action<Incubation> onCollect)
        {
            _app = app;
            var game = app.Game;
            long now = app.Now();

            if (_idle != null) _idle.Bind(game, app.Now, Retime);
            Retime();

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null) continue;
                var slot = i < game.Incubating.Count ? game.Incubating[i] : null;
                bool ready = slot != null && Hatchery.IsReady(slot, now);
                var captured = slot;
                // ⭐ 入っていれば「取り出す」、空いていれば「在庫を開く」
                _slots[i].Bind(slot, now, app.Now,
                    slot == null ? (Action)OpenPicker
                        : ready ? () => onCollect(captured) : null);
            }

            if (_picker != null) _picker.SetActive(false);
            if (_pickerClose != null)
            {
                _pickerClose.onClick.RemoveAllListeners();
                _pickerClose.onClick.AddListener(ClosePicker);
            }
            FillShelf(game, onBegin);
        }

        /// <summary>素材の数だけ描き直す。⚠️ 画面は組み直さない（毎秒作り直すと触れない）。</summary>
        private void Retime()
        {
            if (_materials == null || _app == null) return;
            _materials.text = _app.Game.Idle.Materials.ToString();
        }

        private void OpenPicker()
        {
            if (_picker != null) _picker.SetActive(true);
        }

        private void ClosePicker()
        {
            if (_picker != null) _picker.SetActive(false);
        }

        private void FillShelf(Game game, Action<Egg> onBegin)
        {
            if (_shelf == null || _eggCard == null) return;
            // ⚠️ Destroy はフレームの終わりまで効かない。親から外して無効にし、その場で居なくする
            for (int i = _shelf.childCount - 1; i >= 0; i--)
            {
                var child = _shelf.GetChild(i).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false);
                Destroy(child);
            }

            bool room = Hatchery.HasRoom(game);
            foreach (var egg in game.Eggs)
            {
                var captured = egg;
                var card = Instantiate(_eggCard, _shelf);
                card.gameObject.SetActive(true);
                card.Bind(egg, room, _app.HatchSpeed, () => onBegin(captured));
            }
        }
    }
}
