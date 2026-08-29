using System;
using System.Collections.Generic;
using System.IO;

namespace EggCommand.Web
{
    /// <summary>「`Resources/UI/paint/` に実際に在る絵」の一覧と、その大きさ（ドット数）。
    ///
    /// ⭐ **`IconManifest` を手本にした作り。**⚠️ 違うのは「名前だけでなく大きさも持つ」こと
    /// ── `LayoutDom` の「引き伸ばさない」規則（⭐ 2026-08-29 以降は
    /// `paint`/`icon` だけの規則 ── `pixel` は枠に合わせて整数倍で伸びる）は、
    /// 節点の外から「この絵は何ドット四方か」を知る必要があるため。
    ///
    /// ⚠️ **唯一の出所は `assets/ui/paint/paint-manifest.txt`**
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

        /// <summary>一覧そのものが読めているか。⚠️ 埋め込みが無い新規クローンでは false
        /// （`sim paint-placeholder` を一度も走らせていない・csproj の Condition が外している）。
        /// 🔴 <see cref="SizeOf"/> はこれを見ないので、読めていないとき**全部の `paint` を
        /// 「絵が無い」と誤診していた**（`Exists` は `!_loaded` で安全側に倒すのに、
        /// `SizeOf` は倒さない ── 2026-08-25 監査で発覚）。呼び側（<see cref="LayoutDom"/>）
        /// はこれを見て、読めていないときは「missing」でなく普通の `&lt;img&gt;` を出す。</summary>
        public static bool Loaded
        {
            get { if (_known == null) _known = Load(); return _loaded; }
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

        private static Dictionary<string, Size> Load() =>
            NameSizeManifestIo.Load("PaintManifest.txt", "csproj か sim paint-placeholder", out _loaded);
    }

    /// <summary>⭐ **「名前 幅 高」1行形式の埋め込み一覧を読む、唯一の出所。**
    ///
    /// ⚠️ `PaintManifest`/`IconManifest` は形（読み方・`Exists`/`SizeOf`/`Loaded` の意味）が
    /// 完全に同じなので、読み込みだけをここへまとめた ── 中身（列挙する対象・埋め込みの
    /// 作り方）は各クラス側に残す（薄い包みにする程度・丸ごとの作り直しはしない）。
    /// `SpriteManifest` は「名前だけ」＋参照ベースの引き方で形が違うので、まとめない
    /// （3つ目まで無理に揃えると壊す危険のほうが大きいと判断した）。</summary>
    internal static class NameSizeManifestIo
    {
        /// <param name="resourceName">埋め込みのリンク名（例 "PaintManifest.txt"）。</param>
        /// <param name="howToMake">埋め込みが見つからないときの console 案内に添える、
        /// 「何を走らせれば直るか」の一言。</param>
        /// <param name="loaded">⭐ 呼び出し側の `_loaded` フィールドへそのまま渡す
        /// （埋め込みが読めたか。読めなかったときは呼び側が「無いと疑わない」側へ倒す）。</param>
        public static Dictionary<string, PaintManifest.Size> Load(
            string resourceName, string howToMake, out bool loaded)
        {
            var map = new Dictionary<string, PaintManifest.Size>(StringComparer.Ordinal);
            var asm = typeof(NameSizeManifestIo).Assembly;
            string path = asm.GetName().Name + "." + resourceName;
            using var stream = asm.GetManifestResourceStream(path);
            if (stream == null)
            {
                Console.WriteLine($"{resourceName}: 埋め込みが見つからない（{howToMake} を見る）");
                loaded = false;
                return map;
            }
            loaded = true;
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0) continue;
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3) continue;
                if (int.TryParse(parts[1], out var w) && int.TryParse(parts[2], out var h))
                    map[parts[0]] = new PaintManifest.Size(w, h);
            }
            return map;
        }
    }
}
