using System;
using System.Collections.Generic;
using System.IO;
using EggCommand.Core;

namespace EggCommand.Web
{
    /// <summary>種族の絵を「あらかじめ焼いた PNG」のファイル名に変える。
    ///
    /// ⭐ **なぜ `IconManifest` と分けたか**: `icon` は「骨組みが名前（文字列）で指す」絵。
    /// こちらは「`PixelSprite` / `Palette` の**オブジェクト**で指す」絵 ── 骨組みは
    /// 種族もパレット添字も知らない（`fill.Sprite` / `fill.Palette` が返す実体だけを見る）。
    /// ⭐ だから引き方が違う: 名前の一致ではなく、**参照の一致**で引く。
    ///
    /// ⚠️ **参照で引ける理由**: 種族表（<see cref="SpeciesTable"/>）は起動時に1度だけ
    /// 組まれる static な表で、`species.Sprite` / `species.Palettes[i]` は種族ごとに
    /// ただ1個しか無い。同じ種族を指す限り、誰から辿っても同じオブジェクトに行き着く
    /// （`Creatures.PaletteOf` も `species.Palettes[creature.PaletteIndex]` を返すだけ）。
    ///
    /// ⚠️ **卵（`EggArt`）はここに載らない。**種族表の外にあるので、参照が引っかからない
    /// ── そのときは呼び側が今までどおり SVG で描く（`LayoutDom.Dots`）。</summary>
    public static class SpriteManifest
    {
        /// <summary>Palette の参照 → ファイル名の芯（拡張子・フォルダなし。例 "tamaru-0"）。
        /// ⚠️ Palette だけで種族が一意に決まる ── 種族ごとに別のオブジェクトを持つので
        /// （<c>TamaruPalettes</c> と <c>KibanePalettes</c> は別の配列）、Sprite まで
        /// 見なくても引ける。</summary>
        private static readonly Dictionary<Palette, string> StemOfPalette = BuildStems();

        private static Dictionary<Palette, string> BuildStems()
        {
            var map = new Dictionary<Palette, string>();
            foreach (var species in SpeciesTable.All)
            {
                for (int p = 0; p < species.Palettes.Count; p++)
                    map[species.Palettes[p]] = species.Id + "-" + p;
            }
            return map;
        }

        /// <summary>この (絵, 色) が種族表の絵なら、ファイル名の芯を返す。
        /// ⚠️ 種族表に無ければ null（卵など ── 呼び側は SVG に戻す）。</summary>
        public static string? StemOf(PixelSprite sprite, Palette palette)
        {
            if (palette == null || !StemOfPalette.TryGetValue(palette, out var stem)) return null;
            // ⚠️ **念のための整合性チェック**。パレットは種族を一意に決めるが、
            //    渡された sprite が別物なら呼び側の組み合わせが壊れている ── 焼いていない
            //    絵を指してしまうより、安全側の SVG へ倒す（`Exists` と同じ「黙って進まない」）。
            var stemId = stem.Substring(0, stem.LastIndexOf('-'));
            if (!ReferenceEquals(sprite, SpeciesTable.ById(stemId).Sprite)) return null;
            return stem;
        }

        private static HashSet<string>? _known;
        private static bool _loaded;

        /// <summary>その芯の PNG が実際に焼かれて配られているか。
        /// ⚠️ `IconManifest.Exists` と同じ理由で、**一覧が読めなかったときは「無い」と疑わない**
        /// （埋め込み手順が壊れているだけで全種族が missing 扱いになる事故を避ける）。</summary>
        public static bool Exists(string stem)
        {
            if (_known == null) _known = Load();
            return !_loaded || _known.Contains(stem);
        }

        private static HashSet<string> Load()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            var asm = typeof(SpriteManifest).Assembly;
            string path = asm.GetName().Name + ".SpriteManifest.txt";
            using var stream = asm.GetManifestResourceStream(path);
            if (stream == null)
            {
                Console.WriteLine("SpriteManifest: 埋め込みが見つからない（csproj を見る）");
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
