using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>一覧を絞る軸。⭐ **並べ替えと組み合わせて使う。**
    ///
    /// ⚠️ 並べ替えだけだと、50枠が埋まったときに「探している1体」へ辿り着けない。
    /// 並べ替えは順を変えるだけで、**候補の数を減らさない**。
    ///
    /// ⭐ 軸は「見て分かるもの」だけにする ── 属性・特性の有無・出撃中か。
    /// ⚠️ 数（Lv や素質）で絞る形にしない。しきい値を決めるのは並べ替えの仕事で、
    /// ここに数を持ち込むと2つの道具が同じことをする。
    /// </summary>
    public enum FilterKey
    {
        /// <summary>絞らない。</summary>
        All,
        Fire,
        Water,
        Wood,
        /// <summary>特性を持っている個体だけ。</summary>
        HasTrait,
        /// <summary>いずれかの編成に入っている個体だけ。</summary>
        InParty,
    }

    public static class Filters
    {
        public static readonly FilterKey[] Keys =
        {
            FilterKey.All, FilterKey.Fire, FilterKey.Water, FilterKey.Wood,
            FilterKey.HasTrait, FilterKey.InParty,
        };

        /// <summary>⚠️ 言葉の出所はここ1つ。画面側で書かない。</summary>
        public static string LabelOf(FilterKey key)
        {
            switch (key)
            {
                case FilterKey.All: return "すべて";
                // ⭐ 属性の呼び名は SpeciesTable に1つ（一覧とステ表で食い違わせない）
                case FilterKey.Fire: return SpeciesTable.LabelOf(Element.Fire);
                case FilterKey.Water: return SpeciesTable.LabelOf(Element.Water);
                case FilterKey.Wood: return SpeciesTable.LabelOf(Element.Wood);
                case FilterKey.HasTrait: return "特性あり";
                case FilterKey.InParty: return "出撃中";
                default: throw new System.ArgumentOutOfRangeException(nameof(key));
            }
        }

        /// <summary>その軸で残るか。</summary>
        public static bool Keeps(Game game, Creature creature, FilterKey key)
        {
            switch (key)
            {
                case FilterKey.All: return true;
                case FilterKey.Fire: return creature.Element == Element.Fire;
                case FilterKey.Water: return creature.Element == Element.Water;
                case FilterKey.Wood: return creature.Element == Element.Wood;
                case FilterKey.HasTrait: return creature.TraitId != null;
                case FilterKey.InParty: return Games.IsInParty(game, creature.Id);
                default: throw new System.ArgumentOutOfRangeException(nameof(key));
            }
        }

        /// <summary>絞ってから返す。⚠️ 元の並びは変えない（並べ替えは呼ぶ側の仕事）。</summary>
        public static List<Creature> Apply(Game game, IReadOnlyList<Creature> list, FilterKey key)
        {
            var kept = new List<Creature>();
            foreach (var creature in list)
            {
                if (Keeps(game, creature, key)) kept.Add(creature);
            }
            return kept;
        }
    }
}
