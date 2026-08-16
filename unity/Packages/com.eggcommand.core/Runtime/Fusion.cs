#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>配合＝**2体が卵に還る**。両親は失われる。
    ///
    /// ⭐ 役割をはっきり分けてある:
    /// | 巣の卵 | **新しい素質**を入れる唯一の入口。ランダムで広い |
    /// | 配合   | **持っているものを尖らせる**唯一の出口。2体を失う |
    ///
    /// これで経済に入口と出口ができる。配合は消費なので、続けるには探索に戻るしかない。
    ///
    /// ⭐ 素質の**合計は素では増えない**。増やせるのは巣の卵と変異だけ。
    /// 配合がやるのは「合計はそのままに、両親が共に強い方向へ寄せる」こと。
    /// ⚠️ ここを増やす形にすると配合だけで際限なく強くなり、探索が要らなくなる。
    ///
    /// ⭐ ただし**育てた分は次の代の生まれつきに変わる**。
    /// これが Lv MAX の2体を失う対価。労力が消えずに次へ渡る。
    ///
    /// ⚠️ <see cref="Breeding"/> は移植元の規則そのまま（較正済みの検査60件が踏んでいる）。
    /// こちらが今の遊びで使う規則。両方を混ぜないこと。
    /// </summary>
    public static class Fusion
    {
        /// <summary>尖り具合。⭐ 大きいほど「両親が共に高いステ」へ寄る。
        /// ⚠️ 1.0 だと平均するだけで尖らない。上げすぎると1ステに全部乗って壊れる。</summary>
        /// ⚠️ 2.2 で試したら素質1のステが出た。尖るのは狙いどおりでも、
        /// 1 は「使えない個体」であって選択にならない。1.6 まで戻してある。
        public const double Sharpness = 1.6;

        /// <summary>育てた分のうち、子の生まれつきに変わる割合。
        /// ⚠️ DQM の「親のスキルポイントの半分」に相当。1.0 にすると
        /// 育てて配合するだけで上限まで一直線になる。</summary>
        public const double Carry = 0.25;

        public static bool CanFuse(Creature a, Creature b) => a.Id != b.Id;

        /// <summary>子の生まれつき Lv を**先に**知る。⭐ 配合画面で見せるためのもの。
        ///
        /// ⭐ 「Lv MAX でないと配合できない」という規則を置かずに済むのはこれのおかげ。
        /// 育てていない2体を並べたら小さい数が出る。⚠️ 字で警告を書かない。</summary>
        public static int PreviewBirthLevel(Creature a, Creature b) =>
            BaseTotalOf(a, b);

        private static int BaseTotalOf(Creature a, Creature b)
        {
            // 合計は素では増えない。両親の平均
            int total = (Stats.TotalOf(a.Wild) + Stats.TotalOf(b.Wild)) / 2;
            // 育てた分だけが上乗せされる
            total += (int)Math.Floor((a.Earned + b.Earned) * Carry + 0.5);
            return total;
        }

        /// <summary>配合する。⚠️ 両親を消すのは呼び側（<see cref="Games"/>）の仕事。
        /// ここは卵を作るだけにしておく（消してから例外を投げたら取り返しがつかない）。</summary>
        public static BreedOutcome Fuse(Rng rng, Creature a, Creature b, int serial, int rarity = 0)
        {
            if (!CanFuse(a, b)) throw new InvalidOperationException("同じ個体どうしは配合できない");

            // ── 種族: 50% でどちらかの親（スキル1 が連動する）
            var childSpecies = rng.Chance(0.5) ? Creatures.SpeciesOf(a) : Creatures.SpeciesOf(b);

            // ── 変異: ここだけが合計を素で押し上げる
            int mutations = 0;
            if (Breeding.MutationAllowed(a, b))
            {
                for (int i = 0; i < Breeding.MutationRolls; i++)
                {
                    if (rng.Chance(Breeding.MutationChance)) mutations++;
                }
            }
            int mutationCounter = Math.Max(a.MutationCounter, b.MutationCounter) + mutations;

            int total = BaseTotalOf(a, b) + mutations * Breeding.MutationStep;
            var wild = Stats.ApplyTotalCap(Sharpen(a, b, total), mutationCounter);

            // ── 色: 変異が出たらパレットが変わる
            int paletteIndex = mutations > 0 && childSpecies.Palettes.Count > 1
                ? rng.Int(1, childSpecies.Palettes.Count)
                : 0;

            // ── 技: 両親の4枠から2つ。⭐ 質の軸はここが持つ
            string? skill2, skill3;
            Breeding.InheritSkills(rng, a, b, childSpecies.Skill1, childSpecies.Id, out skill2, out skill3);

            // ── 得意・不得意: 片方ずつ別の親から。⚠️ 同じになったら引き直す
            var strong = (rng.Chance(0.5) ? a.Strong : b.Strong) ?? Stats.Keys[rng.Int(0, Stats.Keys.Length)];
            var weak = (rng.Chance(0.5) ? a.Weak : b.Weak) ?? Stats.Keys[rng.Int(0, Stats.Keys.Length)];
            if (strong.Equals(weak))
            {
                var rest = new List<StatKey>();
                foreach (var key in Stats.Keys)
                {
                    if (!key.Equals(strong)) rest.Add(key);
                }
                weak = rest[rng.Int(0, rest.Count)];
            }

            int generation = Math.Max(a.Generation, b.Generation) + 1;
            int childRarity = rarity > 0
                ? rarity
                : Rarities.Clamp(generation + (mutations > 0 ? 1 : 0));

            var egg = new Egg(
                $"e{serial.ToString().PadLeft(3, '0')}",
                childSpecies.Id, wild, mutationCounter, paletteIndex,
                a.Id, b.Id, generation, EggOrigin.Bred,
                hasSkills: true, skill2: skill2, skill3: skill3,
                rarity: childRarity, strong: strong, weak: weak);

            return new BreedOutcome(egg, mutations);
        }

        /// <summary>合計を <paramref name="total"/> に保ったまま、両親が共に高い方向へ寄せる。
        ///
        /// ⭐ 重みを (親A + 親B) の <see cref="Sharpness"/> 乗にする。
        /// 両方が高いステだけが不釣り合いに伸びるので、「尖る」が数式で成立する。
        /// ⚠️ 平均を取るだけ（1乗）だと丸くなるだけで、配合の意味が消える。</summary>
        private static StatBlock Sharpen(Creature a, Creature b, int total)
        {
            var weight = new double[Stats.Keys.Length];
            double sum = 0.0;
            for (int i = 0; i < Stats.Keys.Length; i++)
            {
                int paired = a.Wild[Stats.Keys[i]] + b.Wild[Stats.Keys[i]];
                weight[i] = Math.Pow(paired, Sharpness);
                sum += weight[i];
            }

            var block = new StatBlock(0, 0, 0, 0);
            if (sum <= 0.0) return block;

            int left = total;
            for (int i = 0; i < Stats.Keys.Length; i++)
            {
                // ⚠️ 最後の1つは残り全部。丸めの端数が消えないように
                int give = i == Stats.Keys.Length - 1
                    ? left
                    : (int)Math.Floor(total * (weight[i] / sum) + 0.5);
                if (give > left) give = left;
                if (give < 0) give = 0;
                block = block.With(Stats.Keys[i], give);
                left -= give;
            }
            return block;
        }
    }
}
