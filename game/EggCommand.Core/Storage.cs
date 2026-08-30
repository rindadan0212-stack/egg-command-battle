#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>並べ替えの軸。⚠️ TS の <c>SORT_KEYS</c> と順を揃える。</summary>
    /// <summary>何の数で並べるか。⭐ **育成を含めるかどうか**（作者の指示 2026-08-19）。
    ///
    /// ⚠️ 前は生の野生ロール（0〜40）で並べていた。種族の基礎値が入らないので、
    /// 画面に出ている数（素質の列）とも、戦闘で使う数とも**別の順**になっていた。</summary>
    public enum SortBasis
    {
        /// <summary>素質だけ。⭐ 種族の基礎値 ＋ 野生（育成前の実値）。
        /// ⚠️ **生まれつきの良し悪しを見る**ときはこちら。育てた個体に埋もれない。</summary>
        Born,
        /// <summary>合計。⭐ 育成を含めた、いま戦闘で使う実値。</summary>
        Total,
    }

    public enum SortKey
    {
        WildTotal,
        Hp,
        Atk,
        Def,
        Spd,
        Generation,
        Mutation,
        /// <summary>⭐ **入手順**（2026-08-22・作者の指示）。
        /// ⚠️ 数で並べるのではなく、**保管庫に入った順そのもの**。
        /// ⭐ 新しく手に入れた個体を探すのが、これが一番速い。</summary>
        Caught,
    }

    /// <summary>保管庫。枠は有限。どれを逃がすかの整理が遊びになる。
    ///
    /// ⭐ 50枠にしたのは、ステごとの専門親を数体ずつ + 世代管理の余裕が持てる下限だから。
    /// 20枠だと ARK 型の「専門親を複数持つ」遊びが成立せず、
    /// 100枠だと捨てる判断が生まれずリストが膨れるだけになる。
    /// </summary>
    public sealed class Storage
    {
        public readonly int Slots;
        public readonly IReadOnlyList<Creature> Creatures;

        public Storage(int slots, IReadOnlyList<Creature> creatures)
        {
            Slots = slots;
            Creatures = creatures;
        }
    }

    public static class Storages
    {
        public const int StorageSlots = 50;

        public static readonly SortKey[] SortKeys =
        {
            SortKey.WildTotal, SortKey.Hp, SortKey.Atk, SortKey.Def, SortKey.Spd,
            SortKey.Generation, SortKey.Mutation, SortKey.Caught,
        };

        public static string LabelOf(SortKey key)
        {
            switch (key)
            {
                case SortKey.WildTotal: return "素質合計";
                // ⭐ 言葉の出所は Stats に1つ（並べ替えの札とステ表で食い違わせない）
                case SortKey.Hp: return Stats.LabelOf(StatKey.Hp);
                case SortKey.Atk: return Stats.LabelOf(StatKey.Atk);
                case SortKey.Def: return Stats.LabelOf(StatKey.Def);
                case SortKey.Spd: return Stats.LabelOf(StatKey.Spd);
                case SortKey.Generation: return "世代";
                case SortKey.Mutation: return "変異";
                case SortKey.Caught: return "入手";
                default: throw new ArgumentOutOfRangeException(nameof(key));
            }
        }

        public static Storage Empty() => new Storage(StorageSlots, new List<Creature>());

        public static bool IsFull(Storage storage) => storage.Creatures.Count >= storage.Slots;

        /// <summary>⚠️ 満杯を黙って捨てない。呼び側に「どれを逃がすか」を決めさせる。</summary>
        public static Storage Accept(Storage storage, Creature creature)
        {
            if (IsFull(storage))
                throw new InvalidOperationException($"保管庫が満杯（{storage.Slots}枠）。先にどれかを逃がす");
            foreach (var existing in storage.Creatures)
            {
                if (existing.Id == creature.Id)
                    throw new InvalidOperationException($"{creature.Id} は既に保管庫にいる");
            }
            var next = new List<Creature>(storage.Creatures) { creature };
            return new Storage(storage.Slots, next);
        }

        public static Storage Release(Storage storage, string id)
        {
            var next = new List<Creature>(storage.Creatures.Count);
            foreach (var creature in storage.Creatures)
            {
                if (creature.Id != id) next.Add(creature);
            }
            if (next.Count == storage.Creatures.Count)
                throw new InvalidOperationException($"{id} は保管庫にいない");
            return new Storage(storage.Slots, next);
        }

        /// <summary>切り替えられる基準。⚠️ 画面はこの並びをそのまま出す。</summary>
        public static readonly SortBasis[] Bases = { SortBasis.Born, SortBasis.Total };

        public static string LabelOf(SortBasis basis) =>
            basis == SortBasis.Born ? "素質だけ" : "合計";

        /// <summary>いまの並べ替えがその個体に見ている数。⭐ **一覧の升へ出すための口**
        /// （2026-08-30・作者の指示「枠内下に並び替え中の数字か星を表示」）。
        /// ⚠️ <see cref="SortKey.Caught"/> は数で並べていない（入った順そのまま）ので
        /// **数を持たない** ── 呼び側は★を出す（<see cref="Sorted"/> の註と対）。</summary>
        public static int? ShownValue(Creature creature, SortKey key, SortBasis basis) =>
            key == SortKey.Caught ? null : SortValue(creature, key, basis);

        private static int SortValue(Creature creature, SortKey key, SortBasis basis)
        {
            switch (key)
            {
                case SortKey.Generation: return creature.Generation;
                case SortKey.Mutation: return creature.MutationCounter;
            }

            // ⭐ **素質だけ**なら育成前の実値、**合計**なら育成込みの実値。
            // ⚠️ どちらも「画面の表に出ている数」と同じ ── 生の野生ロールでは並べない。
            var stats = basis == SortBasis.Born
                ? Creatures.Slanted(Creatures.BornStatsOf(creature.SpeciesId, creature.Wild),
                    creature)
                : Creatures.StatsOf(creature);

            switch (key)
            {
                case SortKey.WildTotal: return Stats.TotalOf(stats);
                case SortKey.Hp: return stats.Hp;
                case SortKey.Atk: return stats.Atk;
                case SortKey.Def: return stats.Def;
                case SortKey.Spd: return stats.Spd;
                default: throw new ArgumentOutOfRangeException(nameof(key));
            }
        }

        /// <summary>降順。同値は id で安定させる（並びが実行ごとに変わると比較できない）。
        /// ⚠️ id は "c001" 形式なので、TS の localeCompare と序数比較の結果が一致する。</summary>
        public static List<Creature> Sorted(Storage storage, SortKey key,
            SortBasis basis = SortBasis.Born)
        {
            var list = new List<Creature>(storage.Creatures);
            // ⭐ **入手順は並べ替えない。**⚠️ 保管庫は入った順に足していく
            //    （<see cref="Accept"/> が末尾へ）ので、**そのままが入手順**。
            //    ⚠️ 数で並べようとすると id の付け方に頼ることになり、
            //    撮影用など別の付け方の id が混じった瞬間に狂う。
            if (key == SortKey.Caught)
            {
                list.Reverse();   // ⭐ 新しいものが先頭（探したいのは直近の1体）
                return list;
            }
            list.Sort((a, b) =>
            {
                int diff = SortValue(b, key, basis) - SortValue(a, key, basis);
                return diff != 0 ? diff : string.CompareOrdinal(a.Id, b.Id);
            });
            return list;
        }
    }
}
