using System;
using System.Collections.Generic;

namespace EggCommand.Web
{
    /// <summary>「`Resources/UI/icon/` に実際に在る絵」の一覧と、その大きさ（ドット数）。
    ///
    /// ⭐ **`PaintManifest` と同じ形**（2026-08-29・ドット絵化計画 段取り4「1ドット=4px」統一）。
    /// 以前は名前だけの一覧を csproj の `IconManifest` ターゲットが**ディレクトリを見て毎回
    /// 作り直して**いたが、大きさを持てなかった（`LayoutDom.IconDots` が全アイコン共通の
    /// 決め打ちだった穴）。⭐ 唯一の出所は `assets/ui/icon/icon-manifest.txt`
    /// （`EggCommand.Sim` の `icon-manifest` コマンドが実物の PNG を読んで書く）。
    /// ⚠️ HTTP で取りに行かない（`PaintManifest` と同じ理由）。</summary>
    public static class IconManifest
    {
        private static Dictionary<string, PaintManifest.Size>? _known;
        /// <summary>一覧そのものが読めたか。⚠️ **読めなかったときは「無い」を疑わない**
        /// （一覧が壊れているだけなのに、全部の絵を missing 扱いにする事故を避ける）。
        /// ⭐ 一覧の壊れは別のテスト（`ArtTests`／ビルドの手順）が見る。</summary>
        private static bool _loaded;

        public static bool Exists(string name)
        {
            if (_known == null) _known = Load();
            return !_loaded || _known.ContainsKey(name);
        }

        /// <summary>一覧そのものが読めているか。⚠️ `PaintManifest.Loaded` と同じ理由 ──
        /// <see cref="SizeOf"/> はこれを見ないので、呼び側（`LayoutDom`）が
        /// 「読めていない」と「読めたが名前が無い」を区別するのに使う。</summary>
        public static bool Loaded
        {
            get { if (_known == null) _known = Load(); return _loaded; }
        }

        /// <summary>その絵の大きさ（ドット数）。⚠️ 無ければ null（呼び側が missing 扱いにする）。</summary>
        public static PaintManifest.Size? SizeOf(string name)
        {
            if (_known == null) _known = Load();
            return _known.TryGetValue(name, out var size) ? (PaintManifest.Size?)size : null;
        }

        /// <summary>⭐ 段E: 「絵を選ぶ」小窓のための一覧そのもの。⚠️ <see cref="Exists"/> と
        /// 同じ埋め込みから作る（出所は1つ）。⚠️ 読めなかったとき（<see cref="Loaded"/> が
        /// false）は空を返す ── `Exists` と違い「読めなかったら全部 OK 扱いにする」は
        /// できない（一覧は実物の名前しか出せない）が、実害は「選べる絵が0件になる」
        /// だけ（`icon-missing` を誤検出するわけではない）。</summary>
        public static IReadOnlyCollection<string> Names
        {
            get
            {
                if (_known == null) _known = Load();
                return _known.Keys;
            }
        }

        private static Dictionary<string, PaintManifest.Size> Load() =>
            NameSizeManifestIo.Load("IconManifest.txt", "csproj か sim icon-manifest", out _loaded);
    }
}
