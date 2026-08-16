using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>
    /// 決定論の土台。乱数はすべてここを通す。
    ///
    /// ⚠️ <c>System.Random</c> や <c>UnityEngine.Random</c> は使わない。
    /// 同じ種からは必ず同じ結果が出ること。これが崩れていると、
    /// どんな観測の仕組みを足しても「たまたま違う」を排除できない。
    ///
    /// ⭐ 系統(stream)を分ける理由:
    /// 乱数の消費数が変わると以降の系列が全部ずれ、較正済みの検査が無効になる。
    /// 系統を分けておけば、戦闘に新しい乱数の使い手が増えても遺伝の系列はずれない。
    ///
    /// <code>
    /// var root = new Rng(20260815);
    /// var breeding = root.Stream("breeding");   // 配合・遺伝・変異
    /// var battle   = root.Stream("battle");     // 敵AI の揺れ
    /// </code>
    ///
    /// ⚠️ **TypeScript 版と1ビットも違わないこと**が移植の条件。
    /// JS の <c>Math.imul</c> と <c>&gt;&gt;&gt;</c> を、C# では
    /// <c>unchecked</c> の <c>int</c> 乗算と <c>uint</c> シフトで再現する。
    /// 較正済みの数値（変異 2.5%×3回 など）は、この系列が同じであることに依存している。
    /// </summary>
    public sealed class Rng
    {
        private const double U32 = 4294967296.0;

        public uint Seed { get; }

        private uint _a;
        private uint _b;
        private uint _c;
        private uint _d;

        public Rng(long seed)
        {
            Seed = unchecked((uint)seed);
            uint s = Seed;
            _a = SplitMix32(ref s);
            _b = SplitMix32(ref s);
            _c = SplitMix32(ref s);
            _d = SplitMix32(ref s);
            // 初期状態の偏りを流す
            for (int i = 0; i < 12; i++) Next();
        }

        /// <summary>FNV-1a。系統名を種に混ぜるためだけに使う（暗号用途ではない）。</summary>
        public static uint HashString(string text)
        {
            uint h = 0x811c9dc5u;
            for (int i = 0; i < text.Length; i++)
            {
                h ^= text[i];
                h = unchecked((uint)((int)h * 0x01000193));
            }
            return h;
        }

        /// <summary>1つの種を4語へ広げる。sfc32 の初期化に使う。</summary>
        private static uint SplitMix32(ref uint state)
        {
            unchecked
            {
                state = (uint)((int)state + unchecked((int)0x9e3779b9));
                uint t = state ^ (state >> 16);
                t = (uint)((int)t * unchecked((int)0x21f0aaad));
                t ^= t >> 15;
                t = (uint)((int)t * unchecked((int)0x735a2d97));
                t ^= t >> 15;
                return t;
            }
        }

        /// <summary>sfc32。状態128bit・整数演算のみ・速い。ゲーム用途には十分な品質。</summary>
        private uint Next()
        {
            unchecked
            {
                uint t = (uint)((int)((uint)((int)_a + (int)_b)) + (int)_d);
                _d = (uint)((int)_d + 1);
                _a = _b ^ (_b >> 9);
                _b = (uint)((int)_c + (int)(_c << 3));
                _c = (_c << 21) | (_c >> 11);
                _c = (uint)((int)_c + (int)t);
                return t;
            }
        }

        /// <summary>系統を分ける。同じ (親の種, 名前) からは必ず同じ系統が出る。</summary>
        public Rng Stream(string name) => new Rng(Seed ^ HashString(name));

        /// <summary>符号なし32bit整数。</summary>
        public uint U32Value() => Next();

        /// <summary>[0, 1) の実数。</summary>
        public double Float() => Next() / U32;

        /// <summary>[min, maxExclusive) の整数。棄却法で偏りを出さない。</summary>
        public int Int(int min, int maxExclusive)
        {
            long range = (long)maxExclusive - min;
            if (range <= 0)
            {
                throw new ArgumentException($"Rng.Int の範囲が空 (min={min}, maxExclusive={maxExclusive})");
            }
            // 端数ぶんを捨てて一様性を保つ
            long limit = (long)Math.Floor(U32 / range) * range;
            uint v = Next();
            while (v >= limit) v = Next();
            return (int)(min + (v % range));
        }

        /// <summary>確率 probability で true。0.025 のような小さい値も扱える。</summary>
        public bool Chance(double probability) => Float() < probability;

        /// <summary>1つ選ぶ。⚠️ 空なら投げる（黙って既定値を返さない）。</summary>
        public T Pick<T>(IReadOnlyList<T> items)
        {
            if (items.Count == 0) throw new ArgumentException("Rng.Pick に空の並びが渡された");
            return items[Int(0, items.Count)];
        }

        /// <summary>破壊的にシャッフル（Fisher-Yates）。</summary>
        public IList<T> Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Int(0, i + 1);
                T a = items[i];
                items[i] = items[j];
                items[j] = a;
            }
            return items;
        }

        /// <summary>重複なしで n 個取り出す。配合の「4枠から2つ抽選」で使う。</summary>
        public List<T> Sample<T>(IReadOnlyList<T> items, int n)
        {
            if (n > items.Count)
            {
                throw new ArgumentException($"Rng.Sample: {items.Count} 個から {n} 個は取れない");
            }
            var copy = new List<T>(items);
            Shuffle(copy);
            return copy.GetRange(0, n);
        }
    }
}
