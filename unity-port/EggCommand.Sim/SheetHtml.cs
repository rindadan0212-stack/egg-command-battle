#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using EggCommand.Core;

namespace EggCommand.Sim
{
    /// <summary>帳面を**クリックで書くための1枚**。`sim sheet html` が書き出す。
    ///
    /// ⭐ 狙いは <see cref="Sheet"/> と同じ ── 作り手が C# を触らずに技と種族を書くこと。
    /// 違いは**入力の仕方**だけで、出てくる文字列は帳面とまったく同じ。
    ///
    /// ⚠️ **書式を2か所に書かない。**前書きも1件ぶんの書き方も <see cref="Sheet"/> から
    /// 貰い、画面の JS が作った文字列を**その場で突き合わせる**（画面上部の「自己検査」）。
    /// ⭐ JS が C# とずれた瞬間に画面が赤くなるので、黙って壊れた帳面が保存されることがない。
    ///
    /// ⚠️ 手ぶんの算数も同じ扱い ── C# が出した57件の値を埋め込み、JS が計算し直して比べる。
    ///
    /// ⚠️ この HTML は書き出したもの。直しても次の書き出しで消える。</summary>
    public static class SheetHtml
    {
        public static string Write(string path)
        {
            // ⚠️ **この瞬間の帳面を丸ごと HTML に焼き込む。**そのあと sheets/*.txt が
            //    手で直されても、開いたままのこの頁は気づかない（2026-08-23 の監査で発覚 ──
            //    エディタ.html を書き出した11分後に技.txt が手で更新され、
            //    古い頁のまま保存すれば足した行が消えるところだった）。
            // ⭐ だから「いつの写しか」を人が読める形で焼き込む。時刻だけでなく
            //    内容のハッシュも持たせる ── 時刻がずれていなくても中身が違えば気づける形にする。
            var generatedAt = DateTime.Now;
            var html = new StringBuilder();
            html.Append("<!-- 帳面エディタ ── ").Append(generatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
                .Append(" 時点の sheets/*.txt を写したもの。それより後に手で直した行があるなら、")
                .Append("保存する前に「帳面を開く」で読み込み直すこと。 -->\n");
            Head(html);
            Body(html);
            html.Append("<script>const D=").Append(Data(generatedAt)).Append(";</script>");
            Script(html);
            html.Append("</body></html>");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, html.ToString(), new UTF8Encoding(false));
            return Path.GetFullPath(path);
        }

        /// <summary>内容の指紋。⚠️ セキュリティ用途ではないので短く切ってよい ──
        /// 「さっき見た内容と同じかどうか」を人の目で見分けられれば足りる。</summary>
        private static string ShortHash(string text)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
            var sb = new StringBuilder(8);
            for (int i = 0; i < 4; i++) sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        // ══ 埋め込むデータ ═════════════════════════════
        // ⚠️ 依存を足さずに書く（プロジェクトの決めごと）。手で組み立てる。

        private static string Data(DateTime generatedAt)
        {
            var mid = MiddleUnit();
            int one = Battle.DamageOf(Skills.DamagePowerOf(PowerTier.Medium), mid.Atk, mid.Def, 1.0);

            var j = new StringBuilder();
            j.Append('{');

            j.Append("\"c\":{");
            j.Append($"\"ctCap\":{Skills.CtCap},\"ctHeavy\":{Skills.CtHeavy},");
            j.Append($"\"baseTotal\":{SpeciesTable.BaseTotal},\"debuffTotal\":{SpeciesTable.DebuffBaseTotal},");
            j.Append($"\"powerUnit\":{Skills.PowerUnit},\"damageBase\":{Battle.DamageBase},");
            j.Append($"\"defSoften\":{Battle.DefSoften},\"hpScale\":{Battle.HpScale},");
            j.Append($"\"buffPercent\":{Skills.BuffPercent},\"tickPercent\":{Skills.TickPercent},");
            j.Append($"\"party\":{Games.PartySize},\"minChance\":{Effect.MinChance},\"wildMax\":{Stats.WildStatMax},");
            j.Append($"\"poolMax\":{Skills.PoolMax},\"spreadMax\":{Skills.SpreadMax},");
            j.Append($"\"atk\":{mid.Atk},\"def\":{mid.Def},\"spd\":{mid.Spd},\"maxHp\":{mid.Hp * Battle.HpScale},\"one\":{one}");
            j.Append("},");

            // ⭐ 語彙は Core から引く。画面だけ別の言い方になるのを防ぐ
            Words(j, "types", Skills.LabelOf, (SkillType[])Enum.GetValues(typeof(SkillType)));
            Words(j, "targets", SkillText.TargetOf, (Target[])Enum.GetValues(typeof(Target)));
            Words(j, "powers", Skills.LabelOf, (PowerTier[])Enum.GetValues(typeof(PowerTier)));

            j.Append("\"powerVals\":{");
            var tiers = (PowerTier[])Enum.GetValues(typeof(PowerTier));
            for (int i = 0; i < tiers.Length; i++)
            {
                j.Append(i > 0 ? "," : "").Append(Str(Skills.LabelOf(tiers[i])))
                 .Append(':').Append(Skills.DamagePowerOf(tiers[i]));
            }
            j.Append("},");

            j.Append("\"stats\":[");
            for (int i = 0; i < Stats.Keys.Length; i++)
                j.Append(i > 0 ? "," : "").Append(Str(Stats.LabelOf(Stats.Keys[i])));
            j.Append("],");
            j.Append("\"buffStats\":[")
             .Append(Str(Stats.LabelOf(StatKey.Atk))).Append(',')
             .Append(Str(Stats.LabelOf(StatKey.Def))).Append(',')
             .Append(Str(Stats.LabelOf(StatKey.Spd))).Append("],");

            // ⭐ 「何で伸びるか」も enum から引く。⚠️ 画面に手で並べない
            //    （スピード依存を足した日に、画面だけ古いままになる）
            // ⭐ 画面に語を手で並べない（Spd を足した日に画面だけ古くなる）
            Words(j, "scales", Skills.LabelOf, (DamageScale[])Enum.GetValues(typeof(DamageScale)));

            // ⭐ 特性が割り込める場面
            Words(j, "whens", Traits.LabelOf, (TraitWhen[])Enum.GetValues(typeof(TraitWhen)));

            // ⭐ ドットの色番号は Core が決める（画面に書き写さない）
            j.Append("\"dotDigits\":").Append(Str(PixelSprite.Digits)).Append(',');
            j.Append("\"freeKind\":").Append(Str(Sheet.FreeKind)).Append(',');
            j.Append("\"memoKey\":").Append(Str(Sheet.MemoKey)).Append(',');
            j.Append("\"headSkill\":").Append(Str(Sheet.SkillHead())).Append(',');
            j.Append("\"headSpecies\":").Append(Str(Sheet.SpeciesHead())).Append(',');
            j.Append("\"headTrait\":").Append(Str(Sheet.TraitHead())).Append(',');

            // ⭐ **帳面の中身をそのまま持たせる。**画面はこれを読んで種にする。
            //
            // ⚠️ 実装だけを種にしていた頃、画面は**帳面の書きかけを知らなかった**ので、
            //    書きかけを持ったまま画面から保存すると**丸ごと消えた**
            //    （2026-08-19。`sim sheet write` に空いていたのと同じ穴が、画面側にもあった）。
            // ⭐ 帳面が無ければ空文字。画面は実装のほうを種にする（初回）。
            string sheetSkillText = Slurp(Sheet.SkillFile);
            string sheetSpeciesText = Slurp(Sheet.SpeciesFile);
            string sheetTraitText = Slurp(Sheet.TraitFile);
            j.Append("\"sheetSkill\":").Append(Str(sheetSkillText)).Append(',');
            j.Append("\"sheetSpecies\":").Append(Str(sheetSpeciesText)).Append(',');
            j.Append("\"sheetTrait\":").Append(Str(sheetTraitText)).Append(',');

            // ⭐ **陳腐化した写しを黙って保存させないための指紋。**
            // ⚠️ ブラウザからは実物のディスクを読めないので「保存前に現物と突き合わせる」は
            //    できない。せめて「これはいつ・どの中身の写しか」を人が気づける形で持たせる
            //    （2026-08-23 の監査 ── エディタ.html を書き出した11分後に技.txt が手で
            //    更新され、古い頁のまま保存すれば消えるところだった）。
            j.Append("\"snapshot\":{");
            j.Append("\"at\":").Append(Str(generatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))).Append(',');
            j.Append("\"skillHash\":").Append(Str(ShortHash(sheetSkillText))).Append(',');
            j.Append("\"speciesHash\":").Append(Str(ShortHash(sheetSpeciesText))).Append(',');
            j.Append("\"traitHash\":").Append(Str(ShortHash(sheetTraitText)));
            j.Append("},");

            // ── 特性 ──
            j.Append("\"traits\":[");
            bool ft = true;
            foreach (var t in Traits.All)
            {
                if (!ft) j.Append(',');
                ft = false;
                j.Append('{')
                 .Append("\"id\":").Append(Str(t.Id))
                 .Append(",\"name\":").Append(Str(t.Name))
                 .Append(",\"when\":").Append(Str(Traits.LabelOf(t.When)))
                 .Append(",\"gist\":").Append(Str(t.Gist))
                 .Append(",\"pairs\":").Append(Str(t.Pairs))
                 .Append(",\"memo\":\"\"")
                 .Append(",\"block\":").Append(Str(Sheet.BlockOf(t)))
                 .Append('}');
            }
            j.Append("],");

            // ── 技 ──
            j.Append("\"skills\":[");
            bool first = true;
            foreach (var s in Skills.All)
            {
                if (!first) j.Append(',');
                first = false;
                double value = Program.TurnValueOf(s, out _);
                j.Append('{')
                 .Append("\"id\":").Append(Str(s.Id))
                 .Append(",\"name\":").Append(Str(s.Name))
                 .Append(",\"gist\":").Append(Str(s.Gist))
                 .Append(",\"type\":").Append(Str(Skills.LabelOf(s.Type)))
                 .Append(",\"ct\":").Append(s.Ct)
                 .Append(",\"target\":").Append(Str(SkillText.TargetOf(s.Target)))
                 .Append(",\"value\":")// ⚠️ **丸めて渡さない。**4桁で丸めていた頃、自己検査は
                 //    「式のちがい」と「丸めのちがい」を区別できず、
                 //    許容を緩めるしか無かった（2026-08-19）。
                 .Append(value.ToString("R", CultureInfo.InvariantCulture))
                 .Append(",\"block\":").Append(Str(Sheet.BlockOf(s)))
                 .Append(",\"says\":").Append(Str(SkillText.Describe(s)))
                 .Append(",\"effects\":[");
                for (int i = 0; i < s.Effects.Count; i++)
                    j.Append(i > 0 ? "," : "").Append(Json(s.Effects[i]));
                j.Append("]}");
            }
            j.Append("],");

            // ── 種族 ──
            j.Append("\"species\":[");
            first = true;
            foreach (var sp in SpeciesTable.All)
            {
                if (!first) j.Append(',');
                first = false;
                j.Append('{')
                 .Append("\"id\":").Append(Str(sp.Id))
                 .Append(",\"name\":").Append(Str(sp.Name))
                 .Append(",\"skill1\":").Append(Str(sp.Skill1))
                 .Append(",\"base\":{");
                for (int i = 0; i < Stats.Keys.Length; i++)
                {
                    j.Append(i > 0 ? "," : "").Append(Str(Stats.LabelOf(Stats.Keys[i])))
                     .Append(':').Append(sp.Base[Stats.Keys[i]]);
                }
                j.Append("},\"slot2\":").Append(Json(sp.Slot2))
                 .Append(",\"slot3\":").Append(Json(sp.Slot3))
                 .Append(",\"sprite\":[");
                for (int y = 0; y < sp.Sprite.Height; y++)
                {
                    var row = new StringBuilder();
                    for (int x = 0; x < sp.Sprite.Width; x++)
                    {
                        byte v = sp.Sprite.At(x, y);
                        row.Append(v == 0 ? '.' : (char)('0' + v));
                    }
                    j.Append(y > 0 ? "," : "").Append(Str(row.ToString()));
                }
                j.Append("],\"palettes\":[");
                for (int i = 0; i < sp.Palettes.Count; i++)
                {
                    j.Append(i > 0 ? ",[" : "[");
                    for (int c = 0; c < sp.Palettes[i].Colors.Length; c++)
                        j.Append(c > 0 ? "," : "").Append(Str(sp.Palettes[i].Colors[c]));
                    j.Append(']');
                }
                j.Append("]}");
            }
            j.Append(']');

