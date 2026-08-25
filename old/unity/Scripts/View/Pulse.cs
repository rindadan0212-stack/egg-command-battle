using UnityEngine;
using UnityEngine.UI;

namespace EggCommand.View
{
    /// <summary>広がって消える丸。⭐ 「そこで何かが起きた」を字を使わずに置く。
    ///
    /// 技を出した足元（構え）にも、当たった体の上（被弾）にも同じ形を使う。
    /// ⚠️ 形を1つに絞る。種類を増やすと、何を見ればいいのか分からなくなる。</summary>
    public sealed class Pulse : MonoBehaviour
    {
        private Image _image;
        private float _age;
        private float _life;
        private float _from;
        private float _to;

        public void Begin(Image image, float from, float to, float life)
        {
            _image = image;
            _from = from;
            _to = to;
            _life = life;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = _age / _life;
            if (t >= 1f) { Destroy(gameObject); return; }

            // 出た瞬間に一番速い。⚠️ 等速だと「膨らんだ風船」に見えて衝撃にならない
            float ease = 1f - (1f - t) * (1f - t) * (1f - t);
            float size = Mathf.Lerp(_from, _to, ease);
            ((RectTransform)transform).sizeDelta = new Vector2(size, size);

            var color = _image.color;
            color.a = (1f - t) * (1f - t);
            _image.color = color;
        }
    }
}
