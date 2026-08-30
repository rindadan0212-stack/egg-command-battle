#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace EggCommand.Sim
{
    /// <summary>`icon` の実寸目録（`assets/ui/icon/icon-manifest.txt`）を作り直す。
    ///
    /// ⭐ **`PaintPlaceholder` の一覧書き出しを手本にした造り**（ドット絵化計画 段取り4・
    /// 「1ドット=4px」統一。2026-08-29）。⚠️ 違うのは大きさの決め方だけ ──
    /// `paint`/`sprite` の PNG は「1 ファイル画素 = 1 ドット」（実寸そのまま拡大しない）で
    /// 焼かれているので IHDR をそのまま読めばよいが、`icon` の PNG は**そうなっていない**
    /// （下の <see cref="DotsOf"/> 参照）。
    ///
    /// 使い方: `dotnet run --project EggCommand.Sim -- icon-manifest`</summary>
    public static class IconManifestTool
    {
        public const string Dir = "assets/ui/icon";
        public const string ManifestFile = "icon-manifest.txt";

        public static void Run(string root)
        {
            var dir = Path.Combine(root, Dir);
            var lines = ComputeManifestLines(dir);
            File.WriteAllText(Path.Combine(dir, ManifestFile),
                string.Join("\n", lines) + (lines.Count > 0 ? "\n" : ""), new UTF8Encoding(false));
            Console.WriteLine($"■ icon の目録を書いた: {Dir}/{ManifestFile}（{lines.Count}枚）");
        }

        /// <summary>⭐ **書き込みをしない側**（`Run` と、目録の鮮度を見る検査
        /// `EggCommand.Tests` の両方がここを呼ぶ ── 判断を2か所に書かない）。</summary>
        public static List<string> ComputeManifestLines(string dir)
        {
            var lines = new List<string>();
            foreach (var path in Directory.GetFiles(dir, "*.png").OrderBy(p => p, StringComparer.Ordinal))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                var bytes = File.ReadAllBytes(path);
                PaintPlaceholder.ReadPngSize(bytes, out int rawW, out int rawH);
                var (w, h) = DotsOf(name, rawW, rawH);
                lines.Add($"{name} {w} {h}");
            }
            return lines;
        }

        /// <summary>1枚の icon PNG の「実ドット数」を決める。
        ///
        /// 🔴 **`status-` は全部16×16固定。** 2026-08-30から実PNGも16×16だが、
        /// この家族は `unit.txt` の同じ枠へ並ぶため、将来32px等の原画へ差し替えても
        /// 論理ドット数を勝手に変えない。
        ///
        /// ⚠️ それ以外（Kenney 風の小物絵）は実測していないので、**旧来の決め打ち
        /// （`LayoutDom.IconDots` = 32、= 実寸128px ÷ 4）をそのまま踏襲**する
        /// （÷4 の安全側 ── 割り切れない大きさが来ても 1 未満にはしない）。
        /// 触れていない骨組み（`square.txt`/`trail.txt` など）の見え方を変えないため。</summary>
        internal static (int Width, int Height) DotsOf(string name, int rawWidth, int rawHeight)
        {
            if (name.StartsWith("status-", StringComparison.Ordinal)) return (16, 16);
            int w = Math.Max(1, (int)Math.Round(rawWidth / 4.0));
            int h = Math.Max(1, (int)Math.Round(rawHeight / 4.0));
            return (w, h);
        }
    }
}
