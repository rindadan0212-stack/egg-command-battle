#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EggCommand.Core;

namespace EggCommand.Sim
{
    /// <summary>まだ描いていない `paint` の絵を、仮置きとして作る。
    ///
    /// ⭐ 作者の指示「まだ用意できていないものはすべてコードで仮置きする」
    /// （ドット絵化計画.md 決定9）への対応。⚠️ **本物と見間違えない見た目**にする
    /// （枠線1ドット＋薄い塗り＋隅の印）。ただし画面の見え方を判断できる程度には
    /// 整えるので、斜線で埋め尽くしはしない（段取り4・第3部）。
    ///
    /// ⚠️ **同じ名前の本物が既に在るなら上書きしない。**
    /// ⭐ 走らせるたびに `paint-manifest.txt`（名前・実ドット数）も書き直す ──
    /// これは仮置きだけでなく、いつか作者が描いた本物にも効く（実物の PNG を読むだけなので）。</summary>
    public static class PaintPlaceholder
    {
        public const string Dir = "assets/ui/paint";
        public const string LayoutsDir = "assets/layouts";
        public const string ManifestFile = "paint-manifest.txt";

        // ⚠️ 本物と紛れない、はっきりした配色（薄い塗り／枠線／隅の印）。
        private static readonly Palette PlaceholderPalette = new Palette(
            "#e4e1f2", "#9089b3", "#ff5da2");

        public static void Run(string root)
        {
            var layoutsDir = Path.Combine(root, LayoutsDir);
            var outDir = Path.Combine(root, Dir);
            Directory.CreateDirectory(outDir);

            var found = ScanPaintNames(layoutsDir);

            int made = 0;
            var madeList = new List<string>();
            var skipped = new List<string>();
            foreach (var pair in found.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                string name = pair.Key;
                int w = pair.Value.Item1, h = pair.Value.Item2;
                var path = Path.Combine(outDir, name + ".png");
                if (File.Exists(path)) { skipped.Add(name); continue; }

                var sprite = BuildPlaceholder(w, h);
                File.WriteAllBytes(path, SpritePng.Encode(sprite, PlaceholderPalette));
                made++;
                madeList.Add($"{name} {w}x{h}");
            }

            WriteManifest(outDir);
            int icons = FillArtTable(root);

            Console.WriteLine();
            if (icons > 0) Console.WriteLine($"■ 仮置きの絵札（`Art` の表）を作った: {icons} 枚");
            Console.WriteLine($"■ 仮置きの paint を作った: {made} 枚 → {Dir}/");
            foreach (var line in madeList) Console.WriteLine("  " + line);
            if (skipped.Count > 0)
                Console.WriteLine($"  ⚠️ 既に本物がある（上書きしない）: {string.Join(", ", skipped)}");
            if (found.Count == 0)
                Console.WriteLine("  ⚠️ 骨組みに paint の pic= が1つも見つからなかった");
            Console.WriteLine($"  一覧を書いた: {Dir}/{ManifestFile}");
        }

        /// <summary>骨組み（`Layouts/*.txt`）を全部読み（`use=` も差し替えたうえで）、
        /// `paint` の `pic=` が指す名前と、その節点の枠の大きさ（÷4 でドット数）を集める。
        /// ⚠️ 同じ名前が複数箇所にあれば、最初に見つかった大きさを使う
        /// （ファイル名・木の並び順で決定的）。</summary>
        private static Dictionary<string, (int, int)> ScanPaintNames(string layoutsDir)
        {
            var result = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
            if (!Directory.Exists(layoutsDir)) return result;

            var files = Directory.GetFiles(layoutsDir, "*.txt")
                .OrderBy(p => p, StringComparer.Ordinal).ToList();

            var raws = new Dictionary<string, Layout>(StringComparer.Ordinal);
            foreach (var path in files)
            {
                string id = Path.GetFileNameWithoutExtension(path);
                raws[id] = Layouts.Parse(id, File.ReadAllText(path));
            }

            foreach (var path in files)
            {
                string id = Path.GetFileNameWithoutExtension(path);
                var resolved = Layouts.Resolve(raws[id],
                    name => raws.TryGetValue(name, out var l) ? l : null);
                foreach (var node in resolved.Roots) Walk(node, result);
            }
            return result;
        }

        private static void Walk(LayoutNode node, Dictionary<string, (int, int)> result)
        {
            if (node.Kind == "paint")
            {
                string? pic = node.Option("pic");
                if (pic != null && !result.ContainsKey(pic))
                {
                    int w = Math.Max(1, (int)Math.Round(node.Width / 4f));
                    int h = Math.Max(1, (int)Math.Round(node.Height / 4f));
                    result[pic] = (w, h);
                }
            }
            foreach (var child in node.Children) Walk(child, result);
        }

