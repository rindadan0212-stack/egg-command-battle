#nullable enable
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>⭐ **絵の「どこを見せるか」**（2026-08-29・作者の指示
    /// 「BOX一覧の升はイラストの一部だけを表示し意図的に見切れさせる。見せたいところだけを見せる」
    /// 「キャラごとにズームアップ箇所を変えたい」）。
    ///
    /// 🔴 **種族の決まり（<see cref="Species"/>）とは別に置く。**あちらは強さと技の話で、
    /// こちらは「窓から覗かせる位置」という**見せ方だけ**の話 ── 混ぜると、絵の座標を
    /// 直したいだけのときに種族表を触ることになる。
    ///
    /// ⚠️ **全種族ぶん手で埋めなくてよい。**書いていない種族は
    /// <see cref="Fallback"/> が絵そのものから割り出す（下に理屈）。⭐ 手で書くのは
    /// 「割り出しでは狙いが外れた種族」だけ。</summary>
    public static class SpeciesArt
    {
        /// <summary>⭐ 手で決めた「見せどころ」（0〜1 の割合・絵の左上が 0,0）。
        ///
        /// ⚠️ ここに**無い種族は書き忘れではない** ── <see cref="Fallback"/> の割り出しで
        /// 足りているという意味。⭐ 見て「顔が切れている」と思ったらここに1行足す。
        ///
        /// ⚠️ 数は「絵のどこを窓の真ん中へ持ってくるか」。⭐ 0.5,0.5 なら絵の中心、
        /// y を小さくするほど**上（顔）**が窓の真ん中に来る。</summary>
        private static readonly Dictionary<string, (double X, double Y)> Chosen =
            new Dictionary<string, (double, double)>
            {
                // ⭐ 作者が描いた 64x64 の4種は、割り出し（体の上から3割）でおおむね顔に載る。
                //    ⚠️ ノビルだけは首が長く、上から3割が**首**に当たるので手で上げてある。
                { "nobiru", (0.49, 0.22) },
            };

        /// <summary>その種族の「見せどころ」。⭐ 手書きが在ればそれ、無ければ絵から割り出す。</summary>
        public static (double X, double Y) FocusOf(string? id, PixelSprite sprite)
        {
            if (id != null && Chosen.TryGetValue(id, out var picked)) return picked;
            return Fallback(sprite);
        }

        /// <summary>⚠️ **絵そのものから割り出す。**⭐ 透明でない画素の囲みを取り、
        /// 横は真ん中・縦は**上から3割**の点を返す。
        ///
        /// ⭐ この3割は「顔は体の上のほうにある」という当てずっぽうではなく、実測に基づく
        /// ── 手描きの 16x16 七種は目が 上から 0.41〜0.53（<see cref="Chosen"/> に1つも
        /// 載っていないのはこのため）、取り込みの 64x64 四種も 0.29〜0.35 に顔が来る。
        /// ⚠️ 中心（0.5）にすると、背の高い種族で顔が窓から外れる。
        ///
        /// ⚠️ 絵が丸ごと透明なら中心を返す（0 で割らない）。</summary>
        private static (double X, double Y) Fallback(PixelSprite sprite)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            for (int y = 0; y < sprite.Height; y++)
            {
                for (int x = 0; x < sprite.Width; x++)
                {
                    if (sprite.At(x, y) == 0) continue;   // ⚠️ 添字0は透明
                    if (x < x0) x0 = x;
                    if (y < y0) y0 = y;
                    if (x > x1) x1 = x;
                    if (y > y1) y1 = y;
                }
            }
            if (x1 < 0) return (0.5, 0.5);
            double cx = (x0 + x1 + 1) / 2.0 / sprite.Width;
            double cy = (y0 + (y1 - y0 + 1) * 0.30) / sprite.Height;
            return (cx, cy);
        }
    }
}
