using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>★（卵のレア度）が、引ける技の格を決める（2026-08-27・作者の指示）。
///
/// ⭐ 唯一の場所は <see cref="Nests"/> の private <c>RollSkills23</c> / <c>CappedPool</c>
/// （<see cref="SkillValues.GradeOf"/> で絞る）。ここは private なので直接は呼べない ──
/// **観測できる出口**（<see cref="Nests.Hatch"/> が返す個体の Skill2/Skill3）から確かめる。
///
/// ⚠️ 野生の卵（<c>hasSkills:false</c>）を直接組み立てて孵す。
/// 巣を経由すると種族が5つ（tamaru/tsunoga/haneru の使い回し）に絞られ、
/// 「どの種族でも」が確かめられない ── <see cref="Nests.Hatch"/> は種族を選ばないので、
/// 全12種族 × 全★を直接検査できる。</summary>
public class EggSkillGradeTests
{
    /// <summary>その★で、種族 <paramref name="speciesId"/> を何回か孵して両方の枠を集める。
    /// ⚠️ 系統を variantごとに分けて何本か引く（1回だけだと「たまたま」を見逃す）。</summary>
    private static List<(string? Skill2, string? Skill3)> HatchMany(string speciesId, int rarity, int count)
    {
        var results = new List<(string?, string?)>();
        for (int i = 0; i < count; i++)
        {
            var rng = new Rng(1000 + i).Stream($"grade-test:{speciesId}:{rarity}:{i}");
            var egg = new Egg($"e{i}", speciesId, new StatBlock(0, 0, 0, 0, 0, 0), 0,
                null, null, 1, EggOrigin.Defeated,
                hasSkills: false, skill2: null, skill3: null, rarity: rarity);
            var creature = Nests.Hatch(rng, egg, $"c{i}");
            results.Add((creature.Skill2, creature.Skill3));
        }
        return results;
    }

    /// <summary>⭐ ボスは配らないので対象外（<see cref="Encounters.BossSpeciesId"/>）。</summary>
    private static IEnumerable<Species> PlayableSpecies()
    {
        foreach (var species in SpeciesTable.All)
        {
            if (species.Id == Encounters.BossSpeciesId) continue;
            yield return species;
        }
    }

    /// <summary>🔴 **★N の卵から、格N を超える技は出ない。**
    /// ⚠️ 全12種族 × ★1〜5 の全組で確かめる（1種族・1★だけだと、たまたま
    /// 通っただけの見落としが起きる）。</summary>
    [Fact]
    public void 星Nの卵から格Nを超える技は出ない()
    {
        var broken = new List<string>();
        foreach (var species in PlayableSpecies())
        {
            for (int rarity = 1; rarity <= Rarities.Max; rarity++)
            {
                foreach (var (skill2, skill3) in HatchMany(species.Id, rarity, 6))
                {
                    foreach (var id in new[] { skill2, skill3 })
                    {
                        if (id == null) continue;
                        int grade = SkillValues.GradeOf(Skills.ById(id));
                        if (grade > rarity)
                        {
                            broken.Add($"{species.Id} ★{rarity}: {id}（格{grade}）");
                        }
                    }
                }
            }
        }
        Assert.True(broken.Count == 0,
            "格が★を超えた: " + string.Join(" / ", broken));
    }

    /// <summary>🔴 **どの★・どの種族でも、枠2・枠3 が空にならない。**
    /// ⚠️ 格で絞った結果0本になっても、<c>CappedPool</c> がそのプールの最低格へ
    /// 落とすので空き枠にはならない ── ここが効いていることの確認。</summary>
    [Fact]
    public void どの星どの種族でも枠2枠3が空にならない()
    {
        var empty = new List<string>();
        foreach (var species in PlayableSpecies())
        {
            for (int rarity = 1; rarity <= Rarities.Max; rarity++)
            {
                foreach (var (skill2, skill3) in HatchMany(species.Id, rarity, 6))
                {
                    if (skill2 == null) empty.Add($"{species.Id} ★{rarity}: 枠2が空");
                    if (skill3 == null) empty.Add($"{species.Id} ★{rarity}: 枠3が空");
                }
            }
        }
        Assert.True(empty.Count == 0, "空き枠があった: " + string.Join(" / ", empty));
    }
}
