#nullable enable
using System;
using System.Collections.Generic;

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
        /// ⚠️ 0 番（透明）は '.'。1 番からがこの文字列の先頭。
        ///
        /// ⚠️ 2026-08-25 に 15 → 35（9 + a〜z）へ広げた（作者の手描き絵が18〜27色
        /// あり、15色では収まらなかったため）。⭐ 数を本文に直書きしない ── 常に
        /// <see cref="Digits"/>.Length（＝<see cref="MaxIndex"/>）を指すこと。
        /// ⚠️ `l` と `1`、`o` と `0` は人の目には紛らわしいが、帳面は主に機械（取り込み道具）
        /// が書くので採用してある。手で帳面を書くときは見間違いに注意すること。
        /// ⚠️ これ以上増やすなら、1文字1画素という書き方そのものを見直すこと
        /// ── 2文字にすると、帳面が人の目で読めなくなる。</summary>
        public const string Digits = "123456789abcdefghijklmnopqrstuvwxyz";

        /// <summary>使える色の数。⭐ <see cref="Digits"/> の長さ（2026-08-25 時点で35）。</summary>
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

    /// <summary>色の組。⚠️ 添字1がこの配列の0番。文字列は "#rrggbb"。
    ///
    /// ⭐ **0番（通常色）以外は、要素に null を書いて「指定しない」にできる**（2026-08-23）。
    /// null は 0番の同じ位置の色を**組み立て時に1度だけ**受け継ぐ（<see cref="ResolveGroup"/>）。
    /// ⚠️ 「指定しない」は**書き方の都合**でしかない ── 実行時に読む側
    /// （<see cref="ColorOf"/> や <c>.Colors</c> を直に読む帳面・PNG書き出し）は
    /// 1か所も変えていない。ここへ来るころには <see cref="Colors"/> に null は残らない。</summary>
    public sealed class Palette
    {
        public readonly string[] Colors;

        /// <summary>⚠️ 引数の型は <c>string?[]</c>（0番以外は null を書ける）だが、
        /// 保つ <see cref="Colors"/> は解決前後を問わず同じ配列型 ── 型を分けると、
        /// 「解決した後の Palette」と「まだ null が残る Palette」を読む側が見分けられなくなる。
        /// ⚠️ null が残ったまま <see cref="ColorOf"/> や PNG 書き出しへ渡ると、
        /// そこで「色は #rrggbb で書く」という別の検査が落ちる（サイレントに壊れない）。</summary>
        public Palette(params string?[] colors)
        {
            if (colors == null) throw new ArgumentNullException(nameof(colors));
            var copy = new string[colors.Length];
            for (int i = 0; i < colors.Length; i++) copy[i] = colors[i]!;
            Colors = copy;
        }

        public int Count => Colors.Length;

        /// <summary>添字色から実際の色を引く。⚠️ 添字0（透明）を渡さない。</summary>
        public string ColorOf(byte index)
        {
            if (index == 0) throw new ArgumentException("添字0は透明。色を引かない");
            if (index - 1 >= Colors.Length) throw new ArgumentException($"パレットに添字 {index} が無い");
            return Colors[index - 1];
        }

        /// <summary>🔴 **null を0番から受け継いで消す。呼ぶのは組み立て時に1度だけ**
        /// （<see cref="Species"/> のコンストラクタ）。
        ///
        /// ⚠️ **0番（<paramref name="raw"/>[0]）自身に null があれば投げる**
        /// （受け継ぐ先が無い）。⭐ 1番以降は、0番と同じ位置が null なら0番の色を写す。
        /// ⚠️ 色の値は1つも変えない ── 埋めるのは「無かったところ」だけ。</summary>
        public static IReadOnlyList<Palette> ResolveGroup(IReadOnlyList<Palette> raw)
        {
            if (raw == null) throw new ArgumentNullException(nameof(raw));
            if (raw.Count == 0) return raw;

            var baseline = raw[0];
            for (int i = 0; i < baseline.Colors.Length; i++)
            {
                if (baseline.Colors[i] == null)
                    throw new ArgumentException($"0番のパレットの{i}番目が null（受け継ぐ先が無い）");
            }

            var resolved = new Palette[raw.Count];
            resolved[0] = baseline;
            for (int p = 1; p < raw.Count; p++)
            {
                var src = raw[p];
                if (src.Colors.Length != baseline.Colors.Length)
                {
                    throw new ArgumentException(
                        $"{p}番のパレットが{src.Colors.Length}色（0番の{baseline.Colors.Length}色に揃える）");
                }
                var colors = new string[src.Colors.Length];
                for (int i = 0; i < colors.Length; i++) colors[i] = src.Colors[i] ?? baseline.Colors[i];
                resolved[p] = new Palette(colors);
            }
            return resolved;
        }
    }
}
