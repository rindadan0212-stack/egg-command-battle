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

            Fill(_gaugeFill, Mathf.Clamp01((float)unit.Gauge / Core.Battle.GaugeMax), _gaugeFullWidth, null);

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
