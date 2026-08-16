#nullable enable
using System;

namespace EggCommand.Core
{
    /// <summary>添字色（index color）のドット絵。
    ///
    /// ⭐ 変異＝パレットスワップ。絵は1つだけ持ち、色の組だけ差し替える。
    /// これで1体ぶんのドットから変異個体が無限に作れる（ARK と同じ手法）。
    ///
    /// ⚠️ 描画はここに置かない。Core は UnityEngine に依存しない（asmdef の
    /// noEngineReferences で機械的に禁じてある）。Texture2D への変換は見た目の層の仕事。
    /// ここが持つのは「どの画素がパレットの何番か」だけ。</summary>
    public sealed class PixelSprite
    {
        /// <summary>添字0は必ず透明。1以降がパレットの添字を指す。</summary>
        public readonly int Width;
        public readonly int Height;
        public readonly byte[] Pixels;

        private PixelSprite(int width, int height, byte[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
        }

        public byte At(int x, int y) => Pixels[y * Width + x];

        /// <summary>'.' を透明、'1'〜'9' をパレットの添字として読む。</summary>
        public static PixelSprite Parse(string[] rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            int height = rows.Length;
            if (height == 0) throw new ArgumentException("PixelSprite.Parse: 行が無い");
            int width = rows[0].Length;

            var pixels = new byte[width * height];
            for (int y = 0; y < height; y++)
            {
                string row = rows[y];
                if (row.Length != width)
                {
                    throw new ArgumentException(
                        $"PixelSprite.Parse: {y} 行目の幅が {row.Length}（期待 {width}）");
                }
                for (int x = 0; x < width; x++)
                {
                    char ch = row[x];
                    if (ch == '.')
                    {
                        pixels[y * width + x] = 0;
                        continue;
                    }
                    if (ch < '0' || ch > '9')
                    {
                        throw new ArgumentException(
                            $"PixelSprite.Parse: {y} 行 {x} 列に '{ch}'。'.' か '0'〜'9' のみ");
                    }
                    pixels[y * width + x] = (byte)(ch - '0');
                }
            }

            return new PixelSprite(width, height, pixels);
        }
    }

    /// <summary>色の組。⚠️ 添字1がこの配列の0番。文字列は "#rrggbb"。</summary>
    public sealed class Palette
    {
        public readonly string[] Colors;

        public Palette(params string[] colors)
        {
            Colors = colors ?? throw new ArgumentNullException(nameof(colors));
        }

        public int Count => Colors.Length;

        /// <summary>添字色から実際の色を引く。⚠️ 添字0（透明）を渡さない。</summary>
        public string ColorOf(byte index)
        {
            if (index == 0) throw new ArgumentException("添字0は透明。色を引かない");
            if (index - 1 >= Colors.Length) throw new ArgumentException($"パレットに添字 {index} が無い");
            return Colors[index - 1];
        }
    }
}
