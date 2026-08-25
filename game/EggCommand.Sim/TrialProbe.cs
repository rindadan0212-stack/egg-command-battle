#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using EggCommand.Core;

namespace EggCommand.Sim
{
    /// <summary>試練の重さを測る。⭐ **手で組んだ相手は、手では測れない。**
    ///
    /// ⚠️ 「かみ合った編成を高難易度で」と決めても、実際に何%で負けるかは
    /// 回してみるまで分からない。⭐ ここが無いと「難しいつもり」で置くことになる
    /// （巣の親を全段 100% で通していたのと同じ穴）。
    ///
    /// ⚠️ 物差しは <see cref="Steal.ReferenceParty"/>（段5・育て切り）。
    /// ⭐ 「その時点で普通に組める、そこそこ強い編成」を表す ── これで勝てないなら、
    /// 噛み合わせを考えて組み直さないと勝てない、という意味になる。</summary>
    public static class TrialProbe
    {
        private const int Runs = 200;

        public static void Run(int seed)
        {
            Console.WriteLine();
            Console.WriteLine($"■ 試練の重さ（参照編成・段5・育て切り × {Runs}回）");
            Console.WriteLine("  段  名前              勝つ    手数   残す");
            foreach (var trial in Trials.All)
            {
                int won = 0, actions = 0, left = 0;
                for (int i = 0; i < Runs; i++)
                {
                    var rng = new Rng(seed + i).Stream("trial-land");
                    var state = Battle.CreateBattle(
                        Steal.ReferenceParty(5), Trials.PartyOf(trial), rng);
                    int steps = 0;
                    while (state.Result == null && steps < Battle.MaxActions)
                    {
                        var actor = Battle.NextActor(state);
                        if (actor == null) break;
                        Battle.PerformAction(state, actor, Ai.ChooseAction(state, actor));
                        steps++;
                    }
                    actions += steps;
                    if (state.Result == Outcome.Ally) won++;
                    foreach (var unit in state.Units)
                        if (unit.Side == Side.Ally && Battle.IsAlive(unit)) left++;
                }
                Console.WriteLine($"  {Trials.StepOf(trial.Id),2}  {trial.Name,-14}"
                    + $"{100.0 * won / Runs,5:0}%  {(double)actions / Runs,6:0}  "
                    + $"{(double)left / Runs,4:0.0}体");
            }
            Console.WriteLine("  ⚠️ 全段 0% なら**組み替えても勝てない**かもしれない（噛み合わせで越えられるか要確認）");
            Console.WriteLine("  ⚠️ 段1が 80% を超えるなら、入口としても軽すぎる");

            Console.WriteLine();
            Console.WriteLine("■ 相手の実値（育て切ったあと・HP は戦闘で使う数）");
            foreach (var trial in Trials.All)
            {
                Console.WriteLine($"  ── 段{Trials.StepOf(trial.Id)} {trial.Name}：{trial.Gist}");
                foreach (var foe in Trials.PartyOf(trial))
                {
                    var species = Creatures.SpeciesOf(foe);
                    var stats = Creatures.StatsOf(foe);
                    var trait = Creatures.TraitOf(foe);
                    var skills = Creatures.SkillsOf(foe);
                    var names = new List<string>();
                    foreach (var skill in skills) names.Add(skill == null ? "—" : skill.Name);
                    Console.WriteLine(
                        $"     {species.Name,-4} {SpeciesTable.LabelOf(foe.Element)}"
                        + $" 変異{foe.MutationCounter,2}"
                        + $"  HP{stats.Hp * Battle.HpScale,8:#,0} 攻{stats.Atk,4} 防{stats.Def,4}"
                        + $" 速{stats.Spd,4} 命{stats.Acc,4} 抵{stats.Res,4}"
                        + $"  {(trait == null ? "—" : trait.Name),-6} {string.Join(" / ", names)}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("■ こちら（参照編成・段5）");
            foreach (var one in Steal.ReferenceParty(5))
            {
                var stats = Creatures.StatsOf(one);
                Console.WriteLine(
                    $"     {Creatures.SpeciesOf(one).Name,-4} {SpeciesTable.LabelOf(one.Element)}"
                    + $"  HP{stats.Hp * Battle.HpScale,8:#,0} 攻{stats.Atk,4} 防{stats.Def,4}"
                    + $" 速{stats.Spd,4} 命{stats.Acc,4} 抵{stats.Res,4}");
            }

            Console.WriteLine();
            Console.WriteLine($"■ ⭐ **答えがあるか**（変異20・育て切りの編成 × {Runs}回）");
            Console.WriteLine("  ⚠️ 素質を天井まで積んだだけの編成（力任せ）と、その段を狙って組んだ編成を並べる。");
            Console.WriteLine("  ⭐ 狙って組んだほうが勝てるなら、その段は**壁ではなく試練**になっている。");
            Console.WriteLine();
            Console.WriteLine("  段  名前              中堅(変異10)  天井(変異20)  狙って組む");
            foreach (var trial in Trials.All)
            {
                double mid = WinRate(seed, Mid(), trial);
                double brute = WinRate(seed, Brute(), trial);
                double aimed = WinRate(seed, Answer(trial.Id), trial);
                Console.WriteLine($"  {Trials.StepOf(trial.Id),2}  {trial.Name,-14}"
                    + $"{100.0 * mid,8:0}%      {100.0 * brute,6:0}%      {100.0 * aimed,6:0}%");
            }
            Console.WriteLine("  ⚠️ 差が出ない段は、噛み合わせではなく**数**で決まっている（作り直しの合図）");

            var problems = Trials.Faults();
            Console.WriteLine();
            Console.WriteLine(problems.Count == 0
                ? "  ⭐ 表の不備は 0 件"
                : "  ⚠️ 表の不備 " + problems.Count + " 件:\n    " + string.Join("\n    ", problems));
        }

        /// <summary>その編成でその段に何回勝つか。</summary>
        private static double WinRate(int seed, List<Creature> party, Trial trial)
        {
            int won = 0;
            for (int i = 0; i < Runs; i++)
            {
                var rng = new Rng(seed + i).Stream("trial-answer");
                var state = Battle.CreateBattle(party, Trials.PartyOf(trial), rng);
                int steps = 0;
                while (state.Result == null && steps < Battle.MaxActions)
                {
                    var actor = Battle.NextActor(state);
                    if (actor == null) break;
                    Battle.PerformAction(state, actor, Ai.ChooseAction(state, actor));
                    steps++;
                }
                if (state.Result == Outcome.Ally) won++;
            }
            return (double)won / Runs;
        }

        /// <summary>⭐ 手で組んだ編成を、試練とまったく同じ道で作る
        /// （<see cref="Trials.PartyOf"/> を通す ── 別の作り方をすると測定が本番からずれる）。</summary>
        private static List<Creature> Party(params TrialFoe[] foes) =>
            Trials.PartyOf(new Trial("probe", "測定用", "測定用", foes));

        private const int Top = 20;                       // 変異の天井
        private const int Cap = Stats.WildStatMax + Top;  // 1ステの上限（60）

        private const int Half = 10;                       // 変異が半分
        private const int MidCap = Stats.WildStatMax + Half; // 1ステの上限（50）

        /// <summary>⭐ **中堅。**何代か配合を重ねた頃の編成（変異は半分）。
        /// ⚠️ 段が上がるほど落ちていく形が見えるのは、この物差しだけ
        /// （参照編成は段2で 0 に張り付き、天井編成は段4まで 100 で張り付く）。</summary>
        private static List<Creature> Mid() => Party(
            new TrialFoe("tsunoga", "attack-heavy", "attack-all", Element.Fire,
                new StatBlock(MidCap, MidCap, MidCap, 0, 0, 0), Half,
                StatKey.Atk, StatKey.Hp, StatKey.Spd, StatKey.Acc),
            new TrialFoe("iwao", "attack-heavy", "def-up", Element.Water,
                new StatBlock(MidCap, MidCap, MidCap, 0, 0, 0), Half,
                StatKey.Atk, StatKey.Def, StatKey.Spd, StatKey.Acc),
            new TrialFoe("nobiru", "attack-thrice", "spd-up", Element.Wood,
                new StatBlock(MidCap, MidCap, 0, MidCap, 0, 0), Half,
                StatKey.Atk, StatKey.Spd, StatKey.Def, StatKey.Acc),
            new TrialFoe("hirabe", "heal-big", "shield", Element.Fire,
                new StatBlock(MidCap, 0, MidCap, 0, 0, MidCap), Half,
                StatKey.Hp, StatKey.Def, StatKey.Atk, StatKey.Acc));

        /// <summary>⚠️ **力任せ。**素質だけ天井まで積んで、噛み合わせは考えない編成。
        /// ⭐ これで勝ててしまう段は、噛み合わせを問うていない。</summary>
        private static List<Creature> Brute() => Party(
            new TrialFoe("tsunoga", "attack-heavy", "attack-all", Element.Fire,
                new StatBlock(Cap, Cap, Cap, 0, 0, 0), Top,
                StatKey.Atk, StatKey.Hp, StatKey.Spd, StatKey.Acc),
            new TrialFoe("iwao", "attack-heavy", "def-up", Element.Water,
                new StatBlock(Cap, Cap, Cap, 0, 0, 0), Top,
                StatKey.Atk, StatKey.Def, StatKey.Spd, StatKey.Acc),
            new TrialFoe("nobiru", "attack-thrice", "spd-up", Element.Wood,
                new StatBlock(Cap, Cap, 0, Cap, 0, 0), Top,
                StatKey.Atk, StatKey.Spd, StatKey.Def, StatKey.Acc),
            new TrialFoe("hirabe", "heal-big", "shield", Element.Fire,
                new StatBlock(Cap, 0, Cap, 0, 0, Cap), Top,
                StatKey.Hp, StatKey.Def, StatKey.Atk, StatKey.Acc));

        /// <summary>⭐ **その段を狙って組んだ編成。**
        /// ⚠️ 完璧な答えではなく「自分ならこう組む」という1案
        /// ── 差が出るかどうかだけを見る道具。</summary>
        private static List<Creature> Answer(string trialId)
        {
            switch (trialId)
            {
                // 段1 弱化を撒いてくる → 抵抗を厚く＋落とす手を持つ
                case "bane": return Party(
                    new TrialFoe("hirabe", "rally", "cleanse-all", Element.Fire,
                        new StatBlock(Cap, 0, 30, 30, 0, Cap), Top,
                        StatKey.Res, StatKey.Hp, StatKey.Atk, StatKey.Acc),
                    new TrialFoe("tsunoga", "attack-heavy", "attack-all", Element.Water,
                        new StatBlock(30, Cap, 0, 30, 0, Cap), Top,
                        StatKey.Atk, StatKey.Res, StatKey.Def, StatKey.Acc),
                    new TrialFoe("iwao", "pierce-strike-heavy", "def-up", Element.Fire,
                        new StatBlock(Cap, Cap, 30, 0, 0, 30), Top,
                        StatKey.Atk, StatKey.Hp, StatKey.Spd, StatKey.Acc),
                    new TrialFoe("nobiru", "attack-thrice", "spd-up", Element.Water,
                        new StatBlock(30, Cap, 0, Cap, 0, 30), Top,
                        StatKey.Atk, StatKey.Spd, StatKey.Def, StatKey.Acc));

                // 段2 止めてくる → 免疫を先に配る＋抵抗
                case "halt": return Party(
                    new TrialFoe("hirabe", "immune-long", "rally", Element.Water,
                        new StatBlock(Cap, 0, 30, Cap, 0, 30), Top,
                        StatKey.Spd, StatKey.Res, StatKey.Atk, StatKey.Acc),
                    new TrialFoe("marumi", "immune", "dispel", Element.Fire,
                        new StatBlock(Cap, 0, 30, Cap, 0, 30), Top,
                        StatKey.Spd, StatKey.Res, StatKey.Atk, StatKey.Acc),
                    new TrialFoe("tsunoga", "attack-heavy", "attack-all", Element.Water,
                        new StatBlock(30, Cap, 0, 30, 0, Cap), Top,
                        StatKey.Atk, StatKey.Res, StatKey.Def, StatKey.Acc),
                    new TrialFoe("iwao", "pierce-strike-heavy", "attack-all-twice", Element.Wood,
                        new StatBlock(Cap, Cap, 30, 0, 0, 30), Top,
                        StatKey.Atk, StatKey.Hp, StatKey.Spd, StatKey.Acc));

                // 段3 倒れない → 最大HPを割る／強化を剥がす／回復を止める
                case "wall": return Party(
                    new TrialFoe("togeru", "life-cut", "curse", Element.Wood,
                        new StatBlock(30, 30, 0, 30, Cap, 0), Top,
                        StatKey.Acc, StatKey.Atk, StatKey.Def, StatKey.Res),
                    new TrialFoe("marumi", "block", "dispel", Element.Fire,
                        new StatBlock(Cap, 0, 30, 30, Cap, 0), Top,
                        StatKey.Acc, StatKey.Hp, StatKey.Atk, StatKey.Res),
                    new TrialFoe("kibane", "strip-strike", "pierce-strike", Element.Wood,
                        new StatBlock(30, Cap, 0, 30, Cap, 0), Top,
                        StatKey.Atk, StatKey.Acc, StatKey.Def, StatKey.Res),
                    new TrialFoe("iwao", "pierce-strike-heavy", "attack-all-twice", Element.Fire,
                        new StatBlock(Cap, Cap, 30, 0, 0, 30), Top,
                        StatKey.Atk, StatKey.Hp, StatKey.Spd, StatKey.Acc));

                // 段4 倒すほど速くなる → 面で削って**同時に**落とす
                case "wake": return Party(
                    new TrialFoe("iwao", "attack-all-twice", "pierce-strike-heavy", Element.Water,
                        new StatBlock(Cap, Cap, 30, 0, 0, 30), Top,
                        StatKey.Atk, StatKey.Hp, StatKey.Spd, StatKey.Acc),
                    new TrialFoe("kibane", "sweep-down", "poison-all", Element.Water,
                        new StatBlock(30, Cap, 0, 30, Cap, 0), Top,
                        StatKey.Atk, StatKey.Acc, StatKey.Def, StatKey.Res),
                    new TrialFoe("tsunoga", "attack-all", "attack-heavy", Element.Wood,
                        new StatBlock(30, Cap, 30, 30, 0, 0), Top,
                        StatKey.Atk, StatKey.Hp, StatKey.Def, StatKey.Acc),
                    new TrialFoe("marumi", "block", "dispel", Element.Fire,
                        new StatBlock(Cap, 0, 30, 30, Cap, 0), Top,
                        StatKey.Acc, StatKey.Hp, StatKey.Atk, StatKey.Res));

                // 段5 全部入り → 免疫＋割合削り＋剥がし＋面
                default: return Party(
                    new TrialFoe("hirabe", "immune-long", "rally", Element.Water,
                        new StatBlock(Cap, 0, 30, 30, 0, Cap), Top,
                        StatKey.Res, StatKey.Hp, StatKey.Atk, StatKey.Acc),
                    new TrialFoe("togeru", "life-cut", "curse", Element.Water,
                        new StatBlock(30, 30, 0, 30, Cap, 0), Top,
                        StatKey.Acc, StatKey.Atk, StatKey.Def, StatKey.Res),
                    new TrialFoe("marumi", "block", "dispel", Element.Fire,
                        new StatBlock(Cap, 0, 30, 30, Cap, 0), Top,
                        StatKey.Acc, StatKey.Hp, StatKey.Atk, StatKey.Res),
                    new TrialFoe("iwao", "pierce-strike-heavy", "attack-all-twice", Element.Wood,
                        new StatBlock(Cap, Cap, 30, 0, 0, 30), Top,
                        StatKey.Atk, StatKey.Hp, StatKey.Spd, StatKey.Acc));
            }
        }
    }
}
