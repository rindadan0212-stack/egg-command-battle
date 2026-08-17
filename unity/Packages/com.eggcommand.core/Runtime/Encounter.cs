#nullable enable
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>いま探索に出ている巣1件。
    ///
    /// ⭐ 出せるのは**見た目とレベルだけ**。種族の素質も段階も伏せる。
    /// ⚠️ 中身を出すと「勝てる相手だけ選ぶ」になり、飛ばして確かめる意味が消える。
    /// </summary>
    public sealed class Encounter
    {
        public readonly Nest Nest;
        /// <summary>唯一の手掛かり。⭐ 段階に沿って上がるので、大小の比較だけはできる。</summary>
        public readonly int Level;

        public Encounter(Nest nest, int level)
        {
            Nest = nest;
            Level = level;
        }
    }

    public static class Encounters
    {
        /// <summary>同時に出す数。⚠️ 増やすと「全部見てから決める」になり選択が薄まる。</summary>
        public const int Shown = 3;

        /// <summary>巣に立てる種族。⚠️ ヌシは終点なので巣には出さない。
        ///
        /// ⚠️ **種族を足したらここにも足す。** 足し忘れると、表にはいるのに
        /// **一生手に入らない種族**になる。コンパイルも検査も通ってしまうので、
        /// <see cref="Audit"/> が数えている。</summary>
        public static IReadOnlyList<string> NestSpecies => Pool;

        private static readonly string[] Pool =
        {
            "tamaru", "tsunoga", "haneru",
            "nobiru", "hirabe", "togeru", "marumi",
        };

        /// <summary>巣に出ない種族。⭐ ボスは終点なので卵からは出さない。</summary>
        public const string BossSpeciesId = "nushi";

        private static readonly string NewLine = System.Environment.NewLine;

        /// <summary>手に入らない種族がいないか数える。</summary>
        public static void Audit()
        {
            var inPool = new HashSet<string>(Pool);
            var problems = new List<string>();

            foreach (var id in Pool)
            {
                if (!SpeciesTable.Has(id)) problems.Add($"巣の種族 {id} が種族表に無い");
            }
            foreach (var species in SpeciesTable.All)
            {
                if (species.Id == BossSpeciesId) continue;
                if (!inPool.Contains(species.Id))
                {
                    problems.Add($"{species.Id}: どの巣にも立たない。**一生手に入らない種族**になっている");
                }
            }

            if (problems.Count > 0)
            {
                throw new System.InvalidOperationException(
                    "探索の巣の不備:" + NewLine + "  " + string.Join(NewLine + "  ", problems));
            }
        }

        private static readonly string[] Places =
        {
            "浅瀬", "藪", "崖", "深み", "嶺", "洞", "沢", "枯野", "霧原", "岩棚",
        };

        /// <summary>1件つくる。⭐ 段階は 1〜5 の一様。浅い巣ばかりにならないように。</summary>
        public static Encounter Make(Rng rng, int serial)
        {
            string speciesId = Pool[rng.Int(0, Pool.Length)];
            int tier = rng.Int(1, 6);
            string place = Places[rng.Int(0, Places.Length)];
            // ⭐ レベルは段階に比例。振れ幅を段階の間隔より小さくして、
            //    「数が大きいほど手強い」という読みが必ず当たるようにする
            int level = tier * 10 + rng.Int(-4, 5);

            var nest = new Nest($"wild-{serial}", $"{place}の巣", speciesId, tier);
            return new Encounter(nest, level);
        }

        /// <summary>3件になるまで補充する。⚠️ 並びは変えない（見ていた札が動かないように）。</summary>
        public static void Refill(Game game)
        {
            while (game.Encounters.Count < Shown)
            {
                game.Encounters.Add(Make(game.RngEncounter, ++game.EncounterSerial));
            }
        }

        /// <summary>片付いた巣を1件だけ差し替える。</summary>
        public static void Replace(Game game, Nest nest)
        {
            for (int i = 0; i < game.Encounters.Count; i++)
            {
                if (game.Encounters[i].Nest.Id != nest.Id) continue;
                game.Encounters[i] = Make(game.RngEncounter, ++game.EncounterSerial);
                return;
            }
        }

        public static Encounter? Find(Game game, string nestId)
        {
            foreach (var encounter in game.Encounters)
            {
                if (encounter.Nest.Id == nestId) return encounter;
            }
            return null;
        }
    }
}
