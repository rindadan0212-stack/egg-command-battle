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
    ///   dotnet run --project EggCommand.Sim -- pace      決着までの行動数
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
                case "pace": Pace(seed); break;
                case "all":
                    Species(seed); SkillCensus(seed); Elements(seed); Roles(seed); Pace(seed);
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

        private static Fight Run(IReadOnlyList<Creature> allies, IReadOnlyList<Creature> foes)
        {
            var fight = new Fight();
            var state = Battle.CreateBattle(allies, foes);

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
                    born.Element));
            }
            return party;
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
