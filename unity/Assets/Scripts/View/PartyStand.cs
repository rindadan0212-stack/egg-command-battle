using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>ホームに立つ1体。⭐ 配置は Prefab（PartyStand）が持つ。
    /// ⚠️ 大きさもコードで計算しない。脇と手前の差は Prefab の localScale で付ける。
    ///
    /// ⚠️ MonoBehaviour は**同じ名前のファイル**に置く。まとめると Unity が
    /// 「script is missing」にして、エラーも出さずに何も動かなくなる。</summary>
    public sealed class PartyStand : MonoBehaviour
    {
        [SerializeField] private Image _art;
        [SerializeField] private Text _name;
        [SerializeField] private Text _role;

        public void Bind(Creature creature)
        {
            if (_art != null)
            {
                var species = Creatures.SpeciesOf(creature);
                _art.sprite = PixelSpriteTexture.ToSprite(species.Sprite, Creatures.PaletteOf(creature));
                _art.preserveAspect = true;
            }
            if (_name != null) _name.text = Creatures.SpeciesOf(creature).Name;
            if (_role != null) Ui.Knockout(_role, 3);
        }
    }
}
