#nullable enable
using System;
using System.Collections.Generic;
using EggCommand.Core;

namespace EggCommand.Sim
{
    /// <summary>🚧 **勘で置いた見積りを実測で潰す**（`sim guess`・2026-08-27）。
    ///
    /// ⚠️ <see cref="SkillValues"/> には「🚧 未測定」と書いた定数が4つ在り、
    /// 挑発・免疫・ブロック・ガッツ・蘇生の手ぶんは**丸ごと勘**だった。
    /// ⭐ そのせいで「1.00手ぶん未満だから弱い」と仕分けようとしても、
    /// **弱いのか、測れていないだけなのか**が分からない。
    ///
    /// ⭐ **測るのは「撃って、実際に何回働いたか」。**⚠️ 1回働いたときの値打ちは
    /// 依然として見積り（挑発1回で相手の狙いがどれだけ悪くなるかは数にならない）だが、
    /// **回数のほうは実測に置き換わる** ── 勘が半分になる。
    ///
    /// ⚠️ 両側に同じ札を持たせる（片側だけだと勝ち負けの偏りが混ざる。`sim delivered` と同じ流儀）。</summary>
    public static class GuessProbe
    {
        private const int Battles = 300;
        private const int Tier = 5;

        /// <summary>測る札と、その「働いた」印。</summary>
        /// ⚠️ **数える分母は「掛かった回数」であって「撃った回数」ではない。**
        /// ⭐ 手ぶんの式は最後に確率を掛けるので、ここで撃った回数を分母にすると
        /// **確率が二重に掛かる**（ブロックの実測 0.31 が画面で 0.19 になっていた）。
        private static readonly (string Skill, string Mate, BattleEventKind Landed,
            BattleEventKind Fired, string What)[] Cases =
        {
            ("taunt",       "attack-heavy", BattleEventKind.Taunt,   BattleEventKind.Pulled,    "狙いがずれた"),
            ("taunt-long",  "attack-heavy", BattleEventKind.Taunt,   BattleEventKind.Pulled,    "狙いがずれた"),
            ("immune",      "poison",       BattleEventKind.Immune,  BattleEventKind.Blocked,   "弱化を弾いた"),
            ("immune-long", "poison",       BattleEventKind.Immune,  BattleEventKind.Blocked,   "弱化を弾いた"),
            // ⚠️ **ブロックは「回復と強化」を弾く札。**相手に毒を持たせて測っていたので
            //    404回撃って0回働いた（2026-08-27・測り方の誤り）── 相方は回復にする
            ("block",       "heal-ratio",   BattleEventKind.Block,   BattleEventKind.Blunted,   "回復・強化を弾いた"),
            ("guts",        "attack-heavy", BattleEventKind.Guts,    BattleEventKind.GutsSaved, "致命傷を耐えた"),
            ("guts-deep",   "attack-heavy", BattleEventKind.Guts,    BattleEventKind.GutsSaved, "致命傷を耐えた"),
            ("revive",      "attack-heavy", BattleEventKind.Revived, BattleEventKind.Revived,   "蘇った"),
            ("revive-heavy","attack-heavy", BattleEventKind.Revived, BattleEventKind.Revived,   "蘇った"),
        };

