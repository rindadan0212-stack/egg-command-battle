#nullable enable
using System;
using System.Collections.Generic;
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
    /// </summary>
    public static class Program
    {
        private const int DefaultSeed = 2026_08_16;

        public static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            string what = args.Length > 0 ? args[0] : "all";
            int seed = DefaultSeed;
            for (int i = 1; i < args.Length - 1; i++)
            {
                if (args[i] == "--seed") int.TryParse(args[i + 1], out seed);
            }

            // ⚠️ 中身が繋がっていない状態で測っても意味が無い。先に数える
            Content.Audit();

            switch (what)
            {
                case "species": Species(seed); break;
                case "skills": SkillCensus(seed); break;
                case "elements": Elements(seed); break;
                case "roles": Roles(seed); break;
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
                case "pace": Pace(seed); break;
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

                int slot = Ai.ChooseAction(state, actor);
                var skill = Battle.SkillAt(actor, slot);
                if (skill != null) Bump(fight.Chosen, skill.Id);

                Battle.PerformAction(state, actor, slot);
                fight.Actions++;
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
        /// どれかが全対戦で勝ち越していたら、その種族を選ぶのが最適解になり、
        /// 他の種族が飾りになる（＝組み合わせが生まれない）。</summary>
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
            Console.WriteLine("      総合");

            foreach (var a in ids)
            {
                Console.Write($"{a,12}");
                int wonAll = 0, playedAll = 0;
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
                    if (a != b) { wonAll += won; playedAll += Samples; }
                }
                Console.WriteLine($"{Pct(wonAll, playedAll),10}");
            }

            Console.WriteLine("  ⚠️ 総合が 35〜65% から外れた種族は、選ぶ理由か避ける理由が固定されている");
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
            };
            // ⚠️ 対照。どの特性とも噛み合わない組み合わせ（単発の一撃 + 自己強化）どうし
            const string Dull2 = "attack-heavy";
            const string Dull3 = "def-up";

            Console.WriteLine();
            Console.WriteLine($"■ 特性の効き目（段階{Tier}・各{Samples}回・属性は両側そろえる）");
            Console.WriteLine("  まったく同じ編成どうしで、片側にだけ特性を足したときの勝率の伸び");

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
            // ⚠️ 素質は合計80まで。1ステ上限40
            var shapes = new[]
            {
                new PartyShape("速度ぞろい",
                    new StatBlock(10, 10, 20, 40), new StatBlock(10, 10, 20, 40), new StatBlock(10, 10, 20, 40)),
                new PartyShape("均等ぞろい",
                    new StatBlock(20, 20, 20, 20), new StatBlock(20, 20, 20, 20), new StatBlock(20, 20, 20, 20)),
                new PartyShape("役割分担",
                    new StatBlock(0, 40, 0, 40), new StatBlock(40, 0, 40, 0), new StatBlock(10, 10, 20, 40)),
                new PartyShape("耐久ぞろい",
                    new StatBlock(40, 0, 40, 0), new StatBlock(40, 0, 40, 0), new StatBlock(40, 0, 40, 0)),
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

            foreach (var shape in shapes)
            {
                var party = shape.Party();
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
                        bool ok = Steal.FindRoomySolution(field, party, Samples,
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

            public PartyShape(string name, params StatBlock[] wild)
            {
                Name = name;
                _wild = wild;
            }

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

        // ── テンポ ──────────────────────────────────────

        private static void Pace(int seed)
        {
            const int Battles = 200;
            var ids = new List<string>();
            foreach (var s in SpeciesTable.All) ids.Add(s.Id);

            int actions = 0, draws = 0, longest = 0;
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
            }

            Console.WriteLine();
            Console.WriteLine($"■ 決着まで（{Battles}戦）");
            Console.WriteLine($"  平均 {(double)actions / Battles,5:0.0} 行動 / 最長 {longest} / 引き分け {draws}");
            Console.WriteLine("  ⚠️ 引き分けが出るなら、決め手が無い組み合わせがある");
        }
    }
}
