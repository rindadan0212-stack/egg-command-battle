using System;
using UnityEngine;
using UnityEngine.UI;

namespace EggCommand.View
{
    /// <summary>短い告知。⭐ 出て、読ませて、自分で消えて、次へ渡す。
    ///
    /// ⚠️ ボタンを置かない。「親に見つかった！」は選択ではなく**結果**なので、
    /// 押させると「押したから戦闘になった」に見えてしまう。
    /// ⭐ 配置は Assets/Resources/Prefabs/Banner.prefab が持つ。
    /// </summary>
    public sealed class BannerView : MonoBehaviour
    {
        [SerializeField] private RectTransform _strip;
        [SerializeField] private Text _line;

        private const float SlideIn = 0.22f;
        private const float Hold = 0.95f;

        private Action _onDone;
        private float _age;
        private bool _done;

        public static void Show(RectTransform parent, string line, Action onDone)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/Banner");
            if (prefab == null)
            {
                // ⚠️ 黙って飛ばさない。演出が出ないことに気づけないほうが困る
                Debug.LogError("Banner.prefab が読めない（Egg Command/画面を Prefab に書き出す を走らせる）");
                onDone?.Invoke();
                return;
            }
            var banner = UnityEngine.Object.Instantiate(prefab, parent).GetComponent<BannerView>();
            if (banner._line != null) { banner._line.text = line; Ui.Knockout(banner._line, 5); }
            banner._onDone = onDone;
        }

        private void Update()
        {
            if (_done) return;
            _age += Time.deltaTime;

            if (_strip != null)
            {
                // 横から伸びる。⭐ 動いて止まると、そこを読む
                float t = Mathf.Clamp01(_age / SlideIn);
                float ease = 1f - (1f - t) * (1f - t);
                _strip.localScale = new Vector3(ease, 1f, 1f);
            }
            if (_age < SlideIn + Hold) return;

            _done = true;
            var callback = _onDone;
            _onDone = null;

            // ⚠️ Destroy はフレームの終わりまで効かない。
            //    残したまま次の画面を組むと、この覆いがクリックを吸う
            gameObject.SetActive(false);
            transform.SetParent(null, false);
            Destroy(gameObject);

            callback?.Invoke();
        }
    }
}
