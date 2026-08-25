using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>手に入れた瞬間・生まれた瞬間の全画面演出。
    ///
    /// ⭐ **配置はすべて Assets/Resources/Prefabs/Fanfare.prefab が持つ。**
    /// ここに座標は1つも書かない。動きの時間だけを持つ。
    ///
    /// ⚠️ ここだけは字を出す。ただし説明ではなく**告知**なので置いてよい
    /// （「〜のたまごをゲットした！！」は遊び方の説明ではない）。
    /// </summary>
    public sealed class Fanfare : MonoBehaviour
    {
        [SerializeField] private RectTransform _pop;      // 弾む対象（絵と★をまとめた入れ物）
        [SerializeField] private Image _art;
        [SerializeField] private Image _burst;
        [SerializeField] private Text _stars;
        [SerializeField] private Text _line;
        [SerializeField] private Button _close;

        private const float PopSeconds = 0.42f;
        private const float SpinSeconds = 6f;

        private Action _onClose;
        private bool _closing;

        /// <summary>いま出ている演出。⚠️ 二重に出さない（下の画面が二度触られる）。</summary>
        private static Fanfare _live;
        public static bool IsUp => _live != null;

        /// <summary>卵を手に入れた。</summary>
        public static void EggGot(Transform parent, Egg egg, Action onClose = null)
        {
            var species = SpeciesTable.ById(egg.SpeciesId);
            Put(parent, PixelSpriteTexture.ToSprite(EggArt.Sprite, EggArt.Shell),
                Rarities.StarsOf(egg.Rarity), $"{species.Name}のたまごをゲットした！！",
                ElementMark.ColorOf(egg.Element), onClose);
        }

        /// <summary>卵が孵った。</summary>
        public static void Born(Transform parent, Creature creature, Action onClose = null)
        {
            var species = Creatures.SpeciesOf(creature);
            Put(parent, PixelSpriteTexture.ToSprite(species.Sprite, Creatures.PaletteOf(creature)),
                "", $"{species.Name}がうまれた！！",
                ElementMark.ColorOf(creature.Element), onClose);
        }

        private static void Put(Transform parent, Sprite art, string stars, string line,
            Color burst, Action onClose)
        {
            if (_live != null) _live.Close();

            var prefab = Resources.Load<GameObject>("Prefabs/Fanfare");
            if (prefab == null)
            {
                // ⚠️ 黙って飛ばさない。演出が出ないことに気づけないほうが困る
                Debug.LogError("Fanfare.prefab が読めない（Resources/Prefabs にあるか）");
                onClose?.Invoke();
                return;
            }

            var fanfare = UnityEngine.Object.Instantiate(prefab, parent).GetComponent<Fanfare>();
            _live = fanfare;
            fanfare._onClose = onClose;

            if (fanfare._art != null) { fanfare._art.sprite = art; fanfare._art.preserveAspect = true; }
            if (fanfare._burst != null) fanfare._burst.color = new Color(burst.r, burst.g, burst.b, 0.35f);
            if (fanfare._stars != null)
            {
                fanfare._stars.text = stars;
                fanfare._stars.gameObject.SetActive(stars.Length > 0);
            }
            if (fanfare._line != null) fanfare._line.text = line;
            if (fanfare._close != null)
            {
                fanfare._close.onClick.RemoveAllListeners();
                fanfare._close.onClick.AddListener(fanfare.Close);
            }
            fanfare.StartCoroutine(fanfare.PlayIn());
        }

        /// <summary>飛び出して1度沈む。⚠️ 秒はここが持つ（座標は Prefab）。</summary>
        private IEnumerator PlayIn()
        {
            if (_pop == null) yield break;
            float t = 0f;
            while (t < PopSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / PopSeconds);
                // 行き過ぎて戻る。⚠️ Lerp だけだと「置いた」だけに見えて弾まない
                float scale = 1f + Mathf.Sin(k * Mathf.PI) * 0.35f;
                _pop.localScale = new Vector3(scale * k, scale * k, 1f);
                yield return null;
            }
            _pop.localScale = Vector3.one;
        }

        private void Update()
        {
            // 後ろの光をゆっくり回す。⭐ 動いているものが1つあるだけで「今起きたこと」に見える
            if (_burst != null)
            {
                _burst.rectTransform.Rotate(0f, 0f, 360f / SpinSeconds * Time.deltaTime);
            }
        }

        public void Close()
        {
            if (_closing) return;
            _closing = true;
            if (_live == this) _live = null;

            var callback = _onClose;
            _onClose = null;

            // ⚠️ Destroy はフレームの終わりまで効かない。
            //    そのまま次の画面を組むと、この覆いが生きたまま重なってクリックを吸う
            gameObject.SetActive(false);
            transform.SetParent(null, false);
            Destroy(gameObject);

            callback?.Invoke();
        }
    }
}
