#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using EggCommand.Core;

namespace EggCommand.Sim
{
    /// <summary>図鑑を1枚の HTML に書き出す。
    ///
    /// ⭐ **狙いは「増やすとき・調整するときに見えること」**。遊ぶ画面ではない。
    /// ⚠️ 手で書かない。Core の表から毎回作り直すので、書き換えたら図鑑もその場で変わる
    /// （手で書いた一覧は必ず古くなる）。
    ///
    /// ⭐ 一番効くのは**逆引き**（この技はどの種族から出るのか）。
    /// 表を目で追って数えるのが一番つらい情報なので、ここで先に数えておく。
    /// </summary>
    public static class Book
    {
        public static string Write(string path)
        {
            var html = new StringBuilder();
            Head(html);

            html.Append("<h1>図鑑</h1>");
            html.Append("<p class=note>")
                .Append($"種族 <b>{SpeciesTable.All.Count}</b>　")
                .Append($"技 <b>{Skills.All.Count}</b>　")
                .Append($"効果の種類 <b>{Enum.GetValues(typeof(EffectKind)).Length}</b>　")
                .Append($"素質 <b>{Traits.All.Count}</b>　")
                .Append($"巣 <b>{Nests.All.Length}</b>")
                .Append("</p>");
            html.Append("<p class=warn>⚠️ この HTML は書き出したもの。直しても次の書き出しで消える。"
                + "直す先は <code>unity/Packages/com.eggcommand.core/Runtime/</code> の表。</p>");

            SpeciesSection(html);
            SkillSection(html);
            TraitSection(html);

            html.Append("</body></html>");

            var full = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, html.ToString(), new UTF8Encoding(false));
            return full;
        }

        // ── 種族 ────────────────────────────────────────

        private static void SpeciesSection(StringBuilder html)
        {
            var nestSpecies = new HashSet<string>(Encounters.NestSpecies);

            html.Append("<h2>種族</h2>");
            html.Append("<p class=note>⚠️ 基礎値の合計は全種族 <b>")
                .Append(SpeciesTable.BaseTotal)
                .Append("</b> で揃える。差は配分で出す。"
                    + "⭐ 属性は種族ではなく<b>個体</b>が持つので、どの種族からも3属性すべてが生まれる。</p>");

            html.Append("<div class=scroll><table><thead><tr>"
                + "<th>絵</th><th>名前</th><th>枠1</th>"
                + "<th class=num>HP</th><th class=num>攻</th><th class=num>防</th><th class=num>速</th>"
                + "<th>卵ガチャ（枠2・3）</th><th>巣</th></tr></thead><tbody>");

            foreach (var species in SpeciesTable.All)
            {
                html.Append("<tr>");
                html.Append("<td>").Append(SpriteSvg(species)).Append("</td>");
                html.Append("<td class=name><b>").Append(Esc(species.Name)).Append("</b><br><code>")
                    .Append(Esc(species.Id)).Append("</code></td>");

                var first = Skills.ById(species.Skill1);
                html.Append("<td class=name>").Append(Esc(first.Name))
                    .Append("<br><span class=dim>").Append(Esc(ScaleOf(first))).Append("</span></td>");

                var b = species.Base;
                html.Append(Num(b.Hp)).Append(Num(b.Atk)).Append(Num(b.Def)).Append(Num(b.Spd));

                html.Append("<td class=pool>");
                foreach (var id in species.Gacha)
                {
                    html.Append("<span class=chip>").Append(Esc(Skills.ById(id).Name)).Append("</span>");
                }
                html.Append("</td>");

                html.Append("<td class=tight>").Append(nestSpecies.Contains(species.Id)
                    ? "立つ"
                    : "<span class=dim>立たない</span>").Append("</td>");
                html.Append("</tr>");
            }
            html.Append("</tbody></table></div>");
        }

        /// <summary>枠1がどのステで伸びるか。⚠️ ここが偏ると、そのステが二重に得になる。</summary>
        private static string ScaleOf(Skill skill)
        {
            foreach (var effect in skill.Effects)
            {
                if (effect.Kind == EffectKind.Damage)
                {
                    return effect.Scale == DamageScale.Def ? "防御で伸びる" : "攻撃で伸びる";
                }
            }
            return "ダメージ無し";
        }

        /// <summary>ドット絵をそのまま SVG にする。⚠️ 拡大でぼかさない。</summary>
        private static string SpriteSvg(Species species)
        {
            var sprite = species.Sprite;
            var palette = species.Palettes[0];
            var svg = new StringBuilder();
            svg.Append($"<svg class=dot viewBox=\"0 0 {sprite.Width} {sprite.Height}\">");
            for (int y = 0; y < sprite.Height; y++)
            {
                for (int x = 0; x < sprite.Width; x++)
                {
                    byte index = sprite.At(x, y);
                    if (index == 0) continue;
                    svg.Append($"<rect x=\"{x}\" y=\"{y}\" width=\"1\" height=\"1\" fill=\"")
                        .Append(palette.ColorOf(index)).Append("\"/>");
                }
            }
            svg.Append("</svg>");
            return svg.ToString();
        }

