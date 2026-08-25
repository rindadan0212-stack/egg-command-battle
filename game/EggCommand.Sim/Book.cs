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
                .Append($"特性 <b>{Traits.All.Count}</b>　")
                .Append($"巣 <b>{Nests.All.Length}</b>")
                .Append("</p>");
            html.Append("<p class=warn>⚠️ この HTML は書き出したもの。直しても次の書き出しで消える。"
                + "直す先は <code>game/EggCommand.Core/</code> の表。</p>");

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
                // ⭐ 枠ごとに型が違う。どちらの枠から出るかが読めるように分けて出す
                foreach (var slot in new[] { species.Slot2, species.Slot3 })
                {
                    html.Append("<div class=eff><b>").Append(Esc(Skills.FlavorOf(slot.Pool)))
                        .Append("</b> ");
                    foreach (var id in slot.Pool)
                    {
                        html.Append("<span class=chip>").Append(Esc(Skills.ById(id).Name))
                            .Append("</span>");
                    }
                    html.Append("</div>");
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
                    // ⚠️ 二択で書かない（スピードを足した日にここだけ古くなった）
            return Skills.LabelOf(effect.Scale) + "で伸びる";
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
                foreach (var id in species.Slot2.Pool)
                {
                    if (id != species.Skill1) Add(from, id, species.Name + "（枠2）");
                }
                foreach (var id in species.Slot3.Pool)
                {
                    if (id != species.Skill1) Add(from, id, species.Name + "（枠3）");
                }
            }

            html.Append("<h2>技</h2>");
            html.Append("<p class=note>⭐ <b>どの種族から出るか</b>が右端。"
                + "空欄の技は<b>手に入らない</b>（数える検査が落ちる）。<br>"
                + "⚠️ 確率が付くのは<b>ダメージと強化以外</b>。"
                + "相手に掛けるものだけ、実際の率が（弱化命中 − 弱化耐性）÷2 ポイント動く。</p>");

            // ⭐ 並びは Wiki の技ページと同じ5項目（スキル名 / 威力 / 効果 / 上昇量 / CT）
            html.Append("<div class=scroll><table><thead><tr>"
                + "<th>スキル名</th><th>型</th><th class=num>威力</th><th>効果</th>"
                + "<th>レベルごとの上昇量</th><th class=num>CT</th>"
                + "<th>出る種族</th></tr></thead><tbody>");

            foreach (var skill in Skills.All)
            {
                html.Append("<tr>");
                html.Append("<td class=name><b>").Append(Esc(skill.Name)).Append("</b><br><code>")
                    .Append(Esc(skill.Id)).Append("</code></td>");
                html.Append("<td class=tight>").Append(Esc(Skills.LabelOf(skill.Type))).Append("</td>");
                // ⚠️ ダメージのある技だけ。他は空欄
                html.Append("<td class=num>").Append(Esc(SkillText.PowerOf(skill))).Append("</td>");
                html.Append("<td>").Append(Esc(SkillText.Describe(skill))).Append("</td>");
                html.Append("<td class=tight>").Append(Esc(SkillText.GrowthOf(skill))).Append("</td>");
                html.Append(Num(skill.Ct));

                html.Append("<td class=pool>");
                List<string>? owners;
                if (from.TryGetValue(skill.Id, out owners))
                {
                    foreach (string owner in owners!)
                    {
                        html.Append("<span class=chip>").Append(Esc(owner)).Append("</span>");
                    }
                }
                else if (Skills.Undistributed.Contains(skill.Id))
                {
                    // ⭐ 実装済みだがまだ配っていない（作者指示 2026-08-19）。事故の「手に入らない」と分ける
                    html.Append("<span class=chip>🚧 未配布</span>");
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

        // ⚠️ 技と効果の言い回しは **Core の SkillText** に集約した（2026-08-18）。
        //    ここに第2の語彙を置かない。

        // ── 特性 ────────────────────────────────────────

        private static void TraitSection(StringBuilder html)
        {
            html.Append("<h2>特性</h2>");
            html.Append("<p class=note>⭐ <b>")
                .Append(Traits.Wired).Append(" / ").Append(Traits.All.Count)
                .Append("</b> 件が戦闘に繋がっている。個体は必ず1つ持つ。</p>");
            html.Append("<p class=warn>⚠️ <b>特性だけでは何もしない。</b>"
                + "効き目は「噛み合うもの」の欄を持っているかで決まる。"
                + "<code>sim traits</code> が、有ると無いとで勝率が何 pt 動くかを測る。</p>");
            html.Append("<p class=note>⭐ 技の3枠とは<b>別枠</b>。"
                + "特性は技そのものを強くせず、<b>特定の動き</b>を強くする"
                + "（技を直に強くすると「その技を持つのが正解」で終わる）。</p>");

            html.Append("<div class=scroll><table><thead><tr>"
                + "<th>特性</th><th>働く場面</th><th>すること</th><th>噛み合うもの</th>"
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
