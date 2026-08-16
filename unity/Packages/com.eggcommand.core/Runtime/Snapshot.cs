#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    // ⭐ 保存の形。実行時の形（Game / Creature …）とは**別に**定義する。
    // ⚠️ 実行時の形をそのまま保存すると、遊びを直すたびに古い保存が読めなくなる。
    // ⚠️ 素の public 欄と [Serializable] だけで書く。Unity の JsonUtility が
    //    そのまま読み書きできる形にしておく（Core はエンジンに触らない）。

    [Serializable]
    public sealed class StatSave
    {
        public int Hp, Atk, Def, Spd;

        public static StatSave Of(StatBlock b) =>
            new StatSave { Hp = b.Hp, Atk = b.Atk, Def = b.Def, Spd = b.Spd };

        public StatBlock To() => new StatBlock(Hp, Atk, Def, Spd);
    }

    [Serializable]
    public sealed class CreatureSave
    {
        public string Id = "", SpeciesId = "";
        public StatSave Wild = new StatSave(), Trained = new StatSave();
        public int Earned, MutationCounter, PaletteIndex, Generation;
        public string? Skill2, Skill3, ParentA, ParentB;
        /// <summary>⚠️ -1 は「持たない」。enum を直に入れると 0 と区別が付かない。</summary>
        public int Strong = -1, Weak = -1;
    }

    [Serializable]
    public sealed class EggSave
    {
        public string Id = "", SpeciesId = "";
        public StatSave Wild = new StatSave();
        public int MutationCounter, PaletteIndex, Generation, How, Rarity;
        public bool HasSkills;
        public string? Skill2, Skill3, ParentA, ParentB;
        public int Strong = -1, Weak = -1;
    }

    [Serializable]
    public sealed class IncubationSave
    {
        public EggSave Egg = new EggSave();
        public long StartUnix, ReadyUnix;
        public int Slot;
    }

    [Serializable]
    public sealed class EncounterSave
    {
        public string NestId = "", Name = "", SpeciesId = "";
        public int Tier, Level;
    }

    [Serializable]
    public sealed class IdleSave
    {
        public int Materials, Defeated;
        public long LastUnix;
        public double EnemyHp, Charge;
        public List<string> DownIds = new List<string>();
        public List<long> DownUntil = new List<long>();
    }

    [Serializable]
    public sealed class GameSave
    {
        /// <summary>保存の版。⚠️ 形を変えたら上げる。合わなければ読まずに作り直す。</summary>
        public int Version = 1;
        public int Seed, Serial, EncounterSerial, Slots;
        public List<CreatureSave> Creatures = new List<CreatureSave>();
        public List<EggSave> Eggs = new List<EggSave>();
        public List<IncubationSave> Incubating = new List<IncubationSave>();
        public List<EncounterSave> Encounters = new List<EncounterSave>();
        public List<string> Party = new List<string>();
        public IdleSave Idle = new IdleSave();
        /// <summary>乱数7系統ぶんの状態を平らに並べたもの（4語 × 7）。</summary>
        public List<uint> Rng = new List<uint>();
    }

    /// <summary>保存と復元。⭐ **ここが唯一の変換場所**。
    ///
    /// ⚠️ JSON にはしない。文字にするのは呼び側（Unity の JsonUtility）の仕事。
    /// Core はエンジンにもファイルにも触らないので、変換だけをここで閉じる。
    /// </summary>
    public static class Snapshots
    {
        public const int Version = 1;

        /// <summary>乱数の並び。⚠️ 順番を変えない（保存した列と対応が崩れる）。</summary>
        private static Rng[] StreamsOf(Game game) => new[]
        {
            game.RngNest, game.RngEgg, game.RngHatch, game.RngSteal,
            game.RngBreed, game.RngRarity, game.RngEncounter, game.RngSlant,
        };

        public static GameSave Save(Game game)
        {
            var save = new GameSave
            {
                Version = Version,
                Seed = game.Seed,
                Serial = game.Serial,
                EncounterSerial = game.EncounterSerial,
                Slots = game.Storage.Slots,
            };

            foreach (var c in game.Storage.Creatures) save.Creatures.Add(Of(c));
            foreach (var e in game.Eggs) save.Eggs.Add(Of(e));
            foreach (var i in game.Incubating)
            {
                save.Incubating.Add(new IncubationSave
                {
                    Egg = Of(i.Egg), StartUnix = i.StartUnix, ReadyUnix = i.ReadyUnix, Slot = i.Slot,
                });
            }
            foreach (var e in game.Encounters)
            {
                save.Encounters.Add(new EncounterSave
                {
                    NestId = e.Nest.Id, Name = e.Nest.Name, SpeciesId = e.Nest.SpeciesId,
                    Tier = e.Nest.Tier, Level = e.Level,
                });
            }
            save.Party.AddRange(game.Party);

            save.Idle.Materials = game.Idle.Materials;
            save.Idle.Defeated = game.Idle.Defeated;
            save.Idle.LastUnix = game.Idle.LastUnix;
            save.Idle.EnemyHp = game.Idle.EnemyHp;
            save.Idle.Charge = game.Idle.Charge;
            foreach (var pair in game.Idle.DownUntil)
            {
                save.Idle.DownIds.Add(pair.Key);
                save.Idle.DownUntil.Add(pair.Value);
            }

            foreach (var rng in StreamsOf(game)) save.Rng.AddRange(rng.State());
            return save;
        }

        /// <summary>復元する。⚠️ 版が合わなければ null（黙って壊れた状態で始めない）。</summary>
        public static Game? Load(GameSave? save)
        {
            if (save == null || save.Version != Version) return null;

            var game = new Game(save.Seed);
            game.Serial = save.Serial;
            game.EncounterSerial = save.EncounterSerial;

            var creatures = new List<Creature>();
            foreach (var c in save.Creatures) creatures.Add(To(c));
            game.Storage = new Storage(save.Slots > 0 ? save.Slots : game.Storage.Slots, creatures);

            foreach (var e in save.Eggs) game.Eggs.Add(To(e));
            foreach (var i in save.Incubating)
            {
                game.Incubating.Add(new Incubation(To(i.Egg), i.StartUnix, i.ReadyUnix, i.Slot));
            }
            foreach (var e in save.Encounters)
            {
                game.Encounters.Add(new Encounter(
                    new Nest(e.NestId, e.Name, e.SpeciesId, e.Tier), e.Level));
            }
            game.Party.AddRange(save.Party);

            game.Idle.Materials = save.Idle.Materials;
            game.Idle.Defeated = save.Idle.Defeated;
            game.Idle.LastUnix = save.Idle.LastUnix;
            game.Idle.EnemyHp = save.Idle.EnemyHp;
            game.Idle.Charge = save.Idle.Charge;
            int pairs = Math.Min(save.Idle.DownIds.Count, save.Idle.DownUntil.Count);
            for (int i = 0; i < pairs; i++)
            {
                game.Idle.DownUntil[save.Idle.DownIds[i]] = save.Idle.DownUntil[i];
            }

            var streams = StreamsOf(game);
            for (int i = 0; i < streams.Length; i++)
            {
                int at = i * 4;
                if (at + 4 > save.Rng.Count) break;
                streams[i].Restore(new[]
                {
                    save.Rng[at], save.Rng[at + 1], save.Rng[at + 2], save.Rng[at + 3],
                });
            }
            return game;
        }

        // ── 個々の変換 ──────────────────────────────────

        private static int Key(StatKey? key) => key == null ? -1 : (int)key.Value;

        private static StatKey? Key(int value)
        {
            if (value < 0) return null;
            foreach (var key in Stats.Keys)
            {
                if ((int)key == value) return key;
            }
            return null;
        }

        private static CreatureSave Of(Creature c) => new CreatureSave
        {
            Id = c.Id, SpeciesId = c.SpeciesId,
            Wild = StatSave.Of(c.Wild), Trained = StatSave.Of(c.Trained),
            Earned = c.Earned, MutationCounter = c.MutationCounter,
            Skill2 = c.Skill2, Skill3 = c.Skill3, PaletteIndex = c.PaletteIndex,
            ParentA = c.ParentA, ParentB = c.ParentB, Generation = c.Generation,
            Strong = Key(c.Strong), Weak = Key(c.Weak),
        };

        private static Creature To(CreatureSave s) => new Creature(
            s.Id, s.SpeciesId, s.Wild.To(), s.Trained.To(), s.Earned,
            s.MutationCounter, s.Skill2, s.Skill3, s.PaletteIndex,
            s.ParentA, s.ParentB, s.Generation, Key(s.Strong), Key(s.Weak));

        private static EggSave Of(Egg e) => new EggSave
        {
            Id = e.Id, SpeciesId = e.SpeciesId, Wild = StatSave.Of(e.Wild),
            MutationCounter = e.MutationCounter, PaletteIndex = e.PaletteIndex,
            Generation = e.Generation, How = (int)e.How, Rarity = e.Rarity,
            HasSkills = e.HasSkills, Skill2 = e.Skill2, Skill3 = e.Skill3,
            ParentA = e.ParentA, ParentB = e.ParentB,
            Strong = Key(e.Strong), Weak = Key(e.Weak),
        };

        private static Egg To(EggSave s) => new Egg(
            s.Id, s.SpeciesId, s.Wild.To(), s.MutationCounter, s.PaletteIndex,
            s.ParentA, s.ParentB, s.Generation, (EggOrigin)s.How,
            s.HasSkills, s.Skill2, s.Skill3, s.Rarity, Key(s.Strong), Key(s.Weak));
    }
}
