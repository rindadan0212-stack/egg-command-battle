#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>素質が働く場面。
    ///
    /// ⭐ **語彙をここで固定する。** 素質は「戦闘のあちこちに割り込むもの」なので、
    /// 条件を自由に書けるようにすると <see cref="Battle"/> が素質だらけになる。
    /// 割り込む場所をこの数に限れば、フックも同じ数で足りる。
    ///
    /// ⚠️ 増やす前に、既にある条件で書けないか必ず疑う（効果のプリミティブと同じ約束）。</summary>
    public enum TraitWhen
    {
        /// <summary>常に効いている。</summary>
        Always,
        /// <summary>戦闘が始まったとき1回。</summary>
        BattleStart,
        /// <summary>攻撃を当てたとき。</summary>
        OnHit,
        /// <summary>攻撃を受けたとき。</summary>
        OnHurt,
        /// <summary>盾が1枚剥がれたとき。</summary>
        OnShieldBreak,
        /// <summary>倒れる一撃を受けたとき。</summary>
        OnDown,
    }

    /// <summary>個体が1つ持つ素質。⭐ **技の3枠とは別枠**（枠を奪わない）。
    ///
    /// ⭐ これがある理由: 技を選ぶ側（個体）に個性が無いと、
    /// どの個体にどの技を付けても同じように働いてしまい、**判断が生まれない**。
    /// 素質は「特定の技・特定の動きだけを強くする」ので、
    /// 「この個体には低確率の大技を持たせる」という組み合わせの判断ができる。
    ///
    /// ⚠️ 素質は**技そのものを強くしない**。強くするのは「動き」のほう。
    /// 技を直に強くすると、結局その技を持つのが正解、で終わる。</summary>
    public sealed class Trait
    {
        public readonly string Id;
        public readonly string Name;
        public readonly TraitWhen When;
        /// <summary>画面に出す短い説明。⚠️ 凝った言い回しにしない。</summary>
        public readonly string Gist;
        /// <summary>何と噛み合うか。⭐ **図鑑に出す。**
        /// 素質の値打ちは単体では読めず、組み合わせでしか読めないため。</summary>
        public readonly string Pairs;

        public Trait(string id, string name, TraitWhen when, string gist, string pairs)
        {
            Id = id;
            Name = name;
            When = when;
            Gist = gist;
            Pairs = pairs;
        }
    }

    /// <summary>素質表。
    ///
    /// ⚠️ **まだ戦闘に繋がっていない。** ここにあるのは「何を作るか」を決めるための一覧で、
    /// <see cref="Battle"/> 側のフックはまだ無い。図鑑に出して形を見てから実装する。
    /// ⚠️ 繋いでいないものを増やさないこと（繋いだ数と表の数は <see cref="Audit"/> が数える）。</summary>
    public static class Traits
    {
        /// <summary>いま戦闘に繋がっている素質の数。⚠️ 実装したらここを上げる。</summary>
        public const int Wired = 0;

        public static string LabelOf(TraitWhen when)
        {
            switch (when)
            {
                case TraitWhen.Always: return "常時";
                case TraitWhen.BattleStart: return "戦闘開始時";
                case TraitWhen.OnHit: return "攻撃を当てたとき";
                case TraitWhen.OnHurt: return "攻撃を受けたとき";
                case TraitWhen.OnShieldBreak: return "盾が剥がれたとき";
                case TraitWhen.OnDown: return "倒れる一撃を受けたとき";
                default: throw new ArgumentOutOfRangeException(nameof(when));
            }
        }

        private static readonly Trait[] List =
        {
            new Trait("aim", "狙い澄まし", TraitWhen.Always,
                "弱化が通る率が上がる",
                "呪詛・鎮めの風・強打。⭐ **通りにくい技ほど得**なので、博打側の技と組む"),

            new Trait("stubborn", "意地", TraitWhen.Always,
                "弱化を受ける率が下がる",
                "相手の弱化役を腐らせる。⚠️ 相手に弱化役がいないと何もしない"),

            new Trait("spite", "返し身", TraitWhen.OnHurt,
                "受けたダメージの一部を返す",
                "挑発・受けの構え。⭐ **わざと殴られる**動きが得になる"),

            new Trait("grit", "執念", TraitWhen.OnShieldBreak,
                "盾が剥がれるたびゲージが溜まる",
                "鉄壁・硬化・シールド。⭐ 盾を「守り」から「手数の元」に変える"),

            new Trait("flurry", "手数", TraitWhen.OnHit,
                "多段の1発ごとに技の待ちが縮む",
                "連撃・乱打。⚠️ 単発の技しか持っていない個体では死ぬ"),

            new Trait("leech", "食らいつき", TraitWhen.OnHit,
                "与えたダメージの一部を吸う",
                "攻撃役の自己完結。⭐ 回復役を1枠空けられる"),
        };

        public static IReadOnlyList<Trait> All => List;

        public static bool Has(string id)
        {
            foreach (var trait in List)
            {
                if (trait.Id == id) return true;
            }
            return false;
        }

        public static Trait ById(string id)
        {
            foreach (var trait in List)
            {
                if (trait.Id == id) return trait;
            }
            throw new ArgumentException($"素質表に {id} が無い");
        }

        public static void Audit()
        {
            var problems = new List<string>();
            var seen = new HashSet<string>();
            foreach (var trait in List)
            {
                if (!seen.Add(trait.Id)) problems.Add($"素質 id が重複している: {trait.Id}");
                if (trait.Name.Length == 0) problems.Add($"{trait.Id}: 名前が空");
                if (trait.Gist.Length == 0) problems.Add($"{trait.Id}: 説明が空");
                if (trait.Pairs.Length == 0) problems.Add($"{trait.Id}: 何と噛み合うかが空");
            }

            // ⚠️ 繋いでいない素質が増え続けるのを止める。
            //    表だけ長くなって戦闘では何も起きない、が一番気づきにくい
            if (Wired < List.Length && List.Length > 8)
            {
                problems.Add(
                    $"戦闘に繋がっているのは {Wired} 件だが表には {List.Length} 件ある。" +
                    "繋ぐ前に増やしすぎている");
            }

            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    "素質表の不備:" + Environment.NewLine + "  " +
                    string.Join(Environment.NewLine + "  ", problems));
            }
        }
    }
}
