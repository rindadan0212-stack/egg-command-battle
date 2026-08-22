#nullable enable
using System;
using System.Collections.Generic;
using EggCommand.Core;

namespace EggCommand.Sim
{
    /// <summary>**親との戦い**を測る。⭐ 巣から最後の卵を得る唯一の道。
    ///
    /// ⚠️ **2026-08-21 まで、親戦を測る道具が1つも無かった。**
    /// `sim species` の総合にヌシが混ざっているだけで、
    /// 「参照編成で挑んだら何％勝つか」は誰も見ていなかった
    /// （[討論] ── 「測る道具が見ていない」の4つめ）。
    ///
    /// ⚠️ 親は **1体**、こちらは <see cref="Games.PartySize"/> 体。
    /// ⭐ 3体だった頃の釣り合いのまま4体にすると、こちらの手数だけが 1.33倍になる。
    /// 「比例する数」（素質の総量・関門の値段）は4体化のときに直したが、
    /// **体数の比そのもの**が効く場所は直していない ── ここがそれ。</summary>
    public static class BossProbe
    {
        /// <summary>1つの巣に、参照編成で何度も挑む。</summary>
        private static (double Win, double Turns) Fight(int seed, Nest nest, int runs, int members,
            int trainPercent = 0, bool rollElement = false)
        {
            int won = 0;
            long turns = 0;
            for (int n = 0; n < runs; n++)
            {
                var rng = new Rng(seed + n).Stream($"boss:{nest.Id}");
                var land = new Rng(seed + n).Stream($"boss-land:{nest.Id}");
                var party = Steal.ReferenceParty(nest.Tier);
                // ⭐ 体数を変えて測れるようにする（4体化の効きを見るため）
                while (party.Count > members) party.RemoveAt(party.Count - 1);
                // ⚠️ **遊びの中では属性を引く**（`Games.DefendersOf`）。
                //    ⭐ 固定で測ると、3すくみが当たりっぱなし／外れっぱなしになる
                var boss = rollElement
                    ? Nests.MakeDefenders(rng, nest, SpeciesTable.Roll(rng))
                    : Nests.MakeDefenders(rng, nest);
                // ⭐ **親も育ててみる。**⚠️ 遊びでは親は素のまま（trained = 0）なので、
                //    ここは「育てたら関門になるか」を見るためだけの手
                if (trainPercent > 0)
                    Creatures.Grow(boss[0], Creatures.TrainMax * trainPercent / 100);

                var state = Battle.CreateBattle(party, boss, land);
                int acts = 0;
                while (state.Result == null && acts < Battle.MaxActions)
                {
                    var actor = Battle.NextActor(state);
                    if (actor == null) break;
                    Battle.PerformAction(state, actor, Ai.ChooseAction(state, actor));
                    acts++;
                }
                turns += acts;
                if (state.Result == Outcome.Ally) won++;
            }
            return (100.0 * won / runs, (double)turns / runs);
        }

