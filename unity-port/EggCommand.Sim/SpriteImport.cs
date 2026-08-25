using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EggCommand.Core;

namespace EggCommand.Sim
{
    /// <summary>作者が描いた手描きの原稿を、Species.cs にそのまま貼れる
    /// C# へ落とす。
    ///
    /// ⭐ **再実行できる取り込み道具**（2026-08-25・作者は今後も絵を描くので使い捨てにしない）。
    /// 使い方: `dotnet run --project EggCommand.Sim -- import-sprite`
    ///   （リポジトリの unity-port から打つ想定。<see cref="SpritePng.Dir"/> 等と同じ相対 "..")
    ///
    /// やること（4つ）:
    /// 1. `art/handmade/sprite/*.png`（512×512）を読む
    /// 2. 1ドット=8px の等倍拡大であることを確かめ、64×64 に戻す（⚠️ 違ったら落とす）
    /// 3. 色を数える。<see cref="PixelSprite.MaxIndex"/> を超えていたら、使用画素数の少ない
    ///    色から近い色へまとめる（⭐ 何を何にまとめたかを必ず出力する）
    /// 4. `PixelSprite.Parse` に渡せる文字列配列と `new Palette(...)` を標準出力へ吐く
    ///
    /// ⚠️ **ここは「貼るものを作る」までが仕事。**Species.cs へ実際に貼るのは人（か Claude）
    /// ── `sim sheet code`（<see cref="Sheet.Run"/> の "code"）と同じ役割分担。
    /// ⭐ 外部の画像ライブラリは使わない（<see cref="SpritePng.DecodeRgba"/> が手書きの読み取り）。</summary>
    public static class SpriteImport
    {
        public const string SourceDir = "art/handmade/sprite";
        public const int SourceSize = 512;
        public const int DotPixels = 8;                       // ⭐ 1ドット=8px（実測・計画 §5）
        public const int GridSize = SourceSize / DotPixels;    // 64

        /// <summary>ファイル名順 → 割り当てる種族 id。⭐ 計画 §5・作者承認済み（2026-08-25）
        /// 「種族は指定しない。初めのほうから出る種族に充ててくれればいい」→ SpeciesTable の並び順。
        /// ⚠️ 5枚目以降が来たら、ここに1行足すだけでよい（無ければ「行き先が無い」と報告して止める）。</summary>
        public static readonly string[] AssignOrder = { "tamaru", "tsunoga", "haneru", "nobiru" };

        /// <summary>変異2枚ぶんの色相回転（度）。⚠️ 乱数は使わない（何度実行しても同じ結果）。
        /// ⭐ 値そのものに意味は無い（作者が後で手直しする前提・段取り3の指示）。</summary>
        private static readonly double[] MutantHueShiftDeg = { 140, 260 };

        /// <summary>これ未満の彩度は「無彩色」とみなし、変異パレットでは回さず null にする
        /// （既存の TamaruPalettes の「刃・目は回さない」慣習に倣う）。</summary>
        private const double GraySaturationThreshold = 0.08;

