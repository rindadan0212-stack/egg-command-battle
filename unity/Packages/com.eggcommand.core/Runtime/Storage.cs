#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>並べ替えの軸。⚠️ TS の <c>SORT_KEYS</c> と順を揃える。</summary>
    public enum SortKey
    {
        WildTotal,
        Hp,
        Atk,
        Def,
        Spd,
        Generation,
        Mutation,
    }

    /// <summary>保管庫。枠は有限。どれを逃がすかの整理が遊びになる。
    ///
    /// ⭐ 50枠にしたのは、4ステぶんの専門親を数体ずつ + 世代管理の余裕が持てる下限だから。
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
            SortKey.Generation, SortKey.Mutation,
        };

        public static string LabelOf(SortKey key)
        {
            switch (key)
            {
                case SortKey.WildTotal: return "素質合計";
                case SortKey.Hp: return "HP";
                case SortKey.Atk: return "攻撃";
                case SortKey.Def: return "防御";
                case SortKey.Spd: return "速度";
                case SortKey.Generation: return "世代";
                case SortKey.Mutation: return "変異";
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

        private static int SortValue(Creature creature, SortKey key)
        {
            switch (key)
            {
                case SortKey.WildTotal: return Creatures.WildTotalOf(creature);
                case SortKey.Generation: return creature.Generation;
                case SortKey.Mutation: return creature.MutationCounter;
                case SortKey.Hp: return creature.Wild.Hp;
                case SortKey.Atk: return creature.Wild.Atk;
                case SortKey.Def: return creature.Wild.Def;
                case SortKey.Spd: return creature.Wild.Spd;
                default: throw new ArgumentOutOfRangeException(nameof(key));
            }
        }

        /// <summary>降順。同値は id で安定させる（並びが実行ごとに変わると比較できない）。
        /// ⚠️ id は "c001" 形式なので、TS の localeCompare と序数比較の結果が一致する。</summary>
        public static List<Creature> Sorted(Storage storage, SortKey key)
        {
            var list = new List<Creature>(storage.Creatures);
            list.Sort((a, b) =>
            {
                int diff = SortValue(b, key) - SortValue(a, key);
                return diff != 0 ? diff : string.CompareOrdinal(a.Id, b.Id);
            });
            return list;
        }
    }
}
