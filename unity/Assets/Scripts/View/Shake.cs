using UnityEngine;

namespace EggCommand.View
{
    /// <summary>画面を揺らす。⭐ **一大事だけ。**
    ///
    /// ⚠️ <see cref="Jolt"/> は「突き出して戻る」1回の動きで、狙った部品に当てるもの。
    /// こちらは**減衰しながら細かく震える**もので、画面ごと当てる。
    ///
    /// ⭐ 決まりは3つ（2026-08-21・手ざわりの調べで確かめた）:
    /// <list type="bullet">
    ///   <item>**短く**（<see cref="Life"/> ＝ 0.28 秒）── 長いと「まだ効いている」に見える</item>
    ///   <item>**減衰させる** ── 一定の振れ幅だと機械の振動に見える</item>
    ///   <item>**必ず元へ戻す** ── 座標そのものは持たない（持つと呼ぶ側の配置が効かなくなる）</item>
    /// </list>
    ///
    /// ⚠️ 画面は操作のたびに組み直されるので、揺らす相手は**組み直されない層**
    /// （<c>App.Overlay</c> の親）にすること。</summary>
    public sealed class Shake : MonoBehaviour
    {
        /// <summary>震えている時間。⚠️ 50〜300ms を外れると「揺れ」に見えない。</summary>
        private const float Life = 0.28f;
        /// <summary>1秒あたりの震えの回数。⚠️ 少ないと「揺れ」でなく「動き」になる。</summary>
        private const float Beats = 34f;

        private RectTransform _rect;
        private Vector2 _home;
        private float _power;
        private float _age;

        /// <param name="power">最初の振れ幅（画素）。⭐ 0 を渡すと何も起きない。</param>
        public static void Play(RectTransform rect, float power = 26f)
        {
            if (rect == null || power <= 0f) return;
            // ⚠️ 二重に付けない。⭐ 付いているなら**強いほうで上書き**して振り直す
            var shake = rect.GetComponent<Shake>() ?? rect.gameObject.AddComponent<Shake>();
            shake._rect = rect;
            // ⚠️ 揺れている最中に取り直すと、ずれた位置を「元」だと覚えてしまう
            if (shake._age <= 0f || shake._power <= 0f) shake._home = rect.anchoredPosition;
            shake._power = Mathf.Max(shake._power, power);
            shake._age = 0f;
        }

        private void Update()
        {
            if (_rect == null || _power <= 0f) return;
            // ⚠️ 画面の演出は時間の伸び縮みを受けない
            _age += Time.unscaledDeltaTime;

            if (_age >= Life)
            {
                _rect.anchoredPosition = _home;
                _power = 0f;
                _age = 0f;
                return;
            }

            // ⭐ 残りの割合を2乗して落とす（終わりぎわがすっと消える）
            float left = 1f - _age / Life;
            float amount = _power * left * left;
            float turn = _age * Beats;
            _rect.anchoredPosition = _home + new Vector2(
                Mathf.Sin(turn * 1.7f) * amount,
                Mathf.Cos(turn) * amount * 0.7f);
        }

        /// <summary>⚠️ 途中で消されても、ずれたまま残さない。</summary>
        private void OnDisable()
        {
            if (_rect == null || _power <= 0f) return;
            _rect.anchoredPosition = _home;
            _power = 0f;
            _age = 0f;
        }
    }
}
