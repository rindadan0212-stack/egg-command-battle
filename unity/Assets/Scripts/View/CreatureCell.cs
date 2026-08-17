using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>一覧の升1つ。⭐ BOX と配合で同じ型を使う（読み方が画面ごとに変わらない）。</summary>
    public sealed class CreatureCell : MonoBehaviour
    {
        [SerializeField] private Image _art;
        [SerializeField] private Image _element;
        [SerializeField] private Text _wild;
        [SerializeField] private GameObject _mark;   // 選ばれている印
        /// <summary>特性を持っている印。⭐ 一覧で持ち主を見つけるためだけの丸。
        /// ⚠️ 名前は出さない（升が小さい）。何を持っているかは詳細で読ませる。</summary>
        [SerializeField] private GameObject _trait;
        [SerializeField] private Button _button;

        public void Bind(Creature creature, bool picked, Action onTap)
        {
            var species = Creatures.SpeciesOf(creature);
            if (_art != null)
            {
                _art.sprite = PixelSpriteTexture.ToSprite(species.Sprite, Creatures.PaletteOf(creature));
                _art.preserveAspect = true;
            }
            if (_element != null) _element.color = ElementMark.ColorOf(creature.Element);
            if (_wild != null) _wild.text = Creatures.WildTotalOf(creature).ToString();
            if (_mark != null) _mark.SetActive(picked);
            if (_trait != null) _trait.SetActive(creature.TraitId != null);
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                if (onTap != null) _button.onClick.AddListener(() => onTap());
            }
        }
    }

    /// <summary>ステ1行。⭐ 並びは Prefab が持つ。</summary>
    [Serializable]
    public sealed class StatRow
    {
        public Text Label;
        public Text Value;
        public Image Bar;
    }

    /// <summary>配合の親1枠。</summary>
    [Serializable]
    public sealed class ParentSlot
    {
        public GameObject Filled;
        public GameObject Empty;
        public Image Art;
        public Image Element;
        public Text Name;
        public Text Wild;
        /// <summary>実値4本。⭐ 何を掛け合わせるのか、数を見て決められるように。</summary>
        public Text[] Stats;
        /// <summary>技3枠を1つの字にまとめたもの。⭐ 配合で狙うのは主にここ。</summary>
        public Text Skills;
        /// <summary>得意・不得意。⭐ **遺伝する**ので配合の判断材料になる。</summary>
        public Text Slant;
        /// <summary>特性。⭐ **★の下限を無視して遺伝する**ので、配合で一番狙う対象になりうる。
        /// ⚠️ 出していなかったので、親が持っているかどうかが配合の画面から読めなかった。</summary>
        public Text Trait;
    }

    /// <summary>格子に札を敷き直す共通処理。
    /// ⚠️ 位置を計算しない。並べるのは Prefab に付けた GridLayoutGroup の仕事。</summary>
    public static class CellGrid
    {
        public static void Fill(RectTransform parent, CreatureCell template,
            IReadOnlyList<Creature> list, Func<string, bool> isPicked, Action<string> onPick)
        {
            if (parent == null || template == null) return;

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(child);
            }
            foreach (var creature in list)
            {
                string id = creature.Id;
                var cell = UnityEngine.Object.Instantiate(template, parent);
                cell.gameObject.SetActive(true);
                cell.Bind(creature, isPicked != null && isPicked(id), () => onPick(id));
            }
        }
    }
}
