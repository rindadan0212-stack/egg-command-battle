#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using EggCommand.Core;

namespace EggCommand.Sim
{
    /// <summary>⭐ **参考作品の技を、本作の物差し（手ぶん）で測る**（`sim mamo`・2026-08-27）。
    ///
    /// ⭐ **狙いは物差しの検算。**向こうには R/SR/UR という**正解のラベル**が付いている。
    /// 本作の <see cref="SkillValues.GradeOf"/> がそのラベルと同じ向きに並ぶなら、
    /// 期待値で格を測るという考え自体が裏取りできる。⚠️ 並ばないなら物差しが疑わしい。
    ///
    /// ⚠️ **これは移植ではない。**名前も数値も持ち込まない（`参考/` の注記のとおり）。
    /// ⭐ 読むのは**効果の並び**だけ ── それを本作の語彙へ写して、本作の式に通す。
    ///
    /// ⚠️ **写しきれないものがある**（無敵・追撃・反撃・貫通系など、本作に無い語彙）。
    /// ⭐ 何割写せたかも一緒に出す ── 写せていないぶんは手ぶんが低く出る。</summary>
    public static class MamoValue
    {
        public const string Path_ = "../参考/まもダン_全キャラ.json";

        /// <summary>効果タグ → 本作の効果。⚠️ **null は「本作に語彙が無い」**。
        /// ⭐ 持続や割合は向こうの本文から読む（既定は本作の並び）。</summary>
        private static Effect Map(string tag, int turns, bool all)
        {
            switch (tag)
            {
                case "攻撃力強化": return Effect.Buff(StatKey.Atk, 1, turns);
                case "防御力強化": return Effect.Buff(StatKey.Def, 1, turns);
                case "速度強化": return Effect.Buff(StatKey.Spd, 1, turns);
                case "攻撃力弱化": return Effect.Buff(StatKey.Atk, -1, turns);
                case "防御力弱化": return Effect.Buff(StatKey.Def, -1, turns);
                case "速度弱化": return Effect.Buff(StatKey.Spd, -1, turns);
                // ⚠️ 「与ダメージアップ」は本作に枠が無い ── 攻撃力強化で代用する
                case "与ダメージアップ":
                case "攻撃ステータスアップ": return Effect.Buff(StatKey.Atk, 1, turns);
                case "被ダメージダウン": return Effect.Buff(StatKey.Def, 1, turns);
                case "毒": return Effect.Poison(1, turns);
                // ⚠️ 「傷」は回復を止める札。本作に無いので毒で代用（削れる量として数える）
                case "傷": return Effect.Poison(1, turns);
                case "リジェネ": return Effect.Regen(1, turns);
                case "回復": return Effect.HealRatio(30);
                case "バリア": return Effect.Shield(2);
                case "スタン": return Effect.Stun(1);
                case "睡眠": return Effect.Sleep(2);
                case "挑発": return Effect.Taunt(3);
                case "ブロック": return Effect.Block(turns);
                case "免疫": return Effect.Immune(turns);
                case "CT短縮": return Effect.Ct(-2);
                case "CT延長": return Effect.Ct(2);
                case "ゲージアップ": return Effect.Gauge(30);
                case "ゲージダウン": return Effect.Gauge(-40);
                case "ゲージ吸収": return Effect.Gauge(-40);
                // ⭐ 「ターン獲得」＝ もう1手。ゲージ満タンぶんとして数える
                case "ターン獲得": return Effect.Gauge(100);
                case "強化解除": return Effect.Dispel(1);
                case "弱化解除": return Effect.Cleanse(1);
                case "強化奪い": return Effect.Steal(1);
                case "蘇生": return Effect.Revive(40);
                case "最大HP削り": return Effect.HealRatio(-20);
                default: return null!;     // ⚠️ 本作に語彙が無い
            }
        }

        /// <summary>攻撃の形を決めるタグ（効果ではなく**一撃の性質**）。</summary>
        private static readonly HashSet<string> Shapes = new HashSet<string>
        {
            "全体攻撃", "2回攻撃", "3回攻撃", "4回攻撃", "5回攻撃", "6回攻撃",
            "防御参照攻撃", "速度参照攻撃", "HP参照攻撃", "防御無視攻撃",
        };

