using System;
using UnityEngine;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>孵化の画面まるごと。
    /// ⭐ 並びは Assets/Resources/Prefabs/HatchScreen.prefab が持つ。ここに座標は無い。
    /// 棚の格子は GridLayoutGroup。桁数も間隔も Prefab 側で決める。</summary>
    public sealed class HatchView : MonoBehaviour
    {
        [SerializeField] private IncubatorSlot[] _slots;
        [SerializeField] private RectTransform _shelf;      // 棚の中身を入れる親（GridLayoutGroup）
        [SerializeField] private EggCard _eggCard;          // 棚に並べる札の型
        [SerializeField] private GameObject _shelfEmpty;    // 卵が1つも無いときの空の台

        public void Bind(Game game, Func<long> clock, int speed,
            Action<Egg> onBegin, Action<Incubation> onCollect)
        {
            long now = clock();

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null) continue;
                var slot = i < game.Incubating.Count ? game.Incubating[i] : null;
                bool ready = slot != null && Hatchery.IsReady(slot, now);
                var captured = slot;
                _slots[i].Bind(slot, now, clock,
                    ready ? () => onCollect(captured) : (Action)null);
            }

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
                card.Bind(egg, room, speed, () => onBegin(captured));
            }
            if (_shelfEmpty != null) _shelfEmpty.SetActive(game.Eggs.Count == 0);
        }
    }
}