        public static void Run(int seed)
        {
            const int Runs = 200;

            Console.WriteLine();
            Console.WriteLine($"■ 親との戦い（参照編成 × {Runs}回）");
            Console.WriteLine($"  {"巣",-14}{"段",4}{"勝つ",8}{"手数",8}");
            foreach (var nest in Nests.All)
            {
                var got = Fight(seed, nest, Runs, Games.PartySize);
                Console.WriteLine($"  {nest.Id,-14}{nest.Tier,4}{got.Win,7:0}%{got.Turns,8:0}");
            }
            Console.WriteLine("  ⚠️ 100% が並ぶなら、親は**関門ですらない**（勝てる相手しか居ない）");

            Console.WriteLine();
            Console.WriteLine("■ 体数を変えると（段5の巣・同じ親）");
            Console.WriteLine($"  {"体数",6}{"勝つ",8}{"手数",8}");
            Nest deep = Nests.All[0];
            foreach (var nest in Nests.All) if (nest.Tier > deep.Tier) deep = nest;
            for (int members = 1; members <= Games.PartySize; members++)
            {
                var got = Fight(seed, deep, Runs, members);
                Console.WriteLine($"  {members,6}{got.Win,7:0}%{got.Turns,8:0}");
            }
            Console.WriteLine("  ⚠️ 3体と4体で差が出ないなら、**体数を増やした意味が親戦に無い**");
            Console.WriteLine("  ⭐ 3体でも 100% なら、4体化より前から親が軽い");

            Console.WriteLine();
            Console.WriteLine($"■ ⭐ ヌシ（ホームから挑む方・`Nests.MakeBossParty`）× {Runs}回");
            Console.WriteLine($"  {"想定編成の段",-14}{"勝つ",8}{"手数",8}");
            for (int tier = 1; tier <= 5; tier++)
            {
                int won = 0;
                long acts = 0;
                for (int n = 0; n < Runs; n++)
                {
                    var land = new Rng(seed + n).Stream("nushi-land");
                    var state = Battle.CreateBattle(
                        Steal.ReferenceParty(tier), Nests.MakeBossParty(), land);
                    int a = 0;
                    while (state.Result == null && a < Battle.MaxActions)
                    {
                        var actor = Battle.NextActor(state);
                        if (actor == null) break;
                        Battle.PerformAction(state, actor, Ai.ChooseAction(state, actor));
                        a++;
                    }
                    acts += a;
                    if (state.Result == Outcome.Ally) won++;
                }
                Console.WriteLine($"  {"段" + tier,-14}{100.0 * won / Runs,7:0}%{(double)acts / Runs,8:0}");
            }
            Console.WriteLine("  ⚠️ ヌシに勝っても、いまは**何も起きない**"
                + "（`App.FinishBattle` は `GrowParty` だけして巣一覧へ戻る）");

            Console.WriteLine();
            Console.WriteLine("■ ⚠️ 属性を遊びと同じに引いたら（親の属性を毎回抽選）");
            Console.WriteLine($"  {"巣",-14}{"固定",8}{"引く",8}");
            foreach (var nest in Nests.All)
                Console.WriteLine($"  {nest.Id,-14}"
                    + $"{Fight(seed, nest, Runs, Games.PartySize).Win,7:0}%"
                    + $"{Fight(seed, nest, Runs, Games.PartySize, 0, true).Win,7:0}%");
            Console.WriteLine("  ⚠️ 差が出るなら、上の数字は属性の固定に引きずられている");

            Console.WriteLine();
            Console.WriteLine("■ ⭐ 親も育てたら関門になるか（4体で挑む・同じ巣）");
            Console.Write($"  {"巣",-14}{"素(いま)",10}");
            foreach (int pct in new[] { 50, 100, 150, 200 }) Console.Write($"{"育" + pct + "%",9}");
            Console.WriteLine();
            foreach (var nest in Nests.All)
            {
                Console.Write($"  {nest.Id,-14}{Fight(seed, nest, Runs, Games.PartySize).Win,9:0}%");
                foreach (int pct in new[] { 50, 100, 150, 200 })
                    Console.Write($"{Fight(seed, nest, Runs, Games.PartySize, pct).Win,8:0}%");
                Console.WriteLine();
            }
            Console.WriteLine("  ⚠️ 遊びの中の親は **育てた分 0**（`trained = (0,0,0,0)`）。");
            Console.WriteLine("  ⭐ こちらは `Creatures.Grow(..., TrainMax)` で**育て切って**いる"
                + " ── 育成の伸びが親に1点も乗らない");

            // ⚠️ **「参照編成が強すぎるだけ」ではないかを先に潰す。**
            //    ⭐ 100% の原因がステの差なのか、手数の差なのかを分ける
            Console.WriteLine();
            Console.WriteLine("■ 親1体と、こちら1体を並べる（素のステ）");
            Console.WriteLine($"  {"段",4}{"",10}{"HP",10}{"攻",8}{"防",8}{"速",8}{"技",6}");
            foreach (var nest in Nests.All)
            {
                var rng = new Rng(seed).Stream($"boss:{nest.Id}");
                var boss = Nests.MakeDefenders(rng, nest)[0];
                var mine = Steal.ReferenceParty(nest.Tier)[0];
                Row(nest.Tier, "親", boss);
                Row(nest.Tier, "こちら", mine);
                var b = Creatures.StatsOf(boss);
                var m = Creatures.StatsOf(mine);
                // ⭐ 編成ぜんぶで見た比。⚠️ 親は1体なので、ここが本当の重さ
                var pool = Trails.PoolOf(Steal.ReferenceParty(nest.Tier));
                Console.WriteLine($"  {"",4}{"編成合計/親",-12}"
                    + $"HP {(double)pool.Hp / b.Hp,4:0.0}倍  攻 {(double)pool.Atk / b.Atk,4:0.0}倍"
                    + $"  防 {(double)pool.Def / b.Def,4:0.0}倍  速 {(double)pool.Spd / b.Spd,4:0.0}倍");
            }
            Console.WriteLine("  ⚠️ 親は**1体**。編成合計との比がそのまま「削り合いの傾き」になる");
        }

        private static void Row(int tier, string who, Creature c)
        {
            var st = Creatures.StatsOf(c);
            int skills = 0;
            if (!string.IsNullOrEmpty(c.Skill2)) skills++;
            if (!string.IsNullOrEmpty(c.Skill3)) skills++;
            Console.WriteLine($"  {tier,4}{who,-10}{st.Hp * Battle.HpScale,10}{st.Atk,8}"
                + $"{st.Def,8}{st.Spd,8}{1 + skills,6}");
        }
    }
}
