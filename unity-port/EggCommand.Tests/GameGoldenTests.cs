using System.Collections.Generic;
using EggCommand.Core;

namespace EggCommand.Tests;

public class NestGoldenTests
{
    [Fact]
    public void 段階ごとの素質合計が一致する()
    {
        var golden = Golden.Load("nest");
        foreach (var entry in golden.GetProperty("tiers").EnumerateArray())
        {
            int tier = entry.GetProperty("tier").GetInt32();
            Assert.Equal(entry.GetProperty("wildTotal").GetInt32(), Nests.WildTotalForTier(tier));
        }
    }

    /// <summary>⭐ id で引く。並び順も件数も見ない（巣を足しても落ちない）。
    /// ⚠️ ただし <c>Nests.All[0]</c> は最初の3体の出所なので、**先頭を入れ替えると game の照合が落ちる**。
    /// 足すときは後ろへ足す。</summary>
    [Fact]
    public void 移植した巣が1つも変わっていない()
    {
        var golden = Golden.Load("nest");
        var list = golden.GetProperty("nests");
        foreach (var entry in list.EnumerateArray())
        {
            var nest = Nests.ById(entry.GetProperty("id").GetString()!);
            Assert.Equal(entry.GetProperty("id").GetString(), nest.Id);
            Assert.Equal(entry.GetProperty("name").GetString(), nest.Name);
            Assert.Equal(entry.GetProperty("speciesId").GetString(), nest.SpeciesId);
            Assert.Equal(entry.GetProperty("tier").GetInt32(), nest.Tier);
        }
        Nests.Audit();
    }

    /// <summary>⚠️ 乱数の消費順がそのまま出る。ここがずれたら以降の全部がずれる。</summary>
    [Fact]
    public void 巣の守り手が一致する()
    {
        var golden = Golden.Load("nest");
        foreach (var entry in golden.GetProperty("defenders").EnumerateArray())
        {
            string nestId = entry.GetProperty("nest").GetString()!;
            var nest = Nests.ById(nestId);
            var rng = new Rng(777).Stream(nestId);
            var units = Nests.MakeDefenders(rng, nest);

            var expected = entry.GetProperty("units");
            Assert.Equal(expected.GetArrayLength(), units.Count);
            int i = 0;
            foreach (var unitJson in expected.EnumerateArray())
            {
                var unit = units[i++];
                string where = $"{nestId}[{i - 1}]";
                Assert.Equal(unitJson.GetProperty("id").GetString(), unit.Id);
                Assert.Equal(unitJson.GetProperty("speciesId").GetString(), unit.SpeciesId);
                Assert.True(Golden.Block(unitJson.GetProperty("wild")).Equals(unit.Wild),
                    $"{where}: 素質が {unit.Wild}");
                Golden.SameSkills23(unitJson.GetProperty("skills23"), unit.Skill2, unit.Skill3, where);
                Assert.Equal(unitJson.GetProperty("wildTotal").GetInt32(), Creatures.WildTotalOf(unit));
                Assert.True(Golden.Block(unitJson.GetProperty("actual")).Equals(Creatures.StatsOf(unit)),
                    $"{where}: 実値が {Creatures.StatsOf(unit)}");
            }
        }
    }

    [Fact]
    public void 卵と孵化が一致する()
    {
        var golden = Golden.Load("nest");
        foreach (var entry in golden.GetProperty("eggs").EnumerateArray())
        {
            string nestId = entry.GetProperty("nest").GetString()!;
            var how = Golden.Origin(entry.GetProperty("how").GetString()!);
            var rng = new Rng(4242).Stream(nestId + entry.GetProperty("how").GetString());

            var egg = Nests.MakeEgg(rng, Nests.ById(nestId), how, 7);
            var eggJson = entry.GetProperty("egg");
            string where = $"{nestId}/{how}";

            Assert.Equal(eggJson.GetProperty("id").GetString(), egg.Id);
            Assert.Equal(eggJson.GetProperty("speciesId").GetString(), egg.SpeciesId);
            Assert.True(Golden.Block(eggJson.GetProperty("wild")).Equals(egg.Wild), $"{where}: 卵の素質が {egg.Wild}");
            Assert.Equal(eggJson.GetProperty("mutationCounter").GetInt32(), egg.MutationCounter);
            Assert.Equal(eggJson.GetProperty("generation").GetInt32(), egg.Generation);
            // ⭐ 野生の卵は技が未定（孵すときにガチャ）
            Assert.False(egg.HasSkills);

            var hatched = Nests.Hatch(rng, egg, "c007");
            var hatchedJson = entry.GetProperty("hatched");
            Assert.Equal(hatchedJson.GetProperty("id").GetString(), hatched.Id);
            Assert.True(Golden.Block(hatchedJson.GetProperty("wild")).Equals(hatched.Wild),
                $"{where}: 孵した素質が {hatched.Wild}");
            Golden.SameSkills23(hatchedJson.GetProperty("skills23"), hatched.Skill2, hatched.Skill3, where);
        }
    }

