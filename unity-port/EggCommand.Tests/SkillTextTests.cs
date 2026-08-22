using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>技の説明文と、味方1体に配る技の狙い先。
///
/// ⚠️ **どちらも検査が1件も無かった。**
/// <see cref="SkillText"/> は 図鑑.html と Wiki の技一覧の本文を作っている本体で、
/// 47技すべての説明が無検査だった。未知の値で throw する作りなので、
/// 効果を1つ足して <c>NameOf</c> に書き忘れると**出荷物の生成が落ちる**。
///
/// ⭐ 文面そのものは固定しない（言い回しは変える）。落ちないことと、
/// **効果の数だけ節がある**ことだけを見る。
/// </summary>
public class SkillTextTests
{
    /// <summary>⚠️ 47技すべてを通す。⭐ 効果を足した日にここが最初に落ちる。</summary>
    [Fact]
    public void 全ての技が説明文になる()
    {
        foreach (var skill in Skills.All)
        {
            string line = SkillText.Describe(skill);
            Assert.False(string.IsNullOrWhiteSpace(line), $"{skill.Name}: 説明が空");
            Assert.DoesNotContain("Effect", line);       // enum 名が漏れていない
            Assert.DoesNotContain("Target", line);
        }
    }

    /// <summary>⚠️ 🔴 **長押しの本文にも検査が無かった。**
    ///
    /// ⭐ 2026-08-22 に <c>SkillGain.Innate</c>（パッシブ技の伸び代）を足した日、
    /// <see cref="SkillText.GainOf"/> に言い方を書き忘れていて、
    /// **パッシブ技を長押しすると例外で落ちていた**（BOX・図鑑の両方から届く道）。
    ///
    /// ⚠️ 上の「全ての技が説明文になる」は `Describe` しか通さないので、素通りしていた。
    /// ⭐ 見つけたのは web の採寸ページ ── **全技を1度に通した**から出た。</summary>
    [Fact]
    public void 全ての技の伸び方が言葉になる()
    {
        foreach (var skill in Skills.All)
        {
            // ⭐ 枠を問わない（図鑑）と、枠1（CT の段が消える）の両方を通す
            foreach (int slot in new[] { -1, 0, 1 })
            {
                string line = SkillText.StepsOf(skill, slot);
                Assert.DoesNotContain("SkillGain", line);
            }
        }
    }

    /// <summary>⚠️ **軸を足した日に、ここが最初に落ちる。**
    /// ⭐ 技に付いているかどうかに関わらず、全部の軸に言い方があること。</summary>
    [Fact]
    public void 伸びる軸に呼び名が無いものは無い()
    {
        foreach (SkillGain gain in System.Enum.GetValues(typeof(SkillGain)))
        {
            Assert.False(string.IsNullOrEmpty(SkillText.GainOf(gain)), $"{gain} に呼び名が無い");
        }
    }

    /// <summary>⭐ 効果の名前・狙い先の呼び名は、全種類そろっている。
    /// ⚠️ 揃っていないと図鑑の生成が例外で止まる（未知の値は throw する作り）。</summary>
    [Fact]
    public void 効果と狙い先に呼び名が無いものは無い()
    {
        var seen = new HashSet<EffectKind>();
        foreach (var skill in Skills.All)
        {
            foreach (var effect in skill.Effects)
            {
                Assert.False(string.IsNullOrEmpty(SkillText.NameOf(effect)),
                    $"{effect.Kind} に名前が無い");
                seen.Add(effect.Kind);
            }
        }

        // ⭐ **仕組みだけ在って技に付いていない効果は無い。**
        // ⚠️ 在ると「遊べない機能」が残り、Wiki にも「まだ付いていません」と書き続けることになる
        foreach (EffectKind kind in System.Enum.GetValues(typeof(EffectKind)))
        {
            Assert.True(seen.Contains(kind), $"{kind} を持つ技が1本も無い");
        }

        foreach (Target target in System.Enum.GetValues(typeof(Target)))
        {
            Assert.False(string.IsNullOrEmpty(SkillText.TargetOf(target)), $"{target} に呼び名が無い");
        }
    }

    /// <summary>⚠️ 威力はダメージが出る技にだけ書く（それ以外は空欄、という約束）。</summary>
    [Fact]
    public void 威力はダメージが出る技にだけ付く()
    {
        foreach (var skill in Skills.All)
        {
            bool hurts = false;
            foreach (var effect in skill.Effects)
            {
                if (effect.Kind == EffectKind.Damage) hurts = true;
            }
            string power = SkillText.PowerOf(skill);
            Assert.True(hurts == !string.IsNullOrEmpty(power),
                $"{skill.Name}: ダメージ {hurts} なのに威力「{power}」");
        }
    }

