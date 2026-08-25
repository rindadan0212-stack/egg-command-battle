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
        /// <summary>尖り具合。⭐ 大きいほど「両親が共に高いステ」へ**伸びしろが寄る**。
        ///
        /// ⚠️ **2026-08-21 に効かせ方を変えた**（作者の指摘）。
        /// 直す前は「決まった合計を、この重みで**配り直す**」形だった。
        /// ⭐ 配り直しは**奪い合い**なので、上位2本が3本目を食い、
        /// **同じ形の親どうしでも代を重ねるほど形が壊れた**:
        /// <code>
        /// [40 40 30] → [46 46 28] → [47 47 24] → … → [60 60 0]
        /// </code>
        /// ⚠️ 「尖った個体を作りたいのに、同じ形どうしを掛けてはいけない」という
        /// あべこべな遊びになっていた。
        ///
        /// ⭐ いまは**伸びしろ（育てた分＋変異）だけ**をこの重みで配る。
        /// 既にある値を奪わないので、⭐ **同じ形どうしを掛ければ、その形のまま濃くなる**。
        ///
        /// ⚠️ 1.0 だと伸びしろが薄く広がって尖らない。上げすぎると1ステに全部乗る。</summary>
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

        /// <summary>生まれる子の希少さの見込み。⚠️ 変異が出れば1つ上がるので「見込み」。
        /// ⭐ 孵るのにどれだけ待つかがここで分かる。</summary>
        public static int PreviewRarity(Creature a, Creature b) =>
            Rarities.Clamp(Math.Max(a.Generation, b.Generation) + 1);

        private static int BaseTotalOf(Creature a, Creature b) =>
            ShapeTotalOf(a, b) + GrowthOf(a, b);

        /// <summary>形（＝両親の平均）の合計。⚠️ 素では増えない。</summary>
        private static int ShapeTotalOf(Creature a, Creature b)
        {
            int total = 0;
            foreach (var key in Stats.Keys) total += (a.Wild[key] + b.Wild[key]) / 2;
            return total;
        }

        /// <summary>伸びしろ。⭐ **育てた分だけが、次の代の生まれつきに変わる。**
        /// ⚠️ 育てずに配合すると 0 ── 枠だけ広がって中身は据え置きになる。</summary>
        private static int GrowthOf(Creature a, Creature b) =>
            (int)Math.Floor((a.Earned + b.Earned) * Carry + 0.5);

        /// <summary>配合する。⚠️ 両親を消すのは呼び側（<see cref="Games"/>）の仕事。
        /// ここは卵を作るだけにしておく（消してから例外を投げたら取り返しがつかない）。</summary>
        /// <param name="element">⚠️ 親のどちらを継ぐかは呼び側が別の系統で引く。
        /// ここで引くと配合の系統がずれて、較正済みの検査が無効になる。</param>
        /// ⚠️ **特性は受け取らない**（2026-08-21）。⭐ 子の種族が決まれば特性も決まる。
        public static BreedOutcome Fuse(Rng rng, Creature a, Creature b, int serial, int rarity = 0,
            Element? element = null)
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
            int generation = Math.Max(a.Generation, b.Generation) + 1;

            // ⭐ **上限を決めるのは世代**（2026-08-21 に変異から渡した）。
            // ⚠️ **上限が上がるだけでは強くならない。**中身は「両親の平均 ＋ 育てた分の
            //    <see cref="Carry"/>」でしか増えないので、育てずに配合を重ねると
            //    **枠だけ広がって中身は薄いまま**になる（作者の指示 2026-08-21）。
            // ⭐ 変異が足す +2 は「その回だけの上振れ」── 積み上がらない。
            //
            // ⭐ **形は親から継ぎ、伸びしろだけを尖らせて配る**（2026-08-21・作者の指摘）。
            //    ⚠️ 合計を配り直していた頃は、同じ形どうしでも形が壊れた（Sharpness の註）。
            int growth = GrowthOf(a, b) + mutations * Breeding.MutationStep;
            var wild = Stats.ApplyTotalCap(Grown(a, b, growth), generation);

            // ⚠️ **色はここで決めない**（2026-08-21・作者の指示）。
            //    ⭐ 「卵から生まれるとき」に引く ── 巣で拾った卵も同じ扱いになる。
            //    ⚠️ 決めていた頃は、色が**配合の副産物**でしかなかった。

            // ── 技: 両親の4枠から2つ。⭐ 質の軸はここが持つ
            string? skill2, skill3;
            Breeding.InheritSkills(rng, a, b, childSpecies.Skill1, childSpecies.Id, out skill2, out skill3);

            // ── 偏り4本: 大得意 → 得意 → 不得意 → 大不得意 の順に、片方の親から継ぐ。
            // ⭐ **順に取って、埋まったステは飛ばす。**⚠️ 4本を独立に引くと重なりが頻繁に出る
            //    （6ステから4本なら、素で引いた組の 8割以上がどこかで重なる）。
            var taken = new List<StatKey>();
            var best = InheritKey(rng, taken, a.Best, b.Best);
            var strong = InheritKey(rng, taken, a.Strong, b.Strong);
            var weak = InheritKey(rng, taken, a.Weak, b.Weak);
            var worst = InheritKey(rng, taken, a.Worst, b.Worst);

            int childRarity = rarity > 0
                ? rarity
                : Rarities.Clamp(generation + (mutations > 0 ? 1 : 0));

            var egg = new Egg(
                $"e{serial.ToString().PadLeft(3, '0')}",
                childSpecies.Id, wild, mutationCounter,
                a.Id, b.Id, generation, EggOrigin.Bred,
                hasSkills: true, skill2: skill2, skill3: skill3,
                rarity: childRarity, strong: strong, weak: weak,
                element: element ?? a.Element,
                best: best, worst: worst);

            return new BreedOutcome(egg, mutations);
        }

        /// <summary>偏りを1本、親のどちらかから継ぐ。
        ///
        /// ⭐ 選んだ親が持っていない／もう埋まっているステなら、もう片方 → 余りの順に降りる。
        /// ⚠️ **黙って重ねない。**重ねた組は <see cref="Creatures.Slanted(StatBlock, Creature)"/>
        /// が両方とも捨てるので、その個体だけ軸が1本消える（画面には▲が出たまま）。</summary>
        private static StatKey InheritKey(Rng rng, List<StatKey> taken, StatKey? from, StatKey? other)
        {
            bool first = rng.Chance(0.5);
            var wants = new List<StatKey?> { first ? from : other, first ? other : from };
            foreach (var want in wants)
            {
                if (want == null || taken.Contains(want.Value)) continue;
                taken.Add(want.Value);
                return want.Value;
            }
            var rest = new List<StatKey>();
            foreach (var key in Stats.Keys)
            {
                if (!taken.Contains(key)) rest.Add(key);
            }
            var picked = rest[rng.Int(0, rest.Count)];
            taken.Add(picked);
            return picked;
        }

        /// <summary>⭐ **形は親の平均、伸びしろは両親が共に高い方向へ。**
        ///
        /// ⭐ 重みを (親A + 親B) の <see cref="Sharpness"/> 乗にして、
        /// **<paramref name="growth"/> の分だけ**を配る。既にある値は動かさない。
        /// ⚠️ だから同じ形の親どうしを掛ければ、その形のまま濃くなる（＝尖る）。
        ///
        /// ⚠️ **合計を配り直さない。**配り直すと奪い合いになり、
        /// 上位のステが下位を食って代ごとに形が壊れる（<see cref="Sharpness"/> の註）。</summary>
        private static StatBlock Grown(Creature a, Creature b, int growth)
        {
            var weight = new double[Stats.Keys.Length];
            double sum = 0.0;
            for (int i = 0; i < Stats.Keys.Length; i++)
            {
                int paired = a.Wild[Stats.Keys[i]] + b.Wild[Stats.Keys[i]];
                weight[i] = Math.Pow(paired, Sharpness);
                sum += weight[i];
            }

            // ⭐ まず形（両親の平均）をそのまま置く
            var block = new StatBlock(0, 0, 0, 0);
            for (int i = 0; i < Stats.Keys.Length; i++)
            {
                var key = Stats.Keys[i];
                block = block.With(key, (a.Wild[key] + b.Wild[key]) / 2);
            }
            if (sum <= 0.0 || growth <= 0) return block;

            // ⭐ 伸びしろを重みで配る。
            // ⚠️ **端数を「最後のステ」へ寄せない。**⭐ 最後は弱化耐性なので、
            //    寄せると**形に無いステが勝手に生える**（実測: 2代目で耐性が 3 になった）。
            //    端数は必ず**一番重いステ**＝尖らせたい所へ。
            int left = growth;
            int top = 0;
            for (int i = 1; i < Stats.Keys.Length; i++) if (weight[i] > weight[top]) top = i;
            for (int i = 0; i < Stats.Keys.Length && left > 0; i++)
            {
                var key = Stats.Keys[i];
                int give = (int)Math.Floor(growth * (weight[i] / sum));
                if (give > left) give = left;
                if (give < 0) give = 0;
                block = block.With(key, block[key] + give);
                left -= give;
            }
            if (left > 0) block = block.With(Stats.Keys[top], block[Stats.Keys[top]] + left);
            return block;
        }
    }
}
