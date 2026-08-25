using System;
using System.Collections.Generic;
using System.IO;

namespace EggCommand.Web
{
    /// <summary>「`Resources/UI/paint/` に実際に在る絵」の一覧と、その大きさ（ドット数）。
    ///
    /// ⭐ **`IconManifest` を手本にした作り。**⚠️ 違うのは「名前だけでなく大きさも持つ」こと
    /// ── `LayoutDom` の「引き伸ばさない」規則（ドット絵化計画 段取り4・第1部）は、
    /// 節点の外から「この絵は何ドット四方か」を知る必要があるため。
    ///
    /// ⚠️ **唯一の出所は `unity/Assets/Resources/UI/paint/paint-manifest.txt`**
    /// （`EggCommand.Sim` の仮置きコマンド `paint-placeholder` が、実物の PNG を
    /// 読んで書く。手描きの本物に差し替えても、同じコマンドを走らせれば更新される）。
    /// ⚠️ HTTP で取りに行かない（`IconManifest` と同じ理由）。</summary>
    public static class PaintManifest
    {
        /// <summary>絵の大きさ（ドット数）。</summary>
        public readonly struct Size
        {
            public readonly int Width;
            public readonly int Height;
            public Size(int width, int height) { Width = width; Height = height; }
        }

        private static Dictionary<string, Size>? _known;
        private static bool _loaded;

        public static bool Exists(string name)
        {
            if (_known == null) _known = Load();
            return !_loaded || _known.ContainsKey(name);
        }

        /// <summary>その絵の大きさ（ドット数）。⚠️ 無ければ null（呼び側が missing 扱いにする）。</summary>
        public static Size? SizeOf(string name)
        {
            if (_known == null) _known = Load();
            return _known.TryGetValue(name, out var size) ? (Size?)size : null;
        }

        /// <summary>⭐ E1-4: 「絵を選ぶ」小窓の `paint` 一覧のための出所。⚠️ `IconManifest.Names`
        /// と同じ形（読めなかったときは空を返す ── 一覧が壊れているだけで全部 missing 扱いに
        /// しない、という <see cref="Exists"/> の「読めなかったら通す」とは違う既定に見える
        /// が、実害は同じ「絵を選ぶ小窓に0件しか出ない」だけ ── `IconManifest.Names` の
        /// コメントと同じ理由）。</summary>
        public static IReadOnlyCollection<string> Names
        {
            get
            {
                if (_known == null) _known = Load();
                return _known.Keys;
            }
        }

        private static Dictionary<string, Size> Load()
        {
            var map = new Dictionary<string, Size>(StringComparer.Ordinal);
            var asm = typeof(PaintManifest).Assembly;
            string path = asm.GetName().Name + ".PaintManifest.txt";
            using var stream = asm.GetManifestResourceStream(path);
            if (stream == null)
            {
                // ⚠️ 埋め込み手順（csproj の `PaintManifest` ターゲット）が動いていない、
                //    または `paint-placeholder` を一度も走らせていない。
                Console.Error.WriteLine("PaintManifest: 埋め込みが見つからない（csproj か sim paint-placeholder を見る）");
                return map;
            }
            _loaded = true;
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0) continue;
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3) continue;
                if (int.TryParse(parts[1], out var w) && int.TryParse(parts[2], out var h))
                    map[parts[0]] = new Size(w, h);
            }
            return map;
        }
    }
}
