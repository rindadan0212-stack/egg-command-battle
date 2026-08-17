#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using EggCommand.Core;

namespace EggCommand.Sim
{
    /// <summary>Wiki のうち、**表が中身のページ**を書き出す。
    ///
    /// ⚠️ **数値を手で転記しない。** 種族・技・特性の表は実装が唯一の出所なので、
    /// 手で写すと必ずどこかでずれる。ずれた Wiki は、無いより悪い
    /// （読んだ人が嘘を信じて編成を組む）。
    ///
    /// ⭐ 説明文もここに置く。ページ全体を生成するので、
    /// 「表だけ生成して周りは手書き」より出所が1つに保たれる。
    /// ⚠️ 手で直したくなったら**このファイルを直す**。生成物を直しても次の生成で消える。
    ///
    /// ⚠️ 生成するのはこの3ページだけ。他のページは手で書く
    /// （遊びの手触りや判断の指針は、表から導けない）。
    /// </summary>
    public static class WikiPages
    {
        /// <summary>生成したページの名前を返す。</summary>
        public static List<string> Write(string dir)
        {
            Directory.CreateDirectory(dir);
            var written = new List<string>();
            written.Add(Put(dir, "種族一覧.md", SpeciesPage()));
            written.Add(Put(dir, "技一覧.md", SkillsPage()));
            written.Add(Put(dir, "特性.md", TraitsPage()));
            return written;
        }

        private static string Put(string dir, string name, string body)
        {
            File.WriteAllText(Path.Combine(dir, name), body, new UTF8Encoding(false));
            return name;
        }

        /// <summary>⚠️ 生成物だと分かるようにする。手で直しても消えることを先に伝える。</summary>
        private static void Stamp(StringBuilder md)
        {
            md.Append("> ⚠️ **このページは実装から自動生成しています。**")
              .Append("直接編集しても次の生成で消えます。\n")
              .Append("> 数値を変えるときは実装側を変えて `sim wiki` を回してください。\n\n");
        }

        // ── 種族 ────────────────────────────────────────

        private static string SpeciesPage()
        {
            var md = new StringBuilder();
            md.Append("# 種族一覧\n\n");
            Stamp(md);

            md.Append("種族が決めるのは**見た目・基礎ステの配分・枠1の技・卵ガチャの中身**です。\n");
            md.Append("⚠️ **基礎ステの合計は全種族で同じ**なので、種族に当たり外れはありません。");
            md.Append("違うのは配分だけです。\n\n");

            md.Append("## 一覧\n\n");
            md.Append("| 種族 | HP | 攻撃 | 防御 | 速度 | 枠1（通常攻撃） |\n");
            md.Append("|---|---|---|---|---|---|\n");
            foreach (var s in SpeciesTable.All)
            {
                var b = s.Base;
                md.Append($"| {s.Name} | {b.Hp} | {b.Atk} | {b.Def} | {b.Spd} | ")
                  .Append($"{Skills.ById(s.Skill1).Name} |\n");
            }
            md.Append($"\n基礎ステの合計はどの種族も **{SpeciesTable.BaseTotal}** です。\n\n");

            md.Append("## 枠1（通常攻撃）\n\n");
            md.Append("⭐ 枠1 は**その種族の通常攻撃**で、CT がありません（いつでも撃てます）。\n");
            md.Append("⚠️ CT が無いのは「行動できない手番を作らない」ためで、大技だからではありません。\n\n");

            md.Append("## 卵ガチャで出る技\n\n");
            md.Append("孵化のとき、枠2・3 はここから引かれます。");
            md.Append("⭐ **種族ごとに違う**ので、欲しい技があるならその種族の巣を狙います。\n\n");
            foreach (var s in SpeciesTable.All)
            {
                var pool = Skills.GachaPoolOf(s.Id, s.Skill1);
                var names = new List<string>();
                foreach (var id in pool) names.Add(Skills.ById(id).Name);
                md.Append($"- **{s.Name}** … {string.Join(" / ", names)}\n");
            }

            md.Append("\n## 関連\n\n- [技一覧](技一覧.md)\n- [ステータス](ステータス.md)\n- [探索](探索.md)\n");
            return md.ToString();
        }

