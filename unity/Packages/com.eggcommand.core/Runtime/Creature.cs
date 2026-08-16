#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>個体。
    ///
    /// ⚠️ 導出できるものは保存しない。
    /// スキル枠1は種族固定なので個体に持たせない — 持たせると種族と食い違いうる
    /// 第2の出所になる。実値も同じ理由で保存せず、毎回 <see cref="Stats"/> で計算する。
    /// </summary>
    public sealed class Creature
    {
        public readonly string Id;
        public readonly string SpeciesId;

        /// <summary>遺伝で決まる素質。変えられない。合計上限は適用済みの値だけを入れる。</summary>
        public readonly StatBlock Wild;

        /// <summary>育成でプレイヤーが振った分。
        /// ⚠️ 個体の中でここと <see cref="Earned"/> だけが書き換わる。素質は変えられない。</summary>
        public StatBlock Trained;

        /// <summary>戦闘で得た育成ポイントの総数（振った分 + 未使用）。</summary>
        public int Earned;

        /// <summary>変異カウンタ。⚠️ 両親とも20以上だと子に変異が出ない（無限強化のブレーキ）。</summary>
        public readonly int MutationCounter;

        /// <summary>枠2・3 のみ。⚠️ 枠1は種族から導出する。null は「空き枠」。</summary>
        public readonly string? Skill2;
        public readonly string? Skill3;

        /// <summary>種族のパレット添字。変異は色変化として出る。</summary>
        public readonly int PaletteIndex;

        public readonly string? ParentA;
        public readonly string? ParentB;
        public readonly int Generation;

        public Creature(string id, string speciesId, StatBlock wild, StatBlock trained, int earned,
            int mutationCounter, string? skill2, string? skill3, int paletteIndex,
            string? parentA, string? parentB, int generation)
        {
            Id = id;
            SpeciesId = speciesId;
            Wild = wild;
            Trained = trained;
            Earned = earned;
            MutationCounter = mutationCounter;
            Skill2 = skill2;
            Skill3 = skill3;
            PaletteIndex = paletteIndex;
            ParentA = parentA;
            ParentB = parentB;
            Generation = generation;
        }
    }

    public static class Creatures
    {
        /// <summary>育成ポイントの上限。
        ///
        /// ⭐ 戦闘に勝つ（または盗みに成功する）と、出撃していた個体が +1 もらう。
        /// 「連れ出す」ことが育成に直結するので、強い個体を使うほど伸びる。
        /// ⚠️ 上限があるので「時間さえかければ素質差を埋められる」にはならない
        /// （素質＝厳選の成果が勝敗を決める、という軸を守るため）。</summary>
        public const int TrainMax = 20;

        public static Species SpeciesOf(Creature creature) => SpeciesTable.ById(creature.SpeciesId);

        public static int SpentOf(Creature creature) => Stats.TotalOf(creature.Trained);

        /// <summary>まだ振っていない育成ポイント。</summary>
        public static int UnspentOf(Creature creature) => creature.Earned - SpentOf(creature);

        /// <summary>戦闘の報酬。⚠️ 上限を超えて溜めない。</summary>
        public static void Award(Creature creature, int amount)
        {
            int next = creature.Earned + amount;
            creature.Earned = next > TrainMax ? TrainMax : next;
        }

        /// <summary>1点を振る。⚠️ 戻せない（取り返しがつかないほうが判断に重みが出る）。</summary>
        public static void SpendPoint(Creature creature, StatKey key)
        {
            if (UnspentOf(creature) <= 0)
                throw new InvalidOperationException($"{creature.Id} に振れる育成ポイントが無い");
            creature.Trained = creature.Trained.With(key, creature.Trained[key] + 1);
        }

        /// <summary>3枠ぶんのスキル。⭐ 枠1は必ず種族のもの。空き枠は null。</summary>
        public static Skill?[] SkillsOf(Creature creature)
        {
            var species = SpeciesOf(creature);
            return new Skill?[]
            {
                Skills.ById(species.Skill1),
                creature.Skill2 == null ? null : Skills.ById(creature.Skill2),
                creature.Skill3 == null ? null : Skills.ById(creature.Skill3),
            };
        }

        /// <summary>実値。唯一の出所は <see cref="Stats"/>。ここは種族基礎を渡すだけ。</summary>
        public static StatBlock StatsOf(Creature creature) =>
            Stats.ActualStats(SpeciesOf(creature).Base, creature.Wild, creature.Trained);

        /// <summary>野生レベルの合計。厳選の目安として並べ替えに使う。</summary>
        public static int WildTotalOf(Creature creature) => Stats.TotalOf(creature.Wild);

        /// <summary>その個体のパレット。添字が範囲外なら黙って通常色にせず投げる。</summary>
        public static Palette PaletteOf(Creature creature)
        {
            var species = SpeciesOf(creature);
            if (creature.PaletteIndex < 0 || creature.PaletteIndex >= species.Palettes.Count)
                throw new ArgumentException($"{species.Id} にパレット添字 {creature.PaletteIndex} が無い");
            return species.Palettes[creature.PaletteIndex];
        }
    }
}