    [Fact]
    public void ボスが一致する()
    {
        var golden = Golden.Load("nest");
        Assert.Equal(golden.GetProperty("bossName").GetString(), Nests.BossName);

        var boss = Nests.MakeBossParty();
        var expected = golden.GetProperty("boss");
        Assert.Equal(expected.GetArrayLength(), boss.Count);
        int i = 0;
        foreach (var entry in expected.EnumerateArray())
        {
            var unit = boss[i++];
            Assert.Equal(entry.GetProperty("id").GetString(), unit.Id);
            Assert.Equal(entry.GetProperty("speciesId").GetString(), unit.SpeciesId);
            Assert.True(Golden.Block(entry.GetProperty("wild")).Equals(unit.Wild), $"ボスの素質が {unit.Wild}");
            Assert.Equal(entry.GetProperty("mutationCounter").GetInt32(), unit.MutationCounter);
            Golden.SameSkills23(entry.GetProperty("skills23"), unit.Skill2, unit.Skill3, "boss");
        }
    }
}

public class BreedingGoldenTests
{
    [Fact]
    public void 配合の定数が一致する()
    {
        var golden = Golden.Load("breeding");
        Assert.Equal(golden.GetProperty("inheritHigher").GetDouble(), Breeding.InheritHigher);
        Assert.Equal(golden.GetProperty("mutationRolls").GetInt32(), Breeding.MutationRolls);
        Assert.Equal(golden.GetProperty("mutationChance").GetDouble(), Breeding.MutationChance);
        Assert.Equal(golden.GetProperty("mutationStep").GetInt32(), Breeding.MutationStep);
        Assert.Equal(golden.GetProperty("mutationCounterLimit").GetInt32(), Breeding.MutationCounterLimit);
    }

    /// <summary>⭐ 較正済みの「変異 2.5%×3回」がここに乗っている。
    /// 乱数の消費が1つでもずれたら、出る子が変わる。</summary>
    [Fact]
    public void 配合の結果が一致する()
    {
        var golden = Golden.Load("breeding");
        var game = Games.NewGame(20260816);
        var pool = new List<Creature>(game.Storage.Creatures);

        // 親が golden と同じであることを先に確かめる（違えば以降は比べる意味が無い）
        var parents = golden.GetProperty("parents");
        Assert.Equal(parents.GetArrayLength(), pool.Count);
        int p = 0;
        foreach (var entry in parents.EnumerateArray())
        {
            var creature = pool[p++];
            Assert.Equal(entry.GetProperty("id").GetString(), creature.Id);
            Assert.True(Golden.Block(entry.GetProperty("wild")).Equals(creature.Wild),
                $"親 {creature.Id} の素質が {creature.Wild}");
        }

        foreach (var entry in golden.GetProperty("bred").EnumerateArray())
        {
            int seed = entry.GetProperty("seed").GetInt32();
            string aId = entry.GetProperty("a").GetString()!;
            string bId = entry.GetProperty("b").GetString()!;
            var a = pool.Find(c => c.Id == aId)!;
            var b = pool.Find(c => c.Id == bId)!;

            // 通し番号は golden の卵 id（"e100"）から取る
            int serial = int.Parse(entry.GetProperty("egg").GetProperty("id").GetString()!.Substring(1));
            var rng = new Rng(seed).Stream("breed");
            var outcome = Breeding.Breed(rng, a, b, serial);

            string where = $"seed={seed} {aId}×{bId}";
            Assert.True(entry.GetProperty("mutations").GetInt32() == outcome.Mutations,
                $"{where}: 変異回数が {outcome.Mutations}");

            var eggJson = entry.GetProperty("egg");
            Assert.Equal(eggJson.GetProperty("id").GetString(), outcome.Egg.Id);
            Assert.Equal(eggJson.GetProperty("speciesId").GetString(), outcome.Egg.SpeciesId);
            Assert.True(Golden.Block(eggJson.GetProperty("wild")).Equals(outcome.Egg.Wild),
                $"{where}: 子の素質が {outcome.Egg.Wild}");
            Assert.Equal(eggJson.GetProperty("mutationCounter").GetInt32(), outcome.Egg.MutationCounter);
            Assert.Equal(eggJson.GetProperty("paletteIndex").GetInt32(), outcome.Egg.PaletteIndex);
            Assert.Equal(eggJson.GetProperty("generation").GetInt32(), outcome.Egg.Generation);
            // ⭐ 配合の卵は技が決まっている。孵すときに引き直さない
            Assert.True(outcome.Egg.HasSkills);
            Golden.SameSkills23(eggJson.GetProperty("skills23"), outcome.Egg.Skill2, outcome.Egg.Skill3, where);
        }
    }
}