        // ── 技 ──────────────────────────────────────────

        private static string SkillsPage()
        {
            var md = new StringBuilder();
            md.Append("# 技一覧\n\n");
            Stamp(md);

            md.Append("個体は技を3枠持ちます。⭐ 枠1 は種族固定の通常攻撃（CT なし）、");
            md.Append("枠2・3 は卵ガチャか配合で決まります。\n\n");
            md.Append("**CT** … 使ったあと、自分が何回行動するまで再使用できないか。");
            md.Append("⚠️ 枠1 に入っているときは常に 0 です。\n\n");
            md.Append("**通る率** … その効果が入る確率。⚠️ ダメージと強化は必ず通ります。\n\n");

            md.Append("## 一覧\n\n");
            md.Append("| 技 | CT | 対象 | 効果 | 通る率 |\n|---|---|---|---|---|\n");
            foreach (var skill in Skills.All)
            {
                md.Append($"| {skill.Name} | {skill.Ct} | {TargetLabel(skill.Target)} | ")
                  .Append($"{EffectsLabel(skill)} | {ChanceLabel(skill)} |\n");
            }

            md.Append("\n## 威力の段位\n\n");
            md.Append("| 段位 | 威力 |\n|---|---|\n");
            foreach (PowerTier tier in Enum.GetValues(typeof(PowerTier)))
            {
                md.Append($"| {Skills.LabelOf(tier)} | {Skills.DamagePowerOf(tier)} |\n");
            }
            md.Append("\n⚠️ **全体攻撃は1段下げて選ばれています。**");
            md.Append("全体の「中」は単体の「中」よりずっと強いためです。\n\n");

            md.Append("## レベルで伸びるもの\n\n");
            md.Append("技はレベルを上げられます（[スキルレベル](スキルレベル.md)）。");
            md.Append("⭐ **何が伸びるかは技ごとに違います。**\n\n");
            md.Append("| 技 | Lv2 | Lv3 | Lv4 | Lv5 |\n|---|---|---|---|---|\n");
            foreach (var skill in Skills.All)
            {
                var g = Skills.GrowthOf(skill);
                md.Append($"| {skill.Name} |");
                foreach (var gain in g) md.Append($" {GainLabel(gain)} |");
                md.Append("\n");
            }
            md.Append($"\n伸び幅は 威力 +{Skills.GainPowerPercent}% / CT −1 / ")
              .Append($"通る率 +{Skills.GainChancePoints}pt / 継続 +1 / 発数 +1 / ")
              .Append($"回復の割合 +{Skills.GainHealPoints}pt / 盾 +1枚 / 回数 +1 です。\n");

            md.Append("\n## 関連\n\n- [効果の種類](効果の種類.md)\n- [スキルレベル](スキルレベル.md)\n")
              .Append("- [種族一覧](種族一覧.md)\n");
            return md.ToString();
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

        private static string GainLabel(SkillGain gain)
        {
            switch (gain)
            {
                case SkillGain.Power: return "威力";
                case SkillGain.Ct: return "CT";
                case SkillGain.Chance: return "通る率";
                case SkillGain.Turns: return "継続";
                case SkillGain.Repeat: return "発数";
                case SkillGain.Percent: return "回復量";
                case SkillGain.Count: return "盾の枚数";
                default: return "回数";
            }
        }

        private static string ChanceLabel(Skill skill)
        {
            int lowest = 100;
            foreach (var e in skill.Effects) if (e.Chance < lowest) lowest = e.Chance;
            return lowest >= 100 ? "必ず通る" : $"{lowest}%";
        }

        private static string EffectsLabel(Skill skill)
        {
            var parts = new List<string>();
            foreach (var e in skill.Effects) parts.Add(EffectLabel(e));
            return string.Join(" ＋ ", parts);
        }

        private static string EffectLabel(Effect e)
        {
            switch (e.Kind)
            {
                case EffectKind.Damage:
                {
                    string scale = e.Scale == DamageScale.Def ? "・防御で伸びる" : "";
                    string shots = e.Repeat > 1 ? $"を{e.Repeat}発" : "";
                    return $"ダメージ{Skills.LabelOf(e.Power)}{shots}{scale}";
                }
                case EffectKind.Buff:
                    return $"{Stats.LabelOf(e.Stat)}{(e.Sign > 0 ? "UP" : "DOWN")}"
                        + $"{Skills.BuffPercent}%（{e.Turns}回）";
                case EffectKind.Poison: return $"毒×{e.Stacks}（{e.Turns}回）";
                case EffectKind.Regen: return $"リジェネ×{e.Stacks}（{e.Turns}回）";
                case EffectKind.HealRatio: return $"HP{e.Percent}%回復";
                case EffectKind.Shield: return $"盾{e.Count}枚";
                case EffectKind.Stun: return $"スタン{e.Turns}回";
                case EffectKind.Ct: return e.Delta < 0 ? $"CT{-e.Delta}短縮" : $"CT{e.Delta}延長";
                case EffectKind.Taunt: return $"挑発{e.Hits}回";
                case EffectKind.Guts: return $"ガッツ（{e.Turns}回）";
                default: return $"免疫（{e.Turns}回）";
            }
        }

        // ── 特性 ────────────────────────────────────────

        private static string TraitsPage()
        {
            var md = new StringBuilder();
            md.Append("# 特性\n\n");
            Stamp(md);

            md.Append("個体は特性を**1つだけ**持ちます。⭐ 技の3枠とは別枠なので、技を圧迫しません。\n\n");
            md.Append("⚠️ **特性は技そのものを強くしません。**強くするのは「動き」のほうです。\n");
            md.Append("だから**噛み合う技を持っていないと、持っていても何も起きません**。\n\n");

            md.Append("## 一覧\n\n");
            md.Append("| 特性 | 働く場面 | すること | 噛み合うもの |\n|---|---|---|---|\n");
            foreach (var trait in Traits.All)
            {
                md.Append($"| {trait.Name} | {Traits.LabelOf(trait.When)} | {trait.Gist} | ")
                  .Append($"{Flatten(trait.Pairs)} |\n");
            }

            md.Append("\n## 効き目\n\n");
            md.Append("| | |\n|---|---|\n");
            md.Append($"| 狙い澄まし | 弱化が通る率 +{Battle.TraitAim}pt |\n");
            md.Append($"| 意地 | 弱化を受ける率 −{Battle.TraitStubborn}pt |\n");
            md.Append($"| 返し身 | 受けたダメージの {Battle.TraitSpitePercent}% を返す |\n");
            md.Append($"| 執念 | 盾が1枚剥がれるごとにゲージ +{Battle.TraitGritGauge}"
                + $"（満タンは {Battle.GaugeMax}）|\n");
            md.Append("| 手数 | 1体に当てた発数−1 だけ技の待ちが縮む |\n");
            md.Append($"| 食らいつき | 与えたダメージの {Battle.TraitLeechPercent}% を吸う |\n");

            md.Append($"\n## いつから出るか\n\n");
            md.Append($"⭐ **★{Traits.MinRarity} 以上の卵からしか出ません。**\n");
            md.Append("浅い巣からは低い★しか出ないので、始めたばかりの個体は特性を持ちません。\n\n");
            md.Append("⚠️ **配合の継承はこの下限を見ません。**");
            md.Append("親が持っていれば、子は★に関係なく受け継ぎます。\n");

            md.Append("\n## 関連\n\n- [レアリティ](レアリティ.md)\n- [技一覧](技一覧.md)\n")
              .Append("- [配合](配合.md)\n");
            return md.ToString();
        }

        /// <summary>⚠️ 表の中に改行や強調が入ると崩れるので均す。</summary>
        private static string Flatten(string text) =>
            text.Replace("**", "").Replace("\n", " ").Replace("|", "／");
    }
}