        public static void Run(string root)
        {
            var dir = Path.Combine(root, SourceDir);
            if (!Directory.Exists(dir))
            {
                Console.WriteLine($"■ 取り込み: {dir} が無い");
                return;
            }

            var files = Directory.GetFiles(dir, "*.png")
                .OrderBy(NumberedSuffix)
                .ThenBy(f => f, StringComparer.Ordinal)
                .ToList();

            Console.WriteLine();
            Console.WriteLine($"■ 手描きの原稿を取り込む（{files.Count} 枚・{dir}/）");

            for (int i = 0; i < files.Count; i++)
            {
                string file = files[i];
                string label = Path.GetFileName(file);
                if (i >= AssignOrder.Length)
                {
                    Console.WriteLine();
                    Console.WriteLine($"  ⚠️ {label}: 行き先が無い（AssignOrder に足りない。SpriteImport.cs の一覧に1行足すこと）");
                    continue;
                }
                string speciesId = AssignOrder[i];
                Console.WriteLine();
                Console.WriteLine($"── {label} → {speciesId} ──────────────────────");
                try
                {
                    ImportOne(file, speciesId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  🔴 落とした: {ex.Message}");
                }
            }
        }

        /// <summary>ファイル名の "(N)" を数値として拾う。無ければ 0（先頭に来る＝1枚目）。
        /// ⚠️ 普通の文字列ソートだと "sprite (1).png" が空白＜ピリオドの都合で
        /// 素の "sprite.png" より前に来てしまう（実測）── だからここで数値を見る。</summary>
        private static int NumberedSuffix(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            int open = name.LastIndexOf('(');
            if (open < 0) return 0;
            int close = name.IndexOf(')', open);
            if (close < 0) return 0;
            string digits = name.Substring(open + 1, close - open - 1);
            return int.TryParse(digits, out int n) ? n : int.MaxValue;
        }

        private static void ImportOne(string file, string speciesId)
        {
            var png = File.ReadAllBytes(file);
            SpritePng.DecodeRgba(png, out int w, out int h, out byte[] rgba);
            if (w != SourceSize || h != SourceSize)
                throw new InvalidOperationException($"{w}×{h}（{SourceSize}×{SourceSize} を期待）");

            // ── ① 1ドット=8px の等倍拡大であることを確かめつつ、64×64 へ戻す ──
            var dotRgba = new (byte R, byte G, byte B, byte A)[GridSize * GridSize];
            var badBlocks = new List<(int X, int Y, int Distinct)>();

            for (int gy = 0; gy < GridSize; gy++)
            {
                for (int gx = 0; gx < GridSize; gx++)
                {
                    var seen = new HashSet<(byte, byte, byte, byte)>();
                    var first = PixelAt(rgba, w, gx * DotPixels, gy * DotPixels);
                    for (int dy = 0; dy < DotPixels; dy++)
                        for (int dx = 0; dx < DotPixels; dx++)
                            seen.Add(PixelAt(rgba, w, gx * DotPixels + dx, gy * DotPixels + dy));

                    if (seen.Count > 1) badBlocks.Add((gx, gy, seen.Count));
                    dotRgba[gy * GridSize + gx] = first;
                }
            }

            if (badBlocks.Count > 0)
            {
                var sample = string.Join(", ", badBlocks.Take(5).Select(b => $"({b.X},{b.Y})に{b.Distinct}色"));
                throw new InvalidOperationException(
                    $"1ドット=8px の等倍拡大になっていない升目が {badBlocks.Count}/{GridSize * GridSize} 個（例: {sample}）"
                    + " ── 縮めずに落とす。原稿を8pxグリッドへ合わせ直してください");
            }

            // ── ② 色を数える（添字0＝透明は alpha=0。⚠️ 半端な alpha は不透明として扱い注意書きを出す）──
            int partialAlpha = 0;
            var firstSeenOrder = new List<(byte R, byte G, byte B)>();
            var pixelCount = new Dictionary<(byte, byte, byte), int>();
            var dotKey = new (byte R, byte G, byte B)?[GridSize * GridSize];

            for (int i = 0; i < dotRgba.Length; i++)
            {
                var px = dotRgba[i];
                if (px.A == 0) { dotKey[i] = null; continue; }
                if (px.A != 255) partialAlpha++;
                var key = (px.R, px.G, px.B);
                dotKey[i] = key;
                if (!pixelCount.ContainsKey(key)) { pixelCount[key] = 0; firstSeenOrder.Add(key); }
                pixelCount[key]++;
            }
            if (partialAlpha > 0)
                Console.WriteLine($"  ⚠️ 半端な alpha が {partialAlpha} 升（不透明として扱った）");

            int originalColors = pixelCount.Count;

            // ── ③ 35色を超えていたら、使用画素の少ない色から近い色へまとめる ──
            var remap = MergeToFit(pixelCount, firstSeenOrder, PixelSprite.MaxIndex,
                out var mergeLog);

            // 統合後の使用数を数え直す（remap を通した実測）
            var finalCount = new Dictionary<(byte, byte, byte), int>();
            var finalFirstSeen = new List<(byte, byte, byte)>();
            for (int i = 0; i < dotKey.Length; i++)
            {
                if (dotKey[i] == null) continue;
                var rep = remap[dotKey[i]!.Value];
                if (!finalCount.ContainsKey(rep)) { finalCount[rep] = 0; finalFirstSeen.Add(rep); }
                finalCount[rep]++;
            }

            // ⭐ 添字の割当ては「画素数が多い色ほど若い添字」。同数は先に出てきた順（決定的）
            var ordered = finalFirstSeen
                .OrderByDescending(c => finalCount[c])
                .ThenBy(c => finalFirstSeen.IndexOf(c))
                .ToList();

            if (ordered.Count > PixelSprite.MaxIndex)
                throw new InvalidOperationException(
                    $"統合しても {ordered.Count} 色（上限 {PixelSprite.MaxIndex}）── MergeToFit の実装を見直すこと");

            var indexOf = new Dictionary<(byte, byte, byte), int>();
            for (int i = 0; i < ordered.Count; i++) indexOf[ordered[i]] = i + 1;   // 1始まり

            Console.WriteLine($"  色数: {originalColors} → {ordered.Count}"
                + (mergeLog.Count == 0 ? "（統合なし）" : $"（{mergeLog.Count} 件統合）"));
            foreach (var line in mergeLog) Console.WriteLine("    " + line);

            // ── ④ 貼れる C# を吐く ──
            string big = ToUpperCamel(speciesId);
            var sb = new StringBuilder();

            sb.Append($"// ── {speciesId} ── 取り込み元: {Path.GetFileName(file)}"
                + $"（{originalColors}色 → {ordered.Count}色。SpriteImport が自動生成・貼り付け前提）\n");
            sb.Append($"private static readonly PixelSprite {big}Sprite = PixelSprite.Parse(new[]\n{{\n");
            for (int gy = 0; gy < GridSize; gy++)
            {
                sb.Append("    \"");
                for (int gx = 0; gx < GridSize; gx++)
                {
                    var key = dotKey[gy * GridSize + gx];
                    if (key == null) { sb.Append('.'); continue; }
                    var rep = remap[key.Value];
                    sb.Append(PixelSprite.CharOf((byte)indexOf[rep]));
                }
                sb.Append("\",\n");
            }
            sb.Append("});\n\n");

            // 通常パレット
            var normalHex = ordered.Select(HexOf).ToList();
            sb.Append($"private static readonly Palette[] {big}Palettes =\n{{\n");
            sb.Append("    new Palette(").Append(string.Join(", ", normalHex.Select(Quote))).Append("), // 通常\n");

            // 変異2枚（色相回転・無彩色は null）
            foreach (var shift in MutantHueShiftDeg)
            {
                var cells = new List<string>();
                foreach (var c in ordered)
                {
                    var (h2, s2, l2) = RgbToHsl(c);
                    if (s2 < GraySaturationThreshold) { cells.Add("null"); continue; }
                    var rotated = HslToRgb((h2 + shift) % 360.0, s2, l2);
                    cells.Add(Quote(HexOf(rotated)));
                }
                sb.Append("    new Palette(").Append(string.Join(", ", cells))
                  .Append($"), // 変異（色相 +{shift:0}°）\n");
            }
            sb.Append("};\n");

            Console.WriteLine();
            Console.WriteLine(sb.ToString());
        }

        private static (byte R, byte G, byte B, byte A) PixelAt(byte[] rgba, int width, int x, int y)
        {
            int i = (y * width + x) * 4;
            return (rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]);
        }

