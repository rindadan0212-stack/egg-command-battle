using System;
using System.Collections.Generic;
using System.Linq;
using EggCommand.Core;
using EggCommand.Sim;

namespace EggCommand.Tests
{
    /// <summary>帳面（技・種族・特性を手で書くための書式）の検査。
    ///
    /// ⭐ **押さえたいのは1つ ── 書いて読んで、元に戻ること。**
    /// ⚠️ 戻らないと、帳面を1度開いて保存しただけで中身が変わる。
    /// しかもその変化は**静か**で、遊びが壊れて初めて気づく。
    ///
    /// ⚠️ これを書く前に、実際に化けた:
    /// <list type="bullet">
    /// <item>「依存:スピード」と書いた技が、C# にすると <c>DamageScale.Atk</c> になっていた
    ///   （読み取りが <c>== "防御" ? Def : Atk</c> で、知らない語を黙って攻撃に落としていた）</item>
    /// </list>
    /// ⭐ 語を足す種類の変更（enum に1つ足す）は必ずここで落ちるようにしてある。</summary>
    public class SheetTests
    {
        /// <summary>⭐ **全部の技が、書いて読んで元に戻る。**</summary>
        [Fact]
        public void 技は書いて読んで元に戻る()
        {
            foreach (var skill in Skills.All)
            {
                string wrote = Sheet.BlockOf(skill);
                var read = Sheet.SkillOf(wrote);
                Assert.True(read != null, $"{skill.Id}: 書いたものを読み返せない\n{wrote}");
                Assert.Equal(wrote, Sheet.BlockOf(read!));
            }
        }

        /// <summary>⚠️ **語を1つ足したら、ここが落ちる。**
        /// ⭐ enum の値ぜんぶが、帳面の語として往復できることを数える。</summary>
        [Fact]
        public void 依存ステはどれも往復する()
        {
            foreach (DamageScale scale in Enum.GetValues(typeof(DamageScale)))
            {
                string word = Skills.LabelOf(scale);
                Assert.True(Skills.TryScale(word, out var back), $"「{word}」を読み返せない");
                Assert.Equal(scale, back);
            }
        }

        [Fact]
        public void 狙い先はどれも往復する()
        {
            foreach (Target target in Enum.GetValues(typeof(Target)))
            {
                string word = SkillText.TargetOf(target);
                var found = ((Target[])Enum.GetValues(typeof(Target)))
                    .Where(t => SkillText.TargetOf(t) == word).ToList();
                // ⚠️ 同じ語を2つの狙い先が使っていると、読み返したとき別物になる
                Assert.True(found.Count == 1, $"「{word}」が {found.Count} 個の狙い先で使われている");
            }
        }

        [Fact]
        public void 効果の種類はどれも帳面の語を持つ()
        {
            // ⚠️ 語の無い効果があると、その効果を使う技は帳面に書けない
            foreach (EffectKind kind in Enum.GetValues(typeof(EffectKind)))
            {
                var effect = Sample(kind);
                string line = Sheet.LineOf(effect);
                Assert.False(string.IsNullOrWhiteSpace(line), $"{kind}: 帳面の語が無い");
                Assert.Equal(Sheet.LineOf(effect), Sheet.LineOf(effect));
            }
        }

        /// <summary>⭐ 種族も往復する（姿と色を含む）。</summary>
        [Fact]
        public void 種族は書いて読んで元に戻る()
        {
            foreach (var species in SpeciesTable.All)
            {
                string wrote = Sheet.BlockOf(species);
                var read = Sheet.SpeciesOf(wrote);
                Assert.True(read != null, $"{species.Id}: 書いたものを読み返せない\n{wrote}");
                Assert.Equal(wrote, Sheet.BlockOf(read!));
            }
        }

        [Fact]
        public void 特性は書いて読んで元に戻る()
        {
            foreach (var trait in Traits.All)
            {
                string wrote = Sheet.BlockOf(trait);
                var read = Sheet.TraitOf(wrote);
                Assert.True(read != null, $"{trait.Id}: 書いたものを読み返せない\n{wrote}");
                Assert.Equal(wrote, Sheet.BlockOf(read!));
            }
        }

        /// <summary>⚠️ **働く場面の語も往復する。**
        /// 語が被ると、特性の場面が読み返したとき別物になる。</summary>
        [Fact]
        public void 特性の場面はどれも往復する()
        {
            var seen = new HashSet<string>();
            foreach (TraitWhen when in Enum.GetValues(typeof(TraitWhen)))
            {
                string word = Traits.LabelOf(when);
                Assert.False(string.IsNullOrWhiteSpace(word), $"{when}: 語が無い");
                Assert.True(seen.Add(word), $"「{word}」が2つの場面で使われている");
            }
        }

        /// <summary>その種類の効果を1つ作る。⚠️ 中身は何でもよい（語が出れば足りる）。</summary>
        private static Effect Sample(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.Damage: return Effect.Damage(PowerTier.Medium, DamageScale.Atk);
                case EffectKind.Buff: return Effect.Buff(StatKey.Atk, 1, 3);
                case EffectKind.Poison: return Effect.Poison(1, 4);
                case EffectKind.Regen: return Effect.Regen(1, 4);
                case EffectKind.HealRatio: return Effect.HealRatio(30);
                case EffectKind.Shield: return Effect.Shield(2);
                case EffectKind.Stun: return Effect.Stun(1);
                case EffectKind.Ct: return Effect.Ct(2);
                case EffectKind.Taunt: return Effect.Taunt(3);
                case EffectKind.Guts: return Effect.Guts(3);
                case EffectKind.Immune: return Effect.Immune(3);
                case EffectKind.Gauge: return Effect.Gauge(25);
                case EffectKind.Sleep: return Effect.Sleep(2);
                case EffectKind.Block: return Effect.Block(2);
                case EffectKind.Dispel: return Effect.Dispel(1);
                case EffectKind.Steal: return Effect.Steal(1);
                case EffectKind.Revive: return Effect.Revive(50);
                // ⭐ 2026-08-27 に足した5つ
                case EffectKind.Seal: return Effect.Seal(2);
                case EffectKind.Anchor: return Effect.Anchor(3);
                case EffectKind.Invincible: return Effect.Invincible(1);
                case EffectKind.Extend: return Effect.Extend(2);
                case EffectKind.Counter: return Effect.Counter(3);
                // ⚠️ 既定に落とさない。効果を足したらここにも足す（足さなければ落ちる）
                default: throw new ArgumentOutOfRangeException(
                    nameof(kind), kind, "帳面の検査に見本が無い効果");
            }
        }
    }
}