        /// <summary>枠線1ドット＋薄い塗り＋隅の小さな印。⚠️ 斜線で埋め尽くさない
        /// （画面の見え方を判断できる程度には整える・段取り4・第3部）。</summary>
        /// <summary>⭐ **`Art` の表が指していて、まだ無い絵を仮置きで埋める**（2026-08-27）。
        ///
        /// ⚠️ この道具は `paint/` しか見ていなかったので、状態異常の札（`icon/`）を足した日に
        /// `ArtTests.表が指す絵は全部ある` が落ち、**手で PNG を置くしか無かった**。
        /// ⭐ 表を出所にして埋めれば、絵札を足しても走らせるだけで済む。
        /// ⚠️ **既にあるものは上書きしない**（この道具の約束）。</summary>
        private static int FillArtTable(string root)
        {
            const int Side = 32;      // ⚠️ 札は 128px 四方だが、ドットは 32 で足りる（×4 で出す）
            int made = 0;
            foreach (var art in Art.All())
            {
                var dir = Path.Combine(root, "assets/ui", art.Folder);
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, art.Name + ".png");
                if (File.Exists(path)) continue;
                File.WriteAllBytes(path,
                    SpritePng.Encode(BuildPlaceholder(Side, Side), PlaceholderPalette));
                made++;
            }
            return made;
        }

        private static PixelSprite BuildPlaceholder(int w, int h)
        {
            w = Math.Max(1, w);
            h = Math.Max(1, h);
            int markSize = Math.Max(0, Math.Min(3, Math.Min(w - 2, h - 2)));

            var rows = new string[h];
            for (int y = 0; y < h; y++)
            {
                var line = new char[w];
                for (int x = 0; x < w; x++)
                {
                    bool border = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                    bool mark = markSize > 0 && x >= 1 && x < 1 + markSize && y >= 1 && y < 1 + markSize;
                    line[x] = border ? '2' : (mark ? '3' : '1');
                }
                rows[y] = new string(line);
            }
            return PixelSprite.Parse(rows);
        }

        /// <summary>paint フォルダの実物を読み直して、大きさ入りの一覧を書く。
        /// ⚠️ **実物の PNG（IHDR）から読む**── 仮置きだけでなく、いつか作者が描いた
        /// 本物に差し替わったときも、このコマンドを走らせ直すだけで一覧が追随する。</summary>
        /// <summary>⭐ 一覧を書き直すだけの入口（`EggSkinPng` が焼いたあとに呼ぶ）。
        /// ⚠️ 仮置きを作らずに一覧だけ直したい場面があるので、外から呼べるようにしてある。</summary>
        public static void WriteManifestFor(string outDir) => WriteManifest(outDir);

        private static void WriteManifest(string outDir)
        {
            var lines = ComputeManifestLines(outDir);
            File.WriteAllText(Path.Combine(outDir, ManifestFile),
                string.Join("\n", lines) + (lines.Count > 0 ? "\n" : ""), new UTF8Encoding(false));
        }

        /// <summary>⭐ **書き込みをしない側**（`WriteManifest` と、目録の鮮度を見る検査
        /// `EggCommand.Tests` の両方がここを呼ぶ ── 判断を2か所に書かない）。
        /// フォルダの実物 PNG から「名前 幅 高」の行を組み立てるだけで、ファイルへは書かない。</summary>
        public static List<string> ComputeManifestLines(string dir)
        {
            var lines = new List<string>();
            foreach (var path in Directory.GetFiles(dir, "*.png").OrderBy(p => p, StringComparer.Ordinal))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                var bytes = File.ReadAllBytes(path);
                ReadPngSize(bytes, out int w, out int h);
                lines.Add($"{name} {w} {h}");
            }
            return lines;
        }

        /// <summary>PNG の IHDR から幅・高さだけを読む。⚠️ 色の型（インデックス／RGBA）を
        /// 問わない ── 仮置き（インデックスカラー）も、いつか作者が描く本物（RGBA かもしれない）も、
        /// 同じ数え方で大きさが読める。
        /// ⭐ `internal`（`private` から変更）── `IconManifestTool` も同じ読み方を要るので、
        /// IHDR パースを2か所に書かない（同じ EggCommand.Sim アセンブリ内）。</summary>
        internal static void ReadPngSize(byte[] png, out int width, out int height)
        {
            if (png.Length < 24) throw new ArgumentException("PNG が短すぎる（IHDR が読めない）");
            width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
            height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        }
    }
}
