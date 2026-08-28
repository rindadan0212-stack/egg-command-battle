#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EggCommand.Core;

namespace EggCommand.Sim
{
    /// <summary>種族ごとの卵を PNG に焼く（`assets/ui/paint/egg-&lt;種族&gt;.png`）。
    ///
    /// ⭐ **意匠は `Core.EggSkins` が持つ。**ここは焼くだけ ── 模様を直したいときは
    /// あちらの式を触って、このコマンドを走らせ直す。
    ///
    /// ⚠️ **なぜ PNG に焼くか。**⭐ 画面は `paint`（`&lt;img&gt;` 1枚）で出す。
    /// `pixel` として実行時に描くと `LayoutDom.Dots` の SVG 経路に落ち、
    /// 卵1つで 1762 個の `&lt;rect&gt;`（巣5つで約9千）になる。
    ///
    /// ⚠️ **`paint-placeholder` とは役割が違う。**あちらは「まだ無い絵の仮置き」で
    /// 既にあるファイルを上書きしない。⭐ こちらは**焼き直す道具**なので上書きする
    /// （出所は `EggSkins` であって PNG ではない）。</summary>
    public static class EggSkinPng
    {
        public const string Dir = "assets/ui/paint";

        public static void Run(string root)
        {
            var outDir = Path.Combine(root, Dir);
            Directory.CreateDirectory(outDir);

            var made = new List<string>();
            foreach (var species in SpeciesTable.All)
            {
                var sprite = EggSkins.Build(species.Id);
                var palette = EggSkins.PaletteOf(species.Id);
                string name = EggSkins.NameOf(species.Id);
                File.WriteAllBytes(Path.Combine(outDir, name + ".png"),
                    SpritePng.Encode(sprite, palette));
                made.Add($"{name}.png  {sprite.Width}x{sprite.Height}  {EggSkins.Of(species.Id).Look}");
            }

            // ⭐ 大きさの一覧を書き直す。⚠️ ここを忘れると `PaintManifest` に載らず、
            //    画面は「絵が無い」の？印を出す（`LayoutDom.Paint`）。
            PaintPlaceholder.WriteManifestFor(outDir);

            Console.WriteLine();
            Console.WriteLine($"■ 卵の絵を焼いた: {made.Count} 枚 → {Dir}/");
            foreach (var line in made) Console.WriteLine("  " + line);
            Console.WriteLine($"  一覧を書き直した: {Dir}/{PaintPlaceholder.ManifestFile}");
        }
    }
}
