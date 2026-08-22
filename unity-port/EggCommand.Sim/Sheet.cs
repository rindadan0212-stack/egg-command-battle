#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using EggCommand.Core;

namespace EggCommand.Sim
{
    /// <summary>技と種族を**手で書くための帳面**。
    ///
    /// ⭐ **狙いは1つ ── 作り手が C# を触らずに技と種族を書けること。**
    /// 書いたものは <see cref="Check"/> が検査し、<see cref="Code"/> が
    /// そのまま貼れる C# にする。
    ///
    /// ⚠️ **遊びはこの帳面を読まない。**実装の唯一の出所は今までどおり
    /// <see cref="Skills"/> と <see cref="SpeciesTable"/> の表で、帳面は**入口**にすぎない。
    /// ⭐ そうしてある理由:
    /// <list type="number">
    /// <item>ゴールデンと 292件の検査が、コンパイル時に確定した表を踏んでいる。
    ///   実行時に外部ファイルを読む形にすると、**照合が「その日のファイル」に依存**する</item>
    /// <item>表には手で書いた ⚠️/⭐ の但し書きが載っている（挑発の狙い先の事故、
    ///   全体技を1段下げる約束など）。機械が表を丸ごと書き直すと**それが消える**</item>
    /// <item>Core は依存を持たない。ファイル入出力を持ち込まない</item>
    /// </list>
    ///
    /// ⭐ 使い方は3つ:
    /// <list type="bullet">
    /// <item><c>sim sheet write</c> … いまの実装を帳面に書き出す（**既存を直す出発点**）</item>
    /// <item><c>sim sheet check</c> … 帳面を読んで検査する。新しい技には**手ぶん**も出す</item>
    /// <item><c>sim sheet code</c> … 実装に無いもの・食い違うものの C# を出す</item>
    /// </list>
    ///
    /// ⚠️ <c>write</c> は帳面を**上書きする**。書きかけがあるときに走らせない。</summary>
    public static class Sheet
    {
        /// <summary>置き場所。⚠️ 決め打ち（`sim wiki` と同じ約束）。</summary>
        public const string Dir = "../sheets";
        public const string SkillFile = "技.txt";
        public const string SpeciesFile = "種族.txt";
        public const string TraitFile = "特性.txt";

        // ── 語彙 ────────────────────────────────────────
        // ⚠️ **画面に出る語をそのまま使う。**帳面だけの言い換えを作らない
        //    （作ると、帳面・画面・Wiki で3つの語彙を覚えることになる）。
        // ⚠️ ただし **符号で名前が変わるもの**（CT短縮/CT延長・ゲージ上昇/減少・攻撃力UP/DOWN）は、
        //    種類の語を**符号から独立**させる。符号は `増減:` `ステ:` の札が持つ。
        //    ⭐ そうしないと「CT短縮 増減:+2」のような、名前と中身が矛盾した行が書けてしまう。

        private const string KindDamage = "ダメージ";
        private const string KindBuffUp = "強化";
        private const string KindBuffDown = "弱化";

        private static readonly (string Word, EffectKind Kind)[] Kinds =
        {
            (KindDamage, EffectKind.Damage),
            (KindBuffUp, EffectKind.Buff),
            (KindBuffDown, EffectKind.Buff),
            ("毒", EffectKind.Poison),
            ("リジェネ", EffectKind.Regen),
            ("HP割合", EffectKind.HealRatio),
            ("シールド", EffectKind.Shield),
            ("スタン", EffectKind.Stun),
            ("睡眠", EffectKind.Sleep),
            ("ブロック", EffectKind.Block),
            ("CT", EffectKind.Ct),
            ("ゲージ", EffectKind.Gauge),
            ("挑発", EffectKind.Taunt),
            ("ガッツ", EffectKind.Guts),
            ("免疫", EffectKind.Immune),
            ("解除", EffectKind.Dispel),
            ("強化強奪", EffectKind.Steal),
            ("蘇生", EffectKind.Revive),
            // ⭐ 生まれつき（パッシブ専用）。⚠️ 語を強化・弱化と分ける ──
            //    同じ語で書けると、パッシブでない技に紛れ込む
            (KindInnate, EffectKind.Buff),
        };

        /// <summary>帳面での「生まれつき」の語。</summary>
        private const string KindInnate = "生まれつき";

        /// <summary>前に使っていた語 → いまの語。⭐ **書いたものを壊さないための表。**
        ///
        /// ⚠️ 語を言い換えた日に、**すでに帳面へ書いてあった技が「そんな効果は無い」で止まった**
        /// （2026-08-19。`割合回復` を `HP割合` にした瞬間、書きかけの全体回復が読めなくなった）。
        /// ⭐ 帳面は作り手が手で書くものなので、**語を変えるときは必ずここに前の語を残す。**
        /// ⚠️ 消してよいのは「その語で書かれた帳面がこの世に1つも無い」と言い切れるときだけ。</summary>
        private static readonly (string Was, string Now)[] Aliases =
        {
            ("割合回復", "HP割合"),      // 符号で削る側も書けるようにしたので、向きから独立させた
            ("強化解除", "解除"),        // 同上（個数が負なら弱化を剥がす）
        };

        /// <summary>前の語なら、いまの語に直す。⭐ 知らない語はそのまま返す。</summary>
        private static string Rename(string word)
        {
            foreach (var (was, now) in Aliases) if (was == word) return now;
            return word;
        }

        /// <summary>まだ実装に無いことを、**言葉のまま**書いておくための種類。
        ///
        /// ⭐ **これが「あらゆる場合に耐える」の正体。**書式が追いつかない要求に当たったとき、
        /// 帳面は**塞がずに文章を通す**。⚠️ 通すが、黙って実装した気にはさせない ──
        /// <see cref="Check"/> は 🚧 として数え、<see cref="Code"/> は C# を出さない。
        /// ⭐ 読むのは人間（と、実装を頼まれた側）。</summary>
        public const string FreeKind = "自由記述";

        /// <summary>1件に付ける覚え書き。⭐ 何を書いてもよい。実装のとき読む。</summary>
        public const string MemoKey = "メモ";

        // ── 入口 ────────────────────────────────────────

        public static void Run(string what)
        {
            switch (what)
            {
                case "write": Write(); break;
                case "check": Check(); break;
                case "code": Code(); break;
                case "html":
                    Console.WriteLine("書き出した: "
                        + SheetHtml.Write(Path.Combine(Dir, "エディタ.html")));
                    break;
                default:
                    Console.WriteLine("sim sheet <write|check|code>");
                    Console.WriteLine("  write … いまの実装を帳面へ書き出す（⚠️ 上書き）");
                    Console.WriteLine("  check … 帳面を検査する");
                    Console.WriteLine("  code  … 貼れる C# を出す");
                    Console.WriteLine("  html  … ⭐ クリックで書くための編集画面を作る");
                    break;
            }
        }

        // ══ 書き出し ═══════════════════════════════════

        private static void Write()
        {
            Directory.CreateDirectory(Dir);
            var utf8 = new UTF8Encoding(false);
            int a = Put(SkillFile, "技", SkillSheet(), utf8);
            int b = Put(SpeciesFile, "種族", SpeciesSheet(), utf8);
            int c = Put(TraitFile, "特性", TraitSheet(), utf8);
            Console.WriteLine($"帳面を書き出した: {Path.GetFullPath(Dir)}");
            Console.WriteLine($"  {SkillFile}   … 技 {Skills.All.Count} 件"
                + (a > 0 ? $"（＋ 書きかけ {a} 件を残した）" : ""));
            Console.WriteLine($"  {SpeciesFile} … 種族 {SpeciesTable.All.Count} 件"
                + (b > 0 ? $"（＋ 書きかけ {b} 件を残した）" : ""));
            Console.WriteLine($"  {TraitFile}   … 特性 {Traits.All.Count} 件"
                + (c > 0 ? $"（＋ 書きかけ {c} 件を残した）" : ""));
        }

        /// <summary>書き出す。⭐ **書きかけを消さない。**
        ///
        /// ⚠️ 素直に上書きすると、**まだ実装していない書きかけが丸ごと消える**。
        /// 「5件書いて `write` を打ったら全部消えた」は、道具として致命的
        /// （2026-08-19、帳面を広げているときに気づいた）。
        /// ⭐ そこで:
        /// <list type="bullet">
        /// <item>実装にある id … 実装から作り直す（＝直したことが反映される）。
        ///   ただし手で足した <c>メモ</c> の行は**そのまま持ち越す**</item>
        /// <item>実装に無い id … **1文字も触らず**末尾へ写す</item>
        /// </list>
        /// ⚠️ だから `write` は「実装で上書き」ではなく「実装と突き合わせて整える」。</summary>
        private static int Put(string file, string head, string made, UTF8Encoding utf8)
        {
            string path = Path.Combine(Dir, file);
            var kept = new Dictionary<string, List<string>>();   // id → メモの行
            var orphan = new List<string>();                     // 実装に無い1件まるごと

            if (File.Exists(path))
            {
                var known = new HashSet<string>();
                foreach (var line in made.Split('\n'))
                {
                    if (!line.StartsWith("# " + head + " ")) continue;
                    known.Add(line.Substring(head.Length + 3).Trim());
                }

                string? id = null;
                var buffer = new List<string>();
                void Flush()
                {
                    if (id == null) return;
                    if (known.Contains(id))
                    {
                        // ⚠️ **手で足した行を全部持ち越す。**メモだけ拾っていた頃、
                        //    実装済みの技に足した「効果 = 自由記述 …」と「// コメント」が
                        //    `write` のたびに**黙って消えて**いた（2026-08-19 の監査）。
                        //    ⭐ 自由記述は「まだ書けないこと」を託す唯一の場所なので、
                        //    消えると「実装したつもり」で完成してしまう。
                        // ⚠️ 札の切り出しは空白の数に頼らない（`メモ=` も `メモ = ` も同じ）。
                        var mine = new List<string>();
                        foreach (var l in buffer)
                        {
                            string t = Normalize(l).Trim();
                            if (t.StartsWith("//")) { mine.Add(l.TrimEnd()); continue; }
                            int eq = t.IndexOf('=');
                            if (eq < 0) continue;
                            string key = t.Substring(0, eq).Trim();
                            string val = t.Substring(eq + 1).Trim();
                            if (key == MemoKey) mine.Add($"{MemoKey} = {val}");
                            else if (key == "効果" && val.StartsWith(FreeKind)) mine.Add($"効果 = {val}");
                        }
                        if (mine.Count > 0) kept[id] = mine;
                    }
                    else
                    {
                        orphan.AddRange(buffer);
                        orphan.Add("");
                    }
                    id = null;
                    buffer.Clear();
                }

                foreach (var raw in File.ReadAllLines(path))
                {
                    string t = Normalize(raw).Trim();
                    if (t.StartsWith("# " + head + " "))
                    {
                        Flush();
                        id = t.Substring(head.Length + 3).Trim();
                        buffer.Add(raw);
                        continue;
                    }
                    if (id != null) buffer.Add(raw);
                }
                Flush();
            }

            // ⭐ メモを本文へ差し戻す
            var sb = new StringBuilder();
            string? now = null;
            foreach (var line in made.Split('\n'))
            {
                if (line.StartsWith("# " + head + " "))
                    now = line.Substring(head.Length + 3).Trim();
                // 1件の終わり（空行）で、持ち越したメモを足す
                if (line.Length == 0 && now != null && kept.TryGetValue(now, out var memos))
                {
                    foreach (var m in memos) sb.Append(m).Append('\n');
                    now = null;
                }
                sb.Append(line).Append('\n');
            }
            // 末尾の余分な改行を1つ落とす（made は既に \n で終わる）
            if (sb.Length > 0 && sb[sb.Length - 1] == '\n') sb.Length--;

            int count = 0;
            if (orphan.Count > 0)
            {
                sb.Append("\n// ══ ここから下は、まだ実装に入っていない書きかけ ══════\n");
                sb.Append("// ⭐ `sim sheet write` はここを**1文字も触りません**。\n");
                sb.Append("// ⚠️ 実装に入れたら、次の write で上の並びへ移ります。\n\n");
                foreach (var l in orphan) sb.Append(l).Append('\n');
                foreach (var l in orphan) if (l.TrimStart().StartsWith("# " + head + " ")) count++;
            }

            File.WriteAllText(path, sb.ToString(), utf8);
            return count;
        }

