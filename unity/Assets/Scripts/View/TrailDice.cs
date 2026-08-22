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
    /// 中に置くと回っている最中に消える。
    ///
    /// ⭐ **立体で回す**（2026-08-20・作者の指示）。⚠️ 立体そのものを Canvas の手前へ置くと、
    /// 上の「組み直しから守る」仕組みの外へ出てしまう。⭐ だから
    /// <see cref="DieCube"/> が焼いた絵を <c>RawImage</c> に貼る形にしてある。
    /// ⚠️ 焼けない環境では**平面の絵に落とす**（黙って何も出さない、はしない）。</summary>
    public sealed class TrailDice : MonoBehaviour
    {
        /// <summary>回している時間。⭐ 短く。⚠️ 長いと、振る回数ぶん待たされる。</summary>
        private const float Spin = (float)Core.Beats.Spin;
        /// <summary>出目を出したまま止めておく時間。
        /// ⭐ **目を読み切るための間。**⚠️ 短いと「何が出たか分からないまま次へ行く」
        /// （2026-08-20・作者の指示「少しの間停止して出目を正確に目視できるように」）。</summary>
        private const float Hold = (float)Core.Beats.DiceHold;
        /// <summary>目が切り替わる間隔。</summary>
        private const float Flick = (float)Core.Beats.Flick;
        /// <summary>回り終わってから、出目の面が正面へ収まるまでの時間。
        /// ⚠️ 長いとぬるっとして「決まった」感じが消える。</summary>
        private const float Settle = 0.12f;

        /// <summary>止まったときの捻り。⭐ 真正面だと**立体に見えない**ので少しだけ傾ける。</summary>
        private const float RestTilt = 18f;

        /// <summary>回している間の1段ぶんの回転。⚠️ **乱数を引かない。**
        /// ⭐ 割り切れない角度にしてあるので、同じ向きが続けて出ない。</summary>
        private static readonly Vector3 Step = new Vector3(73f, 121f, 47f);

        /// <summary>目の絵（`Resources/UI/icon/die-N`）。
        /// ⚠️ 字で「５」と出さない ── ⭐ **さいころの面をそのまま見せる**
        /// （上の帯に並ぶ残りのさいころと同じ絵なので、結び付けの説明が要らない）。
        /// ⚠️ 立体が焼けなかったときだけ使う。</summary>
        private static string FaceOf(int pips) => "die-" + pips;

        /// <summary>覆いが暗くなり切るまでの時間。⚠️ 回っている時間より短く。</summary>
        private const float Veil = 0.18f;
        /// <summary>覆いの濃さ。⚠️ 濃いと盤が読めず、薄いと転がりが背景に紛れる。</summary>
        private const float VeilInk = 0.34f;

        private Image _veil;
        private DieCube _cube;
        private RawImage _shot;
        private Image _face;          // ⚠️ 立体が焼けなかったときの落とし先
        private RectTransform _box;
        private int _result;
        private float _age;
        private float _flicked;
        private int _shown;
        private bool _done;
        private Quaternion _turn = Quaternion.identity;
        private Quaternion _landed = Quaternion.identity;
        private bool _landing;
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
            // ⭐ **溶かして入れる**（2026-08-21）。⚠️ いきなり暗くすると、
            //    さいころが出るより先に「画面が切り替わった」ように見えて、
            //    転がりの始まりが見えない
            veil.color = new Color(0f, 0f, 0f, 0f);
            veil.raycastTarget = true;
            dice._veil = veil;

            // ⭐ **器に入れない。**画面の真ん中でそのまま転がす
            //    （2026-08-20・作者の指示「枠の中で回るんじゃなくて画面にそのまま」）。
            // ⚠️ 札の上に乗せていた頃は、さいころが**札の中の小物**に見えて、
            //    「いま運が決まっている」という場面にならなかった。
            // ⚠️ **焼いた絵の整数倍**にする（いまは 2倍）。半端だとドットが不揃いになる
            const float size = DieCube.Pixels * 2f;
            var box = Ui.Rect("Box", root);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(size, size);
            box.anchoredPosition = Vector2.zero;

            const float art = size;
            dice._cube = DieCube.Make();
            if (dice._cube != null)
            {
                var shot = Ui.Rect("Shot", box);
                Ui.Place(shot, (size - art) / 2f, (size - art) / 2f, art, art);
                var raw = shot.gameObject.AddComponent<RawImage>();
                raw.texture = dice._cube.Shot;
                raw.raycastTarget = false;
                dice._shot = raw;
            }
            else
            {
                // ⚠️ 立体が焼けない環境。⭐ 平面のまま回す（何も出ないよりよい）
                dice._face = Ui.Icon(box, "Face", "die", Ui.Ink,
                    (size - art) / 2f, (size - art) / 2f, art);
            }

            dice._box = box;
            dice._result = Mathf.Clamp(result, 1, Core.Trail.Pips);
            dice._onDone = onDone;
        }

        private void Update()
        {
            if (_done) return;
            _age += Time.deltaTime;

            if (_veil != null)
            {
                float ink = Mathf.Clamp01(_age / Veil) * VeilInk;
                _veil.color = new Color(0f, 0f, 0f, ink);
            }

            if (_age < Spin)
            {
                _flicked += Time.deltaTime;
                if (_flicked >= Flick)
                {
                    _flicked = 0f;
                    // ⚠️ 乱数を引かない。回っている見た目だけなので順に回す
                    _shown = _shown % Core.Trail.Pips + 1;
                    if (_cube != null)
                    {
                        // ⭐ **段で送る。**⚠️ なめらかに回すと、ドット絵の面がぶれて溶ける
                        _turn = Quaternion.Euler(Step) * _turn;
                        _cube.Turn(_turn);
                    }
                    else if (_face != null)
                    {
                        _face.sprite = Ui.SkinSprite("icon/" + FaceOf(_shown));
                    }
                }
                // ⭐ だんだん小さくなって、止まる所へ収まる
                if (_box != null)
                {
                    float wobble = 1f + 0.16f * Mathf.Sin(_age * 34f) * (1f - _age / Spin);
                    _box.localScale = new Vector3(wobble, wobble, 1f);
                }
                return;
            }

            if (_shown != _result)
            {
                _shown = _result;
                if (_cube != null)
                {
                    // ⭐ いまの向きから、出目の面が正面に来る向きへ寄せていく
                    _landed = Quaternion.Euler(RestTilt * 0.6f, RestTilt, RestTilt * 0.3f)
                        * DieCube.PoseOf(_result);
                    _landing = true;
                }
                else if (_face != null)
                {
                    _face.sprite = Ui.SkinSprite("icon/" + FaceOf(_result));
                }
                if (_box != null)
                {
                    _box.localScale = Vector3.one;
                    Jolt.Play(_box, new Vector2(0f, -18f), 0.22f);
                }
            }

            if (_landing && _cube != null)
            {
                float settled = Mathf.Clamp01((_age - Spin) / Settle);
                _cube.Turn(Quaternion.Slerp(_turn, _landed, settled * settled));
                if (settled >= 1f) _landing = false;
            }

            if (_age < Spin + Hold) return;

            _done = true;
            var callback = _onDone;
            _onDone = null;
            // ⚠️ 焼いた絵は自分で解放する（残すと使い回されずに増える）
            if (_cube != null)
            {
                if (_shot != null) _shot.texture = null;
                _cube.Dismiss();
                _cube = null;
            }
            // ⚠️ Destroy はフレームの終わりまで効かない。残すとクリックを吸う
            gameObject.SetActive(false);
            transform.SetParent(null, false);
            Destroy(gameObject);
            callback?.Invoke();
        }

        /// <summary>⚠️ 途中で画面が閉じても、焼いた絵を残さない。</summary>
        private void OnDestroy()
        {
            if (_cube == null) return;
            _cube.Dismiss();
            _cube = null;
        }
    }
}