        public static void Run(string root)
        {
            var path = System.IO.Path.Combine(root, "参考/まもダン_全キャラ.json");
            if (!File.Exists(path)) { Console.WriteLine($"⚠️ 参考データが無い: {path}"); return; }
            using var doc = JsonDocument.Parse(File.ReadAllText(path));

            var rows = new List<(string Rarity, string Slot, int Grade, double Value,
                int Tags, int Missed, string Name, string Shape, int Count)>();
            var missedTags = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var ch in doc.RootElement.GetProperty("characters").EnumerateArray())
            {
                string rarity = ch.GetProperty("rarity").GetString() ?? "?";
                foreach (var slot in new[] { "skill1", "skill2" })
                {
                    if (!ch.TryGetProperty(slot, out var sk) || sk.ValueKind != JsonValueKind.Object)
                        continue;
                    var made = Build(sk, out int tags, out int missed, missedTags);
                    if (made == null) continue;
                    string text = sk.TryGetProperty("text", out var tx) ? tx.GetString() ?? "" : "";
                    rows.Add((rarity, slot, SkillValues.GradeOf(made), SkillValues.Of(made, out _),
                        tags, missed, sk.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "",
                        ShapeOf(made, text), made.Effects.Count));
                }
            }

            Report(rows, missedTags);
        }

        /// <summary>1つの技を本作の語彙で組み直す。⚠️ 写せなければ効果が減る（低く出る）。</summary>
        private static Skill? Build(JsonElement sk, out int tags, out int missed,
            Dictionary<string, int> missedTags)
        {
            tags = 0; missed = 0;
            string text = sk.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
            var list = new List<string>();
            if (sk.TryGetProperty("effects", out var es) && es.ValueKind == JsonValueKind.Array)
                foreach (var e in es.EnumerateArray()) list.Add(e.GetString() ?? "");

            bool all = list.Contains("全体攻撃") || text.Contains("相手全体") || text.Contains("味方全体");
            int repeat = list.Contains("6回攻撃") ? 6 : list.Contains("5回攻撃") ? 5
                : list.Contains("4回攻撃") ? 4 : list.Contains("3回攻撃") ? 3
                : list.Contains("2回攻撃") ? 2 : 1;
            var scale = list.Contains("防御参照攻撃") ? DamageScale.Def
                : list.Contains("速度参照攻撃") ? DamageScale.Spd : DamageScale.Atk;
            bool pierce = list.Contains("防御無視攻撃");
            int turns = TurnsIn(text);

            var effects = new List<Effect>();
            // ⭐ ダメージがあるか ── 威力の欄が空なら「攻撃しない技」
            string power = sk.TryGetProperty("power", out var p) ? p.GetString() ?? "" : "";
            var tier = TierOf(power);
            if (tier != null) effects.Add(Effect.Damage(tier.Value, scale, repeat, pierce));

            foreach (var tag in list)
            {
                if (Shapes.Contains(tag)) continue;      // 形はもう反映済み
                tags++;
                var made = Map(tag, turns, all);
                if (made == null)
                {
                    missed++;
                    missedTags[tag] = missedTags.TryGetValue(tag, out int n) ? n + 1 : 1;
                    continue;
                }
                effects.Add(made);
            }
            if (effects.Count == 0) return null;

            // ⚠️ 味方向けか敵向けか ── 「相手」と書いてあれば敵、無ければ味方
            bool foe = text.Contains("相手") || tier != null;
            var at = all ? (foe ? Target.EnemyAll : Target.AllyAll)
                         : (foe ? Target.EnemyOne : Target.AllyOne);
            return new Skill("mamo", "", "", SkillType.Attack, at, effects.ToArray());
        }

        /// <summary>⭐ **本作の「形」の語彙で言い直す**（`Brew` と同じ札を使う）。
        /// ⚠️ 揃えないと突き合わせにならない ── あちらの分類を新しく作らないこと。</summary>
        private static string ShapeOf(Skill sk, string text)
        {
            bool dmg = sk.Effects.Any(e => e.Kind == EffectKind.Damage);
            bool selfCost = text.Contains("自身に") &&
                (text.Contains("ダウン") || text.Contains("弱化") || text.Contains("スタン"));
            bool selfGain = text.Contains("自身") && !selfCost;
            int boons = sk.Effects.Count(e => e.Kind != EffectKind.Damage && !Skills.IsHarmful(e));
            int banes = sk.Effects.Count(e => e.Kind != EffectKind.Damage && Skills.IsHarmful(e));
            bool all = sk.Target == Target.EnemyAll || sk.Target == Target.AllyAll;

            if (dmg && selfCost) return "代償";
            if (dmg && selfGain && boons > 0) return "攻→自";
            if (dmg && banes > 0) return "攻＋弱";
            if (boons > 0 && banes > 0) return "強＋弱";
            if (banes >= 2) return "弱＋弱";
            if (boons >= 2) return "強＋強";
            if (dmg && boons == 0 && banes == 0) return all ? "全体" : "単品";
            return all ? "全体" : "単品";
        }

