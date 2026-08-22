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

        /// <summary>添字を表す文字。⭐ **この並びが唯一の出所。**
        ///
        /// ⚠️ 帳面（`Sheet`）・編集画面・ここの3か所が同じ規則を**別々に**持っていた頃、
        /// 色を増やすたびに1か所ずつ直し忘れた。⭐ 読む側も書く側もここを通す。
        ///
        /// ⚠️ 0 番（透明）は '.'。1 番からがこの文字列の先頭。</summary>
        public const string Digits = "123456789abcdef";

        /// <summary>使える色の数。⭐ 15（<see cref="Digits"/> の長さ）。
        /// ⚠️ 2026-08-21 に 9 から広げた（作者の絵が 11 色だったため）。
        /// ⚠️ これ以上増やすなら、1文字1画素という書き方そのものを見直すこと
        /// ── 2文字にすると、帳面が人の目で読めなくなる。</summary>
        public static int MaxIndex => Digits.Length;

        /// <summary>添字 → 文字。⚠️ 0（透明）は '.'。</summary>
        public static char CharOf(byte index)
        {
            if (index == 0) return '.';
            if (index > Digits.Length)
                throw new ArgumentException($"添字 {index} は色の上限 {Digits.Length} を超えている");
            return Digits[index - 1];
        }

        /// <summary>文字 → 添字。⚠️ 読めない文字は -1（呼び側が場所つきで叱る）。</summary>
        public static int IndexOf(char ch)
        {
            if (ch == '.') return 0;
            int at = Digits.IndexOf(ch);
            return at < 0 ? -1 : at + 1;
        }

        /// <summary>'.' を透明、<see cref="Digits"/> をパレットの添字として読む。</summary>
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
                    int index = IndexOf(row[x]);
                    if (index < 0)
                    {
                        throw new ArgumentException(
                            $"PixelSprite.Parse: {y} 行 {x} 列に '{row[x]}'。'.' か '{Digits}' のみ");
                    }
                    pixels[y * width + x] = (byte)index;
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