        /// <summary>技の帳面の前書き。⭐ **HTML 版もこれを書く。**
        /// ⚠️ 別々に書くと、画面から保存したファイルだけ前書きが古くなる。</summary>
        public static string SkillHead()
        {
            var md = new StringBuilder();
            Head(md, "技");
            md.Append("// ⭐ 1件 ＝ 「# 技 <英数字の id>」で始まる1かたまり。\n");
            md.Append("// ⭐ 行は「札 = 中身」。⚠️ 札の区切りは = だけ（: は効果の中で使う）。\n");
            md.Append("// ⚠️ 効果は何行でも書ける。書いた順に効く。\n");
            md.Append("//\n");
            md.Append("// 効果の書き方 ── 種類のあとに「札:値」を好きな順で:\n");
            md.Append($"//   {KindDamage}    威力:{Join<PowerTier>(Skills.LabelOf)}"
                + $"  依存:{Join<DamageScale>(Skills.LabelOf)}  発数:N  防御無視\n");
            md.Append($"//   {KindBuffUp}/{KindBuffDown}  ステ:攻撃力|防御力|スピード  ターン:N  確率:N\n");
            md.Append("//   毒/リジェネ  スタック:N  ターン:N  確率:N\n");
            md.Append("//   HP割合/蘇生  割合:±N  確率:N"
                + "（HP割合は ＋で回復、−で最大HPの割合ぶん削る）\n");
            md.Append("//   シールド/解除/強化強奪  個数:±N  確率:N"
                + "（解除は ＋で強化を剥がし、−で弱化を剥がす）\n");
            md.Append("//   スタン/睡眠/ブロック/ガッツ/免疫  ターン:N  確率:N\n");
            md.Append("//   挑発  回数:N  確率:N     CT  増減:±N  確率:N     ゲージ  割合:±N  確率:N\n");
            md.Append($"//   {FreeKind}  そのあとは**何を書いてもよい**（下記）\n");
            md.Append("//\n");
            md.Append("// ⭐ **書けないことに当たったら、言葉で書く。**\n");
            md.Append($"//     効果 = {FreeKind} 相手が毒なら威力1.5倍\n");
            md.Append("//     メモ = 3体とも倒れていたら発動しない、という条件にしたい\n");
            md.Append("//   ⚠️ 帳面は通すが、実装した扱いにはならない（検査が 🚧 として数える）。\n");
            md.Append("//\n");
            md.Append($"// ⭐ CT に上限は無い。⚠️ ただし {Skills.CtCap} を超えると検査が理由を添えて言う\n");
            md.Append($"//   （盤面をひっくり返す技は {Skills.CtHeavy} まで黙る）。**止めはしない。**\n");
            md.Append("// ⚠️ 確率はダメージと強化には付かない（付けても無視される）。\n\n");
            return md.ToString();
        }

        private static string SkillSheet()
        {
            var md = new StringBuilder(SkillHead());
            foreach (var s in Skills.All) md.Append(BlockOf(s)).Append('\n');
            return md.ToString();
        }

        /// <summary>技1件を帳面の書き方で。⭐ **HTML 版の自己検査が突き合わせる相手。**
        /// ⚠️ 画面の JS が同じ文字列を作れなければ、そこで警告が出る。</summary>
        public static string BlockOf(Skill s)
        {
            var md = new StringBuilder();
            md.Append($"# 技 {s.Id}\n名前 = {s.Name}\n説明 = {s.Gist}\n");
            md.Append($"型 = {Skills.LabelOf(s.Type)}\nCT = {s.Ct}\n");
            md.Append($"狙い = {SkillText.TargetOf(s.Target)}\n");
            // ⭐ 押せない技。⚠️ 書き落とすと、読み返したとき普通の技になる
            if (s.Passive) md.Append("パッシブ = はい\n");
            foreach (var e in s.Effects) md.Append($"効果 = {EffectLine(e)}\n");
            return md.ToString();
        }

        /// <summary>特性1件。⚠️ **効き目は文章のまま。**
        /// 特性の中身は <see cref="Battle.React"/> に手で書くもので、表からは組み立てられない。
        /// ⭐ だからここは「何を作りたいか」を渡すための欄でよい（作者の指示 2026-08-19）。</summary>
        public static string BlockOf(Trait t)
        {
            var md = new StringBuilder();
            md.Append($"# 特性 {t.Id}\n名前 = {t.Name}\n");
            md.Append($"働く場面 = {Traits.LabelOf(t.When)}\n");
            md.Append($"すること = {t.Gist}\n");
            md.Append($"噛み合うもの = {t.Pairs}\n");
            return md.ToString();
        }

        public static string TraitHead()
        {
            var md = new StringBuilder();
            Head(md, "特性");
            md.Append("// ⭐ 1件 ＝ 「# 特性 <英数字の id>」で始まる1かたまり。\n");
            md.Append("// ⚠️ **特性は表から組み立てられない。**効き目は Battle.React に手で書く。\n");
            md.Append("//    ⭐ だからここは「何を作りたいか」を渡す欄。文章でよい。\n");
            md.Append("//\n");
            md.Append("// 働く場面（この語から選ぶ）:\n//   ");
            var whens = (TraitWhen[])Enum.GetValues(typeof(TraitWhen));
            for (int i = 0; i < whens.Length; i++)
                md.Append(i > 0 ? " / " : "").Append(Traits.LabelOf(whens[i]));
            md.Append("\n");
            md.Append("// ⭐ **この一覧に無い場面が要るなら、そう書いてよい。**\n");
            md.Append("//    検査が「Battle に割り込み先を足す必要がある」と言う。止めはしない。\n");
            md.Append($"// ⭐ 数値や条件は「{MemoKey} = 〜」に自由に書く。実装のとき読む。\n\n");
            return md.ToString();
        }

        private static string TraitSheet()
        {
            var md = new StringBuilder(TraitHead());
            foreach (var t in Traits.All) md.Append(BlockOf(t)).Append('\n');
            return md.ToString();
        }

        /// <summary>種族の帳面の前書き。⭐ **HTML 版もこれを書く。**</summary>
        public static string SpeciesHead()
        {
            var md = new StringBuilder();
            Head(md, "種族");
            md.Append("// ⭐ 1件 ＝ 「# 種族 <英数字の id>」で始まる1かたまり。\n");
            md.Append($"// ⚠️ 基礎ステの合計は全種族 {SpeciesTable.BaseTotal}、\n");
            md.Append($"//    そのうち 弱化命中＋弱化耐性 は {SpeciesTable.DebuffBaseTotal} に揃える。差は配分だけ。\n");
            md.Append("// ⭐ 枠1 の技を 枠2・枠3 に入れてよい（孵化のとき自動で外れる）。\n");
            md.Append("//    ⚠️ ただし**外すと空になる**書き方は通らない。\n");
            md.Append($"// ⭐ 袋は**型で縛らない**。好きな技を {Skills.PoolMax} 件まで並べる。\n");
            md.Append($"//    ⚠️ 1つの技を入れてよい袋は {Skills.SpreadMax} か所まで"
                + "（どこで奪っても同じ、を避ける）。\n");
            md.Append("//    ⚠️ 枠2 と枠3 で同じ技を入れない。2つの袋で役割が1つに偏らない。\n");
            md.Append("// ⚠️ 姿は **16行×16文字 か 64行×64文字**。'.' が透明、\n");
            md.Append($"//    '{PixelSprite.Digits}' が色の番号（色の並びと同じ数だけ使えます）。\n");
            md.Append("//    ⭐ 16×16 の10種族は 1=輪郭 2=体 3=差し色 4=目 で描いてある。\n");
            md.Append("//    ⚠️ 64×64 のタマルはこの決めごとに従いません（色ごとに役が違います）。\n");
            md.Append("// ⚠️ 色は1行が1組。1行目が通常色で、2行目以降が変異色。\n");
            md.Append("//    ⭐ 数は種族ごとに決めてよい。⚠️ ただし**1件の中では揃える**\n");
            md.Append("//    （通常色が11で変異色が4だと、変異させた瞬間に落ちます）。\n");
            md.Append($"// ⭐ どの1件にも「{MemoKey} = 〜」を足せる。何を書いてもよい。\n\n");
            return md.ToString();
        }

        private static string SpeciesSheet()
        {
            var md = new StringBuilder(SpeciesHead());
            foreach (var sp in SpeciesTable.All) md.Append(BlockOf(sp)).Append('\n');
            return md.ToString();
        }