        private static PowerTier? TierOf(string power)
        {
            if (power.Contains("特大")) return PowerTier.Huge;
            if (power.Contains("大")) return PowerTier.Large;
            if (power.Contains("中")) return PowerTier.Medium;
            if (power.Contains("小")) return PowerTier.Small;
            return null;
        }

        /// <summary>本文から持続を読む。⚠️ 見つからなければ本作の並び（3ターン）。</summary>
        private static int TurnsIn(string text)
        {
            var m = Regex.Match(text, @"(\d+)ターン");
            return m.Success && int.TryParse(m.Groups[1].Value, out int n) && n > 0 && n < 10 ? n : 3;
        }

        private static void Report(List<(string Rarity, string Slot, int Grade, double Value,
            int Tags, int Missed, string Name, string Shape, int Count)> rows,
            Dictionary<string, int> missedTags)
        {
            Console.WriteLine();
            Console.WriteLine($"■ 参考作品の技を、本作の手ぶんで測る（{rows.Count} 本）");
            Console.WriteLine("  ⚠️ 名前も数値も持ち込んでいない ── 効果の並びだけを本作の語彙へ写した");
            int allTags = rows.Sum(r => r.Tags), allMissed = rows.Sum(r => r.Missed);
            Console.WriteLine($"  ⭐ 写せた効果: {allTags - allMissed} / {allTags}"
                + $"（{100.0 * (allTags - allMissed) / Math.Max(1, allTags):0.0}%）");
            Console.WriteLine("  ⚠️ 写せなかったぶん、手ぶんは**低め**に出る");

            foreach (var slot in new[] { "skill2", "skill1" })
            {
                var band = rows.Where(r => r.Slot == slot).ToList();
                Console.WriteLine();
                Console.WriteLine($"  ── {(slot == "skill2" ? "枠2（本命の技）" : "枠1（通常攻撃・CT0）")} ──");
                Console.WriteLine($"  {"レア",-5}{"本数",6}{"手ぶん平均",11}{"外",6}{"★1",5}{"★2",5}{"★3",5}{"★4",5}{"★5",5}");
                foreach (var rarity in new[] { "N", "R", "SR", "UR" })
                {
                    var ms = band.Where(r => r.Rarity == rarity).ToList();
                    if (ms.Count == 0) continue;
                    var c = new int[6];
                    foreach (var m in ms) c[m.Grade]++;
                    Console.WriteLine($"  {rarity,-5}{ms.Count,6}{ms.Average(m => m.Value),11:0.00}"
                        + $"{c[0],6}{c[1],5}{c[2],5}{c[3],5}{c[4],5}{c[5],5}");
                }
            }

            var main = rows.Where(r => r.Slot == "skill2").ToList();
            Console.WriteLine();
            Console.WriteLine("  ── 枠2 の**形**（本作の `sim brew` と同じ語彙）──");
            Console.WriteLine($"  {"形",-8}{"N",5}{"R",5}{"SR",5}{"UR",5}{"UR率",7}{"手ぶん",8}");
            foreach (var g in main.GroupBy(r => r.Shape).OrderByDescending(g => g.Count()))
            {
                int n = g.Count(r => r.Rarity == "N"), r_ = g.Count(r => r.Rarity == "R");
                int sr = g.Count(r => r.Rarity == "SR"), ur = g.Count(r => r.Rarity == "UR");
                Console.WriteLine($"  {g.Key,-8}{n,5}{r_,5}{sr,5}{ur,5}"
                    + $"{100.0 * ur / g.Count(),6:0}%{g.Average(x => x.Value),8:0.00}");
            }

            Console.WriteLine();
            Console.WriteLine("  ── 1つの技に載っている効果の数（本作の語彙に写したあと）──");
            foreach (var rarity in new[] { "N", "R", "SR", "UR" })
            {
                var ms = main.Where(r => r.Rarity == rarity).ToList();
                if (ms.Count == 0) continue;
                Console.WriteLine($"  {rarity,-4}平均 {ms.Average(m => m.Count),4:0.00} 個"
                    + $"   1個 {ms.Count(m => m.Count == 1),4}"
                    + $" / 2個 {ms.Count(m => m.Count == 2),4}"
                    + $" / 3個以上 {ms.Count(m => m.Count >= 3),4}");
            }

            Console.WriteLine();
            Console.WriteLine("  写せなかった効果（本作に語彙が無いもの・多い順）");
            foreach (var kv in missedTags.OrderByDescending(kv => kv.Value).Take(12))
                Console.WriteLine($"    {kv.Value,4}  {kv.Key}");
        }
    }
}
