using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>戦闘に立つ1体。⭐ **配置はこの Prefab が持つ。コードは値を流し込むだけ。**
    ///
    /// ⚠️ ここに座標を書かない。位置・大きさを変えたいときは
    /// Unity Editor で Prefab を開いてドラッグする。それが Unity へ移した理由。
    /// </summary>
    public sealed class UnitStand : MonoBehaviour
    {
        [SerializeField] private Image _art;
        /// <summary>HP の帯まるごと（ピル）。⭐ **上の帯に出すときは、こちらを消す。**
        /// ⚠️ 同じ数を2か所に出さない。どちらを見ればいいか決まらなくなる。</summary>
        [SerializeField] private GameObject _hpBar;
        [SerializeField] private Image _hpFill;
        [SerializeField] private Image _hpBadge;
        [SerializeField] private Text _hpNumber;
        /// <summary>行動ゲージの帯まるごと。⭐ HP と一緒に上の帯へ移すときに消す。</summary>
        [SerializeField] private GameObject _gaugeBar;
        [SerializeField] private Image _gaugeFill;
        [SerializeField] private GameObject _glow;
        [SerializeField] private Image _elementMark;
        [SerializeField] private Image _elementBeats;
        [SerializeField] private Text _status;
        /// <summary>狙い先の印。⭐ **いま誰を狙っているか**を体の上で示す。
        /// ⚠️ 別の欄に「狙い: 2番」と書くと、盤とラベルを目で往復することになる。</summary>
        [SerializeField] private GameObject _targetMark;
        /// <summary>体そのものの押しどころ。⭐ 押すと狙い先になる。</summary>
        [SerializeField] private Button _tap;

        /// <summary>帯の伸びる元の幅。⚠️ 実行時に縮めるので、最初に控えておく。</summary>
        private float _hpFullWidth = -1f;
        private float _gaugeFullWidth = -1f;

        // ⭐ ゲージは「いま出している値」と「本当の値」を分けて持つ。
        //    Core は誰かが満ちる瞬間まで飛ぶので、そのまま描くと目で追えない。
        private float _gaugeShown = -1f;
        private float _gaugeTarget;
        private string _key;

        /// <summary>いま出している帯の値。⚠️ 画面を組み直すとこの器は作り直されるので、
        /// 器の外に覚えておく。持たないと組み直すたびに帯が飛ぶ。</summary>
        private static readonly System.Collections.Generic.Dictionary<string, float> Shown =
            new System.Collections.Generic.Dictionary<string, float>();

        /// <summary>戦闘を始めるときに忘れる。⚠️ 前の戦闘の値が残ると初手から満タンに見える。</summary>
        public static void ForgetGauges() => Shown.Clear();

        /// <summary>1秒で詰められる割合。⚠️ 速すぎると結局パッと見える。</summary>
        private const float GaugeCatchUp = 6f;

        /// <summary>帯だけ描き直す。⚠️ 画面を組み直さない
        /// （毎フレーム組み直すと、押しどころが作り直されて触れなくなる）。</summary>
        public void Retick(Unit unit)
        {
            _gaugeTarget = Mathf.Clamp01((float)unit.Gauge / Core.Battle.GaugeMax);
        }

        private void Update()
        {
            if (_gaugeShown < 0f || _gaugeFullWidth < 0f) return;
            if (Mathf.Approximately(_gaugeShown, _gaugeTarget)) return;

            // ⚠️ 手番を使った直後は本当の値が下がる。そこは追いかけず、すぐ合わせる
            //    （じわじわ戻ると「まだ溜まっている」に見える）
            _gaugeShown = _gaugeTarget < _gaugeShown
                ? _gaugeTarget
                : Mathf.MoveTowards(_gaugeShown, _gaugeTarget, GaugeCatchUp * Time.deltaTime);
            if (_key != null) Shown[_key] = _gaugeShown;
            Fill(_gaugeFill, _gaugeShown, _gaugeFullWidth, null);
        }

        public void Bind(Unit unit, bool isActor, bool isFoe) => Bind(unit, isActor, isFoe, false, null);

        /// <param name="isTarget">いま狙い先に選ばれているか。</param>
        /// <param name="onTap">押されたとき。⚠️ null なら押せない（倒れている・決着後）。</param>
        /// <param name="showHp">HP の帯を体の足元に出すか。
        /// ⭐ 相手が1体のときは**上の帯**に出すので、ここは消す。</param>
        public void Bind(Unit unit, bool isActor, bool isFoe, bool isTarget, System.Action onTap,
            bool showHp = true)
        {
            if (_targetMark != null) _targetMark.SetActive(isTarget);
            if (_hpBar != null) _hpBar.SetActive(showHp);
            // ⚠️ **属性の印も一緒に消す。**HP の帯だけ消したとき、印が体の右に
            //    取り残されて「何かの破片」に見えた（実測）。
            //    ⭐ 上の帯が属性の色を持っているので、二重でもある。
            if (_elementMark != null) _elementMark.gameObject.SetActive(showHp);
            if (_elementBeats != null) _elementBeats.gameObject.SetActive(showHp);
            // ⚠️ 行動ゲージも一緒に。HP だけ消したとき、白い線が体の下に
            //    取り残されて破片に見えた（実測）。⭐ 読むものは1か所へ集める
            if (_gaugeBar != null) _gaugeBar.SetActive(showHp);
            if (_tap != null)
            {
                _tap.onClick.RemoveAllListeners();
                _tap.interactable = onTap != null;
                if (onTap != null) _tap.onClick.AddListener(() => onTap());
            }
            if (_hpFullWidth < 0f && _hpFill != null) _hpFullWidth = _hpFill.rectTransform.sizeDelta.x;
            if (_gaugeFullWidth < 0f && _gaugeFill != null) _gaugeFullWidth = _gaugeFill.rectTransform.sizeDelta.x;

            bool alive = Core.Battle.IsAlive(unit);

            if (_art != null)
            {
                _art.sprite = PixelSpriteTexture.ToSprite(
                    Creatures.SpeciesOf(unit.Creature).Sprite, Creatures.PaletteOf(unit.Creature));
                _art.color = alive ? Color.white : new Color(1f, 1f, 1f, 0.25f);
                // ⭐ 敵は左右反転。⚠️ 器ではなく**絵だけ**に掛ける（字が裏返らないように）
                Ui.Face(_art.rectTransform, isFoe);
            }

            var tint = alive ? (isFoe ? Ui.Danger : Ui.Good) : Ui.InkFaint;
            float ratio = unit.MaxHp > 0 ? (float)unit.Hp / unit.MaxHp : 0f;
            Fill(_hpFill, ratio, _hpFullWidth, tint);
            // ⚠️ **HP の帯の脇に属性の丸を出さない**（2026-08-22・作者の指示）。
            //    ⭐ 属性は体の脇（`_elementMark`）が既に出している ── 同じことを
            //    2か所で言うと、どちらを見ればいいか決まらない。
            //    ⭐ 欄は残す（Prefab に置いてあるものを消すと、次に作り直すとき手で置き直しになる）。
            if (_hpBadge != null) _hpBadge.gameObject.SetActive(false);
            if (_hpNumber != null) _hpNumber.gameObject.SetActive(false);

            // ⚠️ 組み直しのたびに出し直さない。前に出していた値から続ける
            _key = unit.Key;
            _gaugeTarget = Mathf.Clamp01((float)unit.Gauge / Core.Battle.GaugeMax);
            float shown;
            _gaugeShown = Shown.TryGetValue(_key, out shown) ? shown : _gaugeTarget;
            Shown[_key] = _gaugeShown;
            Fill(_gaugeFill, _gaugeShown, _gaugeFullWidth, null);

            if (_glow != null) _glow.SetActive(isActor);

            var element = unit.Creature.Element;
            if (_elementMark != null) _elementMark.color = ElementMark.ColorOf(element);
            if (_elementBeats != null) _elementBeats.color = ElementMark.ColorOf(SpeciesTable.Beats(element));

            if (_status != null)
            {
                var list = Core.Battle.ActiveStatuses(unit);
                _status.text = list.Count > 0 ? string.Join(" ", list) : "";
            }
        }

        private static void Fill(Image image, float ratio, float fullWidth, Color? color)
        {
            if (image == null) return;
            var size = image.rectTransform.sizeDelta;
            size.x = Mathf.Max(0f, fullWidth) * Mathf.Clamp01(ratio);
            image.rectTransform.sizeDelta = size;
            if (color.HasValue) image.color = color.Value;
        }
    }
}