        /// <summary>1つの効果を帳面の1行にする。⭐ <see cref="ParseEffect"/> と対で読むこと。</summary>
        public static string LineOf(Effect e) => EffectLine(e);

        /// <summary>enum の語を「A|B|C」に並べる。⭐ **候補を手で書かないため。**
        /// ⚠️ 手で書いていた頃、スピードを足しても前書きだけ「攻撃|防御」のままだった。</summary>
        private static string Join<T>(Func<T, string> label) where T : Enum =>
            string.Join("|", Array.ConvertAll((T[])Enum.GetValues(typeof(T)), v => label(v)));

        /// <summary>どの帳面にも同じ前置きを書く。⚠️ **ここだけが約束の出所。**</summary>
        private static void Head(StringBuilder md, string what)
        {
            md.Append($"// \u2550\u2550 {what}の帳面 \u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\n");
            md.Append("// \u26a0\ufe0f **これは入口であって、遊びの出所ではない。**\n");
            md.Append("//    ここを直しただけでは何も変わらない。sheet check で検査し、\n");
            md.Append("//    sheet code が出す C# を表へ入れて初めて反映される。\n");
            md.Append("// \u2b50 sheet write は**書きかけを消さない**（実装に無い1件はそのまま残る）。\n");
            md.Append("// \u2b50 「//」で始まる行と空行は読み飛ばす。\n\n");
        }

        private static string EffectLine(Effect e)
        {
            var sb = new StringBuilder();
            switch (e.Kind)
            {
                case EffectKind.Damage:
                    sb.Append(KindDamage)
                      .Append(" 威力:").Append(Skills.LabelOf(e.Power))
                      .Append(" 依存:").Append(Skills.LabelOf(e.Scale));
                    if (e.Repeat > 1) sb.Append(" 発数:").Append(e.Repeat);
                    if (e.Pierce) sb.Append(" 防御無視");
                    // ⚠️ **ここで return しない。**していた頃、ダメージの行だけ
                    //    飛び先・条件・数えが書き落とされ、読み返すと別の技になっていた
                    break;
                case EffectKind.Buff when e.Innate:
                    // ⭐ 生まれつきは持続も確率も無い（⚠️ 書いても読み返せない）
                    return KindInnate + " ステ:" + Stats.LabelOf(e.Stat)
                        + " 向き:" + (e.Sign > 0 ? "上" : "下");
                case EffectKind.Buff:
                    sb.Append(e.Sign > 0 ? KindBuffUp : KindBuffDown)
                      .Append(" ステ:").Append(Stats.LabelOf(e.Stat))
                      // ⭐ 切れない持続は数で書かない（「ターン:-1」では読めない）
                      .Append(" ターン:").Append(e.Turns < 0 ? Everlasting : e.Turns.ToString());
                    break;
                case EffectKind.Poison:
                case EffectKind.Regen:
                    sb.Append(e.Kind == EffectKind.Poison ? "毒" : "リジェネ")
                      .Append(" スタック:").Append(e.Stacks)
                      .Append(" ターン:").Append(e.Turns);
                    break;
                case EffectKind.HealRatio:
                    // ⭐ 符号で向きが変わるので、語は向きから独立させる（CT・ゲージと同じ）
                    sb.Append("HP割合 割合:").Append(e.Percent > 0 ? "+" : "").Append(e.Percent);
                    break;
                case EffectKind.Revive:
                    sb.Append("蘇生 割合:").Append(e.Percent);
                    break;
                case EffectKind.Shield:
                    sb.Append("シールド 個数:").Append(e.Count);
                    break;
                case EffectKind.Dispel:
                case EffectKind.Steal:
                    // ⭐ 符号が意味を持つのは「解除」だけ（＋強化 / −弱化）。
                    // ⚠️ 強化強奪に負は無い（弱化を押し付ける手は作らない）ので符号を付けない。
                    if (e.Kind == EffectKind.Dispel)
                        sb.Append("解除 個数:").Append(e.Count > 0 ? "+" : "").Append(e.Count);
                    else sb.Append("強化強奪 個数:").Append(e.Count);
                    break;
                case EffectKind.Stun:
                case EffectKind.Sleep:
                case EffectKind.Block:
                case EffectKind.Guts:
                case EffectKind.Immune:
                    sb.Append(WordOf(e.Kind)).Append(" ターン:").Append(e.Turns);
                    break;
                case EffectKind.Ct:
                    sb.Append("CT 増減:").Append(e.Delta > 0 ? "+" : "").Append(e.Delta);
                    break;
                case EffectKind.Gauge:
                    sb.Append("ゲージ 割合:").Append(e.Percent > 0 ? "+" : "").Append(e.Percent);
                    break;
                case EffectKind.Taunt:
                    sb.Append("挑発 回数:").Append(e.Hits);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(e), e.Kind, "帳面に書けない効果");
            }
            if (e.Chance < 100) sb.Append(" 確率:").Append(e.Chance);
            // ⭐ 1手2役。⚠️ 書き落とすと、読み返したとき飛び先が消えて別の技になる
            if (e.Own != null) sb.Append(" 飛び先:").Append(SkillText.TargetOf(e.Own.Value));
            // ⭐ 条件と数え。⚠️ 同上（落とすと条件の無い技として読み返され、値段まで変わる）
            if (e.When != null) sb.Append(" 条件:").Append(WordOfWhen(e.When.Value));
            if (e.Per != Tally.None) sb.Append(" 数え:").Append(WordOfTally(e.Per));
            return sb.ToString();
        }

        /// <summary>帳面での条件の語。⚠️ 画面の言い回し（<see cref="SkillText"/>）とは別 ──
        /// ⭐ 帳面は**短い札**で書く（1行に収める）。</summary>
        private static string WordOfWhen(SkillWhen when)
        {
            switch (when)
            {
                case SkillWhen.FoeWeakened: return "相手に弱化";
                case SkillWhen.FoeBoosted: return "相手に強化";
                case SkillWhen.FoeStopped: return "相手が動けない";
                case SkillWhen.FoeHalf: return "相手が半分以下";
                case SkillWhen.SelfHalf: return "自分が半分以下";
                default: throw new ArgumentOutOfRangeException(nameof(when), when, "帳面の語が無い条件");
            }
        }

        private static bool TryWhen(string? word, out SkillWhen when)
        {
            foreach (SkillWhen value in Enum.GetValues(typeof(SkillWhen)))
            {
                if (WordOfWhen(value) == word) { when = value; return true; }
            }
            when = default;
            return false;
        }

        private static string WordOfTally(Tally per)
        {
            switch (per)
            {
                case Tally.FoeBanes: return "相手の弱化";
                case Tally.FoeBoons: return "相手の強化";
                case Tally.OwnBoons: return "自分の強化";
                default: throw new ArgumentOutOfRangeException(nameof(per), per, "帳面の語が無い数え方");
            }
        }

        private static bool TryTally(string? word, out Tally per)
        {
            foreach (Tally value in Enum.GetValues(typeof(Tally)))
            {
                if (value != Tally.None && WordOfTally(value) == word) { per = value; return true; }
            }
            per = Tally.None;
            return false;
        }

        /// <summary>帳面での「切れない持続」の書き方。⚠️ 書く側と読む側で同じ語を使う。</summary>
        private const string Everlasting = "永続";