        private static string HexOf((byte R, byte G, byte B) c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";
        private static string Quote(string s) => "\"" + s + "\"";

        private static string ToUpperCamel(string id)
        {
            var sb = new StringBuilder();
            bool up = true;
            foreach (char c in id)
            {
                if (c == '-') { up = true; continue; }
                sb.Append(up ? char.ToUpperInvariant(c) : c);
                up = false;
            }
            return sb.ToString();
        }

        /// <summary>使用画素の少ない色から、まだ残っている中で一番近い色へ統合する
        /// （ユークリッド距離・RGB）。⭐ 決定的（乱数を使わない・同点は登場順で決める）。
        /// ⚠️ 連鎖統合に対応 ── 途中で吸収された色をさらに引き継ぐ先へ、必ず解決する。</summary>
        private static Dictionary<(byte, byte, byte), (byte, byte, byte)> MergeToFit(
            Dictionary<(byte, byte, byte), int> counts,
            List<(byte, byte, byte)> firstSeenOrder,
            int maxColors,
            out List<string> log)
        {
            log = new List<string>();
            // 元の色 → いま指している代表色（最初は自分自身）
            var remap = firstSeenOrder.ToDictionary(c => c, c => c);
            // 生きている代表色の使用数（統合するたびに合算）
            var alive = new Dictionary<(byte, byte, byte), int>(counts);
            var order = new List<(byte, byte, byte)>(firstSeenOrder);   // 生きている代表色。決定的な走査順を保つ

            while (order.Count > maxColors)
            {
                // 使用数最小（同点は登場順で先のもの）を選ぶ
                (byte, byte, byte) least = order[0];
                int leastCount = alive[least];
                foreach (var c in order)
                {
                    if (alive[c] < leastCount) { least = c; leastCount = alive[c]; }
                }

                // 残りの中で一番近い色を選ぶ（自分以外・同点は登場順）
                (byte, byte, byte) nearest = default;
                double bestDist = double.MaxValue;
                bool found = false;
                foreach (var c in order)
                {
                    if (c.Equals(least)) continue;
                    double dist = Dist2(least, c);
                    if (dist < bestDist) { bestDist = dist; nearest = c; found = true; }
                }
                if (!found) throw new InvalidOperationException("統合先が見つからない（色が1つしかない）");

                log.Add($"色 {HexOf(least)}（{leastCount}画素）→ {HexOf(nearest)} へ統合");

                // 元の色から見た解決先を、統合された色すべてについて付け替える
                int mergedCount = alive[least];
                foreach (var key in firstSeenOrder)
                {
                    if (remap[key].Equals(least)) remap[key] = nearest;
                }
                alive[nearest] += mergedCount;
                alive.Remove(least);
                order.Remove(least);
            }

            return remap;
        }

        private static double Dist2((byte, byte, byte) a, (byte, byte, byte) b)
        {
            double dr = a.Item1 - b.Item1, dg = a.Item2 - b.Item2, db = a.Item3 - b.Item3;
            return dr * dr + dg * dg + db * db;
        }

        // ── RGB ⇔ HSL（変異パレットの色相回転のためだけに使う・簡易実装） ──────

        private static (double H, double S, double L) RgbToHsl((byte R, byte G, byte B) c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            double l = (max + min) / 2.0;
            if (max == min) return (0.0, 0.0, l);   // 無彩色（彩度0）

            double d = max - min;
            double s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            double h;
            if (max == r) h = (g - b) / d + (g < b ? 6.0 : 0.0);
            else if (max == g) h = (b - r) / d + 2.0;
            else h = (r - g) / d + 4.0;
            h *= 60.0;
            return (h, s, l);
        }

        private static (byte R, byte G, byte B) HslToRgb(double h, double s, double l)
        {
            if (s <= 0.0)
            {
                byte v = (byte)Math.Round(l * 255.0);
                return (v, v, v);
            }
            double hh = ((h % 360.0) + 360.0) % 360.0 / 360.0;
            double q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
            double p = 2.0 * l - q;
            double r = HueToRgb(p, q, hh + 1.0 / 3.0);
            double g = HueToRgb(p, q, hh);
            double b = HueToRgb(p, q, hh - 1.0 / 3.0);
            return ((byte)Math.Round(r * 255.0), (byte)Math.Round(g * 255.0), (byte)Math.Round(b * 255.0));
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0.0) t += 1.0;
            if (t > 1.0) t -= 1.0;
            if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
            return p;
        }
    }
}
