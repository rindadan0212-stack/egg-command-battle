using UnityEngine;
using UnityEngine.UI;

namespace EggCommand.View
{
    /// <summary>浮いて消える字。
    ///
    /// ⚠️ **MonoBehaviour はファイル名と同じ名前で、単独のファイルに置く。**
    /// 入れ子や別名のファイルに置くと、Unity は付けられるのに Update を回さない
    /// （実際それで戦闘が1手も進まなかった。エラーも警告も出ないので気づけない）。
    /// </summary>
    public sealed class FloatingNumber : MonoBehaviour
    {
        private Text _label;
        private float _age;
        private float _life = 0.85f;
        private float _rise = 90f;
        private Vector2 _from;

        /// <summary>⭐ 上がる高さと生きる長さを変えられる。
        /// 技名は「読ませたい」ので長く・低く、数字は「見せたい」ので短く・高く。</summary>
        public void Begin(Text label, float life = 0.85f, float rise = 90f)
        {
            _label = label;
            _life = life;
            _rise = rise;
            _from = ((RectTransform)transform).anchoredPosition;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = _age / _life;
            if (t >= 1f) { Destroy(gameObject); return; }

            // 立ち上がりだけ速く、あとはゆっくり。⭐ 出た瞬間に目が行く
            float ease = 1f - (1f - t) * (1f - t);
            ((RectTransform)transform).anchoredPosition = _from + new Vector2(0f, _rise * ease);
            var color = _label.color;
            color.a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
            _label.color = color;
        }
    }
}
