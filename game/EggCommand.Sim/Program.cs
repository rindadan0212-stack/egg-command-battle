#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using EggCommand.Core;

namespace EggCommand.Sim
{
    /// <summary>自動対戦の測定器。
    ///
    /// ⭐ **数値を勘で置かないための仕組み。**「この値が良いはず」ではなく、ここに当てて決める。
    ///
    /// ⚠️ 前身の `scripts/sim.mjs` は **TypeScript 実装**を測っていた。
    /// Unity へ移したあとに足した 放置・レベル・配合・希少さ は TS 側に無いので、
    /// あれを回しても「いま遊んでいるもの」は一切測れない。ここは Core を直接呼ぶ。
    ///
    /// ⚠️ 個体づくりも**本番の経路**（MakeEgg → Hatch）を通す。
    /// 測定用に個体を組み立て直すと、卵ガチャの偏りが測定から消える。
    ///
    /// 使い方:
    ///   dotnet run --project EggCommand.Sim -- species   種族どうしの勝率行列
    ///   dotnet run --project EggCommand.Sim -- skills    技ごとの採用率（**死んでいる技**を探す）
    ///   dotnet run --project EggCommand.Sim -- elements  3すくみが効いているか
    ///   dotnet run --project EggCommand.Sim -- roles     役割を1つ抜いたらどれだけ困るか
    ///   dotnet run --project EggCommand.Sim -- traits    特性を付け外しするとどれだけ違うか
    ///   dotnet run --project EggCommand.Sim -- steal     潜入が解けるか（段階 × 盗まれた回数）
    ///   dotnet run --project EggCommand.Sim -- pace      決着までの行動数
    ///   dotnet run --project EggCommand.Sim -- book      図鑑を書き出す（種族・技・特性）
    ///   dotnet run --project EggCommand.Sim -- wiki      Wiki の表ページを書き出す（数値の二重管理を避ける）
    ///   dotnet run --project EggCommand.Sim -- wikinames 手書きの Wiki に、実装に無い名前が残っていないか
    ///   dotnet run --project EggCommand.Sim -- record    現行の記録を作り直す（⚠️ 遊びを変えたときだけ）
    ///   dotnet run --project EggCommand.Sim -- slant     得意・不得意が素質と独立して引かれているか
    ///   dotnet run --project EggCommand.Sim -- statvalue ステ1点が勝率を何 pt 動かすか（ステごとの価値差）
    ///   dotnet run --project EggCommand.Sim -- skillvalue 技1つが勝率を何 pt 動かすか（特性と同じ物差し）
    ///   dotnet run --project EggCommand.Sim -- grade      技を格（生の値段）の順に1枚へ並べる
    ///   dotnet run --project EggCommand.Sim -- turnvalue  1手で何手ぶんを生むか（算数。AI を通さない）
    ///   dotnet run --project EggCommand.Sim -- delivered  算数の見積もりが実戦で入っているか（食い違いを掘る）
    ///   dotnet run --project EggCommand.Sim -- import-sprite 手描きの原稿（art/handmade/sprite/*.png）を
    ///                                          Species.cs に貼れる C# へ落とす（⚠️ 貼るのは人の仕事）
    ///   dotnet run --project EggCommand.Sim -- egg-art        種族ごとの卵を焼き直す（意匠は Core.EggSkins）
    ///   dotnet run --project EggCommand.Sim -- egg-try [地 模様] 模様と色の見本を1枚に並べる（shots/）
    ///   dotnet run --project EggCommand.Sim -- paint-placeholder 骨組みが指す `paint` の絵で、
    ///                                          まだ無いものを仮置きで作る（ドット絵化計画 段取り4）
    ///   dotnet run --project EggCommand.Sim -- icon-manifest      `icon` の実寸目録
    ///                                          （assets/ui/icon/icon-manifest.txt）を作り直す
    /// </summary>
    public static class Program
    {
        private const int DefaultSeed = 2026_08_16;

