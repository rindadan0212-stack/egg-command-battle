#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EggCommand.Core;

namespace EggCommand.Sim
{
    /// <summary>模様と色を差し替えて見比べる（`shots/egg-patterns.png`）。
    ///
    /// ⭐ **決めるための道具であって、焼くための道具ではない。**⚠️ ここが書く PNG は
    /// 見本の一覧1枚だけで、ゲームは読まない（`shots/` は版管理に入れない）。
    /// 気に入った組を `Core.EggSkins` の表へ書いて、`sim egg-art` で焼く。
    ///
    /// 使い方:
    ///   sim egg-try                       … 全模様 × 既定の3色組
    ///   sim egg-try #1a1d49 #f7f5ea       … 全模様を、その 地/模様 の2色で
    ///
    /// ⚠️ **色は自由**（16進をそのまま渡す）── 模様は種族を見ないので、
    /// どの模様にどの色を当ててもよい。</summary>
    public static class EggTry
    {
        public const string Dir = "shots";
        public const string File_ = "egg-patterns.png";

        /// <summary>色を指定しなかったときの見本。⭐ 明暗・寒暖を散らす
        /// （同系色だけだと「模様が見えるか」の判断を誤る）。</summary>
        private static readonly (string Ground, string Ink, string Name)[] Sets =
        {
            ("#e0d0a8", "#8f6a3e", "生成りに茶"),
            ("#3a4a7a", "#f2e9c9", "紺に生成り"),
            ("#d95f4a", "#ffd76a", "朱に金"),
        };

        public static void Run(string root, string[] args)
        {
            // ⚠️ 引数は「地・模様」の順。⭐ 片方だけ渡されたら既定を使う（黙って落とさない）
            var sets = args.Length >= 2
                ? new[] { (Ground: args[0], Ink: args[1], Name: args[0] + " / " + args[1]) }
                : Sets;

            var looks = Enum.GetValues(typeof(EggSkins.Mode)).Cast<EggSkins.Mode>().ToArray();
            int cellW = EggSkins.Shape.Width, cellH = EggSkins.Shape.Height;
            const int Pad = 3;

            int cols = looks.Length;
            int rows = sets.Length;
            int wide = cols * (cellW + Pad) + Pad;
            int tall = rows * (cellH + Pad) + Pad;

            // ⭐ 地は暗い灰。⚠️ 白地だと薄い色の模様が飛ぶ
            var canvas = new byte[wide * tall * 4];
            for (int i = 0; i < wide * tall; i++)
            {
                canvas[i * 4] = 0x1c; canvas[i * 4 + 1] = 0x20;
                canvas[i * 4 + 2] = 0x30; canvas[i * 4 + 3] = 0xff;
            }

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    var sprite = Shape(looks[c]);
                    var palette = new Palette(EggSkins.EdgeColor, sets[r].Ground, sets[r].Ink);
                    Blit(canvas, wide, Pad + c * (cellW + Pad), Pad + r * (cellH + Pad), sprite, palette);
                }
            }

            var outDir = Path.Combine(root, Dir);
            Directory.CreateDirectory(outDir);
            var path = Path.Combine(outDir, File_);
            File.WriteAllBytes(path, SpritePng.EncodeRgba(wide, tall, canvas));

            Console.WriteLine();
            Console.WriteLine($"■ 模様の見本を書いた: {Dir}/{File_}  （{cols} 模様 × {rows} 色組）");
            Console.WriteLine("  横の並び（左から）:");
            for (int c = 0; c < cols; c++)
                Console.WriteLine($"    {c + 1,2}. {looks[c]}{(Assigned(looks[c]) is string who ? "  ← " + who : "  （種族に未割り当て）")}");
            Console.WriteLine("  縦の並び（上から）:");
            for (int r = 0; r < rows; r++) Console.WriteLine($"    {sets[r].Name}");
            Console.WriteLine();
            Console.WriteLine("  ⭐ 気に入った組は `Core.EggSkins` の表へ書いて `sim egg-art` で焼く。");
        }

        /// <summary>その模様を使っている種族（無ければ null）。
        /// ⚠️ 一覧に「もう使っている模様」が分かるように出す。</summary>
        private static string? Assigned(EggSkins.Mode look)
        {
            var users = SpeciesTable.All.Where(s => EggSkins.Of(s.Id).Look == look)
                .Select(s => s.Name).ToList();
            return users.Count == 0 ? null : string.Join("・", users);
        }

        /// <summary>その模様を当てた卵の形。⚠️ 種族を通さずに模様だけで組む
        /// （`EggSkins.Build` は種族 id を取るので、ここは表を経由しない道が要る）。</summary>
        private static PixelSprite Shape(EggSkins.Mode look) => EggSkins.BuildLook(look);

        /// <summary>1枚を RGBA の板へ貼る。⚠️ 添字0（透明）は飛ばす。</summary>
        private static void Blit(byte[] canvas, int wide, int atX, int atY,
            PixelSprite sprite, Palette palette)
        {
            for (int y = 0; y < sprite.Height; y++)
            {
                for (int x = 0; x < sprite.Width; x++)
                {
                    byte at = sprite.At(x, y);
                    if (at == 0) continue;
                    var (r, g, b) = Rgb(palette.ColorOf(at));
                    int i = ((atY + y) * wide + (atX + x)) * 4;
                    canvas[i] = r; canvas[i + 1] = g; canvas[i + 2] = b; canvas[i + 3] = 0xff;
                }
            }
        }

        private static (byte R, byte G, byte B) Rgb(string hex)
        {
            string s = hex.TrimStart('#');
            return (Convert.ToByte(s.Substring(0, 2), 16),
                    Convert.ToByte(s.Substring(2, 2), 16),
                    Convert.ToByte(s.Substring(4, 2), 16));
        }
    }
}
