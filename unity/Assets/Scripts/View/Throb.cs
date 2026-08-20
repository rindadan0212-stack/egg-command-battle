using UnityEngine;

namespace EggCommand.View
{
    /// <summary>ゆっくり脈打つ。⭐ **「急げ」を字で書かないための動き。**
    ///
    /// ⚠️ <see cref="Pulse"/> は広がって消える一発の丸で、別物。
    /// こちらは**止めるまで続く**ので、「最後の1つ」「いま押せる」を居座らせて示す。
    ///
    /// ⚠️ 元の大きさは呼ぶ側が決めている。ここは**掛け算で膨らませて必ず戻す**だけで、
    /// 寸法そのものは持たない（持つと呼ぶ側の配置が効かなくなる）。</summary>
    public sealed class Throb : MonoBehaviour
    {
        private RectTransform _rect;
        private Vector3 _home;
        private float _depth;
        private float _age;

        /// <param name="depth">どれだけ膨らむか（0.9 で ±9%）。</param>
        public static void On(RectTransform rect, float depth = 0.08f)
        {
            if (rect == null) return;
            var throb = rect.GetComponent<Throb>();
            // ⚠️ 2つ付けると元の大きさを2重に覚えて、戻る先がずれる
            if (throb == null) throb = rect.gameObject.AddComponent<Throb>();
            throb._rect = rect;
            throb._home = rect.localScale;
            throb._depth = depth;
            throb._age = 0f;
        }

        private void Update()
        {
            if (_rect == null) return;
            _age += Time.deltaTime;
            float wave = 1f + _depth * Mathf.Sin(_age * 5.2f);
            _rect.localScale = new Vector3(_home.x * wave, _home.y * wave, _home.z);
        }

        private void OnDisable()
        {
            if (_rect != null) _rect.localScale = _home;
        }
    }
}
