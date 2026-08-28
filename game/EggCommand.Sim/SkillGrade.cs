#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using EggCommand.Core;

namespace EggCommand.Sim
{
    /// <summary>技を**格**の順に1枚へ並べる（`sim grade`）。
    ///
    /// ⭐ **これは測る道具ではなく、見渡す道具。**戦闘を1回も回さないので一瞬で出る。
    /// 効き目の実測は `sim skillvalue`（勝率の差）が別に持つ ── ⚠️ 混ぜないこと。
    ///
    /// ⭐ **格の出所は <see cref="SkillValues.GradeOf"/>（期待値）。**
    /// ⚠️ 効果の数では切らない ── 「毒を10重ねる」のような単品が効果1つのまま
    /// 最上位の働きをする（3.62手ぶん）ので、構造は代理指標にしかならない
    /// （作者の指摘 2026-08-27）。
    ///
    /// ⭐ 欄の読み方:
    ///   **格**… ★1〜5。「外」＝ <see cref="SkillValues.Floor"/> 未満＝**押すと枠1 で殴るより損**
    ///   **手ぶん**… 枠1 の一撃を 1.0 とした期待値
    ///   **生**… CT の式が出す値段（天井で刈る前）。⚠️ 手ぶんとは別物
    ///   **確率**… 手書き30か所の散らばり（式が無い）
    public static class SkillGrade
    {
        public static void Run()
        {
            var reachable = Reachable();
            var rows = new List<Row>();
            foreach (var skill in Skills.All)
            {
                int load = Skills.LoadOf(skill);
                int ct = skill.Ct;
                rows.Add(new Row
                {
                    Skill = skill,
                    Load = load,
                    Ct = ct,
                    // ⚠️ パッシブとひっくり返す級（CT7）は式の外なので潰れを数えない
                    Crushed = skill.Passive || Skills.IsHeavyCt(skill) ? 0 : Math.Max(0, load - ct),
                    Chance = MinChanceOf(skill),
                    Grade = SkillValues.GradeOf(skill),
                    Value = SkillValues.Of(skill, out _),
                    Where = Skills.BossOnly.Contains(skill.Id) ? "ボス"
                        : reachable.Contains(skill.Id) ? "配布済" : "未配布",
                });
            }

            Console.WriteLine();
            Console.WriteLine($"■ 技の格（{rows.Count} 本・戦闘は回していない）");
            Console.WriteLine("  生 ＝ 天井で刈る前の値段（Skills.LoadOf）/ 潰 ＝ 生−CT（天井に吸われた量）");
            Console.WriteLine();
            Console.WriteLine("  格 手ぶん  生  CT 効 確率  配布   型      技名");
            Console.WriteLine("  ──────────────────────────────────────────────");
            foreach (var r in rows.OrderByDescending(x => x.Value).ThenBy(x => x.Skill.Id))
            {
                string crushed = r.Crushed > 0 ? r.Crushed.ToString() : "·";
                string ct = r.Skill.Passive ? "─" : r.Ct.ToString();
                string chance = r.Chance >= 100 ? "─" : r.Chance + "%";
                string grade = r.Grade == 0 ? "外" : "★" + r.Grade;
                Console.WriteLine($"  {grade,-3}{r.Value,5:0.00}  {r.Load,2}  {ct,2} {r.Skill.Effects.Count,2} {chance,4}"
                    + $"  {r.Where,-6} {Skills.LabelOf(r.Skill.Type),-6} {r.Skill.Name}");
            }

            Histogram(rows);
            Crushing(rows);
            Chances(rows);
        }

        /// <summary>⭐ 生の値段の散らばり。**格の境目をここから決める**（勘で切らない）。</summary>
        private static void Histogram(IReadOnlyList<Row> rows)
        {
            Console.WriteLine();
            Console.WriteLine("■ 生の値段の散らばり（＝格の物差しの分布）");
            var byLoad = rows.GroupBy(r => r.Load).OrderBy(g => g.Key);
            foreach (var g in byLoad)
            {
                Console.WriteLine($"  生 {g.Key,2}: {new string('#', g.Count()),-24} {g.Count(),2} 本"
                    + $"   {string.Join("・", g.Take(4).Select(r => r.Skill.Name))}"
                    + (g.Count() > 4 ? " …" : ""));
            }
        }

