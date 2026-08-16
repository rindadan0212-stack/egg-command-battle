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
        [SerializeField] private Image _hpFill;
        [SerializeField] private Image _hpBadge;
        [SerializeField] private Text _hpNumber;
        [SerializeField] private Image _gaugeFill;
        [SerializeField] private GameObject _glow;
        [SerializeField] private Image _elementMark;
        [SerializeField] private Image _elementBeats;
        [SerializeField] private Text _status;

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

        public void Bind(Unit unit, bool isActor, bool isFoe)
        {
            if (_hpFullWidth < 0f && _hpFill != null) _hpFullWidth = _hpFill.rectTransform.sizeDelta.x;
            if (_gaugeFullWidth < 0f && _gaugeFill != null) _gaugeFullWidth = _gaugeFill.rectTransform.sizeDelta.x;

            bool alive = Core.Battle.IsAlive(unit);

            if (_art != null)
            {
                _art.sprite = PixelSpriteTexture.ToSprite(
                    Creatures.SpeciesOf(unit.Creature).Sprite, Creatures.PaletteOf(unit.Creature));
                _art.color = alive ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            }

            var tint = alive ? (isFoe ? Ui.Danger : Ui.Good) : Ui.InkFaint;
            float ratio = unit.MaxHp > 0 ? (float)unit.Hp / unit.MaxHp : 0f;
            Fill(_hpFill, ratio, _hpFullWidth, tint);
            if (_hpBadge != null) _hpBadge.color = tint;
            if (_hpNumber != null)
            {
                _hpNumber.text = unit.Hp.ToString();
                // ⚠️ 桁が増えると丸からはみ出す。字を縮めて折り返させない
                _hpNumber.fontSize = unit.Hp >= 1000 ? 16 : unit.Hp >= 100 ? 19 : 23;
                _hpNumber.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            // ⚠️ 組み直しのたびに出し直さない。前に出していた値から続ける
            _key = unit.Key;
            _gaugeTarget = Mathf.Clamp01((float)unit.Gauge / Core.Battle.GaugeMax);
            float shown;
            _gaugeShown = Shown.TryGetValue(_key, out shown) ? shown : _gaugeTarget;
            Shown[_key] = _gaugeShown;
            Fill(_gaugeFill, _gaugeShown, _gaugeFullWidth, null);

            if (_glow != null) _glow.SetActive(isActor);

            var element = Creatures.SpeciesOf(unit.Creature).Element;
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