        /// <summary>絵や骨組みを書き出す道具のための、この作品の根。
        ///
        /// 🔴 **走らせた場所から上へ辿って探す。**⚠️ 決め打ちの `".."` は、`game/` から
        /// 走らせる約束に頼っていた ── 根から走らせると1つ上（`Desktop/gamedev/`）に
        /// `assets/ui/` を生やす（2026-08-29 に実際にそうなった。害は出なかったが、
        /// **黙って親のフォルダへ書く**のが怖い）。
        /// ⭐ 目印は `assets/layouts`（この作品にしか無く、道具が必ず要る場所）。
        /// ⚠️ 見つからなければ**止まる**。当てずっぽうで書き出さない。</summary>
        private static string FindRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int up = 0; dir != null && up < 6; up++, dir = dir.Parent)
                if (Directory.Exists(Path.Combine(dir.FullName, "assets", "layouts")))
                    return dir.FullName;
            throw new DirectoryNotFoundException(
                $"`assets/layouts` が見つからない（{Directory.GetCurrentDirectory()} から上へ6段まで探した）"
                + " ── この作品の中で走らせること");
        }

        public static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            string what = args.Length > 0 ? args[0] : "all";
            int seed = DefaultSeed;
            int bump = 0;
            int levels = 0;
            for (int i = 1; i < args.Length - 1; i++)
            {
                if (args[i] == "--seed") int.TryParse(args[i + 1], out seed);
                // ⭐ 足す量を外から変えられる。⚠️ 上下限のあるステは増やしても飽和するので、
                //    「配る量を増やせば価値が揃うのか」はここを動かして確かめる。
                if (args[i] == "--bump") int.TryParse(args[i + 1], out bump);
                if (args[i] == "--levels") int.TryParse(args[i + 1], out levels);
            }

            // ⚠️ 中身が繋がっていない状態で測っても意味が無い。先に数える
            Content.Audit();

            // 🔴 **走らせた場所から根を探す。**⚠️ 以前は `".."` 決め打ちだった ──
            //    `game/` から走らせる前提で、**根から走らせると1つ上（`gamedev/`）へ
            //    書き出してしまう**（2026-08-29 に実際にやり、Desktop に `assets/ui/` が
            //    17ファイル生えた）。⭐ 黙って親を汚すより、見つからないなら止まるほうがよい。
            string root = FindRoot();

            switch (what)
            {
                case "species": Species(seed); break;
                case "skills": SkillCensus(seed); break;
                // ⭐ 技を**格**の順に並べる（戦闘は回さない・一瞬で出る）
                case "grade": SkillGrade.Run(); break;
                // 🚧 勘で置いた見積り（挑発・免疫・ガッツ・蘇生）を実測で潰す
                case "guess": GuessProbe.Run(seed); break;
                // ⭐ 技を組み合わせで作り直す（候補を数える）
                case "brew": Brew.Run(args[1..]); break;
                // ⭐ 参考作品の技を本作の手ぶんで測る（物差しの検算）
                case "mamo": MamoValue.Run(root); break;
                case "elements": Elements(seed); break;
                case "roles": Roles(seed); break;
                // ⭐ 4対4・弱化ビルドを実際に組んで測る（`roles` の4つの欠陥を直した版）
                case "debuff": DebuffProbe(seed, levels); break;
                // ⭐ ダメージ式の案を実戦で比べる（`Battle.DamageOverride` を差し替える）
                case "damagemodel": DamageModelProbe(seed); break;
                // ⭐ ARK式の自由配分が「判断」になるかを式ごとに見る
                case "allocate": AllocateProbe(seed); break;
                // ⭐ 弱化命中と弱化耐性を**両側同時に**動かして噛み合いを見る
                case "resist": ResistProbe(seed); break;
                // ⭐ 命中と速度を混ぜたときに相乗があるか（二者択一では見えない）
                case "mix": MixProbe(seed); break;
                // ⭐ ステが実際どこまで行くか（桁を動かす前に測る）
                case "range": RangeProbe(seed); break;
                // ⭐ 生の桁ではなく「効き目の幅」を見る（桁を決める根拠）
                case "feel": FeelProbe(seed); break;
                // ⭐ 通る率の帯[25,95]と感度を振って、弱化の投資価値が上がるか見る
                case "landband": LandBandProbe(seed); break;
                // ⭐ 通る率を実数で出す（式の単位を突き合わせるため）
                case "landcalc": LandCalcProbe(seed); break;
                // ⭐ 弱化技を UR 級の設計に組み替えると席が取れるか
                case "urskill": UrSkillProbe(seed); break;
                case "traits": TraitCensus(seed); break;
                case "steal": Infiltrate(seed); break;
                case "gencost": GenCost(); break;
                case "genstress": GenStress(); break;
                case "ramp": Ramp(); break;
                case "growth": Growth(); break;
                case "wiki":
                {
                    // ⚠️ 置き場所は決め打ち。⭐ 数値の出所を実装1つに保つための生成
                    var made = WikiPages.Write("../wiki");
                    Console.WriteLine("Wiki を書き出した: " + string.Join(" / ", made));
                    break;
                }
                // ⭐ Wiki に出てくる名前が実装に在るか（転記は腐る）
                case "wikinames": WikiNames.Run(); break;
                // ⭐ 親との戦い（巣から最後の卵を得る唯一の道）
                case "boss": BossProbe.Run(seed); break;
                case "trial": TrialProbe.Run(seed); break;
                case "lineage": LineageProbe.Run(seed); break;
                case "pace": Pace(seed); break;
                case "landprobe": LandProbe(); break;
                case "flight": FlightProbe(seed); break;
                case "trail": TrailProbe(seed); break;
                case "dice": DiceProbe.Run(seed); break;
                case "sprites": SpritePng.Run(root); break;
                // ⭐ 手描きの原稿（art/handmade/sprite/*.png）→ Species.cs に貼れる C#（再実行できる取り込み道具）
                case "import-sprite": SpriteImport.Run(root); break;
                // ⭐ まだ無い paint の絵を仮置きで作る（ドット絵化計画 段取り4・第3部）
                case "paint-placeholder": PaintPlaceholder.Run(root); break;
                // ⭐ icon の実寸目録を作り直す（ドット絵化計画 段取り4・「1ドット=4px」統一）
                case "icon-manifest": IconManifestTool.Run(root); break;
                // ⭐ 種族ごとの卵を焼く（意匠は `Core.EggSkins`）。⚠️ 上書きする道具
                case "egg-art": EggSkinPng.Run(root); break;
                // ⭐ 模様と色を差し替えて見比べる見本（`shots/` へ・ゲームは読まない）
                case "egg-try": EggTry.Run(root, args[1..]); break;
                // ⭐ pixelizer で起こした画面を、絵と骨組みに落とす
                //    （wiki/開発/画面をドット絵で組む.md）。⚠️ 既存の骨組みは上書きしない。
                case "import-screen":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("sim import-screen <.pixelizer.json のパス>");
                        Console.WriteLine("  例: sim import-screen ../art/screens/home.pixelizer.json");
                        return 1;
                    }
                    return ImportScreen.Run(root, args[1]);
                case "determinism": Console.WriteLine(Determinism.Run()); break;   // ⚠️ 他の出力と同じく cwd 相対（game から打つ）
                case "strategy":
                    // ⭐ `sim strategy 4` で4対4。⚠️ 既定を変えない（3対3の記録が読めなくなる）
                    StrategyProbe(seed, args.Length > 1 && args[1] == "4" ? 4 : 3);
                    break;
                case "speed": SpeedProbe(seed); break;
                // ⭐ 技と種族を手で書くための帳面（Sheet.cs）
                case "sheet": Sheet.Run(args.Length > 1 ? args[1] : ""); break;
                case "slant": SlantProbe(seed); break;
                case "statvalue": StatValue(seed, bump, levels); break;
                case "skillvalue": SkillValue(seed); break;
                case "turnvalue": TurnValue(); break;
                case "delivered": Delivered(seed); break;
                case "record":
                {
                    // ⚠️ 意図して遊びを変えたときだけ走らせる（SeriesRecord の注記）
                    Console.WriteLine("現行の記録を書き直した: " + SeriesRecord.Write("records/series.json"));
                    break;
                }
                case "book":
                {
                    // ⚠️ 置き場所は決め打ち。毎回同じ場所に上書きする（版が散らからないように）
                    string where = Book.Write("../図鑑.html");
                    Console.WriteLine($"図鑑を書き出した: {where}");
                    break;
                }
                case "all":
                    Species(seed); SkillCensus(seed); Elements(seed); Roles(seed);
                    TraitCensus(seed); Pace(seed);
                    break;
                default:
                    Console.WriteLine($"知らない指定: {what}");
                    return 1;
            }
            return 0;
        }

        // ── 対戦を1回まわす ────────────────────────────────

        private sealed class Fight
        {
            public Outcome Result;
            public int Actions;
            /// <summary>誰がどの技を何回選んだか。⭐ 死んでいる技を探すのはここ。</summary>
            public readonly Dictionary<string, int> Chosen = new Dictionary<string, int>();
            /// <summary>場に出ていた技（選ばれたかは問わない）。採用率の分母。</summary>
            public readonly Dictionary<string, int> Present = new Dictionary<string, int>();
            /// <summary>枠ごとの選ばれた回数。⭐ 「枠1ばかり撃っている」かを数える唯一の出所。</summary>
            public readonly int[] BySlot = new int[3];
            /// <summary>手番が回ってきたとき、枠が待ちで塞がっていた回数。</summary>
            public readonly int[] Locked = new int[3];
            /// <summary>⭐ **味方側が取った手番の数。**速度の効きを直に測るための欄。
            /// ⚠️ 勝敗と違って side の有利不利が混ざらない
            /// （どちらが先に倒れたかではなく、何回動けたかを数える）。</summary>
            public int AllyActions;
        }

        /// <param name="land">弱化が通るかを引く乱数。
        /// ⚠️ **渡さないと全戦闘が同じ列になる**（`CreateBattle` の既定が固定の種）。
        /// 何回まわしても弱化の当たり外れは1標本ぶんしか無い、という事故が起きる。
        /// ⚠️ 既にある測定（species / elements / roles / pace）は渡していない ＝
        /// 記録済みの数値を動かさないため。⭐ 直すなら記録ごと測り直すこと。</param>
        private static Fight Run(IReadOnlyList<Creature> allies, IReadOnlyList<Creature> foes,
            Rng? land = null)
        {
            var fight = new Fight();
            var state = Battle.CreateBattle(allies, foes, land);

            foreach (var unit in state.Units)
            {
                for (int slot = 0; slot < 3; slot++)
                {
                    var skill = Battle.SkillAt(unit, slot);
                    if (skill != null) Bump(fight.Present, skill.Id);
                }
            }

            while (state.Result == null && fight.Actions < Battle.MaxActions)
            {
                var actor = Battle.NextActor(state);
                if (actor == null) break;

                // ⚠️ 選ぶ前に数える（選んだ結果ではなく、選べたかを見たい）
                for (int i = 1; i < 3; i++)
                {
                    if (Battle.SkillAt(actor, i) != null && actor.Cooldowns[i] > 0) fight.Locked[i]++;
                }

                int slot = Ai.ChooseAction(state, actor);
                if (slot >= 0 && slot < fight.BySlot.Length) fight.BySlot[slot]++;
                var skill = Battle.SkillAt(actor, slot);
                if (skill != null) Bump(fight.Chosen, skill.Id);

                Battle.PerformAction(state, actor, slot);
                fight.Actions++;
                if (actor.Side == Side.Ally) fight.AllyActions++;
            }

            fight.Result = state.Result ?? Outcome.Draw;
            return fight;
        }

        private static void Bump(Dictionary<string, int> counter, string key)
        {
            int n;
            counter[key] = counter.TryGetValue(key, out n) ? n + 1 : 1;
        }

        // ── 個体づくり。⭐ 本番と同じ経路（巣 → 卵 → 孵化）────────────

        /// <param name="element">⚠️ 指定しなければ引く。属性は**個体**が持つので、
        /// 測るときも個体ごとに決める（種族から決まらない）。</param>
        private static Creature Born(Rng rng, string speciesId, int tier, ref int serial,
            Element? element = null)
        {
            // ⚠️ 巣は「その種族の卵が出る器」としてだけ使う。表に無い巣でも Nest は作れる
            var nest = new Nest($"sim-{speciesId}-{tier}", "測定", speciesId, tier);
            var egg = Nests.MakeEgg(rng, nest, EggOrigin.Defeated, ++serial,
                element: element ?? SpeciesTable.Roll(rng));
            return Nests.Hatch(rng, egg, $"s{serial:D4}");
        }

        private static List<Creature> Party(Rng rng, string speciesId, int tier, ref int serial,
            Element? element = null)
        {
            var party = new List<Creature>();
            for (int i = 0; i < Games.PartySize; i++)
            {
                party.Add(Born(rng, speciesId, tier, ref serial, element));
            }
            return party;
        }

        /// <summary>属性を混ぜた編成。⭐ **まもダン型のモンスターシステムでは編成は混ざる。**
        /// 単一属性の編成だけを測ると、属性の効き目を過大に見積もる。</summary>
        private static List<Creature> MixedParty(Rng rng, int tier, ref int serial)
        {
            var ids = new List<string>();
            foreach (var s in SpeciesTable.All) ids.Add(s.Id);

            var party = new List<Creature>();
            for (int i = 0; i < Games.PartySize; i++)
            {
                party.Add(Born(rng, ids[rng.Int(0, ids.Count)], tier, ref serial));
            }
            return party;
        }

        private static string Pct(int part, int whole) =>
            whole == 0 ? "  - " : $"{100.0 * part / whole,5:0.0}%";

        // ── 種族どうし ────────────────────────────────────

        /// <summary>⭐ **種族を足した日に一番見たいもの。**
        ///
        /// ⚠️ **総合勝率を 50% に揃えにいかない**（作者の判断 2026-08-19）。
        /// 種族には得意不得意があり、ビルドも違うので、**差が出るのは必然**。
        /// 揃えにいくと、せっかく作った「止める／重い／配る」という顔が消える。
        /// ⭐ 物には役割がある。適材適所。
        ///
        /// ⭐ **見るのは総合ではなく「刺さる相手を持っているか」。**
        /// | 勝ち越す相手が 0 | どこにも居場所が無い ── 役割が無い |
        /// | 負け越す相手が 0 | 誰の役割も奪う ── 対策が存在しない |
        /// ⚠️ 総合が 30% でも、3種族に勝ち越しているなら**それは役割**であって欠陥ではない。</summary>
        private static void Species(int seed)
        {
            const int Samples = 40;
            const int Tier = 5;

            var ids = new List<string>();
            foreach (var s in SpeciesTable.All) ids.Add(s.Id);

            Console.WriteLine();
            Console.WriteLine($"■ 種族どうしの勝率（段階{Tier}・各{Samples}回・行が攻め手）");
            Console.Write("            ");
            foreach (var id in ids) Console.Write($"{id,10}");
            Console.WriteLine("      総合  勝ち越し  負け越し");

            foreach (var a in ids)
            {
                Console.Write($"{a,12}");
                int wonAll = 0, playedAll = 0, beats = 0, loses = 0;
                foreach (var b in ids)
                {
                    int won = 0;
                    for (int i = 0; i < Samples; i++)
                    {
                        var rng = new Rng(seed + i).Stream($"{a}v{b}");
                        int serial = 0;
                        var fight = Run(Party(rng, a, Tier, ref serial), Party(rng, b, Tier, ref serial));
                        if (fight.Result == Outcome.Ally) won++;
                    }
                    Console.Write($"{Pct(won, Samples),10}");
                    // ⚠️ 自分どうし（鏡）は数えない。必ず 50% 付近になるので意味が無い
                    if (a == b) continue;
                    wonAll += won;
                    playedAll += Samples;
                    // ⭐ 誤差（±8%）を跨いだものだけを「刺さる／刺さらない」と数える
                    if (100 * won / Samples >= 58) beats++;
                    if (100 * won / Samples <= 42) loses++;
                }
                Console.WriteLine($"{Pct(wonAll, playedAll),10}{beats,8}{loses,8}");
            }

            Console.WriteLine();
            Console.WriteLine("  ⭐ **総合は揃えなくてよい。**種族には得意不得意があり、差が出るのは必然。");
            Console.WriteLine("  ⚠️ 見るのは右の2列 ── **勝ち越す相手が 0**（居場所が無い）と");
            Console.WriteLine("     **負け越す相手が 0**（対策が存在しない）だけが直し先。");
        }

        // ── 技ごとの採用率 ──────────────────────────────────

        /// <summary>⭐ **技を増やすときに一番効く測定。**
        /// 場に出ているのに一度も選ばれない技は、あっても無いのと同じ。
        /// ⚠️ 表に足しただけでは「増えた」ことにならない、をここで数字にする。</summary>
        private static void SkillCensus(int seed)
        {
            const int Battles = 400;

            var ids = new List<string>();
            foreach (var s in SpeciesTable.All) ids.Add(s.Id);

            var chosen = new Dictionary<string, int>();
            var present = new Dictionary<string, int>();
            int actions = 0;

            for (int i = 0; i < Battles; i++)
            {
                var rng = new Rng(seed + i).Stream("census");
                int serial = 0;
                string a = ids[rng.Int(0, ids.Count)];
                string b = ids[rng.Int(0, ids.Count)];
                int tier = rng.Int(1, 6);

                var fight = Run(Party(rng, a, tier, ref serial), Party(rng, b, tier, ref serial));
                actions += fight.Actions;
                foreach (var pair in fight.Chosen) Bump2(chosen, pair.Key, pair.Value);
                foreach (var pair in fight.Present) Bump2(present, pair.Key, pair.Value);
            }

            Console.WriteLine();
            Console.WriteLine($"■ 技ごとの採用（{Battles}戦・のべ{actions}行動）");
            Console.WriteLine($"{"技",-16}{"場に出た枠",12}{"選ばれた",10}{"枠あたり",10}");

            var dead = new List<string>();
            foreach (var skill in Skills.All)
            {
                int p, c;
                present.TryGetValue(skill.Id, out p);
                chosen.TryGetValue(skill.Id, out c);
                double per = p == 0 ? 0 : (double)c / p;
                Console.WriteLine($"{skill.Name,-16}{p,12}{c,10}{per,10:0.00}");
                if (p > 0 && c == 0) dead.Add(skill.Name);
                if (p == 0) dead.Add($"{skill.Name}(場に出ない)");
            }

            Console.WriteLine();
            if (dead.Count == 0) Console.WriteLine("  ⭐ 死んでいる技は無い");
            else Console.WriteLine($"  ⚠️ 一度も選ばれない技: {string.Join(" / ", dead)}");
        }

        private static void Bump2(Dictionary<string, int> counter, string key, int by)
        {
            int n;
            counter[key] = counter.TryGetValue(key, out n) ? n + by : by;
        }

        // ── 3すくみ ──────────────────────────────────────

        private static void Elements(int seed)
        {
            const int Samples = 60;
            const int Tier = 5;

            // ⭐ 属性は個体が持つので、種族は同じにして**属性だけ**を変える。
            //    これで「属性の効き目」だけを取り出せる（種族の差が混ざらない）
            const string Same = "tamaru";

            Console.WriteLine();
            Console.WriteLine($"■ 3すくみ（段階{Tier}・各{Samples}回・種族は揃えて属性だけ変える）");
            foreach (var attacker in SpeciesTable.Elements)
            {
                var beaten = SpeciesTable.Beats(attacker);

                int won = 0;
                for (int i = 0; i < Samples; i++)
                {
                    var rng = new Rng(seed + i).Stream($"elem{attacker}");
                    int serial = 0;
                    var fight = Run(
                        Party(rng, Same, Tier, ref serial, attacker),
                        Party(rng, Same, Tier, ref serial, beaten));
                    if (fight.Result == Outcome.Ally) won++;
                }
                Console.WriteLine(
                    $"  {SpeciesTable.LabelOf(attacker)} → {SpeciesTable.LabelOf(beaten)}（有利）" +
                    $"  勝率 {Pct(won, Samples)}");
            }
            Console.WriteLine("  ⚠️ 有利側が 50% 付近なら、属性が勝敗に効いていない");

            // ⭐ 上は**編成3体が全部同じ属性**の場合。倍率が編成まるごとに掛かるので、
            //    有利 ×1.5 と 不利 ×0.75 が両側で重なって **2.0倍の差**になる。
            //    実際に遊ぶときの編成が混成なら、差は組み合わせごとに散る。どちらなのかを測る。
            Console.WriteLine();
            Console.WriteLine($"  ・編成の属性を混ぜた場合（{Samples}回）");
            int mixedWon = 0, mixedDraw = 0;
            for (int i = 0; i < Samples; i++)
            {
                var rng = new Rng(seed + i).Stream("mixed");
                int serial = 0;
                var fight = Run(MixedParty(rng, Tier, ref serial), MixedParty(rng, Tier, ref serial));
                if (fight.Result == Outcome.Ally) mixedWon++;
                if (fight.Result == Outcome.Draw) mixedDraw++;
            }
            Console.WriteLine($"    先手側の勝率 {Pct(mixedWon, Samples)}（引き分け {mixedDraw}）");
            Console.WriteLine("    ⭐ 50% 付近なら、混成では属性が勝敗を決めきっていない");
        }

        // ── 型（特化 と 均等）────────────────────────────────

        /// <summary>役割ごとの貢献度。**「型どうしの勝率」ではない。**
        ///
        /// ⚠️ 以前はここで「攻速の編成 vs HP防の編成」を戦わせて型の強弱を測っていた。
        /// それは**3体とも同じ役割しか持たない編成**どうしの勝負で、実際の遊びの形ではない。
        /// 属性を単一属性どうしで測って 100% と読み違えたのと同じ間違いだった。
        ///
        /// ⭐ 役割の価値は「その役だけで勝てるか」ではなく
        /// **「抜けたときにどれだけ困るか」**に出る。攻撃は勝率に直に出るが、
        /// 弱化役や壁役の価値は勝率の数字そのものには出にくい。
        /// だから**揃った編成を基準に、1つずつ抜いて落ち込みを測る**。
        ///
        /// ⚠️ 落ち込みが 0 に近い役は「居ても居なくても同じ」＝その役が仕事をしていない。</summary>
        private static void Roles(int seed)
        {
            const int Samples = 120;
            const int Tier = 5;
            int total = Stats.WildTotalMax;
            int high = total * 3 / 8;
            int low = total / 8;

            // ⭐ 役割はステの寄せ方で作る。⚠️ どの役も全ステに下限を残す
            //    （2ステを0にすると「役が弱い」ではなく「HPが無いから死ぬ」を測る）
            // ⚠️ 役割は**ステだけでは作れない**。技を卵ガチャ任せにしていたときは
            //    どの役を抜いても落ち込みが 0 だった（弱化役が弱化技を持っていなかった）。
            //    ⭐ 役割 = 寄せたステ + それを活かす技。両方を揃えて初めて役になる。
            var attacker = new Role("攻撃役", new StatBlock(low, high, low, high),
                "attack-heavy", "attack-twice");
            var tank = new Role("壁役", new StatBlock(high, low, high, low),
                "bulwark", "harden");
            var support = new Role("弱化役", new StatBlock(high, low, low, high),
                "curse", "slow-all");
            var even = new Role("均等", new StatBlock(total / 4, total / 4, total / 4, total - total / 4 * 3),
                "attack", "def-up");

            var full = new[] { attacker, tank, support };

            Console.WriteLine();
            Console.WriteLine($"■ 役割の貢献度（段階{Tier}・各{Samples}回）");
            Console.WriteLine("  基準 = 攻撃役・壁役・弱化役の3体。相手は毎回同じ基準の編成");

            int baseWon = 0;
            for (int i = 0; i < Samples; i++)
            {
                var rng = new Rng(seed + i).Stream("roles-base");
                int serial = 0;
                var fight = Run(Shaped(rng, full, ref serial), Shaped(rng, full, ref serial));
                if (fight.Result == Outcome.Ally) baseWon++;
            }
            Console.WriteLine($"  基準どうし            {Pct(baseWon, Samples)}");

            for (int drop = 0; drop < full.Length; drop++)
            {
                // 抜いた役を「均等」に置き換える。⚠️ 2体にすると体数の差を測ってしまう
                var missing = new Role[full.Length];
                for (int k = 0; k < full.Length; k++) missing[k] = k == drop ? even : full[k];

                int won = 0;
                for (int i = 0; i < Samples; i++)
                {
                    var rng = new Rng(seed + i).Stream($"roles-{drop}");
                    int serial = 0;
                    var fight = Run(Shaped(rng, missing, ref serial), Shaped(rng, full, ref serial));
                    if (fight.Result == Outcome.Ally) won++;
                }
                double drop_pp = 100.0 * baseWon / Samples - 100.0 * won / Samples;
                Console.WriteLine(
                    $"  {full[drop].Name}を均等に替える  {Pct(won, Samples)}   落ち込み {drop_pp,5:0.0}pt");
            }

            Console.WriteLine("  ⚠️ 落ち込みが 0 付近の役は、居ても居なくても同じ＝仕事をしていない");
        }

        /// <summary>役割ごとに素質を差し替えた編成。⚠️ 技は本番どおり卵ガチャで引く
        /// （技を固定すると「役の差」ではなく「技の差」を測ってしまう）。</summary>
        private sealed class Role
        {
            public readonly string Name;
            public readonly StatBlock Wild;
            public readonly string Skill2;
            public readonly string Skill3;

            public Role(string name, StatBlock wild, string skill2, string skill3)
            {
                Name = name; Wild = wild; Skill2 = skill2; Skill3 = skill3;
            }
        }

        private static List<Creature> Shaped(Rng rng, Role[] roles, ref int serial)
        {
            var party = new List<Creature>();
            var ids = new List<string>();
            foreach (var s in SpeciesTable.All) ids.Add(s.Id);

            for (int i = 0; i < Games.PartySize && i < roles.Length; i++)
            {
                string speciesId = ids[rng.Int(0, ids.Count)];
                var born = Born(rng, speciesId, 5, ref serial);
                party.Add(new Creature(
                    born.Id, born.SpeciesId, roles[i].Wild, born.Trained, born.Earned,
                    born.MutationCounter, roles[i].Skill2, roles[i].Skill3, born.PaletteIndex,
                    born.ParentA, born.ParentB, born.Generation, born.Strong, born.Weak,
                    born.Element, born.TraitId));
            }
            return party;
        }

        // ── 弱化の役割（2026-08-26・作者の指示）──────────────────
        //
        // 🔴 **既にある `roles`/`statvalue` は弱化ビルドを一度も測っていない。**
        //    4つの欠陥が重なっていた（2026-08-26 に判明）:
        //    1. 役割が3体 ── 実物は `Games.PartySize = 4`
        //    2. 弱化役の素質 Acc/Res が **0**（`new StatBlock(hp,atk,def,spd)` の既定）
        //    3. 弱化技が `curse`/`slow-all` だけ ── スタンも毒も防御DOWNも入っていない
        //    4. `Run` に `land` を渡していない ＝ **弱化の当たり外れが全戦闘で同じ列**
        //       （種を変えるだけで貢献度の符号が反転していた実体がこれ）
        //
        // ⭐ ここは4つとも直してある。⚠️ 既存の記録を動かさないため**別の指定**にした。

        /// <summary>役割ごとの推奨ステ（2026-08-26・作者の指示）。
        /// ⭐ 先に書いたものほど優先度が高い。⚠️ どの役も全ステに下限を残す
        /// （2ステを0にすると「役が弱い」ではなく「HPが無いから死ぬ」を測ってしまう）。
        /// ⚠️ 合計は <see cref="Stats.WildTotalMax"/>(120)、1ステ上限は 40。</summary>
        /// <param name="sharp">🔴 **尖らせるか**（2026-08-26・作者の指摘）。
        ///
        /// ⚠️ `false`（丸い）は全ステに下限 10〜12 を残す ── 既存 probe の流儀
        /// （「2ステを0にすると『役が弱い』ではなく『HPが無いから死ぬ』を測る」）。
        /// 🔴 **だがそれでは「限られた枠を配る犠牲」が一切発生しない。**
        ///    アタッカーが防御も耐性も12持っている編成では、弱化が刺さる隙が無い。
        ///
        /// ⭐ `true`（尖った）は**優先ステを 40 で埋め、余りは HP へ、残りは 0**。
        ///    ＝ アタッカーは**防御0・弱化耐性0**。これが作者の言う
        ///    「おろそかにせざるを得ない状況」の実物。</param>
        private static Role[] DebuffRoles(bool sharp)
        {
            //                        丸い: hp atk def spd acc res  / 尖った: 同順
            var attacker = new Role("アタッカー",   // 攻撃 > 速度
                sharp ? new StatBlock(40, 40, 0, 40, 0, 0)
                      : new StatBlock(12, 40, 12, 32, 12, 12), "attack-heavy", "attack-twice");
            var tank = new Role("タンク",           // HP > 防御 > 弱化耐性
                sharp ? new StatBlock(40, 0, 40, 0, 0, 40)
                      : new StatBlock(36, 10, 30, 10, 10, 24), "bulwark", "harden");
            var support = new Role("サポート",       // 速度 > 弱化耐性
                sharp ? new StatBlock(40, 0, 0, 40, 0, 40)
                      : new StatBlock(12, 12, 12, 40, 12, 32), "atk-up", "spd-up");
            var healer = new Role("ヒーラー",        // 防御 > 速度 > HP
                sharp ? new StatBlock(40, 0, 40, 40, 0, 0)
                      : new StatBlock(24, 12, 32, 28, 12, 12), "heal-ratio", "regen");
            // ⭐ スタンと毒の両方を持たせる（作者の指示）。⚠️ **どちらも命中が効く札**
            //    ── `stun`(100%) や `poison`(100%) は `LandChanceOf` が素通しするので、
            //    命中に振った価値がそもそも出ない。`stun-heavy`(40%)・`venom-heavy`(65%)。
            var debuffer = new Role("デバッファー",   // 弱化命中 > 速度
                sharp ? new StatBlock(40, 0, 0, 40, 40, 0)
                      : new StatBlock(12, 12, 12, 32, 40, 12), "stun-heavy", "venom-heavy");
            return new[] { attacker, tank, support, healer, debuffer };
        }

        /// <summary>指定した席だけを N レベルぶん伸ばす。
        /// ⚠️ <see cref="Leveled"/> は**編成全員**に乗せる ── 弱化技を持たない
        /// アタッカーや壁役の命中まで上がって、投資の 3/4 が死んでいた。</summary>
        private static List<Creature> LeveledAt(List<Creature> party, StatKey key, int levels,
            Func<int, bool> who)
        {
            var made = new List<Creature>();
            for (int i = 0; i < party.Count; i++)
            {
                var c = party[i];
                if (!who(i)) { made.Add(c); continue; }
                int grown = Creatures.TrainedFor(c.SpeciesId, c.Wild, levels)[key];
                made.Add(Rebuilt(c, c.Trained.With(key, c.Trained[key] + grown)));
            }
            return made;
        }

        /// <summary>⚠️ 直前の <see cref="CompWinRate"/> の平均手数。
        /// ⭐ **「試合が短すぎて支援役の出番が無い」を見抜くための欄**（2026-08-26）。
        /// 弱化は3ターン・毒は4ターンで効くので、試合がそれより短ければ
        /// 「弱化が弱い」ではなく「弱化が働く前に終わっている」ことになる。
        /// ⚠️ probe は単スレッドなので、この持ち方で足りる。</summary>
        private static double _lastActions;

        /// <summary>編成どうしの勝率。⭐ **`land` を必ず渡す**（弱化の当たり外れを毎回引き直す）。</summary>
        private static double CompWinRate(int seed, Role[] mine, Role[] theirs, int samples,
            StatKey? key = null, int levels = 0, Func<int, bool>? who = null)
        {
            int won = 0;
            long actions = 0;
            for (int i = 0; i < samples; i++)
            {
                var rng = new Rng(seed + i).Stream("debuffprobe");
                var land = new Rng(seed + i).Stream("land-debuffprobe");
                int serial = 0;
                var a = Shaped(rng, mine, ref serial);
                if (key != null) a = LeveledAt(a, key.Value, levels, who ?? (_ => true));
                var b = Shaped(rng, theirs, ref serial);
                var fight = Run(a, b, land);
                actions += fight.Actions;
                if (fight.Result == Outcome.Ally) won++;
            }
            _lastActions = samples == 0 ? 0.0 : (double)actions / samples;
            return samples == 0 ? 0.0 : 100.0 * won / samples;
        }

        private static void DebuffProbe(int seed, int levelsOverride)
        {
            // ⚠️ `roles` の 120回では種を変えるだけで符号が反転した（2026-08-26 実測）。
            const int Samples = 400;
            int Levels = levelsOverride > 0 ? levelsOverride : 20;

            // ⭐ 丸い/尖った を並べて、**枠の奪い合いが起きる編成でも同じ結論か**を見る
            //    （2026-08-26・作者の指摘「限られた枠で配るので、アタッカーは
            //    弱化耐性や防御力をおろそかにせざるを得ない」）。
            foreach (bool sharp in new[] { false, true })
            {
                var rr = DebuffRoles(sharp);
                Role a2 = rr[0], t2 = rr[1], s2 = rr[2], h2 = rr[3], d2 = rr[4];
                var foe = new[] { a2, t2, h2, d2 };
                // 🔴 **硬い敵**: 尖ったタンク2枚。⚠️ 攻めを continue するだけでは落ちない相手
                //    （作者の仮説「何の対策もなく挑めば倒せない耐久役が居れば話が変わる」）。
                var foeWall = new[] { t2, t2, h2, a2 };
                // 🔴 **不落**: 重装（HP/防御/攻撃を40）2枚＋癒2枚。
                //    ⚠️ 上の `foeWall` は尖ったタンクの ATK が 0 なので「硬いだけで脅威が無い」
                //    ── 攻撃4枚が無リスクで削り切れてしまい、作者の仮説を検証できていなかった
                //    （2026-08-26 の自己反省）。⭐ **耐久・火力・回復の3つが揃った相手**で測る。
                var heavy = new Role("重装", new StatBlock(40, 40, 40, 0, 0, 0),
                    "attack-heavy", "harden");
                var foeSustain = new[] { heavy, heavy, h2, h2 };

                Console.WriteLine();
                Console.WriteLine($"■ 編成の比較（4対4・各{Samples}回・{(sharp ? "🔴 尖った編成（優先ステ40・他0）" : "丸い編成（全ステに下限）")}）");

                var comps = new (string Name, Role[] Party)[]
                {
                    ("攻 壁 癒 弱（標準）", new[] { a2, t2, h2, d2 }),
                    ("攻 攻 壁 癒（弱化なし）", new[] { a2, a2, t2, h2 }),
                    ("攻 攻 攻 攻（攻撃4枚）", new[] { a2, a2, a2, a2 }),
                    ("攻 弱 弱 癒（弱化2枚）", new[] { a2, d2, d2, h2 }),
                    ("攻 支 壁 弱（支援入り）", new[] { a2, s2, t2, d2 }),
                };

                Console.WriteLine($"  ── 相手: 標準（攻/壁/癒/弱）──");
                double std = 0;
                for (int i = 0; i < comps.Length; i++)
                {
                    double pct = CompWinRate(seed, comps[i].Party, foe, Samples);
                    double act = _lastActions;
                    if (i == 0) std = pct;
                    Console.WriteLine($"    {comps[i].Name,-24} {pct,5:0.0}%"
                        + (i == 0 ? "   （基準）" : $"   基準から {pct - std,5:0.0}pt")
                        + $"   手数 {act,5:0.0}");
                }

                // 🔴 ここが作者の仮説の本体
                Console.WriteLine($"  ── 相手: 🔴 硬い（壁/壁/癒/攻）── 攻めるだけでは落ちない相手 ──");
                double stdWall = 0;
                for (int i = 0; i < comps.Length; i++)
                {
                    double pct = CompWinRate(seed, comps[i].Party, foeWall, Samples);
                    double act = _lastActions;
                    if (i == 0) stdWall = pct;
                    Console.WriteLine($"    {comps[i].Name,-24} {pct,5:0.0}%"
                        + (i == 0 ? "   （基準）" : $"   基準から {pct - stdWall,5:0.0}pt")
                        + $"   手数 {act,5:0.0}");
                }

                Console.WriteLine($"  ── 相手: 🔴 不落（重装/重装/癒/癒）── 耐久＋火力＋回復 ──");
                double stdSus = 0;
                for (int i = 0; i < comps.Length; i++)
                {
                    double pct = CompWinRate(seed, comps[i].Party, foeSustain, Samples);
                    double act = _lastActions;
                    if (i == 0) stdSus = pct;
                    Console.WriteLine($"    {comps[i].Name,-24} {pct,5:0.0}%"
                        + (i == 0 ? "   （基準）" : $"   基準から {pct - stdSus,5:0.0}pt")
                        + $"   手数 {act,5:0.0}");
                }
            }
            Console.WriteLine();
            Console.WriteLine("  ⚠️ 「弱化なし」「攻撃4枚」が基準を上回り続けるなら、弱化役は席を取る価値が無い");
            Console.WriteLine("  ⭐ 硬い相手で符号が反転するなら、弱化は**対耐久の札**として既に成立している");

            // ── 弱化命中への投資 ──────────────────────────
            var r = DebuffRoles(false);
            Role attacker = r[0], tank = r[1], support = r[2], healer = r[3], debuffer = r[4];
            var foeStd = new[] { attacker, tank, healer, debuffer };
            var mine = new[] { attacker, tank, healer, debuffer };
            double baseline = CompWinRate(seed, mine, foeStd, Samples);

            Console.WriteLine();
            Console.WriteLine($"■ 弱化命中への投資（{Levels}レベルぶん・**デバッファー1体だけ**）");
            Console.WriteLine($"  振らない（基準）                  {baseline,5:0.0}%");
            double accOnly = CompWinRate(seed, mine, foeStd, Samples,
                StatKey.Acc, Levels, i => i == 3);
            Console.WriteLine($"  デバッファーの命中 +{Levels}Lv        {accOnly,5:0.0}%"
                + $"   基準から {accOnly - baseline,5:0.0}pt");
            // ⚠️ 比較用: 同じ点を全員に配った場合（＝いままでの測り方）
            double accAll = CompWinRate(seed, mine, foeStd, Samples,
                StatKey.Acc, Levels, _ => true);
            Console.WriteLine($"  （参考）全員の命中 +{Levels}Lv         {accAll,5:0.0}%"
                + $"   基準から {accAll - baseline,5:0.0}pt");
            // ⭐ 物差し: 同じ点をアタッカーの攻撃に入れたら
            double atkOnly = CompWinRate(seed, mine, foeStd, Samples,
                StatKey.Atk, Levels, i => i == 0);
            Console.WriteLine($"  〈物差し〉アタッカーの攻撃 +{Levels}Lv {atkOnly,5:0.0}%"
                + $"   基準から {atkOnly - baseline,5:0.0}pt");

            // ── 弱化耐性への投資 ──────────────────────────
            // ⭐ 相手を**弱化2枚**にして、耐性が働く場面を作る。
            // ⚠️ 標準の相手（弱化1枚）だと、耐性に振っても受ける札が少なすぎて出ない。
            var foeHeavy = new[] { attacker, debuffer, debuffer, healer };
            double baseHeavy = CompWinRate(seed, mine, foeHeavy, Samples);

            Console.WriteLine();
            Console.WriteLine($"■ 弱化耐性への投資（{Levels}レベルぶん・相手は弱化2枚）");
            Console.WriteLine($"  振らない（基準）                  {baseHeavy,5:0.0}%");
            double resFront = CompWinRate(seed, mine, foeHeavy, Samples,
                StatKey.Res, Levels, i => i == 1 || i == 2);
            Console.WriteLine($"  壁とヒーラーの耐性 +{Levels}Lv       {resFront,5:0.0}%"
                + $"   基準から {resFront - baseHeavy,5:0.0}pt");
            double resAll = CompWinRate(seed, mine, foeHeavy, Samples,
                StatKey.Res, Levels, _ => true);
            Console.WriteLine($"  全員の耐性 +{Levels}Lv               {resAll,5:0.0}%"
                + $"   基準から {resAll - baseHeavy,5:0.0}pt");
            double hpAll = CompWinRate(seed, mine, foeHeavy, Samples,
                StatKey.Hp, Levels, i => i == 1);
            Console.WriteLine($"  〈物差し〉壁のHP +{Levels}Lv          {hpAll,5:0.0}%"
                + $"   基準から {hpAll - baseHeavy,5:0.0}pt");
            Console.WriteLine();
            Console.WriteLine("  ⚠️ 〈物差し〉と比べて桁が違うなら、その軸は振り先として成立していない");
        }

        // ── ダメージ式の比べ合わせ（2026-08-26・作者の指示）────────────
        //
        // 🔴 **仮説（作者）**: 「高耐久に対してダメージが通りやすい」。
        //    高耐久にただの火力押しが通らなければ、攻撃役だけの編成はジリ貧になり、
        //    弱化で軟化させる・支援で手数を増やす、といった対策が生まれるはず。
        //
        // ⭐ 見るのは1点: **「攻撃4枚」が不落（重装2＋癒2）に勝てなくなるか。**
        // ⚠️ 同時に「弱化2枚」が上回るかも見る ── 攻撃4枚を弱くしただけで
        //    どの編成も勝てなくなったのでは、選択肢が増えたことにならない。

        private sealed class DamageModel
        {
            public readonly string Name;
            public readonly string How;
            public readonly Func<int, int, int, double, int>? Fn;
            public DamageModel(string name, string how, Func<int, int, int, double, int>? fn)
            { Name = name; How = how; Fn = fn; }
        }

        /// <summary>試す式。⚠️ **威力・属性の扱いは全案で同じ**にしてある
        /// （変えると「式の違い」ではなく「威力の違い」を測ってしまう）。</summary>
        private static DamageModel[] DamageModels()
        {
            const int Unit = Skills.PowerUnit;
            int B = Battle.DamageBase;
            int S = Battle.DefSoften;

            int Clamp(double raw, double mult)
            {
                int v = (int)Math.Floor(raw * mult);
                return v < 1 ? 1 : v;
            }
            double Base(int power, int atk) => (double)atk * power / Unit * B;

            return new[]
            {
                // 🔴 **index 0 が「いまの本番」**（`Battle.DamageOf` そのもの・null で素通し）。
                //    ⚠️ 2026-08-26 に二乗飽和を本採用したので、ここの意味が入れ替わっている。
                new DamageModel("現行 二乗飽和", $"({S}/({S}+防))^2", null),
                // ⭐ 採用前の式。⚠️ **消さない** ── 「戻したらどうなるか」を測れなくなる。
                new DamageModel("旧 線形飽和", $"{S}/({S}+防)",
                    (p, a, d, m) => Clamp(Base(p, a) * (double)S / (S + d), m)),

                // ⭐ 飽和の効き始めを早める。⚠️ 低防御へのダメージも一律で下がる
                new DamageModel("軟化1/2", $"{S/2}/({S/2}+防)",
                    (p, a, d, m) => Clamp(Base(p, a) * (S / 2.0) / (S / 2.0 + d), m)),
                new DamageModel("軟化1/4", $"{S/4}/({S/4}+防)",
                    (p, a, d, m) => Clamp(Base(p, a) * (S / 4.0) / (S / 4.0 + d), m)),

                new DamageModel("三乗飽和", $"({S}/({S}+防))^3",
                    (p, a, d, m) => Clamp(Base(p, a) * Math.Pow((double)S / (S + d), 3), m)),

                // ⚠️ 減算式。⭐ 高耐久が**完全に無効化**しうる（下限で止める）。
                //    ⚠️ 下限が無いと 0 ダメージの睨み合いになる ── 素の1割を残す。
                new DamageModel("減算(下限1割)", "威力 − 防×8、下限は1割",
                    (p, a, d, m) =>
                    {
                        double raw = Base(p, a);
                        double cut = raw - d * 8.0;
                        return Clamp(Math.Max(cut, raw * 0.10), m);
                    }),
            };
        }

        private static void DamageModelProbe(int seed)
        {
            const int Samples = 300;
            var r = DebuffRoles(true);   // ⭐ 尖った編成で見る（枠の奪い合いが起きる側）
            Role a = r[0], t = r[1], s2 = r[2], h = r[3], d = r[4];
            var heavy = new Role("重装", new StatBlock(40, 40, 40, 0, 0, 0),
                "attack-heavy", "harden");
            var foeSustain = new[] { heavy, heavy, h, h };

            var comps = new (string Name, Role[] Party)[]
            {
                ("攻撃4枚", new[] { a, a, a, a }),
                ("弱化なし", new[] { a, a, t, h }),
                ("標準", new[] { a, t, h, d }),
                ("弱化2枚", new[] { a, d, d, h }),
                ("支援入り", new[] { a, s2, t, d }),
            };

            // ⚠️ **2つの相手で見る。**⭐ 不落だけで良く見える式は、普通の相手を壊しているかもしれない
            //    （「高耐久に効く」ではなく「ただの全体弱体化」だと選択肢は増えない）。
            var foeStd = new[] { a, t, h, d };
            var arenas = new (string Name, Role[] Foe)[]
            {
                ("不落〈重装2＋癒2〉", foeSustain),
                ("標準〈攻/壁/癒/弱〉", foeStd),
            };

            foreach (var arena in arenas)
            {
                Console.WriteLine();
                Console.WriteLine($"■ ダメージ式の比べ合わせ（尖った編成・相手 {arena.Name}・各{Samples}回）");
                Console.Write($"  {"式",-16}{"効き方",-22}");
                foreach (var c in comps) Console.Write($"{c.Name,10}");
                Console.WriteLine("   手数(攻4)");

                foreach (var model in DamageModels())
                {
                    Battle.DamageOverride = model.Fn;
                    try
                    {
                        Console.Write($"  {model.Name,-16}{model.How,-22}");
                        double firstActions = 0;
                        for (int i = 0; i < comps.Length; i++)
                        {
                            double pct = CompWinRate(seed, comps[i].Party, arena.Foe, Samples);
                            if (i == 0) firstActions = _lastActions;
                            Console.Write($"{pct,9:0.0}%");
                        }
                        Console.WriteLine($"   {firstActions,8:0.0}");
                    }
                    finally { Battle.DamageOverride = null; }   // ⚠️ 必ず戻す
                }
            }

            Console.WriteLine();
            Console.WriteLine("  ⚠️ 攻撃4枚だけが下がって他も全部下がるなら、ただの弱体化で選択肢は増えていない");
            Console.WriteLine("  ⭐ 攻撃4枚 < 弱化2枚 になる式が、作者の狙い（対策が生まれる）を満たす");
        }

        // ── ステ配分が「判断」になるか（2026-08-26・作者の指示）──────────
        //
        // 🔴 **ARK式の自由配分が成立する条件**は3つ:
        //    ① 役割ごとに最適な振り先が**違う**（同じ答えなら役割が要らない）
        //    ② 1位と2位の差が**小さい**（圧倒的なら判断ではなく作業）
        //    ③ **罠のステが無い**（振ると損なステがあると選択肢が実質減る）
        // ⚠️ 現行式(A)では弱化命中が攻撃の 1/10 ＝ ③に反していた（2026-08-26 実測）。
        // ⭐ ここは「式を変えると③が直るか」を見る。

        private static void AllocateProbe(int seed)
        {
            const int Samples = 250;
            const int Levels = 20;

            var r = DebuffRoles(true);   // ⭐ 尖った編成（枠の奪い合いが起きる側）
            Role a = r[0], t = r[1], h = r[3], d = r[4];
            var heavy = new Role("重装", new StatBlock(40, 40, 40, 0, 0, 0),
                "attack-heavy", "harden");

            var mine = new[] { a, t, h, d };
            var arenas = new (string Name, Role[] Foe)[]
            {
                ("標準〈攻/壁/癒/弱〉", new[] { a, t, h, d }),
                ("不落〈重装2＋癒2〉", new[] { heavy, heavy, h, h }),
            };
            // ⚠️ 席の番号は `mine` の並び順
            var seats = new (string Name, int Index)[]
            {
                ("アタッカー", 0), ("タンク", 1), ("ヒーラー", 2), ("デバッファー", 3),
            };

            var models = DamageModels();
            foreach (var model in new[] { models[1], models[0] })   // 旧 線形飽和 と 現行 二乗飽和
            {
                foreach (var arena in arenas)
                {
                    Battle.DamageOverride = model.Fn;
                    try
                    {
                        double baseline = CompWinRate(seed, mine, arena.Foe, Samples);
                        Console.WriteLine();
                        Console.WriteLine($"■ どのステに {Levels}Lv 振ると何pt効くか"
                            + $"（{model.Name}・相手 {arena.Name}・各{Samples}回・基準 {baseline:0.0}%）");
                        Console.Write($"  {"振る席",-14}");
                        foreach (var key in Stats.Keys) Console.Write($"{Stats.LabelOf(key),9}");
                        Console.WriteLine("     1位/2位");

                        foreach (var seat in seats)
                        {
                            Console.Write($"  {seat.Name,-14}");
                            double best = 0, second = 0;
                            foreach (var key in Stats.Keys)
                            {
                                double pct = CompWinRate(seed, mine, arena.Foe, Samples,
                                    key, Levels, i => i == seat.Index);
                                double gain = pct - baseline;
                                Console.Write($"{gain,8:+0.0;-0.0;0.0}");
                                if (gain > best) { second = best; best = gain; }
                                else if (gain > second) second = gain;
                            }
                            // ⭐ 1位が2位の何倍か。⚠️ 大きいほど「判断」ではなく「作業」
                            string ratio = second > 0.05 ? $"{best / second,7:0.0}倍" : "    ―";
                            Console.WriteLine($"  {ratio}");
                        }
                    }
                    finally { Battle.DamageOverride = null; }
                }
            }

            Console.WriteLine();
            Console.WriteLine("  ⭐ ①役割ごとに1位が違う ②1位/2位が小さい ③負の値(罠)が無い ── 3つ揃えば配分は判断になる");
        }

        // ── 弱化命中 × 弱化耐性 の噛み合い（2026-08-26・作者の指示）────────
        //
        // 🔴 **この2本は互いに干渉するので、片側だけ動かしても価値が出ない。**
        //    ⚠️ 2026-08-26 の私の測定は両方ともこれを外していた:
        //    ・命中の価値 → 相手の耐性が 0 の編成で測った（通って当然）
        //    ・耐性の価値 → 相手にデバッファーが居ない編成で測った（受けないので 0 で当然）
        // ⭐ ここは**両側を同時に動かした表**で見る。
        //    さらに**味方の手番数**も出す ── 「スタンや速度DOWNで動けない」は
        //    勝率より先に**手番の数**に出るはずなので（作者の読み）。

        /// <summary>直前の <see cref="DuelWinRate"/> の、味方側が取れた手番の平均。
        /// ⭐ 「動けているか」を勝敗と切り離して見るための欄。</summary>
        private static double _lastAllyActions;

        /// <summary>両側に別々の育成を乗せて回す。⭐ `land` は必ず渡す。</summary>
        private static double DuelWinRate(int seed, Role[] mine, Role[] theirs, int samples,
            StatKey? myKey, int myLv, Func<int, bool>? myWho,
            StatKey? foeKey, int foeLv, Func<int, bool>? foeWho)
        {
            int won = 0; long allyActs = 0;
            for (int i = 0; i < samples; i++)
            {
                var rng = new Rng(seed + i).Stream("resistprobe");
                var land = new Rng(seed + i).Stream("land-resistprobe");
                int serial = 0;
                var a = Shaped(rng, mine, ref serial);
                if (myKey != null && myLv > 0)
                    a = LeveledAt(a, myKey.Value, myLv, myWho ?? (_ => true));
                var b = Shaped(rng, theirs, ref serial);
                if (foeKey != null && foeLv > 0)
                    b = LeveledAt(b, foeKey.Value, foeLv, foeWho ?? (_ => true));
                var fight = Run(a, b, land);
                allyActs += fight.AllyActions;
                if (fight.Result == Outcome.Ally) won++;
            }
            _lastAllyActions = samples == 0 ? 0.0 : (double)allyActs / samples;
            return samples == 0 ? 0.0 : 100.0 * won / samples;
        }

        private static void ResistProbe(int seed)
        {
            const int Samples = 250;
            var steps = new[] { 0, 20, 40 };   // 育成レベルぶん

            var r = DebuffRoles(true);
            Role a = r[0], t = r[1], sup = r[2], h = r[3], d = r[4];
            // 🔴 **足止め型**（作者の指摘「スタンで動けない・速度DOWNで動けない」）。
            //    `curse` は攻撃力とスピードを同時に下げる 70% の札。
            var jam = new Role("妨害役", new StatBlock(40, 0, 0, 40, 40, 0),
                "stun-heavy", "curse");

            // ⭐ 2026-08-26 に二乗飽和が本番になったので、差し替えは要らない
            try
            {
                // ── ① 守る側: 味方の耐性 × 敵デバッファーの命中 ──────────
                var mine = new[] { a, t, h, sup };            // 攻/壁/癒/支 ＝ 受ける側だけ
                var foeJam = new[] { a, jam, jam, h };        // 相手は足止め2枚
                Console.WriteLine();
                Console.WriteLine($"■ ① 守る側（味方 攻/壁/癒/支 vs 相手 攻/妨/妨/癒・D式・各{Samples}回）");
                Console.WriteLine("  ⭐ 縦＝味方の弱化耐性を育てた量／横＝相手デバッファーの弱化命中を育てた量");
                Console.WriteLine("  ⚠️ 括弧内は**味方が取れた手番の数**（動けているか）");
                Console.Write($"  {"味方の耐性",-12}");
                foreach (int f in steps) Console.Write($"{"敵命中+" + f,18}");
                Console.WriteLine();
                foreach (int my in steps)
                {
                    Console.Write($"  {"+" + my + "Lv",-12}");
                    foreach (int foe in steps)
                    {
                        double pct = DuelWinRate(seed, mine, foeJam, Samples,
                            StatKey.Res, my, null,            // ⭐ 味方は全員が耐性を持つ
                            StatKey.Acc, foe, i => i == 1 || i == 2);
                        Console.Write($"{pct,10:0.0}%({_lastAllyActions,4:0})");
                    }
                    Console.WriteLine();
                }

                // ── ② 攻める側: 味方デバッファーの命中 × 敵の耐性 ──────────
                var mineJam = new[] { a, jam, jam, h };
                var foeStd = new[] { a, t, h, sup };
                Console.WriteLine();
                Console.WriteLine($"■ ② 攻める側（味方 攻/妨/妨/癒 vs 相手 攻/壁/癒/支・D式・各{Samples}回）");
                Console.WriteLine("  ⭐ 縦＝味方デバッファーの弱化命中／横＝相手の弱化耐性");
                Console.Write($"  {"味方の命中",-12}");
                foreach (int f in steps) Console.Write($"{"敵耐性+" + f,18}");
                Console.WriteLine();
                foreach (int my in steps)
                {
                    Console.Write($"  {"+" + my + "Lv",-12}");
                    foreach (int foe in steps)
                    {
                        double pct = DuelWinRate(seed, mineJam, foeStd, Samples,
                            StatKey.Acc, my, i => i == 1 || i == 2,
                            StatKey.Res, foe, null);
                        Console.Write($"{pct,10:0.0}%({_lastAllyActions,4:0})");
                    }
                    Console.WriteLine();
                }

                // ── ③ 噛み合う場面で、命中は他のステに勝てるか ──────────
                // 🔴 ここが最後の判定。⚠️ ①② は「命中を振るか振らないか」しか比べていない。
                //    ⭐ **同じ20Lvを速度や防御に入れた場合**と並べて初めて、
                //    「弱化命中は振り先として選ばれうるか」が言える。
                Console.WriteLine();
                Console.WriteLine($"■ ③ デバッファー席の振り先くらべ（相手は**耐性+40**・D式・各{Samples}回）");
                double b3 = DuelWinRate(seed, mineJam, foeStd, Samples,
                    null, 0, null, StatKey.Res, 40, null);
                Console.WriteLine($"  振らない（基準）  {b3,5:0.0}%");
                foreach (var key in Stats.Keys)
                {
                    double pct = DuelWinRate(seed, mineJam, foeStd, Samples,
                        key, 20, i => i == 1 || i == 2,     // ⭐ デバッファー2体だけ
                        StatKey.Res, 40, null);
                    Console.WriteLine($"  {Stats.LabelOf(key),-8} +20Lv  {pct,5:0.0}%"
                        + $"   基準から {pct - b3,5:0.0}pt   手番 {_lastAllyActions,4:0}");
                }

                // ── ④ 受ける側で、耐性は他のステに勝てるか ──────────
                Console.WriteLine();
                Console.WriteLine($"■ ④ 受ける側の振り先くらべ（相手は**命中+40**の妨害2枚・D式・各{Samples}回）");
                double b4 = DuelWinRate(seed, mine, foeJam, Samples,
                    null, 0, null, StatKey.Acc, 40, i => i == 1 || i == 2);
                Console.WriteLine($"  振らない（基準）  {b4,5:0.0}%");
                foreach (var key in Stats.Keys)
                {
                    double pct = DuelWinRate(seed, mine, foeJam, Samples,
                        key, 20, null,                       // ⭐ 味方4体すべてに振る
                        StatKey.Acc, 40, i => i == 1 || i == 2);
                    Console.WriteLine($"  {Stats.LabelOf(key),-8} +20Lv  {pct,5:0.0}%"
                        + $"   基準から {pct - b4,5:0.0}pt   手番 {_lastAllyActions,4:0}");
                }
            }
            finally { Battle.DamageOverride = null; }

            Console.WriteLine();
            Console.WriteLine("  ⭐ 表の中で**縦に動く**なら、その投資は効いている");
            Console.WriteLine("  ⭐ **右へ行くほど縦の効きが強まる**なら、2本は噛み合っている（片方が上がると片方が要る）");
        }

        // ── 通る率の帯と感度（2026-08-26・作者の指摘）──────────────────
        //
        // 🔴 **作者の読み**: 「耐性をいくら上げても 1/4 で当たるなら上げる価値は低い。
        //    命中もいくら上げても 5% で外れるなら価値が下がる」。
        //    ⭐ 投資の天井を決めているのは <see cref="Battle.LandFloor"/>(25) と
        //    <see cref="Battle.LandCeil"/>(95)、そして感度 <see cref="Battle.LandStatDivisor"/>(10)。
        // ⚠️ 帯を広げると**特性（狙い澄まし/意地 ±20）の効きも変わる**ので、
        //    採用するならそちらも測り直しが要る。

        private static void LandBandProbe(int seed)
        {
            const int Samples = 250;
            int keepDiv = Battle.LandStatDivisor, keepLo = Battle.LandFloor, keepHi = Battle.LandCeil;

            var r = DebuffRoles(true);
            Role a = r[0], t = r[1], sup = r[2], h = r[3];
            var jam = new Role("妨害役", new StatBlock(40, 0, 0, 40, 40, 0),
                "stun-heavy", "curse");
            var mineJam = new[] { a, jam, jam, h };     // こちらが弱化を撃つ側
            var mineTake = new[] { a, t, h, sup };      // こちらが弱化を受ける側
            var foeStd = new[] { a, t, h, sup };
            var foeJam = new[] { a, jam, jam, h };

            // (名前, 下限, 上限, 感度の割る数)
            var configs = new (string Name, int Lo, int Hi, int Div)[]
            {
                ("現行 [25,95] ÷10", 25, 95, 2 * Stats.Scale),
                ("帯広 [10,99] ÷10", 10, 99, 2 * Stats.Scale),
                ("帯最大 [0,100] ÷10", 0, 100, 2 * Stats.Scale),
                ("感度2倍 [25,95] ÷5", 25, 95, Stats.Scale),
                ("帯最大+感度2倍 ÷5", 0, 100, Stats.Scale),
            };

            // ⭐ 2026-08-26 に二乗飽和が本番になったので、差し替えは要らない
            try
            {
                Console.WriteLine();
                Console.WriteLine($"■ 通る率の帯・感度を振ったときの「弱化に振る価値」（D式・各{Samples}回）");
                Console.WriteLine("  ⭐ ③＝デバッファー席（相手 耐性+40）／④＝受ける側（相手 命中+40の妨害2枚）");
                Console.WriteLine("  ⚠️ 弱化の数字が**速度に並べば**、6本での配分が成立する");
                Console.WriteLine();
                Console.WriteLine($"  {"設定",-22}{"③命中",8}{"③速度",8}{"③防御",8}   |{"④耐性",8}{"④速度",8}{"④防御",8}");

                foreach (var cfg in configs)
                {
                    Battle.LandFloor = cfg.Lo; Battle.LandCeil = cfg.Hi;
                    Battle.LandStatDivisor = cfg.Div;

                    double b3 = DuelWinRate(seed, mineJam, foeStd, Samples,
                        null, 0, null, StatKey.Res, 40, null);
                    double Gain3(StatKey k) => DuelWinRate(seed, mineJam, foeStd, Samples,
                        k, 20, i => i == 1 || i == 2, StatKey.Res, 40, null) - b3;

                    double b4 = DuelWinRate(seed, mineTake, foeJam, Samples,
                        null, 0, null, StatKey.Acc, 40, i => i == 1 || i == 2);
                    double Gain4(StatKey k) => DuelWinRate(seed, mineTake, foeJam, Samples,
                        k, 20, null, StatKey.Acc, 40, i => i == 1 || i == 2) - b4;

                    Console.WriteLine($"  {cfg.Name,-22}"
                        + $"{Gain3(StatKey.Acc),7:+0.0;-0.0;0.0}{Gain3(StatKey.Spd),8:+0.0;-0.0;0.0}"
                        + $"{Gain3(StatKey.Def),8:+0.0;-0.0;0.0}   |"
                        + $"{Gain4(StatKey.Res),7:+0.0;-0.0;0.0}{Gain4(StatKey.Spd),8:+0.0;-0.0;0.0}"
                        + $"{Gain4(StatKey.Def),8:+0.0;-0.0;0.0}");
                }
            }
            finally
            {
                Battle.DamageOverride = null;
                Battle.LandStatDivisor = keepDiv;      // ⚠️ 必ず戻す
                Battle.LandFloor = keepLo; Battle.LandCeil = keepHi;
            }
            Console.WriteLine();
            Console.WriteLine("  ⭐ 帯を広げても弱化が速度に届かないなら、原因は帯ではなく「確率を買っていること」そのもの");
        }

        // ── 通る率を実数で出す（2026-08-26・作者の問い）────────────────
        // ⭐ 「式がどうなっているか」を**実際に出る数**で示す。
        //    ⚠️ 割る数の議論は単位が分からないと噛み合わない
        //    （命中・抵抗は**実値**＝種族基礎＋野生レベル×Stats.Scale＋育成ぶん）。
        private static void LandCalcProbe(int seed)
        {
            var rng = new Rng(seed).Stream("landcalc");
            int serial = 0;
            var ids = new List<string>();
            foreach (var sp in SpeciesTable.All) ids.Add(sp.Id);

            // ⭐ 同じ種族で、野生レベルだけ変えた3体を作って実値を見る
            string speciesId = ids[0];
            (string Name, StatBlock Wild)[] shapes =
            {
                ("命中40/耐性0", new StatBlock(40, 0, 0, 40, 40, 0)),
                ("命中0/耐性0 ", new StatBlock(40, 0, 40, 40, 0, 0)),
                ("命中0/耐性40", new StatBlock(40, 0, 40, 0, 0, 40)),
            };

            Console.WriteLine();
            Console.WriteLine("■ 弱化命中・弱化耐性の**実値**（種族基礎 ＋ 野生レベル×Stats.DebuffScale）");
            Console.WriteLine($"  ⚠️ 野生レベルは 0〜{Stats.WildStatMax}、DebuffScale = {Stats.DebuffScale}"
                + $"、育成でさらに +{Creatures.GrowthFlatOf(StatKey.Acc)}/Lv");
            var made = new List<(string Name, StatBlock S)>();
            foreach (var sh in shapes)
            {
                var born = Born(rng, speciesId, 5, ref serial);
                var c = new Creature(born.Id, speciesId, sh.Wild, new StatBlock(0, 0, 0, 0), 0,
                    born.MutationCounter, null, null, born.PaletteIndex,
                    null, null, 1, null, null, born.Element, born.TraitId);
                var st = Creatures.StatsOf(c);
                made.Add((sh.Name, st));
                Console.WriteLine($"  {sh.Name}   命中 {st.Acc,5}   耐性 {st.Res,5}");
            }

            Console.WriteLine();
            Console.WriteLine($"  式: 技の基礎率 ＋ (命中 − 耐性) ÷ {Battle.LandStatDivisor}"
                + $" ＋ 属性±{Battle.LandElementSwing} ＋ 特性±{Battle.TraitAim}"
                + $"  → [{Battle.LandFloor}, {Battle.LandCeil}]");

            // ⭐ 技の基礎率は**表から読む**（写すと第2の出所になる）
            string[] watch = { "stun", "poison", "stun-heavy", "venom-heavy", "curse", "def-down" };
            Console.WriteLine();
            Console.WriteLine("■ 実際の通る率");
            Console.WriteLine($"  {"技",-14}{"基礎率",7}{"命中98→耐性22",15}{"命中98→耐性102",16}{"命中18→耐性102",16}");
            foreach (var id in watch)
            {
                var sk = Skills.ById(id);
                int baseChance = 100;
                foreach (var e in sk.Effects) if (Skills.IsHarmful(e)) { baseChance = e.Chance; break; }
                int Rate(StatBlock at, StatBlock df) => Math.Clamp(
                    baseChance + (at.Acc - df.Res) / Battle.LandStatDivisor,
                    Battle.LandFloor, Battle.LandCeil);
                Console.WriteLine($"  {sk.Name,-14}{baseChance,6}%"
                    + $"{Rate(made[0].S, made[1].S),14}%{Rate(made[0].S, made[2].S),15}%"
                    + $"{Rate(made[1].S, made[2].S),15}%");
            }
            Console.WriteLine();
            Console.WriteLine("  ⭐ 一番右が 0% なら「振っていない相手の弱化を、耐性で弾き切れる」");
        }

        // ── 弱化技の格を上げると席が取れるか（2026-08-26・作者の指示）──────
        //
        // 🔴 **作者の診断**: 「現状の技はまもダンの R キャラのものばかりで控えめ。
        //    戦闘への介入度が低い。UR のスキルを持ってきて試すのがいい」。
        // ⭐ `参考/まもダン_全キャラスキル.md` の UR デバッファーを読むと、設計が違う:
        //    ① ダメージと弱化を同時に載せる ② 多段で毎回判定 ③ 1発で複数の弱化
        //    ④ ゲージ操作で手番そのものを奪う
        // ⚠️ **名前も数値も持ってこない**（この作品の決めごと）。⭐ 借りるのは設計だけ。
        // ⭐ 効果の種類（Gauge/Sleep/Block/Dispel/Steal）は**既に全部ある** ──
        //    足りていないのは「それを使う技」だけなので、まず**既存技の組み替え**で測る。
        private static void UrSkillProbe(int seed)
        {
            const int Samples = 300;
            var r = DebuffRoles(true);
            Role a = r[0], t = r[1], h = r[3];
            var heavy = new Role("重装", new StatBlock(40, 40, 40, 0, 0, 0),
                "attack-heavy", "harden");
            var foeSustain = new[] { heavy, heavy, h, h };
            var foeStd = new[] { a, t, h, r[4] };

            // ⭐ 弱化役のステは固定。**技だけ**を替える（測っているのが技だと言い切れるように）
            // 🔴 **毒を外した案は全滅した**（2026-08-26 実測 47.7% → 2.0〜5.3%）。
            //    ⭐ 毒は「最大HPの割合・防御無視」なので、**硬い相手ほど効く**唯一の札だった。
            //    ⚠️ つまり前の実験は「技の格」ではなく「毒の有無」を測っていた。
            //    ここは**毒を固定**して、もう1枠だけを替える（測る対象を1つに絞る）。
            var loadouts = new (string Name, string S2, string S3)[]
            {
                // ── R級（既存）──────────────────────────
                ("R 毒 + スタン",       "venom-heavy",   "stun-heavy"),
                ("R 毒 ×2",           "venom-heavy",   "poison-all"),
                ("R 純粋弱化（毒なし）",  "stun-heavy",    "curse"),
                // ── UR級（2026-08-26 に新設）─────────────
                ("UR 乱打+毒",         "venom-barrage", "venom-heavy"),
                ("UR 崩落（全体+2弱化）", "collapse",     "venom-heavy"),
                ("UR 停滞（ゲージ+スタン）","stagnate",    "venom-heavy"),
                ("UR 乱打+崩落",        "venom-barrage", "collapse"),
                ("UR 3種盛り(乱打+停滞)", "venom-barrage", "stagnate"),
            };

            // ⭐ 2026-08-26 に二乗飽和が本番になったので、差し替えは要らない
            try
            {
                foreach (var (arenaName, foe) in new[]
                    { ("不落〈重装2＋癒2〉", foeSustain), ("標準〈攻/壁/癒/弱〉", foeStd) })
                {
                    // ⭐ 物差し ── 弱化を1枚も入れない編成
                    double rush = CompWinRate(seed, new[] { a, a, a, a }, foe, Samples);
                    Console.WriteLine();
                    Console.WriteLine($"■ 弱化技の格くらべ（味方 攻/弱/弱/癒・相手 {arenaName}・D式・各{Samples}回）");
                    Console.WriteLine($"  〈物差し〉攻撃4枚（弱化なし）   {rush,5:0.0}%");
                    Console.WriteLine();
                    foreach (var lo in loadouts)
                    {
                        var jam = new Role("弱化役", new StatBlock(40, 0, 0, 40, 40, 0), lo.S2, lo.S3);
                        double pct = CompWinRate(seed, new[] { a, jam, jam, h }, foe, Samples);
                        string verdict = pct > rush ? "  ⭐ 席を取れる" : "";
                        Console.WriteLine($"  {lo.Name,-22}{pct,5:0.0}%"
                            + $"   物差しから {pct - rush,5:0.0}pt   手数 {_lastActions,5:0}{verdict}");
                    }
                }
            }
            finally { Battle.DamageOverride = null; }
            Console.WriteLine();
            Console.WriteLine("  ⭐ 物差しを上回る技があれば、原因は「弱化の仕組み」ではなく**技の格**だったことになる");
        }

        // ── 命中と速度の組み合わせ（2026-08-26・作者の指摘）──────────────
        //
        // 🔴 **作者の指摘**: 「弱化命中は速度と組み合わせることでデバフを素早く撒ける
        //    ので、一概に『速度に振ったほうがいい』とは言えないのでは」。
        // ⚠️ **これまでの計測は二者択一しか見ていない**（Acc に20点 vs Spd に20点）。
        //    ⭐ 実際の配分は「同じ予算をどう割るか」なので、**混ぜた場合**を測らないと
        //    相乗効果（速く撒く × 通る）を見落とす。
        // ⭐ ここは予算を固定して、Acc:Spd の割り振りだけを動かす。

        /// <summary>席を選んで複数のステを同時に伸ばす。⭐ `land` は必ず渡す。</summary>
        private static double MixWinRate(int seed, Role[] mine, Role[] theirs, int samples,
            (StatKey Key, int Lv)[] mix, Func<int, bool> who,
            StatKey? foeKey, int foeLv, Func<int, bool>? foeWho)
        {
            int won = 0; long acts = 0;
            for (int i = 0; i < samples; i++)
            {
                var rng = new Rng(seed + i).Stream("mixprobe");
                var land = new Rng(seed + i).Stream("land-mixprobe");
                int serial = 0;
                var a = Shaped(rng, mine, ref serial);
                foreach (var (key, lv) in mix)
                    if (lv > 0) a = LeveledAt(a, key, lv, who);
                var b = Shaped(rng, theirs, ref serial);
                if (foeKey != null && foeLv > 0)
                    b = LeveledAt(b, foeKey.Value, foeLv, foeWho ?? (_ => true));
                var fight = Run(a, b, land);
                acts += fight.AllyActions;
                if (fight.Result == Outcome.Ally) won++;
            }
            _lastAllyActions = samples == 0 ? 0.0 : (double)acts / samples;
            return samples == 0 ? 0.0 : 100.0 * won / samples;
        }

        private static void MixProbe(int seed)
        {
            const int Samples = 300;
            const int Budget = 40;   // ⭐ デバッファー1体に配る予算（点）

            var r = DebuffRoles(true);
            Role a = r[0], t = r[1], sup = r[2], h = r[3];
            Func<int, bool> jamSeats = i => i == 1 || i == 2;

            // 🔴 **技の格を変えて2通り測る**（2026-08-26）。
            //    ⚠️ R級の弱化は効き目が小さいので、「通す価値」自体が小さい。
            //    ⭐ UR級（毒2重＋崩落）なら1発の重みが違うので、命中の価値も変わるはず。
            var kits = new (string Name, string S2, string S3)[]
            {
                ("R級 スタン+足止め", "stun-heavy", "curse"),
                ("UR級 乱打+崩落",   "venom-barrage", "collapse"),
            };
            // ⚠️ **天井に張り付くと比べられない。**⭐ UR級は普通の相手だと 96〜100% で
            //    差が潰れたので（2026-08-26 実測）、硬い相手（重装2＋癒2）を足してある。
            var heavy = new Role("重装", new StatBlock(40, 40, 40, 0, 0, 0),
                "attack-heavy", "harden");
            var arenas = new (string Name, Role[] Foe, StatKey? Key, int Lv)[]
            {
                ("普通・耐性+40", new[] { a, t, h, sup }, StatKey.Res, 40),
                ("不落・耐性+40", new[] { heavy, heavy, h, h }, StatKey.Res, 40),
            };

            foreach (var (kitName, s2, s3) in kits)
            foreach (var arena in arenas)
            {
                var jam = new Role("妨害役", new StatBlock(40, 0, 0, 40, 40, 0), s2, s3);
                var mine = new[] { a, jam, jam, h };
                var foe = arena.Foe;
                Console.WriteLine();
                Console.WriteLine($"■ {kitName}／{arena.Name}／{Budget}点の割り振り（各{Samples}回）");
                Console.WriteLine("  ⭐ 予算は固定。⚠️ 混ぜた列が両端より高ければ、組み合わせに意味がある");
                Console.WriteLine($"  {"命中 : 速度",-14}{"勝率",8}{"味方の手番",12}");

                double best = -1; string bestAt = "";
                for (int acc = 0; acc <= Budget; acc += Budget / 4)
                {
                    int spd = Budget - acc;
                    double pct = MixWinRate(seed, mine, foe, Samples,
                        new[] { (StatKey.Acc, acc), (StatKey.Spd, spd) }, jamSeats,
                        arena.Key, arena.Lv, null);
                    if (pct > best) { best = pct; bestAt = $"{acc}:{spd}"; }
                    Console.WriteLine($"  {acc,4} : {spd,-7}{pct,7:0.0}%{_lastAllyActions,11:0}");
                }
                Console.WriteLine($"  ⭐ 一番高いのは 命中:速度 = {bestAt}（{best:0.0}%）");
            }
            Console.WriteLine();
            Console.WriteLine("  ⚠️ 両端（0:40 か 40:0）が最高なら、混ぜる意味は無い＝二者択一のまま");
        }

        // ── ステの実際の桁（2026-08-26・作者の指摘「速度の見かけが大きい」）────
        // ⭐ **推測で桁を動かさない。**⚠️ `Stats.Scale` まわりは較正が連鎖しているので、
        //    まず「実際にどこまで行くのか」を出す。
        private static void RangeProbe(int seed)
        {
            var rng = new Rng(seed).Stream("range");
            int serial = 0;
            string best = "haneru";   // ⭐ 速度の種族基礎が最高（130）

            (string Name, int Gen, int Wild, int Train, bool Slant)[] cases =
            {
                ("孵ったばかり（素質0・無育成）",      1,  0,  0, false),
                ("素質MAX（野生40）・無育成",         1, 40,  0, false),
                ("素質MAX＋育成MAX（50点）",          1, 40, 50, false),
                ("21代・素質MAX（野生60）＋育成MAX",  21, 60, 50, false),
                ("同上＋大得意（×1.30×1.15）",        21, 60, 50, true),
            };

            Console.WriteLine();
            Console.WriteLine($"■ ステの実際の桁（種族 {best}・1本に全部寄せた場合）");
            Console.WriteLine($"  ⚠️ 育成は**そのステ1本へ全部**振った場合（`TrainMax` = {Creatures.TrainMax}）");
            Console.WriteLine();
            Console.Write($"  {"条件",-34}");
            foreach (var k in Stats.Keys) Console.Write($"{Stats.LabelOf(k),10}");
            Console.WriteLine();

            foreach (var (name, gen, wild, train, slant) in cases)
            {
                Console.Write($"  {name,-34}");
                foreach (var key in Stats.Keys)
                {
                    var w = new StatBlock(0, 0, 0, 0);
                    w = w.With(key, wild);
                    var born = Born(rng, best, 5, ref serial);
                    var c = new Creature(born.Id, best, w, new StatBlock(0, 0, 0, 0), train,
                        born.MutationCounter, null, null, born.PaletteIndex, null, null, gen,
                        slant ? (StatKey?)null : null, null, born.Element, born.TraitId,
                        slant ? (StatKey?)key : null, slant ? Other(key) : null);
                    if (train > 0) Creatures.Spend(c, key, train);
                    Console.Write($"{Creatures.StatsOf(c)[key],10}");
                }
                Console.WriteLine();
            }

            Console.WriteLine();
            Console.WriteLine($"  ⭐ ゲージは (GaugeBase {Battle.GaugeBase} ＋ 速度) ずつ溜まり、"
                + $"{Battle.GaugeMax} で1手番");
            Console.WriteLine("  ⚠️ 速度だけ桁を下げるなら GaugeBase と GaugeMax も一緒に割る必要がある");
        }

        /// <summary>大得意の相方（大不得意）。⚠️ 同じキーだと `Slanted` が両方捨てる。</summary>
        private static StatKey Other(StatKey key) =>
            key == StatKey.Hp ? StatKey.Res : StatKey.Hp;

        // ── 数字ではなく「効き目の幅」を見る（2026-08-26・作者の問い）──────
        //
        // 🔴 **作者の問い**: 「この数字を決めるのってどうやるのが賢い？ダメージ計算から逆算する？」
        // ⭐ **答え: そのとおり。**ステの生の桁は**任意**で、意味を持つのは
        //    「その数が式に入ったとき何倍になるか」だけ。だから決める順は:
        //      ① 遊びの言葉で目標を置く（例「最大まで育てた壁は被ダメを8割減らす」）
        //      ② 式を解いて相棒の定数を出す（防御なら `DefSoften`）
        //      ③ 生の桁は**読みやすさ**だけで決める（効き目は②が保証する）
        // ⚠️ 生の桁だけ動かすと効き目まで動く ── だから `Stats.cs` に
        //    「一緒に動かす定数の一覧」が書いてある。
        private static void FeelProbe(int seed)
        {
            var rng = new Rng(seed).Stream("feel");
            int serial = 0;

            int Stat(string sp, StatKey key, int wild, int train, int gen)
            {
                var w = new StatBlock(0, 0, 0, 0).With(key, wild);
                var born = Born(rng, sp, 5, ref serial);
                var c = new Creature(born.Id, sp, w, new StatBlock(0, 0, 0, 0), train,
                    born.MutationCounter, null, null, born.PaletteIndex, null, null, gen,
                    null, null, born.Element, born.TraitId);
                if (train > 0) Creatures.Spend(c, key, train);
                return Creatures.StatsOf(c)[key];
            }

            (string Name, int Wild, int Train, int Gen)[] tiers =
            {
                ("孵ったばかり", 0, 0, 1),
                ("素質MAX",     40, 0, 1),
                ("素質＋育成MAX", 40, Creatures.TrainMax, 1),
                ("21代＋育成MAX", 60, Creatures.TrainMax, 21),
            };

            Console.WriteLine();
            Console.WriteLine("■ ① HP ── 生ステ × HpScale が戦闘のHP");
            Console.WriteLine($"  ⭐ HpScale = {Battle.HpScale}（= 3 × HpBoost {Battle.HpBoost}）");
            foreach (var (name, wild, train, gen) in tiers)
            {
                int raw = Stat("tamaru", StatKey.Hp, wild, train, gen);
                Console.WriteLine($"  {name,-16}生 {raw,6}  →  戦闘 {raw * Battle.HpScale,9:#,0}");
            }

            Console.WriteLine();
            Console.WriteLine("■ ② 防御 ── 何割の攻撃を止めるか");
            Console.WriteLine($"  ⭐ 軽減 = (DefSoften {Battle.DefSoften} ÷ (DefSoften ＋ 防御))²");
            foreach (var (name, wild, train, gen) in tiers)
            {
                int d = Stat("tamaru", StatKey.Def, wild, train, gen);
                double soft = (double)Battle.DefSoften / (Battle.DefSoften + d);
                Console.WriteLine($"  {name,-16}防御 {d,6}  →  通すのは {soft * soft * 100,5:0.0}%"
                    + $"  （{(1 - soft * soft) * 100,5:0.0}% 止める）");
            }

            Console.WriteLine();
            Console.WriteLine("■ ③ 速度 ── 手番がどれだけ速くなるか");
            Console.WriteLine($"  ⭐ ゲージは (GaugeBase {Battle.GaugeBase} ＋ 速度) ずつ、{Battle.GaugeMax} で1手番");
            int slowest = 0;
            foreach (var (name, wild, train, gen) in tiers)
            {
                int sp = Stat("haneru", StatKey.Spd, wild, train, gen);
                int rate = Battle.GaugeBase + sp;
                if (slowest == 0) slowest = rate;
                Console.WriteLine($"  {name,-16}速度 {sp,6}  →  ゲージ {rate,6}/刻"
                    + $"  一番遅い者の {(double)rate / slowest,4:0.0}倍");
            }

            Console.WriteLine();
            Console.WriteLine("■ ④ 何発で落ちるか（威力中・攻撃と防御を同じ段で当てる）");
            Console.WriteLine("  🔴 **ここが遊びの手触りそのもの。**⚠️ 生の桁ではなくこの数を狙って決める");
            foreach (var (name, wild, train, gen) in tiers)
            {
                int atk = Stat("tsunoga", StatKey.Atk, wild, train, gen);
                int def = Stat("tamaru", StatKey.Def, wild, train, gen);
                int hp = Stat("tamaru", StatKey.Hp, wild, train, gen) * Battle.HpScale;
                int hit = Battle.DamageOf(1000, atk, def, 1.0);
                Console.WriteLine($"  {name,-16}一撃 {hit,7:#,0}  HP {hp,9:#,0}"
                    + $"  →  {(double)hp / Math.Max(1, hit),5:0.0} 発");
            }
            Console.WriteLine();
            Console.WriteLine("  ⭐ 生の桁を1/5にしても、DefSoften・GaugeBase・HpScale を同じだけ動かせば");
            Console.WriteLine("     ①〜④ は**1つも変わらない** ── 読みやすさだけを取れる");
        }

        // ── 特性 ────────────────────────────────────────

        /// <summary>特性1つぶんの効き目。**「特性どうしの勝率」ではない。**
        ///
        /// ⭐ 役割のときと同じ測り方 — 「有ると無いとでどれだけ違うか」。
        /// 両側にまったく同じ編成を組み、片側に特性だけを足す。出る差は特性のぶんだけになる。
        /// ⚠️ 同じ種を使うので、比べている2回は**特性以外が1つも違わない**（対にして測る）。
        ///
        /// ⚠️ **噛み合う技を持たせないと 0 になる。** 役割を測ったとき、技を卵ガチャ任せに
        /// していたらどの役を抜いても落ち込みが 0 だった。特性も同じで、特性だけでは何もしない
        /// （技を強くするのではなく**動き**を強くするため）。
        /// だから噛み合う技と噛み合わない技の両方で測り、**右が 0 に近いこと**まで見る。
        /// 右が動いていたら、測れているのは特性ではなく技の差。</summary>
        private static void TraitCensus(int seed)
        {
            // ⚠️ 役割（120回）より多く取る。特性には数 pt しか動かないものがあり、
            //    120回だと種を変えるだけで符号が変わって「効いている」と読み違える
            //    （最初 120回で測って、狙い澄まし +2.5pt / 意地 +4.2pt と書いてしまった）
            const int Samples = 400;
            const int Tier = 5;

            // ⭐ 噛み合わせは Trait.cs の「噛み合うもの」の欄そのまま。勝手に足さない。
            // ⚠️ **両側とも同じ技**にする。片側だけ別の技を持たせると勝率が 95% や 11% へ張り付き、
            //    天井と床に潰されて特性のぶんが見えなくなる（最初にそれをやって測り直した）。
            // ⚠️ 意地は相手に弱化役が要るが、両側が弱化役なら条件は満たされる。
            var cases = new[]
            {
                new TraitCase(Traits.Aim, "curse", "slow-all"),
                new TraitCase(Traits.Stubborn, "curse", "slow-all"),
                new TraitCase(Traits.Spite, "taunt", "bulwark"),
                new TraitCase(Traits.Grit, "shield-wall", "harden"),
                new TraitCase(Traits.Flurry, "attack-twice", "attack-thrice"),
                new TraitCase(Traits.Leech, "attack", "attack-twice"),
                // ── 条件付きの層（2026-08-19）。噛み合わせは Trait.cs の欄のとおり
                // ⚠️ 先駆け・置き土産・背水・粘り腰は「動き」の条件が技より広いので、
                //    右（噛み合わない技）も多少動くのは織り込み（返し身と同じ性格）
                new TraitCase(Traits.Opener, "slow-all", "curse"),
                new TraitCase(Traits.Parting, "taunt", "revive"),
                new TraitCase(Traits.Pursuit, "curse", "poison"),
                new TraitCase(Traits.Desperation, "attack-all-twice", "stun-heavy"),
                new TraitCase(Traits.Tenacity, "heal-ratio", "regen"),
                // ⭐ 畳み掛けは「弱化を通すこと」が条件なので、弱化技を持たせる
                new TraitCase(Traits.Surge, "curse", "poison"),
                // ⭐ 盤面を見る2件。⚠️ 条件を作る技（止める／倒れる）を持たせないと測れない
                new TraitCase(Traits.Ambush, "stun", "stun-heavy"),
                new TraitCase(Traits.Legacy, "taunt", "attack-all-heavy"),
            };
            // ⚠️ 対照。どの特性とも噛み合わない組み合わせ（単発の一撃 + 自己強化）どうし
            const string Dull2 = "attack-heavy";
            const string Dull3 = "def-up";

            Console.WriteLine();
            Console.WriteLine($"■ 特性の効き目（段階{Tier}・各{Samples}回・属性は両側そろえる）");
            Console.WriteLine("  まったく同じ編成どうしで、片側にだけ特性を足したときの勝率の伸び");
            Console.WriteLine("  ⭐ pt ＝ 勝率の**差**（54.5% → 74.5% なら 20.0pt）。⚠️ 誤差は ±2.5pt 程度");
            Console.WriteLine("  ⭐ 左＝噛み合う技を持たせたとき / 右＝わざと関係ない技を持たせたとき");

            foreach (var one in cases)
            {
                string tag = $"trait-{one.Trait}";
                double bare = WinRate(seed, tag, one.Skill2, one.Skill3, null, Samples, Tier);
                double with = WinRate(seed, tag, one.Skill2, one.Skill3, one.Trait, Samples, Tier);

                string dullTag = $"trait-dull-{one.Trait}";
                double dullBare = WinRate(seed, dullTag, Dull2, Dull3, null, Samples, Tier);
                double dullWith = WinRate(seed, dullTag, Dull2, Dull3, one.Trait, Samples, Tier);

                var trait = Traits.ById(one.Trait);
                Console.WriteLine(
                    $"  {trait.Name,-6} 噛み合う技 {bare,5:0.0}% → {with,5:0.0}%  伸び {with - bare,6:0.0}pt" +
                    $"   噛み合わない技 伸び {dullWith - dullBare,6:0.0}pt");
            }
            Console.WriteLine("  ⚠️ 左の伸びが 0 付近なら、繋がっていても仕事をしていない");
            Console.WriteLine("  ⚠️ 右も動いているなら、その特性は技を選ばない＝噛み合わせの判断が生まれない");
        }

        private sealed class TraitCase
        {
            public readonly string Trait;
            public readonly string Skill2, Skill3;

            public TraitCase(string trait, string skill2, string skill3)
            {
                Trait = trait; Skill2 = skill2; Skill3 = skill3;
            }
        }

        /// <summary>⚠️ <paramref name="tag"/> が同じなら、特性以外は1つも違わない試合になる。
        /// 有り無しを比べるときは必ず同じ tag を渡すこと（違う tag だと編成の差を測ってしまう）。</summary>
        private static double WinRate(int seed, string tag, string skill2, string skill3, string? traitId,
            int samples, int tier)
        {
            int won = 0;
            for (int i = 0; i < samples; i++)
            {
                var rng = new Rng(seed + i).Stream(tag);
                int serial = 0;
                // ⚠️ 弱化の乱数も1戦ごとに変える。⭐ ただし**有り無しで同じ列**にする
                //    （tag と i が同じなら同じ列 ＝ 比べている2回は特性以外が1つも違わない）
                var land = new Rng(seed + i).Stream($"land-{tag}");
                var fight = Run(
                    TraitParty(rng, skill2, skill3, traitId, tier, ref serial),
                    TraitParty(rng, skill2, skill3, null, tier, ref serial),
                    land);
                if (fight.Result == Outcome.Ally) won++;
            }
            return samples == 0 ? 0.0 : 100.0 * won / samples;
        }

        /// <summary>同じ技と同じ特性を3体に持たせた編成。
        /// ⚠️ 属性は両側 Fire に揃える。倍率が 1.0 になるので、出る差が特性だけになる。</summary>
        /// <summary>⭐ 枠2・3 を決め打ちにした4体を、外の道具からも作れるようにする入口
        /// （`GuessProbe` が使う）。⚠️ 組み方を写さない ── 写すと測る相手が別物になる。</summary>
        public static List<Creature> PartyWith(Rng rng, string skill2, string skill3,
            int tier, ref int serial) => TraitParty(rng, skill2, skill3, null, tier, ref serial);

        private static List<Creature> TraitParty(Rng rng, string skill2, string skill3, string? traitId,
            int tier, ref int serial)
        {
            var party = new List<Creature>();
            var ids = new List<string>();
            foreach (var s in SpeciesTable.All) ids.Add(s.Id);

            for (int i = 0; i < Games.PartySize; i++)
            {
                string speciesId = ids[rng.Int(0, ids.Count)];
                var born = Born(rng, speciesId, tier, ref serial, Element.Fire);
                party.Add(new Creature(
                    born.Id, born.SpeciesId, born.Wild, born.Trained, born.Earned,
                    born.MutationCounter, skill2, skill3, born.PaletteIndex,
                    born.ParentA, born.ParentB, born.Generation, born.Strong, born.Weak,
                    born.Element, traitId));
            }
            return party;
        }

        // ── 潜入（発射フェーズ）──────────────────────────

        /// <summary>その巣が**解けるか**を編成の型ごとに調べる。
        ///
        /// ⚠️ **「解けない巣」を出荷したら、プレイヤーは運が悪いのだと思ってしまう。**
        /// 関門とリレーが入って、盤が解けるかどうかは目で見て分からなくなった。ここが唯一の確認手段。
        ///
        /// ⭐ 盗まれた回数（raids）が増えると解けなくなるのは**仕様**（最後は親が塞ぎ切る）。
        /// ⚠️ 見るのは「raids 0 でどの型も解けない段があるか」。あればそれは設計の穴。</summary>
        private static void Infiltrate(int seed)
        {
            // ⚠️ 素質は合計120まで。1ステ上限40（＝3つまで振り切れる）
            //                        HP  攻  防  速  命中 抵抗
            var shapes = new[]
            {
                new PartyShape("速度ぞろい",
                    new StatBlock(20, 10, 20, 40, 10, 20), new StatBlock(20, 10, 20, 40, 10, 20),
                    new StatBlock(20, 10, 20, 40, 10, 20)),
                new PartyShape("均等ぞろい",
                    new StatBlock(20, 20, 20, 20, 20, 20), new StatBlock(20, 20, 20, 20, 20, 20),
                    new StatBlock(20, 20, 20, 20, 20, 20)),
                new PartyShape("役割分担",
                    new StatBlock(0, 40, 0, 40, 40, 0), new StatBlock(40, 0, 40, 0, 0, 40),
                    new StatBlock(10, 10, 20, 40, 10, 30)),
                new PartyShape("耐久ぞろい",
                    new StatBlock(40, 0, 40, 0, 0, 40), new StatBlock(40, 0, 40, 0, 0, 40),
                    new StatBlock(40, 0, 40, 0, 0, 40)),
            };
            const int Samples = 17;
            // ⭐ 旧設計（一投）の実測は「成功する角度の幅 6〜17度」。そこを目安にする
            const int WantDegrees = 8;
            // ⚠️ 幅を測るのは重い。何本まで測るかの上限が無いと総当たりになる
            const int Give = 40;

            Console.WriteLine();
            Console.WriteLine("■ 潜入が解けるか（3体リレー・角度は10度刻みで走査）");
            Console.WriteLine($"  数字 = 通る角度の幅（一番狭い一投）。⭐ 目安は {WantDegrees}度以上・× は解なし");
            Console.WriteLine("  ⭐ raids が増えて解けなくなるのは仕様。⚠️ raids 0 の段で全滅したら設計の穴");

            // ⭐ 参照編成そのもの。⚠️ **ここが × なら盤の生成のバグ**（線引きの片側）
            foreach (var shape in Prepend(shapes))
            {
                var party = shape.Party(0);
                double reach = 0;
                foreach (var c in party) reach += Steal.DistanceFor(c);

                Console.WriteLine();
                Console.WriteLine($"  {shape.Name}（飛距離の合計 {reach:0}）");
                for (int tier = 1; tier <= 5; tier++)
                {
                    var cells = new List<string>();
                    for (int raids = 0; raids <= 3; raids++)
                    {
                        // ⭐ 出荷する経路で作る（検査と振り直しを通す）
                        var nest = new Nest($"sim-t{tier}", "測定", "tamaru", tier);
                        var field = Steal.MakeValidatedField(tier, FieldSide.Right, raids,
                            Steal.RngFor(nest, raids));
                        List<Steal.Shot> plan;
                        int window;
                        // ⚠️ 「解が在るか」ではなく「通る角度が何度ぶんあるか」を見る。
                        //    幅1度の針の穴は、プレイヤーには「運が悪い」としか見えない
                        // ⚠️ 参照編成は段ごとに素質が違う。段の中で組み直す
                        var probe = shape.Party(tier);
                        bool ok = Steal.FindRoomySolution(field, probe, Samples,
                            WantDegrees, Give, out plan, out window);
                        cells.Add(ok ? $"{window,3}°" : "  × ");
                    }
                    Console.WriteLine($"    段{tier}（奥行き {Steal.DepthForTier(tier):0}・関門 " +
                        $"{Steal.GimmickCountFor(tier, 0)}〜{Steal.GimmickCountFor(tier, 3)}）  " +
                        string.Join("  ", cells));
                }
            }
            Console.WriteLine();
            Console.WriteLine("  列は raids 0 / 1 / 2 / 3。× は解が1つも無い");

            MobReach(shapes[1].Party());
        }

        /// <summary>雑魚に当たる角度が何度ぶんあるか。
        ///
        /// ⭐ 雑魚は「わざと当てに行く」ものなので、**狙って当てられる幅**が要る。
        /// ⚠️ 幅が 0 だと、盤に居るのに一生届かない飾りになる。
        /// ⚠️ 逆に広すぎると、卵へ向かうたびに事故で戦闘が始まる。</summary>
        private static void MobReach(IReadOnlyList<Creature> party)
        {
            Console.WriteLine();
            Console.WriteLine("■ 雑魚に当たる角度（初期位置から1投目・0.2度刻み）");
            Console.WriteLine("  ⭐ 狙って当てられる幅が要る。⚠️ 0度なら盤に居るだけの飾り");

            for (int tier = 1; tier <= 5; tier++)
            {
                var cells = new List<string>();
                for (int raids = 0; raids <= 3; raids++)
                {
                    var nest = new Nest($"sim-mob-t{tier}", "測定", "tamaru", tier);
                    var field = Steal.MakeValidatedField(tier, FieldSide.Right, raids,
                        Steal.RngFor(nest, raids));
                    if (field.Mobs.Count == 0) { cells.Add("  － "); continue; }

                    int hits = 0, steps = 0;
                    for (double a = -1.2; a <= 1.2; a += 0.2 * Math.PI / 180.0)
                    {
                        steps++;
                        var probe = new Steal.Infiltration(field, party);
                        if (Steal.Hop(probe, 0, -1, a).Outcome == StealOutcome.Fought) hits++;
                    }
                    cells.Add($"{hits * 0.2,4:0.0}°");
                }
                Console.WriteLine($"    段{tier}（雑魚 {Steal.MobCountFor(tier)} 体）  "
                    + string.Join("  ", cells));
            }
            Console.WriteLine();
            Console.WriteLine("  列は raids 0 / 1 / 2 / 3。－ は雑魚が居ない段");
        }

        /// <summary>弱化の通る率が、実際にどれだけ動くか。
        /// ⚠️ 定数だけ見ても効き目が読めないので、現実のステ域で測る。</summary>
        /// <summary>投げた1回が**何をして終わっているか**。
        ///
        /// ⭐ 「モンストの部分が機能していない」という作者の指摘を、
        /// 推測でなく数で見るための道具（2026-08-20）。
        /// ⚠️ 見るのは勝ち負けではなく**跳ね返りの回数と、終わり方の内訳**。</summary>
        private static void FlightProbe(int seed)
        {
            Console.WriteLine();
            Console.WriteLine("■ 投げた1回はどう終わっているか（段ごと・角度を1度刻みで全部試す）");
            Console.WriteLine($"  {"段",4}{"投数",7}{"壁で跳ねた回数",16}{"関門で跳ねた",14}"
                + $"{"卵",6}{"親",6}{"雑魚",7}{"力尽き",8}");

            var rng = new Rng(seed);
            for (int tier = 1; tier <= 5; tier++)
            {
                var party = Steal.ReferenceParty(tier);
                int shots = 0, wall = 0, gate = 0, egg = 0, parent = 0, mob = 0, spent = 0;
                double pathSum = 0;

                for (int n = 0; n < 12; n++)
                {
                    var field = Steal.MakeField(tier, FieldSide.Right, 0, rng);
                    foreach (var who in party)
                    {
                        for (int deg = 0; deg < 360; deg += 1)
                        {
                            var run = Steal.Preview(
                                new Steal.Infiltration(field, party), 0, -1, deg * Math.PI / 180.0);
                            if (run.Path.Count < 2) continue;
                            shots++;
                            pathSum += run.Path.Count;
                            // ⭐ 壁の跳ね返りは経路の折れで数える
                            wall += Turns(run.Path);
                            if (run.Bounced) gate++;
                            switch (run.Outcome)
                            {
                                case StealOutcome.Success: egg++; break;
                                case StealOutcome.Blocked: parent++; break;
                                case StealOutcome.Fought: mob++; break;
                                default: spent++; break;
                            }
                        }
                        break;   // 1体ぶんで足りる
                    }
                }
                Console.WriteLine($"  {tier,4}{shots,7}{(double)wall / shots,16:0.00}"
                    + $"{100.0 * gate / shots,13:0}%{100.0 * egg / shots,5:0}%"
                    + $"{100.0 * parent / shots,5:0}%{100.0 * mob / shots,6:0}%"
                    + $"{100.0 * spent / shots,7:0}%");
            }

            Console.WriteLine();
            Console.WriteLine("  ⚠️ 「力尽き」＝何にも当たらず飛距離を使い切った投");

            // ══ 捨てている接触を数える ══════════════════
            // ⭐ いまは**最初に当たった1つで飛行が終わる**。
            //    もし終わらなかったら、その1投は何回ぶつかっていたか？
            Console.WriteLine();
            Console.WriteLine("■ 1投が「当たれたはずの数」（飛行を止めずに最後まで飛ばした場合）");
            Console.WriteLine($"  {"段",4}{"いま当たる数",14}{"止めなければ",14}{"捨てている割合",16}");

            for (int tier = 1; tier <= 5; tier++)
            {
                var party = Steal.ReferenceParty(tier);
                int shots = 0, now = 0, could = 0;
                for (int n = 0; n < 12; n++)
                {
                    var field = Steal.MakeField(tier, FieldSide.Right, 0, rng);
                    var infil = new Steal.Infiltration(field, party);
                    for (int deg = 0; deg < 360; deg += 1)
                    {
                        var run = Steal.Preview(infil, 0, -1, deg * Math.PI / 180.0);
                        if (run.Path.Count < 2) continue;
                        shots++;
                        if (run.Outcome != StealOutcome.Landed && run.Outcome != StealOutcome.Stalled)
                            now++;
                        could += FreeFlight(field, field.Start,
                            deg * Math.PI / 180.0, Steal.DistanceFor(party[0]));
                    }
                }
                Console.WriteLine($"  {tier,4}{(double)now / shots,14:0.00}{(double)could / shots,14:0.00}"
                    + $"{100.0 * (could - now) / Math.Max(1, could),15:0}%");
            }

            Console.WriteLine();
            Console.WriteLine("  ⭐ モンストは「当たって跳ねて**また当たる**」が本体。");
            Console.WriteLine("  ⚠️ いまは最初の1つで飛行が終わるので、上の差ぶんが丸ごと消えている。");

            // ══ 盤の埋まり具合 ═══════════════════════════
            Console.WriteLine();
            Console.WriteLine("■ 盤に的がどれだけ在るか");
            Console.WriteLine($"  {"段",4}{"盤の広さ",12}{"的の数",8}{"的が覆う割合",14}");
            for (int tier = 1; tier <= 5; tier++)
            {
                double area = 0, targets = 0, boards = 0;
                for (int n = 0; n < 12; n++)
                {
                    var field = Steal.MakeField(tier, FieldSide.Right, 0, rng);
                    double a2 = Steal.FieldWidth * field.Height;
                    double t = Math.PI * Steal.EggRadius * Steal.EggRadius
                        + Steal.ParentWidth * 18.0
                        + field.Mobs.Count * Math.PI * Steal.MobRadius * Steal.MobRadius;
                    area += a2; targets += t; boards++;
                    if (n == 0) Console.Write($"  {tier,4}{a2,12:N0}{field.Mobs.Count + 2,8}");
                }
                Console.WriteLine($"{100.0 * targets / area,13:0.0}%");
            }
            Console.WriteLine();
            Console.WriteLine("  ⚠️ モンストの盤は的で埋まっている。ここは**ほぼ空き地**。");
        }

        /// <summary>**止まらずに**飛ばしたら何回ぶつかったか。
        ///
        /// ⚠️ `Steal.Preview` は最初の接触で止まった経路を返すので、
        /// そこから「止めなかったら」は測れない。⭐ ここでは壁と関門だけで跳ね返し、
        /// 的に当たっても**止めずに**飛距離を使い切るまで進める。
        /// ⚠️ 本番の式（Fly）の写しなので、刻みと跳ね返りは同じにしてある。</summary>
        /// <summary>経路の折れ（＝跡ね返り）の回数。</summary>
        private static int Turns(IReadOnlyList<Point> path)
        {
            int n = 0;
            for (int i = 2; i < path.Count; i++)
            {
                double ax = path[i - 1].X - path[i - 2].X, ay = path[i - 1].Y - path[i - 2].Y;
                double bx = path[i].X - path[i - 1].X, by = path[i].Y - path[i - 1].Y;
                if (Math.Abs(ax - bx) > 1e-9 || Math.Abs(ay - by) > 1e-9) n++;
            }
            return n;
        }

        private const double STEP = 1;   // ⚠️ Steal.Step の写し（private なので見えない）

        private static int FreeFlight(StealField field, Point from, double angle, double budget)
        {
            double x = from.X, y = from.Y;
            double dx = Math.Sin(angle), dy = -Math.Cos(angle);
            double traveled = 0;
            int hits = 0, lastMob = -2;
            bool inEgg = false, inParent = false;

            while (traveled < budget)
            {
                x += dx * STEP;
                y += dy * STEP;
                traveled += STEP;

                if (x < Steal.RunnerRadius) { x = Steal.RunnerRadius; dx = -dx; }
                else if (x > Steal.FieldWidth - Steal.RunnerRadius)
                { x = Steal.FieldWidth - Steal.RunnerRadius; dx = -dx; }
                if (y < Steal.RunnerRadius) { y = Steal.RunnerRadius; dy = -dy; }
                else if (y > field.Height - Steal.RunnerRadius)
                { y = field.Height - Steal.RunnerRadius; dy = -dy; }

                int mob = -1;
                for (int i = 0; i < field.Mobs.Count; i++)
                {
                    double mx = field.Mobs[i].At.X - x, my = field.Mobs[i].At.Y - y;
                    double r = field.Mobs[i].Radius + Steal.RunnerRadius;
                    if (mx * mx + my * my <= r * r) { mob = i; break; }
                }
                if (mob >= 0 && mob != lastMob) hits++;
                lastMob = mob;

                double ex = field.Egg.X - x, ey = field.Egg.Y - y;
                double er = Steal.EggRadius + Steal.RunnerRadius;
                bool egg = ex * ex + ey * ey <= er * er;
                if (egg && !inEgg) hits++;
                inEgg = egg;

                bool par = y >= field.BandTop && y <= field.BandBottom
                    && (x < field.GapFrom || x > field.GapTo);
                if (par && !inParent) hits++;
                inParent = par;
            }
            return hits;
        }

        /// <summary>すごろくの数字が成立するか。⭐ **実際の編成で回して測る。**
        ///
        /// ⚠️ 机上の分布（さいころの合計）だけでは、分岐・雑魚・増減が入った後の
        /// 実際の届く率は出ない。ここでは本番と同じ <see cref="Trails"/> を回す。</summary>
        private static void TrailProbe(int seed)
        {
            Console.WriteLine();
            Console.WriteLine("■ 盤の大きさと、編成の持ち分");
            Console.WriteLine($"  {"段",4}{"分かれ道",10}{"マス数",8}{"関門",7}{"最短",7}{"最長",7}{"無関門",8}"
                + $"{"振れる回数",12}{"攻",7}{"HP",7}{"防",7}");
            for (int tier = 1; tier <= 5; tier++)
            {
                var party = Steal.ReferenceParty(tier);
                var trail = Trails.Make(new Rng(seed).Stream($"size:{tier}"), tier);
                var raid = Trails.Begin(trail, party);
                int shortest = Trails.Left(raid);
                // ⚠️ どの繋ぎも1段だけ進むので、**どの行き方も同じマス数**（2026-08-21）。
                //    ⭐ 距離の伸び縮みは、いまはマスがくれる Hop が担う。
                int longest = trail.Depth;
                int safe = trail.Depth;
                int gates = 0;
                foreach (var sq in trail.Squares) if (sq.IsGate) gates++;
                var pool = Trails.PoolOf(party);
                Console.WriteLine($"  {tier,4}{trail.Junctions.Count,10}{trail.Count,8}"
                    + $"{gates,7}{shortest,7}{longest,7}{safe,8}{raid.Rolls,12}"
                    + $"{pool.Atk,7}{pool.Hp,7}{pool.Def,7}");
            }

            Console.WriteLine();
            Console.WriteLine("■ 関門の段ごとの重さ（参照編成の持ち分に対する %・攻の場合）");
            Console.Write($"  {"段",4}");
            for (int g = 1; g <= Trail.GateGrades; g++) Console.Write($"{"段" + g,9}");
            Console.WriteLine();
            for (int tier = 1; tier <= 5; tier++)
            {
                var pool = Trails.PoolOf(Steal.ReferenceParty(tier));
                Console.Write($"  {tier,4}");
                for (int g = 1; g <= Trail.GateGrades; g++)
                {
                    int price = Trail.PriceOfGrade(GimmickKind.Wall, tier, g);
                    Console.Write($"{100.0 * price / pool.Atk,8:0}%");
                }
                Console.WriteLine();
            }
            Console.WriteLine("  ⚠️ **消費**なので、持ち分をこの率で割った数が『何回払えるか』");

            // ⭐ 指し手を並べる。⚠️ 同じ盤・同じ編成で、選び方だけを変えて比べる。
            // ⭐ **払い方も指し手のうち**（2026-08-21・関門は払って対価をもらう形になった）。
            var moves = new[]
            {
                new { Name = "素で行く（何も払わない）", Pick = (Pick)Nearest, Purse = (Purse)Keep },
                new { Name = "払えるだけ払う", Pick = (Pick)Nearest, Purse = (Purse)Spend },
                // ⭐ **関門を拾いに行く。**⚠️ 2026-08-21 まで表に無かった一手
                new { Name = "関門を拾う＋払う", Pick = (Pick)Tolls, Purse = (Purse)Spend },
                new { Name = "敵を拾う＋払う", Pick = (Pick)Hunt, Purse = (Purse)Spend },
                new { Name = "▲を拾う＋払う", Pick = (Pick)Gather, Purse = (Purse)Spend },
                new { Name = "いちばん先へ＋払う", Pick = (Pick)((r, o) => Far(r, o, true)), Purse = (Purse)Spend },
            };

            Console.WriteLine();
            Console.WriteLine("■ 指し手を変えて回す（段5・6000回・雑魚に 8% 負ける想定）");
            Console.WriteLine($"  {"指し手",-26}{"卵",7}{"詰み",8}{"敵に負け",10}{"力尽き",9}"
                + $"{"払った",8}{"踏んだ関門",11}{"倒した",7}{"+回数",7}{"+マス",7}");
            foreach (var move in moves)
                Console.WriteLine("  " + RunTrail(seed, 5, move.Name, move.Pick, 6000, move.Purse));

            Console.WriteLine();
            Console.WriteLine("  ⚠️ 『いつも近い道』を上回る指し手が無いなら、選ぶ意味が無い");
            Console.WriteLine("  ⚠️ 『詰み』＝どの道も通れなくなった（編成が足りない）");
            TrailDoubt(seed);
            TrailPace();

            Console.WriteLine();
            Console.WriteLine("■ 段ごと（3000回）");
            Console.WriteLine($"  {"段",4}{"素で行く",11}{"払う",9}{"関門を拾う",14}{"払いの効き",14}");
            for (int tier = 1; tier <= 5; tier++)
            {
                // ⭐ **同じ指し手で払う／払わないを並べる。**⚠️ 分けないと
                //    「道の選び方の差」と「払いの差」が混ざって読めない
                double bare = WinRate(seed, tier, Nearest, Keep, 3000);
                double paid = WinRate(seed, tier, Nearest, Spend, 3000);
                double seek = WinRate(seed, tier, Tolls, Spend, 3000);
                Console.WriteLine($"  {tier,4}{bare,10:0%}{paid,9:0%}{seek,12:0%}{paid - bare,12:+0%;-0%}");
            }
            Console.WriteLine("  ⚠️ 『素で行く』＝払える物を全部見送った率。"
                + "⭐ ここが遊べる率でないと、払いが**義務**になる");

            Console.WriteLine();
            Console.WriteLine("■ ⭐ 寄せた編成は、噛み合う巣でなら強いか（段5の道 600本）");
            Console.WriteLine($"  {"道の顔つき",-16}{"ならし",10}{"攻に寄せ",11}{"防に寄せ",11}");
            {
                var party = Steal.ReferenceParty(5);
                var flat = Trails.PoolOf(party);
                var pools = new (string Name, StatBlock Pool)[]
                {
                    ("ならし", flat),
                    ("攻に寄せ", flat.With(StatKey.Atk, flat.Atk * 3 / 2)
                        .With(StatKey.Def, flat.Def / 2).With(StatKey.Hp, flat.Hp / 2)),
                    ("防に寄せ", flat.With(StatKey.Def, flat.Def * 3 / 2)
                        .With(StatKey.Atk, flat.Atk / 2).With(StatKey.Hp, flat.Hp / 2)),
                };
                foreach (var face in new[] { GimmickKind.Wall, GimmickKind.Pressure })
                {
                    var picked = new List<Trail>();
                    var made = new Rng(seed).Stream("faces");
                    int guard = 0;
                    while (picked.Count < 600 && guard++ < 200000)
                    {
                        var t = Trails.Make(made, 5);
                        int mine = 0, all = 0;
                        foreach (var sq in t.Squares)
                        {
                            if (sq.Toll == null) continue;
                            all++; if (sq.Toll.Kind == face) mine++;
                        }
                        // ⭐ その関門が多く寄っている盤
                        if (all > 0 && mine * 5 > all * 2) picked.Add(t);
                    }
                    var row = $"  {(face == GimmickKind.Wall ? "壁が多い" : "重圧が多い"),-16}";
                    foreach (var p in pools)
                    {
                        int win = 0, runs = 0;
                        var play = new Rng(seed).Stream("play");
                        foreach (var t in picked)
                            for (int rep = 0; rep < 6; rep++)
                            {
                                var raid = Trails.Begin(t, party);
                                raid.Pool = p.Pool;
        
                                // ⭐ **払わせる。**⚠️ ここは「寄せた編成が噛み合う関門で強いか」を
                                //    見る表なので、払わないと**測りたい物がそもそも起きない**
                                if (raid.Result == null) Play(play, raid, Gather, Spend);
                                runs++;
                                if (raid.Result == StealOutcome.Success) win++;
                            }
                        row += $"{100.0 * win / Math.Max(1, runs),10:0}%";
                    }
                    Console.WriteLine(row);
                }
            }

            Console.WriteLine();
            Console.WriteLine("■ 盗むほど苦しくなるか（段5・3000回・指し手は『危なければ遠回り』）");
            Console.WriteLine($"  {"盗んだ回数",12}{"振れる回数",12}{"卵に届く",10}");
            {
                var party = Steal.ReferenceParty(5);
                for (int raids = 0; raids < Steal.RaidsToSeal; raids++)
                {
                    var rng = new Rng(seed).Stream("trail:raids");
                    int win = 0;
                    const int runs = 3000;
                    for (int n = 0; n < runs; n++)
                    {
                        var raid = Trails.Begin(Trails.Make(rng, 5), party, raids);
                        if (raid.Result == null) Play(rng, raid, Gather, Spend);
                        if (raid.Result == StealOutcome.Success) win++;
                    }
                    Console.WriteLine($"  {raids,12}{Trails.RollsFor(party, raids),12}"
                        + $"{100.0 * win / runs,9:0}%");
                }
                Console.WriteLine($"  {Steal.RaidsToSeal,12}{"—",12}{"必ず戦闘",10}");
            }

            Console.WriteLine();
            Console.WriteLine("■ 巣ごとに道が固定されるか（同じ巣を2回作って突き合わせる）");
            {
                int same = 0, total = 0;
                foreach (var nest in Nests.All)
                {
                    var a = Trails.OfNest(nest);
                    var b = Trails.OfNest(nest);
                    total++;
                    bool eq = a.Count == b.Count;
                    for (int i = 0; eq && i < a.Count; i++)
                    {
                        eq = a.Squares[i].Kind == b.Squares[i].Kind
                            && a.Squares[i].Ways.Count == b.Squares[i].Ways.Count;
                        for (int w = 0; eq && w < a.Squares[i].Ways.Count; w++)
                            eq = a.Squares[i].Ways[w].To == b.Squares[i].Ways[w].To;
                    }
                    if (eq) same++;
                }
                Console.WriteLine($"  {same}/{total} の巣で同じ道が出た");
            }
        }

        /// <summary>近い道／遠い道を選ぶ（通れるほう優先）。</summary>
        /// <summary>⭐ **行ける先から1つ選ぶ。**⚠️ 引数は <see cref="Trails.Reach"/> の結果。
        ///
        /// ⚠️ 以前は「道」を選んでいたが、関門がマスになり道がただの繋がりになった
        /// （2026-08-20）ので、選ぶ対象は**止まる先**になった。</summary>
        private delegate int Pick(Raid raid, List<List<int>> open);

        /// <summary>いちばん先へ進む／いちばん手前に留まる。</summary>
        private static int Far(Raid raid, List<List<int>> open, bool far)
        {
            int best = 0;
            for (int i = 1; i < open.Count; i++)
            {
                int a = raid.Trail.Squares[open[i][open[i].Count - 1]].Row;
                int b = raid.Trail.Squares[open[best][open[best].Count - 1]].Row;
                if (far ? a > b : a < b) best = i;
            }
            return best;
        }

        /// <summary>⭐ 卵までの残りが一番短くなる先。</summary>
        private static int Nearest(Raid raid, List<List<int>> open)
        {
            int best = 0, bestLeft = int.MaxValue;
            for (int i = 0; i < open.Count; i++)
            {
                int left = Trails.LeftFrom(raid.Trail, open[i][open[i].Count - 1]);
                if (left < 0) continue;
                if (left < bestLeft) { bestLeft = left; best = i; }
            }
            return best;
        }

        /// <summary>⭐ **敵を拾いに行く。**⚠️ 倒せば振れる回数が戻る。</summary>
        private static int Hunt(Raid raid, List<List<int>> open)
        {
            for (int i = 0; i < open.Count; i++)
            {
                int end = open[i][open[i].Count - 1];
                if (raid.Trail.Squares[end].Kind == SquareKind.Mob
                    && !raid.Beaten.Contains(end)) return i;
            }
            return Nearest(raid, open);
        }

        /// <summary>⭐ **▲ を拾いに行く。**⚠️ この先の関門が通れるようになる。</summary>
        private static int Gather(Raid raid, List<List<int>> open)
        {
            for (int i = 0; i < open.Count; i++)
            {
                if (raid.Trail.Squares[open[i][open[i].Count - 1]].Kind == SquareKind.Boon) return i;
            }
            return Nearest(raid, open);
        }

        /// <summary>⭐ **いま払える関門を拾いに行く。**
        ///
        /// ⚠️ 2026-08-21 の討論まで、この指し手が表に1本も無かった。⭐ 関門は
        /// 「踏んだら払うか訊かれる物」ではなく **「寄り道してでも踏みに行く物」** に
        /// なったのに、測る側が寄り道を一度も試していなかったので、
        /// **払いの効きが丸ごと視界の外**にあった。</summary>
        private static int Tolls(Raid raid, List<List<int>> open)
        {
            for (int i = 0; i < open.Count; i++)
            {
                // ⭐ `CanPay` が「払い済み」も「足りない」も見てくれる
                if (Trails.CanPay(raid, open[i][open[i].Count - 1])) return i;
            }
            return Nearest(raid, open);
        }



        /// <summary>雑魚に負ける割合。⚠️ Core は戦闘を知らないので、測るときだけ置く見積り。
        /// ⭐ 0 にすると敵が「ただの回数の素」になり、遠回りが不当に強く見える。</summary>
        private const double MobRisk = 0.08;

        /// <summary>払うか決める指し手。⭐ true なら払う。</summary>
        private delegate bool Purse(Raid raid);

        /// <summary>⭐ **払える関門は必ず払う。**</summary>
        private static bool Spend(Raid raid) => true;

        /// <summary>⭐ **何も払わない**（素で行く）。</summary>
        private static bool Keep(Raid raid) => false;

        /// <summary>1回の潜入を最後まで回す。
        ///
        /// ⚠️ **<paramref name="purse"/> は省略できない。**⭐ 省略できた頃は
        /// 既定が「何も払わない」だったので、**段ごとの表も・寄せた編成の表も・
        /// 盗むほど苦しくなるかの表も、誰も払わない世界を測っていた**
        /// （2026-08-21 の討論で発覚 ── 関門を払う形にした当日から、
        /// 釣り合いの判断が全部その数字の上に乗っていた）。
        /// ⭐ 払わない側を測りたいときは <see cref="Keep"/> を**明示的に**渡す。</summary>
        private static void Play(Rng rng, Raid raid, Pick pick, Purse purse,
            Action<IReadOnlyList<int>>? onStep = null, Action<Gift>? onPay = null)
        {
            int guard = 0;
            while (raid.Result == null)
            {
                if (++guard > 5000) throw new InvalidOperationException("潜入が終わらない");
                switch (raid.Step)
                {
                    case RaidStep.Choosing:
                    {
                        // ⭐ 出目で行ける先を並べ、その中から選ぶ（2026-08-20 の作り替え）
                        var all = Trails.Reach(raid, raid.Pending);
                        if (all.Count == 0)
                        {
                            // ⚠️ 1マスも動けない ── そこで見つかる
                            Trails.Stuck(raid);
                            break;
                        }
                        int at = pick(raid, all);
                        if (at < 0 || at >= all.Count) at = 0;
                        onStep?.Invoke(all[at]);
                        Trails.Go(raid, all[at]);
                        break;
                    }
                    case RaidStep.Met:
                        // ⚠️ 敵は戦闘。⭐ 一定の割合で負ける前提で測る
                        if (rng.Chance(MobRisk)) Trails.Lost(raid); else Trails.Beat(raid);
                        break;
                    case RaidStep.Offered:
                        // ⭐ 払うかは指し手が決める（2026-08-21）
                        if (purse(raid))
                        {
                            // ⭐ **払う前に見る。**⚠️ 払うと段が進んで居場所が変わる
                            var got = raid.Trail.Squares[raid.At].Face;
                            Trails.Pay(raid);
                            if (got != null && onPay != null) onPay(got);
                        }
                        else Trails.Pass(raid);
                        break;
                    default:
                        if (raid.Rolls <= 0) throw new InvalidOperationException("振れないのに続いている");
                        Trails.Roll(rng, raid); break;
                }
            }
        }

        private static double WinRate(int seed, int tier, Pick pick, Purse purse, int runs)
        {
            var rng = new Rng(seed).Stream($"trail:{tier}");
            var party = Steal.ReferenceParty(tier);
            int win = 0;
            for (int n = 0; n < runs; n++)
            {
                var raid = Trails.Begin(Trails.Make(rng, tier), party);
                if (raid.Result == null) Play(rng, raid, pick, purse);
                if (raid.Result == StealOutcome.Success) win++;
            }
            return (double)win / runs;
        }

        /// <summary>⭐ **速度に投資すると、関門を避けられるようになるか**を数で見る。
        ///
        /// ⭐ 潜入の駆け引きの土台になる比（2026-08-21・作者の言葉）:
        /// 「十分な速度を持ったパーティで挑めばサイコロを振れる回数が多くなるので
        /// 関門を避ける選択肢が生まれる」。
        /// ⚠️ **『要る速度 ÷ 参照の速度』が 1.0 を大きく超えないこと。**
        /// 1.0 未満なら誰でも遠回りできて関門が要らず、2.0 を超えると遠回りが絵に描いた餅になる。
        /// ⚠️ 振れる回数は割り算の**切り捨て**なので、速度は**段で効く**
        /// （少し上げても回数は増えない）。</summary>
        private static void TrailPace()
        {
            Console.WriteLine();
            Console.WriteLine("■ 速度と距離の釣り合い");
            Console.WriteLine($"  {"段",4}{"参照の速度",12}{"振れる",8}{"平均で進める",14}"
                + $"{"無関門の長さ",14}{"足りるか",10}{"要る速度",10}{"倍率",8}{"増える回数",12}");
            for (int tier = 1; tier <= 5; tier++)
            {
                var party = Steal.ReferenceParty(tier);
                int spd = 0;
                foreach (var c in party) spd += Creatures.StatsOf(c).Spd;
                int rolls = Trails.RollsFor(party);
                double reach = rolls * (Trail.Pips + 1) / 2.0;
                var trail = Trails.Make(new Rng(7).Stream($"pace:{tier}"), tier);
                int safe = trail.Depth;
                int needRolls = (int)Math.Ceiling(safe / ((Trail.Pips + 1) / 2.0));
                int needSpd = needRolls * Trail.SpeedPerRoll;
                // ⭐ **そこまで寄せたら、さいころが何回増えるか。**
                //    ⚠️ 「払ってもらう回数」と直に比べる数なので、註に書くならここから採る
                int more = needSpd / Trail.SpeedPerRoll - rolls;
                Console.WriteLine($"  {tier,4}{spd,12}{rolls,8}{reach,14:0.0}{safe,14}"
                    + $"{(reach >= safe ? "○" : "×"),10}{needSpd,10}{(double)needSpd / spd,8:0.00}"
                    + $"{"+" + more,8}");
            }
            Console.WriteLine("  ⚠️ 『要る速度』＝ 関門を1つも通らずに卵へ届く見込みが立つ速度");
            Console.WriteLine($"  ⭐ 1回振るのに要る速度 = {Trail.SpeedPerRoll}"
                + $"（1体 {Trail.SpeedPerRollEach} × {Games.PartySize}体）");

            Console.WriteLine();
            Console.WriteLine("■ ステの持ち分は、関門いくつぶんか（段5・攻の場合）");
            var pool = Trails.PoolOf(Steal.ReferenceParty(5));
            for (int g = 1; g <= Trail.GateGrades; g++)
            {
                int price = Trail.PriceOfGrade(GimmickKind.Wall, 5, g);
                Console.WriteLine($"  段{g}: {price,6} → 持ち分 {pool.Atk} を払い切ると {pool.Atk / price} 回");
            }
        }

        /// <summary>⭐ **分かれ道は意味を持っているか**を数で見る（2026-08-21）。
        ///
        /// ⚠️ 作者の指摘「分岐が意味をなしていない」を、感想ではなく数で確かめるために足した。
        /// ⭐ 見るのは3つ:
        /// <list type="bullet">
        ///   <item>**光った先が全部同じ距離か** ── 同じなら「どれだけ進むか」は選べていない</item>
        ///   <item>**距離も中身も同じか** ── 両方同じなら、その手番の選択は**完全な無意味**</item>
        ///   <item>**出目より少なく進んだか**（卵に着いたときを除く）</item>
        /// </list>
        /// ⚠️ 盤を作り替えたら必ずここを見ること。マスを増やすと**見た目は豊かになるのに
        /// 選択は薄くなる**（同じ距離の行き先が増えるだけなので）。</summary>
        private static void TrailDoubt(int seed)
        {
            Console.WriteLine();
            Console.WriteLine("■ 作者の指摘を数で見る");
            for (int tier = 1; tier <= 5; tier++)
            {
                var rng = new Rng(seed).Stream($"doubt:{tier}");
                var party = Steal.ReferenceParty(tier);
                int boards = 40;
                int squares = 0, forks = 0, hubs = 0, ways = 0;
                int rolls = 0, shortMove = 0, shortNotGoal = 0, oneWay = 0;
                int lit = 0, litSame = 0, litDead = 0, litKind = 0;
                int plain = 0, mob = 0, boon = 0, bane = 0, gate = 0;
                for (int n = 0; n < boards; n++)
                {
                    var trail = Trails.Make(rng, tier);
                    foreach (var sq in trail.Squares)
                    {
                        squares++;
                        ways += sq.Ways.Count;
                        if (sq.IsJunction) forks++;
                        // ⭐ 黒丸が出るのは「分かれ道 かつ 中身が無い」マス
                        if (sq.IsJunction && sq.Kind == SquareKind.Plain && !sq.IsGoal) hubs++;
                        if (sq.Kind == SquareKind.Plain) plain++;
                        else if (sq.Kind == SquareKind.Mob) mob++;
                        else if (sq.Kind == SquareKind.Boon) boon++;
                        else if (sq.Kind == SquareKind.Bane) bane++;
                        else if (sq.Kind == SquareKind.Gate) gate++;
                    }

                    var raid = Trails.Begin(trail, party);
                    int guard = 0;
                    while (raid.Result == null && guard++ < 90)
                    {
                        if (raid.Step == RaidStep.Met) { Trails.Beat(raid); continue; }
                        if (raid.Step == RaidStep.Offered) { Trails.Pay(raid); continue; }
                        if (raid.Step != RaidStep.Choosing)
                        {
                            if (raid.Rolls <= 0) break;
                            Trails.Roll(rng, raid);
                        }
                        var open = Trails.Reach(raid, raid.Pending);
                        if (open.Count == 0) { Trails.Stuck(raid); break; }
                        rolls++;
                        lit += open.Count;
                        if (open.Count == 1) oneWay++;
                        // ⭐ 光った先が「残りマス数」で見て全部同じなら、選ぶ意味が無い
                        var reach = new HashSet<int>();
                        var kinds = new HashSet<SquareKind>();
                        var grades = new HashSet<int>();
                        foreach (var p in open)
                        {
                            int end = p[p.Count - 1];
                            reach.Add(Trails.LeftFrom(trail, end));
                            var sq = trail.Squares[end];
                            // ⭐ 関門は**段まで**見る（段が違えば払う額も対価も違う）
                            kinds.Add(sq.Kind);
                            if (sq.Toll != null) grades.Add(sq.Toll.Grade);
                        }
                        if (reach.Count <= 1) litSame++;
                        // ⚠️ 距離も中身も同じなら、**まったくの無意味**
                        if (reach.Count <= 1 && kinds.Count <= 1 && grades.Count <= 1) litDead++;
                        if (kinds.Count > 1 || grades.Count > 1) litKind++;

                        var path = open[rng.Int(0, open.Count)];
                        int walked = path.Count - 1;
                        if (walked < raid.Pending)
                        {
                            shortMove++;
                            if (!trail.Squares[path[path.Count - 1]].IsGoal) shortNotGoal++;
                        }
                        Trails.Go(raid, path);
                    }
                }
                Console.WriteLine($"  段{tier}: マス {squares / boards,3} / 分かれ道 {100 * forks / squares,2}%"
                    + $" / 素通りの分かれ道 {100 * hubs / squares,2}%"
                    + $" / 平均の行き先 {(double)ways / squares,4:0.00}"
                    + $" ‖ 1回に光る先 {(double)lit / Math.Max(1, rolls),4:0.0}"
                    + $" / 選べない(1つだけ) {100 * oneWay / Math.Max(1, rolls),2}%"
                    + $" / 光った先が全部同じ距離 {100 * litSame / Math.Max(1, rolls),2}%"
                    + $" ‖ 出目より少なく進む {100 * shortMove / Math.Max(1, rolls),2}%"
                    + $"（うち卵でない {100 * shortNotGoal / Math.Max(1, rolls),2}%）");
                Console.WriteLine($"       盤の中身: 素通り {100 * plain / squares,2}% / 敵 {100 * mob / squares,2}%"
                    + $" / ▲ {100 * boon / squares,2}% / ▼ {100 * bane / squares,2}% / 関門 {100 * gate / squares,2}%"
                    + $" ‖ 光った先が距離も中身も同じ {100 * litDead / Math.Max(1, rolls),2}%"
                    + $" / 中身が違う {100 * litKind / Math.Max(1, rolls),2}%");
            }
        }

        private static string RunTrail(int seed, int tier, string name,
            Pick pick, int runs, Purse purse)
        {
            var rng = new Rng(seed).Stream($"trail:{tier}");
            var party = Steal.ReferenceParty(tier);
            int win = 0, stuck = 0, killed = 0, spent = 0, near = 0, far = 0, mobs = 0;
            // ⭐ **払って何をもらったか。**⚠️ 註に「+N回」と書くなら、まずここで測る
            //    （2026-08-21 ── 註の数が計算で埋められていて、実装とずれていた）
            int gotRolls = 0, gotHops = 0;
            for (int n = 0; n < runs; n++)
            {
                var raid = Trails.Begin(Trails.Make(rng, tier), party);
                var board = raid.Trail;
                // ⭐ **踏んだマスを数える。**⚠️ 分かれ道の記録（Took）だけを見ると、
                //    分かれ道でない関門を踏んでも数に入らない（2026-08-20 に 0.0 と出た）
                if (raid.Result == null) Play(rng, raid, pick, purse, path =>
                {
                    for (int k = 1; k < path.Count; k++)
                        if (board.Squares[path[k]].IsGate) far++;
                }, got =>
                {
                    if (got.Kind == GiftKind.Rolls) gotRolls += got.Amount;
                    else if (got.Kind == GiftKind.Hop) gotHops += got.Amount;
                });
                near += raid.Paid.Count;
                if (raid.Result == StealOutcome.Success) win++;
                // ⚠️ 「どの道も通れない」と「敵に負けた」を混ぜない。直す先が違う
                else if (raid.Result == StealOutcome.Blocked)
                { if (raid.Step == RaidStep.Caught && raid.Trail.Squares[raid.At].Kind == SquareKind.Mob) killed++; else stuck++; }
                else spent++;
                mobs += raid.Beaten.Count;
            }
            return $"{name,-26}{100.0 * win / runs,6:0}%{100.0 * stuck / runs,7:0}%"
                + $"{100.0 * killed / runs,8:0}%{100.0 * spent / runs,8:0}%"
                + $"{(double)near / runs,8:0.0}{(double)far / runs,8:0.0}{(double)mobs / runs,7:0.0}"
                + $"{(double)gotRolls / runs,7:0.0}{(double)gotHops / runs,7:0.0}";
        }

        /// <summary>1つの編成案。⭐ **ステの寄せ方と技を、狙いを持って組んだもの。**</summary>
        private sealed class Plan
        {
            public readonly string Name;
            public readonly string Aim;
            public readonly StatBlock[] Wild;
            public readonly string[] Skill2;
            public readonly string[] Skill3;

            public Plan(string name, string aim, StatBlock[] wild, string[] s2, string[] s3)
            {
                Name = name; Aim = aim; Wild = wild; Skill2 = s2; Skill3 = s3;
            }
        }

        /// <summary>⭐ **戦略編成は均等ステに勝てるか。**
        ///
        /// ⚠️ ここが「役割が死んでいる」の本丸。台帳の `sim roles` は
        /// **ステの寄せ方**だけを比べているが、この作品の技はもう十分に豊かなので、
        /// 「寄せたステ＋それを活かす技」を**狙って組んだ編成**が
        /// 均等に勝てるかどうかで測り直す。
        ///
        /// ⚠️ 交絡を全部止めてある:
        /// <list type="bullet">
        /// <item>種族を3体とも固定（枠1の技が編成ごとに変わらない）</item>
        /// <item>属性を枠ごとに固定し、**両側同じ**（3すくみが勝敗に入らない）</item>
        /// <item>得意・不得意と特性を外す（技とステだけを見る）</item>
        /// <item>素質は全編成とも1体120・1ステ40まで（総量が同じ）</item>
        /// <item>⭐ **`land` 乱数を毎回渡す** ── 渡さないと確率つき効果が1標本しか引かれない</item>
        /// </list></summary>
        private static void StrategyProbe(int seed, int members)
        {
            // ⚠️ 1体120・1ステ40 まで。どの案も総量は同じ
            StatBlock W(int hp, int atk, int def, int spd, int acc, int res) =>
                new StatBlock(hp, atk, def, spd, acc, res);

            var plans = new[]
            {
                new Plan("均等", "寄せない。台帳がいう最強",
                    new[] { W(20,20,20,20,20,20), W(20,20,20,20,20,20), W(20,20,20,20,20,20), W(20,20,20,20,20,20) },
                    new[] { "attack", "attack", "attack", "attack" },
                    new[] { "def-up", "def-up", "def-up", "def-up" }),

                new Plan("止め", "行動させない。仕留めは1体に任せる",
                    new[] { W(40,0,0,40,40,0), W(40,0,0,40,40,0), W(40,40,0,40,0,0), W(40,0,0,40,40,0) },
                    new[] { "stun-heavy", "gauge-drain", "attack-heavy", "sleep" },
                    new[] { "ct-lock", "block", "pierce-strike", "gauge-drain" }),

                new Plan("壁と挑発", "受け皿を1体に固定して、後ろから殴る",
                    new[] { W(40,0,40,0,0,40), W(40,0,40,0,0,40), W(40,40,0,40,0,0), W(40,0,40,0,0,40) },
                    new[] { "taunt-long", "bulwark", "attack-heavy", "taunt-long" },
                    new[] { "shield-wall", "guts-deep", "atk-up", "shield-wall" }),

                new Plan("毒と持久", "削って粘る。倒しきらない",
                    new[] { W(40,0,0,0,40,40), W(40,0,40,0,0,40), W(40,0,0,40,40,0), W(40,0,0,0,40,40) },
                    new[] { "venom-heavy", "regen-heavy", "curse", "poison-all" },
                    new[] { "poison-all", "heal-miracle", "slow-all", "regen-heavy" }),

                new Plan("速攻", "相手が動く前に終わらせる",
                    new[] { W(40,40,0,40,0,0), W(40,40,0,40,0,0), W(40,40,0,40,0,0), W(40,40,0,40,0,0) },
                    new[] { "dash", "ct-short", "attack-all-heavy", "dash" },
                    new[] { "attack-heavy", "pierce-strike", "atk-up", "attack-heavy" }),

                new Plan("剥がし", "相手の強化を消して殴る",
                    new[] { W(40,40,0,0,40,0), W(40,40,0,0,40,0), W(40,40,0,40,0,0), W(40,40,0,0,40,0) },
                    new[] { "strip-strike", "buff-steal", "attack-twice", "dispel" },
                    new[] { "dispel", "attack", "atk-down", "attack-twice" }),

                // ⭐ 台帳の `sim roles` が測っている形（攻撃役・壁役・弱化役）。
                // ⚠️ ただし素質は法内（1ステ40まで）にし、交絡は全部そろえてある
                new Plan("役割分担", "攻撃役・壁役・弱化役の3点セット",
                    new[] { W(40,40,0,40,0,0), W(40,0,40,0,0,40), W(40,0,0,40,40,0), W(40,0,20,0,20,40) },
                    new[] { "attack-heavy", "bulwark", "curse", "heal-big" },
                    new[] { "attack-twice", "harden", "slow-all", "regen-heavy" }),

                // ⭐ 速攻から**速度だけ**抜いた版。⚠️ 速攻の強さが速度か攻撃かを分ける
                new Plan("速攻-速度抜", "同じ技のまま、速度を防御に振り替えた",
                    new[] { W(40,40,40,0,0,0), W(40,40,40,0,0,0), W(40,40,40,0,0,0), W(40,40,40,0,0,0) },
                    new[] { "dash", "ct-short", "attack-all-heavy", "dash" },
                    new[] { "attack-heavy", "pierce-strike", "atk-up", "attack-heavy" }),

                // ══ 参考作品（R帯 60体）を写した編成 ═══════════════════════
                // ⚠️ **借りたのは「どの効果を、どう組み合わせるか」だけ。**
                //    名前も数値も持ってきていない（`参考/まもダン_全キャラスキル.md`）。
                // ⭐ 向こうの3体編成の型を、本作の技に置き換えて並べる。
                // ⚠️ ステの寄せ方は**向こうの実測に合わせて緩く**してある（下記）。

                // ⭐ 毒撒き（ゴブリン光・マッシュ水・ツボ光 の型）
                //    全体攻撃＋毒を重ね、削り切る前に相手を溶かす
                new Plan("R:毒撒き", "全体攻撃に毒を重ねて溶かす",
                    new[] { W(30,30,20,20,20,0), W(30,20,20,20,30,0), W(30,20,30,20,20,0), W(30,20,20,20,30,0) },
                    new[] { "poison-all", "venom-fang", "venom-heavy", "venom-heavy" },
                    new[] { "attack-all", "attack-all", "poison", "attack-all" }),

                // ⭐ 足止め（ハンター火・ゴブリン闇・マッシュ火 の型）
                //    速度弱化を全体に撒き、ゲージも削る
                new Plan("R:足止め", "速度弱化とゲージ削りで動かせない",
                    new[] { W(30,20,20,30,20,0), W(30,20,20,30,20,0), W(30,30,20,20,20,0), W(30,20,20,30,20,0) },
                    new[] { "slow-all", "gauge-drain", "spd-down", "slow-all" },
                    new[] { "attack-all", "attack", "attack-twice", "gauge-drain" }),

                // ⭐ 妨害（ミミック闇・オーク水・ハンター光 の型）
                //    CT延長で相手の大技を遅らせ続ける
                new Plan("R:CT縛り", "CT延長で相手の技を出させない",
                    new[] { W(30,20,20,20,30,0), W(30,20,20,20,30,0), W(30,30,20,20,20,0), W(30,20,20,20,30,0) },
                    new[] { "ct-long", "ct-lock", "atk-down", "ct-long" },
                    new[] { "attack", "attack", "attack-heavy", "attack" }),

                // ⭐ 防御参照（ゴブリン火・ハンター水・ひとくいばな闇 の型）
                //    防御を積んで、その防御で殴る
                new Plan("R:防御殴り", "防御を積んで、その防御で殴る",
                    new[] { W(30,0,40,20,0,30), W(30,0,40,20,0,30), W(30,0,40,20,0,30), W(30,0,40,20,0,30) },
                    new[] { "attack-def", "attack-def-twice", "attack-def", "attack-def-twice" },
                    new[] { "def-up", "harden", "attack-def-twice", "harden" }),

                // ⭐ 支援（ハンター風・マッシュ闇・ホネ火 の型）
                //    強化と回復で長く保たせる
                new Plan("R:支援", "強化と回復で保たせて押し切る",
                    new[] { W(40,20,20,0,0,40), W(30,20,30,20,0,20), W(30,30,20,20,20,0), W(30,20,30,20,0,20) },
                    new[] { "atk-up", "spd-up", "heal-big", "heal-big" },
                    new[] { "def-up", "regen-heavy", "attack-all", "regen-heavy" }),

                // ⭐ 混成（向こうの役割表どおり アタッカー＋デバッファー＋ヒーラー）
                // ⭐ 2026-08-20 に足した語彙（弱化解除 / 1手2役 / 切れない持続）だけで組んだ案。
                // ⚠️ 4体目の枠が空いたぶんを「返す手」に使うと強いのか、を測るために並べる。
                new Plan("新語彙", "解除で返し、1手2役で稼ぎ、構えで固める",
                    new[] { W(30,40,20,30,0,0), W(40,0,40,0,0,40), W(30,20,20,20,30,0), W(40,0,20,0,20,40) },
                    // ⚠️ sturdy はパッシブ ── この1体は枠を1つ潰して常時の底上げを買っている
                    new[] { "drain-all", "sturdy", "warcry", "cleanse" },
                    new[] { "attack-heavy", "taunt-long", "reckless", "heal-big" }),

                new Plan("R:混成", "アタッカー・デバッファー・ヒーラーの3点",
                    new[] { W(30,40,20,30,0,0), W(30,20,20,20,30,0), W(40,20,30,10,0,20), W(30,30,20,20,20,0) },
                    new[] { "attack-all", "slow-all", "heal-big", "attack-heavy" },
                    new[] { "attack-heavy", "poison-all", "def-up", "atk-up" }),

                // ══ 回収する側を持たせた対照（2026-08-20）═══════════════
                // ⚠️ 元の案と**ステは1つも変えない。**替えたのは技だけ ──
                //    「仕込みが沈んでいるのは回収が無いからか」を切り分けるため。

                new Plan("R:毒撒き+回収", "同じ毒編成に、弱化を数える技を持たせた",
                    new[] { W(30,30,20,20,20,0), W(30,20,20,20,30,0), W(30,20,30,20,20,0), W(30,20,20,20,30,0) },
                    new[] { "poison-all", "venom-fang", "chase-down", "venom-heavy" },
                    new[] { "sweep-down", "chase-down", "poison", "attack-all" }),

                new Plan("R:足止め+回収", "同じ足止め編成に、弱化を数える技を持たせた",
                    new[] { W(30,20,20,30,20,0), W(30,20,20,30,20,0), W(30,30,20,20,20,0), W(30,20,20,30,20,0) },
                    new[] { "slow-all", "gauge-drain", "chase-down", "slow-all" },
                    new[] { "sweep-down", "attack", "chase-down", "gauge-drain" }),

                new Plan("止め+回収", "同じ止め編成に、動けない相手を叩く技を持たせた",
                    new[] { W(40,0,0,40,40,0), W(40,0,0,40,40,0), W(40,40,0,40,0,0), W(40,0,0,40,40,0) },
                    new[] { "stun-heavy", "gauge-drain", "ambush-strike", "sleep" },
                    new[] { "ct-lock", "ambush-strike", "finisher", "gauge-drain" }),

                // ⭐ **対照: R:混成 から回復だけ抜いた版。**
                // ⚠️ 2026-08-20 の突き合わせで「回復が効いている疑い ── 未検証」と
                //    書いたまま残っていた問い。ステも技も1か所しか変えていない
                //    （ヒーラーの2枠を攻撃に差し替えただけ）。
                new Plan("R:混成-回復抜", "同じ3点セットから回復だけ攻撃に替えた",
                    new[] { W(30,40,20,30,0,0), W(30,20,20,20,30,0), W(40,20,30,10,0,20), W(30,30,20,20,20,0) },
                    new[] { "attack-all", "slow-all", "attack-heavy", "attack-heavy" },
                    new[] { "attack-heavy", "poison-all", "attack-twice", "atk-up" }),
            };

            const int Bouts = 240;

            Console.WriteLine();
            Console.WriteLine("■ 組んだ編成");
            foreach (var plan in plans)
                Console.WriteLine($"  {plan.Name,-8} {plan.Aim}");

            Console.WriteLine();
            Console.WriteLine($"■ 総当たり（{members}対{members}・1組 {Bouts} 戦・属性と種族と特性は両側そろえてある）");
            Console.Write($"  {"",-10}");
            foreach (var plan in plans) Console.Write($"{plan.Name,8}");
            Console.WriteLine($"{"総合",8}");

            var overall = new double[plans.Length];
            for (int a = 0; a < plans.Length; a++)
            {
                Console.Write($"  {plans[a].Name,-10}");
                int won = 0, played = 0;
                for (int b = 0; b < plans.Length; b++)
                {
                    if (a == b) { Console.Write($"{"—",8}"); continue; }
                    int wins = Duel(seed, plans[a], plans[b], Bouts, members: members);
                    won += wins; played += Bouts;
                    Console.Write($"{100.0 * wins / Bouts,7:0}%");
                }
                overall[a] = 100.0 * won / played;
                Console.Write($"{overall[a],7:0}%");
                Console.WriteLine();
            }

            Console.WriteLine();
            Console.WriteLine("■ ⭐ 均等ステに勝てるか（ここが本題）");
            Console.WriteLine($"  {"編成",-10}{"対 均等",10}{"総合",8}");
            for (int a = 1; a < plans.Length; a++)
                Console.WriteLine($"  {plans[a].Name,-10}"
                    + $"{100.0 * Duel(seed, plans[a], plans[0], Bouts, members: members) / Bouts,9:0}%"
                    + $"{overall[a],7:0}%");

            Console.WriteLine();
            Console.WriteLine("  ⚠️ どれも 50% を超えないなら、**技をどう組んでも寄せる意味が無い**");
            Console.WriteLine("  ⭐ 超えるものが在るなら、役割は死んでいるのではなく「作り方」の問題");

            // ══ 台帳の「役割が死んだ」を、同じ管理下で測り直す ══════
            // ⚠️ `sim roles` は (a) land 乱数を渡さない (b) 種族と属性を毎回引く
            //    (c) 特性と得意・不得意も引く (d) 素質が1ステ40の上限を超えている
            //    ⭐ ここでは全部そろえて、**役を1つ均等に置き換える**という同じ問いだけを測る
            Console.WriteLine();
            Console.WriteLine($"■ ⭐ 役を1つ均等に置き換えると弱くなるか（1組 {Bouts} 戦）");
            {
                var full = plans[6];                       // 役割分担
                var flat = W(20, 20, 20, 20, 20, 20);
                var names = new[] { "攻撃役", "壁役", "弱化役", "回復役" };
                Console.WriteLine($"  {"抜いた役",-12}{$"対 {members}役（land有）",16}{"落ち込み",10}{"land無し",12}{"落ち込み",10}");
                for (int drop = 0; drop < members; drop++)
                {
                    var wild = (StatBlock[])full.Wild.Clone();
                    var s2 = (string[])full.Skill2.Clone();
                    var s3 = (string[])full.Skill3.Clone();
                    wild[drop] = flat; s2[drop] = "attack"; s3[drop] = "def-up";
                    var missing = new Plan("欠け", "", wild, s2, s3);
                    double won = 100.0 * Duel(seed, missing, full, Bouts, members: members) / Bouts;
                    // ⚠️ 同じ測定を「land を渡さない」で並べる（既存 probe と同じ条件）
                    double blind = 100.0 * Duel(seed, missing, full, Bouts, land: false, members: members) / Bouts;
                    Console.WriteLine($"  {names[drop],-12}{won,13:0}%{won - 50.0,9:+0;-0}pt"
                        + $"{blind,12:0}%{blind - 50.0,9:+0;-0}pt");
                }
                Console.WriteLine();
                Console.WriteLine("  ⚠️ 50% を下回るほど、その役は**居ないと困る** ＝ 生きている");
                Console.WriteLine("  ⭐ 台帳は「抜いたほうが強い（＋側）」と記録している。ここで符号が逆なら測り方の問題");
            }

            Console.WriteLine();
            Console.WriteLine("■ 決着の付き方（均等どうしと比べる）");
            Console.WriteLine($"  {"組み合わせ",-20}{"行動数",9}{"引き分け",10}");
            foreach (var a in new[] { 0, 1, 2, 3, 4, 5 })
            {
                double acts = 0; int draws = 0;
                for (int i = 0; i < Bouts; i++)
                {
                    var fight = Bout(seed, i, plans[a], plans[0], members: members);
                    acts += fight.Actions;
                    if (fight.Result == null) draws++;
                }
                Console.WriteLine($"  {plans[a].Name + " 対 均等",-20}{acts / Bouts,8:0.0}"
                    + $"{100.0 * draws / Bouts,9:0}%");
            }
        }

        /// <summary>2つの案を戦わせて、先の案が勝った回数。</summary>
        private static int Duel(int seed, Plan mine, Plan yours, int bouts, bool land = true,
            int members = 3)
        {
            int won = 0;
            for (int i = 0; i < bouts; i++)
                if (Bout(seed, i, mine, yours, land, members).Result == Outcome.Ally) won++;
            return won;
        }

        /// <param name="land">確率つき効果の乱数を毎回変えるか。
        /// ⚠️ false にすると、既にある probe（roles / species）と同じ条件になる ──
        /// **全戦闘で同じ目**が出るので、確率つきの技は1標本しか引かれない。</param>
        private static Fight Bout(int seed, int round, Plan mine, Plan yours, bool land = true,
            int members = 3)
        {
            var rng = new Rng(seed + round).Stream("plan");
            int serial = 0;
            var draw = land ? new Rng(seed * 7919 + round).Stream("land") : null;
            return Run(Cast(rng, mine, ref serial, members), Cast(rng, yours, ref serial, members), draw);
        }

        /// <summary>案どおりの N 体を作る。⚠️ 交絡になるものは全部そろえる。</summary>
        /// <param name="members">⭐ 3 か 4。⚠️ <see cref="Battle"/> は体数を決め打ちしていない
        /// （<see cref="Battle.LoneScale"/> が体数の**比**で効くので、増やしても式は変わらない）。
        /// ⚠️ <see cref="Game.PartySize"/> は遊びの側の約束なので、ここでは見ない。</param>
        private static List<Creature> Cast(Rng rng, Plan plan, ref int serial, int members = 3)
        {
            // ⚠️ 種族を固定 ＝ 枠1の技を固定（attack / attack-def / attack-twice / attack）
            var species = new[] { "tsunoga", "tamaru", "haneru", "hirabe" };
            // ⚠️ 属性を枠ごとに固定。両側同じ並びなので 3すくみは勝敗に入らない。
            //    ⭐ 属性は3つしか無いので、4体目は1体目と同じ（両側そろっているので偏らない）
            var elements = new[] { Element.Fire, Element.Water, Element.Wood, Element.Fire };

            var party = new List<Creature>();
            for (int i = 0; i < members; i++)
            {
                var born = Born(rng, species[i], 5, ref serial, elements[i]);
                party.Add(new Creature(
                    born.Id, born.SpeciesId, plan.Wild[i], born.Trained, born.Earned,
                    born.MutationCounter, plan.Skill2[i], plan.Skill3[i], born.PaletteIndex,
                    born.ParentA, born.ParentB, born.Generation,
                    // ⚠️ 得意・不得意と特性は外す（技とステだけを見る）
                    null, null, elements[i], null));
            }
            return party;
        }

        /// <summary>⭐ **速度は本当に一番広いステか。**
        ///
        /// ⚠️ これを書いたのは、2026-08-20 の突き合わせで「速度の幅 6.60倍」と報告したのが
        /// **持ち幅（生の数）を測ったものでしかなかった**から。
        /// ⭐ 戦闘で効くのはゲージの速さで、そこには全員ぶんの下駄
        /// （<see cref="Battle.GaugeBase"/>）が乗っている ── 生の幅はそのまま効かない。
        /// ⚠️ 一方、潜入のさいころは速度の合計を割るだけなので**生の幅がそのまま効く**。
        /// ⭐ その2つを並べて出すのがこの道具の仕事。</summary>
        private static void SpeedProbe(int seed)
        {
            Console.WriteLine();
            Console.WriteLine("■ ステの持ち幅（種族の基礎値 ＋ 素質の上限）");
            Console.WriteLine("  ⚠️ 生の数の幅。⭐ これがそのまま効くとは限らない（下の表）");

            int lift = Stats.WildStatMax * Stats.Scale;
            var keys = new[] { StatKey.Hp, StatKey.Atk, StatKey.Def, StatKey.Spd };
            var raw = new Dictionary<StatKey, double>();
            Console.WriteLine();
            Console.WriteLine("  ステ    基礎の幅        素質込みの幅      倍率");
            foreach (var key in keys)
            {
                int lo = int.MaxValue, hi = int.MinValue;
                foreach (var sp in SpeciesTable.All)
                {
                    int v = sp.Base[key];
                    if (v < lo) lo = v;
                    if (v > hi) hi = v;
                }
                double ratio = (double)(hi + lift) / lo;
                raw[key] = ratio;
                Console.WriteLine($"  {Stats.LabelOf(key),-6} {lo,4}〜{hi,4}      {lo,4}〜{hi + lift,4}    {ratio,5:0.00}倍");
            }

            Console.WriteLine();
            Console.WriteLine("■ その幅が**戦闘**でどれだけ効くか（手番の回りやすさ）");
            Console.WriteLine($"  ⭐ ゲージは 1刻みで「{Battle.GaugeBase}（全員ぶんの下駄）＋ 速度」溜まる");
            {
                int lo = int.MaxValue, hi = int.MinValue;
                foreach (var sp in SpeciesTable.All)
                {
                    if (sp.Base.Spd < lo) lo = sp.Base.Spd;
                    if (sp.Base.Spd > hi) hi = sp.Base.Spd;
                }
                int fast = hi + lift;
                double rateRatio = (double)Battle.GaugeRate(fast) / Battle.GaugeRate(lo);
                Console.WriteLine($"  一番遅い 速度{lo,4} → 1刻み {Battle.GaugeRate(lo),4} ／ "
                    + $"満タンまで {Battle.TicksToAct(0, lo),3} 刻み");
                Console.WriteLine($"  一番速い 速度{fast,4} → 1刻み {Battle.GaugeRate(fast),4} ／ "
                    + $"満タンまで {Battle.TicksToAct(0, fast),3} 刻み");
                Console.WriteLine($"  ⭐ 手番の回りやすさの幅 = **{rateRatio:0.00}倍**"
                    + $"（生の幅 {raw[StatKey.Spd]:0.00}倍 に対して）");

                Console.WriteLine();
                Console.WriteLine("■ その幅が**潜入**でどれだけ効くか（さいころの数）");
                Console.WriteLine($"  ⭐ さいころ = 3体の速度合計 ÷ {Trail.SpeedPerRoll}");
                int slowRolls = Math.Max(1, lo * 3 / Trail.SpeedPerRoll);
                int fastRolls = Math.Max(1, fast * 3 / Trail.SpeedPerRoll);
                Console.WriteLine($"  一番遅い3体 合計{lo * 3,5} → {slowRolls,2}回");
                Console.WriteLine($"  一番速い3体 合計{fast * 3,5} → {fastRolls,2}回");
                Console.WriteLine($"  ⭐ さいころの幅 = **{(double)fastRolls / slowRolls:0.00}倍**"
                    + "（⚠️ 下駄が無いので生の幅に近い）");
            }

            Console.WriteLine();
            Console.WriteLine("■ 実測: 同じ技・同じ総量で、速度に振ったぶんだけ手番が増えるか");
            Console.WriteLine("  ⚠️ 属性・特性・得意不得意は止めてある。動かすのは素質の配り方だけ");

            var swings = new[]
            {
                ("速度 0", new StatBlock(40, 40, 40, 0, 0, 0)),
                // ⭐ **どこで勝敗が振り切れるか**を見るために、下のほうを細かく刻む
                ("速度 2", new StatBlock(40, 40, 38, 2, 0, 0)),
                ("速度 5", new StatBlock(40, 40, 35, 5, 0, 0)),
                ("速度10", new StatBlock(40, 40, 30, 10, 0, 0)),
                ("速度20", new StatBlock(40, 40, 20, 20, 0, 0)),
                ("速度30", new StatBlock(40, 40, 10, 30, 0, 0)),
                ("速度40", new StatBlock(40, 40, 0, 40, 0, 0)),
            };
            var skills2 = new[] { "attack-heavy", "attack-twice", "attack" };
            var skills3 = new[] { "atk-up", "attack", "def-up" };

            // ⚠️ **表と裏を同じ回数やる。**片側だけだと味方側の構造的な有利が混ざる
            //    （速度0 どうしで勝率 100% になり、測っているものが速度でなくなる）。
            const int Rounds = 240;
            var baseline = new Plan(swings[0].Item1, "",
                new[] { swings[0].Item2, swings[0].Item2, swings[0].Item2 }, skills2, skills3);

            Console.WriteLine();
            Console.WriteLine("  編成      速度合計   手番の取り分   速度0 に対する勝率");
            foreach (var (name, wild) in swings)
            {
                var plan = new Plan(name, "", new[] { wild, wild, wild }, skills2, skills3);
                int wins = 0, mine = 0, all = 0;
                for (int round = 0; round < Rounds / 2; round++)
                {
                    var front = Bout(seed, round, plan, baseline);
                    if (front.Result == Outcome.Ally) wins++;
                    mine += front.AllyActions; all += front.Actions;

                    var back = Bout(seed, round, baseline, plan);
                    if (back.Result == Outcome.Enemy) wins++;
                    mine += back.Actions - back.AllyActions; all += back.Actions;
                }
                int serial = 0;
                var party = Cast(new Rng(seed).Stream("plan"), plan, ref serial);
                int sum = 0;
                foreach (var c in party) sum += Creatures.StatsOf(c).Spd;
                Console.WriteLine($"  {name,-8} {sum,7}   {100.0 * mine / all,11:0.0}%   {100.0 * wins / Rounds,12:0}%");
            }
            Console.WriteLine("  ⚠️ 速度0 の行が『取り分 50%・勝率 50%』でなければ、この測り方が偏っている");
            Console.WriteLine();
            Console.WriteLine("  ⭐ **信用できるのは「手番の取り分」の列だけ。**");
            Console.WriteLine("  ⚠️ 勝率の列は読まないこと ── ほぼ同じ編成どうしなので、");
            Console.WriteLine("     素質2つぶんの差で 0% と 100% に振り切れる（速度の値打ちではなく");
            Console.WriteLine("     『ほぼ鏡の勝負は僅差で決まりきる』という別のことを測ってしまっている）。");
        }

        private static void LandProbe()
        {
            Console.WriteLine();
            Console.WriteLine("■ 命中と抵抗の差で、通る率がどれだけ動くか");

            var accs = new List<int>();
            var resists = new List<int>();
            var speeds = new List<int>();
            for (int tier = 1; tier <= 5; tier++)
                foreach (var c in Steal.ReferenceParty(tier))
                {
                    accs.Add(Creatures.StatsOf(c).Acc);
                    resists.Add(Creatures.StatsOf(c).Res);
                    speeds.Add(Creatures.StatsOf(c).Spd);
                }
            accs.Sort(); resists.Sort(); speeds.Sort();
            Console.WriteLine($"  想定編成の命中: 最小 {accs[0]} / 中央 {accs[accs.Count/2]} / 最大 {accs[accs.Count-1]}");
            Console.WriteLine($"  想定編成の抵抗: 最小 {resists[0]} / 中央 {resists[resists.Count/2]} / 最大 {resists[resists.Count-1]}");

            // ⚠️ **目盛りを手で書かない。**0/15/30/45 と書いてあったが、桁を 5倍にした日に
            //    置き去りになり、実際は 0〜300 の帯を「0〜45」で測っていた
            //    ── 表の数字が全部 0 に潰れて、差が見えなくなっていた（2026-08-19 の監査）。
            // ⭐ 想定編成で実際に出る帯から4点を引く。
            int lo = Math.Min(accs[0], resists[0]);
            int hi = Math.Max(accs[accs.Count - 1], resists[resists.Count - 1]);
            var axis = new int[4];
            for (int i = 0; i < axis.Length; i++)
                axis[i] = lo + (hi - lo) * i / (axis.Length - 1);

            Console.WriteLine();
            Console.WriteLine("  命中 / 抵抗  " + string.Join("  ", Array.ConvertAll(axis, v => $"{v,5}")));
            foreach (int mine in axis)
            {
                var row = new List<string>();
                foreach (int yours in axis)
                {
                    row.Add($"{(mine - yours) / Battle.LandStatDivisor,+5}");
                }
                Console.WriteLine($"  {mine,10}   " + string.Join("  ", row));
            }
            Console.WriteLine($"  ⭐ 表の数字が『素の率に足される %ポイント』（属性 ±{Battle.LandElementSwing} が別に乗る）");
            Console.WriteLine($"  ⚠️ 床 {Battle.LandFloor}% / 天井 {Battle.LandCeil}%");

            Console.WriteLine();
            Console.WriteLine("■ 強化が実時間でどれだけ保つか（3行動ぶんの強化）");
            Console.WriteLine("  ⭐ 速度を弱化の式から外したので、ここは『速さの取引』としてだけ残る");
            // ⭐ 速度の目盛りも実測の帯から引く（同じ理由）
            int slow = speeds[0];
            int fast = speeds[speeds.Count - 1];
            var spdAxis = new int[4];
            for (int i = 0; i < spdAxis.Length; i++)
                spdAxis[i] = slow + (fast - slow) * i / (spdAxis.Length - 1);
            foreach (int spd in spdAxis)
            {
                int rate = Battle.GaugeRate(spd);
                double perAction = (double)Battle.GaugeMax / rate;
                Console.WriteLine($"  速度 {spd,4}: 1行動 {perAction,5:0.0} 刻み → 3行動 {perAction*3,5:0.0} 刻み");
            }
        }

        /// <summary>盤を1枚作るのに何ミリ秒かかるか。⚠️ 画面に入るたび走るなら、ここが体感になる。</summary>
        private static void GenCost()
        {
            Console.WriteLine();
            Console.WriteLine("■ 盤を1枚作る費用（検査と振り直しを含む）");
            var clock = new System.Diagnostics.Stopwatch();
            for (int tier = 1; tier <= 5; tier++)
            {
                var line = new List<string>();
                for (int raids = 0; raids < Steal.RaidsToSeal; raids++)
                {
                    var nest = new Nest($"cost-t{tier}", "測定", "tamaru", tier);
                    clock.Restart();
                    int window;
                    Steal.MakeValidatedField(tier, FieldSide.Right, raids,
                        Steal.RngFor(nest, raids), out window);
                    clock.Stop();
                    line.Add($"{clock.ElapsedMilliseconds,5}ms({window,2}°)");
                }
                Console.WriteLine($"  段{tier}  " + string.Join("  ", line));
            }
            Console.WriteLine($"  ⚠️ 検査の下限は {Steal.MinWindowDegrees}度。括弧内がそれ未満なら検査に落ちたまま出している");
        }

        /// <summary>たくさんの種で盤を作り、**検査に落ちる出目がどれだけ出るか**を数える。
        ///
        /// ⚠️ 1つの種で通ったことは、ランダム化した生成が安全な証拠にならない。
        /// 本番で効くのは**珍しい悪い出目**のほうで、それは数を撃たないと出てこない。</summary>
        private static void GenStress()
        {
            const int Seeds = 120;
            Console.WriteLine();
            Console.WriteLine($"■ 生成の当たり外れ（巣を {Seeds} 通り × 段階 × 盗まれた回数）");

            int worstMs = 0;
            string worstWhere = "";
            for (int tier = 1; tier <= 5; tier++)
            {
                var cells = new List<string>();
                for (int raids = 0; raids < Steal.RaidsToSeal; raids++)
                {
                    int bad = 0, sumWindow = 0, minWindow = int.MaxValue;
                    var clock = new System.Diagnostics.Stopwatch();
                    for (int s = 0; s < Seeds; s++)
                    {
                        var nest = new Nest($"stress-{s}", "測定", "tamaru", tier);
                        var side = (s % 2 == 0) ? FieldSide.Left : FieldSide.Right;
                        clock.Restart();
                        int window;
                        Steal.MakeValidatedField(tier, side, raids,
                            Steal.RngFor(nest, raids), out window);
                        clock.Stop();
                        if (clock.ElapsedMilliseconds > worstMs)
                        {
                            worstMs = (int)clock.ElapsedMilliseconds;
                            worstWhere = $"段{tier} raids{raids} seed{s}";
                        }
                        if (window < Steal.MinWindowDegrees) bad++;
                        sumWindow += window;
                        if (window < minWindow) minWindow = window;
                    }
                    cells.Add($"落ち{bad,3}/{Seeds}(平均{sumWindow / Seeds,2}° 最低{minWindow,2}°)");
                }
                Console.WriteLine($"  段{tier}  " + string.Join("  ", cells));
            }
            Console.WriteLine($"  ⚠️ 一番遅かったのは {worstWhere} の {worstMs}ms");
            Console.WriteLine("  ⚠️ 「落ち」は検査に通らないまま出荷した数。0 でないなら生成が甘い");
        }

        /// <summary>力量ごとに探索へ何が出るか。⭐ **「序盤なのに強すぎる」の直接の確認。**</summary>
        private static void Ramp()
        {
            const int Samples = 3000;
            Console.WriteLine();
            Console.WriteLine("■ 力量ごとに探索へ出るもの（各{0}件）", Samples);
            Console.WriteLine("  力量 = 編成の Lv 平均。始めた直後はおよそ 24");

            foreach (int reach in new[] { 24, 38, 52, 66, 80 })
            {
                var tiers = new int[6];
                var species = new Dictionary<string, int>();
                var rng = new Rng(2026_08_17).Stream($"ramp-{reach}");
                for (int i = 0; i < Samples; i++)
                {
                    var e = Encounters.Make(rng, i, reach);
                    tiers[e.Nest.Tier]++;
                    Bump(species, e.Nest.SpeciesId);
                }
                var bars = new List<string>();
                for (int t = 1; t <= 5; t++) bars.Add($"段{t} {100.0 * tiers[t] / Samples,4:0}%");
                Console.WriteLine($"  力量{reach,3}  " + string.Join("  ", bars) + $"   種族 {species.Count}種");
            }
            Console.WriteLine("  ⚠️ 力量24（始めた直後）で段4・段5 が出ていたら栓が効いていない");
        }

        /// <summary>技ごとの成長表。⚠️ 導いた結果が読めるものになっているかを目で見る。</summary>
        private static void Growth()
        {
            Console.WriteLine();
            Console.WriteLine("■ スキルレベルで伸びるもの（Lv2 → Lv5）");
            Console.WriteLine($"  値段 {SkillCosts.CostOf(1)} / {SkillCosts.CostOf(2)} / "
                + $"{SkillCosts.CostOf(3)} / {SkillCosts.CostOf(4)}  "
                + $"卵 ★1={Rarities.PointsOf(1)} ★2={Rarities.PointsOf(2)} "
                + $"★3={Rarities.PointsOf(3)} ★4={Rarities.PointsOf(4)} ★5={Rarities.PointsOf(5)}");
            Console.WriteLine();
            foreach (var skill in Skills.All)
            {
                var g = Skills.GrowthOf(skill);
                var cells = new List<string>();
                foreach (var one in g) cells.Add($"{one,-6}");
                Console.WriteLine($"  {skill.Name,-8} CT{skill.Ct}  " + string.Join(" ", cells));
            }
        }

        private sealed class PartyShape
        {
            public readonly string Name;
            private readonly StatBlock[] _wild;

            /// <summary>⭐ 段ごとに組み直す編成（参照編成）を渡すとき用。null なら固定の型。</summary>
            private readonly Func<int, List<Creature>>? _byTier;

            public PartyShape(string name, params StatBlock[] wild)
            {
                Name = name;
                _wild = wild;
            }

            public PartyShape(string name, Func<int, List<Creature>> byTier)
            {
                Name = name;
                _wild = new StatBlock[0];
                _byTier = byTier;
            }

            public List<Creature> Party(int tier) =>
                _byTier != null ? _byTier(tier < 1 ? 1 : tier) : Party();

            /// <summary>⚠️ 種族は揃える。種族基礎が混ざると、測っているのが型か種族か分からなくなる。</summary>
            public List<Creature> Party()
            {
                var list = new List<Creature>();
                for (int i = 0; i < _wild.Length; i++)
                {
                    list.Add(new Creature($"p{i}", "tamaru", Stats.ApplyTotalCap(_wild[i]),
                        new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1));
                }
                return list;
            }
        }

        /// <summary>⭐ 参照編成を先頭に足す。⚠️ 盤は参照編成で解けることを検査してから出荷される。
        /// ここが × なら「解けるはずの盤が解けない」＝生成のバグ。</summary>
        private static PartyShape[] Prepend(PartyShape[] shapes)
        {
            var list = new List<PartyShape>
            {
                new PartyShape("参照編成（生成の検査に使う相手）", Steal.ReferenceParty),
            };
            list.AddRange(shapes);
            return list.ToArray();
        }

        /// <summary>得意・不得意が**素質の配りと独立して**引かれているかを数える。
        ///
        /// ⚠️ 種族や素質に引きずられていると、「素質は理想なのに得意が真逆」という
        /// 当たり外れが起きなくなる（＝厳選する理由が1つ減る）。
        /// ⭐ 独立なら、得意が一番高い素質に乗る率は **1/6 ≒ 16.7%** に落ち着く。</summary>
        /// <summary>ステ1点が勝率を何 pt 動かすか。**「どのステが強いか」ではなく「1点の価値」。**
        ///
        /// ⭐ 特性のときと同じ測り方 — 両側にまったく同じ編成を組み、片側の1ステだけを足す。
        /// 出る差はそのステのぶんだけになる。
        ///
        /// ⚠️ **式から手で出した効き目は当てにならない。**
        /// 弱化命中・弱化耐性は通る率に **%ポイントで足す**、攻撃・防御は **比で効く**、
        /// HP は最大HPに **3倍で乗る**。単位が違うものは、勝率という1つの物差しでしか比べられない。
        ///
        /// ⚠️ 1点あたりが揃っていないほど、育成の +1 の意味がステで食い違っている。</summary>
        private static void StatValue(int seed, int bumpOverride, int levelsOverride)
        {
            // ⚠️ 特性（400回）と同じ数を取る。数 pt しか動かないステがあり、
            //    少ないと種を変えるだけで符号が変わる。
            const int Samples = 400;
            // ⭐ 育成の上限（20）の半分。⚠️ 1点だけ足しても勝率の揺れに埋もれて測れない。
            int Bump = bumpOverride > 0 ? bumpOverride : 10;
            // ⭐ 育成の上限の半分。⚠️ 1レベルだけでは勝率の揺れに埋もれる
            int Levels = levelsOverride > 0 ? levelsOverride : 10;

            int total = Stats.WildTotalMax;
            int high = total * 3 / 8;
            int low = total / 8;
            var full = new[]
            {
                new Role("攻撃役", new StatBlock(low, high, low, high), "attack-heavy", "attack-twice"),
                new Role("壁役", new StatBlock(high, low, high, low), "bulwark", "harden"),
                new Role("弱化役", new StatBlock(high, low, low, high), "curse", "slow-all"),
            };

            Console.WriteLine();
            Console.WriteLine($"■ ステ1点の価値（各{Samples}回・片側の3体だけ +{Bump}）");
            Console.WriteLine("  ⭐ 両側は同じ組み合わせ。足したステのぶんだけが差になる");
            Console.WriteLine();

            double basePct = BumpedWinRate(seed, full, null, 0, Samples);
            Console.WriteLine($"  足さない（基準）        {basePct,5:0.0}%");
            Console.WriteLine();

            foreach (var key in Stats.Keys)
            {
                double pct = BumpedWinRate(seed, full, key, Bump, Samples);
                double gain = pct - basePct;
                Console.WriteLine($"  {Stats.LabelOf(key),-8} +{Bump}   {pct,5:0.0}%"
                    + $"   基準から {gain,5:0.0}pt   1点あたり {gain / Bump,5:0.00}pt");
            }

            Console.WriteLine();
            Console.WriteLine("  ⚠️ 1点あたりが揃っていないほど、育成の +1 の意味がステで食い違っている");

            // ⭐ **こちらが本番の物差し。**遊びで配られるのは「1点」ではなく「1レベル」で、
            //    1レベルの伸びはステごとに違う（素質 × 割合）。揃っているべきはこちら。
            Console.WriteLine();
            Console.WriteLine($"■ 1レベルぶんの価値（各{Samples}回・片側の3体だけ {Levels}レベル）");
            Console.WriteLine();
            foreach (var key in Stats.Keys)
            {
                double pct = LeveledWinRate(seed, full, key, Levels, Samples);
                double gain = pct - basePct;
                string how = Creatures.GrowthPermilOf(key) > 0
                    ? $"素質の{Creatures.GrowthPermilOf(key) / 10.0,4:0.0}%"
                    : $"平らに ＋{Creatures.GrowthFlatOf(key)}   ";
                Console.WriteLine($"  {Stats.LabelOf(key),-8} {how}"
                    + $"   {pct,5:0.0}%   基準から {gain,5:0.0}pt   1レベル {gain / Levels,5:0.00}pt");
            }
            Console.WriteLine();
            Console.WriteLine("  ⚠️ ここが揃っていないと、レベルを上げたときの手応えがステで食い違う");
        }

        /// <summary>片側の1ステだけを「N レベルぶん」伸ばしたときの勝率。
        /// ⭐ 伸ばし方は本番と同じ <see cref="Creatures.TrainedFor"/> を通す。</summary>
        private static double LeveledWinRate(int seed, Role[] roles, StatKey key, int levels, int samples)
        {
            int won = 0;
            for (int i = 0; i < samples; i++)
            {
                var rng = new Rng(seed + i).Stream("statvalue");
                var land = new Rng(seed + i).Stream("land-statvalue");
                int serial = 0;
                var mine = Leveled(Shaped(rng, roles, ref serial), key, levels);
                var theirs = Shaped(rng, roles, ref serial);
                if (Run(mine, theirs, land).Result == Outcome.Ally) won++;
            }
            return samples == 0 ? 0.0 : 100.0 * won / samples;
        }

        /// <summary>1つのステだけを N レベルぶん伸ばした編成。</summary>
        private static List<Creature> Leveled(List<Creature> party, StatKey key, int levels)
        {
            var made = new List<Creature>();
            foreach (var c in party)
            {
                int grown = Creatures.TrainedFor(c.SpeciesId, c.Wild, levels)[key];
                made.Add(Rebuilt(c, c.Trained.With(key, c.Trained[key] + grown)));
            }
            return made;
        }

        /// <summary>片側の1ステだけを足したときの勝率。
        /// ⚠️ 種と tag を足す前後で必ず同じにする（違う列を引くと編成の差を測ってしまう）。</summary>
        private static double BumpedWinRate(int seed, Role[] roles, StatKey? key, int amount, int samples)
        {
            int won = 0;
            for (int i = 0; i < samples; i++)
            {
                var rng = new Rng(seed + i).Stream("statvalue");
                var land = new Rng(seed + i).Stream("land-statvalue");
                int serial = 0;
                var mine = Shaped(rng, roles, ref serial);
                var theirs = Shaped(rng, roles, ref serial);
                if (key.HasValue) mine = Boosted(mine, key.Value, amount);
                if (Run(mine, theirs, land).Result == Outcome.Ally) won++;
            }
            return samples == 0 ? 0.0 : 100.0 * won / samples;
        }

        /// <summary>1つのステだけを足した編成。
        /// ⭐ 育てた分に足すので、実値の作り方（得意・不得意の ±15% 込み）は本番と同じ。</summary>
        private static List<Creature> Boosted(List<Creature> party, StatKey key, int amount)
        {
            var made = new List<Creature>();
            foreach (var c in party) made.Add(Rebuilt(c, c.Trained.With(key, c.Trained[key] + amount)));
            return made;
        }

        /// <summary>育てた分だけ差し替えた同じ個体。⚠️ 欄は書き換えず作り直す。</summary>
        private static Creature Rebuilt(Creature c, StatBlock trained) => new Creature(
            c.Id, c.SpeciesId, c.Wild, trained, c.Earned,
            c.MutationCounter, c.Skill2, c.Skill3, c.PaletteIndex,
            c.ParentA, c.ParentB, c.Generation, c.Strong, c.Weak,
            c.Element, c.TraitId);

        /// <summary>技1つの効き目。⭐ **特性と同じ物差し（勝率の差）で並べる。**
        ///
        /// ⚠️ `sim skills` は「AI がどれだけ選ぶか（採用率）」で、**強さではない**。
        /// よく選ばれる技が強いとは限らないし、選ばれない技が弱いとも限らない
        /// （AI の採点が古いだけ、ということが実際にあった ── 2026-08-19 の23技）。
        ///
        /// ⭐ 測り方は特性と同じ: まったく同じ編成を2つ並べ、**片側の枠2だけ**を差し替える。
        /// 相手の枠2は基準の技（<see cref="SkillValueControl"/>）のまま。
        /// つまり出る数は **「その枠に基準の技を入れる代わりにこれを入れたら何 pt 動くか」**。
        ///
        /// ⚠️ **場面を選ぶ技は低く出る**（蘇生は味方が倒れていないと働かない、
        /// 挑発は殴られないと働かない）。低い＝弱いではなく、**この測り方では出ない**だけ。
        /// ⭐ それでも並べる価値はある ── 「どの技も同じくらい」が理想なので、
        /// 突出しているものと沈んでいるものが見える。</summary>
        private static void SkillValue(int seed)
        {
            const int Samples = 400;
            const int Tier = 5;

            Console.WriteLine();
            Console.WriteLine($"■ 技1つの効き目（段階{Tier}・各{Samples}回・属性は両側そろえる）");
            Console.WriteLine($"  枠3 を「{Skills.ById(SkillValueControl).Name}」から差し替えたときの勝率の伸び");
            Console.WriteLine($"  ⚠️ 枠1＝種族の通常攻撃 / 枠2＝{Skills.ById(SkillValueFiller).Name}（両側そろえて固定）");
            Console.WriteLine("  ⭐ pt ＝ 勝率の**差**。⚠️ 誤差は ±2.5pt 程度");
            Console.WriteLine("  ⚠️ 場面を選ぶ技（蘇生・挑発など）は、この測り方では低く出る");
            Console.WriteLine();

            double baseAll = SkillWinRate(seed, "skillvalue",
                SkillValueControl, SkillValueControl, Samples, Tier, false);
            double baseOne = SkillWinRate(seed, "skillvalue-one",
                SkillValueControl, SkillValueControl, Samples, Tier, true);
            Console.WriteLine($"  基準どうし   3体とも {baseAll,5:0.0}%   1体だけ {baseOne,5:0.0}%");
            Console.WriteLine();
            Console.WriteLine($"  {"技",-14}  {"3体とも",8}  {"1体だけ",8}");

            var rows = new List<Row>();
            foreach (var skill in Skills.All)
            {
                if (skill.Id == SkillValueControl) continue;
                rows.Add(new Row
                {
                    Name = skill.Name,
                    All = SkillWinRate(seed, "skillvalue", skill.Id, SkillValueControl,
                        Samples, Tier, false) - baseAll,
                    One = SkillWinRate(seed, "skillvalue-one", skill.Id, SkillValueControl,
                        Samples, Tier, true) - baseOne,
                });
            }
            rows.Sort((a, b) => b.One.CompareTo(a.One));

            foreach (var row in rows)
            {
                Console.WriteLine($"  {row.Name,-14}  {row.All,6:0.0}pt  {row.One,6:0.0}pt");
            }
            Console.WriteLine();
            Console.WriteLine("  ⚠️ **1体だけの列を信じる。**3体とも持たせると、重ねて効く技（全体攻撃・");
            Console.WriteLine("     回復）が3倍になり、重ならない技（強化・挑発）が余る ── 技の差ではなく");
            Console.WriteLine("     「重ねられるか」を測ってしまう。⭐ 並びは1体だけの列の順");
        }

        /// <summary>差し替えの基準にする技。⭐ 一番あたりまえの一撃。
        /// ⚠️ ここを強い技にすると全部が負の数で並び、読みにくくなる。</summary>
        private const string SkillValueControl = "attack";

        /// <summary>枠2 を埋める技。⭐ どの編成も持っている「もう1発の攻撃」。
        /// ⚠️ ここを空にすると、測っているのが技の強さではなく「攻撃の数」になる。</summary>
        private const string SkillValueFiller = "attack-heavy";

        /// <summary>片側の枠2 だけを差し替えたときの勝率。
        /// ⚠️ 種と tag を差し替えの前後で必ず同じにする（違う列を引くと編成の差を測ってしまう）。</summary>
        private sealed class Row
        {
            public string Name = string.Empty;
            public double All;
            public double One;
        }

        /// <param name="onlyFirst">⭐ true なら**先頭の1体だけ**が差し替わる。
        /// ⚠️ 3体とも差し替えると、重ねて効く技だけが不当に高く出る。</param>
        private static double SkillWinRate(int seed, string tag, string mine, string theirs,
            int samples, int tier, bool onlyFirst)
        {
            int won = 0;
            for (int i = 0; i < samples; i++)
            {
                var rng = new Rng(seed + i).Stream(tag);
                var land = new Rng(seed + i).Stream($"land-{tag}");
                int serial = 0;
                // ⚠️ **枠2 は両側とも本物の攻撃で埋める。**空にしていたとき、
                //    支援・弱化の技が軒並み −15pt 前後で並んだ ── あれは技の弱さではなく
                //    「攻撃が1つ減る」ことの重さを測っていた（枠1 と合わせて手が2つしか無い編成）。
                //    ⭐ 埋めておけば「3つ目の枠に、もう1発の攻撃か・それ以外か」という
                //    実際の選択と同じ形になる。
                var fight = Run(
                    SkillParty(rng, mine, onlyFirst, tier, ref serial),
                    SkillParty(rng, theirs, onlyFirst, tier, ref serial),
                    land);
                if (fight.Result == Outcome.Ally) won++;
            }
            return samples == 0 ? 0.0 : 100.0 * won / samples;
        }

        /// <summary>枠3 だけを差し替えた編成。⚠️ 枠2 は両側とも <see cref="SkillValueFiller"/>。</summary>
        private static List<Creature> SkillParty(Rng rng, string slot3, bool onlyFirst,
            int tier, ref int serial)
        {
            var party = new List<Creature>();
            var ids = new List<string>();
            foreach (var sp in SpeciesTable.All) ids.Add(sp.Id);

            for (int i = 0; i < Games.PartySize; i++)
            {
                string speciesId = ids[rng.Int(0, ids.Count)];
                var born = Born(rng, speciesId, tier, ref serial, Element.Fire);
                // ⭐ 1体だけの回では、2体目以降は基準の技のまま
                string mine = !onlyFirst || i == 0 ? slot3 : SkillValueControl;
                party.Add(new Creature(
                    born.Id, born.SpeciesId, born.Wild, born.Trained, born.Earned,
                    born.MutationCounter, SkillValueFiller, mine, born.PaletteIndex,
                    born.ParentA, born.ParentB, born.Generation, born.Strong, born.Weak,
                    born.Element, null));
            }
            return party;
        }

        // ══ 1手あたりの価値（算数）═══════════════════════════
        //
        // ⭐ **中身は `Core.SkillValues` へ移した**（2026-08-27）。
        //    ⚠️ ★で技を引くにはゲーム本体が格を知る必要があり、Core から Sim は見えない。
        // ⭐ ここに残すのは**並べて印字する側**だけ（`sim turnvalue`）。
        private sealed class TurnRow
        {
            public string Name = string.Empty;
            public double Value;
            public bool Guessed;
            public string Why = string.Empty;
            public int Ct;
        }

        /// <summary>1手を使って何手ぶんを生むか。⭐ 基準は「枠1 の一撃 ＝ 1.0」。</summary>
        private static void TurnValue()
        {
            var mid = SkillValues.Middle();
            int atk = mid.Atk, def = mid.Def;
            int maxHp = mid.Hp * Battle.HpScale;
            int one = Battle.DamageOf(Skills.DamagePowerOf(PowerTier.Medium), atk, def, 1.0);

            Console.WriteLine();
            Console.WriteLine("■ 1手で何手ぶんを生むか（算数・AI を通さない）");
            Console.WriteLine($"  代表の個体: HP {mid.Hp}（最大HP {maxHp:N0}）/ 攻撃 {atk} / 防御 {def}");
            Console.WriteLine($"  基準: 枠1 の一撃 ＝ {one:N0} ダメージ ＝ 最大HP の {100.0 * one / maxHp:0.0}% ＝ **1.0手ぶん**");
            Console.WriteLine();
            Console.WriteLine("  ⭐ 1.0 を超えれば「枠1 で殴るより得」、下回れば「殴ったほうが得」");
            Console.WriteLine("  ⚠️ 「見積」印は文脈で変わるもの ── 算数ではなく勘です");
            Console.WriteLine($"  ⚠️ 後で効くもの（毒・回復・強化・弱化）は ×{SkillValues.LateDiscount} 割り引いています");
            Console.WriteLine("     ── 使い切る前に決着し、相手の手番も奪えないため（`sim delivered` 実測）");
            Console.WriteLine();

            // ⚠️ **一覧も TurnValueOf を通す。**別々に足していた頃、
            //    味方全体の掛け算がこちらだけ抜けていた（2026-08-19 の監査）。
            var rows = new List<TurnRow>();
            foreach (var skill in Skills.All)
            {
                double total = SkillValues.Of(skill, out string why);
                rows.Add(new TurnRow
                {
                    Name = skill.Name,
                    Value = total,
                    Guessed = why.StartsWith("見積"),
                    Why = why.StartsWith("見積 ") ? why.Substring(3) : why,
                    Ct = skill.Ct,
                });
            }
            rows.Sort((a, b) => b.Value.CompareTo(a.Value));

            Console.WriteLine($"  {"技",-14}{"手ぶん",8}{"CT",4}  内訳");
            foreach (var r in rows)
            {
                string mark = r.Guessed ? "見積" : "    ";
                Console.WriteLine($"  {r.Name,-14}{r.Value,8:0.00}{r.Ct,4}  {mark} {r.Why}");
            }
            Console.WriteLine();
            Console.WriteLine("  ⚠️ CT は「1戦闘に何回撃てるか」を決めるだけで、1回ぶんの価値には効きません");
            Console.WriteLine($"  ⭐ 1体が動けるのはおよそ {SkillValues.PaceTurns}手（`sim pace`）。CT5 なら1戦闘に1回");
        }

        /// <summary>代表の個体。⭐ **種族の基礎値の平均 ＋ 野生を均等に配ったぶん**（育成なし）。
        /// ⚠️ ここを1つに保たないと、`turnvalue` と帳面の検査が別の相手を測ることになる。</summary>
        /// <summary>算数の見積もりが、実戦で本当に入っているか。
        ///
        /// ⭐ **算数とシミュレーションが食い違ったところを掘る道具**（2026-08-19）。
        /// 毒は算数で 1.44手ぶん（枠1 の1.4倍）なのに、勝率で測ると −0.1pt だった。
        /// ⚠️ どちらが嘘かではなく、**間に何が挟まっているか**を見る。</summary>
        private static void Delivered(int seed)
        {
            const int Battles = 200;
            const int Tier = 5;

            var ids = new List<string>();
            foreach (var sp in SpeciesTable.All) ids.Add(sp.Id);

            int cast = 0, ticks = 0, poisonSum = 0, hitSum = 0, hits = 0;
            int diedWithPoison = 0, poisonLeft = 0;

            for (int i = 0; i < Battles; i++)
            {
                var rng = new Rng(seed + i).Stream("delivered");
                var land = new Rng(seed + i).Stream("land-delivered");
                int serial = 0;
                // ⭐ 両側に毒を持たせる（片側だけだと勝ち負けの偏りが混ざる）
                var state = Battle.CreateBattle(
                    TraitParty(rng, "poison", null, null, Tier, ref serial),
                    TraitParty(rng, "poison", null, null, Tier, ref serial),
                    land);

                int read = 0;
                while (state.Result == null && state.Actions < Battle.MaxActions)
                {
                    var actor = Battle.NextActor(state);
                    if (actor == null) break;
                    Battle.PerformAction(state, actor, Ai.ChooseAction(state, actor));

                    for (; read < state.Log.Count; read++)
                    {
                        var e = state.Log[read];
                        if (e.Kind == BattleEventKind.Applied && e.Label != null
                            && e.Label.StartsWith("毒")) cast++;
                        else if (e.Kind == BattleEventKind.Poison) { ticks++; poisonSum += e.Amount; }
                        else if (e.Kind == BattleEventKind.Damage) { hits++; hitSum += e.Amount; }
                    }
                }

                // ⚠️ 終わった時点で毒が残っている ＝ **捨てられたぶん**
                foreach (var unit in state.Units)
                {
                    if (unit.Status.Poison.Turns <= 0) continue;
                    poisonLeft += unit.Status.Poison.Turns;
                    if (!Battle.IsAlive(unit)) diedWithPoison++;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"■ 毒が実戦でどれだけ入っているか（{Battles}戦・両側が毒持ち）");
            Console.WriteLine();
            Console.WriteLine($"  毒を撃った回数        {cast,8}");
            Console.WriteLine($"  実際に削れた回数      {ticks,8}   （1回撃つと平均 {(cast == 0 ? 0 : (double)ticks / cast),4:0.0} 回）");
            Console.WriteLine($"  ⚠️ 表の持続は4ターン ── ここが4より小さければ**使い切れていない**");
            Console.WriteLine();
            Console.WriteLine($"  毒で入った合計        {poisonSum,8:N0}");
            Console.WriteLine($"  直接の一撃 平均       {(hits == 0 ? 0 : hitSum / hits),8:N0}   （{hits} 発）");
            double perCast = cast == 0 ? 0 : (double)poisonSum / cast;
            double one = hits == 0 ? 1 : (double)hitSum / hits;
            Console.WriteLine($"  1回の毒で入った量     {perCast,8:N0}   ＝ 一撃の {perCast / one,4:0.00} 倍");
            Console.WriteLine();
            Console.WriteLine($"  ⚠️ 使い切る前に終わった毒（残ターンの合計） {poisonLeft}");
            Console.WriteLine($"     うち相手が倒れて消えたもの               {diedWithPoison} 体ぶん");
            Console.WriteLine();
            Console.WriteLine("  ⭐ 算数の見積もりは 1.44倍（最大HPの20% ÷ 一撃）。");
            Console.WriteLine("     実測がこれを大きく下回るなら、**持続を使い切れていない**のが原因。");
        }

        private static void SlantProbe(int seed)
        {
            const int Samples = 4000;
            var rng = new Rng(seed).Stream("slantprobe");
            int n = Stats.Keys.Length;

            // ⚠️ **4本ぜんぶ数える**（2026-08-21）。⭐ 大得意・大不得意を足したのに
            //    道具が2本しか見ていないと、偏った引き方をしていても「異常なし」と出る。
            var bestCount = new int[n];
            var strongCount = new int[n];
            var weakCount = new int[n];
            var worstCount = new int[n];
            int strongOnTop = 0, weakOnTop = 0, strongOnBottom = 0;
            int bestOnTop = 0, worstOnTop = 0;
            int overlap = 0;
            // ⚠️ **増減の pt は ±0 でも、実値の合計は動く**（ステごとに桁が違うため）。
            //    ⭐ どれだけ動くかを数で押さえる ── 「濃くなっただけ」と言い切れるか確かめる。
            double sumFlat = 0, sumTwo = 0, sumFour = 0;
            // 種族ごとの「得意がどのステに乗ったか」。⚠️ 種族で偏るならここに出る
            var perSpecies = new Dictionary<string, int[]>();
            foreach (var species in SpeciesTable.All) perSpecies[species.Id] = new int[n];

            for (int i = 0; i < Samples; i++)
            {
                // ⭐ 出荷の経路を通す（MakeEgg → Hatch）。組み立て直すと偏りが測定から消える
                var nest = Nests.All[rng.Int(0, Nests.All.Length)];
                var egg = Nests.MakeEgg(rng, nest, EggOrigin.Stolen, i + 1);
                StatKey best, strong, weak, worst;
                Nests.RollSlant(rng, out best, out strong, out weak, out worst);
                var creature = Nests.Hatch(rng, egg, $"p{i}", strong, weak, best, worst);

                bestCount[(int)best]++;
                strongCount[(int)strong]++;
                weakCount[(int)weak]++;
                worstCount[(int)worst]++;
                perSpecies[creature.SpeciesId][(int)strong]++;
                // ⚠️ 4本が別のステになっているか。⭐ 重なると Slanted が両方とも捨てる
                var picked = new HashSet<StatKey> { best, strong, weak, worst };
                if (picked.Count != 4) overlap++;

                var flat = Creatures.BornStatsOf(creature.SpeciesId, creature.Wild);
                sumFlat += Stats.TotalOf(flat);
                sumTwo += Stats.TotalOf(Creatures.Slanted(flat, strong, weak));
                sumFour += Stats.TotalOf(Creatures.Slanted(flat, strong, weak, best, worst));

                // 一番高い素質・一番低い素質
                var top = Stats.Keys[0];
                var bottom = Stats.Keys[0];
                foreach (var key in Stats.Keys)
                {
                    if (creature.Wild[key] > creature.Wild[top]) top = key;
                    if (creature.Wild[key] < creature.Wild[bottom]) bottom = key;
                }
                if (strong == top) strongOnTop++;
                if (weak == top) weakOnTop++;
                if (strong == bottom) strongOnBottom++;
                if (best == top) bestOnTop++;
                if (worst == top) worstOnTop++;
            }

            Console.WriteLine();
            Console.WriteLine($"■ 偏り4本の引かれ方（{Samples} 体・出荷の経路で孵化）");
            Console.WriteLine($"  ⭐ 独立なら どれも 1/{n} ≒ {100.0 / n:0.0}%");
            Console.WriteLine();
            SlantRow("  大得意の行き先  ", bestCount);
            SlantRow("  得意の行き先    ", strongCount);
            SlantRow("  不得意の行き先  ", weakCount);
            SlantRow("  大不得意の行き先", worstCount);
            Console.WriteLine();
            Console.WriteLine($"  4本が重なった個体               {100.0 * overlap / Samples,5:0.0}%  ⚠️ 0% でないと軸が消える");
            Console.WriteLine($"  大得意が**一番高い素質**に乗った {100.0 * bestOnTop / Samples,5:0.0}%  ⭐ 当たり");
            Console.WriteLine($"  得意が**一番高い素質**に乗った   {100.0 * strongOnTop / Samples,5:0.0}%  ⭐ 噛み合った個体");
            Console.WriteLine($"  不得意が**一番高い素質**に乗った {100.0 * weakOnTop / Samples,5:0.0}%  ⚠️ 真逆の個体");
            Console.WriteLine($"  大不得意が**一番高い素質**に乗った{100.0 * worstOnTop / Samples,4:0.0}%  ⚠️ 一番の外れ");
            Console.WriteLine($"  得意が**一番低い素質**に乗った   {100.0 * strongOnBottom / Samples,5:0.0}%  ⚠️ 無駄になった個体");
            Console.WriteLine();
            Console.WriteLine("  実値の合計（育てる前）はどれだけ動くか");
            Console.WriteLine($"    偏り無し {sumFlat / Samples,8:0.0}");
            Console.WriteLine($"    2本      {sumTwo / Samples,8:0.0}  ({100.0 * (sumTwo - sumFlat) / sumFlat,+5:0.0}%)");
            Console.WriteLine($"    4本      {sumFour / Samples,8:0.0}  ({100.0 * (sumFour - sumFlat) / sumFlat,+5:0.0}%)");
            Console.WriteLine("  ⚠️ 0% から離れるなら、偏りは「濃さ」ではなく**強さ**を足している");
            Console.WriteLine();
            Console.WriteLine("  種族ごとの得意の行き先（⚠️ 種族で偏るならここが揃わない）");
            foreach (var species in SpeciesTable.All)
            {
                var row = perSpecies[species.Id];
                int total = 0;
                foreach (var v in row) total += v;
                if (total == 0) continue;
                Console.Write($"    {species.Name,-6}({total,4}体) ");
                for (int i = 0; i < n; i++) Console.Write($"{100.0 * row[i] / total,5:0.0}% ");
                Console.WriteLine();
            }
        }

        /// <summary>1本ぶんの行き先を1行で出す。⚠️ 4本ぶん書き写さない。</summary>
        private static void SlantRow(string label, int[] count)
        {
            int total = 0;
            foreach (int v in count) total += v;
            if (total == 0) { Console.WriteLine(label + "  （0体）"); return; }
            Console.Write(label + "");
            for (int i = 0; i < Stats.Keys.Length; i++)
                Console.Write($"{Stats.LabelOf(Stats.Keys[i])} {100.0 * count[i] / total,5:0.0}%  ");
            Console.WriteLine();
        }

        // ── テンポ ──────────────────────────────────────

        private static void Pace(int seed)
        {
            const int Battles = 200;
            var ids = new List<string>();
            foreach (var s in SpeciesTable.All) ids.Add(s.Id);

            int actions = 0, draws = 0, longest = 0;
            var bySlot = new int[3];
            var locked = new int[3];
            for (int i = 0; i < Battles; i++)
            {
                var rng = new Rng(seed + i).Stream("pace");
                int serial = 0;
                int tier = rng.Int(1, 6);
                var fight = Run(
                    Party(rng, ids[rng.Int(0, ids.Count)], tier, ref serial),
                    Party(rng, ids[rng.Int(0, ids.Count)], tier, ref serial));
                actions += fight.Actions;
                if (fight.Result == Outcome.Draw) draws++;
                if (fight.Actions > longest) longest = fight.Actions;
                for (int k = 0; k < 3; k++) { bySlot[k] += fight.BySlot[k]; locked[k] += fight.Locked[k]; }
            }

            Console.WriteLine();
            Console.WriteLine($"■ 決着まで（{Battles}戦）");
            Console.WriteLine($"  平均 {(double)actions / Battles,5:0.0} 行動 / 最長 {longest} / 引き分け {draws}");
            Console.WriteLine("  ⚠️ 引き分けが出るなら、決め手が無い組み合わせがある");

            // ⭐ **「枠1ばかり撃っている」を数字にする。**
            // ⚠️ 枠1 は種族の通常攻撃で CT 0 ── いつでも撃てる。
            //    枠2・3 が待ちで塞がっている間、選べる手はこれだけになる。
            int chosen = bySlot[0] + bySlot[1] + bySlot[2];
            Console.WriteLine();
            Console.WriteLine("■ どの枠を撃っているか");
            for (int k = 0; k < 3; k++)
            {
                string extra = k == 0
                    ? "（種族の通常攻撃・CT 0）"
                    : $"  手番が来たとき待ちで塞がっていた回数 {locked[k]}";
                Console.WriteLine($"  枠{k + 1}  {bySlot[k],6} 回  {100.0 * bySlot[k] / chosen,5:0.0}%{extra}");
            }
            int lockedAll = locked[1] + locked[2];
            Console.WriteLine($"  ⚠️ 枠2・3 が塞がっていた割合 {100.0 * lockedAll / (chosen * 2),5:0.0}%"
                + "（手番 × 2枠 に対して）");
        }
    }
}
