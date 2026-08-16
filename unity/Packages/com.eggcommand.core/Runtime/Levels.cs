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
    /// この仕組みの肝なので、見せるのは**4本の内訳**。Lv は添え物。
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

        /// <summary>合成で入る点。⭐ 食わせた個体の Lv に比例する。
        ///
        /// ⭐ だから「育てた個体を食わせる」ほうが効く。余りをただ流し込む蛇口ではなく、
        /// 「この個体を育てるか、燃料にするか」の判断になる。
        /// ⚠️ 1 未満にしない（食わせたのに何も起きないと、何が悪かったのか分からない）。</summary>
        public static int FeedValueOf(Creature food)
        {
            int value = Of(food) / FeedDivisor;
            return value < 1 ? 1 : value;
        }

        /// <summary>⚠️ 小さくすると余りを流し込むだけで上限に届く。
        /// 大きくすると合成が無意味になる。素質24の個体3体でおよそ +9。</summary>
        public const int FeedDivisor = 8;
    }
}