        private static string WordOf(EffectKind kind)
        {
            foreach (var (word, k) in Kinds) if (k == kind) return word;
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "帳面の語が無い");
        }

        // ══ 読み取り ═══════════════════════════════════

        /// <summary>帳面の1かたまり。⭐ 行番号を持つ（文句を言うときに要る）。</summary>
        private sealed class Block
        {
            public string Id = "";
            public int Line;
            public readonly List<(string Key, string Value, int Line)> Fields = new();
            public readonly List<string> Grid = new();

            public string? One(string key)
            {
                foreach (var f in Fields) if (f.Key == key) return f.Value;
                return null;
            }

            public List<string> Many(string key)
            {
                var found = new List<string>();
                foreach (var f in Fields) if (f.Key == key) found.Add(f.Value);
                return found;
            }
        }

        /// <summary>⚠️ 全角で書かれても通す。⭐ IME は「＝」「：」「０」を平気で出す。</summary>
        private static string Normalize(string line)
        {
            var sb = new StringBuilder(line.Length);
            foreach (char c in line)
            {
                if (c >= '０' && c <= '９') sb.Append((char)('0' + (c - '０')));
                else if (c == '＝') sb.Append('=');
                else if (c == '：') sb.Append(':');
                else if (c == '＋') sb.Append('+');
                // ⚠️ **ー（長音）をマイナスにしない。**一度やって、
                //    「ダメージ」が「ダメ-ジ」になり全部読めなくなった（2026-08-19）。
                else if (c == '－' || c == '−') sb.Append('-');
                else if (c == '　') sb.Append(' ');
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static List<Block> Read(string path, string head, List<string> problems)
        {
            if (!File.Exists(path))
            {
                problems.Add($"{Path.GetFileName(path)} が無い（先に `sim sheet write`）");
                return new List<Block>();
            }
            return ReadText(File.ReadAllLines(path), head, problems);
        }

        /// <summary>文字列から読む。⭐ **検査が往復を確かめるための口。**
        /// ⚠️ ファイルから読むときと**同じ道**を通ること（別に書くと片方だけ直る）。</summary>
        private static List<Block> ReadText(string[] lines, string head, List<string> problems)
        {
            var blocks = new List<Block>();
            Block? now = null;
            bool inGrid = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                int no = i + 1;
                string line = Normalize(raw);
                string trimmed = line.Trim();

                if (trimmed.Length == 0) { inGrid = false; continue; }
                if (trimmed.StartsWith("//")) continue;

                // ⭐ 姿の格子は「字下げされた行」。⚠️ 空行か次の札で終わる
                if (inGrid && (raw.StartsWith(" ") || raw.StartsWith("\t") || raw.StartsWith("　")))
                {
                    if (now == null) { problems.Add($"{no}行: 見出しの外に格子がある"); continue; }
                    now.Grid.Add(trimmed);
                    continue;
                }
                inGrid = false;

                if (trimmed.StartsWith("#"))
                {
                    var parts = trimmed.TrimStart('#').Trim()
                        .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2 || parts[0] != head)
                    {
                        // ⚠️ **`now` を落とす。**落とさないと、壊れた見出しの下の札が
                        //    **1つ前の1件に混ざる** ── 実測で、直前の技が余分なダメージ効果を
                        //    吸って手ぶんが 0.80→2.80 になっていた（2026-08-19 の監査）。
                        problems.Add($"{no}行: 見出しは「# {head} <id>」の形で書く（{trimmed}）"
                            + " ── ここから次の見出しまでを読み飛ばします");
                        now = null;
                        continue;
                    }
                    now = new Block { Id = parts[1], Line = no };
                    blocks.Add(now);
                    continue;
                }

                int eq = trimmed.IndexOf('=');
                if (eq < 0) { problems.Add($"{no}行: 「札 = 中身」の形になっていない（{trimmed}）"); continue; }
                if (now == null) { problems.Add($"{no}行: 見出しの前に札がある（{trimmed}）"); continue; }

                string key = trimmed.Substring(0, eq).Trim();
                string value = trimmed.Substring(eq + 1).Trim();
                now.Fields.Add((key, value, no));
                if (key == "姿") inGrid = true;
            }
            return blocks;
        }

        // ══ 検査 ═══════════════════════════════════════

        /// <summary>言いたいことは2種類ある。⭐ **混ぜない。**
        ///
        /// ⚠️ <b>止まる</b> … そのままでは C# にできない（id が無い・効果が読めない等）。
        /// ⭐ <b>言うだけ</b> … 通るが疑わしい（CT が長い・帯の外・型が中身と違う）。
        ///
        /// ⚠️ **上限で書き手を縛らない**（作者の指示 2026-08-19）。
        /// CT も威力も対象も、**書けることは全部書ける**。検査は理由を添えて言うだけで、
        /// 決めるのは作り手。⭐ 止めるのは「実装が受け取れない」場合だけ。</summary>
        private sealed class Notes
        {
            public readonly List<string> Stop = new();   // 🚧 C# にできない
            public readonly List<string> Say = new();    // ⚠️ 通るが疑わしい
            public readonly List<string> Todo = new();   // ✍️ 実装が要る（自由記述など）
            public readonly List<string> Fatal = new();  // ⛔ 貼ると起動時の検査が落ちる
        }

        /// <summary>自由記述を含む技の id。⭐ 手ぶんに「数えていない」と添えるため。</summary>
        private static readonly HashSet<string> Freeform = new();

        private static void Check()
        {
            Freeform.Clear();
            var n = new Notes();
            var skills = ReadSkills(n, out var newSkills);
            var species = ReadSpecies(n, skills);
            var traits = ReadTraits(n);

            Console.WriteLine();
            Console.WriteLine($"■ 帳面の検査（技 {skills.Count} / 種族 {species.Count} / 特性 {traits} 件）");

            if (n.Stop.Count == 0 && n.Say.Count == 0 && n.Todo.Count == 0)
            {
                Console.WriteLine("  ⭐ 問題なし");
            }
            if (n.Stop.Count > 0)
            {
                Console.WriteLine($"  🚧 そのままでは実装にできない（{n.Stop.Count} 件）");
                foreach (var p in n.Stop) Console.WriteLine("    ⚠️ " + p);
            }
            if (n.Todo.Count > 0)
            {
                Console.WriteLine($"  ✍️ 実装が要る（{n.Todo.Count} 件）── **書いてよい。まだ動かないだけ**");
                foreach (var p in n.Todo) Console.WriteLine("    ・" + p);
            }
            if (n.Say.Count > 0)
            {
                Console.WriteLine($"  ⚠️ 気になること（{n.Say.Count} 件）── **止めはしない**");
                foreach (var p in n.Say) Console.WriteLine("    ・" + p);
            }

            // ⭐ **貼ったらどうなるかを、貼る前に言う。**
            // ⚠️ ここが無かった頃、帳面が ⚠️ で通した技（CT12）を貼った瞬間に
            //    `Skills.Audit` が投げ、292件のどれが自分のせいか読み解く羽目になっていた
            //    （2026-08-19 の監査）。⭐ 規則は Core の Faults() 1か所から借りる。
            Preflight(skills, species, n);
            if (n.Fatal.Count > 0)
            {
                Console.WriteLine($"  ⛔ このまま貼ると**起動時の検査が落ちます**（{n.Fatal.Count} 件）");
                foreach (var p in n.Fatal) Console.WriteLine("    ・" + p);
            }

            // ⚠️ **消えた1件を言う。**帳面から消しても実装からは消えないので、
            //    黙っていると「削除したのに戻ってくる」に見える。
            var gone = new List<string>();
            foreach (var s in Skills.All)
            {
                bool found = false;
                foreach (var t in skills) if (t.Id == s.Id) { found = true; break; }
                if (!found) gone.Add($"技 {s.Id}「{s.Name}」");
            }
            foreach (var sp in SpeciesTable.All)
            {
                bool found = false;
                foreach (var t in species) if (t.Id == sp.Id) { found = true; break; }
                if (!found) gone.Add($"種族 {sp.Id}「{sp.Name}」");
            }
            if (gone.Count > 0)
            {
                Console.WriteLine($"  🗑 帳面から消えているもの（{gone.Count} 件）"
                    + "── **実装からは消えません。**表から手で消してください");
                foreach (var p in gone) Console.WriteLine("    ・" + p);
            }

            if (newSkills.Count > 0)
            {
                // ⭐ **強すぎ・弱すぎは読んで分かるように数で出す**（作者方針 2026-08-19）。
                //    ⚠️ 勝率ではなく算数。1.0 ＝ 枠1 で殴るのと同じ値打ち。
                Console.WriteLine();
                // ⚠️ **固定のしきい値で叱らない。**「2.5 を超えたら強すぎ」と決め打ちしたら、
                //    いま在る全体強攻撃（4.00）が毎回引っかかった。
                // ⭐ 比べる相手は**いま在る技の帯**にする。外に出たときだけ言う。
                double lo = double.MaxValue, hi = 0;
                string loName = "", hiName = "";
                foreach (var m in Skills.All)
                {
                    double v = Program.TurnValueOf(m, out _);
                    if (v < lo) { lo = v; loName = m.Name; }
                    if (v > hi) { hi = v; hiName = m.Name; }
                }
                Console.WriteLine("■ 実装に無い技の手ぶん（`sim turnvalue` と同じ算数）");
                Console.WriteLine("  ⭐ 1.0 が「枠1 で殴るのと同じ」");
                Console.WriteLine($"  ⭐ いま在る57技の帯: {lo:0.00}（{loName}）〜 {hi:0.00}（{hiName}）");
                Console.WriteLine("  ⚠️ この帯の外に出たものだけ印を付けます");
                foreach (var s in newSkills)
                {
                    double v = Program.TurnValueOf(s, out string why);
                    string mark = v > hi ? $" 🚧 いまの最強（{hi:0.00}）より上" 
                        : v < lo ? $" 🚧 いまの最弱（{lo:0.00}）より下" : "";
                    // ⚠️ **自由記述ぶんは数えていないと言う。**言わないと、
                    //    文章で足した強さが数字に出ていないことに気づけない。
                    if (Freeform.Contains(s.Id)) mark += " ✍️ 自由記述ぶんは数えていません";
                    Console.WriteLine($"  {s.Name,-14}{v,6:0.00} 手ぶん  CT{s.Ct}  {why}{mark}");
                }
            }
        }

        /// <summary>帳面の技を読む。⚠️ 読めた分だけ返す（1件の失敗で全部を捨てない）。</summary>
        private static List<Skill> ReadSkills(Notes note, out List<Skill> fresh)
        {
            var problems = note.Stop;
            fresh = new List<Skill>();
            var made = new List<Skill>();
            var ids = new HashSet<string>();
            var names = new HashSet<string>();

            foreach (var b in Read(Path.Combine(Dir, SkillFile), "技", problems))
            {
                string at = $"技 {b.Id}（{b.Line}行）";
                if (!ids.Add(b.Id)) problems.Add($"{at}: id が重複している");
                if (!IsGoodId(b.Id))
                    problems.Add($"{at}: id は英小文字で始め、英小文字・数字・ハイフンだけ");

                string name = b.One("名前") ?? "";
                string gist = b.One("説明") ?? "";
                if (name.Length == 0) { problems.Add($"{at}: 名前が無い"); continue; }
                if (gist.Length == 0) problems.Add($"{at}: 説明が無い（画面にそのまま出る）");
                if (!names.Add(name)) problems.Add($"{at}: 名前「{name}」が重複している");

                if (!TryType(b.One("型"), out var type))
                {
                    problems.Add($"{at}: 型は アタック/サポート/デバフ/ヒール のどれか（{b.One("型")}）");
                    continue;
                }
                if (!TryTarget(b.One("狙い"), out var target))
                {
                    problems.Add($"{at}: 狙いが読めない（{b.One("狙い")}）");
                    continue;
                }
                if (!int.TryParse(b.One("CT"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int ct))
                {
                    problems.Add($"{at}: CT が数でない（{b.One("CT")}）");
                    continue;
                }

                // ⭐ **書けないことは言葉で受ける。**実装が要るものとして数える。
                bool free = false;
                foreach (var line in b.Many("効果"))
                {
                    if (!line.StartsWith(FreeKind)) continue;
                    free = true;
                    Freeform.Add(b.Id);
                    note.Todo.Add($"{at}「{name}」: {line.Substring(FreeKind.Length).Trim()}");
                }
                string? memo = b.One(MemoKey);
                if (!string.IsNullOrWhiteSpace(memo)) note.Todo.Add($"{at}「{name}」メモ: {memo}");

                var effects = new List<Effect>();
                bool ok = true;
                foreach (var line in b.Many("効果"))
                {
                    if (line.StartsWith(FreeKind)) continue;
                    if (ParseEffect(line, at, problems, out var e)) effects.Add(e);
                    else ok = false;
                }
                if (!ok) continue;
                if (effects.Count == 0)
                {
                    // ⚠️ 自由記述だけの技は「まだ形になっていない」だけ。止めない
                    if (!free) problems.Add($"{at}: 効果が1つも無い");
                    continue;
                }

                // ⭐ パッシブは別の作り方（CT と狙い先は Skill.Always が決める）
                var skill = b.One("パッシブ") == "はい"
                    ? Skill.Always(b.Id, name, gist, type, effects.ToArray())
                    : new Skill(b.Id, name, gist, type, target, effects.ToArray());

                // ⚠️ **CT では止めない**（作者の指示 2026-08-19）。理由を添えて言うだけ。
                if (ct < 0) problems.Add($"{at}: CT が負（{ct}）");
                else
                {
                    bool heavy = Skills.IsHeavyCt(skill);
                    int cap = heavy ? Skills.CtHeavy : Skills.CtCap;
                    if (ct > cap)
                    {
                        note.Say.Add($"{at}: CT {ct} ── 1体が動けるのは1戦闘でおよそ 5.6手なので、"
                            + $"{cap} を超えると**1戦闘に1回**しか撃てません"
                            + (heavy ? "" : "（盤面をひっくり返す技なら " + Skills.CtHeavy + " まで妥当）"));
                    }
                }

                // ⚠️ 型と中身が食い違うと、卵の枠から**出ない**技になる
                var derived = DeriveType(skill);
                if (derived != null && derived != type)
                {
                    note.Say.Add($"{at}: 型「{Skills.LabelOf(type)}」だが中身は"
                        + $"「{Skills.LabelOf(derived.Value)}」に見える（枠から出ない技になりうる）");
                }
                // ⚠️ 相手に掛ける技を味方に向けていないか（挑発が Self だった事故と同じ形）
                bool harmful = false, kind = false;
                foreach (var e in skill.Effects)
                {
                    if (Skills.IsHarmful(e)) harmful = true;
                    else if (e.Kind != EffectKind.Damage) kind = true;
                }
                bool atFoe = target == Target.EnemyOne || target == Target.EnemyAll
                    || target == Target.EnemyRandom;
                if (harmful && !atFoe) note.Say.Add($"{at}: 弱化なのに狙いが敵でない");
                if (kind && !harmful && atFoe) note.Say.Add($"{at}: 味方に効くものを敵へ向けている");

                made.Add(skill);
                if (!Has(Skills.All, b.Id)) fresh.Add(skill);
            }
            return made;
        }

        private static List<Species> ReadSpecies(Notes note, List<Skill> sheetSkills)
        {
            var problems = note.Stop;
            var made = new List<Species>();
            var ids = new HashSet<string>();
            var names = new HashSet<string>();

            foreach (var b in Read(Path.Combine(Dir, SpeciesFile), "種族", problems))
            {
                string at = $"種族 {b.Id}（{b.Line}行）";
                if (!ids.Add(b.Id)) problems.Add($"{at}: id が重複している");
                // ⚠️ **id は C# の名前になる。**`9bad` や `my-mon` を通していた頃、
                //    `9badSprite` というコンパイルできない C# が出ていた（2026-08-19 の監査）。
                if (!IsGoodId(b.Id))
                    problems.Add($"{at}: id は英小文字で始め、英小文字・数字・ハイフンだけ");

                string name = b.One("名前") ?? "";
                if (name.Length == 0) { problems.Add($"{at}: 名前が無い"); continue; }
                if (!names.Add(name)) problems.Add($"{at}: 名前「{name}」が重複している");
                string? spMemo = b.One(MemoKey);
                if (!string.IsNullOrWhiteSpace(spMemo)) note.Todo.Add($"{at}「{name}」メモ: {spMemo}");

                string skill1 = b.One("枠1") ?? "";
                if (!Known(skill1, sheetSkills)) problems.Add($"{at}: 枠1 の技 {skill1} が見つからない");
                // ⚠️ 特性は種族に1つ（2026-08-21）。⭐ 抜けていると誰も持てない特性が増える
                string traitId = b.One("特性") ?? "";
                if (traitId.Length == 0) problems.Add($"{at}: 特性が書かれていない");
                else if (!Traits.Has(traitId)) problems.Add($"{at}: 特性 {traitId} が見つからない");

                if (!TryStats(b.One("基礎"), at, problems, out var stats)) continue;
                int total = Stats.TotalOf(stats);   // ⭐ Stats.Keys は6本なので弱化2本も入る
                // ⚠️ **合計も止めない。**⭐ ただし `SpeciesTable.Audit` が起動時に落とすので、
                //    そのまま貼ると検査が赤くなることは、はっきり言う。
                if (total != SpeciesTable.BaseTotal)
                {
                    note.Say.Add($"{at}: 基礎値の合計が {total}（他は {SpeciesTable.BaseTotal}）"
                        + " ── このまま貼ると SpeciesTable.Audit が落ちます");
                }
                int pair = stats.Acc + stats.Res;
                if (pair != SpeciesTable.DebuffBaseTotal)
                {
                    note.Say.Add($"{at}: 弱化命中＋弱化耐性が {pair}"
                        + $"（他は {SpeciesTable.DebuffBaseTotal}）── 同上");
                }

                var pools = new SkillPool?[2];
                for (int slot = 0; slot < 2; slot++)
                {
                    string label = slot == 0 ? "枠2" : "枠3";
                    pools[slot] = TryPool(b.One(label), $"{at} {label}", sheetSkills, problems);
                    if (pools[slot] == null) continue;
                    // ⚠️ **枠1 の技が袋に入っているのは咎めない。**
                    //    孵化は `Skills.SlotPoolOf` が枠1 を外してから引くので害が無く、
                    //    既存3種族（ノビル・キバネ・ヌシ）が現にそう書いてある。
                    // ⭐ 本当に困るのは「外すと空になる」ほう ── そこだけ数える。
                    int usable = 0;
                    foreach (var id in pools[slot]!.Pool) if (id != skill1) usable++;
                    if (usable < 1)
                        problems.Add($"{at}: {label} が枠1 の {skill1} を除くと空になる");
                }
                if (pools[0] == null || pools[1] == null) continue;

                if (b.Grid.Count == 0) { problems.Add($"{at}: 姿が無い"); continue; }
                if (!TryGrid(b.Grid, at, problems, out var sprite)) continue;

                var palettes = PalettesOf(b, at, problems);
                if (palettes.Count == 0) { problems.Add($"{at}: 色が1行も無い"); continue; }

                // ⚠️ **姿の添字が色数を超えていないか。**通していた頃、色4つの種族に
                //    「5」で描いた姿を許し、**遊びで描いた瞬間に例外**で落ちていた
                //    （Palette.ColorOf が投げる／2026-08-19 の監査）。
                int biggest = 0;
                foreach (var row in b.Grid)
                    foreach (char c in row)
                    {
                        int at2 = PixelSprite.IndexOf(c);
                        if (at2 > biggest) biggest = at2;
                    }
                foreach (var pal in palettes)
                {
                    if (biggest <= pal.Count) continue;
                    problems.Add($"{at}: 姿が色 {biggest} 番を使っているのに、色が {pal.Count} つしかない");
                    break;
                }

                made.Add(new Species(b.Id, name, skill1, b.One("特性") ?? "",
                    stats, sprite, palettes, pools[0]!, pools[1]!));
            }
            return made;
        }

        /// <summary>特性を読む。⭐ **中身は文章のまま受け取る。**
        /// ⚠️ 効き目は <see cref="Battle.React"/> に手で書くものなので、ここでは組み立てない。
        /// ⭐ 検査がすることは「実装が要る」と数えることと、場面の語を照らすことだけ。</summary>
        private static int ReadTraits(Notes note)
        {
            int count = 0;
            var ids = new HashSet<string>();
            foreach (var b in Read(Path.Combine(Dir, TraitFile), "特性", note.Stop))
            {
                count++;
                string at = $"特性 {b.Id}（{b.Line}行）";
                if (!ids.Add(b.Id)) note.Stop.Add($"{at}: id が重複している");
                string name = b.One("名前") ?? "";
                if (name.Length == 0) { note.Stop.Add($"{at}: 名前が無い"); continue; }

                string when = b.One("働く場面") ?? "";
                bool known = false;
                foreach (TraitWhen w in Enum.GetValues(typeof(TraitWhen)))
                    if (Traits.LabelOf(w) == when) { known = true; break; }
                if (!known)
                {
                    // ⭐ **知らない場面でも通す。**新しい割り込み先が要ると言うだけ。
                    note.Todo.Add($"{at}「{name}」: 場面「{when}」は Battle.React に無い"
                        + " ── TraitWhen に足して割り込み先を作る必要があります");
                }

                string gist = b.One("すること") ?? "";
                if (gist.Length == 0) note.Say.Add($"{at}: すること が空（画面に出ます）");

                bool made = Traits.All.Count > 0 && Has(b.Id);
                if (!made) note.Todo.Add($"{at}「{name}」: {gist}"
                    + (b.One(MemoKey) is string m && m.Length > 0 ? $" ／ メモ: {m}" : ""));
                else if (b.One(MemoKey) is string m2 && m2.Length > 0)
                    note.Todo.Add($"{at}「{name}」メモ: {m2}");
            }
            return count;
        }

        private static bool Has(string traitId)
        {
            foreach (var t in Traits.All) if (t.Id == traitId) return true;
            return false;
        }

        // ── 小分けの読み取り ────────────────────────────

        private static bool ParseEffect(string line, string at, List<string> problems, out Effect made)
        {
            if (!ParseEffectCore(line, at, problems, out made)) return false;

            // ⭐ **1手2役。**この効果だけ、技の狙い先と違う相手へ飛ばす。
            // ⚠️ ここを飛ばすと、書いた飛び先が黙って消えて別の技になる。
            const string mark = "飛び先:";
            int found = line.IndexOf(mark, StringComparison.Ordinal);
            if (found >= 0)
            {
                string word = line.Substring(found + mark.Length).Split(' ')[0];
                if (!TryTarget(word, out var aside))
                {
                    problems.Add($"{at}: 飛び先が読めない（{word}）");
                    return false;
                }
                made = made.To(aside);
            }
            return Trailing(line, at, problems, ref made);
        }

        /// <summary>条件と数えを読む。⚠️ 飛び先の有無に関わらず必ず通す
        /// （⭐ 飛び先が無いときに素通りさせていた頃、条件が黙って消えて値段まで変わった）。</summary>
        private static bool Trailing(string line, string at, List<string> problems, ref Effect made)
        {
            int found = line.IndexOf("条件:", StringComparison.Ordinal);
            if (found >= 0)
            {
                string word = line.Substring(found + 3).Split(' ')[0];
                if (!TryWhen(word, out var when))
                { problems.Add($"{at}: 条件が読めない（{word}）"); return false; }
                made = made.If(when);
            }
            found = line.IndexOf("数え:", StringComparison.Ordinal);
            if (found >= 0)
            {
                string word = line.Substring(found + 3).Split(' ')[0];
                if (!TryTally(word, out var per))
                { problems.Add($"{at}: 数え方が読めない（{word}）"); return false; }
                made = made.Each(per);
            }
            return true;
        }

        private static bool ParseEffectCore(string line, string at, List<string> problems, out Effect made)
        {
            made = default;
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) { problems.Add($"{at}: 空の効果行"); return false; }

            // ⭐ **前の語でも読む。**帳面は手で書いたものが残るので、語を変えても壊さない
            string word = Rename(parts[0]);
            EffectKind? kind = null;
            foreach (var (w, k) in Kinds) if (w == word) { kind = k; break; }
            if (kind == null)
            {
                problems.Add($"{at}: 「{word}」という効果は無い");
                return false;
            }

            // 札を拾う
            var tags = new Dictionary<string, string>();
            bool pierce = false;
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i] == "防御無視") { pierce = true; continue; }
                int c = parts[i].IndexOf(':');
                if (c < 0) { problems.Add($"{at}: 「{parts[i]}」は「札:値」の形で書く"); return false; }
                tags[parts[i].Substring(0, c)] = parts[i].Substring(c + 1);
            }

            int Num(string tag, int fallback)
            {
                if (!tags.TryGetValue(tag, out var v)) return fallback;
                if (!int.TryParse(v, NumberStyles.Integer | NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture, out int n))
                {
                    problems.Add($"{at}: {tag}:{v} が数でない");
                    return fallback;
                }
                // ⚠️ **数えるものは1以上。**負を通していた頃、毒 スタック:-3 が
                //    手ぶん −3.48 で並び、極端な数は int が溢れていた（2026-08-19 の監査）。
                bool counts = tag == "ターン" || tag == "スタック" || tag == "個数"
                    || tag == "回数" || tag == "発数";
                if (counts && n < 1)
                {
                    problems.Add($"{at}: {tag} は1以上（{n}）");
                    return fallback;
                }
                if (n > 9999 || n < -9999)
                {
                    problems.Add($"{at}: {tag} が極端（{n}）── 桁を確かめてください");
                    return fallback;
                }
                return n;
            }

            /// ⭐ 符号が意味を持つ札（解除の個数）。⚠️ 「1以上」の縛りを掛けない。
            int Signed(string tag, int fallback)
            {
                if (!tags.TryGetValue(tag, out var v)) return fallback;
                if (!int.TryParse(v, NumberStyles.Integer | NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture, out int n))
                {
                    problems.Add($"{at}: {tag}:{v} が数でない");
                    return fallback;
                }
                if (n > 9999 || n < -9999)
                {
                    problems.Add($"{at}: {tag} が極端（{n}）── 桁を確かめてください");
                    return fallback;
                }
                return n;
            }

            /// ⭐ 強化・弱化の持続だけ「永続」を通す（<see cref="Skills.Lasting"/>）。
            /// ⚠️ 毒やスタンには通さない ── 切れない毒は勝負が終わらない。
            int Turns(int fallback)
            {
                if (tags.TryGetValue("ターン", out var v) && v == Everlasting) return Skills.Lasting;
                return Num("ターン", fallback);
            }

            // ⚠️ **下限は実装から引く。**1〜100 で通していた頃、確率10 と書いた技が
            //    黙って 20 に切り上がっていた（Effect の下限）。
            int chance = Num("確率", 100);
            if (chance < Effect.MinChance || chance > 100)
            {
                problems.Add($"{at}: 確率 {chance} は {Effect.MinChance}〜100"
                    + $"（{Effect.MinChance} 未満は実装が {Effect.MinChance} に切り上げます）");
                return false;
            }

            switch (kind.Value)
            {
                case EffectKind.Damage:
                {
                    if (!TryPower(tags.TryGetValue("威力", out var p) ? p : null, out var tier))
                    { problems.Add($"{at}: 威力は 小/中/大/特大（{p}）"); return false; }
                    // ⚠️ **知らない語を黙って「攻撃」にしない。**
                    //    `== "防御" ? Def : Atk` と書いていたときは、
                    //    「依存:スピード」が黙って攻撃依存になっていた（2026-08-19）。
                    tags.TryGetValue("依存", out var d);
                    if (!Skills.TryScale(d ?? Skills.LabelOf(DamageScale.Atk), out var scale))
                    { problems.Add($"{at}: 依存は 攻撃/防御/スピード（{d}）"); return false; }
                    made = Effect.Damage(tier, scale, Num("発数", 1), pierce);
                    return true;
                }
                case EffectKind.Buff when word == KindInnate:
                {
                    if (!TryStat(tags.TryGetValue("ステ", out var i) ? i : null, out var innateStat))
                    { problems.Add($"{at}: ステが読めない（{i}）"); return false; }
                    tags.TryGetValue("向き", out var way);
                    if (way != "上" && way != "下")
                    { problems.Add($"{at}: 向きは 上/下（{way}）"); return false; }
                    made = Effect.Always(innateStat, way == "上" ? 1 : -1);
                    return true;
                }
                case EffectKind.Buff:
                {
                    if (!TryStat(tags.TryGetValue("ステ", out var s) ? s : null, out var stat))
                    { problems.Add($"{at}: ステは 攻撃力/防御力/スピード（{s}）"); return false; }
                    // ⚠️ HP・弱化命中・弱化耐性には修正枠が無い（Battle が持っていない）
                    if (stat != StatKey.Atk && stat != StatKey.Def && stat != StatKey.Spd)
                    { problems.Add($"{at}: {Stats.LabelOf(stat)} は強化・弱化できない"); return false; }
                    made = Effect.Buff(stat, word == KindBuffUp ? 1 : -1, Turns(3), chance);
                    return true;
                }
                case EffectKind.Poison:
                    made = Effect.Poison(Num("スタック", 1), Num("ターン", 4), chance); return true;
                case EffectKind.Regen:
                    made = Effect.Regen(Num("スタック", 1), Num("ターン", 4), chance); return true;
                case EffectKind.HealRatio:
                {
                    int pct = Num("割合", 30);
                    if (pct == 0) { problems.Add($"{at}: 割合が 0"); return false; }
                    made = Effect.HealRatio(pct, chance); return true;
                }
                case EffectKind.Revive:
                    made = Effect.Revive(Num("割合", 50), chance); return true;
                case EffectKind.Shield:
                    made = Effect.Shield(Num("個数", 2), chance); return true;
                case EffectKind.Dispel:
                {
                    // ⭐ 正なら強化を剥がす／負なら弱化を剥がす（味方の毒などを治す）
                    int n = Signed("個数", 1);
                    if (n == 0) { problems.Add($"{at}: 個数が 0"); return false; }
                    made = Effect.Dispel(n, chance); return true;
                }
                case EffectKind.Steal:
                    made = Effect.Steal(Num("個数", 1), chance); return true;
                case EffectKind.Stun:
                    made = Effect.Stun(Num("ターン", 1), chance); return true;
                case EffectKind.Sleep:
                    made = Effect.Sleep(Num("ターン", 2), chance); return true;
                case EffectKind.Block:
                    made = Effect.Block(Num("ターン", 2), chance); return true;
                case EffectKind.Guts:
                    made = Effect.Guts(Num("ターン", 3), chance); return true;
                case EffectKind.Immune:
                    made = Effect.Immune(Num("ターン", 3), chance); return true;
                case EffectKind.Ct:
                {
                    int delta = Num("増減", 0);
                    if (delta == 0) { problems.Add($"{at}: CT の増減が 0"); return false; }
                    made = Effect.Ct(delta, chance); return true;
                }
                case EffectKind.Gauge:
                {
                    int pct = Num("割合", 0);
                    if (pct == 0) { problems.Add($"{at}: ゲージの割合が 0"); return false; }
                    made = Effect.Gauge(pct, chance); return true;
                }
                case EffectKind.Taunt:
                    made = Effect.Taunt(Num("回数", 3), chance); return true;
                default:
                    problems.Add($"{at}: 「{word}」を組み立てられない");
                    return false;
            }
        }

        /// <summary>色の組を読む。⭐ **唯一の読み方**（本番も往復の検査も画面もこの規則）。</summary>
        private static List<Palette> PalettesOf(Block b, string at, List<string> problems)
        {
            var made = new List<Palette>();
            foreach (var line in b.Many("色"))
            {
                var kept = new List<string>();
                foreach (var c in line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (c.Length == 7 && c[0] == '#') kept.Add(c);
                    else if (!c.StartsWith("#")) break;   // ⭐ 末尾の覚え書きはここで切る
                    else problems.Add($"{at}: 色 {c} は #rrggbb の形で書く");
                }
                // ⚠️ **数は決め打ちにしない**（2026-08-21）。⭐ 種族ごとに色数が違ってよい
                //    （タマルは 11色・他は 4色）。⚠️ ただし**1件の中では揃える** ──
                //    通常色が11で変異色が4だと、変異させた瞬間に落ちる。
                if (kept.Count == 0)
                {
                    problems.Add($"{at}: 色が1つも書かれていない行がある");
                }
                else if (kept.Count > PixelSprite.MaxIndex)
                {
                    problems.Add($"{at}: 色が {kept.Count} 個（上限 {PixelSprite.MaxIndex}）");
                }
                else if (made.Count > 0 && kept.Count != made[0].Count)
                {
                    problems.Add($"{at}: 色の数が組ごとに違う（{made[0].Count} と {kept.Count}）"
                        + " ── 変異させた瞬間に落ちる");
                }
                else made.Add(new Palette(kept.ToArray()));
            }
            return made;
        }

        private static bool TryStats(string? line, string at, List<string> problems, out StatBlock made)
        {
            made = new StatBlock(0, 0, 0, 0);
            if (line == null) { problems.Add($"{at}: 基礎が無い"); return false; }
            var got = new Dictionary<StatKey, int>();
            foreach (var token in line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int c = token.IndexOf(':');
                if (c < 0) { problems.Add($"{at}: 基礎は「ステ名:数」を並べる（{token}）"); return false; }
                if (!TryStat(token.Substring(0, c), out var key))
                { problems.Add($"{at}: 「{token.Substring(0, c)}」というステは無い"); return false; }
                if (!int.TryParse(token.Substring(c + 1), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int v))
                { problems.Add($"{at}: {token} が数でない"); return false; }
                got[key] = v;
            }
            foreach (var key in Stats.Keys)
            {
                if (!got.ContainsKey(key)) { problems.Add($"{at}: 基礎に {Stats.LabelOf(key)} が無い"); return false; }
                made = made.With(key, got[key]);
            }
            return true;
        }

        private static SkillPool? TryPool(string? line, string at, List<Skill> sheetSkills,
            List<string> problems)
        {
            if (line == null) { problems.Add($"{at}: 袋が無い"); return null; }

            // ⚠️ **古い書き方も読む。**2026-08-19 より前の帳面は「型 / 技 技 技」だった。
            //    ⭐ 型はもう縛りではないので、左側があれば黙って捨てる。
            int slash = line.IndexOf('/');
            if (slash >= 0) line = line.Substring(slash + 1);

            var ids = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (ids.Length == 0) { problems.Add($"{at}: 技が1つも無い"); return null; }
            if (ids.Length > Skills.PoolMax)
            {
                problems.Add($"{at}: 袋に {ids.Length} 件（上限 {Skills.PoolMax}）"
                    + " ── 狙える確率はここで決まります");
            }
            var seen = new HashSet<string>();
            foreach (var id in ids)
            {
                if (!seen.Add(id)) problems.Add($"{at}: {id} が2回入っている");
                if (Find(id, sheetSkills) == null) problems.Add($"{at}: 技 {id} が見つからない");
            }
            return new SkillPool(ids);
        }

        private static bool TryGrid(List<string> rows, string at, List<string> problems,
            out PixelSprite made)
        {
            made = null!;
            int width = rows[0].Length;
            foreach (var r in rows)
            {
                if (r.Length != width)
                { problems.Add($"{at}: 姿の幅が揃っていない（{width} と {r.Length}）"); return false; }
                foreach (char c in r)
                {
                    // ⭐ 読める文字は Core が決める（ここに書き写さない）
                    if (PixelSprite.IndexOf(c) >= 0) continue;
                    problems.Add($"{at}: 姿に '{c}' がある（'.' と '{PixelSprite.Digits}' だけ）");
                    return false;
                }
            }
            if (rows.Count != width)
                problems.Add($"{at}: 姿が {width}×{rows.Count}（正方形にする）");
            made = PixelSprite.Parse(rows.ToArray());
            return true;
        }

        private static bool TryType(string? word, out SkillType type)
        {
            foreach (SkillType t in Enum.GetValues(typeof(SkillType)))
            {
                if (Skills.LabelOf(t) == word) { type = t; return true; }
            }
            type = SkillType.Attack;
            return false;
        }

        private static bool TryTarget(string? word, out Target target)
        {
            foreach (Target t in Enum.GetValues(typeof(Target)))
            {
                if (SkillText.TargetOf(t) == word) { target = t; return true; }
            }
            target = Target.EnemyOne;
            return false;
        }

        private static bool TryPower(string? word, out PowerTier tier)
        {
            foreach (PowerTier t in Enum.GetValues(typeof(PowerTier)))
            {
                if (Skills.LabelOf(t) == word) { tier = t; return true; }
            }
            tier = PowerTier.Medium;
            return false;
        }

        private static bool TryStat(string? word, out StatKey key)
        {
            foreach (var k in Stats.Keys) if (Stats.LabelOf(k) == word) { key = k; return true; }
            key = StatKey.Hp;
            return false;
        }

        /// <summary>中身から型を見立てる。⚠️ **見立てられないときは null**（口出ししない）。</summary>
        private static SkillType? DeriveType(Skill skill)
        {
            bool damage = false, harmful = false, heal = false, boon = false;
            foreach (var e in skill.Effects)
            {
                if (e.Kind == EffectKind.Damage) damage = true;
                else if (Skills.IsHarmful(e)) harmful = true;
                else if (e.Kind == EffectKind.HealRatio || e.Kind == EffectKind.Regen
                    || e.Kind == EffectKind.Revive) heal = true;
                else boon = true;
            }
            // ⚠️ **複合技には口を出さない。**「ダメージ＋弱化」をデバフ型で出すのは
            //    作り手の宣言（Skill.Type の但し書き）── 痺れ打ちがまさにそれ。
            if (damage) return harmful || heal || boon ? (SkillType?)null : SkillType.Attack;
            if (harmful && !heal && !boon) return SkillType.Debuff;
            if (heal && !harmful && !boon) return SkillType.Heal;
            if (boon && !harmful && !heal) return SkillType.Support;
            return null;
        }

        private static bool Has(IReadOnlyList<Skill> list, string id)
        {
            foreach (var s in list) if (s.Id == id) return true;
            return false;
        }

        private static Skill? Find(string id, List<Skill> sheet)
        {
            foreach (var s in sheet) if (s.Id == id) return s;
            foreach (var s in Skills.All) if (s.Id == id) return s;
            return null;
        }

        private static bool Known(string id, List<Skill> sheet) => Find(id, sheet) != null;

        /// <summary>帳面のものを表に混ぜたら、起動時の検査が何を言うか。
        ///
        /// ⭐ **規則を書き写さない。**<see cref="Skills.Faults"/> と
        /// <see cref="SpeciesTable.Faults"/> をそのまま呼ぶ。
        /// ⚠️ 写すと必ず片方が古くなる（この道具が何度も踏んだ形）。</summary>
        private static void Preflight(List<Skill> skills, List<Species> species, Notes note)
        {
            try
            {
                foreach (var p in Skills.Faults(skills, species)) note.Fatal.Add("技表: " + p);
                foreach (var p in SpeciesTable.Faults(species, skills)) note.Fatal.Add("種族表: " + p);
            }
            catch (Exception e)
            {
                note.Fatal.Add("検査そのものが落ちました: " + e.Message);
            }
        }

        // ══ 往復を確かめるための口 ═══════════════════════
        // ⭐ 検査（SheetTests）が「書いて読んで元に戻るか」を数えるために使う。
        // ⚠️ **本番の読み書きと同じ関数を通す。**別経路にすると、検査だけ通って本番が壊れる。

        /// <summary>帳面の1件（技）を読む。⚠️ 読めなければ null。</summary>
        public static Skill? SkillOf(string text)
        {
            var note = new Notes();
            var blocks = ReadText(text.Split('\n'), "技", note.Stop);
            if (blocks.Count != 1) return null;
            var b = blocks[0];
            if (!TryType(b.One("型"), out var type)) return null;
            if (!TryTarget(b.One("狙い"), out var target)) return null;
            if (!int.TryParse(b.One("CT"), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int ct)) return null;
            var effects = new List<Effect>();
            foreach (var line in b.Many("効果"))
            {
                if (line.StartsWith(FreeKind)) continue;
                if (!ParseEffect(line, "", note.Stop, out var e)) return null;
                effects.Add(e);
            }
            if (effects.Count == 0 || note.Stop.Count > 0) return null;
            if (b.One("パッシブ") == "はい")
            {
                return Skill.Always(b.Id, b.One("名前") ?? "", b.One("説明") ?? "",
                    type, effects.ToArray());
            }
            return new Skill(b.Id, b.One("名前") ?? "", b.One("説明") ?? "",
                type, target, effects.ToArray());
        }

        /// <summary>帳面の1件（種族）を読む。⚠️ 読めなければ null。</summary>
        public static Species? SpeciesOf(string text)
        {
            var note = new Notes();
            var blocks = ReadText(text.Split('\n'), "種族", note.Stop);
            if (blocks.Count != 1) return null;
            var b = blocks[0];
            if (!TryStats(b.One("基礎"), "", note.Stop, out var stats)) return null;
            var slot2 = TryPool(b.One("枠2"), "", new List<Skill>(), note.Stop);
            var slot3 = TryPool(b.One("枠3"), "", new List<Skill>(), note.Stop);
            if (slot2 == null || slot3 == null) return null;
            if (b.Grid.Count == 0) return null;
            if (!TryGrid(b.Grid, "", note.Stop, out var sprite)) return null;
            // ⚠️ **本番と同じ読み方を通す。**別々に書いていた頃、往復の検査だけが
            //    色の数を見ておらず、3色の組が検査を素通りしていた（2026-08-19 の監査）。
            var palettes = PalettesOf(b, "", note.Stop);
            if (palettes.Count == 0) return null;
            return new Species(b.Id, b.One("名前") ?? "", b.One("枠1") ?? "",
                b.One("特性") ?? "", stats, sprite, palettes, slot2, slot3);
        }

        /// <summary>帳面の1件（特性）を読む。⚠️ 読めなければ null。</summary>
        public static Trait? TraitOf(string text)
        {
            var note = new Notes();
            var blocks = ReadText(text.Split('\n'), "特性", note.Stop);
            if (blocks.Count != 1) return null;
            var b = blocks[0];
            string when = b.One("働く場面") ?? "";
            foreach (TraitWhen w in Enum.GetValues(typeof(TraitWhen)))
            {
                if (Traits.LabelOf(w) != when) continue;
                return new Trait(b.Id, b.One("名前") ?? "", w,
                    b.One("すること") ?? "", b.One("噛み合うもの") ?? "");
            }
            return null;
        }

        /// <summary>種族1件を帳面の書き方で。⭐ 書き出しと同じ道を通る。</summary>
        public static string BlockOf(Species sp)
        {
            var md = new StringBuilder();
            md.Append($"# 種族 {sp.Id}\n名前 = {sp.Name}\n枠1 = {sp.Skill1}\n"
                + $"特性 = {sp.TraitId}\n基礎 = ");
            for (int i = 0; i < Stats.Keys.Length; i++)
            {
                var key = Stats.Keys[i];
                md.Append(i > 0 ? " " : "").Append(Stats.LabelOf(key)).Append(':').Append(sp.Base[key]);
            }
            md.Append('\n');
            // ⭐ 型は書かない（2026-08-19 に縛りを外した）。袋は技を並べるだけ
            md.Append($"枠2 = {string.Join(" ", sp.Slot2.Pool)}\n");
            md.Append($"枠3 = {string.Join(" ", sp.Slot3.Pool)}\n");
            md.Append("姿 =\n");
            for (int y = 0; y < sp.Sprite.Height; y++)
            {
                md.Append("  ");
                // ⭐ 添字 → 文字も Core を通す（規則を2か所に持たない）
                for (int x = 0; x < sp.Sprite.Width; x++) md.Append(PixelSprite.CharOf(sp.Sprite.At(x, y)));
                md.Append('\n');
            }
            foreach (var p in sp.Palettes) md.Append($"色 = {string.Join(" ", p.Colors)}\n");
            return md.ToString();
        }

        // ══ C# を出す ═══════════════════════════════════

        private static void Code()
        {
            var n = new Notes();
            var skills = ReadSkills(n, out _);
            var species = ReadSpecies(n, skills);
            ReadTraits(n);
            if (n.Stop.Count > 0)
            {
                Console.WriteLine("🚧 そのままでは実装にできない書き方があるので C# は出しません。");
                Console.WriteLine("   `sim sheet check` を見てください。");
                return;
            }
            // ⚠️ **「気になること」では止めない。**決めるのは作り手（作者の指示 2026-08-19）。
            if (n.Say.Count > 0)
                Console.WriteLine($"// ⚠️ 気になることが {n.Say.Count} 件あります（check で読めます）");
            if (n.Todo.Count > 0)
            {
                Console.WriteLine($"// ✍️ 手で書く必要があるものが {n.Todo.Count} 件:");
                foreach (var t in n.Todo) Console.WriteLine("//    ・" + t);
            }

            var newSkills = new List<Skill>();
            foreach (var s in skills) if (!Same(s)) newSkills.Add(s);
            var newSpecies = new List<Species>();
            foreach (var sp in species) if (!Same(sp)) newSpecies.Add(sp);

            if (newSkills.Count == 0 && newSpecies.Count == 0)
            {
                Console.WriteLine("⭐ 帳面と実装は同じです。貼るものはありません。");
                return;
            }

            if (newSkills.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"// ══ Skills.cs の List に入れる（{newSkills.Count} 件）══");
                foreach (var s in newSkills) Console.WriteLine(CodeOf(s));
            }
            if (newSpecies.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"// ══ Species.cs に入れる（{newSpecies.Count} 件）══");
                foreach (var sp in newSpecies) Console.WriteLine(CodeOf(sp));
            }
            Console.WriteLine();
            Console.WriteLine("// ⚠️ 貼ったら `dotnet test` を通し、`sim wiki` で一覧を作り直すこと。");
        }

        /// <summary>実装の同じ id と**一字一句同じか**。⭐ 違えば「貼るもの」。</summary>
        private static bool Same(Skill s)
        {
            foreach (var m in Skills.All)
            {
                if (m.Id != s.Id) continue;
                if (m.Name != s.Name || m.Gist != s.Gist || m.Type != s.Type
                    || m.Ct != s.Ct || m.Target != s.Target
                    || m.Effects.Count != s.Effects.Count) return false;
                for (int i = 0; i < m.Effects.Count; i++)
                {
                    if (EffectLine(m.Effects[i]) != EffectLine(s.Effects[i])) return false;
                }
                return true;
            }
            return false;
        }

        private static bool Same(Species sp)
        {
            foreach (var m in SpeciesTable.All)
            {
                if (m.Id != sp.Id) continue;
                if (m.Name != sp.Name || m.Skill1 != sp.Skill1) return false;
                foreach (var key in Stats.Keys) if (m.Base[key] != sp.Base[key]) return false;
                if (!Same(m.Slot2, sp.Slot2) || !Same(m.Slot3, sp.Slot3)) return false;
                if (m.Sprite.Width != sp.Sprite.Width || m.Sprite.Height != sp.Sprite.Height) return false;
                for (int i = 0; i < m.Sprite.Pixels.Length; i++)
                    if (m.Sprite.Pixels[i] != sp.Sprite.Pixels[i]) return false;
                if (m.Palettes.Count != sp.Palettes.Count) return false;
                for (int i = 0; i < m.Palettes.Count; i++)
                {
                    if (string.Join(" ", m.Palettes[i].Colors)
                        != string.Join(" ", sp.Palettes[i].Colors)) return false;
                }
                return true;
            }
            return false;
        }

        private static bool Same(SkillPool a, SkillPool b) =>
            string.Join(" ", a.Pool) == string.Join(" ", b.Pool);

        private static string CodeOf(Skill s)
        {
            var sb = new StringBuilder();
            // ⭐ パッシブは CT も狙い先も持たない（Skill.Always が決める）
            if (s.Passive)
            {
                sb.Append($"Skill.Always(\"{s.Id}\", \"{Quote(s.Name)}\", \"{Quote(s.Gist)}\", ")
                  .Append($"SkillType.{s.Type},\n    ");
            }
            else
            {
                sb.Append($"new Skill(\"{s.Id}\", \"{Quote(s.Name)}\", \"{Quote(s.Gist)}\", SkillType.{s.Type}, ")
                  .Append($"{s.Ct}, Target.{s.Target},\n    ");
            }
            for (int i = 0; i < s.Effects.Count; i++)
            {
                if (i > 0) sb.Append(",\n    ");
                var e = s.Effects[i];
                sb.Append(CodeOf(e));
                // ⭐ 欄で足したものは、生成する C# でも欄として出す
                if (e.Own != null) sb.Append($".To(Target.{e.Own.Value})");
                if (e.When != null) sb.Append($".If(SkillWhen.{e.When.Value})");
                if (e.Per != Tally.None) sb.Append($".Each(Tally.{e.Per})");
            }
            sb.Append("),");
            return sb.ToString();
        }

        /// <summary>C# の文字列に入れられる形にする。
        /// ⚠️ 名前や説明に <c>"</c> を入れられたとき、出てくる C# が**コンパイルできなかった**
        /// （帳面は通すので気づけない／2026-08-19 の監査）。</summary>
        private static string Quote(string text) =>
            text.Replace("\\", "\\\\").Replace("\"", "\\\"");

        /// <summary>C# の名前として使える id か。</summary>
        private static bool IsGoodId(string id)
        {
            if (id.Length == 0 || id[0] < 'a' || id[0] > 'z') return false;
            foreach (char c in id)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-';
                if (!ok) return false;
            }
            return true;
        }

        private static string CodeOf(Effect e)
        {
            string chance = e.Chance < 100 ? $", chance: {e.Chance}" : "";
            switch (e.Kind)
            {
                case EffectKind.Damage:
                {
                    var sb = new StringBuilder($"Effect.Damage(PowerTier.{e.Power}, DamageScale.{e.Scale}");
                    if (e.Repeat > 1 || e.Pierce) sb.Append($", {e.Repeat}");
                    if (e.Pierce) sb.Append(", pierce: true");
                    return sb.Append(')').ToString();
                }
                case EffectKind.Buff when e.Innate:
                    return $"Effect.Always(StatKey.{e.Stat}, {e.Sign})";
                case EffectKind.Buff:
                    return $"Effect.Buff(StatKey.{e.Stat}, {e.Sign}, {e.Turns}{chance})";
                case EffectKind.Poison: return $"Effect.Poison({e.Stacks}, {e.Turns}{chance})";
                case EffectKind.Regen: return $"Effect.Regen({e.Stacks}, {e.Turns}{chance})";
                case EffectKind.HealRatio: return $"Effect.HealRatio({e.Percent}{chance})";
                case EffectKind.Revive: return $"Effect.Revive({e.Percent}{chance})";
                case EffectKind.Shield: return $"Effect.Shield({e.Count}{chance})";
                case EffectKind.Dispel: return $"Effect.Dispel({e.Count}{chance})";
                case EffectKind.Steal: return $"Effect.Steal({e.Count}{chance})";
                case EffectKind.Stun: return $"Effect.Stun({e.Turns}{chance})";
                case EffectKind.Sleep: return $"Effect.Sleep({e.Turns}{chance})";
                case EffectKind.Block: return $"Effect.Block({e.Turns}{chance})";
                case EffectKind.Guts: return $"Effect.Guts({e.Turns}{chance})";
                case EffectKind.Immune: return $"Effect.Immune({e.Turns}{chance})";
                case EffectKind.Ct: return $"Effect.Ct({e.Delta}{chance})";
                case EffectKind.Gauge: return $"Effect.Gauge({e.Percent}{chance})";
                case EffectKind.Taunt: return $"Effect.Taunt({e.Hits}{chance})";
                default: throw new ArgumentOutOfRangeException(nameof(e), e.Kind, "C# にできない効果");
            }
        }

        private static string CodeOf(Species sp)
        {
            // ⚠️ ハイフンを落としてから繋ぐ（`my-mon` → `MyMon`）
            var name = new StringBuilder();
            bool up = true;
            foreach (char c in sp.Id)
            {
                if (c == '-') { up = true; continue; }
                name.Append(up ? char.ToUpperInvariant(c) : c);
                up = false;
            }
            string big = name.ToString();
            var sb = new StringBuilder();

            sb.Append($"// ── {sp.Name} ──\n");
            sb.Append($"private static readonly PixelSprite {big}Sprite = PixelSprite.Parse(new[]\n{{\n");
            for (int y = 0; y < sp.Sprite.Height; y++)
            {
                sb.Append("    \"");
                for (int x = 0; x < sp.Sprite.Width; x++)
                {
                    byte v = sp.Sprite.At(x, y);
                    sb.Append(v == 0 ? '.' : (char)('0' + v));
                }
                sb.Append("\",\n");
            }
            sb.Append("});\n\n");

            sb.Append($"private static readonly Palette[] {big}Palettes =\n{{\n");
            for (int i = 0; i < sp.Palettes.Count; i++)
            {
                sb.Append("    new Palette(");
                for (int c = 0; c < sp.Palettes[i].Colors.Length; c++)
                    sb.Append(c > 0 ? ", " : "").Append('"').Append(sp.Palettes[i].Colors[c]).Append('"');
                sb.Append(i == 0 ? "), // 通常\n" : "), // 変異\n");
            }
            sb.Append("};\n\n");

            sb.Append($"new Species(\"{sp.Id}\", \"{Quote(sp.Name)}\", \"{sp.Skill1}\",\n");
            sb.Append($"    new StatBlock({sp.Base.Hp}, {sp.Base.Atk}, {sp.Base.Def}, ")
              .Append($"{sp.Base.Spd}, {sp.Base.Acc}, {sp.Base.Res}), {big}Sprite, {big}Palettes,\n");
            sb.Append($"    {CodeOf(sp.Slot2)},\n    {CodeOf(sp.Slot3)}),");
            return sb.ToString();
        }

        private static string CodeOf(SkillPool pool)
        {
            var sb = new StringBuilder("new SkillPool(");
            for (int i = 0; i < pool.Pool.Count; i++)
                sb.Append(i > 0 ? ", " : "").Append('"').Append(pool.Pool[i]).Append('"');
            return sb.Append(')').ToString();
        }
    }
}