        // ── 技 ─────────────────────────────────────────

        private static void SkillSection(StringBuilder html)
        {
            // ⭐ 逆引き。目で数えるのが一番つらい情報なので先に作る
            var from = new Dictionary<string, List<string>>();
            foreach (var species in SpeciesTable.All)
            {
                Add(from, species.Skill1, species.Name + "（枠1）");
                foreach (var id in species.Gacha)
                {
                    if (id != species.Skill1) Add(from, id, species.Name);
                }
            }

            html.Append("<h2>技</h2>");
            html.Append("<p class=note>⭐ <b>どの種族から出るか</b>が右端。"
                + "空欄の技は<b>手に入らない</b>（数える検査が落ちる）。<br>"
                + "⚠️ 確率が付くのは<b>ダメージと強化以外</b>。"
                + "相手に掛けるものだけ、実際の率が速度差で ±30pt 動く。</p>");

            html.Append("<div class=scroll><table><thead><tr>"
                + "<th>技</th><th class=num>CT</th><th>対象</th><th>効果</th>"
                + "<th>出る種族</th></tr></thead><tbody>");

            foreach (var skill in Skills.All)
            {
                html.Append("<tr>");
                html.Append("<td class=name><b>").Append(Esc(skill.Name)).Append("</b><br><code>")
                    .Append(Esc(skill.Id)).Append("</code><br><span class=dim>")
                    .Append(Esc(skill.Gist)).Append("</span></td>");
                html.Append(Num(skill.Ct));
                html.Append("<td class=tight>").Append(Esc(TargetLabel(skill.Target))).Append("</td>");

                html.Append("<td>");
                foreach (var effect in skill.Effects)
                {
                    html.Append("<div class=eff>").Append(Esc(EffectLabel(effect)));
                    if (effect.Chance < 100)
                    {
                        html.Append(" <span class=chance>").Append(effect.Chance).Append("%</span>");
                    }
                    html.Append("</div>");
                }
                html.Append("</td>");

                html.Append("<td class=pool>");
                List<string>? owners;
                if (from.TryGetValue(skill.Id, out owners))
                {
                    foreach (string owner in owners!)
                    {
                        html.Append("<span class=chip>").Append(Esc(owner)).Append("</span>");
                    }
                }
                else
                {
                    html.Append("<span class=bad>手に入らない</span>");
                }
                html.Append("</td></tr>");
            }
            html.Append("</tbody></table></div>");
        }

        private static void Add(Dictionary<string, List<string>> map, string key, string value)
        {
            List<string>? list;
            if (!map.TryGetValue(key, out list)) map[key] = list = new List<string>();
            list!.Add(value);
        }

        private static string TargetLabel(Target target)
        {
            switch (target)
            {
                case Target.EnemyOne: return "敵1体";
                case Target.EnemyAll: return "敵全体";
                case Target.AllyLowest: return "味方1体";
                default: return "自分";
            }
        }

        private static string EffectLabel(Effect e)
        {
            switch (e.Kind)
            {
                case EffectKind.Damage:
                    string scale = e.Scale == DamageScale.Def ? "防御" : "攻撃";
                    string times = e.Repeat > 1 ? $" ×{e.Repeat}回" : "";
                    return $"ダメージ {Skills.LabelOf(e.Power)}（{scale}で伸びる）{times}";
                case EffectKind.Buff:
                    string dir = e.Sign > 0 ? "UP" : "DOWN";
                    return $"{Stats.LabelOf(e.Stat)}{dir} {Skills.BuffPercent}% / {e.Turns}行動";
                case EffectKind.Poison: return $"毒 ×{e.Stacks} / {e.Turns}行動";
                case EffectKind.Regen: return $"リジェネ ×{e.Stacks} / {e.Turns}行動";
                case EffectKind.HealRatio: return $"HP回復 最大の{e.Percent}%";
                case EffectKind.Shield: return $"盾 {e.Count}枚";
                case EffectKind.Stun: return $"スタン {e.Turns}回";
                case EffectKind.Ct: return $"CT {(e.Delta > 0 ? "+" : "")}{e.Delta}";
                case EffectKind.Taunt: return $"挑発 {e.Hits}回ぶん";
                case EffectKind.Guts: return $"ガッツ {e.Turns}行動";
                case EffectKind.Immune: return $"免疫 {e.Turns}行動";
                default: return e.Kind.ToString();
            }
        }

        // ── 素質 ────────────────────────────────────────

