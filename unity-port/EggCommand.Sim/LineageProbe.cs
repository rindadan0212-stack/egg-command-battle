#nullable enable
using System;
using System.Collections.Generic;
using EggCommand.Core;

namespace EggCommand.Sim
{
    /// <summary>血統を伸ばすと何が起きるか。⭐ **作者の指示（2026-08-21）を数で確かめる道具。**
    ///
    /// 「配合を適当に繰り返せばいいというわけではなく、適切に育てたキャラを配合することで
    /// 上限を上げていく。弱い個体の配合では上限は上がるが実値は弱いまま」
    ///
    /// ⭐ ここが見るのは**枠（上限）と中身（実際の合計）の差**。
    /// ⚠️ 差が開かないなら「育てる意味」が数字に出ていない ── 作り直しの合図。</summary>
    public static class LineageProbe
    {
        private const int Generations = 24;

        public static void Run(int seed)
        {
            Console.WriteLine();
            Console.WriteLine("■ 血統を伸ばすと（同じ乱数で2本・育てるか育てないかだけが違う）");
            Console.WriteLine("  ⭐ 上限は**世代**が押し上げる（2026-08-21 に変異から渡した）");
            Console.WriteLine("  ⚠️ 中身は「両親の平均 ＋ 育てた分の "
                + Fusion.Carry.ToString("0.00") + "」でしか増えない");
            Console.WriteLine();
            Console.WriteLine("   代    上限   同型   形違い  育てない    同型の中身（尖りが保たれるか）");

            var same = Line(seed, true, false);
            var mixed = Line(seed, true, true);
            var lazy = Line(seed, false, false);
            for (int i = 0; i < same.Count; i++)
            {
                int cap = Stats.WildTotalMaxFor(same[i].Generation);
                var w = same[i].Wild;
                Console.WriteLine($"  {same[i].Generation,3}  {cap,6}"
                    + $"  {Stats.TotalOf(w),5}  {Stats.TotalOf(mixed[i].Wild),6}"
                    + $"  {Stats.TotalOf(lazy[i].Wild),8}"
                    + $"    [{w.Hp,2} {w.Atk,2} {w.Def,2} {w.Spd,2} {w.Acc,2} {w.Res,2}]");
            }
            Console.WriteLine("  ⭐ **同じ形どうしを掛けると、その形のまま濃くなる**（2026-08-21 に直した）");
            Console.WriteLine("  ⚠️ 直す前は合計を配り直していたので、同型どうしでも形が壊れた");
            Console.WriteLine("     （[40 40 30] → [46 46 28] → … → [60 60 0]）");
            Console.WriteLine("  ⚠️ **育てない**と枠だけ広がって中身は据え置き（作者の指示どおりの形）");

            // ⭐ 変異が何回出たかも数えておく。⚠️ **色とは別物**（2026-08-21 に切り離した）
            Console.WriteLine();
            int mutated = 0;
            for (int i = 1; i < mixed.Count; i++)
                if (mixed[i].MutationCounter > mixed[i - 1].MutationCounter) mutated++;
            Console.WriteLine($"■ 変異（素質の上振れ）は {Generations} 回の配合で {mutated} 回出た"
                + $"（1回あたり {100.0 * (1.0 - Math.Pow(1.0 - Breeding.MutationChance, Breeding.MutationRolls)),0:0.0}%）");
            Console.WriteLine("  ⭐ 変異は**その子だけ +" + Breeding.MutationStep
                + "**。⚠️ 上限は押し上げない（積み上げる必要が無い）");

            // ⭐ 色は「孵るとき」に引く（2026-08-21）。⚠️ 変異とは別のもの
            Console.WriteLine();
            Console.WriteLine($"■ 色（孵るときに1回引く・{SpeciesTable.VariantChance * 100:0.#}%）");
            const int Eggs = 20000;
            var palette = new Rng(seed).Stream("palette-probe");
            var count = new Dictionary<int, int>();
            foreach (var species in SpeciesTable.All)
            {
                int variants = 0;
                for (int i = 0; i < Eggs; i++)
                {
                    int at = SpeciesTable.RollPalette(palette, species.Id);
                    if (at > 0) variants++;
                    if (!count.ContainsKey(at)) count[at] = 0;
                    count[at]++;
                }
                if (species.Id != SpeciesTable.All[0].Id) continue;
                Console.WriteLine($"  {species.Name}: {Eggs} 個のうち 変わった色 {variants} 個"
                    + $"（{100.0 * variants / Eggs:0.0}%・色は {species.Palettes.Count - 1} 種）");
            }
            Console.Write("  内訳（全種族ぶん）:");
            foreach (var pair in count)
                Console.Write($"  色{pair.Key}={100.0 * pair.Value / (Eggs * SpeciesTable.All.Count):0.0}%");
            Console.WriteLine();
            Console.WriteLine("  ⭐ 巣で拾った卵も配合の卵も同じ確率（代は関係しない）");
        }