public class GameGoldenTests
{
    [Fact]
    public void 定数が一致する()
    {
        var golden = Golden.Load("game");
        Assert.Equal(golden.GetProperty("partySize").GetInt32(), Games.PartySize);
        Assert.Equal(golden.GetProperty("storageSlots").GetInt32(), Storages.StorageSlots);
        Assert.Equal(golden.GetProperty("trainMax").GetInt32(), Creatures.TrainMax);
    }

    /// <summary>⭐ newGame から一連の操作までを丸ごと。
    /// 系統ごとの乱数（nest / egg / hatch / steal / breed）がずれていないかが出る。</summary>
    [Fact]
    public void ゲームの進行が一致する()
    {
        var golden = Golden.Load("game");
        foreach (var run in golden.GetProperty("runs").EnumerateArray())
        {
            int seed = run.GetProperty("seed").GetInt32();
            var game = Games.NewGame(seed);
            var steps = run.GetProperty("steps");
            int index = 0;

            foreach (var stepJson in steps.EnumerateArray())
            {
                string step = stepJson.GetProperty("step").GetString()!;
                switch (step)
                {
                    case "newGame":
                        break;
                    case "gainEgg":
                        Games.GainEgg(game, Nests.ById("thicket-fang"), EggOrigin.Defeated);
                        break;
                    case "hatchEgg":
                        Games.HatchEgg(game, game.Eggs[0].Id);
                        break;
                    case "breedPair":
                    {
                        var ids = new List<string>();
                        foreach (var c in game.Storage.Creatures) ids.Add(c.Id);
                        Games.BreedPair(game, ids[0], ids[1]);
                        break;
                    }
                    case "toggleParty":
                    {
                        var ids = new List<string>();
                        foreach (var c in game.Storage.Creatures) ids.Add(c.Id);
                        Games.TogglePartyMember(game, ids[2]);
                        Games.TogglePartyMember(game, ids[0]);
                        break;
                    }
                    case "awardParty":
                        Games.AwardParty(Games.PartyOf(game), 2);
                        break;
                    default:
                        throw new System.InvalidOperationException($"知らない手順: {step}");
                }

                SameState(stepJson.GetProperty("state"), game, $"seed={seed} step={step}({index})");
                index++;
            }
        }
    }

    private static void SameState(System.Text.Json.JsonElement expected, Game game, string where)
    {
        Assert.True(expected.GetProperty("serial").GetInt32() == game.Serial,
            $"{where}: 通し番号が {game.Serial}");

        var creatures = expected.GetProperty("creatures");
        Assert.True(creatures.GetArrayLength() == game.Storage.Creatures.Count,
            $"{where}: 保管数が {game.Storage.Creatures.Count}（期待 {creatures.GetArrayLength()}）");
        int i = 0;
        foreach (var entry in creatures.EnumerateArray())
        {
            var creature = game.Storage.Creatures[i++];
            string spot = $"{where}/{creature.Id}";
            Assert.True(entry.GetProperty("id").GetString() == creature.Id, $"{spot}: id");
            Assert.True(entry.GetProperty("speciesId").GetString() == creature.SpeciesId, $"{spot}: 種族");
            Assert.True(Golden.Block(entry.GetProperty("wild")).Equals(creature.Wild), $"{spot}: 素質が {creature.Wild}");
            Assert.True(entry.GetProperty("mutationCounter").GetInt32() == creature.MutationCounter, $"{spot}: 変異");
            Assert.True(entry.GetProperty("generation").GetInt32() == creature.Generation, $"{spot}: 世代");
            Assert.True(entry.GetProperty("earned").GetInt32() == creature.Earned, $"{spot}: 育成点");
            Golden.SameSkills23(entry.GetProperty("skills23"), creature.Skill2, creature.Skill3, spot);
        }

        var eggs = expected.GetProperty("eggs");
        Assert.True(eggs.GetArrayLength() == game.Eggs.Count, $"{where}: 卵の数が {game.Eggs.Count}");
        i = 0;
        foreach (var entry in eggs.EnumerateArray())
        {
            var egg = game.Eggs[i++];
            Assert.True(entry.GetProperty("id").GetString() == egg.Id, $"{where}: 卵 id");
            Assert.True(entry.GetProperty("speciesId").GetString() == egg.SpeciesId, $"{where}: 卵の種族");
            Assert.True(Golden.Block(entry.GetProperty("wild")).Equals(egg.Wild), $"{where}: 卵の素質が {egg.Wild}");
            Assert.True(Golden.Origin(entry.GetProperty("how").GetString()!) == egg.How, $"{where}: 入手経路");
        }

        Assert.Equal(Golden.Strings(expected.GetProperty("party")), game.Party);

        var partyOf = new List<string>();
        foreach (var c in Games.PartyOf(game)) partyOf.Add(c.Id);
        Assert.True(Golden.Strings(expected.GetProperty("partyOf")).Count == partyOf.Count
            && string.Join(",", Golden.Strings(expected.GetProperty("partyOf"))) == string.Join(",", partyOf),
            $"{where}: 出撃が {string.Join(",", partyOf)}");
    }
}