        public static void Run(int seed)
        {
            Console.WriteLine();
            Console.WriteLine($"■ 見積りの実測（各{Battles}戦・段階{Tier}・両側が同じ札）");
            Console.WriteLine("  ⭐ 「撃った回数」に対して「実際に働いた回数」を数える");
            Console.WriteLine("  ⚠️ 1回ぶんの値打ちは依然として見積り ── ここで潰れるのは**回数の勘**だけ");
            Console.WriteLine();
            Console.WriteLine($"  {"札",-12}{"撃った",6}{"掛かった",6}{"働いた",6}{"1回掛かると",10}  いまの手ぶん");
            Console.WriteLine("  ────────────────────────────────────────────────");

            foreach (var (id, mate, landed, fired, what) in Cases)
            {
                var skill = Skills.ById(id);
                int cast = 0, land_ = 0, works = 0, afterRevive = 0;
                var revived = new HashSet<string>(StringComparer.Ordinal);

                for (int i = 0; i < Battles; i++)
                {
                    var rng = new Rng(seed + i).Stream("guess-" + id);
                    var land = new Rng(seed + i).Stream("land-guess-" + id);
                    int serial = 0;
                    var state = Battle.CreateBattle(
                        TraitPartyFor(rng, id, mate, Tier, ref serial),
                        TraitPartyFor(rng, id, mate, Tier, ref serial),
                        land);

                    int read = 0;
                    revived.Clear();
                    while (state.Result == null && state.Actions < Battle.MaxActions)
                    {
                        var actor = Battle.NextActor(state);
                        if (actor == null) break;
                        Battle.PerformAction(state, actor, Ai.ChooseAction(state, actor));

                        for (; read < state.Log.Count; read++)
                        {
                            var e = state.Log[read];
                            // ⚠️ 🔴 **`else if` の連鎖をやめて独立した `if` にする**（2026-08-27）。
                            //    ⭐ 両陣営4体全員に同じ枠2・3（蘇生を含む）を配る作りなので、
                            //    **蘇生で戻った個体も蘇生技を持っている**。連鎖のままだと、
                            //    戻った個体がもう一度「蘇生」を撃った Act は最初の分岐
                            //    （`cast++`）に吸われ、最後の分岐（`afterRevive++`）へ**永久に届かない**
                            //    ── 「戻ってから動いた回数」が過小に出ていた
                            //    （この数字がそのまま `SkillValues.ReviveActs` の根拠）。
                            //    ⚠️ 独立させても二重加算にはならない ── `cast`/`afterRevive` は
                            //    問うている中身が違う（「その技を撃ったか」／「戻ってから動いたか」）ので、
                            //    同じ1手が両方に数えられるのが正しい。
                            //
                            // ⚠️ **撃った回数は Act で数える。**⭐ Applied だと外れた分が落ちるが、
                            //    値打ちを問うているのは「1手を使ったこと」なので撃った側で数える
                            if (e.Kind == BattleEventKind.Act && e.Label == skill.Name) cast++;
                            if (e.Kind == landed && landed != fired) land_++;
                            if (e.Kind == fired)
                            {
                                works++;
                                if (landed == fired) land_++;
                                if (fired == BattleEventKind.Revived) revived.Add(e.Unit);
                            }
                            // ⭐ 蘇生だけは「戻ってから何回動いたか」まで数える
                            if (e.Kind == BattleEventKind.Act && revived.Contains(e.Unit))
                                afterRevive++;
                        }
                    }
                }

                double per = land_ == 0 ? 0 : (double)works / land_;
                string now = NowGuess(id, skill);
                Console.WriteLine($"  {skill.Name,-12}{cast,6}{land_,6}{works,6}{per,10:0.00}  {now}");
                if (fired == BattleEventKind.Revived && works > 0)
                {
                    Console.WriteLine($"  {"",-12}{"",6}{"",6}{"",6}{"",10}  "
                        + $"⭐ 戻ったあと動いた回数 平均 {(double)afterRevive / works:0.00}"
                        + $"（式が使う値 {SkillValues.ReviveActs:0.0}）");
                }
            }

            Console.WriteLine();
            Console.WriteLine("  ⚠️ 「1回掛かると」が式の前提より小さければ、その札は**過大評価**されている");
        }

        /// <summary>いまの見積りが、その札に何手ぶんを与えているか。</summary>
        private static string NowGuess(string id, Skill skill)
        {
            double value = SkillValues.Of(skill, out _);
            return $"手ぶん {value:0.00}";
        }

        /// <summary>枠2・枠3 を決め打ちにした4体。⚠️ `Program.TraitParty` と同じ組み方。</summary>
        private static List<Creature> TraitPartyFor(Rng rng, string skill2, string skill3,
            int tier, ref int serial) =>
            Program.PartyWith(rng, skill2, skill3, tier, ref serial);
    }
}
