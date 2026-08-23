using System;
using System.Collections.Generic;
using System.IO;

namespace EggCommand.Web
{
    /// <summary>「`Resources/UI/icon/` に実際に在る絵」の一覧。
    ///
    /// ⭐ **埋め込む**（csproj の `IconManifest` ターゲットが起動時に1回作る）。
    /// ⚠️ HTTP で取りに行かない ── `LayoutStore` と同じ理由（dev サーバが
    /// 200 を返しながら 0 バイトを返す形があった）。
    ///
    /// 🔴 これが要る理由: `LayoutDom` の `icon` は `pic=` の名前をそのまま
    /// ファイル名へ組み立てて出す（`url(icon/&lt;名前&gt;.png)`）。
    /// ファイルが実在しなくても **CSS はエラーを出さない**（ただの空白になる）
    /// ── 骨組みや `Art` の表が指す名前を打ち間違えても、画面には何も出ず、
    /// 気づけない。⭐ ここで実在を確かめて、無ければ見える印を出す
    /// （`LayoutDom` の `icon-missing`）。</summary>
    public static class IconManifest
    {
        private static HashSet<string>? _known;
        /// <summary>一覧そのものが読めたか。⚠️ **読めなかったときは「無い」を疑わない**
        /// （一覧が壊れているだけなのに、全部の絵を missing 扱いにする事故を避ける）。
        /// ⭐ 一覧の壊れは別のテスト（`ArtTests`／ビルドの手順）が見る。</summary>
        private static bool _loaded;

        public static bool Exists(string name)
        {
            if (_known == null) _known = Load();
            return !_loaded || _known.Contains(name);
        }

        private static HashSet<string> Load()
        {
            var set = new HashSet<string>(System.StringComparer.Ordinal);
            var asm = typeof(IconManifest).Assembly;
            string path = asm.GetName().Name + ".IconManifest.txt";
            using var stream = asm.GetManifestResourceStream(path);
            if (stream == null)
            {
                // ⚠️ 埋め込み手順（csproj の `IconManifest` ターゲット）が動いていない。
                //    ⭐ ブラウザの console に残す ── 気づかずにいるほうが困る。
                Console.Error.WriteLine("IconManifest: 埋め込みが見つからない（csproj を見る）");
                return set;
            }
            _loaded = true;
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = reader.ReadLine()) != null)
                if (line.Length > 0) set.Add(line);
            return set;
        }
    }
}
