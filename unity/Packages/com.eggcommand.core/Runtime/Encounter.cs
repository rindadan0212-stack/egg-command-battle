#nullable enable
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>いま探索に出ている巣1件。
    ///
    /// ⭐ 出せるのは**見た目とレベルだけ**。種族の素質も段階も伏せる。
    /// ⚠️ 中身を出すと「勝てる相手だけ選ぶ」になり、飛ばして確かめる意味が消える。
    /// </summary>
    /// <summary>探索で出るものの種類。
    ///
    /// ⭐ 育成の資源を分けるための区別:
    /// | 巣   | 卵が獲れる ＝ **新しい素質**の入口。引っ張って届けば盗み、失敗なら戦闘 |
    /// | 野良 | 卵は無い ＝ **Lv** の入口。押したらそのまま戦闘 |
    ///
    /// ⚠️ 野良が無いと、戦えるのは強奪に失敗したときだけになり、
    /// レベルを上げる手段が事実上「失敗すること」になってしまう。
    /// </summary>
    public enum EncounterKind
    {
        Nest,
        Wild,
    }

    public sealed class Encounter
    {
        public readonly Nest Nest;
        /// <summary>唯一の手掛かり。⭐ 段階に沿って上がるので、大小の比較だけはできる。</summary>
        public readonly int Level;
        public readonly EncounterKind Kind;

        public Encounter(Nest nest, int level, EncounterKind kind)
        {
            Nest = nest;
            Level = level;
            Kind = kind;
        }
    }

    public static class Encounters
    {
        /// <summary>同時に出す数。⚠️ 増やすと「全部見てから決める」になり選択が薄まる。</summary>
        public const int Shown = 3;

        /// <summary>巣に立てる種族。⚠️ ヌシは終点なので巣には出さない。</summary>
        private static readonly string[] Pool = { "tamaru", "tsunoga", "haneru" };

        private static readonly string[] Places =
        {
            "浅瀬", "藪", "崖", "深み", "嶺", "洞", "沢", "枯野", "霧原", "岩棚",
        };

        /// <summary>野良に勝ったときに入る Lv。⭐ 巣の戦闘（+1）より厚くする。
        /// ⚠️ 卵が出ないぶんの見返り。ここが同じだと野良を選ぶ理由が無い。</summary>
        public const int WildReward = 2;

        /// <summary>1件つくる。⭐ 段階は 1〜5 の一様。浅い巣ばかりにならないように。</summary>
        public static Encounter Make(Rng rng, int serial)
        {
            string speciesId = Pool[rng.Int(0, Pool.Length)];
            int tier = rng.Int(1, 6);
            string place = Places[rng.Int(0, Places.Length)];
            // ⭐ レベルは段階に比例。振れ幅を段階の間隔より小さくして、
            //    「数が大きいほど手強い」という読みが必ず当たるようにする
            int level = tier * 10 + rng.Int(-4, 5);
            var kind = rng.Chance(0.5) ? EncounterKind.Nest : EncounterKind.Wild;

            var nest = new Nest($"wild-{serial}", $"{place}の巣", speciesId, tier);
            return new Encounter(nest, level, kind);
        }

        /// <summary>3件になるまで補充する。⚠️ 並びは変えない（見ていた札が動かないように）。</summary>
        public static void Refill(Game game)
        {
            while (game.Encounters.Count < Shown)
            {
                game.Encounters.Add(Make(game.RngEncounter, ++game.EncounterSerial));
            }
            EnsureBoth(game);
        }

        /// <summary>巣と野良が必ず1件ずつは出ているようにする。
        /// ⚠️ 3件とも同じ種類だと「卵が獲れない回」「育てられない回」が生まれ、
        /// 探索が引き直し待ちの作業になる。⭐ 選ばせたいのだから、選択肢を切らさない。</summary>
        private static void EnsureBoth(Game game)
        {
            if (game.Encounters.Count < 2) return;
            int nests = 0;
            foreach (var e in game.Encounters)
            {
                if (e.Kind == EncounterKind.Nest) nests++;
            }
            if (nests > 0 && nests < game.Encounters.Count) return;

            // 全部同じだった。最後の1件だけ逆にする
            int last = game.Encounters.Count - 1;
            var odd = game.Encounters[last];
            game.Encounters[last] = new Encounter(odd.Nest, odd.Level,
                nests == 0 ? EncounterKind.Nest : EncounterKind.Wild);
        }

        /// <summary>片付いた巣を1件だけ差し替える。</summary>
        public static void Replace(Game game, Nest nest)
        {
            for (int i = 0; i < game.Encounters.Count; i++)
            {
                if (game.Encounters[i].Nest.Id != nest.Id) continue;
                game.Encounters[i] = Make(game.RngEncounter, ++game.EncounterSerial);
                EnsureBoth(game);
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