            j.Append('}');
            return j.ToString();
        }

        /// <summary>帳面をそのまま読む。⚠️ 無ければ空（画面が実装のほうを使う）。</summary>
        private static string Slurp(string file)
        {
            string path = Path.Combine(Sheet.Dir, file);
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }

        private static void Words<T>(StringBuilder j, string key, Func<T, string> label, T[] values)
        {
            j.Append('"').Append(key).Append("\":[");
            for (int i = 0; i < values.Length; i++)
                j.Append(i > 0 ? "," : "").Append(Str(label(values[i])));
            j.Append("],");
        }

        private static string Json(SkillPool pool)
        {
            var j = new StringBuilder("{\"pool\":[");
            for (int i = 0; i < pool.Pool.Count; i++)
                j.Append(i > 0 ? "," : "").Append(Str(pool.Pool[i]));
            return j.Append("]}").ToString();
        }

        /// <summary>効果を、画面が読む形に。⭐ **帳面の語をそのまま持たせる。**</summary>
        private static string Json(Effect e)
        {
            string word = Sheet.LineOf(e).Split(' ')[0];
            var j = new StringBuilder("{\"k\":").Append(Str(word))
                .Append(",\"名\":").Append(Str(SkillText.NameOf(e)));
            void Put(string key, int v) => j.Append(",\"").Append(key).Append("\":").Append(v);

            switch (e.Kind)
            {
                case EffectKind.Damage:
                    j.Append(",\"威力\":").Append(Str(Skills.LabelOf(e.Power)))
                     .Append(",\"依存\":").Append(Str(Skills.LabelOf(e.Scale)));
                    Put("発数", e.Repeat);
                    j.Append(",\"防御無視\":").Append(e.Pierce ? "true" : "false");
                    break;
                case EffectKind.Buff:
                    j.Append(",\"ステ\":").Append(Str(Stats.LabelOf(e.Stat)));
                    Put("ターン", e.Turns);
                    break;
                case EffectKind.Poison:
                case EffectKind.Regen:
                    Put("スタック", e.Stacks); Put("ターン", e.Turns); break;
                case EffectKind.HealRatio:
                case EffectKind.Revive:
                    Put("割合", e.Percent); break;
                case EffectKind.Gauge:
                    Put("割合", e.Percent); break;
                case EffectKind.Shield:
                case EffectKind.Dispel:
                case EffectKind.Steal:
                    Put("個数", e.Count); break;
                case EffectKind.Stun:
                case EffectKind.Sleep:
                case EffectKind.Block:
                case EffectKind.Guts:
                case EffectKind.Immune:
                    Put("ターン", e.Turns); break;
                case EffectKind.Ct:
                    Put("増減", e.Delta); break;
                case EffectKind.Taunt:
                    Put("回数", e.Hits); break;
            }
            Put("確率", e.Chance);
            return j.Append('}').ToString();
        }

