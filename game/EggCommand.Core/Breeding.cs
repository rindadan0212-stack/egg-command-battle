#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    public sealed class BreedOutcome
    {
        public readonly Egg Egg;
        /// <summary>この配合で出た変異の回数（0〜3）。</summary>
        public readonly int Mutations;

        public BreedOutcome(Egg egg, int mutations)
        {
            Egg = egg;
            Mutations = mutations;
        }
    }

    /// <summary>配合と遺伝。ARK 準拠。
    ///
    /// | 要素 | 仕様 |
    /// |---|---|
    /// | 種族 | 50% でどちらかの親。スキル1 はその種族のもの（連動） |
    /// | ステ | 各ステ独立にロール。高いほうの親が 55% |
    /// | 変異 | 2.5% を3回振る。⭐ 出るとその子の素質が +2（⚠️ 色とは無関係）|
    /// | 上限 | ⭐ **世代**が押し上げる（配合するたび +1・<see cref="Stats.GenerationCapSteps"/> で頭打ち）|
    /// | スキル2・3 | 両親の4枠から2つ抽選（枠1と重なるものは除く） |
    /// | 合計上限 | 変異ぶん押し上げた上限で、超過は低いステから削る |
    ///
    /// ⭐ ステごとに独立ロールするのが厳選の中毒性の源。
    /// 「専門化した親を複数持って組み合わせる」遊びがここから生まれる。
    /// </summary>
    public static class Breeding
    {
        /// <summary>高いほうの親から取る確率。ARK 準拠。</summary>
        public const double InheritHigher = 0.55;

        /// <summary>変異の判定回数と1回あたりの確率。
        ///
        /// ⭐ 2.5% を3回振ると、ARK の公表値がそのまま出る:
        /// 1回以上 = 1 - 0.975³ = 7.31% / ちょうど2回 = 3×0.025²×0.975 = 0.183%
        /// / 3回 = 0.025³ = 0.00156%。
        /// 個別に確率を置くより素直で、値が食い違う余地が無い。</summary>
        public const int MutationRolls = 3;
        public const double MutationChance = 0.025;

        /// <summary>1回の変異で上がるレベル。⚠️ **その子だけ**。
        /// ⭐ 積み上がらない（次の代は両親の平均から数え直す）。</summary>
        public const int MutationStep = 2;

        /// <summary>⚠️ **もう「無限強化のブレーキ」ではない**（2026-08-21）。
        ///
        /// ⚠️ 変異が上限を押し上げていた頃は、ここが無いと無限に強くなった。
        /// ⭐ いまは上限を決めるのは世代（<see cref="Stats.GenerationCapSteps"/> で頭打ち）で、
        /// 変異が足す +2 もその枠の中で削られるので、**暴走しようがない**。
        ///
        /// ⭐ **数はそのまま 20 で置いてある。**移植元が記録している定数なので動かさない
        /// （ゴールデンが見ている）。⚠️ ここに届くのは 7.31% を20回引いたあと＝
        /// **約267回の配合**なので、遊びの中でぶつかることはまず無い。
        /// ⭐ いまのここの役は「色が変わらなくなる上限」だけ。</summary>
        public const int MutationCounterLimit = 20;

        public static bool CanBreed(Creature a, Creature b) => a.Id != b.Id;

        /// <summary>変異が出うるか。⭐ **いつでも出る**（2026-08-21 に止めるのをやめた）。
        /// ⚠️ 欄は残す ── 呼び側を全部書き換えると、止めたくなった日に戻せない。</summary>
        public static bool MutationAllowed(Creature a, Creature b) =>
            a.MutationCounter < MutationCounterLimit || b.MutationCounter < MutationCounterLimit;

        /// <summary><paramref name="rarity"/> は 0 のとき「世代と変異から決める」。
        /// ⚠️ ここで乱数を引かない。既にある breed の系統がずれると較正済みの検査が無効になる。</summary>
        public static BreedOutcome Breed(Rng rng, Creature a, Creature b, int serial, int rarity = 0)
        {
            if (!CanBreed(a, b)) throw new InvalidOperationException("同じ個体どうしは配合できない");

            // ── 種族（スキル1 と連動する）
            var childSpecies = rng.Chance(0.5) ? Creatures.SpeciesOf(a) : Creatures.SpeciesOf(b);

            // ── ステ: 各ステ独立に、高いほうの親が 55%
            var wild = new StatBlock(0, 0, 0, 0);
            foreach (var key in Stats.Keys)
            {
                var high = a.Wild[key] >= b.Wild[key] ? a : b;
                var low = ReferenceEquals(high, a) ? b : a;
                wild = wild.With(key, (rng.Chance(InheritHigher) ? high : low).Wild[key]);
            }

            // ── 変異: 2.5% を3回。当たったステに +2
            int mutations = 0;
            if (MutationAllowed(a, b))
            {
                for (int i = 0; i < MutationRolls; i++)
                {
                    if (!rng.Chance(MutationChance)) continue;
                    mutations++;
                    var key = rng.Pick(Stats.Keys);
                    wild = wild.With(key, wild[key] + MutationStep);
                }
            }

            int mutationCounter = Math.Max(a.MutationCounter, b.MutationCounter) + mutations;
            int generation = Math.Max(a.Generation, b.Generation) + 1;

            // ⚠️ 上限は**世代**で押し上げる（2026-08-21 に変異から渡した）。
            //    ⭐ 変異が足した +2 はその世代の枠の中で効く。
            var capped = Stats.ApplyTotalCap(wild, generation);

            // ⚠️ **色はここで決めない**（2026-08-21）。孵るときに引く。

            // ⭐ 配合の卵はここで技が決まる。孵すときに引き直さない
            string? skill2, skill3;
            InheritSkills(rng, a, b, childSpecies.Skill1, childSpecies.Id, out skill2, out skill3);

            // ⭐ 重ねた世代ぶん孵るのが遅くなる。深い血統ほど時間を払う、という形にする。
            //    ⚠️ 乱数ではなく世代と変異から決める（同じ親からは同じ重さになる）
            int childRarity = rarity > 0
                ? rarity
                : Rarities.Clamp(generation + (mutations > 0 ? 1 : 0));

            var egg = new Egg(
                $"e{serial.ToString().PadLeft(3, '0')}",
                childSpecies.Id,
                capped,
                mutationCounter,
                a.Id, b.Id,
                generation,
                EggOrigin.Bred,
                hasSkills: true, skill2: skill2, skill3: skill3,
                rarity: childRarity);

            return new BreedOutcome(egg, mutations);
        }

        /// <summary>両親の4枠から、子の枠2・3を決める。
        /// ⚠️ 子の枠1（種族スキル）と重なるものは外す。同じ技が2枠を占めると片方が無駄になる。</summary>
        /// <summary>⚠️ <see cref="Fusion"/> からも呼ぶ。技の遺伝は両方で同じ規則にしておく。</summary>
        /// <summary>🔴 **配合は「★N は格N 以下」の縛りを受けない**（作者の決定 2026-08-27）。
        ///
        /// ⚠️ 卵ガチャ（<see cref="Nests.RollSkills23"/>）は★で引ける格に上限を掛けるが、
        /// ここは掛けない ── ⭐ **配合は「両親が持っている技を継ぐ」**という別の筋だから。
        /// 親が持っていない技は出ないので、無から上位格が湧くことはない。
        ///
        /// ⚠️ **不具合ではない。**★の低い子が親ゆずりの上位格を持つのは、
        /// 「血統を重ねて良い個体を作る」という配合の値打ちそのもの。
        /// ⭐ 揃えたくなったら、それは**仕様の変更**であって修正ではない。</summary>
        internal static void InheritSkills(Rng rng, Creature a, Creature b, string childSkill1,
            string childSpeciesId, out string? skill2, out string? skill3)
        {
            // ⚠️ JS の Set は入れた順を保つ。C# の HashSet は保たないので、List で順を守る
            var unique = new List<string>();
            foreach (var id in new[] { a.Skill2, a.Skill3, b.Skill2, b.Skill3 })
            {
                if (id == null || id == childSkill1) continue;
                if (!unique.Contains(id)) unique.Add(id);
            }

            if (unique.Count >= 2)
            {
                var picked = rng.Sample(unique, 2);
                skill2 = picked[0];
                skill3 = picked[1];
                return;
            }

            // ⚠️ 親から2つ取れないときは、子の種族のプールから補う。
            //    空き枠のまま返すと、配合を重ねるほど技が痩せていく
            var fallback = new List<string>();
            foreach (var id in Skills.GachaPoolOf(childSpeciesId, childSkill1))
            {
                if (!unique.Contains(id)) fallback.Add(id);
            }
            int need = 2 - unique.Count;
            int take = need < fallback.Count ? need : fallback.Count;
            var extra = rng.Sample(fallback, take);

            var all = new List<string>(unique);
            all.AddRange(extra);
            skill2 = all.Count > 0 ? all[0] : null;
            skill3 = all.Count > 1 ? all[1] : null;
        }

        /// <summary>画面で「この2体を配合すると何が起こりうるか」を見せるための要約。</summary>
        public static void PreviewOf(Creature a, Creature b,
            out List<string> speciesNames, out List<string> skillPool, out bool mutable)
        {
            speciesNames = new List<string>();
            foreach (var name in new[] { Creatures.SpeciesOf(a).Name, Creatures.SpeciesOf(b).Name })
            {
                if (!speciesNames.Contains(name)) speciesNames.Add(name);
            }

            // ⚠️ 重複は名前でなく技そのもので落とす（TS 側も Set<Skill> で落としている）
            skillPool = new List<string>();
            var seen = new List<string>();
            foreach (var source in new[] { Creatures.SkillsOf(a), Creatures.SkillsOf(b) })
            {
                for (int i = 1; i < source.Length; i++)
                {
                    var skill = source[i];
                    if (skill == null || seen.Contains(skill.Id)) continue;
                    seen.Add(skill.Id);
                    skillPool.Add(skill.Name);
                }
            }

            mutable = MutationAllowed(a, b);
        }
    }
}