    /// <summary>⚠️ **挑発だけ単位が「回」。**T は「その個体の行動回数」だが、
    /// 挑発が数えるのは**相手が単体技を撃った回数**なので、T と書くと意味が違う。</summary>
    [Fact]
    public void 挑発の単位は回()
    {
        var taunt = Skills.ById("taunt");
        string line = SkillText.Describe(taunt);
        Assert.Contains("回", line);
        Assert.DoesNotContain("回T", line);
    }

    // ── 味方1体に配る技の狙い先 ──────────────────────

    private static Creature Make(string id, int hp, int atk, int def, int spd) =>
        new Creature(id, "tamaru", new StatBlock(hp, atk, def, spd),
            new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1);

    private static BattleState Trio()
    {
        var allies = new List<Creature>
        {
            Make("a", 30, 10, 10, 10),   // 攻撃力が一番低い
            Make("b", 30, 40, 10, 10),   // ⭐ 攻撃力が一番高い
            Make("c", 30, 20, 10, 10),
        };
        var foes = new List<Creature> { Make("x", 30, 10, 10, 10) };
        return Battle.CreateBattle(allies, foes);
    }

    /// <summary>⭐ **伸ばす札は、それが一番活きる味方へ。**
    /// ⚠️ 一律に「一番弱った味方」へ落としていた頃は、攻撃力UP が壁役に乗っていた。</summary>
    [Fact]
    public void 攻撃力UPは攻撃力が一番高い味方に乗る()
    {
        var state = Trio();
        var actor = state.Units.Find(u => u.Creature.Id == "a")!;
        var landed = Battle.AllyLandingFor(state, actor, Skills.ById("atk-up"));

        Assert.NotNull(landed);
        Assert.Equal("b", landed!.Creature.Id);
    }

    /// <summary>⭐ 手当ては一番弱った味方へ（伸ばす札とは規則が違う）。</summary>
    [Fact]
    public void 回復は一番弱った味方に乗る()
    {
        var state = Trio();
        var hurt = state.Units.Find(u => u.Creature.Id == "c")!;
        hurt.Hp = 1;
        var actor = state.Units.Find(u => u.Creature.Id == "a")!;
        var landed = Battle.AllyLandingFor(state, actor, Skills.ById("heal-ratio"));

        Assert.NotNull(landed);
        Assert.Equal("c", landed!.Creature.Id);
    }

    /// <summary>⚠️ プレイヤーが選んだときは、その選択が常に勝つ。</summary>
    [Fact]
    public void 選んだ味方があればそちらに乗る()
    {
        var state = Trio();
        var actor = state.Units.Find(u => u.Creature.Id == "a")!;
        var pick = state.Units.Find(u => u.Creature.Id == "c")!;
        var landed = Battle.TargetsFor(state, actor, Target.AllyOne, pick);

        Assert.Single(landed);
        Assert.Equal("c", landed[0].Creature.Id);
    }

    /// <summary>⭐ 「選ぶ」の札を出すかどうかの門番。⚠️ 味方に配る技は味方を選ばせる。</summary>
    [Fact]
    public void 狙いを選ばせる技の見分けが付く()
    {
        Assert.True(Battle.NeedsTarget(Skills.ById("atk-up")));
        Assert.True(Battle.TargetsAlly(Skills.ById("atk-up")));

        Assert.True(Battle.NeedsTarget(Skills.ById("attack")));
        Assert.False(Battle.TargetsAlly(Skills.ById("attack")));

        // 全体技は選ばせない
        Assert.False(Battle.NeedsTarget(Skills.ById("attack-all")));
    }

    /// <summary>⚠️ 枠2と枠3は**別のプール**から引く（狙った組み合わせを出しやすくするため）。
    /// ⭐ slot に 1 以外を渡すと全部 枠3 になる作りなので、境界を1つ固定しておく。</summary>
    [Fact]
    public void 枠ごとにプールが分かれている()
    {
        var species = SpeciesTable.ById("tamaru");
        var slot2 = Skills.SlotPoolOf("tamaru", 1, species.Skill1);
        var slot3 = Skills.SlotPoolOf("tamaru", 2, species.Skill1);

        Assert.NotEmpty(slot2);
        Assert.NotEmpty(slot3);
        // ⚠️ **型の一致はもう見ない**（2026-08-19 に袋の型縛りを外した）。
        //    ⭐ 袋の不変条件は GoldenTests.袋の不変条件 が数えている。
        foreach (string id in slot2) Assert.NotNull(Skills.ById(id));
        foreach (string id in slot3) Assert.NotNull(Skills.ById(id));
        // ⚠️ 枠1（種族固定）は、どちらのプールにも入らない
        Assert.DoesNotContain(species.Skill1, slot2);
        Assert.DoesNotContain(species.Skill1, slot3);
    }
}
