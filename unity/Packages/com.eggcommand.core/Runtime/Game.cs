#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>ゲーム全体の状態。唯一の出所。
    ///
    /// ⚠️ 乱数は系統を分けて持つ。片方で消費数が変わっても、もう片方の列がずれない
    /// （較正済みの検査を無効にしないため）。
    /// </summary>
    public sealed class Game
    {
        public readonly int Seed;
        public Storage Storage;
        /// <summary>手に入れてまだ孵していない卵。</summary>
        public readonly List<Egg> Eggs = new List<Egg>();
        /// <summary>出撃させる3体の id。⚠️ 空なら素質の高い順に自動で選ぶ。</summary>
        public readonly List<string> Party = new List<string>();
        /// <summary>通し番号。id を一意にするためだけに使う。</summary>
        public int Serial;

        // 系統ごとの乱数
        public readonly Rng RngNest;
        public readonly Rng RngEgg;
        public readonly Rng RngHatch;
        public readonly Rng RngSteal;
        public readonly Rng RngBreed;

        public Game(int seed)
        {
            Seed = seed;
            Storage = Storages.Empty();
            var root = new Rng(seed);
            RngNest = root.Stream("nest");
            RngEgg = root.Stream("egg");
            RngHatch = root.Stream("hatch");
            RngSteal = root.Stream("steal");
            RngBreed = root.Stream("breed");
        }
    }

    public static class Games
    {
        public const int PartySize = 3;

        public static Game NewGame(int seed)
        {
            var game = new Game(seed);

            // 最初の3体。一番浅い巣の卵を孵したところから始める
            var first = Nests.ById("shallow-scale");
            for (int i = 0; i < 3; i++)
            {
                var egg = Nests.MakeEgg(game.RngEgg, first, EggOrigin.Defeated, ++game.Serial);
                string id = $"c{game.Serial.ToString().PadLeft(3, '0')}";
                game.Storage = Storages.Accept(game.Storage, Nests.Hatch(game.RngHatch, egg, id));
            }
            return game;
        }

        /// <summary>巣の守り手。⚠️ 挑むたびに作り直す（同じ巣でも顔ぶれが変わる）。</summary>
        public static List<Creature> DefendersOf(Game game, Nest nest) =>
            Nests.MakeDefenders(game.RngNest, nest);

        public static Egg GainEgg(Game game, Nest nest, EggOrigin how)
        {
            var egg = Nests.MakeEgg(game.RngEgg, nest, how, ++game.Serial);
            game.Eggs.Add(egg);
            return egg;
        }

        /// <summary>孵す。⚠️ 保管庫が満杯なら孵さない（黙って捨てない）。</summary>
        public static Creature HatchEgg(Game game, string eggId)
        {
            if (Storages.IsFull(game.Storage))
                throw new InvalidOperationException($"保管庫が満杯（{game.Storage.Slots}枠）。先にどれかを逃がす");

            int index = -1;
            for (int i = 0; i < game.Eggs.Count; i++)
            {
                if (game.Eggs[i].Id == eggId) { index = i; break; }
            }
            if (index < 0) throw new ArgumentException($"{eggId} という卵は持っていない");

            var egg = game.Eggs[index];
            game.Eggs.RemoveAt(index);
            string id = $"c{(++game.Serial).ToString().PadLeft(3, '0')}";
            var creature = Nests.Hatch(game.RngHatch, egg, id);
            game.Storage = Storages.Accept(game.Storage, creature);
            return creature;
        }

        public static void ReleaseCreature(Game game, string id)
        {
            game.Storage = Storages.Release(game.Storage, id);
            game.Party.Remove(id);
        }

        public static Creature CreatureById(Game game, string id)
        {
            foreach (var creature in game.Storage.Creatures)
            {
                if (creature.Id == id) return creature;
            }
            throw new ArgumentException($"{id} は保管庫にいない");
        }

        /// <summary>配合する。卵は保管庫ではなく卵の棚に入る（孵すまでが1手間）。</summary>
        public static BreedOutcome BreedPair(Game game, string aId, string bId)
        {
            var outcome = Breeding.Breed(
                game.RngBreed,
                CreatureById(game, aId),
                CreatureById(game, bId),
                ++game.Serial);
            game.Eggs.Add(outcome.Egg);
            return outcome;
        }

        /// <summary>戦闘の報酬。⭐ 出撃していた個体だけがもらう（連れ出すことが育成に直結する）。</summary>
        public static void AwardParty(IReadOnlyList<Creature> party, int amount = 1)
        {
            foreach (var creature in party) Creatures.Award(creature, amount);
        }

        /// <summary>出撃する3体。⚠️ 選んでいなければ素質の高い順に埋める（遊び始めで詰まらないように）。</summary>
        public static List<Creature> PartyOf(Game game)
        {
            var chosen = new List<Creature>();
            foreach (var id in game.Party)
            {
                foreach (var creature in game.Storage.Creatures)
                {
                    if (creature.Id == id) { chosen.Add(creature); break; }
                }
            }
            if (chosen.Count >= PartySize) return chosen.GetRange(0, PartySize);

            var rest = new List<Creature>();
            foreach (var creature in game.Storage.Creatures)
            {
                if (!chosen.Contains(creature)) rest.Add(creature);
            }
            rest.Sort((a, b) =>
            {
                int diff = Creatures.WildTotalOf(b) - Creatures.WildTotalOf(a);
                return diff != 0 ? diff : string.CompareOrdinal(a.Id, b.Id);
            });

            var all = new List<Creature>(chosen);
            all.AddRange(rest);
            return all.Count <= PartySize ? all : all.GetRange(0, PartySize);
        }

        /// <summary>出撃の入り切りを切り替える。⚠️ 上限を超えたら古いものから外す。</summary>
        public static void TogglePartyMember(Game game, string id)
        {
            if (game.Party.Remove(id)) return;
            game.Party.Add(id);
            while (game.Party.Count > PartySize) game.Party.RemoveAt(0);
        }

        public static bool IsInParty(Game game, string id) => game.Party.Contains(id);
    }
}
