using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EggCommand.View
{
    /// <summary>長押しを1つの部品にする。⭐ **同じ札に「触る」と「じっくり見る」を同居させる。**
    ///
    /// ⚠️ 触っただけで詳細が開くと、選ぶたびに札が邪魔になる。
    /// かといって別に「詳細」の押しどころを並べると、
    /// 小さい札の上に押しどころが2つ乗って、どちらを押したのか分からなくなる。
    /// ⭐ 短く触れば選ぶ、押し続ければ見る ── 押しどころは1つのまま。
    ///
    /// ⚠️ **Button と重ねない。**Button は指を離した瞬間に必ず反応するので、
    /// 長押しで開いたあと、指を離した拍子に選びも走ってしまう。
    /// ここが押しどころそのものになり、短い触りは <see cref="OnTap"/> で返す。
    /// </summary>
    public sealed class LongPress : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        /// <summary>これだけ押し続けたら「長押し」。
        /// ⚠️ 短すぎると、選ぶつもりの指が詳細を開いてしまう。</summary>
        public const float Hold = 0.45f;

        /// <summary>指が動いてよい幅（画面の px）。⚠️ これを超えたら見回しているとみなす。</summary>
        private const float Slip = 30f;

        public Action OnTap;
        public Action OnHold;

        private bool _down;
        private float _since;
        private Vector2 _from;
        /// <summary>もう長押しとして返した。⚠️ 離すときに <see cref="OnTap"/> を出さないための札。</summary>
        private bool _fired;

        public void OnPointerDown(PointerEventData data)
        {
            _down = true;
            _fired = false;
            _since = Time.unscaledTime;
            _from = data.position;
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (!_down) return;
            _down = false;
            if (_fired) return;
            if (Vector2.Distance(data.position, _from) > Slip) return;
            if (OnTap != null) OnTap();
        }

        public void OnPointerExit(PointerEventData data)
        {
            _down = false;
        }

        private void Update()
        {
            if (!_down || _fired) return;
            if (Time.unscaledTime - _since < Hold) return;
            _fired = true;
            _down = false;
            if (OnHold != null) OnHold();
        }
    }
}
