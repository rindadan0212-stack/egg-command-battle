using UnityEngine;
using UnityEngine.UI;

namespace EggCommand.View
{
    /// <summary>浮いて消える数字。
    ///
    /// ⚠️ **MonoBehaviour はファイル名と同じ名前で、単独のファイルに置く。**
    /// 入れ子や別名のファイルに置くと、Unity は付けられるのに Update を回さない
    /// （実際それで戦闘が1手も進まなかった。エラーも警告も出ないので気づけない）。
    /// </summary>
    public sealed class FloatingNumber : MonoBehaviour
    {
        private const float Life = 0.85f;
        private Text _label;
        private float _age;
        private Vector2 _from;

        public void Begin(Text label)
        {
            _label = label;
            _from = ((RectTransform)transform).anchoredPosition;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = _age / Life;
            if (t >= 1f) { Destroy(gameObject); return; }

            // 立ち上がりだけ速く、あとはゆっくり。⭐ 出た瞬間に目が行く
            float ease = 1f - (1f - t) * (1f - t);
            ((RectTransform)transform).anchoredPosition = _from + new Vector2(0f, 90f * ease);
            var color = _label.color;
            color.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            _label.color = color;
        }
    }
}
