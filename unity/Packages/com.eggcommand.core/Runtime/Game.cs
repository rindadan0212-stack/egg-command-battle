#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>ゲーム全体の状態。唯一の出所。
    ///
    /// ⚠️ 乱数は系統を分けて持つ。片方で消費数が変わっても、もう片方の列がずれない
    /// （較正済みの検査を無効にしないため）。
    /// </summary>
    public sealed class Game
    {
        public readonly int Seed;
        public Storage Storage;
        /// <summary>手に入れてまだ孵化器へ入れていない卵。</summary>
        public readonly List<Egg> Eggs = new List<Egg>();
        /// <summary>いま温めている卵。⚠️ 上限は <see cref="Hatchery.Slots"/>。</summary>
        public readonly List<Incubation> Incubating = new List<Incubation>();
        /// <summary>いま探索に出ている巣。⚠️ 上限は <see cref="Encounters.Shown"/>。</summary>
        public readonly List<Encounter> Encounters = new List<Encounter>();
        /// <summary>ホームで進み続けている放置。⭐ 素材の唯一の出所。</summary>
        public readonly IdleRun Idle = new IdleRun();
        /// <summary>出撃させる3体の id。⚠️ 空なら素質の高い順に自動で選ぶ。</summary>
        /// <summary>放置に出している3体。⚠️ **巣へ潜る編成とは別**。
        /// ⭐ 放置は「置いておく」ものなので、潜るたびに組み替えたくない。
        /// 同じにすると、巣に合わせて入れ替えた瞬間に放置が止まる。</summary>
        public readonly List<string> Party = new List<string>();

        /// <summary>巣へ潜る編成。⭐ **3つ登録できる**。
        /// ⚠️ 長さは常に <see cref="NestPartySlots"/>。読み込みでもここを崩さない。</summary>
        public readonly List<List<string>> NestParties = new List<List<string>>
        {
            new List<string>(), new List<string>(), new List<string>(),
        };

        /// <summary>いま使う巣の編成の番号（0..2）。</summary>
        public int NestParty;
        /// <summary>巣ごとに何回盗んだか。⭐ **盤の難易度と巣の寿命の唯一の出所。**
        ///
        /// ⚠️ 盗んだ回数で関門が増え、隙間が狭まり、いずれ親が塞ぎ切る。
        /// ここを持たないと、挑むたびに盤が振り直せてしまう（粘れば良い盤が出る）。</summary>
        public readonly Dictionary<string, int> Raids = new Dictionary<string, int>();

        /// <summary>勝った試練の id。⭐ **試練が返す唯一のもの。**
        ///
        /// ⚠️ 卵も EXP も出さない（出すと「試練で稼ぐ」が最短経路になり、
        /// 潜入も配合も回らなくなる）。⭐ 出撃していた個体の育成 +1 は、
        /// 他の戦闘とまったく同じ扱いで付く。
        /// ⚠️ 重複して足さない（<see cref="Games.BeatTrial"/> が見る）。</summary>
        public readonly List<string> TrialsBeaten = new List<string>();

        /// <summary>⭐ **一度でも手に入れた種族の id**（図鑑の中身）。
        ///
        /// ⚠️ 「いま持っている種族」ではない ── ⭐ [分解](../../../wiki/分解.md)しても
        /// 図鑑からは消えない。消える作りにすると「集めた記録」にならず、
        /// **枠を空けるたびに図鑑が減る**という妙なことになる。
        /// ⚠️ 足すのは <see cref="Games.Keep"/> と <see cref="Games.See"/> だけ
        /// （保管庫へ入れる道が増えたときに、書き忘れを1か所で防ぐ）。</summary>
        public readonly List<string> SpeciesSeen = new List<string>();

        /// <summary>通し番号。id を一意にするためだけに使う。</summary>
        public int Serial;
        /// <summary>探索の巣の通し番号。⚠️ <see cref="Serial"/> と分ける。
        /// 混ぜると巣を1つ引き直すたびに卵や個体の id が飛び、較正済みの検査がずれる。</summary>
        public int EncounterSerial;

        // 系統ごとの乱数
        public readonly Rng RngNest;
        public readonly Rng RngEgg;
        public readonly Rng RngHatch;
        public readonly Rng RngSteal;
        public readonly Rng RngBreed;
        // ⚠️ 後から足した系統。既にある5本の消費順を1つも変えていないので、
        //    較正済みの検査（45件）はそのまま通る。混ぜて引かないこと。
        public readonly Rng RngRarity;
        public readonly Rng RngEncounter;
        public readonly Rng RngSlant;
        /// <summary>属性を引く系統。⚠️ 後から足したもの。既にある系統の消費順を1つも変えていない。</summary>
        public readonly Rng RngElement;
        /// <summary>⚠️ **もう引いていない系統**（特性は種族から決まる・2026-08-21）。
        ///
        /// ⚠️ **消さない。**保存は系統を順番に4語ずつ並べるので、途中の1本を抜くと
        /// **そこから後ろが全部ずれる**（<see cref="Snapshots"/> の <c>StreamsOf</c>）。
        /// ⭐ 残しておけば、古い保存もそのまま読める。</summary>
        public readonly Rng RngTrait;

        /// <summary>⭐ **戦闘の運を引く系統。**弱化が通るかはここから出る。
        ///
        /// ⚠️ 2026-08-21 まで、画面が <see cref="Battle.CreateBattle"/> へ乱数を
        /// **渡していなかった** ── 既定の固定の種が使われ、
        /// **同じ編成なら必ず同じ試合**になっていた。
        /// ⭐ `sim` のほうは渡していて、註に「渡さないと全戦闘が同じ列になる」と
        /// 警告まで書いてあった。⚠️ **測る経路と遊ぶ経路が食い違っていた**。
        ///
        /// ⚠️ **系統を分ける。**卵や巣と混ぜると、戦うだけで次に出る卵が変わる。</summary>
        public readonly Rng RngBattle;

        /// <summary>⭐ **色を引く系統**（2026-08-21）。孵すたびに1回引く。
        /// ⚠️ 孵化の系統（RngHatch）に混ぜない ── 技のガチャの列がずれる。</summary>
        public readonly Rng RngPalette;

        public Game(int seed)
        {
            Seed = seed;
            Storage = Storages.Empty();
            var root = new Rng(seed);
            RngNest = root.Stream("nest");
            RngEgg = root.Stream("egg");
            RngHatch = root.Stream("hatch");
            RngSteal = root.Stream("steal");
            RngBreed = root.Stream("breed");
            RngRarity = root.Stream("rarity");
            RngEncounter = root.Stream("encounter");
            RngSlant = root.Stream("slant");
            RngElement = root.Stream("element");
            RngTrait = root.Stream("trait");
            RngBattle = root.Stream("battle");
            RngPalette = root.Stream("palette");
        }
    }

    public static class Games
    {
        /// <summary>1つの編成に並ぶ体数。⭐ **戦闘・潜入・放置のすべてがこれを見る。**
        ///
        /// ⚠️ 2026-08-20 に 3 → 4（作者の判断）。
        /// ⭐ 戦闘の式は体数を決め打ちしていない（<see cref="Battle.LoneScale"/> が体数の**比**で
        /// 効く）ので、戦闘そのものは変えていない。
        /// ⚠️ **較正した数は連動させること。**体数に比例して動くもの:
        /// <see cref="Trail.SpeedPerRoll"/>（3体の速度合計で較正）・
        /// <see cref="Trails.RefStat"/>（3体のステ合計で較正）・雑魚の頭数。
        /// ⚠️ 測定で「4対4 は奥行きを増やさず、上下の開きが広がる」と出ている
        /// （速攻 +14 / 役割分担 −16・[釣り合い](../../../../wiki/開発/釣り合い.md)）。
        /// 作者の判断で進めた変更なので、**釣り合いは測り直して別に扱う**。</summary>
        public const int PartySize = 4;

        /// <summary>⚠️ **較正した数がどの体数で測られたか。**
        /// ⭐ 体数を変えたとき、比例して動かすための分母。</summary>
        public const int CalibratedParty = 3;

        /// <param name="nowUnix">いまの時刻。⚠️ **渡さないと開幕の3件が永久に消えません**
        /// （期限 0 ＝「期限を持たない」扱いになるため）。⭐ 既定 0 は較正済みの照合のため。</param>
        /// <param name="startWith">最初に持つ体数。⚠️ **0 なら <see cref="PartySize"/>。**
        /// ⭐ 渡せるようにしてあるのは、移植の照合（ゴールデン）が
        /// **較正した当時の体数**で遊びを再生するため（<paramref name="nowUnix"/> と同じ理由）。
        /// ⚠️ 遊びの中からは渡さない。</param>
        public static Game NewGame(int seed, long nowUnix = 0, int startWith = 0)
        {
            var game = new Game(seed);

            // 最初の編成ぶん。一番浅い巣の卵を孵したところから始める
            var first = Nests.ById("shallow-scale");
            int start = startWith > 0 ? startWith : PartySize;
            for (int i = 0; i < start; i++)
            {
                var egg = Nests.MakeEgg(game.RngEgg, first, EggOrigin.Defeated, ++game.Serial,
                    element: SpeciesTable.Roll(game.RngElement));
                string id = $"c{game.Serial.ToString().PadLeft(3, '0')}";
                StatKey best, strong, weak, worst;
                Nests.RollSlant(game.RngSlant, out best, out strong, out weak, out worst);
                // ⭐ 色は**孵るたび**に引く（巣の卵も配合の卵も同じ扱い）
                int color = SpeciesTable.RollPalette(game.RngPalette, egg.SpeciesId);
                Keep(game, Nests.Hatch(game.RngHatch, egg, id, strong, weak, best, worst, color));
            }
            Encounters.Refill(game, nowUnix);
            return game;
        }

        /// <summary>巣の守り手。⚠️ 挑むたびに作り直す（同じ巣でも顔ぶれが変わる）。</summary>
        public static List<Creature> DefendersOf(Game game, Nest nest) =>
            Nests.MakeDefenders(game.RngNest, nest, SpeciesTable.Roll(game.RngElement));

        /// <summary>その巣から何回盗んだか。⚠️ 一度も盗んでいなければ 0。</summary>
        public static int RaidsOn(Game game, Nest nest)
        {
            int count;
            return game.Raids.TryGetValue(nest.Id, out count) ? count : 0;
        }

        /// <summary>盗みが成った。⭐ **次からこの巣は難しくなる。**
        /// ⚠️ 戦って勝ったときは数えない（守りを固めるのは盗まれたときだけ）。</summary>
        public static void RecordRaid(Game game, Nest nest) =>
            game.Raids[nest.Id] = RaidsOn(game, nest) + 1;

        /// <summary>片付いた巣の記録を捨てる。⚠️ 巣の id は再利用されないので、
        /// 残しても害は無い**ように見えて**、辞書が単調に増えて保存が膨らみ続ける。
        /// ⭐ 差し替えるたびに捨てる（<see cref="Encounters.Replace"/> が呼ぶ）。</summary>
        public static void ForgetRaids(Game game, string nestId) => game.Raids.Remove(nestId);

        /// <summary>もう**潜入では**届かない巣か。⭐ 親が完全にふさいでいる。
        ///
        /// ⚠️ **巣が消えるわけではない。**探索には残り続ける。
        /// ⭐ 塞がった巣に入ると、どう投げても親に当たる ＝ **必ず親との戦闘になる**。
        /// そこで勝てば最後の卵が手に入り、**親が失われるので巣が消える**。
        /// ⚠️ 塞がりは壁ではなく、**戦闘へ向かわせる漏斗**。
        /// ここで巣を消してしまうと、最後の1個を取り上げることになる。</summary>
        public static bool IsNestSealed(Game game, Nest nest) =>
            Steal.IsSealed(RaidsOn(game, nest));

        /// <summary>巣から卵を取る（**遊びで使うほう**）。⭐ 素質も孵化時間も★だけで決まる。
        /// ⚠️ <see cref="GainEgg"/> は移植元の規則。較正済みの照合が踏んでいるので残す。</summary>
        public static Egg TakeEgg(Game game, Nest nest, EggOrigin how)
        {
            // ⚠️ 希少さは別の系統で引く。ここを RngEgg に混ぜると素質の列がずれる
            int rarity = Rarities.Roll(game.RngRarity, nest.Tier, how);
            var egg = Nests.MakeEggOfRarity(game.RngEgg, nest, how, ++game.Serial, rarity,
                element: SpeciesTable.Roll(game.RngElement));
            game.Eggs.Add(egg);
            return egg;
        }

        public static Egg GainEgg(Game game, Nest nest, EggOrigin how)
        {
            // ⚠️ 希少さは別の系統で引く。ここを RngEgg に混ぜると素質の列がずれて、
            //    較正済みの検査が無効になる
            int rarity = Rarities.Roll(game.RngRarity, nest.Tier, how);
            var egg = Nests.MakeEgg(game.RngEgg, nest, how, ++game.Serial, rarity,
                element: SpeciesTable.Roll(game.RngElement));
            game.Eggs.Add(egg);
            return egg;
        }

        /// <summary>孵す。⚠️ 保管庫が満杯なら孵さない（黙って捨てない）。</summary>
        public static Creature HatchEgg(Game game, string eggId)
        {
            if (Storages.IsFull(game.Storage))
                throw new InvalidOperationException($"保管庫が満杯（{game.Storage.Slots}枠）。先にどれかを逃がす");

            int index = -1;
            for (int i = 0; i < game.Eggs.Count; i++)
            {
                if (game.Eggs[i].Id == eggId) { index = i; break; }
            }
            if (index < 0) throw new ArgumentException($"{eggId} という卵は持っていない");

            var egg = game.Eggs[index];
            game.Eggs.RemoveAt(index);
            string id = $"c{(++game.Serial).ToString().PadLeft(3, '0')}";
            var creature = Nests.Hatch(game.RngHatch, egg, id);
            Keep(game, creature);
            return creature;
        }

        /// <summary>⭐ **保管庫へ入れる唯一の口。**⚠️ <see cref="Storages.Accept"/> を
        /// 直に呼ばない ── 呼ぶと図鑑に載らない個体ができる。
        ///
        /// ⚠️ 満杯なら <see cref="Storages.Accept"/> が投げる（ここでは握り潰さない）。</summary>
        public static void Keep(Game game, Creature creature)
        {
            game.Storage = Storages.Accept(game.Storage, creature);
            See(game, creature.SpeciesId);
        }

        /// <summary>その種族を図鑑に載せる。⚠️ 二重に足さない。</summary>
        public static void See(Game game, string speciesId)
        {
            if (string.IsNullOrEmpty(speciesId)) return;
            // ⚠️ **表に無い id を書き込まない。**⭐ 書くと、種族を消したときに
            //    図鑑が「知らない何か」を1枠抱えたまま残る。
            if (!SpeciesTable.Has(speciesId)) return;
            if (game.SpeciesSeen.Contains(speciesId)) return;
            game.SpeciesSeen.Add(speciesId);
        }

        /// <summary>その種族を手に入れたことがあるか。</summary>
        public static bool HasSeen(Game game, string speciesId) =>
            game.SpeciesSeen.Contains(speciesId);

        /// <summary>図鑑に載っている数。⚠️ 分母は <see cref="SpeciesTable.All"/> の数。</summary>
        public static int SeenCount(Game game) => game.SpeciesSeen.Count;

        public static void ReleaseCreature(Game game, string id)
        {
            game.Storage = Storages.Release(game.Storage, id);
            // ⚠️ **すべての編成から外す。**1本だけ外していた頃の形を残すと、
            //    消えた個体の id が別の編成に残り、その枠が永久に空になる。
            game.Party.Remove(id);
            foreach (var roster in game.NestParties) roster.Remove(id);
            // ⚠️ **放置の「倒れている」帳からも外す。**外していなかった頃は、
            //    逃がした・配合した個体の id が残り続けて保存が膚らんだ。
            //    ⭐ Raids に対して ForgetRaids が同じ理由で用意されている（片方だけ抜けていた）
            game.Idle.DownUntil.Remove(id);
        }

        public static Creature CreatureById(Game game, string id)
        {
            foreach (var creature in game.Storage.Creatures)
            {
                if (creature.Id == id) return creature;
            }
            throw new ArgumentException($"{id} は保管庫にいない");
        }

        /// <summary>配合する。卵は保管庫ではなく卵の棚に入る（孵すまでが1手間）。
        /// ⚠️ 移植元の規則。較正済みの検査が踏んでいるので残す。
        /// 遊びで使うのは <see cref="FusePair"/>。</summary>
        public static BreedOutcome BreedPair(Game game, string aId, string bId)
        {
            var outcome = Breeding.Breed(
                game.RngBreed,
                CreatureById(game, aId),
                CreatureById(game, bId),
                ++game.Serial);
            game.Eggs.Add(outcome.Egg);
            return outcome;
        }

        /// <summary>配合＝2体が卵に還る。⭐ **両親は失われる**。
        ///
        /// ⚠️ 卵を作ってから消す。先に消すと、作る途中で投げたときに
        /// 2体を失っただけで何も残らない。</summary>
        public static BreedOutcome FusePair(Game game, string aId, string bId)
        {
            var a = CreatureById(game, aId);
            var b = CreatureById(game, bId);
            // ⭐ 属性は親のどちらかから受け継ぐ。⚠️ 別の系統で引く（配合の列をずらさない）
            var element = game.RngElement.Chance(0.5) ? a.Element : b.Element;
            // ⚠️ **特性はここで引かない**（2026-08-21）。⭐ 子の種族が決まった時点で決まる
            //    ── 配合で特性を狙うなら、狙うのは**種族**のほう。
            var outcome = Fusion.Fuse(game.RngBreed, a, b, ++game.Serial, element: element);
            game.Eggs.Add(outcome.Egg);

            ReleaseCreature(game, aId);
            ReleaseCreature(game, bId);
            return outcome;
        }

        /// <summary>選んだ卵を**まとめて**1枠へ注ぐ。⭐ 「分解」と同じ形
        /// （<see cref="Dissolve"/>）── 選んでから、最後に一度だけ実行する。
        ///
        /// ⚠️ 2026-08-21 まで**1個押すごとに即座にレベルが上がっていた**（作者の指摘）。
        /// ⭐ 取り消せない操作を、押した回数だけ黙って重ねていたことになる。
        ///
        /// ⚠️ **入る順に入れて、入らなくなったらそこで止める。**⭐ 上限を超える卵は
        /// 受け取らない（<see cref="FeedEggToSkill"/> と同じ約束）ので、
        /// 10個選んで7個ぶんしか入らないこともある ── 画面はその数を先に出すこと。</summary>
        /// <returns>実際に入ったポイントの合計。0 なら卵も減っていない。</returns>
        public static int FeedEggsToSkill(Game game, string creatureId, int slot,
            IReadOnlyList<string> eggIds)
        {
            int total = 0;
            foreach (string eggId in eggIds) total += FeedEggToSkill(game, creatureId, slot, eggId);
            return total;
        }

        /// <summary>その試練に勝ったことがあるか。</summary>
        public static bool BeatTrial(Game game, string trialId) =>
            game.TrialsBeaten.Contains(trialId);

        /// <summary>勝った印を付ける。⚠️ 二重に足さない。</summary>
        /// <returns>初めて勝ったなら true。</returns>
        public static bool MarkTrial(Game game, string trialId)
        {
            if (!Trials.Has(trialId)) throw new ArgumentException($"試練 {trialId} は無い");
            if (game.TrialsBeaten.Contains(trialId)) return false;
            game.TrialsBeaten.Add(trialId);
            return true;
        }

        /// <summary>いくつ勝ったか。⭐ 画面の見出しに出す。</summary>
        public static int TrialsCleared(Game game)
        {
            int count = 0;
            foreach (var trial in Trials.All) if (game.TrialsBeaten.Contains(trial.Id)) count++;
            return count;
        }

        /// <summary>孵化前の卵を素材にして、スキルを1枠ぶん鍛える。
        /// ⭐ **卵の唯一の「孵さない使い道」。**★＝強さを成立させている支え。
        ///
        /// ⚠️ 孵化器に入っている卵は使えない（棚にあるものだけ）。
        /// 温め始めたものを取り上げると、待った時間が黙って消える。
        /// ⚠️ 上限に達している枠には食わせない。⭐ 押したのに何も起きず卵だけ減る、を作らない。</summary>
        /// <returns>実際に入ったポイント。0 なら何も起きていない（卵も減らない）。</returns>
        public static int FeedEggToSkill(Game game, string creatureId, int slot, string eggId)
        {
            var creature = CreatureById(game, creatureId);
            if (slot < 0 || slot >= creature.SkillPoints.Length)
                throw new ArgumentException($"枠 {slot} は無い");
            // ⚠️ 空き枠には注げない（技が無いのにレベルだけ上がる状態を作らない）
            if (Creatures.SkillsOf(creature)[slot] == null) return 0;
            if (SkillCosts.IsMaxed(creature.SkillPoints[slot])) return 0;

            int index = -1;
            for (int i = 0; i < game.Eggs.Count; i++)
            {
                if (game.Eggs[i].Id == eggId) { index = i; break; }
            }
            if (index < 0) throw new ArgumentException($"{eggId} という卵は棚に無い");

            // ⚠️ **上限を超える卵は受け取らない。**丸めて受け取ると、
            //    上限の1つ手前に★5（81pt）を入れたとき 80pt が黙って消えて、
            //    画面には「+81」と出る（2時間待った卵が蒸発する）。
            int points = Rarities.PointsOf(game.Eggs[index].Rarity);
            int room = SkillCosts.TotalFor(Skills.MaxLevel) - creature.SkillPoints[slot];
            if (points > room) return 0;

            game.Eggs.RemoveAt(index);
            creature.SkillPoints[slot] += points;
            return points;
        }

        /// <summary>分解＝個体を EXP に還す。⭐ **選んだぶんをまとめて。**
        ///
        /// ⚠️ 2026-08-19 に「合成」（1体に食わせて直接 Lv を上げる）から置き換えた
        /// （作者の指示）。⭐ EXP を**溜める側**に一本化したので、
        /// 「誰に食わせるか」を先に決めなくてよくなり、どの個体にも後から使える。
        ///
        /// ⚠️ 戻せない。⭐ 「逃がす」の代わりでもある ── 捨てるのではなく必ず EXP になる。</summary>
        /// <returns>入った EXP の合計。</returns>
        public static int Dissolve(Game game, IReadOnlyList<string> ids)
        {
            int total = 0;
            foreach (string id in ids)
            {
                var creature = FindCreature(game, id);
                // ⚠️ 見つからないものは黙って飛ばす（同じ id が二度来ても壊れない）
                if (creature == null) continue;
                total += Levels.DissolveExpOf(creature);
                ReleaseCreature(game, id);
            }
            game.Idle.Exp += total;
            return total;
        }

        /// <summary>居なければ null。⚠️ <see cref="CreatureById"/> は投げる。</summary>
        private static Creature? FindCreature(Game game, string id)
        {
            foreach (var c in game.Storage.Creatures)
            {
                if (c.Id == id) return c;
            }
            return null;
        }

        /// <summary>戦闘の報酬。⭐ 出撃していた個体だけがもらう（連れ出すことが育成に直結する）。
        /// ⚠️ 移植元の規則（振り先を持たない）。遊びで使うのは <see cref="GrowParty"/>。</summary>
        public static void AwardParty(IReadOnlyList<Creature> party, int amount = 1)
        {
            foreach (var creature in party) Creatures.Award(creature, amount);
        }

        /// <summary>戦闘の報酬。⭐ 得意の方向へ自動で乗る（振り先は選ばせない）。</summary>
        public static void GrowParty(IReadOnlyList<Creature> party, int amount = 1)
        {
            foreach (var creature in party) Creatures.Grow(creature, amount);
        }

        /// <summary>巣の編成を何つ登録できるか。</summary>
        public const int NestPartySlots = 3;

        /// <summary>その用途の編成の中身。⭐ **ここが唯一の出口**。
        /// ⚠️ 各所で game.Party を直に見ない（見ると放置と巣の区別が崩れる）。</summary>
        public static List<string> RosterOf(Game game, PartyKind kind) =>
            kind == PartyKind.Idle ? game.Party : game.NestParties[Slot(game)];

        /// <summary>⚠️ 番号が範囲外なら 0 に丸める（古い保存・壊れた値除け）。</summary>
        public static int Slot(Game game) =>
            game.NestParty < 0 || game.NestParty >= NestPartySlots ? 0 : game.NestParty;

        /// <summary>出撃する3体。⚠️ 選んでいなければ素質の高い順に埋める（遊び始めで詰まらないように）。</summary>
        public static List<Creature> PartyOf(Game game) => PartyOf(game, PartyKind.Nest);

        public static List<Creature> PartyOf(Game game, PartyKind kind)
        {
            var roster = RosterOf(game, kind);
            var chosen = new List<Creature>();
            foreach (var id in roster)
            {
                foreach (var creature in game.Storage.Creatures)
                {
                    if (creature.Id == id) { chosen.Add(creature); break; }
                }
            }
            if (chosen.Count >= PartySize) return chosen.GetRange(0, PartySize);

            var rest = new List<Creature>();
            foreach (var creature in game.Storage.Creatures)
            {
                if (!chosen.Contains(creature)) rest.Add(creature);
            }
            rest.Sort((a, b) =>
            {
                int diff = Creatures.WildTotalOf(b) - Creatures.WildTotalOf(a);
                return diff != 0 ? diff : string.CompareOrdinal(a.Id, b.Id);
            });

            var all = new List<Creature>(chosen);
            all.AddRange(rest);
            return all.Count <= PartySize ? all : all.GetRange(0, PartySize);
        }

        /// <summary>出撃の入り切りを切り替える。⚠️ 上限を超えたら古いものから外す。</summary>
        public static void TogglePartyMember(Game game, string id) =>
            TogglePartyMember(game, id, PartyKind.Nest);

        public static void TogglePartyMember(Game game, string id, PartyKind kind)
        {
            var roster = RosterOf(game, kind);
            if (roster.Remove(id)) return;
            roster.Add(id);
            while (roster.Count > PartySize) roster.RemoveAt(0);
        }

        public static bool IsInParty(Game game, string id) =>
            IsInParty(game, id, PartyKind.Nest) || IsInParty(game, id, PartyKind.Idle);

        public static bool IsInParty(Game game, string id, PartyKind kind) =>
            RosterOf(game, kind).Contains(id);

        /// <summary>いまの編成をそのまま書き留める。⭐ **勝手に入れ替わらなくする**。
        ///
        /// ⚠️ <see cref="PartyOf"/> は選んでいない枠を「素質の高い順」で埋める。
        /// 便利だが、良い個体を手に入れた瞬間に編成が黙って変わってしまう。
        /// 手に入れた直後にここを通せば、それ以降は選んだ3体で固定される。
        /// ⚠️ 既に選んである枠は触らない（プレイヤーの選択を上書きしない）。</summary>
        public static void LockParty(Game game)
        {
            Lock(game, PartyKind.Idle);
            // ⚠️ 巣の編成は**3つとも**固める。使っていない番号を放置すると、
            //    切り替えた瞬間に「素質の高い順」で埋まって選んだはずの編成が消える。
            int keep = game.NestParty;
            for (int i = 0; i < NestPartySlots; i++)
            {
                game.NestParty = i;
                Lock(game, PartyKind.Nest);
            }
            game.NestParty = keep;
        }

        private static void Lock(Game game, PartyKind kind)
        {
            var roster = RosterOf(game, kind);
            if (roster.Count >= PartySize) return;
            foreach (var creature in PartyOf(game, kind))
            {
                if (roster.Count >= PartySize) break;
                if (!roster.Contains(creature.Id)) roster.Add(creature.Id);
            }
        }
    }
}
