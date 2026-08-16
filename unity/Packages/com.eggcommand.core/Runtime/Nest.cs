#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>どうやって手に入れたか。盗んだ卵はやや劣る。</summary>
    public enum EggOrigin
    {
        Defeated,
        Stolen,
        Bred,
    }

    public sealed class Nest
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string SpeciesId;
        /// <summary>段階。高いほど親が強く、落とす卵も良い。</summary>
        public readonly int Tier;

        public Nest(string id, string name, string speciesId, int tier)
        {
            Id = id;
            Name = name;
            SpeciesId = speciesId;
            Tier = tier;
        }
    }

    public sealed class Egg
    {
        public readonly string Id;
        public readonly string SpeciesId;
        public readonly StatBlock Wild;
        public readonly int MutationCounter;
        public readonly int PaletteIndex;
        public readonly string? ParentA;
        public readonly string? ParentB;
        public readonly int Generation;
        public readonly EggOrigin How;

        /// <summary>⭐ null なら孵すときにガチャで決まる（野生の卵）。
        /// 値が入っていれば配合で既に決まっている（両親の4枠から抽選済み）。
        /// ⚠️ ここを区別しないと、配合で狙って引いた技を孵化時に引き直してしまう。</summary>
        public readonly bool HasSkills;
        public readonly string? Skill2;
        public readonly string? Skill3;

        /// <summary>希少さ 1〜5。⭐ 孵るまでの時間はここだけで決まる。
        /// ⚠️ 素質（<see cref="Wild"/>）とは別の軸にしてある。混ぜると
        /// 「時間をかけた＝強い」が確定してしまい、待つ以外の選択が消える。</summary>
        public readonly int Rarity;

        /// <summary>生まれつきの得意・不得意。⭐ null なら孵すときに引く（野生の卵）。
        /// ⚠️ <see cref="HasSkills"/> と同じ約束。配合で決まっているものを引き直さない。</summary>
        public readonly StatKey? Strong;
        public readonly StatKey? Weak;

        public Egg(string id, string speciesId, StatBlock wild, int mutationCounter, int paletteIndex,
            string? parentA, string? parentB, int generation, EggOrigin how,
            bool hasSkills, string? skill2, string? skill3, int rarity = 1,
            StatKey? strong = null, StatKey? weak = null)
        {
            Rarity = rarity < 1 ? 1 : rarity > Rarities.Max ? Rarities.Max : rarity;
            Strong = strong;
            Weak = weak;
            Id = id;
            SpeciesId = speciesId;
            Wild = wild;
            MutationCounter = mutationCounter;
            PaletteIndex = paletteIndex;
            ParentA = parentA;
            ParentB = parentB;
            Generation = generation;
            How = how;
            HasSkills = hasSkills;
            Skill2 = skill2;
            Skill3 = skill3;
        }
    }

    /// <summary>巣と卵。
    ///
    /// ⭐ 強い親ほど良い卵。これが難易度と報酬を自動で結ぶので、
    /// 報酬テーブルを別に設計しなくてよい。
    ///
    /// ⭐ 巣では二択:
    /// | 親を倒す | 確実に奪える。良い卵。ただし勝てる相手に限る |
    /// | 盗んで逃げる | 格上の巣でも狙えるが、失敗のリスクがある |
    ///
    /// これで「まだ勝てない巣に挑む」動機が生まれ、輪の駆動力になる。
    /// </summary>
    public static class Nests
    {
        /// <summary>段階ごとの、親が持つ野生レベルの合計。
        /// ⚠️ 上限 80 に届くのは最上位だけ。そこまで行くと配合でしか伸ばせなくなる。</summary>
        public static int WildTotalForTier(int tier)
        {
            var table = new[] { 24, 38, 52, 66, Stats.WildTotalMax };
            int index = tier - 1;
            if (index < 0) index = 0;
            if (index > table.Length - 1) index = table.Length - 1;
            return table[index];
        }

        public static readonly Nest[] All =
        {
            new Nest("shallow-scale", "浅瀬の巣", "tamaru", 1),
            new Nest("thicket-fang", "藪の巣", "tsunoga", 2),
            new Nest("cliff-plume", "崖の巣", "haneru", 3),
            new Nest("deep-scale", "深みの巣", "tamaru", 4),
            new Nest("peak-fang", "嶺の巣", "tsunoga", 5),
        };

        public static Nest ById(string id)
        {
            foreach (var nest in All)
            {
                if (nest.Id == id) return nest;
            }
            throw new ArgumentException($"巣の表に {id} が無い");
        }

        /// <summary>⚠️ JS の <c>Math.round</c> は「0.5 は上へ」。
        /// C# の <c>Math.Round</c> は既定が銀行丸めなので、そのまま使うと系列がずれる。</summary>
        private static int JsRound(double value) => (int)Math.Floor(value + 0.5);

        /// <summary>合計 total を4ステへ配る。偏らせたいので1〜2箇所に寄せる。</summary>
        private static StatBlock SpreadWild(Rng rng, int total)
        {
            var keys = new List<StatKey>(Stats.Keys);
            rng.Shuffle(keys);

            // 上位2つに多く配り、残りを下位へ。⭐ 野生も「得意2つ」の形にする
            var shares = new[] { 0.42, 0.32, 0.16, 0.1 };
            var raw = new StatBlock(0, 0, 0, 0);
            int left = total;
            for (int i = 0; i < keys.Count; i++)
            {
                int want = i == keys.Count - 1 ? left : JsRound(total * shares[i]);
                int give = want;
                if (give > left) give = left;
                if (give > Stats.WildStatMax) give = Stats.WildStatMax;
                if (give < 0) give = 0;
                raw = raw.With(keys[i], give);
                left -= give;
            }
            return Stats.ApplyTotalCap(raw);
        }

        private static void RollSkills23(Rng rng, string speciesId, string skill1,
            out string? skill2, out string? skill3)
        {
            var pool = Skills.GachaPoolOf(speciesId, skill1);
            int take = pool.Count < 2 ? pool.Count : 2;
            var picked = rng.Sample(pool, take);
            skill2 = picked.Count > 0 ? picked[0] : null;
            skill3 = picked.Count > 1 ? picked[1] : null;
        }

        /// <summary>巣を守るのは親1体だけ。
        ///
        /// ⭐ 発射フェーズで立ちはだかるのも親1体なので、話が繋がる。
        /// ⚠️ 以前は見張り2体を足して3体にしていたが、同じ種族が3体並ぶだけで、
        /// 画面でも戦術でも区別が付かなかった。1体にすると「この親をどう崩すか」に話が絞れる。
        /// HP の埋め合わせは loneScale（体数の比）が持つので、ここでは何もしない。</summary>
        public static List<Creature> MakeDefenders(Rng rng, Nest nest)
        {
            var species = SpeciesTable.ById(nest.SpeciesId);
            var wild = SpreadWild(rng, WildTotalForTier(nest.Tier));
            string? skill2, skill3;
            RollSkills23(rng, nest.SpeciesId, species.Skill1, out skill2, out skill3);

            return new List<Creature>
            {
                new Creature($"{nest.Id}-0", nest.SpeciesId, wild, new StatBlock(0, 0, 0, 0), 0,
                    0, skill2, skill3, 0, null, null, 1),
            };
        }

        /// <summary>親から卵を作る。
        /// ⚠️ 盗んだ卵は素質が落ちる。倒したほうが良い卵、という企画どおりにするため。</summary>
        public static Egg MakeEgg(Rng rng, Nest nest, EggOrigin how, int serial, int rarity = 1)
        {
            int baseTotal = WildTotalForTier(nest.Tier);
            double quality = how == EggOrigin.Defeated ? 1.0 : 0.78;
            int jitter = rng.Int(-3, 4);
            int total = JsRound(baseTotal * quality) + jitter;
            if (total < 4) total = 4;
            if (total > Stats.WildTotalMax) total = Stats.WildTotalMax;

            return new Egg(
                $"e{serial.ToString().PadLeft(3, '0')}",
                nest.SpeciesId,
                SpreadWild(rng, total),
                0, 0, null, null, 1, how,
                hasSkills: false, skill2: null, skill3: null, // 野生の卵。孵すときにガチャ
                rarity: rarity);
        }

        /// <summary>孵す。⭐ 野生の卵はここでスキル2・3のガチャを引く。
        /// 配合の卵は既に決まっているのでそのまま使う。</summary>
        /// <summary><paramref name="strong"/>/<paramref name="weak"/> は卵が持っていないときの
        /// 引き直し結果。⚠️ ここで乱数を引かない — 引くと既にある hatch の系統がずれて、
        /// 較正済みの検査が無効になる。呼び側が別の系統で引いて渡す。</summary>
        public static Creature Hatch(Rng rng, Egg egg, string id,
            StatKey? strong = null, StatKey? weak = null)
        {
            var species = SpeciesTable.ById(egg.SpeciesId);
            string? skill2 = egg.Skill2;
            string? skill3 = egg.Skill3;
            if (!egg.HasSkills)
            {
                RollSkills23(rng, egg.SpeciesId, species.Skill1, out skill2, out skill3);
            }

            return new Creature(id, egg.SpeciesId, egg.Wild, new StatBlock(0, 0, 0, 0), 0,
                egg.MutationCounter, skill2, skill3, egg.PaletteIndex,
                egg.ParentA, egg.ParentB, egg.Generation,
                egg.Strong ?? strong, egg.Weak ?? weak);
        }

        /// <summary>得意・不得意を引く。⚠️ 同じステにならないよう2つ別々に取る。</summary>
        public static void RollSlant(Rng rng, out StatKey strong, out StatKey weak)
        {
            var keys = new List<StatKey>(Stats.Keys);
            rng.Shuffle(keys);
            strong = keys[0];
            weak = keys[1];
        }

        // ── ボス ─────────────────────────────────────────

        /// <summary>最後の壁。⭐ 手で書いた固定の相手にしてある。
        ///
        /// 巣の守り手は挑むたびに顔ぶれが変わるが、ボスは毎回同じ。
        /// ⭐ そうしないと「何が足りないか考えて、配合で作って、挑み直す」という
        /// 輪の駆動力が働かない（相手が毎回変わるなら対策の立てようがない）。</summary>
        public const string BossName = "淵のヌシ";

        /// <summary>⭐ ヌシ1体だけ。眷属は置かない。
        ///
        /// ⚠️ 以前は眷属2体（壁と撹乱）を付けていたが、同じ画面に3体並ぶと
        /// 「どれを狙うか」が作業になり、ヌシ本体に一度も触れないまま負けることがあった。
        /// 1体にすると、難しさがその1体の技の噛み合いだけで決まる。
        ///
        /// ⭐ 変異を4回重ねた個体という扱い。上限が 44/88 に上がるので、
        /// ボス専用の例外ルールを足さずに強くできる。
        /// ⭐ 震撼（全体強攻撃）は枠2へ。枠1は CT が無いので、大技はここに置いて CT を効かせる。</summary>
        public static List<Creature> MakeBossParty()
        {
            const int mutation = 4;
            var wild = Stats.ApplyTotalCap(new StatBlock(16, 22, 21, 3), mutation);
            return new List<Creature>
            {
                new Creature("boss-0", "nushi", wild, new StatBlock(0, 0, 0, 0), 0,
                    mutation, "attack-all-heavy", "spd-down", 0, null, null, 1),
            };
        }

        /// <summary>巣の表に抜けが無いか数える検査。</summary>
        public static void Audit()
        {
            var problems = new List<string>();
            var ids = new HashSet<string>();
            foreach (var nest in All) ids.Add(nest.Id);
            if (ids.Count != All.Length) problems.Add("巣の id が重複している");

            foreach (var nest in All)
            {
                // 存在しない種族を指していないか（指していると孵した瞬間に落ちる）
                var species = SpeciesTable.ById(nest.SpeciesId);
                if (Skills.GachaPoolOf(nest.SpeciesId, species.Skill1).Count == 0)
                {
                    problems.Add($"{nest.Id}: 卵ガチャのプールが空");
                }
                if (nest.Tier < 1) problems.Add($"{nest.Id}: 段階が {nest.Tier}");
            }

            if (problems.Count > 0)
                throw new InvalidOperationException("巣の表の不備:\n  " + string.Join("\n  ", problems));
        }
    }
}
