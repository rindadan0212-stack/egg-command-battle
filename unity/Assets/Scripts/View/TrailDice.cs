using System;
using UnityEngine;
using UnityEngine.UI;

namespace EggCommand.View
{
    /// <summary>さいころを振る間。⭐ **目が決まる瞬間だけを見せる。**
    ///
    /// ⚠️ 出目は <see cref="Core.Trails.Roll"/> が**先に**決めている。ここは見せるだけで、
    /// 何が出るかは決めない（決めると出所が2つになる）。
    ///
    /// ⚠️ 画面の外（Overlay）に置く。画面は操作のたびに丸ごと組み直されるので、
    /// 中に置くと回っている最中に消える。</summary>
    public sealed class TrailDice : MonoBehaviour
    {
        /// <summary>回している時間。⭐ 短く。⚠️ 長いと、振る回数ぶん待たされる。</summary>
        private const float Spin = 0.42f;
        /// <summary>出目を出したまま止めておく時間。</summary>
        private const float Hold = 0.30f;
        /// <summary>目が切り替わる間隔。</summary>
        private const float Flick = 0.055f;

        /// <summary>目の字。⚠️ <see cref="Core.Trail.Pips"/> ぶん要る。</summary>
        private static readonly string[] Faces = { "", "１", "２", "３", "４", "５", "６" };

        private Text _face;
        private RectTransform _box;
        private int _result;
        private float _age;
        private float _flicked;
        private int _shown;
        private bool _done;
        private Action _onDone;

        /// <param name="result">実際に出た目。⚠️ ここで引き直さない。</param>
        public static void Show(RectTransform parent, int result, Action onDone)
        {
            var go = new GameObject("TrailDice", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var dice = go.AddComponent<TrailDice>();

            var root = (RectTransform)go.transform;
            Ui.Stretch(root);
            // ⚠️ 覆いは触れない。回っている最中に盤を押させない
            var veil = go.AddComponent<Image>();
            veil.color = new Color(0f, 0f, 0f, 0.34f);
            veil.raycastTarget = true;

            const float size = 260f;
            var box = Ui.Rect("Box", root);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(size, size);
            box.anchoredPosition = Vector2.zero;
            var plate = box.gameObject.AddComponent<Image>();
            plate.sprite = Ui.SkinSprite("panel");
            plate.type = Image.Type.Sliced;
            plate.raycastTarget = false;

            var face = Ui.Label(box, "Face", "", 150, Ui.Ink, TextAnchor.MiddleCenter,
                0f, 0f, size, size);
            face.horizontalOverflow = HorizontalWrapMode.Overflow;

            dice._face = face;
            dice._box = box;
            dice._result = Mathf.Clamp(result, 1, Mathf.Min(Core.Trail.Pips, Faces.Length - 1));
            dice._onDone = onDone;
        }

        private void Update()
        {
            if (_done) return;
            _age += Time.deltaTime;

            if (_age < Spin)
            {
                _flicked += Time.deltaTime;
                if (_flicked >= Flick)
                {
                    _flicked = 0f;
                    // ⚠️ 乱数を引かない。回っている見た目だけなので順に回す
                    _shown = _shown % Mathf.Min(Core.Trail.Pips, Faces.Length - 1) + 1;
                    if (_face != null) _face.text = Faces[_shown];
                }
                // ⭐ だんだん小さくなって、止まる所へ収まる
                if (_box != null)
                {
                    float wobble = 1f + 0.16f * Mathf.Sin(_age * 34f) * (1f - _age / Spin);
                    _box.localScale = new Vector3(wobble, wobble, 1f);
                }
                return;
            }

            if (_face != null && _face.text != Faces[_result])
            {
                _face.text = Faces[_result];
                if (_box != null)
                {
                    _box.localScale = Vector3.one;
                    Jolt.Play(_box, new Vector2(0f, -18f), 0.22f);
                }
            }
            if (_age < Spin + Hold) return;

            _done = true;
            var callback = _onDone;
            _onDone = null;
            // ⚠️ Destroy はフレームの終わりまで効かない。残すとクリックを吸う
            gameObject.SetActive(false);
            transform.SetParent(null, false);
            Destroy(gameObject);
            callback?.Invoke();
        }
    }
}
