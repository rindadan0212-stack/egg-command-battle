#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace EggCommand.Sim
{
    /// <summary>手書きの Wiki に、**実装に無い名前**が残っていないかを見る。
    ///
    /// ⚠️ <see cref="WikiPages"/> は「数値を手で転記しない」を表のページで解いたが、
    /// **手書きのページに書かれた `SwayMax` のような名前**は誰も見ていなかった。
    /// ⭐ 2026-08-21 に洗ったら、実装に無い名前が **8つ**残っていた
    /// （`SwayMax` `LiftMax` `RichMob` `RichBoon` `PlainMob` `PlainBane`
    /// `ShortcutShare` `GradeShare`）── どれも「いまの決まり」の表に載っていた。
    ///
    /// ⚠️ **消した名前が Wiki に残るのは、古いだけでなく嘘。**読んだ人は
    /// `Trail.cs` を開いて「無い」と気づくまで探す。
    ///
    /// ⭐ 逃がし方は <see cref="Retired"/> の1本だけ。
    /// ⚠️ 定数を消したら、ここに1行足すか Wiki を直すか**どちらかを必ずやる**ことになる。
    /// それが狙い ── 黙って腐らせない。</summary>
    public static class WikiNames
    {
        /// <summary>⭐ **もう実装に無いが、Wiki が経緯として名前を出してよいもの。**
        ///
        /// ⚠️ 増やすときは「なぜ消したか」を Wiki 側に書いてから。
        /// ⭐ ここは逃がし場所であって、置き場所ではない。</summary>
        private static readonly HashSet<string> Retired = new HashSet<string>
        {
            // ── 盤を「幅から作る」→「筋から作る」に替えて消えた（2026-08-21）
            "ThinRun", "ShortcutShare", "SectionsFor", "JunctionsFor",
            "LaneMin", "LaneMax", "WaysMin", "WaysMax", "ClearPath",
            // ── 揺らぎ（升目がバラバラに見えた）ごと撤回（2026-08-21）
            "SwayMax", "LiftMax",
            // ── 道ごとの中身の偏り。マスの出方の表に一本化した（2026-08-21）
            "RichMob", "RichBoon", "PlainMob", "PlainBane",
            // ── 関門が「検査」だった頃の値段まわり（2026-08-21 に消費へ）
            "ShortShare", "LongShare", "PriceLow", "PriceHigh",
            "GradeStep", "GradePerStep", "GradeShare",
            // ── 技の作り直し案（未実装のまま案として残してある）
            "CtLowered", "CtRepriced", "LateDiscount", "BattleGoldenTests",
            "EffectLabel", "TargetLabel",
            // ── 旧 TS 版のもの（Unity へ載せ替えて消えた）
            "ResizeObserver", "ENEMY", "PARTY",
        };

        /// <summary>⚠️ 名前らしく見えるもの ── 大文字で始まり、途中にも大文字が混じる。
        /// ⭐ `Trail` のような1つ山の語は普通名詞と紛れるので見ない。</summary>
        private static readonly Regex Token =
            new Regex(@"`([A-Z][A-Za-z0-9]*(?:[A-Z][A-Za-z0-9]*)+)(?:\([^`]*\))?`");

        /// <summary>調べた結果を返す。⭐ 空なら合格。</summary>
        public static List<string> Check(string wikiDir, IReadOnlyList<string> codeDirs)
        {
            var code = new StringBuilder();
            foreach (var dir in codeDirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                    code.Append(File.ReadAllText(file)).Append('\n');
            }
            string blob = code.ToString();
            if (blob.Length == 0)
                return new List<string> { "⚠️ コードが1文字も読めなかった（置き場所を確かめて）" };

            var found = new List<string>();
            var seen = new HashSet<string>();
            foreach (var page in Directory.GetFiles(wikiDir, "*.md", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(wikiDir, page);
                var lines = File.ReadAllLines(page);
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (Match m in Token.Matches(lines[i]))
                    {
                        string name = m.Groups[1].Value;
                        if (Retired.Contains(name)) continue;
                        if (Regex.IsMatch(blob, @"\b" + Regex.Escape(name) + @"\b")) continue;
                        if (!seen.Add(name + "@" + rel)) continue;
                        found.Add($"{rel}:{i + 1}  `{name}`");
                    }
                }
            }
            return found;
        }

        /// <summary>⭐ `sim wikinames` の中身。</summary>
        public static void Run()
        {
            // ⚠️ 置き場所は `sim wiki` と同じ決め打ち（呼ぶのは unity-port から）
            var bad = Check("../wiki", new[]
            {
                "../unity/Packages/com.eggcommand.core/Runtime",
                "../unity/Assets",
            });

            Console.WriteLine("■ Wiki に出てくる名前が実装に在るか");
            if (bad.Count == 0)
            {
                Console.WriteLine("  ⭐ 実装に無い名前は 0 件");
                return;
            }
            Console.WriteLine($"  ⚠️ 実装に無い名前が {bad.Count} 件");
            foreach (var line in bad) Console.WriteLine("    " + line);
            Console.WriteLine();
            Console.WriteLine("  ⭐ 直すか、経緯として残すなら WikiNames.Retired に足すこと");
        }
    }
}