        private static void TraitSection(StringBuilder html)
        {
            html.Append("<h2>素質</h2>");
            html.Append("<p class=warn>⚠️ <b>まだ戦闘に繋がっていない</b>（")
                .Append(Traits.Wired).Append(" / ").Append(Traits.All.Count)
                .Append(" 件）。形を見て決めるための一覧。</p>");
            html.Append("<p class=note>⭐ 技の3枠とは<b>別枠</b>。"
                + "素質は技そのものを強くせず、<b>特定の動き</b>を強くする"
                + "（技を直に強くすると「その技を持つのが正解」で終わる）。</p>");

            html.Append("<div class=scroll><table><thead><tr>"
                + "<th>素質</th><th>働く場面</th><th>すること</th><th>噛み合うもの</th>"
                + "</tr></thead><tbody>");
            foreach (var trait in Traits.All)
            {
                html.Append("<tr>");
                html.Append("<td class=name><b>").Append(Esc(trait.Name)).Append("</b><br><code>")
                    .Append(Esc(trait.Id)).Append("</code></td>");
                html.Append("<td class=tight>").Append(Esc(Traits.LabelOf(trait.When))).Append("</td>");
                html.Append("<td>").Append(Esc(trait.Gist)).Append("</td>");
                html.Append("<td>").Append(Mark(trait.Pairs)).Append("</td>");
                html.Append("</tr>");
            }
            html.Append("</tbody></table></div>");
        }

        // ── 器 ─────────────────────────────────────────

        private static string Num(int value) => $"<td class=num>{value}</td>";

        private static string Esc(string text) => text
            .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        /// <summary>**強調** だけ通す。⚠️ それ以上は解釈しない。</summary>
        private static string Mark(string text)
        {
            string escaped = Esc(text);
            var output = new StringBuilder();
            bool open = false;
            for (int i = 0; i < escaped.Length; i++)
            {
                if (i + 1 < escaped.Length && escaped[i] == '*' && escaped[i + 1] == '*')
                {
                    output.Append(open ? "</b>" : "<b>");
                    open = !open;
                    i++;
                    continue;
                }
                output.Append(escaped[i]);
            }
            if (open) output.Append("</b>");
            return output.ToString();
        }

        /// <summary>⚠️ 意匠は控えめに。読むための道具なので、区切りは余白と面の明度で作る
        /// （線で囲わない）。差し色は1つだけ。</summary>
        private static void Head(StringBuilder html)
        {
            html.Append(@"<!doctype html><html lang=ja><head><meta charset=utf-8>
<meta name=viewport content=""width=device-width,initial-scale=1"">
<title>図鑑 — Egg Command Battle</title>
<style>
:root{--ink:#22201c;--dim:#8a8175;--bg:#faf8f4;--panel:#fff;--band:#f1ede5;--accent:#c98a2e;--bad:#c9452e}
@media(prefers-color-scheme:dark){
:root{--ink:#e8e2d6;--dim:#8a8175;--bg:#1a1815;--panel:#221f1b;--band:#2a2622;--accent:#e0a94e;--bad:#e0705a}}
*{box-sizing:border-box}
body{margin:0;padding:40px 24px 96px;background:var(--bg);color:var(--ink);
 font:15px/1.7 ""Hiragino Sans"",""Noto Sans JP"",system-ui,sans-serif}
h1{font-size:34px;margin:0 0 4px;letter-spacing:.04em}
h2{font-size:22px;margin:64px 0 8px;letter-spacing:.04em}
.note,.warn{margin:0 0 20px;color:var(--dim);font-size:13px;max-width:70em}
.warn{color:var(--bad)}
code{font:12px/1.5 ui-monospace,monospace;color:var(--dim)}
.scroll{overflow-x:auto;-webkit-overflow-scrolling:touch}
table{border-collapse:collapse;width:100%;min-width:900px;max-width:1400px}
thead th{text-align:left;font-size:12px;font-weight:600;color:var(--dim);
 padding:0 12px 8px;white-space:nowrap}
tbody tr{background:var(--panel)}
tbody tr:nth-child(even){background:var(--band)}
td{padding:12px;vertical-align:top}
td.name,td.tight{white-space:nowrap}
td.num{text-align:right;font-variant-numeric:tabular-nums;white-space:nowrap}
.dim{color:var(--dim);font-size:12px}
.bad{color:var(--bad);font-size:12px}
.pool{line-height:2.1}
.chip{display:inline-block;padding:1px 8px;margin:0 4px 0 0;font-size:12px;
 background:var(--band);color:var(--dim)}
tbody tr:nth-child(even) .chip{background:var(--panel)}
.eff{font-size:13px;white-space:nowrap}
.chance{color:var(--accent);font-weight:700}
svg.dot{width:56px;height:56px;image-rendering:pixelated;shape-rendering:crispEdges;display:block}
</style></head><body>");
        }
    }
}
