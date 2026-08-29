#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EggCommand.Sim
{
    /// <summary>チェックイン済みの目録・PNG を、埋め込みリソース越しに読む。
    ///
    /// ⭐ **`EggCommand.Tests`（目録の鮮度を見る検査）専用の入口。**⚠️ `assets/ui/paint/*.png`
    /// は `EggCommand.Tests.csproj` のコピー設定に無く、今回の作業範囲は
    /// `EggCommand.Tests.csproj` を含まない ── そこで、ここ（`EggCommand.Sim`、作業範囲内）に
    /// チェックイン済みの実物を埋め込み、`EggCommand.Tests` は既存の `ProjectReference`
    /// （`SeriesRecord.cs` などと同じ経路）越しに読む。`icon` は既存のコピー設定
    /// （`icon\*.png`）で実物へ届くので、こちらは目録の字だけ埋め込む。
    /// ⚠️ 埋め込みは `EggCommand.Sim` の**ビルドごとに実物から作り直される**ので、
    /// 「チェックイン済みの中身」を古いまま固定してしまう心配はない。</summary>
    public static class CheckedInAssets
    {
        public static List<string> IconManifestLines() => ReadLines("icon-manifest.txt");
        public static List<string> PaintManifestLines() => ReadLines("paint-manifest.txt");

        /// <summary>⭐ `assets/ui/paint/*.png` を**その場で読み直した**のと同じ形の行
        /// （`PaintPlaceholder.ComputeManifestLines` と同じ「名前 幅 高」・同じ並び順）を、
        /// 埋め込んだ実物のバイトから組み立てる。⚠️ ディスクの `assets/ui/paint/` に
        /// 直接アクセスできない `EggCommand.Tests` の代わりに、ここで計算まで済ませる。</summary>
        public static List<string> FreshPaintManifestLines()
        {
            var asm = typeof(CheckedInAssets).Assembly;
            string prefix = asm.GetName().Name + ".CheckedIn.paint_png.";
            var names = asm.GetManifestResourceNames()
                .Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".png", StringComparison.Ordinal))
                .OrderBy(n => n, StringComparer.Ordinal);

            var lines = new List<string>();
            foreach (var res in names)
            {
                string name = res.Substring(prefix.Length, res.Length - prefix.Length - ".png".Length);
                using var stream = asm.GetManifestResourceStream(res)!;
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                PaintPlaceholder.ReadPngSize(ms.ToArray(), out int w, out int h);
                lines.Add($"{name} {w} {h}");
            }
            return lines;
        }

        private static List<string> ReadLines(string name)
        {
            var asm = typeof(CheckedInAssets).Assembly;
            string path = asm.GetName().Name + ".CheckedIn." + name;
            using var stream = asm.GetManifestResourceStream(path);
            if (stream == null)
                throw new InvalidOperationException(
                    $"{name} の埋め込みが見つからない（EggCommand.Sim.csproj を見る）");
            using var reader = new StreamReader(stream);
            var lines = new List<string>();
            string? line;
            while ((line = reader.ReadLine()) != null)
                if (line.Length > 0) lines.Add(line);
            return lines;
        }
    }
}
