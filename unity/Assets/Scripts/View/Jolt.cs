using UnityEngine;

namespace EggCommand.View
{
    /// <summary>突き出して戻る動き。⭐ 「打った」「打たれた」を体の動きで見せる。
    ///
    /// ⚠️ 元の位置は Prefab が決めている。ここは**ずらして必ず戻す**だけで、
    /// 座標そのものは持たない（持つと Prefab で動かした位置が効かなくなる）。</summary>
    public sealed class Jolt : MonoBehaviour
    {
        private RectTransform _rect;
        private Vector2 _home;
        private Vector2 _push;
        private float _age;
        private float _life;

        public static void Play(RectTransform rect, Vector2 push, float life = 0.26f)
        {
            if (rect == null) return;
            var jolt = rect.GetComponent<Jolt>();
            // ⚠️ 2つ付けると元の位置を2重に覚えてしまい、戻る先がずれる
            if (jolt == null) jolt = rect.gameObject.AddComponent<Jolt>();
            else jolt.Restore();
            jolt._rect = rect;
            jolt._home = rect.anchoredPosition;
            jolt._push = push;
            jolt._age = 0f;
            jolt._life = life;
        }

        private void Restore()
        {
            if (_rect != null) _rect.anchoredPosition = _home;
        }

        private void Update()
        {
            if (_rect == null) { Destroy(this); return; }
            _age += Time.deltaTime;
            float t = _age / _life;
            if (t >= 1f)
            {
                _rect.anchoredPosition = _home;
                Destroy(this);
                return;
            }
            // 行って戻る。⭐ 山を1つだけにする（震わせると弱っているように見える）
            _rect.anchoredPosition = _home + _push * Mathf.Sin(t * Mathf.PI);
        }

        private void OnDestroy() => Restore();
    }
}
