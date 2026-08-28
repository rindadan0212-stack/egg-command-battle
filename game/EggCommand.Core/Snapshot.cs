#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    // ⭐ 保存の形。実行時の形（Game / Creature …）とは**別に**定義する。
    // ⚠️ 実行時の形をそのまま保存すると、遊びを直すたびに古い保存が読めなくなる。
    // ⚠️ 素の public 欄と [Serializable] だけで書く。Unity の JsonUtility が
    //    そのまま読み書きできる形にしておく（Core はエンジンに触らない）。

    [Serializable]
    public sealed class StatSave
    {
        public int Hp, Atk, Def, Spd;
        /// <summary>命中・抵抗。⚠️ 0 は「この2本より前の保存」＝振っていない、で正しく読める。</summary>
        public int Acc, Res;

        public static StatSave Of(StatBlock b) =>
            new StatSave { Hp = b.Hp, Atk = b.Atk, Def = b.Def, Spd = b.Spd, Acc = b.Acc, Res = b.Res };

        public StatBlock To() => new StatBlock(Hp, Atk, Def, Spd, Acc, Res);
    }

    [Serializable]
    public sealed class CreatureSave
    {
        public string Id = "", SpeciesId = "";
        public StatSave Wild = new StatSave(), Trained = new StatSave();
        /// <summary>🔴 **どのステに何点振ったか**（2026-08-26・ARK式の自由配分）。
        /// ⚠️ 古い保存には無い ── その場合は全部 0 ＝「未使用のまま」になり、
        /// ⭐ next に開いたとき **1度だけ振り直せる**（移行時だけの例外・振り直し不可の唯一の穴）。</summary>
        public StatSave Points = new StatSave();
        public int Earned, MutationCounter, PaletteIndex, Generation;
        public string? Skill2, Skill3, ParentA, ParentB;
        /// <summary>⚠️ -1 は「持たない」。enum を直に入れると 0 と区別が付かない。</summary>
        public int Strong = -1, Weak = -1;
        /// <summary>大得意・大不得意。⚠️ -1 は「大得意より前の保存」（そのまま持たない扱い）。</summary>
        public int Best = -1, Worst = -1;
        /// <summary>属性。⚠️ -1 は「属性を個体に持たせる前の保存」。種族の昔の属性で埋める。</summary>
        public int Element = -1;
        /// <summary>⚠️ **読まない欄**（2026-08-21 から特性は種族で決まる）。
        /// ⭐ 書き続けるのは、古い版のアプリでも保存が読めるようにするため。</summary>
        public string? Trait;
        /// <summary>枠ごとのスキルポイント。⚠️ 短い／空なら 0（スキルレベルより前の保存）。
        /// ⭐ レベルは保存しない（ポイントから導出する）。</summary>
        public List<int> SkillPoints = new List<int>();
    }

    [Serializable]
    public sealed class EggSave
    {
        public string Id = "", SpeciesId = "";
        public StatSave Wild = new StatSave();
        /// <summary>⚠️ **PaletteIndex は 2026-08-21 に卵から外した**（色は孵るときに引く）。
        /// 古い保存に入っていても読まない。</summary>
        public int MutationCounter, Generation, How, Rarity;
        public bool HasSkills;
        public string? Skill2, Skill3, ParentA, ParentB;
        public int Strong = -1, Weak = -1;
        public int Best = -1, Worst = -1;
        public int Element = -1;
        /// <summary>⚠️ **読まない欄**（<see cref="CreatureSave.Trait"/> と同じ）。</summary>
        public string? Trait;
    }

    [Serializable]
    public sealed class IncubationSave
    {
        public EggSave Egg = new EggSave();
        public long StartUnix, ReadyUnix;
        public int Slot;
    }

    [Serializable]
    public sealed class EncounterSave
    {
        public string NestId = "", Name = "", SpeciesId = "";
        public int Tier, Level;
        /// <summary>いつ居なくなるか。⚠️ 0 は「期限を持たない」（巣の寿命より前の保存）。</summary>
        public long UntilUnix;
    }

    [Serializable]
    public sealed class IdleSave
    {
        /// <summary>⚠️ <c>Materials</c> は 2026-08-19 より前の欄名。
        /// ⭐ 中身は同じもの（いまの名前は EXP）。読むときだけ拾う。</summary>
        public int Materials, Exp, Defeated;
        /// <summary>次の敵が現れてからの1拍（Idle.SpawnSeconds）。</summary>
        public double Spawn;
        public long LastUnix;
        public double EnemyHp, Charge;
        public List<string> DownIds = new List<string>();
        public List<long> DownUntil = new List<long>();
    }

    [Serializable]
    public sealed class GameSave
    {
        /// <summary>保存の版。⚠️ 形を変えたら上げる。合わなければ読まずに作り直す。</summary>
        public int Version = 1;
        public int Seed, Serial, EncounterSerial, Slots;
        public List<CreatureSave> Creatures = new List<CreatureSave>();
        public List<EggSave> Eggs = new List<EggSave>();
        public List<IncubationSave> Incubating = new List<IncubationSave>();
        public List<EncounterSave> Encounters = new List<EncounterSave>();
        public List<string> Party = new List<string>();
        /// <summary>巣の編成 3本を平らに並べたもの。
        /// ⚠️ JsonUtility は入れ子の List を書けないので、区切りの数と一緒に持つ。</summary>
        public List<string> NestParties = new List<string>();
        public List<int> NestPartyCounts = new List<int>();
        public int NestParty;
        public IdleSave Idle = new IdleSave();
        /// <summary>巣ごとに盗んだ回数。⚠️ 2本を添字で対にする（JsonUtility が辞書を書けない）。</summary>
        public List<string> RaidNests = new List<string>();
        public List<int> RaidCounts = new List<int>();
        /// <summary>乱数の系統ぶんの状態を平らに並べたもの（4語 × 系統数）。
        /// ⚠️ 件数は書かない。系統を足すたびに直す羽目になり、直し忘れが嘘になる。</summary>
        public List<uint> Rng = new List<uint>();
        /// <summary>勝った試練の id。⚠️ 無ければ「まだ1つも勝っていない」。</summary>
        public List<string> Trials = new List<string>();
        /// <summary>⭐ **一度でも手に入れた種族の id**（図鑑）。
        /// ⚠️ 空でも版は上げない ── 読む側が保管庫から**継ぎ足す**ので、
        /// 古い保存も「いま持っているぶんは載っている」状態から始まる。</summary>
        public List<string> Seen = new List<string>();
    }

    /// <summary>保存と復元。⭐ **ここが唯一の変換場所**。
    ///
    /// ⚠️ JSON にはしない。文字にするのは呼び側（Unity の JsonUtility）の仕事。
    /// Core はエンジンにもファイルにも触らないので、変換だけをここで閉じる。
    /// </summary>
    public static class Snapshots
    {
        public const int Version = 1;

        /// <summary>乱数の並び。⚠️ **順番を変えない**（保存した列と対応が崩れる）。
        /// ⭐ 足すのは末尾だけ。古い保存は短いので、読む側は届いたぶんだけ戻す。</summary>
        private static Rng[] StreamsOf(Game game) => new[]
        {
            game.RngNest, game.RngEgg, game.RngHatch, game.RngSteal,
            game.RngBreed, game.RngRarity, game.RngEncounter, game.RngSlant,
            game.RngElement, game.RngTrait,
            // ⚠️ **足すときは必ず末尾へ。**⭐ 読み込みは前から4語ずつ当てて、
            //    足りなくなったら止まる（`if (at + 4 > save.Rng.Count) break;`）ので、
            //    末尾に足すかぎり**古い保存もそのまま読める**。
            game.RngBattle,
            game.RngPalette,
        };

        public static GameSave Save(Game game)
        {
            var save = new GameSave
            {
                Version = Version,
                Seed = game.Seed,
                Serial = game.Serial,
                EncounterSerial = game.EncounterSerial,
                Slots = game.Storage.Slots,
            };

            foreach (var c in game.Storage.Creatures) save.Creatures.Add(Of(c));
            foreach (var e in game.Eggs) save.Eggs.Add(Of(e));
            foreach (var i in game.Incubating)
            {
                save.Incubating.Add(new IncubationSave
                {
                    Egg = Of(i.Egg), StartUnix = i.StartUnix, ReadyUnix = i.ReadyUnix, Slot = i.Slot,
                });
            }
            foreach (var e in game.Encounters)
            {
                save.Encounters.Add(new EncounterSave
                {
                    NestId = e.Nest.Id, Name = e.Nest.Name, SpeciesId = e.Nest.SpeciesId,
                    Tier = e.Nest.Tier, Level = e.Level, UntilUnix = e.UntilUnix,
                });
            }
            save.Party.AddRange(game.Party);
            save.NestParty = game.NestParty;
            foreach (var roster in game.NestParties)
            {
                save.NestPartyCounts.Add(roster.Count);
                save.NestParties.AddRange(roster);
            }

            save.Idle.Exp = game.Idle.Exp;
            save.Idle.Spawn = game.Idle.Spawn;
            save.Idle.Defeated = game.Idle.Defeated;
            save.Idle.LastUnix = game.Idle.LastUnix;
            save.Idle.EnemyHp = game.Idle.EnemyHp;
            save.Idle.Charge = game.Idle.Charge;
            foreach (var pair in game.Idle.DownUntil)
            {
                save.Idle.DownIds.Add(pair.Key);
                save.Idle.DownUntil.Add(pair.Value);
            }

            foreach (var pair in game.Raids)
            {
                save.RaidNests.Add(pair.Key);
                save.RaidCounts.Add(pair.Value);
            }

            save.Trials.AddRange(game.TrialsBeaten);
            save.Seen.AddRange(game.SpeciesSeen);

            foreach (var rng in StreamsOf(game)) save.Rng.AddRange(rng.State());
            return save;
        }

        /// <summary>復元する。
        ///
        /// ⚠️ **知らない種族・技の id が来ても投げない。** 遊びの最中なら投げるのが正しいが、
        /// ここで投げると「二度と開けないセーブ」になる。置き換えて先へ進み、
        /// 何をしたかを <paramref name="notes"/> に残す。
        ///
        /// ⚠️ 版が**新しすぎる**ときだけ null。古い版は既定値で埋めて読む
        /// （古いセーブを捨てるのは、直せない壊し方の中で一番よくあるもの）。</summary>
        public static Game? Load(GameSave? save, List<string>? notes = null)
        {
            if (save == null || save.Version > Version) return null;

            var game = new Game(save.Seed);
            game.Serial = save.Serial;
            game.EncounterSerial = save.EncounterSerial;

            var creatures = new List<Creature>();
            foreach (var c in save.Creatures)
            {
                var creature = To(c, notes);
                // ⚠️ 届いたぶんだけ戻す。古い保存は空なので 0 のまま＝素のスキル
                int slots = Math.Min(c.SkillPoints.Count, creature.SkillPoints.Length);
                for (int i = 0; i < slots; i++) creature.SkillPoints[i] = c.SkillPoints[i];
                creatures.Add(creature);
            }
            game.Storage = new Storage(save.Slots > 0 ? save.Slots : game.Storage.Slots, creatures);

            foreach (var e in save.Eggs) game.Eggs.Add(To(e, notes));
            foreach (var i in save.Incubating)
            {
                var hatching = To(i.Egg, notes);
                // 🔴 **枠の外に居る卵を黙って消さない。**⚠️ 孵化器の枠数（`Hatchery.Slots`）は
                //    減ることがある（2026-08-27: 6→5）。⭐ 枠から溢れた卵は**棚へ戻す**
                //    ── 画面が読まない番号に置いたままだと、持っているのに一生取り出せない。
                if (i.Slot < 0 || i.Slot >= Hatchery.Slots)
                {
                    game.Eggs.Add(hatching);
                    notes?.Add($"孵化器の {i.Slot} 番は今の枠数（{Hatchery.Slots}）の外なので、卵を棚へ戻した");
                    continue;
                }
                game.Incubating.Add(new Incubation(hatching, i.StartUnix, i.ReadyUnix, i.Slot));
            }
            foreach (var e in save.Encounters)
            {
                game.Encounters.Add(new Encounter(
                    new Nest(e.NestId, e.Name, ResolveSpecies(e.SpeciesId, notes), e.Tier),
                    e.Level, e.UntilUnix));
            }
            game.Party.AddRange(save.Party);
            // ⚠️ **表から消えた試練の id は捨てる。**⭐ 残すと「勝った印が付いているのに
            //    その段が無い」状態になり、数え方（TrialsCleared）と食い違う。
            foreach (var id in save.Trials)
            {
                if (Trials.Has(id)) game.TrialsBeaten.Add(id);
                else notes?.Add($"試練 {id} が表に無いので勝った印を外した");
            }
            // ⭐ **図鑑。**⚠️ `Games.See` を通すので、表から消えた種族は落ちる。
            foreach (var id in save.Seen) Games.See(game, id);
            // ⚠️ **保管庫から継ぎ足す。**⭐ 古い保存（この欄が無い頃のもの）でも、
            //    いま持っている個体ぶんは図鑑に載る ── 手元に居るのに
            //    「まだ見ていない」と出るほうが嘘になる。
            //    ⭐ 直し（self-heal）でもある: 口を通さずに入った個体を拾い直す。
            foreach (var creature in game.Storage.Creatures) Games.See(game, creature.SpeciesId);

            game.NestParty = save.NestParty;
            if (save.NestPartyCounts.Count == 0)
            {
                // ⭐ **古い保存。**編成が1本しか無かった頃のものなので、
                //    それを**放置と巣1の両方**へ引き継ぐ。
                //    ⚠️ 片方だけにすると、続きから始めた人の編成が片方だけ消える。
                game.NestParties[0].AddRange(save.Party);
            }
            else
            {
                int at = 0;
                for (int i = 0; i < Games.NestPartySlots; i++)
                {
                    int n = i < save.NestPartyCounts.Count ? save.NestPartyCounts[i] : 0;
                    for (int k = 0; k < n && at < save.NestParties.Count; k++, at++)
                        game.NestParties[i].Add(save.NestParties[at]);
                }
            }

            // ⚠️ 古い保存は Materials にしか入っていない。⭐ 新しい欄が空なら拾う
            game.Idle.Exp = save.Idle.Exp > 0 ? save.Idle.Exp : save.Idle.Materials;
            game.Idle.Spawn = save.Idle.Spawn;
            game.Idle.Defeated = save.Idle.Defeated;
            game.Idle.LastUnix = save.Idle.LastUnix;
            game.Idle.EnemyHp = save.Idle.EnemyHp;
            game.Idle.Charge = save.Idle.Charge;
            int pairs = Math.Min(save.Idle.DownIds.Count, save.Idle.DownUntil.Count);
            for (int i = 0; i < pairs; i++)
            {
                game.Idle.DownUntil[save.Idle.DownIds[i]] = save.Idle.DownUntil[i];
            }

            // ⚠️ 短いほうに合わせる。片方だけ壊れた保存で巣の難易度を捏造しない
            int raidPairs = Math.Min(save.RaidNests.Count, save.RaidCounts.Count);
            for (int i = 0; i < raidPairs; i++)
            {
                game.Raids[save.RaidNests[i]] = save.RaidCounts[i];
            }

            var streams = StreamsOf(game);
            for (int i = 0; i < streams.Length; i++)
            {
                int at = i * 4;
                if (at + 4 > save.Rng.Count) break;
                streams[i].Restore(new[]
                {
                    save.Rng[at], save.Rng[at + 1], save.Rng[at + 2], save.Rng[at + 3],
                });
            }
            return game;
        }

        // ── id の解決 ──────────────────────────────────
        // ⭐ セーブに入っているのは id の**文字そのもの**。表から消えた id を復元できる場所は
        //    ここしか無いので、引っ越し表を通すのもここ1か所に閉じる。

        private static string ResolveSpecies(string id, List<string>? notes)
        {
            string moved = Migrations.SpeciesOf(id);
            if (SpeciesTable.Has(moved)) return moved;

            // ⚠️ 見た目も属性も変わる。それでも「開かないセーブ」よりはましだと決めた
            var fallback = SpeciesTable.Fallback;
            notes?.Add($"種族 {id} が表に無いので {fallback.Id} で置き換えた");
            return fallback.Id;
        }

        private static string? ResolveSkill(string? id, List<string>? notes)
        {
            // ⚠️ 空文字も「無い」。Unity の JsonUtility は null 文字列を書けず "" にするので、
            //    null だけ見ていると空き枠のたびに嘘の引っ越し記録が1行出る
            if (string.IsNullOrEmpty(id)) return null;
            string moved = Migrations.SkillOf(id);
            if (Skills.Has(moved)) return moved;

            // ⚠️ 別の技で埋めない。埋めると「持っていない技を持っている」状態になる。
            //    枠が空くほうが、まだ読める
            notes?.Add($"技 {id} が表に無いので枠を空けた");
            return null;
        }

        /// <summary>読み込み時に特性を**種族から引き直す**。
        ///
        /// ⚠️ 保存に入っている値は見ない。2026-08-21 まで個体ごとに引いていたので、
        /// そのまま読むと**同じ種族なのに特性が14通りある箱**が残り続ける。
        /// ⭐ 特性は種族から一意に決まるので、読むたびに作り直せる ── 失うものは無い
        /// （育てた分を <see cref="Creatures.TrainedFor"/> で作り直すのと同じ約束）。</summary>
        private static string? ResolveTrait(string speciesId, string? saved, List<string>? notes)
        {
            string now = Creatures.TraitIdFor(speciesId);
            if (!string.IsNullOrEmpty(saved) && saved != now)
                notes?.Add($"特性を種族のもの（{now}）にした");
            return now;
        }

        // ── 個々の変換 ──────────────────────────────────

        private static int Key(StatKey? key) => key == null ? -1 : (int)key.Value;

        /// <summary>⚠️ -1 は属性を個体へ移す前の保存。null を返して種族の昔の属性に任せる。</summary>
        private static Element? Elem(int value)
        {
            if (value < 0) return null;
            foreach (var element in SpeciesTable.Elements)
            {
                if ((int)element == value) return element;
            }
            return null;
        }

        private static StatKey? Key(int value)
        {
            if (value < 0) return null;
            foreach (var key in Stats.Keys)
            {
                if ((int)key == value) return key;
            }
            return null;
        }

        private static CreatureSave Of(Creature c) => new CreatureSave
        {
            Id = c.Id, SpeciesId = c.SpeciesId,
            Wild = StatSave.Of(c.Wild), Trained = StatSave.Of(c.Trained),
            Points = StatSave.Of(c.Points),
            Earned = c.Earned, MutationCounter = c.MutationCounter,
            Skill2 = c.Skill2, Skill3 = c.Skill3, PaletteIndex = c.PaletteIndex,
            ParentA = c.ParentA, ParentB = c.ParentB, Generation = c.Generation,
            Strong = Key(c.Strong), Weak = Key(c.Weak),
            Best = Key(c.Best), Worst = Key(c.Worst), Element = (int)c.Element,
            Trait = c.TraitId,
            SkillPoints = new List<int>(c.SkillPoints),
        };

        private static Creature To(CreatureSave s, List<string>? notes) =>
            To(s, ResolveSpecies(s.SpeciesId, notes), notes);

        private static Creature To(CreatureSave s, string speciesId, List<string>? notes)
        {
            var points = s.Points.To();
            // 🔴 **振った点も渡す**（2026-08-26 に構築後の手当てで踏んだ事故の再発防止）。
            //    ⚠️ 2026-08-27（監査で発覚）: 以前は構築後に `made.Points = points;` と
            //    手で補っていた（`Creature` のコンストラクタに `points` が無かったため）。
            //    ⭐ コンストラクタが `points` を受けるようになったので、ここも構築の1回で
            //    渡す ── 「作ってから直す」の2段構えをやめて、忘れようが無い形にした。
            //    戻し忘れると `UnspentOf` が「まだ全部余っている」と答え、
            //    **同じ点を二度振れる**（実際に踏んだ）。
            var made = new Creature(
                s.Id, speciesId, s.Wild.To(),
                Creatures.TrainedFor(speciesId, s.Wild.To(), points), s.Earned,
                s.MutationCounter, ResolveSkill(s.Skill2, notes), ResolveSkill(s.Skill3, notes),
                ClampPalette(speciesId, s.PaletteIndex, s.Id, notes),
                s.ParentA, s.ParentB, s.Generation, Key(s.Strong), Key(s.Weak),
                Elem(s.Element), ResolveTrait(speciesId, s.Trait, notes),
                Key(s.Best), Key(s.Worst), points);
            return made;
        }

        // 🔴 **色の添字を種族の実際の色数に収める。**⚠️ 種族を改名／削除したり色表を
        //    縮めたりすると、古い保存の `PaletteIndex` が範囲外のまま残る。読み込み
        //    （`Load`）自体は成功するのに、BOX を描いた瞬間に `ArgumentOutOfRangeException`
        //    で落ち、そのセーブが二度と開けなくなっていた（2026-08-25 監査で発覚）。
        //    ⭐ 通常色（0）へ倒し、note に残す（作り直さない ── ここで直すと個体の色が
        //    黙って変わるので、次に開いたとき気づけるようにする）。
        private static int ClampPalette(string speciesId, int index, string creatureId, List<string>? notes)
        {
            int count = SpeciesTable.ById(speciesId).Palettes.Count;
            if (index >= 0 && index < count) return index;
            notes?.Add($"{creatureId}: 色 {index} 番が無い（{speciesId} は {count} 色）── 通常色へ戻した");
            return 0;
        }

        // ⚠️ 育てた分は保存から読まず、**Lv から作り直す**（Creatures.TrainedFor）。
        //    育成の規則を 2026-08-19 に二度変えており（得意1本 → 平らに＋1 → 素質の割合）、
        //    古い保存をそのまま読むと、同じ Lv なのに弱い個体が箱に残り続ける。
        // ⭐ 育てた分は Lv から一意に決まるので、読むたびに作り直せる ── 失うものは無い。
        // ⚠️ 保存の欄そのものは残す（読めなくなる版を作らない）。

        private static EggSave Of(Egg e) => new EggSave
        {
            Id = e.Id, SpeciesId = e.SpeciesId, Wild = StatSave.Of(e.Wild),
            MutationCounter = e.MutationCounter,
            Generation = e.Generation, How = (int)e.How, Rarity = e.Rarity,
            HasSkills = e.HasSkills, Skill2 = e.Skill2, Skill3 = e.Skill3,
            ParentA = e.ParentA, ParentB = e.ParentB,
            Strong = Key(e.Strong), Weak = Key(e.Weak),
            Best = Key(e.Best), Worst = Key(e.Worst), Element = (int)e.Element,
        };

        private static Egg To(EggSave s, List<string>? notes) => new Egg(
            s.Id, ResolveSpecies(s.SpeciesId, notes), s.Wild.To(), s.MutationCounter,
            s.ParentA, s.ParentB, s.Generation, (EggOrigin)s.How,
            s.HasSkills, ResolveSkill(s.Skill2, notes), ResolveSkill(s.Skill3, notes),
            s.Rarity, Key(s.Strong), Key(s.Weak), Elem(s.Element),
            Key(s.Best), Key(s.Worst));
    }
}
