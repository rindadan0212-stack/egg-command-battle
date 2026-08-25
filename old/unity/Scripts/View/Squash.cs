using UnityEngine;

namespace EggCommand.View
{
    /// <summary>潰れて伸びて戻る。⭐ **「置いた」を体でわからせる。**
    ///
    /// ⚠️ <see cref="Jolt"/> は位置をずらして戻すもの、<see cref="Throb"/> は
    /// 止めるまで膨らみ続けるもの。こちらは**一度だけ、縦横を逆に**動かす。
    ///
    /// ⭐ 縦に潰れるとき横は広がる（体積が変わらないように見える）。
    /// ⚠️ 両方を同じ向きに動かすと、ただ小さくなるだけで「着いた」に見えない。
    ///
    /// ⚠️ 元の大きさは呼ぶ側が決めている。ここは**掛け算して必ず戻す**だけで、
    /// 寸法そのものは持たない。</summary>
    public sealed class Squash : MonoBehaviour
    {
        /// <summary>どれだけ潰れるか。⚠️ 大きいと餅に見える。</summary>
        private const float Depth = 0.22f;

        private RectTransform _rect;
        private Vector3 _home;
        private float _life;
        private float _age;

        public static void Play(RectTransform rect, float life = 0.22f)
        {
            if (rect == null || life <= 0f) return;
            var squash = rect.GetComponent<Squash>() ?? rect.gameObject.AddComponent<Squash>();
            // ⚠️ 潰れている最中に取り直すと、潰れた形を「元」だと覚えてしまう
            if (squash._age <= 0f) squash._home = rect.localScale;
            squash._rect = rect;
            squash._life = life;
            squash._age = 0f;
        }

        private void Update()
        {
            if (_rect == null || _life <= 0f) return;
            _age += Time.unscaledDeltaTime;

            if (_age >= _life)
            {
                _rect.localScale = _home;
                _life = 0f;
                _age = 0f;
                return;
            }

            // ⭐ 潰れ切ってから戻る（半周ぶんの正弦で、行って帰る）
            float wave = Mathf.Sin(_age / _life * Mathf.PI);
            _rect.localScale = new Vector3(
                _home.x * (1f + Depth * wave),
                _home.y * (1f - Depth * wave),
                _home.z);
        }

        /// <summary>⚠️ 途中で消されても、潰れたまま残さない。</summary>
        private void OnDisable()
        {
            if (_rect == null || _life <= 0f) return;
            _rect.localScale = _home;
            _life = 0f;
            _age = 0f;
        }
    }
}