        private static string Str(string? value)
        {
            if (value == null) return "null";
            var sb = new StringBuilder("\"");
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '<': sb.Append("\\u003c"); break;   // ⚠️ </script> で閉じられないように
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        private static StatBlock MiddleUnit()
        {
            var sum = new int[Stats.Keys.Length];
            int count = 0;
            foreach (var sp in SpeciesTable.All)
            {
                if (sp.Id == Encounters.BossSpeciesId) continue;
                for (int i = 0; i < Stats.Keys.Length; i++) sum[i] += sp.Base[Stats.Keys[i]];
                count++;
            }
            int wildEach = Stats.WildTotalMax / Stats.Keys.Length;
            var mid = new StatBlock(0, 0, 0, 0);
            for (int i = 0; i < Stats.Keys.Length; i++)
                mid = mid.With(Stats.Keys[i], sum[i] / count + wildEach * Stats.Scale);
            return mid;
        }

        // ══ 見た目 ═════════════════════════════════════
        // ⚠️ 図鑑（Book.cs）と同じ意匠にそろえる ── 同じ道具箱に見えるように。
        //    区切りは余白と面の明度。線で囲わない。差し色は1つ（琥珀）だけ。
        //    角丸は**押せるもの**にしか付けない。

        private static void Head(StringBuilder html)
        {
            html.Append(@"<!doctype html><html lang=ja><head><meta charset=utf-8>
<meta name=viewport content=""width=device-width,initial-scale=1"">
<title>帳面エディタ — Egg Command Battle</title>
<style>
:root{--ink:#22201c;--dim:#8a8175;--bg:#faf8f4;--panel:#fff;--band:#f1ede5;
 --line:#e4ded2;--accent:#c98a2e;--bad:#c9452e;--good:#4a7c4e}
@media(prefers-color-scheme:dark){
:root{--ink:#e8e2d6;--dim:#8a8175;--bg:#1a1815;--panel:#221f1b;--band:#2a2622;
 --line:#332e28;--accent:#e0a94e;--bad:#e0705a;--good:#7fae83}}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--ink);
 font:15px/1.7 ""Hiragino Sans"",""Noto Sans JP"",system-ui,sans-serif}
h1{font-size:20px;margin:0;letter-spacing:.04em;font-weight:600}
h2{font-size:13px;margin:0 0 10px;letter-spacing:.12em;color:var(--dim);font-weight:600}
button,select,input,textarea{font:inherit;color:inherit}
button{cursor:pointer;border:0;background:var(--band);color:var(--ink);
 padding:7px 14px;border-radius:6px}
button:hover{background:var(--line)}
button.go{background:var(--accent);color:#1a1815;font-weight:700;padding:9px 20px}
button.go:hover{filter:brightness(1.08)}
button.mini{padding:3px 9px;font-size:12px;border-radius:5px}
input[type=text],input[type=number],select,textarea{background:var(--panel);
 border:0;border-bottom:2px solid var(--line);padding:6px 8px;border-radius:0;width:100%}
input:focus,select:focus,textarea:focus{outline:0;border-bottom-color:var(--accent)}
input[type=number]{font-variant-numeric:tabular-nums}

/* 帯 */
#top{display:flex;align-items:center;gap:20px;padding:16px 24px;background:var(--panel)}
#top .grow{flex:1}
#self{font-size:12px;color:var(--good)}
#self.ng{color:var(--bad);font-weight:700}
/* ⚠️ 常に見える場所に「いつの写しか」を出す ── 自己検査の1行に混ぜると、
   スクロールや他の警告に埋もれて気づかれない（2026-08-23 の監査）。 */
#snapshot{font-size:12px;color:var(--dim)}
.auto{color:var(--ink);font-size:14px;flex:1}
.file{cursor:pointer;background:var(--band);padding:7px 14px;border-radius:6px;font-size:15px}
.file:hover{background:var(--line)}
.probs li.stop{color:var(--bad);font-weight:700}
.probs li.todo{color:var(--accent)}
.eff.free{background:var(--panel);box-shadow:inset 3px 0 var(--accent)}

/* 二段組 */
#wrap{display:grid;grid-template-columns:250px 1fr;gap:36px;
 padding:28px 24px 120px;max-width:1500px}
@media(max-width:900px){#wrap{grid-template-columns:1fr;gap:24px}}

/* 左 */
#rail .tabs{display:flex;gap:6px;margin-bottom:14px}
#rail .tabs button{flex:1}
#rail .tabs button[aria-selected=true]{background:var(--ink);color:var(--bg)}
#list{margin-top:12px;max-height:62vh;overflow-y:auto}
#list .row{display:flex;align-items:baseline;gap:8px;padding:5px 10px;cursor:pointer}
#list .row:hover{background:var(--band)}
#list .row[aria-current=true]{background:var(--band);box-shadow:inset 3px 0 var(--accent)}
#list .row .n{flex:1;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
#list .row .v{font-size:12px;color:var(--dim);font-variant-numeric:tabular-nums}
#list .row.fresh .n::after{content:' 新';color:var(--accent);font-size:11px}

/* 右 */
.card{background:var(--panel);padding:22px 24px;margin-bottom:18px}
.grid{display:grid;gap:14px 18px}
.g2{grid-template-columns:1fr 1fr}
.g3{grid-template-columns:1fr 1fr 1fr}
.g6{grid-template-columns:repeat(6,1fr)}
@media(max-width:700px){.g3,.g6{grid-template-columns:1fr 1fr}}
label{display:block;font-size:12px;color:var(--dim);margin-bottom:3px}
.eff{background:var(--band);padding:12px 14px;margin-bottom:10px}
.eff .head{display:flex;gap:10px;align-items:center;margin-bottom:10px}
.eff .head select{flex:0 0 150px}
.eff .head .sp{flex:1}
.eff .args{display:flex;flex-wrap:wrap;gap:12px}
.eff .args .a{flex:0 0 108px}
.eff .args .chk{display:flex;align-items:center;gap:6px;font-size:13px;align-self:flex-end}
.eff .args .chk input{width:auto}

/* 判定 ⚠️ position:sticky にしない。長いページだと張り付いた位置で
   次のカードに重なり、効果の1行目が丸ごと隠れていた（2026-08-19 のスクショで発覚）。
   ⭐ 判定は編集する場所のすぐ上に在ればよく、追いかけて来る必要は無い。 */
.val{display:flex;align-items:baseline;gap:12px}
.val b{font-size:34px;font-variant-numeric:tabular-nums;letter-spacing:-.02em}
.val .u{font-size:13px;color:var(--dim)}
.bar{height:6px;background:var(--band);margin:12px 0 6px;position:relative}
.bar i{position:absolute;top:0;bottom:0;background:var(--accent)}
.bar u{position:absolute;top:-3px;bottom:-3px;width:2px;background:var(--ink);opacity:.35}
.why{font-size:12px;color:var(--dim)}
.probs{margin:0;padding:0;list-style:none;font-size:13px}
/* ⚠️ **3色に分ける。**⚠️ と 🚧 を同じ赤にしていた頃、合計が 600 でない種族を
   開くと ⚠️ だけで赤が並び「壊れている」に見えた（2026-08-19 の監査）。 */
.probs li{padding:3px 0;color:var(--dim)}
.probs li.ok{color:var(--good)}

/* 袋 */
.chips{display:flex;flex-wrap:wrap;gap:6px;margin-top:6px}
.chip{padding:3px 10px;font-size:12px;background:var(--band);color:var(--dim);
 cursor:pointer;border-radius:11px}
.chip[aria-pressed=true]{background:var(--accent);color:#1a1815;font-weight:700}

/* ── 袋をドラッグ&ドロップで組む ── */
.bags{display:grid;grid-template-columns:1fr 1fr;gap:18px}
@media(max-width:760px){.bags{grid-template-columns:1fr}}
.bag{background:var(--band);padding:14px;min-height:132px}
.bag h3{margin:0 0 2px;font-size:13px;letter-spacing:.1em;color:var(--dim);font-weight:600}
.bag .flavor{font-size:12px;color:var(--dim);margin:0 0 10px}
.bag.over{box-shadow:inset 0 0 0 2px var(--accent)}
.bag.full{opacity:.55}
.slot{display:flex;align-items:center;gap:8px;background:var(--panel);
 padding:6px 10px;margin-bottom:5px;cursor:grab}
.slot:active{cursor:grabbing}
.slot .g{color:var(--dim);font-size:13px;letter-spacing:-.1em}
.slot .n{flex:1}
.slot .v{font-size:12px;color:var(--dim);font-variant-numeric:tabular-nums}
.slot.drag{opacity:.35}
.slot button{padding:1px 7px;font-size:11px}
.empty{color:var(--dim);font-size:12px;padding:6px 10px}
.shelf{margin-top:16px}
.shelf .find{max-width:280px;margin-bottom:8px}
.tray{display:flex;flex-wrap:wrap;gap:5px;max-height:210px;overflow-y:auto;
 background:var(--band);padding:10px}
.card .tray{background:var(--panel)}
.pill{padding:3px 10px;font-size:12px;background:var(--panel);border-radius:11px;cursor:grab}
.card .pill{background:var(--band)}
.pill:active{cursor:grabbing}
.pill[data-in=yes]{opacity:.4}
.pill.drag{opacity:.35}

/* ドット */
.dotwrap{display:flex;gap:22px;flex-wrap:wrap}
/* ⚠️ 列の数と升の大きさは JS が入れる（16 と 64 の両方が来るため） */
#dot{display:grid;gap:1px;background:var(--line);
 padding:1px;width:max-content;touch-action:none}
#dot i{display:block;cursor:crosshair}
.pens{display:flex;flex-direction:column;gap:8px}
.pen{display:flex;align-items:center;gap:8px;cursor:pointer;font-size:12px;color:var(--dim);
 padding:3px 8px 3px 3px;border-radius:14px}
.pen[aria-pressed=true]{background:var(--band);color:var(--ink);font-weight:700}
.pen s{width:22px;height:22px;display:block;border-radius:50%;text-decoration:none}
.pal{display:flex;align-items:center;gap:6px;margin-bottom:6px}
.pal input[type=color]{width:34px;height:26px;padding:0;border:0;background:none;cursor:pointer}
#prev{image-rendering:pixelated;shape-rendering:crispEdges;width:96px;height:96px}
.hint{font-size:12px;color:var(--dim);margin:8px 0 0}
.warn{font-size:12px;color:var(--bad);margin:8px 0 0}
</style></head><body>");
        }

        private static void Body(StringBuilder html)
        {
            html.Append(@"
<div id=top>
  <h1>帳面</h1>
  <span id=self>自己検査</span>
  <span id=snapshot></span>
  <span class=grow></span>
  <label class=file>帳面を開く<input type=file id=load accept='.txt' multiple hidden></label>
  <button id=copy>この1件をコピー</button>
  <button id=save class=go>保存</button>
</div>

<div id=wrap>
  <div id=rail>
    <div class=tabs>
      <button data-tab=skill aria-selected=true>技</button>
      <button data-tab=species>種族</button>
      <button data-tab=trait>特性</button>
    </div>
    <input type=text id=find placeholder='絞り込み'>
    <div id=list></div>
    <div style='display:flex;gap:6px;margin-top:12px'>
      <button id=add class=mini>＋ 新しく作る</button>
      <button id=dup class=mini>複製</button>
      <button id=del class=mini>削除</button>
    </div>
    <p class=hint id=tpl></p>
  </div>
  <div id=main></div>
</div>
");
        }

        // ══ 中身（JS）══════════════════════════════════
        // ⚠️ **JS の中で二重引用符を使わない**（C# の逐語文字列に入れるため）。
        //    単引用符とテンプレートリテラルで書く。

        private static void Script(StringBuilder html)
        {
            html.Append(@"<script>
'use strict';
const $=s=>document.querySelector(s), el=(t,c)=>{const n=document.createElement(t);if(c)n.className=c;return n;};
const FREE=D.freeKind, MEMO=D.memoKey;

// ── 効果の型ごとの札。⚠️ Sheet.cs の ParseEffect と同じ並びにする ──
const ARGS={
 'ダメージ':[['威力','sel','powers'],['依存','sel','scales'],['発数','num',1],['防御無視','chk']],
 // ⚠️ 強化に確率は効かない（Effect.Buff が 100 に固定する）ので欄を出さない
 '強化':[['ステ','sel','buffStats'],['ターン','num',3]],
 '弱化':[['ステ','sel','buffStats'],['ターン','num',3],['確率','num',100]],
 '毒':[['スタック','num',1],['ターン','num',4],['確率','num',100]],
 'リジェネ':[['スタック','num',1],['ターン','num',4],['確率','num',100]],
 'HP割合':[['割合','num',30],['確率','num',100]],
 '蘇生':[['割合','num',50],['確率','num',100]],
 'シールド':[['個数','num',2],['確率','num',100]],
 '解除':[['個数','num',1],['確率','num',100]],
 '強化強奪':[['個数','num',1],['確率','num',100]],
 'スタン':[['ターン','num',1],['確率','num',100]],
 '睡眠':[['ターン','num',2],['確率','num',100]],
 'ブロック':[['ターン','num',2],['確率','num',100]],
 'ガッツ':[['ターン','num',3],['確率','num',100]],
 '免疫':[['ターン','num',3],['確率','num',100]],
 'CT':[['増減','num',2],['確率','num',100]],
 'ゲージ':[['割合','num',25],['確率','num',100]],
 '挑発':[['回数','num',3],['確率','num',100]],
};
// ⭐ **書けないことは言葉で書く。**これが「あらゆる場合に耐える」の正体。
//    ⚠️ 画面は通す。実装した扱いにはしない（✍️ として数える）。
ARGS[FREE]=[['文','free','']];
const KINDS=Object.keys(ARGS);
// ⭐ **前の語も読む。**Sheet.Aliases と同じ表（帳面を壊さないため）
const ALIAS={'割合回復':'HP割合','強化解除':'解除'};
const rename=w=>ALIAS[w]||w;
// ⚠️ **符号まで見る。**種類の名前だけで数えていた頃、CT延長・ゲージ減少・解除 が
//    「相手に効くもの」に数えられず、**実装済みの3技に誤って警告**していた
//    （「型『デバフ』だが中身は『サポート』に見える」／2026-08-19 の監査）。
//    ⭐ Skills.IsHarmful と同じ規則。片方だけ直さないこと。
const HARM=e=>{
  if(typeof e==='string') return false;      // 旧い呼び方は使わない
  if(['弱化','毒','スタン','睡眠','ブロック','強化強奪','挑発'].includes(e.k)) return true;
  if(e.k==='CT') return e.増減>0;
  if(e.k==='ゲージ') return e.割合<0;
  if(e.k==='HP割合') return e.割合<0;
  if(e.k==='解除') return e.個数>0;
  return false;
};
const HEAL=k=>['HP割合','リジェネ','蘇生'].includes(k);
const AT_FOE=t=>t==='敵1体'||t==='敵全体'||t==='敵のだれか1体';
const AT_ALL=t=>t==='敵全体'||t==='味方全体';

// ── 雛形 ──
const TPL={
 '攻撃技':{type:'アタック',ct:3,target:'敵1体',effects:[{k:'ダメージ',威力:'中',依存:'攻撃',発数:1,防御無視:false,確率:100}]},
 '弱化技':{type:'デバフ',ct:4,target:'敵1体',effects:[{k:'弱化',ステ:'攻撃力',ターン:3,確率:70}]},
 '支援技':{type:'サポート',ct:4,target:'味方1体',effects:[{k:'シールド',個数:2,確率:100}]},
 '回復技':{type:'ヒール',ct:4,target:'味方1体',effects:[{k:'HP割合',割合:30,確率:100}]},
 '複合技':{type:'デバフ',ct:4,target:'敵1体',effects:[
   {k:'ダメージ',威力:'小',依存:'攻撃',発数:1,防御無視:false,確率:100},
   {k:'スタン',ターン:1,確率:45}]},
 '速さで殴る':{type:'アタック',ct:3,target:'敵1体',effects:[{k:'ダメージ',威力:'小',依存:'スピード',発数:1,防御無視:false,確率:100}]},
 'ランダム':{type:'アタック',ct:4,target:'敵のだれか1体',effects:[{k:'ダメージ',威力:'小',依存:'攻撃',発数:3,防御無視:false,確率:100}]},
 '味方全体':{type:'ヒール',ct:5,target:'味方全体',effects:[{k:'HP割合',割合:20,確率:100}]},
 '割合で削る':{type:'デバフ',ct:5,target:'敵1体',effects:[{k:'HP割合',割合:-30,確率:60}]},
 '弱化を治す':{type:'ヒール',ct:4,target:'味方1体',effects:[{k:'解除',個数:-2,確率:100}]},
 'まだ書けない':{type:'アタック',ct:4,target:'敵1体',effects:[{k:FREE,文:'ここに、やりたいことを日本語で書く'}]},
};

// ══ 帳面を読む ══════════════════════════════════
// ⚠️ **保存の逆をここに書く。**画面が帳面を読めないと、
//    テキストで書いた書きかけを画面が知らないまま保存し、**消してしまう**。
// ⭐ 全角の ＝ ： ０-９ ＋ － 　 は Sheet.Normalize と同じように直す。
//    ⚠️ 長音「ー」はマイナスにしない（「ダメージ」が壊れる）。
function norm(t){
  let o='';
  for(const c of t){
    if(c>='０'&&c<='９') o+=String.fromCharCode(48+c.charCodeAt(0)-0xFF10);
    else if(c==='＝') o+='='; else if(c==='：') o+=':';
    else if(c==='＋') o+='+'; else if(c==='－'||c==='−') o+='-';
    else if(c==='　') o+=' '; else o+=c;
  }
  return o;
}
function parseLine(text){
  const parts=norm(text).trim().split(/ +/);
  const k=rename(parts[0]);
  if(k===FREE) return {k:FREE,文:norm(text).trim().slice(FREE.length).trim()};
  if(!ARGS[k]) return null;
  const e={k:k}; const tags={};
  for(let i=1;i<parts.length;i++){
    if(parts[i]==='防御無視'){ e.防御無視=true; continue; }
    const c=parts[i].indexOf(':');
    if(c>0) tags[parts[i].slice(0,c)]=parts[i].slice(c+1);
  }
  for(const [n,t,d] of ARGS[k]){
    if(t==='chk'){ e[n]=e[n]||false; continue; }
    if(n in tags) e[n]=(t==='num')?(parseInt(tags[n],10)||0):tags[n];
    else e[n]=(t==='num')?d:(D[d]||[])[d==='powers'?1:0];
  }
  e.確率 = ('確率' in tags) ? (parseInt(tags.確率,10)||100) : 100;
  return e;
}
function parseSheet(text,head){
  const out=[]; let now=null, grid=false;
  for(const raw of (text||'').split('\n')){
    const t=norm(raw).trim();
    if(!t){ grid=false; continue; }
    if(t.startsWith('//')) continue;
    if(grid&&/^[ \t　]/.test(raw)){ now&&now._grid.push(t); continue; }
    grid=false;
    if(t.startsWith('#')){
      const p=t.replace(/^#+/,'').trim().split(/ +/);
      if(p[0]!==head||p.length<2){ now=null; continue; }
      now={id:p[1],_f:[],_grid:[]}; out.push(now); continue;
    }
    const eq=t.indexOf('=');
    if(eq<0||!now) continue;
    const key=t.slice(0,eq).trim(), val=t.slice(eq+1).trim();
    now._f.push([key,val]);
    if(key==='姿') grid=true;
  }
  return out;
}
const one=(b,k,d)=>{ for(const [a,v] of b._f) if(a===k) return v; return d; };
const many=(b,k)=>b._f.filter(([a])=>a===k).map(([,v])=>v);

function toSkill(b){
  // ⚠️ **読めなかった行を捨てない。**捨てていた頃、未知の効果を書いた技が
  //    黙って「ダメージ 威力:中」にすり替わって保存されていた（2026-08-19 の監査）。
  //    ⭐ 読めない行は自由記述として**原文のまま**残し、検査が 🚧 で言う。
  const eff=many(b,'効果').map(t=>parseLine(t)||{k:FREE,文:t,_unread:true});
  return {id:b.id, name:one(b,'名前',''), gist:one(b,'説明',''),
    type:one(b,'型',D.types[0]), ct:parseInt(one(b,'CT','3'),10)||0,
    target:one(b,'狙い',D.targets[0]),
    effects:eff.length?eff:[fresh('ダメージ')], memo:one(b,MEMO,'')};
}
function toSpecies(b){
  const base={};
  for(const tok of one(b,'基礎','').split(/ +/)){
    const c=tok.indexOf(':'); if(c>0) base[tok.slice(0,c)]=parseInt(tok.slice(c+1),10)||0;
  }
  D.stats.forEach(k=>{ if(!(k in base)) base[k]=0; });
  // ⚠️ 古い書き方（型 / 技…）も読む。⭐ 型はもう縛りではないので左側は捨てる
  const pool=v=>{ const i=v.indexOf('/');
    return {pool:(i<0?v:v.slice(i+1)).trim().split(/ +/).filter(Boolean)}; };
  // ⚠️ **黙って16×16に均さない。**均していた頃、24×24 の種族を開くだけで
  //    左上16×16に切り詰められ、保存でそれが書かれていた（2026-08-19 の監査）。
  let sprite=b._grid.slice();
  const odd = sprite.length!==16 || sprite.some(r=>r.length!==16);
  if(!sprite.length) sprite=Array.from({length:16},()=>'.'.repeat(16));   // ⭐ 新しい種族は 16×16 から
  const pals=many(b,'色').map(v=>v.split(/ +/).filter(x=>x.startsWith('#')));
  return {id:b.id, name:one(b,'名前',''), skill1:one(b,'枠1',''), base:base,
    slot2:pool(one(b,'枠2','')), slot3:pool(one(b,'枠3','')),
    sprite:sprite, palettes:pals.length?pals:[['#2e2418','#8fc96e','#c8eaa8','#1a1410']],
    memo:one(b,MEMO,'')};
}
function toTrait(b){
  return {id:b.id, name:one(b,'名前',''), when:one(b,'働く場面',D.whens[0]),
    gist:one(b,'すること',''), pairs:one(b,'噛み合うもの',''), memo:one(b,MEMO,'')};
}

let tab='skill', cur=0;
// ⭐ **帳面があればそれが種。**無ければ実装（初回）。
// ⚠️ **「帳面がある」と「帳面が読めた」を分ける。**
//    一緒にしていた頃、見出しを全角で書いただけで画面が実装57件に戻り、
//    保存で書きかけが全滅した（2026-08-19 の監査）。
let seedFailed=[];
const seed=(text,head,conv,fallback)=>{
  const blocks=parseSheet(text,head);
  if(blocks.length) return blocks.map(conv);
  if((text||'').trim().length) seedFailed.push(head);   // 中身はあるのに1件も読めなかった
  return fallback.map(o=>Object.assign(JSON.parse(JSON.stringify(o)),{memo:''}));
};
const S=seed(D.sheetSkill,'技',toSkill,D.skills);
const P=seed(D.sheetSpecies,'種族',toSpecies,D.species);
const T=seed(D.sheetTrait,'特性',toTrait,D.traits);
const list=()=>tab==='skill'?S:tab==='species'?P:T;

// ══ 手ぶん ══════════════════════════════════════
// ⚠️ Program.ValueOf の写し。⭐ ずれたら上の「自己検査」が赤くなる。
const LATE=0.7, G={ct:0.31,taunt:0.3,guts:1.0,ward:0.3,buff:0.9,revive:3.0};
function dmg(power,atk,def){
  const raw=atk*power/D.c.powerUnit*D.c.damageBase*D.c.defSoften/(D.c.defSoften+def);
  return Math.max(1,Math.floor(raw));
}
function statOf(dep){ return dep==='防御'?D.c.def : dep==='スピード'?D.c.spd : D.c.atk; }
function valueOf(sk){
  const c=D.c, why=[]; let total=0, guessed=false, free=false;
  for(const e of sk.effects){
    let v=0;
    switch(e.k){
      case FREE: free=true; break;
      case 'ダメージ':{
        const hit=dmg(D.powerVals[e.威力],statOf(e.依存),e.防御無視?0:c.def);
        const n=sk.target==='敵全体'?c.party:1;
        v=hit*(e.発数||1)*n/c.one; why.push(`ダメージ ${hit.toLocaleString()}×${e.発数||1}×${n}体`); break;}
      case 'HP割合': v=c.maxHp*Math.abs(e.割合)/100/c.one;
        why.push(`${e.割合>0?'回復':'削り'} 最大HPの${Math.abs(e.割合)}%`); break;
      case '毒': case 'リジェネ':{
        const pct=c.tickPercent*e.スタック*e.ターン;
        v=c.maxHp*pct/100/c.one*LATE; why.push(`${e.k} 最大HPの${pct}%（割引）`); break;}
      case 'シールド': v=e.個数; why.push(`盾${e.個数}枚`); break;
      case 'スタン': v=e.ターン; why.push(`相手の${e.ターン}手を消す`); break;
      case '睡眠': v=e.ターン*0.5; why.push(`相手の${e.ターン}手（殴ると解ける）`); break;
      case '強化': case '弱化':{
        const pct=c.buffPercent/100, sign=e.k==='強化'?1:-1;
        if(e.ステ===D.buffStats[1]){
          const now=c.defSoften/(c.defSoften+c.def);
          const moved=c.defSoften/(c.defSoften+c.def*(1+pct*sign));
          const gap=Math.abs(1-moved/now);
          v=gap*e.ターン*LATE; why.push(`${sign>0?'被ダメ −':'与ダメ +'}${Math.round(gap*100)}% × ${e.ターン}T`);
        } else { v=pct*e.ターン*LATE; why.push(`${e.ステ} ${sign>0?'+':'−'}${c.buffPercent}% × ${e.ターン}T`); }
        break;}
      case 'ゲージ': v=Math.abs(e.割合)/100; why.push(`ゲージ ${e.割合>0?'+':''}${e.割合}%`); break;
      case 'CT': guessed=true; v=Math.abs(e.増減)*G.ct; why.push(`CT ${e.増減>0?'+':''}${e.増減}`); break;
      case '挑発': guessed=true; v=e.回数*G.taunt; why.push(`狙いを${e.回数}回ずらす`); break;
      case 'ガッツ': guessed=true; v=G.guts; why.push('致命傷を1回耐える'); break;
      case '免疫': case 'ブロック': guessed=true; v=e.ターン*G.ward; why.push(`弱化を${e.ターン}T無駄に`); break;
      case '解除': guessed=true; v=Math.abs(e.個数)*G.buff;
        why.push(`${e.個数>0?'強化':'弱化'}を${Math.abs(e.個数)}つ消す`); break;
      case '強化強奪': guessed=true; v=e.個数*G.buff*2; why.push(`強化を${e.個数}つ奪う`); break;
      case '蘇生': guessed=true; v=G.revive; why.push(`HP${e.割合}%で復帰`); break;
    }
    total+=v*(e.確率===undefined?100:e.確率)/100;
  }
  const hasDmg=sk.effects.some(e=>e.k==='ダメージ');
  if(!hasDmg&&AT_ALL(sk.target)) total*=D.c.party;
  return {v:total,why:(guessed?'見積 ':'')+why.join(' ＋ '),free};
}

// ══ 説明の下書き ════════════════════════════════
// ⚠️ **SkillText.Describe の写し。**⭐ 自己検査が57技ぶん突き合わせるので、
//    ずれた瞬間に画面が赤くなる（書式・手ぶんと同じ扱い）。
// ⭐ 文法は固定 ── 「〈狙い先〉〈助詞〉〈効果の文〉…する」。
//    ⚠️ 付与するものは「〜に」、増減させるものは「〜の」、素の攻撃だけ「〜を」。
function nameOf(e){
  if(e.名) return e.名;                      // C# から来た1件はそのまま
  if(e.k==='強化'||e.k==='弱化') return e.ステ+(e.k==='強化'?'UP':'DOWN');
  if(e.k==='CT') return e.増減<0?'CT短縮':'CT延長';
  if(e.k==='ゲージ') return e.割合<0?'ゲージ減少':'ゲージ上昇';
  if(e.k==='HP割合') return e.割合<0?'HP割合ダメージ':'HP割合回復';
  if(e.k==='解除') return e.個数<0?'弱化解除':'強化解除';
  if(e.k==='ダメージ') return '攻撃';
  return e.k;
}
function stateClause(e){
  const name=nameOf(e);
  const c=(e.確率===undefined||e.確率>=100)?'':`${e.確率}%の確率で`;
  const of=(p,b,v,caus)=>({p:p,b:b,v:v,caus:!!caus});
  switch(e.k){
    case 'HP割合':
      return e.割合<0 ? of('の',`HPを${c}${-e.割合}%`,'削減')
                      : of('の',`HPを${c}${e.割合}%`,'回復');
    case '蘇生': return of('を',`${c}HP${e.割合}%で`,'蘇生',true);
    case 'ゲージ': return of('の',`ゲージを${c}${Math.abs(e.割合)}%`,e.割合<0?'減少':'上昇',true);
    case 'CT': return of('の',`全スキルのCTを${c}${Math.abs(e.増減)}`,e.増減<0?'短縮':'延長');
    case '解除':
      return e.個数<0 ? of('の',`弱化を${c}${-e.個数}個`,'解除')
                      : of('の',`強化を${c}${e.個数}個`,'解除');
    case '強化強奪': return of('の',`強化を${c}${e.個数}個`,'強奪');
    case 'シールド': return of('に',`${name}を${c}${e.個数}枚`,'付与');
    case '毒': case 'リジェネ':
      return of('に',`${name}${e.スタック>1?'×'+e.スタック:''}を${c}${e.ターン}T`,'付与');
    case '挑発': return of('に',`${name}を${c}${e.回数}回`,'付与');
    default: return of('に',`${name}を${c}${e.ターン}T`,'付与');
  }
}
function attackClause(e){
  const how=[];
  if(e.依存!=='攻撃') how.push(e.依存+'で伸びる');
  if(e.防御無視) how.push('防御力を無視する');
  const shots=(e.発数||1)>1?`${e.発数}回`:'';
  return {p:how.length?'に':'を',
    b:how.length?how.join('・')+'攻撃を'+shots:shots,
    v:how.length?'':'攻撃', caus:false};
}
function describe(sk){
  const cl=[];
  for(const e of sk.effects) if(e.k!=='ダメージ'&&e.k!==FREE) cl.push(stateClause(e));
  for(const e of sk.effects) if(e.k==='ダメージ') cl.push(attackClause(e));
  if(!cl.length) return '';
  const end=(c,last)=>c.caus ? c.b+c.v+(last?'させる':'させ') : c.b+c.v+(last?'する':'し');
  let t=sk.target+cl[0].p;
  cl.forEach((c,i)=>{ if(i>0) t+='、'; t+=end(c,i===cl.length-1); });
  return t;
}

// ══ 帳面の文字列 ════════════════════════════════
// ⚠️ Sheet.EffectLine / BlockOf の写し。自己検査が突き合わせる。
function lineOf(e){
  if(e.k===FREE) return `${FREE} ${e.文||''}`.trimEnd();
  const t=[e.k]; const p=(n,v)=>t.push(`${n}:${v}`);
  switch(e.k){
    case 'ダメージ': p('威力',e.威力); p('依存',e.依存);
      if((e.発数||1)>1)p('発数',e.発数); if(e.防御無視)t.push('防御無視'); return t.join(' ');
    case '強化': case '弱化': p('ステ',e.ステ); p('ターン',e.ターン); break;
    case '毒': case 'リジェネ': p('スタック',e.スタック); p('ターン',e.ターン); break;
    case '蘇生': p('割合',e.割合); break;
    case 'HP割合': case 'ゲージ': p('割合',(e.割合>0?'+':'')+e.割合); break;
    case 'シールド': case '強化強奪': p('個数',e.個数); break;
    case '解除': p('個数',(e.個数>0?'+':'')+e.個数); break;
    case 'スタン': case '睡眠': case 'ブロック': case 'ガッツ': case '免疫': p('ターン',e.ターン); break;
    case 'CT': p('増減',(e.増減>0?'+':'')+e.増減); break;
    case '挑発': p('回数',e.回数); break;
  }
  if(e.確率<100) p('確率',e.確率);
  return t.join(' ');
}
// ⚠️ **改行を畳む。**帳面は1行1札なので、改行入りのメモは
//    次の行が「札 = 中身の形でない」で 🚧 になり、開き直すと消えていた（2026-08-19 の監査）。
const memoLine=o=>o.memo&&o.memo.trim()
  ?`${MEMO} = ${o.memo.trim().replace(/\s*\n\s*/g,'　')}\n`:'';
function blockOf(s){
  let t=`# 技 ${s.id}\n名前 = ${s.name}\n説明 = ${s.gist}\n`;
  t+=`型 = ${s.type}\nCT = ${s.ct}\n狙い = ${s.target}\n`;
  for(const e of s.effects) t+=`効果 = ${lineOf(e)}\n`;
  return t;
}
function blockOfSp(p){
  let t=`# 種族 ${p.id}\n名前 = ${p.name}\n枠1 = ${p.skill1}\n基礎 = `;
  t+=D.stats.map(k=>`${k}:${p.base[k]}`).join(' ')+'\n';
  t+=`枠2 = ${p.slot2.pool.join(' ')}\n`;
  t+=`枠3 = ${p.slot3.pool.join(' ')}\n姿 =\n`;
  for(const r of p.sprite) t+='  '+r+'\n';
  for(const pal of p.palettes) t+=`色 = ${pal.join(' ')}\n`;
  return t;
}
function blockOfTr(t){
  return `# 特性 ${t.id}\n名前 = ${t.name}\n働く場面 = ${t.when}\n`
    +`すること = ${t.gist}\n噛み合うもの = ${t.pairs}\n`;
}
// ⭐ 保存するのは「本体 ＋ メモ」。自己検査は本体だけを突き合わせる
const outOf=o=>(tab==='skill'?blockOf(o):tab==='species'?blockOfSp(o):blockOfTr(o))+memoLine(o);

// ══ 自己検査 ════════════════════════════════════
// ⭐ **C# が出した答えと突き合わせる。**ずれたら画面が赤くなる。
(function(){
  const bad=[];
  for(const s of D.skills){
    if(blockOf(s)!==s.block) bad.push(`${s.name}: 書式`);
    if(describe(s)!==s.says) bad.push(`${s.name}: 説明文`);
    // ⚠️ **緩い許容にしない。**0.005 にしていたとき、スピード依存を防御で測る
    //    という実バグの差が 0.0049 で、**ぎりぎり素通り**していた（2026-08-19 の監査）。
    //    ⭐ 同じ式を写しているので、丸め誤差ぶんだけ許せばよい。
    if(Math.abs(valueOf(s).v-s.value)>1e-9*Math.max(1,s.value)) bad.push(`${s.name}: 手ぶん`);
  }
  for(const t of D.traits) if(blockOfTr(t)!==t.block) bad.push(`${t.name}: 書式`);
  // ⭐ **読んで書いて元に戻るか。**戻らなければ、帳面を開くだけで中身が変わる。
  for(const s of D.skills){
    const back=parseSheet(s.block,'技').map(toSkill)[0];
    if(!back||blockOf(back)!==s.block) bad.push(`${s.name}: 読み書き`);
  }
  for(const t of D.traits){
    const back=parseSheet(t.block,'特性').map(toTrait)[0];
    if(!back||blockOfTr(back)!==t.block) bad.push(`${t.name}: 読み書き`);
  }
  const n=$('#self');
  // ⚠️ 帳面が読めなかったときは、**保存を止める**（上書きで消えるため）
  if(seedFailed.length){
    n.className='ng';
    n.textContent=`🚧 ${seedFailed.join('・')}の帳面を読めませんでした ── 保存しないでください`;
    $('#save').disabled=true; $('#save').textContent='保存できません';
    return;
  }
  if(!D.sheetSkill&&!D.sheetSpecies&&!D.sheetTrait){
    n.className='ng';
    n.textContent='🚧 帳面が見つかりません（別の場所で sheet html を打った？）── 保存しないでください';
    $('#save').disabled=true; $('#save').textContent='保存できません';
    return;
  }
  if(bad.length){ n.className='ng'; n.textContent=`🚧 自己検査 ${bad.length}件ずれ: `+bad.slice(0,3).join(' / '); }
  else n.textContent=`⭐ 自己検査 ${D.skills.length+D.traits.length}件一致`;
})();

// ⚠️ **常に見える場所に「いつの写しか」を出す。**ブラウザからは実物のディスクを
//    読めないので、保存前に現物と突き合わせることはできない。せめて
//    「この頁がいつの sheets/*.txt を写したものか」を毎回思い出せるようにする
//    （2026-08-23 の監査 ── 開いたままの古い頁で保存すると、あとから手で足した行が消える）。
if(D.snapshot) $('#snapshot').textContent=`写し: ${D.snapshot.at} 時点`;

// ══ 検査 ════════════════════════════════════════
// ⚠️ **3色に分ける。**🚧＝実装が受け取れない／✍️＝手で書く必要がある／⚠️＝通るが疑わしい。
//    ⭐ 上限で書き手を縛らない（作者の指示 2026-08-19）。⚠️ は理由を添えて言うだけ。
function heavyCt(s){
  return s.effects.some(e=>e.k==='蘇生'
    || (e.k==='ダメージ'&&s.target==='敵全体'
        &&(['大','特大'].includes(e.威力)||(e.発数||1)>1)));
}
function checkSkill(s,i){
  const stop=[],say=[],todo=[];
  if(!/^[a-z0-9-]+$/.test(s.id)) stop.push('id は英小文字・数字・ハイフンで');
  if(S.some((o,j)=>j!==i&&o.id===s.id)) stop.push('id が重複');
  if(!s.name) stop.push('名前が空');
  if(!s.effects.length) stop.push('効果が1つも無い');
  if(S.some((o,j)=>j!==i&&o.name===s.name)) say.push('名前が重複（画面にそのまま出ます）');
  if(!s.gist) say.push('説明が空（画面にそのまま出ます）');
  if(s.ct<0) stop.push('CT が負');
  else {
    const cap=heavyCt(s)?D.c.ctHeavy:D.c.ctCap;
    if(s.ct>cap) say.push(`CT ${s.ct} ── 1体が動けるのは1戦闘でおよそ 5.6手なので、`
      +`${cap} を超えると1戦闘に1回しか撃てません`);
  }
  for(const e of s.effects){
    if(e.k===FREE){
      if(e._unread) stop.push(`読めなかった行をそのまま持っています: ${e.文}`);
      else if(!(e.文||'').trim()) stop.push('自由記述が空');
      else todo.push(`実装が要る: ${e.文}`);
      continue;
    }
    if(e.確率<D.c.minChance||e.確率>100)
      stop.push(`確率は ${D.c.minChance}〜100（${D.c.minChance} 未満は実装が切り上げます）`);
    if(e.k==='CT'&&e.増減===0) say.push('CT の増減が 0 ── 何も起きません');
    if(e.k==='ゲージ'&&e.割合===0) say.push('ゲージの割合が 0 ── 何も起きません');
  }
  if(s.memo&&s.memo.trim()) todo.push(`メモ: ${s.memo.trim()}`);
  const real=s.effects.filter(e=>e.k!==FREE);
  const harm=real.some(HARM);
  const kind=real.some(e=>e.k!=='ダメージ'&&!HARM(e));
  if(harm&&!AT_FOE(s.target)) say.push('弱化なのに狙いが敵でない');
  if(kind&&!harm&&AT_FOE(s.target)) say.push('味方に効くものを敵へ向けている');
  const dmg=real.some(e=>e.k==='ダメージ');
  let want=null;
  if(dmg&&!harm&&!kind) want='アタック';
  else if(!dmg&&harm&&!kind) want='デバフ';
  else if(!dmg&&!harm&&real.length&&real.every(e=>HEAL(e.k))) want='ヒール';
  else if(!dmg&&!harm&&kind&&!real.some(e=>HEAL(e.k))) want='サポート';
  if(want&&want!==s.type) say.push(`型「${s.type}」だが中身は「${want}」に見える（枠から出ません）`);
  return {stop,say,todo};
}
function checkSp(sp,i){
  const stop=[],say=[],todo=[];
  if(!/^[a-z0-9-]+$/.test(sp.id)) stop.push('id は英小文字・数字・ハイフンで');
  if(P.some((o,j)=>j!==i&&o.id===sp.id)) stop.push('id が重複');
  if(!sp.name) stop.push('名前が空');
  if(P.some((o,j)=>j!==i&&o.name===sp.name)) say.push('名前が重複');
  const tot=D.stats.reduce((a,k)=>a+(+sp.base[k]||0),0);
  if(tot!==D.c.baseTotal) say.push(`基礎の合計が ${tot}（他は ${D.c.baseTotal}）`
    +' ── このまま貼ると SpeciesTable.Audit が落ちます');
  const pair=(+sp.base[D.stats[4]]||0)+(+sp.base[D.stats[5]]||0);
  if(pair!==D.c.debuffTotal) say.push(`弱化2本が ${pair}（他は ${D.c.debuffTotal}）── 同上`);
  if(!S.some(s=>s.id===sp.skill1)) stop.push(`枠1 の ${sp.skill1} が無い`);
  const roles=new Set();
  for(const [nm,sl] of [['枠2',sp.slot2],['枠3',sp.slot3]]){
    if(!sl.pool.length){ stop.push(`${nm} が空`); continue; }
    if(sl.pool.length>D.c.poolMax) stop.push(`${nm} が ${sl.pool.length} 件（上限 ${D.c.poolMax}）`);
    if(sl.pool.filter(id=>id!==sp.skill1).length<1) stop.push(`${nm} は枠1 を除くと空`);
    if(new Set(sl.pool).size!==sl.pool.length) stop.push(`${nm} に同じ技が2回`);
    for(const id of sl.pool){
      const f=S.find(s=>s.id===id);
      if(!f) stop.push(`${nm}: ${id} が無い`); else roles.add(f.type);
    }
  }
  // ⚠️ 型の縛りを外した代わりに数える3つ
  for(const id of sp.slot2.pool)
    if(sp.slot3.pool.includes(id)) stop.push(`${id} が枠2 と枠3 の両方に居る`);
  if(roles.size<2) say.push('2つの袋が同じ役割しか持たない（分けた意味が無い）');
  const homes=spreadCount();
  for(const id of [...sp.slot2.pool,...sp.slot3.pool])
    if((homes[id]||0)>D.c.spreadMax)
      say.push(`${id} が ${homes[id]} か所の袋に居る（上限 ${D.c.spreadMax}）── どこで奪っても同じになる`);
  if(sp.sprite.length!==16||sp.sprite.some(r=>r.length!==16))
    stop.push(`姿が ${sp.sprite[0]?sp.sprite[0].length:0}×${sp.sprite.length}`
      +' ── この画面は16×16しか塗れません（テキストで直してください）');
  // ⚠️ 姿の添字が色数を超えていないか（遊びで描いた瞬間に落ちる）
  const big=Math.max(0,...sp.sprite.join('').split('').map(c=>c>='1'&&c<='9'?+c:0));
  if(sp.palettes.some(p=>big>p.length))
    stop.push(`姿が色 ${big} 番を使っているのに、色が ${Math.min(...sp.palettes.map(p=>p.length))} つしかない`);
  if(sp.palettes.some(p=>p.length!==4))
    stop.push('色は1行に4つ');
  if(!sp.palettes.length) stop.push('色が1組も無い');
  if(sp.memo&&sp.memo.trim()) todo.push(`メモ: ${sp.memo.trim()}`);
  return {stop,say,todo};
}
function checkTr(t,i){
  const stop=[],say=[],todo=[];
  if(!/^[a-z0-9-]+$/.test(t.id)) stop.push('id は英小文字・数字・ハイフンで');
  if(T.some((o,j)=>j!==i&&o.id===t.id)) stop.push('id が重複');
  if(!t.name) stop.push('名前が空');
  if(!t.gist) say.push('すること が空（画面に出ます）');
  if(!D.whens.includes(t.when))
    todo.push(`場面「${t.when}」は Battle.React に無い ── TraitWhen に足して割り込み先を作る必要があります`);
  if(!D.traits.some(o=>o.id===t.id))
    todo.push(`効き目を Battle.React に手で書く必要があります: ${t.gist||'（未記入）'}`);
  if(t.memo&&t.memo.trim()) todo.push(`メモ: ${t.memo.trim()}`);
  return {stop,say,todo};
}
const checkOf=(o,i)=>tab==='skill'?checkSkill(o,i):tab==='species'?checkSp(o,i):checkTr(o,i);

// ══ 描く ════════════════════════════════════════
function band(){
  let lo=1e9,hi=0,ln='',hn='';
  for(const s of S){const v=valueOf(s).v; if(v<lo){lo=v;ln=s.name;} if(v>hi){hi=v;hn=s.name;}}
  return {lo,hi,ln,hn};
}
function drawList(){
  const q=$('#find').value.trim(), box=$('#list'); box.innerHTML='';
  list().forEach((o,i)=>{
    if(q&&!(o.name+o.id).includes(q)) return;
    const r=el('div','row'+(born.has(tab+o.id)?' fresh':''));
    if(i===cur) r.setAttribute('aria-current','true');
    const n=el('span','n'); n.textContent=o.name||'(名前なし)';
    const v=el('span','v');
    v.textContent=tab==='skill'?valueOf(o).v.toFixed(2)
      :tab==='species'?D.stats.reduce((a,k)=>a+(+o.base[k]||0),0):'';
    r.append(n,v); r.onclick=()=>{cur=i;draw();}; box.append(r);
  });
}
// ⚠️ **打つたびに画面を作り直さない。**作り直していた頃、1文字入れるごとに
//    入力欄が作り直されて**フォーカスが外れ、まともに書けなかった**（2026-08-19・作者の指摘）。
// ⭐ 打っている間に変えるのは「そこから導かれるもの」だけ ── 判定・下書き・左の一覧。
//    ⚠️ 形が変わるとき（効果を足す・種類を変える・別の1件を開く）だけ draw() を呼ぶ。
function field(lab,val,on,type){
  const w=el('div'); const l=el('label'); l.textContent=lab;
  const i=el('input'); i.type=type||'text'; i.value=val;
  i.oninput=()=>{ on(type==='number'?(+i.value||0):i.value); echo(); };
  w.append(l,i); return w;
}
function area(lab,val,on,rows){
  const w=el('div'); const l=el('label'); l.textContent=lab;
  const t=el('textarea'); t.rows=rows||2; t.value=val||'';
  t.oninput=()=>{ on(t.value); echo(); };
  w.append(l,t); return w;
}

/// 打った内容から導かれるものだけを描き直す。⭐ 入力欄には触らない
function echo(){
  touched=true; dirty[tab]=true;
  const o=list()[cur]; if(!o) return;

  // ① 判定（🚧 ✍️ ⚠️）
  const old=$('#judge');
  if(old){
    const keep=old.firstChild && !old.firstChild.classList.contains('probs')
      ? old.firstChild : null;
    const now=judge(checkOf(o,cur), keep);
    old.replaceWith(now);
  }
  // ② 帳面の下書き
  const pre=document.querySelector('#main textarea[readonly]');
  if(pre) pre.value=outOf(o);
  // ③ 左の一覧（名前と手ぶん）
  const row=document.querySelector('#list .row[aria-current=true]');
  if(row){
    row.querySelector('.n').textContent=o.name||'(名前なし)';
    const v=row.querySelector('.v');
    if(v) v.textContent = tab==='skill' ? valueOf(o).v.toFixed(2)
      : tab==='species' ? D.stats.reduce((a,k)=>a+(+o.base[k]||0),0) : '';
  }
  // ④ 手ぶんの大きな数
  if(tab==='skill'){
    const b=$('#judge .val b'); if(b) b.textContent=valueOf(o).v.toFixed(2);
  }
  // ⑤ 説明の下書き（技のみ）
  const auto=$('#auto'); if(auto&&tab==='skill') auto.textContent=describe(o);
}
function pick(lab,val,opts,on){
  const w=el('div'); const l=el('label'); l.textContent=lab;
  const s=el('select');
  for(const o of opts){const p=el('option');p.value=o;p.textContent=o;if(o===val)p.selected=true;s.append(p);}
  // ⭐ 一覧に無い値でも捨てない（帳面から来た未知の語を消さないため）
  if(!opts.includes(val)){const p=el('option');p.value=val;p.textContent=val+'（一覧に無い）';p.selected=true;s.append(p);}
  s.onchange=()=>on(s.value);
  w.append(l,s); return w;
}
function judge(r,extra){
  const c=el('div','card'); c.id='judge';
  if(extra) c.append(extra);
  const ul=el('ul','probs');
  const put=(cls,mark,arr)=>arr.forEach(p=>{const li=el('li',cls);li.textContent=mark+p;ul.append(li);});
  put('stop','🚧 ',r.stop); put('todo','✍️ ',r.todo); put('','⚠️ ',r.say);
  if(!r.stop.length&&!r.say.length&&!r.todo.length){
    const li=el('li','ok');li.textContent='⭐ 問題なし';ul.append(li);
  }
  c.append(ul);
  const legend=el('p','hint');
  legend.textContent='🚧 実装に入れられない　✍️ 手で書く必要がある　⚠️ 通るが気になる';
  c.append(legend);
  return c;
}
function memoCard(o){
  const c=el('div','card');
  const h=el('h2'); h.textContent='メモ　⭐ 書式で書けないことは、ここに日本語で'; c.append(h);
  c.append(area('',o.memo,x=>{o.memo=x;},3));
  const p=el('p','hint');
  p.textContent='⭐ 保存すると「メモ = 〜」の行になります。実装するとき読みます。'
    +'⚠️ sim sheet write を打っても消えません。';
  c.append(p); return c;
}


function drawSkill(){
  const s=S[cur]; const m=$('#main'); m.innerHTML='';
  const {v,why,free}=valueOf(s), b=band();

  const g=el('div'); const vv=el('div','val');
  const bb=el('b'); bb.textContent=v.toFixed(2);
  const uu=el('span','u'); uu.textContent='手ぶん　⭐ 1.00 が「枠1 で殴るのと同じ」';
  vv.append(bb,uu);
  const bar=el('div','bar');
  const fill=el('i'); fill.style.width=Math.min(100,v/Math.max(b.hi,v)*100)+'%';
  const mark=el('u'); mark.style.left=Math.min(100,1/Math.max(b.hi,v)*100)+'%';
  bar.append(fill,mark);
  const wy=el('p','why');
  wy.textContent=(free?'✍️ 自由記述ぶんは数えていません　／　':'')
    +`${why}　／　いま在る${S.length}技の帯 ${b.lo.toFixed(2)}（${b.ln}）〜 ${b.hi.toFixed(2)}（${b.hn}）`;
  g.append(vv,bar,wy);
  m.append(judge(checkSkill(s,cur),g));

  const c1=el('div','card');
  const h=el('h2'); h.textContent='だれが何をする技か'; c1.append(h);
  const gr=el('div','grid g3');
  gr.append(field('id（英数字）',s.id,x=>{s.id=x;}));
  gr.append(field('名前',s.name,x=>{s.name=x;}));
  gr.append(field('説明（画面に出ます）',s.gist,x=>{s.gist=x;}));
  gr.append(pick('型',s.type,D.types,x=>{s.type=x;draw();}));
  gr.append(pick('狙い',s.target,D.targets,x=>{s.target=x;draw();}));
  // ⚠️ **CT を選択肢で縛らない**（作者の指示 2026-08-19）。いくつでも書ける
  gr.append(field('CT（いくつでも可）',s.ct,x=>{s.ct=x;},'number'));
  c1.append(gr);

  // ⭐ **説明の下書き。**効果から組み立てた文をそのまま出す。
  // ⚠️ 押すまで説明欄には入れない ── 手で書いた文を勝手に上書きしない。
  const draft=el('div'); draft.style.marginTop='16px';
  const dl2=el('label'); dl2.textContent='効果から組むとこうなります';
  const line=el('div'); line.style.display='flex'; line.style.gap='10px'; line.style.alignItems='baseline';
  const auto=el('span','auto'); auto.id='auto'; auto.textContent=describe(s);
  const use=el('button','mini'); use.textContent='この文にする';
  use.onclick=()=>{ s.gist=describe(s); draw(); };
  line.append(auto,use);
  draft.append(dl2,line);
  c1.append(draft);
  m.append(c1);

  const c2=el('div','card');
  const h2=el('h2'); h2.textContent='効果（上から順に効きます）'; c2.append(h2);
  s.effects.forEach((e,i)=>c2.append(effRow(s,e,i)));
  const add=el('button','mini'); add.textContent='＋ 効果を足す';
  add.onclick=()=>{s.effects.push(fresh('ダメージ'));draw();};
  const addF=el('button','mini'); addF.textContent='＋ '+FREE; addF.style.marginLeft='6px';
  addF.onclick=()=>{s.effects.push(fresh(FREE));draw();};
  c2.append(add,addF); m.append(c2);

  m.append(memoCard(s));
  m.append(preview(outOf(s),7+s.effects.length));
}
function preview(text,rows){
  const c=el('div','card');
  const h=el('h2'); h.textContent='帳面ではこう書かれます'; c.append(h);
  const pre=el('textarea'); pre.rows=rows; pre.readOnly=true; pre.value=text;
  pre.style.fontFamily='ui-monospace,monospace'; pre.style.fontSize='12px';
  c.append(pre); return c;
}
function fresh(kind){
  const e={k:kind};
  if(kind!==FREE) e.確率=100;
  for(const [n,t,d] of ARGS[kind]){
    if(t==='num') e[n]=d;
    else if(t==='chk') e[n]=false;
    else if(t==='free') e[n]='';
    else e[n]=(D[d]||[])[d==='powers'?1:0];
  }
  return e;
}
function effRow(s,e,i){
  const w=el('div','eff'+(e.k===FREE?' free':''));
  const hd=el('div','head');
  const sel=el('select');
  for(const k of KINDS){const o=el('option');o.value=k;o.textContent=k;if(k===e.k)o.selected=true;sel.append(o);}
  sel.onchange=()=>{s.effects[i]=fresh(sel.value);draw();};
  const sp=el('span','sp');
  const up=el('button','mini'); up.textContent='↑'; up.onclick=()=>{
    if(i>0){[s.effects[i-1],s.effects[i]]=[s.effects[i],s.effects[i-1]];draw();}};
  const rm=el('button','mini'); rm.textContent='削除';
  rm.onclick=()=>{s.effects.splice(i,1);draw();};
  hd.append(sel,sp,up,rm); w.append(hd);

  const ar=el('div','args');
  for(const [n,t,d] of ARGS[e.k]){
    if(t==='chk'){
      const lab=el('label','chk'); const cb=el('input'); cb.type='checkbox'; cb.checked=!!e[n];
      cb.onchange=()=>{e[n]=cb.checked;draw();};
      lab.append(cb,document.createTextNode(n)); ar.append(lab); continue;
    }
    if(t==='free'){
      const box=el('div'); box.style.flex='1';
      const l=el('label'); l.textContent='やりたいこと（日本語でよい）';
      const ip=el('input'); ip.type='text'; ip.value=e[n]||'';
      ip.placeholder='例: 相手が毒なら威力1.5倍 / 倒れた味方の数だけ強くなる';
      ip.oninput=()=>{e[n]=ip.value;echo();};
      box.append(l,ip); ar.append(box); continue;
    }
    const box=el('div','a');
    if(t==='sel') box.append(pick(n,e[n],D[d]||[],x=>{e[n]=x;draw();}));
    else box.append(field(n,e[n],x=>{e[n]=x;},'number'));
    ar.append(box);
  }
  w.append(ar); return w;
}

function drawSpecies(){
  const p=P[cur]; const m=$('#main'); m.innerHTML='';
  const tot=D.stats.reduce((a,k)=>a+(+p.base[k]||0),0);
  const pair=(+p.base[D.stats[4]]||0)+(+p.base[D.stats[5]]||0);
  const sum=el('p','why');
  sum.textContent=`合計 ${tot} / ${D.c.baseTotal}　　弱化2本 ${pair} / ${D.c.debuffTotal}`;
  m.append(judge(checkSp(p,cur),sum));

  const c1=el('div','card');
  const h=el('h2'); h.textContent='だれか'; c1.append(h);
  const g=el('div','grid g3');
  g.append(field('id（英数字）',p.id,x=>{p.id=x;}));
  g.append(field('名前',p.name,x=>{p.name=x;}));
  g.append(pick('枠1（通常攻撃）',p.skill1,S.map(s=>s.id),x=>{p.skill1=x;draw();}));
  c1.append(g);
  const h2=el('h2'); h2.textContent='基礎ステ'; h2.style.marginTop='18px'; c1.append(h2);
  const g2=el('div','grid g6');
  for(const k of D.stats) g2.append(field(k,p.base[k],x=>{p.base[k]=x;},'number'));
  c1.append(g2); m.append(c1);

  m.append(bagCard(p));

  const c3=el('div','card');
  const h3=el('h2'); h3.textContent='姿　⭐ 押して塗る（引きずれます）'; c3.append(h3);
  const wrap=el('div','dotwrap');
  const grid=el('div'); grid.id='dot';
  const pal=p.palettes[0]||['#000','#888','#ccc','#fff'];
  // ⭐ **大きさは絵から読む**（16 と 64 の両方が来る）。⚠️ 16 を焼き付けない
  const N=p.sprite.length, CELL=Math.max(6,Math.floor(320/N));
  const DIG=D.dotDigits||'123456789abcdef';
  grid.style.gridTemplateColumns='repeat('+N+','+CELL+'px)';
  let pen=window.__pen===undefined?2:window.__pen;
  let down=false, dirty=false;
  const cells=[];
  const tint=ch=>ch==='.'?'var(--bg)':(pal[DIG.indexOf(ch)]||'#f0f');
  const paint=(x,y)=>{
    const ch=pen===0?'.':DIG[pen-1];
    if(p.sprite[y][x]===ch) return;
    const r=p.sprite[y].split(''); r[x]=ch; p.sprite[y]=r.join('');
    cells[y*N+x].style.background=tint(ch);
    dirty=true; drawPrev();
  };
  function redrawDot(){
    grid.innerHTML=''; cells.length=0;
    for(let y=0;y<N;y++) for(let x=0;x<N;x++){
      const i=el('i');
      i.style.width=CELL+'px'; i.style.height=CELL+'px';
      i.style.background=tint(p.sprite[y][x]);
      i.onpointerdown=ev=>{ev.preventDefault();down=true;paint(x,y);};
      i.onpointerenter=()=>{if(down)paint(x,y);};
      cells.push(i); grid.append(i);
    }
    drawPrev();
  }
  document.onpointerup=()=>{ down=false; if(dirty){dirty=false;draw();} };
  const pens=el('div','pens');
  // ⭐ **筆は色の数だけ並べる**（4色の種族は4本・11色の種族は11本）。
  // ⚠️ 名前が付くのは 16×16 の決めごと（1=輪郭 2=体 3=差し色 4=目）の4本まで
  const NAMED=['輪郭','体','差し色','目'];
  const penList=[[0,'透明','var(--bg)']];
  for(let n=1;n<=pal.length;n++) penList.push([n,NAMED[n-1]||DIG[n-1],pal[n-1]]);
  penList.forEach(([n,lab,col])=>{
    const b=el('div','pen'); b.setAttribute('aria-pressed',pen===n?'true':'false');
    const sw=el('s'); sw.style.background=col; if(n===0)sw.style.boxShadow='inset 0 0 0 2px var(--line)';
    b.append(sw,document.createTextNode(lab));
    b.onclick=()=>{window.__pen=pen=n;draw();};
    pens.append(b);
  });
  const side=el('div');
  const pv=el('canvas'); pv.id='prev'; pv.width=N; pv.height=N;
  function drawPrev(){
    const g2=pv.getContext('2d'); g2.clearRect(0,0,N,N);
    for(let y=0;y<N;y++) for(let x=0;x<N;x++){
      const ch=p.sprite[y][x]; if(ch==='.') continue;
      g2.fillStyle=pal[DIG.indexOf(ch)]||'#f0f'; g2.fillRect(x,y,1,1);
    }
  }
  const tools=el('div'); tools.style.marginTop='10px';
  const clr=el('button','mini'); clr.textContent='全部消す';
  clr.onclick=()=>{p.sprite=Array.from({length:N},()=>'.'.repeat(N));draw();};
  const flip=el('button','mini'); flip.textContent='左右反転'; flip.style.marginLeft='6px';
  flip.onclick=()=>{p.sprite=p.sprite.map(r=>r.split('').reverse().join(''));draw();};
  tools.append(clr,flip);
  side.append(pv,tools);
  wrap.append(grid,pens,side); c3.append(wrap);
  const hint=el('p','hint');
  hint.textContent=N+'×'+N+'・色'+pal.length+'。⭐ 色は下の1組目が塗りに使われます'
    +(pal.length===4?'（1=輪郭 2=体 3=差し色 4=目）':'');
  c3.append(hint); m.append(c3);
  redrawDot();

  const c4=el('div','card');
  const h4=el('h2'); h4.textContent='色　⭐ 1組目が通常色、2組目からが変異色'; c4.append(h4);
  p.palettes.forEach((row,i)=>{
    const r=el('div','pal');
    row.forEach((col,j)=>{
      const ip=el('input'); ip.type='color'; ip.value=col;
      ip.oninput=()=>{row[j]=ip.value;draw();};
      r.append(ip);
    });
    const lab=el('span','why'); lab.textContent=i===0?'通常':'変異'+i;
    const rm=el('button','mini'); rm.textContent='削除';
    rm.onclick=()=>{p.palettes.splice(i,1);draw();};
    r.append(lab,rm); c4.append(r);
  });
  const addp=el('button','mini'); addp.textContent='＋ 色を足す';
  // ⚠️ **1組目と同じ長さで足す。**⭐ 数が揃っていないと、変異させた瞬間に落ちる
  addp.onclick=()=>{p.palettes.push([...(p.palettes[0]||['#000000','#888888','#cccccc','#ffffff'])]);draw();};
  c4.append(addp); m.append(c4);

  m.append(memoCard(p));
  m.append(preview(outOf(p),26));
}

/// 枠2・枠3 をドラッグ&ドロップで組む。
/// ⚠️ **型で絞らない**（2026-08-19 に袋の型縛りを外した）。全技が棚に並ぶ。
/// ⭐ 型は「顔つき」として袋の見出しに出るだけ（Skills.FlavorOf の写し）。
function flavorOf(ids){
  const kinds=[];
  for(const id of ids){
    const f=S.find(x=>x.id===id);
    if(f&&!kinds.includes(f.type)) kinds.push(f.type);
  }
  return kinds.length?kinds.join('・'):'（空）';
}
function bagCard(p){
  const c=el('div','card');
  const h=el('h2');
  h.textContent='枠2・枠3　⭐ 棚から袋へドラッグ、袋から棚へ戻すと外れます';
  c.append(h);

  const bags=el('div','bags');
  const homes=spreadCount();
  let dragging=null;                       // {id, from:'shelf'|2|3}

  function bagOf(which){
    const sl = which===2 ? p.slot2 : p.slot3;
    const box=el('div','bag'+(sl.pool.length>=D.c.poolMax?' full':''));
    const t=el('h3'); t.textContent=`枠${which}　${sl.pool.length} / ${D.c.poolMax}`;
    const f=el('p','flavor'); f.textContent=flavorOf(sl.pool);
    box.append(t,f);

    if(!sl.pool.length){
      const e=el('div','empty'); e.textContent='ここへドラッグ';
      box.append(e);
    }
    sl.pool.forEach((id,i)=>{
      const sk=S.find(x=>x.id===id);
      const row=el('div','slot'); row.draggable=true;
      const g=el('span','g'); g.textContent='⠿';
      const n=el('span','n'); n.textContent=sk?sk.name:id+'（無い技）';
      const v=el('span','v'); v.textContent=sk?valueOf(sk).v.toFixed(2):'';
      const x=el('button','mini'); x.textContent='外す';
      x.onclick=()=>{sl.pool.splice(i,1);draw();};
      row.ondragstart=ev=>{dragging={id:id,from:which};row.classList.add('drag');
        ev.dataTransfer.effectAllowed='move';ev.dataTransfer.setData('text/plain',id);};
      row.ondragend=()=>row.classList.remove('drag');
      row.append(g,n,v,x);
      box.append(row);
    });

    box.ondragover=ev=>{
      if(!dragging) return;
      if(dragging.from===which) return;                       // 同じ袋の中は無視
      if(sl.pool.length>=D.c.poolMax&&dragging.from==='shelf') return;
      ev.preventDefault(); ev.dataTransfer.dropEffect='move';
      box.classList.add('over');
    };
    box.ondragleave=()=>box.classList.remove('over');
    box.ondrop=ev=>{
      ev.preventDefault(); box.classList.remove('over');
      if(!dragging) return;
      const id=dragging.id;
      // ⚠️ もう片方の袋から来たなら、そちらから抜く（同じ技が2枠に居ないように）
      if(dragging.from!=='shelf'){
        const other = dragging.from===2 ? p.slot2 : p.slot3;
        const i=other.pool.indexOf(id);
        if(i>=0) other.pool.splice(i,1);
      }
      if(!sl.pool.includes(id)) sl.pool.push(id);
      dragging=null; draw();
    };
    return box;
  }
  bags.append(bagOf(2),bagOf(3));
  c.append(bags);

  // ── 棚（全技） ──
  const shelf=el('div','shelf');
  const sh=el('h2'); sh.textContent='棚　⭐ ここから袋へドラッグ'; shelf.append(sh);
  const find=el('input','find'); find.type='text'; find.placeholder='技を絞り込む';
  shelf.append(find);
  const tray=el('div','tray');
  function fillTray(){
    tray.innerHTML='';
    const q=find.value.trim();
    for(const sk of S){
      if(q&&!(sk.name+sk.id+sk.type).includes(q)) continue;
      const inBag = p.slot2.pool.includes(sk.id)||p.slot3.pool.includes(sk.id);
      const b=el('span','pill'); b.draggable=true;
      b.textContent=sk.name;
      b.title=`${sk.type}／${valueOf(sk).v.toFixed(2)} 手ぶん／いま ${homes[sk.id]||0} か所`;
      if(inBag) b.dataset.in='yes';
      // ⚠️ 入れすぎの技は入口で言う（あとで検査に叱られるより早い）
      if((homes[sk.id]||0)>=D.c.spreadMax&&!inBag) b.style.textDecoration='line-through';
      b.ondragstart=ev=>{dragging={id:sk.id,from:'shelf'};b.classList.add('drag');
        ev.dataTransfer.effectAllowed='copy';ev.dataTransfer.setData('text/plain',sk.id);};
      b.ondragend=()=>b.classList.remove('drag');
      // ⭐ ドラッグできない人のために、押しても入る（枠2が空いていれば枠2へ）
      b.onclick=()=>{
        if(inBag) return;
        const sl = p.slot2.pool.length<D.c.poolMax ? p.slot2 : p.slot3;
        if(sl.pool.length>=D.c.poolMax) return;
        sl.pool.push(sk.id); draw();
      };
      tray.append(b);
    }
  }
  find.oninput=fillTray;
  fillTray();
  shelf.append(tray);

  // ⭐ 棚へ戻すと外れる
  shelf.ondragover=ev=>{ if(dragging&&dragging.from!=='shelf'){ev.preventDefault();
    ev.dataTransfer.dropEffect='move'; tray.classList.add('over');} };
  shelf.ondragleave=()=>tray.classList.remove('over');
  shelf.ondrop=ev=>{
    ev.preventDefault(); tray.classList.remove('over');
    if(!dragging||dragging.from==='shelf') return;
    const sl = dragging.from===2 ? p.slot2 : p.slot3;
    const i=sl.pool.indexOf(dragging.id);
    if(i>=0) sl.pool.splice(i,1);
    dragging=null; draw();
  };
  c.append(shelf);

  const hint=el('p','hint');
  hint.textContent=`⭐ 袋は ${D.c.poolMax} 件まで。狙える確率は 1/(枠2 × 枠3) で決まります`
    +`　⚠️ 1つの技を入れてよい袋は ${D.c.spreadMax} か所まで（取り消し線はもう上限）`;
  c.append(hint);
  return c;
}

/// いまその技が何か所の袋に居るか。⚠️ 自分の種族も数える
function spreadCount(){
  const n={};
  for(const sp of P){
    for(const id of sp.slot2.pool) n[id]=(n[id]||0)+1;
    for(const id of sp.slot3.pool) n[id]=(n[id]||0)+1;
  }
  return n;
}

function drawTrait(){
  const t=T[cur]; const m=$('#main'); m.innerHTML='';
  m.append(judge(checkTr(t,cur)));

  const c1=el('div','card');
  const h=el('h2'); h.textContent='どんな特性か'; c1.append(h);
  const g=el('div','grid g2');
  g.append(field('id（英数字）',t.id,x=>{t.id=x;}));
  g.append(field('名前',t.name,x=>{t.name=x;}));
  c1.append(g);
  const g2=el('div','grid'); g2.style.marginTop='14px';
  // ⭐ 一覧から選べる。⚠️ **一覧に無い場面も書ける**（新しい割り込み先が要ると検査が言う）
  const w=el('div');
  const l=el('label'); l.textContent='働く場面　⭐ 一覧に無いものを書いてもよい';
  const row=el('div'); row.style.display='flex'; row.style.gap='8px';
  const sel=el('select'); sel.style.flex='0 0 240px';
  for(const o of D.whens){const p2=el('option');p2.value=o;p2.textContent=o;if(o===t.when)p2.selected=true;sel.append(p2);}
  const other=el('option'); other.value='__other'; other.textContent='… 一覧に無い場面を書く';
  if(!D.whens.includes(t.when)) other.selected=true;
  sel.append(other);
  const free=el('input'); free.type='text'; free.value=D.whens.includes(t.when)?'':t.when;
  free.placeholder='例: 相手が強化を得たとき';
  free.style.display=D.whens.includes(t.when)?'none':'block';
  sel.onchange=()=>{ if(sel.value==='__other'){free.style.display='block';free.focus();}
    else {t.when=sel.value;draw();} };
  free.oninput=()=>{t.when=free.value;echo();};
  row.append(sel,free); w.append(l,row); g2.append(w);
  c1.append(g2);
  c1.append(area('すること（画面に出ます）',t.gist,x=>{t.gist=x;},2));
  c1.append(area('噛み合うもの（図鑑に出ます）',t.pairs,x=>{t.pairs=x;},2));
  m.append(c1);

  const c2=el('div','card');
  const h2=el('h2'); h2.textContent='噛み合う技　⭐ 押すと「噛み合うもの」に足します'; c2.append(h2);
  const ch=el('div','chips');
  for(const s of S){
    const b=el('span','chip'); b.textContent=s.name;
    b.setAttribute('aria-pressed',(t.pairs||'').includes(s.name)?'true':'false');
    b.onclick=()=>{
      const has=(t.pairs||'').includes(s.name);
      t.pairs = has ? (t.pairs||'').split('・').filter(x=>x.trim()!==s.name).join('・')
                    : ((t.pairs||'')?t.pairs+'・'+s.name:s.name);
      draw();
    };
    ch.append(b);
  }
  c2.append(ch); m.append(c2);

  const c3=el('div','card');
  const h3=el('h2'); h3.textContent='⚠️ 効き目は手で書きます'; c3.append(h3);
  const p=el('p','hint');
  p.innerHTML='特性の効き目は <code>Battle.React</code> に書くもので、表からは組み立てられません。'
    +'<br>⭐ だから <b>下のメモに「どういう数字で、どう働くか」を書いてください</b>。'
    +'そのまま実装の指示になります。';
  c3.append(p);
  const eg=el('p','hint');
  eg.textContent='例: 「ゲージを満タンぶん(5000)進める。1戦闘1回。倒れたら解除しない」';
  c3.append(eg); m.append(c3);

  m.append(memoCard(t));
  m.append(preview(outOf(t),9));
}

// ⚠️ **`input` だけを見ない。**ドット絵の塗り・札の付け外し・雛形・追加削除は
//    input を出さないので、姿を1時間塗ってタブを閉じても無警告で消えていた
//    （2026-08-19 の監査）。⭐ 描き直し＝何か変えた、として数える。
let firstDraw=true, touched=false;
const dirty={skill:false,species:false,trait:false};
function draw(){
  if(!firstDraw){ touched=true; dirty[tab]=true; }
  firstDraw=false;
  drawList();
  const src=list();
  if(cur>=src.length) cur=Math.max(0,src.length-1);
  if(!src.length){ $('#main').innerHTML='<div class=card>＋ 新しく作る を押してください</div>'; return; }
  tab==='skill'?drawSkill():tab==='species'?drawSpecies():drawTrait();
  const tp=$('#tpl'); tp.innerHTML='';
  if(tab!=='skill'){
    tp.textContent = tab==='species'
      ? '⭐ 棚から袋へドラッグして組みます'
      : '⭐ 効き目はメモに書けば実装できます';
    return;
  }
  tp.append(document.createTextNode('雛形: '));
  for(const k of Object.keys(TPL)){
    const b=el('button','mini'); b.textContent=k; b.style.margin='0 4px 4px 0';
    b.onclick=()=>{
      Object.assign(S[cur],JSON.parse(JSON.stringify(TPL[k])));
      // ⭐ 雛形を押したら説明も組み直す（手で書いた文が無いときだけ）
      if(!S[cur].gist||S[cur].gist===describe(S[cur])) S[cur].gist=describe(S[cur]);
      draw();
    };
    tp.append(b);
  }
}

// ══ 帯の操作 ════════════════════════════════════
const born=new Set();
document.querySelectorAll('#rail .tabs button').forEach(b=>b.onclick=()=>{
  tab=b.dataset.tab; cur=0;
  document.querySelectorAll('#rail .tabs button')
    .forEach(o=>o.setAttribute('aria-selected',o===b?'true':'false'));
  draw();
});
$('#find').oninput=drawList;
$('#add').onclick=()=>{
  if(tab==='skill'){
    const born_={id:'new-skill',name:'新しい技',gist:'',type:'アタック',ct:3,target:'敵1体',
      effects:[fresh('ダメージ')],memo:''};
    born_.gist=describe(born_);   // ⭐ 最初から下書きが入っている
    S.push(born_);
  } else if(tab==='species'){
    const b={}; D.stats.forEach(k=>b[k]=100);
    P.push({id:'new-species',name:'新しい種族',skill1:S.length?S[0].id:'',base:b,
      slot2:{pool:[]},slot3:{pool:[]},
      sprite:Array.from({length:16},()=>'.'.repeat(16)),
      palettes:[['#2e2418','#8fc96e','#c8eaa8','#1a1410']],memo:''});
  } else {
    T.push({id:'new-trait',name:'新しい特性',when:D.whens[0],gist:'',pairs:'',memo:''});
  }
  cur=list().length-1; born.add(tab+list()[cur].id); draw();
};
$('#dup').onclick=()=>{
  const src=list();
  const c=JSON.parse(JSON.stringify(src[cur]));
  c.id+='-2'; c.name+='・写し'; src.splice(cur+1,0,c); cur++;
  born.add(tab+c.id); draw();
};
$('#del').onclick=()=>{
  const src=list();
  if(src.length<=1) return;
  src.splice(cur,1); draw();
};
// ⭐ 手でテキストを直したときのための入口。⚠️ 名前で見分ける
$('#load').onchange=async ev=>{
  for(const f of ev.target.files){
    const text=await f.text();
    // ⚠️ **名前でなく中身で見分ける。**名前で見ていた頃、`skills.txt` のような
    //    名前のファイルが**黙って無視**され、読み込んだつもりで保存＝上書きしていた。
    const head = text.includes('# 技 ') ? '技'
      : text.includes('# 種族 ') ? '種族'
      : text.includes('# 特性 ') ? '特性' : null;
    if(head==='技'){ S.length=0; parseSheet(text,'技').map(toSkill).forEach(o=>S.push(o)); dirty.skill=true; }
    else if(head==='種族'){ P.length=0; parseSheet(text,'種族').map(toSpecies).forEach(o=>P.push(o)); dirty.species=true; }
    else if(head==='特性'){ T.length=0; parseSheet(text,'特性').map(toTrait).forEach(o=>T.push(o)); dirty.trait=true; }
    else alert(f.name+' は帳面に見えません（# 技 / # 種族 / # 特性 で始まる行がありません）');
  }
  cur=0; draw();
  ev.target.value='';
};
$('#copy').onclick=async()=>{
  try{ await navigator.clipboard.writeText(outOf(list()[cur])); $('#copy').textContent='コピーしました'; }
  catch(_){ $('#copy').textContent='コピーできません'; }
  setTimeout(()=>$('#copy').textContent='この1件をコピー',1400);
};
// ⚠️ **触った柱だけ落とす。**3つとも落としていた頃、技だけ直したのに
//    種族と特性が**この画面を作った時点まで巻き戻って**いた（2026-08-19 の監査）。
//    ⭐ ブラウザの「複数ファイルの一括ダウンロード」に阻まれる事故も同時に減る。
$('#save').onclick=()=>{
  const mk=(head,arr,fn)=>head+arr.map(o=>fn(o)+memoLine(o)).join('\n')+'\n';
  const jobs=[];
  if(dirty.skill) jobs.push(['技.txt',mk(D.headSkill,S,blockOf)]);
  if(dirty.species) jobs.push(['種族.txt',mk(D.headSpecies,P,blockOfSp)]);
  if(dirty.trait) jobs.push(['特性.txt',mk(D.headTrait,T,blockOfTr)]);
  if(!jobs.length){ $('#save').textContent='変わっていません'; setTimeout(()=>$('#save').textContent='保存',1400); return; }
  // ⚠️ **黙って古い写しで上書きさせない。**ディスクの現物とは突き合わせられないので、
  //    せめて「これは○○時点の写しだ」を保存の直前にもう一度突きつける
  //    （2026-08-23 の監査 ── 一呼吸置くだけで、開いたままの古い頁が
  //    あとから手で足した行を消す事故を防げる）。
  if(D.snapshot){
    const names=jobs.map(j=>j[0]).join('・');
    const ok=confirm(`${names} を保存します。\n`
      +`この頁は ${D.snapshot.at} 時点の sheets/*.txt の写しです。\n`
      +`それより後に手で直接ファイルへ足した行があれば、保存で消えます。\n\n`
      +`このまま保存してよいですか？`);
    if(!ok) return;
  }
  jobs.forEach(([n,t],i)=>setTimeout(()=>dl(n,t),i*250));
  $('#save').textContent=jobs.map(j=>j[0]).join(' / ')+' を保存';
  setTimeout(()=>{$('#save').textContent='保存';},2000);
};
function dl(name,text){
  const a=document.createElement('a');
  a.href=URL.createObjectURL(new Blob([text],{type:'text/plain;charset=utf-8'}));
  a.download=name; a.click(); setTimeout(()=>URL.revokeObjectURL(a.href),4000);
}
// ⚠️ 書きかけを持ったまま閉じられるのを止める
document.addEventListener('input',()=>{touched=true;dirty[tab]=true;},true);
window.onbeforeunload=e=>{ if(touched){e.preventDefault();return e.returnValue='保存しましたか？';} };
draw();
</script>");
        }
    }
}