        /// <summary>1本の血統を伸ばす。⭐ 毎代、同じ形の相手と配合する。</summary>
        /// <param name="grow">毎代きっちり育ててから配合するか。</param>
        /// <param name="mix">⭐ 形の違う相手と掛けるか。⚠️ false は同型どうし
        /// （<see cref="Fusion.Sharpen"/> が2本へ寄せるので枠を埋め切れない）。</param>
        private static List<Creature> Line(int seed, bool grow, bool mix)
        {
            var rng = new Rng(seed).Stream("lineage");
            int serial = 0;
            var line = new List<Creature>();

            var current = Seed(ref serial);
            line.Add(current);
            for (int i = 0; i < Generations; i++)
            {
                // ⚠️ 相手も同じ深さの個体にする（片親だけ深いと世代が伸びない）
                var mate = Clone(current, ref serial, mix);
                if (grow)
                {
                    Creatures.Grow(current, Creatures.TrainMax);
                    Creatures.Grow(mate, Creatures.TrainMax);
                }
                var egg = Fusion.Fuse(rng, current, mate, ++serial).Egg;
                current = Nests.Hatch(rng, egg, $"g{serial}");
                line.Add(current);
            }
            return line;
        }

        /// <summary>始まりの1体。⭐ 巣から出る★1相当（素質は上限のちょうど半分）。</summary>
        private static Creature Seed(ref int serial) => new Creature(
            $"seed{++serial}", "tamaru",
            Stats.ApplyTotalCap(new StatBlock(20, 20, 20, 0, 0, 0)),
            new StatBlock(0, 0, 0, 0), 0, 0, "shield", "regen", 0, null, null, 1,
            StatKey.Hp, StatKey.Spd, Element.Fire,
            Creatures.TraitIdFor("tamaru"), StatKey.Atk, StatKey.Acc);

        /// <summary>相方を作る。⭐ <paramref name="mix"/> なら**形を裏返した**個体
        /// （＝別の所を専門にした親）。⚠️ id は必ず変える（同じ個体どうしは配合できない）。</summary>
        private static Creature Clone(Creature one, ref int serial, bool mix)
        {
            var w = one.Wild;
            // ⭐ 6本を裏返す。両親が別の所で高いので、Sharpen が寄せ切らずに広く残る
            var shape = mix
                ? new StatBlock(w.Res, w.Acc, w.Spd, w.Def, w.Atk, w.Hp)
                : w;
            return Made(one, shape, ref serial);
        }

        private static Creature Made(Creature one, StatBlock wild, ref int serial) => new Creature(
            $"mate{++serial}", one.SpeciesId, wild, new StatBlock(0, 0, 0, 0), 0,
            one.MutationCounter, one.Skill2, one.Skill3, one.PaletteIndex,
            null, null, one.Generation,
            one.Strong, one.Weak, one.Element,
            Creatures.TraitIdFor(one.SpeciesId), one.Best, one.Worst);
    }
}