        /// <summary>⚠️ 天井に吸われている技。⭐ **ここが「格が作れない」の実体。**</summary>
        private static void Crushing(IReadOnlyList<Row> rows)
        {
            var crushed = rows.Where(r => r.Crushed > 0).OrderByDescending(r => r.Crushed).ToList();
            Console.WriteLine();
            Console.WriteLine($"■ 天井（CT{Skills.CtCap}）に吸われている技: {crushed.Count} 本 / {rows.Count} 本");
            if (crushed.Count == 0) { Console.WriteLine("  なし"); return; }
            Console.WriteLine("  ⚠️ 生の値段は違うのに、画面では同じ CT に見えている");
            foreach (var r in crushed)
            {
                Console.WriteLine($"  生 {r.Load,2} → CT {r.Ct}（−{r.Crushed}）  {r.Skill.Name}");
            }
            var tops = crushed.Select(r => r.Ct).Distinct().ToList();
            Console.WriteLine($"  ⭐ この {crushed.Count} 本は、生 {crushed.Min(r => r.Load)}〜{crushed.Max(r => r.Load)} が"
                + $" CT {string.Join("/", tops)} の {tops.Count} 段に潰れている");
        }

        /// <summary>⚠️ 手書きの確率。⭐ 値の散らばりと、式が無いことを見せる。</summary>
        private static void Chances(IReadOnlyList<Row> rows)
        {
            var withChance = rows.Where(r => r.Chance < 100).ToList();
            Console.WriteLine();
            Console.WriteLine($"■ 確率つきの技: {withChance.Count} 本（手書き・式なし）");
            foreach (var g in withChance.GroupBy(r => r.Chance).OrderByDescending(g => g.Key))
            {
                Console.WriteLine($"  {g.Key,3}%: {g.Count(),2} 本   "
                    + string.Join("・", g.Select(r => r.Skill.Name)));
            }
            if (withChance.Count > 0)
            {
                var vals = withChance.Select(r => r.Chance).OrderBy(v => v).ToList();
                Console.WriteLine($"  ⭐ 幅 {vals[0]}〜{vals[^1]} / 中央 {vals[vals.Count / 2]}"
                    + $" / 種類 {vals.Distinct().Count()} 通り");
            }
        }

        /// <summary>載っている効果の種類。⭐ **値段の表を較正するときの突き合わせ先。**
        /// ⚠️ 順は書いた順のまま（並べ替えると「先頭が主役」が読めなくなる）。</summary>
        private static string KindsOf(Skill skill)
        {
            var seen = new List<string>();
            foreach (var e in skill.Effects)
            {
                string k = e.Kind.ToString();
                if (!seen.Contains(k)) seen.Add(k);
            }
            return string.Join("+", seen);
        }

        /// <summary>その技のいちばん外れやすい効果の率。⚠️ 相手が抵抗しないものは 100。</summary>
        private static int MinChanceOf(Skill skill)
        {
            int min = 100;
            foreach (var e in skill.Effects)
            {
                if (Skills.IsHarmful(e) && e.Chance < min) min = e.Chance;
            }
            return min;
        }

        /// <summary>プレイヤーが持てる技（どれかの種族の枠1〜3に入っている）。</summary>
        private static HashSet<string> Reachable()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var species in SpeciesTable.All)
            {
                if (species.Id == Encounters.BossSpeciesId) continue;
                set.Add(species.Skill1);
                foreach (var id in species.Slot2.Pool) set.Add(id);
                foreach (var id in species.Slot3.Pool) set.Add(id);
            }
            return set;
        }

        private sealed class Row
        {
            public Skill Skill = null!;
            public int Load, Ct, Crushed, Chance, Grade;
            public double Value;
            public string Where = "";
        }
    }
}
