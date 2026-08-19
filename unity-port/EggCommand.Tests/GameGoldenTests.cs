using System.Collections.Generic;
using EggCommand.Core;

namespace EggCommand.Tests;

public class NestGoldenTests
{
    /// <summary>⚠️ 素質の合計上限を ×2 → ×3 にした（2026-08-18）ので、段ごとの総量も 1.5倍になった。
    /// ⭐ **坂の形は1つも変えていない** ── そこをここで固定する。
    /// ⚠️ 段だけを勝手に緩めたり急にしたりしたら、比が崩れて落ちる。</summary>
    private const double TierScale = 3.0 / 2.0;

    [Fact]
    public void 段階ごとの素質合計が一致する()
    {
        var golden = Golden.Load("nest");
        foreach (var entry in golden.GetProperty("tiers").EnumerateArray())
        {
            int tier = entry.GetProperty("tier").GetInt32();
            int ported = entry.GetProperty("wildTotal").GetInt32();
            Assert.Equal((int)System.Math.Floor(ported * TierScale + 0.5), Nests.WildTotalForTier(tier));
        }
        // ⭐ 最終段はいまも「1体で振り切れる量」ちょうど
        Assert.Equal(Stats.WildTotalMax, Nests.WildTotalForTier(5));
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

    /// <summary>⚠️ **素質を配る乱数の消費が変わった**（2026-08-18）。
    ///
    /// 素質が4本から6本になったので、<c>Nests.SpreadWild</c> の並べ替えも
    /// <c>Breeding.Breed</c> の継承判定も、引く回数そのものが増えた。
    /// ⭐ つまり**素質より後に引く全部**（技・色・変異・孵化）が移植元とは別の系列になる。
    /// これは移植のミスではなく、素質を足すと決めた時点で必ずこうなる。
    ///
    /// ⚠️ **ゴールデンは作り直さない。**作り直すと「移植元と一致している」証明が消える。
    /// 消えるのは「同じ乱数から同じ個体が出る」証明だけで、
    /// 乱数そのもの・技表・種族表・戦闘式・削り方の証明は1つも失われていない
    /// （それぞれ別のゴールデンで見続けている）。
    ///
    /// ⭐ 系列そのものの取りこぼしは SeriesRecordTests（現行の記録）が受け持つ。</summary>
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
                // ⚠️ 素質と、素質より後に引く技は別系列（上の注記）。
                // ⭐ 代わりに「段の総量を超えていない」ことを見る
                Assert.True(Creatures.WildTotalOf(unit) <= Nests.WildTotalForTier(nest.Tier),
                    $"{where}: 素質合計が {Creatures.WildTotalOf(unit)}");
                Assert.NotNull(unit.Skill2);
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
            Assert.Equal(eggJson.GetProperty("generation").GetInt32(), egg.Generation);
            // ⭐ 野生の卵は技が未定（孵すときにガチャ）
            Assert.False(egg.HasSkills);
            // ⚠️ 素質・変異・技は別系列（クラスの注記）。⭐ 孵しても素質が変わらないことは見続ける
            var before = egg.Wild;
            var hatched = Nests.Hatch(rng, egg, "c007");
            Assert.Equal(entry.GetProperty("hatched").GetProperty("id").GetString(), hatched.Id);
            Assert.Equal(before, hatched.Wild);
            Assert.True(hatched.Skill2 != null, $"{where}: 孵しても技が決まっていない");
        }
    }

    /// <summary>⚠️ ヌシに抵抗 24・命中 8 を足した（2026-08-18）。
    /// ⭐ 移植元にある4本（HP・攻・防・速）は**1つも動かしていない**ので、そこは丸ごと照合する。</summary>
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
            var ported = Golden.Block(entry.GetProperty("wild"));
            Assert.True(ported.Hp == unit.Wild.Hp && ported.Atk == unit.Wild.Atk
                && ported.Def == unit.Wild.Def && ported.Spd == unit.Wild.Spd,
                $"ボスの素質（移植元の4本）が {unit.Wild}");
            // ⚠️ 足した2本。⭐ 抵抗が 0 に戻ったら、弱化を積むだけでヌシが止まる
            Assert.Equal(8, unit.Wild.Acc);
            Assert.Equal(24, unit.Wild.Res);
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

    /// <summary>⚠️ 素質が6本になり、継承の判定を引く回数が 4→6 に増えた（2026-08-18）。
    /// ⭐ 較正済みの「変異 2.5%×3回」という**決め事**は 配合の定数が一致する が見続ける。
    /// ここは系列が別になったので、比べられるのは形だけ。
    /// 系列の取りこぼしは SeriesRecordTests（現行の記録）が受け持つ。</summary>
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
            Assert.True(Stats.TotalOf(creature.Wild) <= Stats.WildTotalMax,
                $"親 {creature.Id} の素質合計が {Stats.TotalOf(creature.Wild)}");
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
            // ⚠️ 当たり外れの並びは別系列。⭐ 回数の上限（3回まで）は決め事なので見続ける
            Assert.InRange(outcome.Mutations, 0, Breeding.MutationRolls);

            var eggJson = entry.GetProperty("egg");
            Assert.Equal(eggJson.GetProperty("id").GetString(), outcome.Egg.Id);
            // ⭐ 世代は乱数を通らない。⚠️ ここが動いたら血統の数え方が壊れている
            Assert.Equal(eggJson.GetProperty("generation").GetInt32(), outcome.Egg.Generation);
            Assert.Equal(System.Math.Max(a.MutationCounter, b.MutationCounter) + outcome.Mutations,
                outcome.Egg.MutationCounter);
            // ⭐ 配合の卵は技が決まっている。孵すときに引き直さない
            Assert.True(outcome.Egg.HasSkills);
            // ⭐ 上限は変異ぶん押し上がる（押し上げないと変異の +2 が即削られる）
            Assert.True(Stats.TotalOf(outcome.Egg.Wild)
                <= Stats.WildTotalMaxFor(outcome.Egg.MutationCounter), $"{where}: 合計が上限超え");
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
    /// 系統ごとの乱数（nest / egg / hatch / steal / breed）が**取り違えられていない**かが出る。
    /// ⚠️ 中身の数（素質・技）は別系列になった ── NestGoldenTests の注記を見る。</summary>
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
            Assert.True(entry.GetProperty("generation").GetInt32() == creature.Generation, $"{spot}: 世代");
            Assert.True(entry.GetProperty("earned").GetInt32() == creature.Earned, $"{spot}: 育成点");
            Assert.True(Stats.TotalOf(creature.Wild) <= Stats.WildTotalMaxFor(creature.MutationCounter),
                $"{spot}: 素質合計が {Stats.TotalOf(creature.Wild)}");
        }

        var eggs = expected.GetProperty("eggs");
        Assert.True(eggs.GetArrayLength() == game.Eggs.Count, $"{where}: 卵の数が {game.Eggs.Count}");
        i = 0;
        foreach (var entry in eggs.EnumerateArray())
        {
            var egg = game.Eggs[i++];
            Assert.True(entry.GetProperty("id").GetString() == egg.Id, $"{where}: 卵 id");
            Assert.True(Golden.Origin(entry.GetProperty("how").GetString()!) == egg.How, $"{where}: 入手経路");
        }

        // ⭐ プレイヤーが選んだ枠は乱数を通らない。ここは丸ごと照合する
        //
        // ⚠️ **編成は 1本 → 4本（放置1＋巣3）に分けた**（2026-08-18）。
        //    移植元の 1本 は「戦闘に出す編成」なので、**巣の編成**と突き合わせる。
        //    ⭐ ゴールデンの値は変えていない ── 読む場所だけを移した。
        var roster = Games.RosterOf(game, PartyKind.Nest);
        Assert.Equal(Golden.Strings(expected.GetProperty("party")), roster);

        // ⚠️ 空き枠は「素質の高い順」で埋まるので、素質が別系列になれば並びも変わる。
        // ⭐ 見続けるのは「選んだ枠が必ず先頭に来る」「保管にある個体だけで埋まる」の2つ。
        var partyOf = new List<string>();
        foreach (var c in Games.PartyOf(game)) partyOf.Add(c.Id);
        Assert.True(Golden.Strings(expected.GetProperty("partyOf")).Count == partyOf.Count,
            $"{where}: 出撃数が {partyOf.Count}");
        for (int k = 0; k < roster.Count && k < partyOf.Count; k++)
        {
            Assert.True(roster[k] == partyOf[k], $"{where}: 選んだ枠が先頭に来ていない");
        }
        foreach (var id in partyOf)
        {
            Assert.True(System.Linq.Enumerable.Any(game.Storage.Creatures, c => c.Id == id), $"{where}: 保管に無い {id}");
        }
    }
}
