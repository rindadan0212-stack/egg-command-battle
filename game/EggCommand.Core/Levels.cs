#nullable enable
using System;

namespace EggCommand.Core
{
    /// <summary>レベル。⭐ **ARK と同じで「振られた点の数」**。別の物差しを増やさない。
    ///
    /// <code>
    /// Lv = 素質の合計（生まれつき） + 育てた分
    /// </code>
    ///
    /// ⭐ 育てられる分は**全個体で共通**なので、上へ行くには
    /// 「生まれつきが高い個体」が要る ＝ 次の世代へ繋ぐ動機になる。
    ///
    /// ⚠️ 画面で Lv を主役にしない。Lv150 同士がまるで別物であることが
    /// この仕組みの肝なので、見せるのは**ステの内訳**。Lv は添え物。
    /// </summary>
    public static class Levels
    {
        /// <summary>育てられる分。全個体共通。⚠️ ここを個体差にしない
        /// （差にすると「育ちやすい個体」が一番強いという別の一本道ができる）。</summary>
        public const int GrowMax = Creatures.TrainMax;

        /// <summary>生まれつき。⭐ 遺伝だけで決まる。</summary>
        public static int BirthOf(Creature creature) => Stats.TotalOf(creature.Wild);

        public static int Of(Creature creature) => BirthOf(creature) + creature.Earned;

        public static int MaxOf(Creature creature) => BirthOf(creature) + GrowMax;

        public static bool IsMaxed(Creature creature) => creature.Earned >= GrowMax;

        // ── EXP ───────────────────────────────────────────
        // ⭐ **育成の通貨は EXP ひとつ。**（作者の指示 2026-08-19）
        //    放置で溜まるのも、合成で個体を食わせて得るのも、同じ EXP。
        // ⚠️ 前は「素材」という別の名前で、1レベルの値段が一律 10 だった。
        //    一律だと Lv1 も Lv19 も同じ手間で、育てきる山場が無かった。

        /// <summary>Lv0 の次の1段に要る EXP（土台）。</summary>
        public const int ExpBase = 2;

        /// <summary>次の1レベルに要る EXP。⭐ **「何レベルになるか」で決まる。**
        ///
        /// ⚠️ **育てた回数ではない。**（作者の指示 2026-08-19）
        /// Lv1 の個体が Lv20 になるのと、Lv80 の個体が Lv100 になるのが同じ値段では、
        /// Lv という数の意味が消える。⭐ 高い Lv ほど重くなるのは**意図どおり**。
        ///
        /// ⚠️ ここでいう Lv は <see cref="Of"/>（素質の合計 ＋ 育てた分）。
        /// 素質の高い個体は生まれた時点で高い Lv にいるので、1段が最初から重い。</summary>
        public static int ExpToNextAt(int level) => ExpBase + (level < 0 ? 0 : level);

        /// <summary>その個体の次の1レベルに要る EXP。⚠️ 上限に達していたら 0。</summary>
        public static int ExpToNext(Creature creature) =>
            IsMaxed(creature) ? 0 : ExpToNextAt(Of(creature));

        /// <summary>Lv <paramref name="from"/> から <paramref name="to"/> までに要る EXP。
        /// ⭐ 等差数列の和。⚠️ 1段ずつ足した値と必ず一致する（検査で押さえてある）。</summary>
        public static int ExpBetween(int from, int to)
        {
            if (from < 0) from = 0;
            if (to <= from) return 0;
            int steps = to - from;
            return steps * ExpBase + (from + to - 1) * steps / 2;
        }

        /// <summary>その個体に注がれた EXP。⭐ 分解で返ってくる元。</summary>
        public static int InvestedExpOf(Creature creature) =>
            ExpBetween(BirthOf(creature), Of(creature));

        /// <summary>その EXP で何レベル上がるか。⚠️ 育成の上限も見る。
        /// ⭐ 画面の予告と実際の処理が食い違わないよう、数え方はここ1箇所。</summary>
        public static int LevelsFor(Creature creature, int exp)
        {
            int gained = 0;
            while (creature.Earned + gained < GrowMax)
            {
                int cost = ExpToNextAt(Of(creature) + gained);
                if (exp < cost) break;
                exp -= cost;
                gained++;
            }
            return gained;
        }

        /// <summary>分解で返る EXP。⭐ **その個体の Lv に応じて増える。**
        ///
        /// ⭐ 内訳は「その個体に注いだ EXP」＋「生まれつきのぶん」。
        /// だから **育てた個体を分解するほど返る** ── 捨てる操作ではなく、
        /// 「この個体を育てるか、EXP に還すか」の判断になる。
        /// ⚠️ 1 未満にしない（分解したのに何も入らないと、何が悪かったのか分からない）。</summary>
        public static int DissolveExpOf(Creature creature)
        {
            int value = InvestedExpOf(creature) + BirthOf(creature) * BirthExp / BirthDivisor;
            return value < 1 ? 1 : value;
        }

        /// <summary>生まれつき1点あたりの EXP（<see cref="BirthDivisor"/> で割る）。
        /// ⭐ 育てていない個体でも「素質のぶん」は返る。
        /// ⚠️ 大きくすると、孵しては分解するだけで EXP が回り、探索に戻る理由が消える。</summary>
        public const int BirthExp = 3;

        public const int BirthDivisor = 8;
    }
}
