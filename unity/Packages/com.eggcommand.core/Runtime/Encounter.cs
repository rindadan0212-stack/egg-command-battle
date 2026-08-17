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

        /// <summary>いつ居なくなるか。⚠️ **「あと何秒」ではなく「いつ」を持つ。**
        /// 残り秒で持つと、画面を見ていない間の時間が進まない（孵化器と同じ約束）。</summary>
        public readonly long UntilUnix;

        public Encounter(Nest nest, int level, long untilUnix = 0)
        {
            Nest = nest;
            Level = level;
            UntilUnix = untilUnix;
        }
    }

    public static class Encounters
    {
        /// <summary>同時に出す数。⚠️ 増やすと「全部見てから決める」になり選択が薄まる。</summary>
        public const int Shown = 3;

        /// <summary>巣に立てる種族（全段階の総和）。⚠️ ヌシは終点なので巣には出さない。
        ///
        /// ⚠️ **平らな表を別に持たない。**<see cref="ByTier"/> から**導く**。
        /// 以前は7種を並べた表を別に持っていたが、抽選は段階別プールしか見ていなかったので、
        /// **段階別に足し忘れた種族が図鑑には「出る」と表示される**状態だった（出所が2つ）。
        ///
        /// ⚠️ **種族を足したら <see cref="ByTier"/> に足す。** 足し忘れると
        /// 表にはいるのに**一生手に入らない種族**になる。<see cref="Audit"/> が数えている。</summary>
        public static IReadOnlyList<string> NestSpecies
        {
            get
            {
                var all = new List<string>();
                for (int tier = 1; tier <= ByTier.Length; tier++)
                {
                    foreach (var id in PoolFor(tier))
                    {
                        if (!all.Contains(id)) all.Add(id);
                    }
                }
                return all;
            }
        }

        /// <summary>段階ごとに解禁される種族。⭐ **深い巣ほど顔ぶれが増える。**
        ///
        /// ⚠️ 以前は段階と無関係に7種から一様に引いていた。浅瀬の巣に
        /// 実測で総合勝率が突出している種族（hirabe 68.2%）が立ちうる形だった。
        ///
        /// ⭐ 並びは `sim species` の総合勝率の低い順。弱い顔ぶれから覚えていける。
        /// ⚠️ **これは釣り合いの直し方ではない。**hirabe の 68.2% は
        /// 「35〜65% から外れた種族」として別途直すべきもので、
        /// ここで奥へ隠すのは**問題を見えなくしているだけ**。混同しないこと。
        ///
        /// ⚠️ 増えるだけで減らない（累積）。⭐ 上の段で覚えた顔ぶれが消えると、
        /// 「この種族はこう動く」という学習が無駄になる。</summary>
        private static readonly string[][] ByTier =
        {
            new[] { "tsunoga", "tamaru" },                                  // 段1
            new[] { "tsunoga", "tamaru", "togeru" },                        // 段2
            new[] { "tsunoga", "tamaru", "togeru", "haneru", "nobiru" },    // 段3
            new[] { "tsunoga", "tamaru", "togeru", "haneru", "nobiru", "marumi" },
            new[] { "tsunoga", "tamaru", "togeru", "haneru", "nobiru", "marumi", "hirabe" },
        };

        public static IReadOnlyList<string> PoolFor(int tier)
        {
            int index = tier - 1;
            if (index < 0) index = 0;
            if (index > ByTier.Length - 1) index = ByTier.Length - 1;
            return ByTier[index];
        }

        /// <summary>巣に出ない種族。⭐ ボスは終点なので卵からは出さない。</summary>
        public const string BossSpeciesId = "nushi";

        private static readonly string NewLine = System.Environment.NewLine;

        /// <summary>手に入らない種族がいないか数える。</summary>
        public static void Audit()
        {
            // ⚠️ 見るのは**段階別プールの総和**。どの段階のプールにも入っていなければ
            //    一生手に入らない（NestSpecies が同じ導出を持っている）
            var inPool = new HashSet<string>(NestSpecies);
            var problems = new List<string>();

            foreach (var id in inPool)
            {
                if (!SpeciesTable.Has(id)) problems.Add($"巣の種族 {id} が種族表に無い");
            }
            // ⚠️ 段階が上がって顔ぶれが減っていないか。減ると覚えた学習が無駄になる
            for (int tier = 2; tier <= ByTier.Length; tier++)
            {
                foreach (var id in PoolFor(tier - 1))
                {
                    bool kept = false;
                    foreach (var later in PoolFor(tier)) if (later == id) kept = true;
                    if (!kept) problems.Add($"段{tier} で {id} が消えている（顔ぶれは減らさない）");
                }
            }
            if (PoolFor(1).Count == 0) problems.Add("段1 に立つ種族が1つも無い");
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

        /// <summary>巣が居座る秒。⭐ **深い巣ほど短い。**
        ///
        /// ⭐ 良い巣ほど早く消えるので、「見つけたら今すぐ挑む」理由になります。
        /// ⚠️ 逆（深いほど長く居座る）にすると、良い巣が居続けて**探索そのものが止まります**
        /// ── 1件を掘り尽くすまで他を見なくてよくなるためです。
        ///
        /// ⚠️ 一番浅い巣でも消えます。居座り続ける巣を作ると、
        /// そこが「安全に稼げる場所」になって輪が止まります。</summary>
        public static int SecondsFor(int tier)
        {
            var table = new[] { 3600, 2400, 1500, 900, 600 };   // 60分 / 40分 / 25分 / 15分 / 10分
            int index = tier - 1;
            if (index < 0) index = 0;
            if (index > table.Length - 1) index = table.Length - 1;
            return table[index];
        }

        /// <summary>あと何秒で居なくなるか。⚠️ 期限を持たない巣（古い保存）は 0。
        /// ⭐ 画面に必ず出すこと（巣が黙って消えると理不尽にしかならない）。</summary>
        public static int LeftOf(Encounter encounter, long nowUnix)
        {
            if (encounter.UntilUnix <= 0) return 0;
            long left = encounter.UntilUnix - nowUnix;
            return left < 0 ? 0 : (int)left;
        }

        public static bool IsGone(Encounter encounter, long nowUnix) =>
            encounter.UntilUnix > 0 && nowUnix >= encounter.UntilUnix;

        /// <summary>期限を持たない巣に、いまから期限を与える。
        ///
        /// ⚠️ **時刻を渡さずに始めた保存には、期限の無い巣が残っている。**
        /// そのままだと居座り続け、残り時間も出せない（0 は「切れた」ではなく「無い」）。
        /// ⭐ 消さずに**いまから数え直す**。消すと、遊んでいた人の探索が
        /// 起動しただけで作り替わってしまう。</summary>
        /// <returns>期限を与えた件数。⚠️ 既に持っている巣には触らない。</returns>
        public static int Stamp(Game game, long nowUnix)
        {
            if (nowUnix <= 0) return 0;
            int stamped = 0;
            for (int i = 0; i < game.Encounters.Count; i++)
            {
                var encounter = game.Encounters[i];
                if (encounter.UntilUnix > 0) continue;
                game.Encounters[i] = new Encounter(encounter.Nest, encounter.Level,
                    nowUnix + SecondsFor(encounter.Nest.Tier));
                stamped++;
            }
            return stamped;
        }

        /// <summary>期限切れの巣を差し替える。⭐ **巣が入れ替わる唯一のもう1つの経路。**
        ///
        /// ⚠️ もう一方は「親が死ぬ」（戦って倒す・負ける）。
        /// ⭐ どちらも**その巣が終わったから**入れ替わる、という同じ理屈です。
        ///
        /// ⚠️ 差し替えた巣の盗んだ回数は捨てます（<see cref="Games.ForgetRaids"/>）。
        /// 残しても id は再利用されませんが、辞書が単調に増えて保存が膨らみ続けます。</summary>
        /// <returns>差し替えた件数。</returns>
        public static int Expire(Game game, long nowUnix)
        {
            var gone = new List<Nest>();
            foreach (var encounter in game.Encounters)
            {
                if (IsGone(encounter, nowUnix)) gone.Add(encounter.Nest);
            }
            foreach (var nest in gone) Replace(game, nest, nowUnix);
            return gone.Count;
        }

        /// <summary>その編成に見合う巣の段階。⭐ **ここが「序盤なのに強すぎる」の唯一の栓。**
        ///
        /// ⚠️ 以前は 1〜5 の一様だった。始めた直後から段5の巣が 1/5 で出て、
        /// プレイヤーの進み具合が探索に一切反映されていなかった。
        ///
        /// ⭐ 中心はプレイヤーの力量、振れ幅は ±1。
        /// ⚠️ **上へ1つは必ず出す。**全部が身の丈だと「まだ勝てない巣に挑む」という
        /// 輪の駆動力（企画 §2）が消える。⚠️ 逆に +3 まで出すと元の一様に戻る。</summary>
        /// <param name="reach">プレイヤーの力量。編成の Lv の平均。</param>
        public static int TierFor(Rng rng, int reach)
        {
            // ⭐ 巣の段階が要求する素質合計と、プレイヤーの Lv は同じ物差し（どちらもステの和）
            int center = 1;
            for (int tier = 1; tier <= 5; tier++)
            {
                if (reach >= Nests.WildTotalForTier(tier)) center = tier;
            }
            int rolled = center + rng.Int(-1, 2);
            return rolled < 1 ? 1 : rolled > 5 ? 5 : rolled;
        }

        /// <summary>いまの編成の力量。⚠️ 1体でも欠けたら弱い側に寄せる（平均で見る）。</summary>
        public static int ReachOf(Game game)
        {
            var party = Games.PartyOf(game);
            if (party.Count == 0) return 0;
            int sum = 0;
            foreach (var creature in party) sum += Levels.Of(creature);
            return sum / party.Count;
        }

        /// <summary>1件つくる。</summary>
        /// <param name="reach">プレイヤーの力量。⚠️ 0 だと段1 ばかりになる。</param>
        /// <param name="nowUnix">いまの時刻。⚠️ ここから居座る秒を足して期限にする。</param>
        public static Encounter Make(Rng rng, int serial, int reach, long nowUnix = 0)
        {
            int tier = TierFor(rng, reach);
            // ⚠️ 種族は段階のあとで引く。⭐ 段階ごとに立てる種族が違う
            var pool = PoolFor(tier);
            string speciesId = pool[rng.Int(0, pool.Count)];
            string place = Places[rng.Int(0, Places.Length)];
            // ⭐ レベルは段階に比例。振れ幅を段階の間隔より小さくして、
            //    「数が大きいほど手強い」という読みが必ず当たるようにする
            int level = tier * 10 + rng.Int(-4, 5);

            var nest = new Nest($"wild-{serial}", $"{place}の巣", speciesId, tier);
            // ⚠️ 時刻を渡されなければ期限を持たない（較正済みの検査と移植元の経路のため）
            long until = nowUnix > 0 ? nowUnix + SecondsFor(tier) : 0;
            return new Encounter(nest, level, until);
        }

        /// <summary>3件になるまで補充する。⚠️ 並びは変えない（見ていた札が動かないように）。</summary>
        public static void Refill(Game game, long nowUnix = 0)
        {
            int reach = ReachOf(game);
            while (game.Encounters.Count < Shown)
            {
                game.Encounters.Add(Make(game.RngEncounter, ++game.EncounterSerial, reach, nowUnix));
            }
        }

        /// <summary>片付いた巣を1件だけ差し替える。</summary>
        public static void Replace(Game game, Nest nest, long nowUnix = 0)
        {
            int reach = ReachOf(game);
            for (int i = 0; i < game.Encounters.Count; i++)
            {
                if (game.Encounters[i].Nest.Id != nest.Id) continue;
                // ⚠️ 盗んだ回数を捨てる。id は再利用されないので副作用は無いが、
                //    残すと辞書が単調に増えて保存が膨らみ続ける
                Games.ForgetRaids(game, nest.Id);
                game.Encounters[i] = Make(game.RngEncounter, ++game.EncounterSerial, reach, nowUnix);
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
