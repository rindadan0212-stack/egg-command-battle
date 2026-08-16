// ⚠️ Unity は既定で nullable 文脈が切れている。ファイル単位で入れて
//    dotnet 側（csproj の Nullable=enable）と食い違わないようにする。
#nullable enable
using System;

namespace EggCommand.Core
{
    /// <summary>ステータスの並び。⚠️ TS の <c>STAT_KEYS</c> と順が1つでも違うと、
    /// 合計上限の削り方（同値のとき先に来たものから削る）がずれる。</summary>
    public enum StatKey
    {
        Hp = 0,
        Atk = 1,
        Def = 2,
        Spd = 3,
    }

    /// <summary>ステの4つ組。値の意味は文脈で変わる（基礎値・野生レベル・育成・実値）。</summary>
    public readonly struct StatBlock : IEquatable<StatBlock>
    {
        public readonly int Hp;
        public readonly int Atk;
        public readonly int Def;
        public readonly int Spd;

        public StatBlock(int hp, int atk, int def, int spd)
        {
            Hp = hp;
            Atk = atk;
            Def = def;
            Spd = spd;
        }

        public int this[StatKey key]
        {
            get
            {
                switch (key)
                {
                    case StatKey.Hp: return Hp;
                    case StatKey.Atk: return Atk;
                    case StatKey.Def: return Def;
                    case StatKey.Spd: return Spd;
                    default: throw new ArgumentOutOfRangeException(nameof(key));
                }
            }
        }

        public StatBlock With(StatKey key, int value)
        {
            switch (key)
            {
                case StatKey.Hp: return new StatBlock(value, Atk, Def, Spd);
                case StatKey.Atk: return new StatBlock(Hp, value, Def, Spd);
                case StatKey.Def: return new StatBlock(Hp, Atk, value, Spd);
                case StatKey.Spd: return new StatBlock(Hp, Atk, Def, value);
                default: throw new ArgumentOutOfRangeException(nameof(key));
            }
        }

        public bool Equals(StatBlock other) =>
            Hp == other.Hp && Atk == other.Atk && Def == other.Def && Spd == other.Spd;

        public override bool Equals(object? obj) => obj is StatBlock other && Equals(other);

        public override int GetHashCode() => unchecked((((Hp * 397) ^ Atk) * 397 ^ Def) * 397 ^ Spd);

        public override string ToString() => $"hp={Hp} atk={Atk} def={Def} spd={Spd}";
    }

    /// <summary>強さの唯一の出所。
    ///
    /// ⚠️ 実値・上限・削りの計算をここ以外に書かない。
    /// 戦闘・シミュレータ・画面が全部この関数を呼ぶ。
    /// 同じことを2箇所で決めると、片方だけ直しても直らない不具合になる。
    /// </summary>
    public static class Stats
    {
        /// <summary>⚠️ この順が削りの順。TS の <c>STAT_KEYS</c> と揃える。</summary>
        public static readonly StatKey[] Keys = { StatKey.Hp, StatKey.Atk, StatKey.Def, StatKey.Spd };

        /// <summary>1つのステに振れる野生レベルの上限。</summary>
        public const int WildStatMax = 40;

        /// <summary>野生レベルの合計上限。
        /// ⭐ = <see cref="WildStatMax"/> × 2。この比が「1体でいくつのステを伸ばせるか」を決めている。
        /// 2倍にしたのは「得意を2つ作れる」を保証したかったから。</summary>
        public const int WildTotalMax = WildStatMax * 2;

        /// <summary>変異が上限を押し上げられる回数。⚠️ ここが血統全体の天井になる。</summary>
        public const int MutationCapSteps = 20;

        public static string LabelOf(StatKey key)
        {
            switch (key)
            {
                case StatKey.Hp: return "HP";
                case StatKey.Atk: return "攻撃";
                case StatKey.Def: return "防御";
                case StatKey.Spd: return "速度";
                default: throw new ArgumentOutOfRangeException(nameof(key));
            }
        }

        /// <summary>その個体の1ステ上限。変異1回につき +1。
        /// ⚠️ 変異で押し上げたぶんが上限で即削られると、変異の価値が消える。</summary>
        public static int WildStatMaxFor(int mutationCounter)
        {
            int clamped = mutationCounter < 0 ? 0 : mutationCounter;
            if (clamped > MutationCapSteps) clamped = MutationCapSteps;
            return WildStatMax + clamped;
        }

        /// <summary>その個体の合計上限。⭐ 常に1ステ上限の2倍。
        /// この比を保つことで「得意を2つ作れる」がどの変異段階でも崩れない。</summary>
        public static int WildTotalMaxFor(int mutationCounter) => WildStatMaxFor(mutationCounter) * 2;

        public static int TotalOf(StatBlock stats)
        {
            int sum = 0;
            for (int i = 0; i < Keys.Length; i++) sum += stats[Keys[i]];
            return sum;
        }

        /// <summary>合計上限を守る。超過分は低いステから削る。
        ///
        /// ⭐ これが「何かが特化していれば何かが伸びない」を実装に落としている。
        /// 高いステは残り、低いステが犠牲になるので、特化は保たれたまま万能個体だけが禁じられる。
        ///
        /// 同値のステが複数あるときは順に1ずつ削る（片方だけを掘り下げて偏らせないため）。
        /// </summary>
        public static StatBlock ApplyTotalCap(StatBlock wild, int mutationCounter = 0)
        {
            int statMax = WildStatMaxFor(mutationCounter);
            int totalMax = WildTotalMaxFor(mutationCounter);

            var work = new int[Keys.Length];
            for (int i = 0; i < Keys.Length; i++)
            {
                int v = wild[Keys[i]];
                if (v < 0) v = 0;
                if (v > statMax) v = statMax;
                work[i] = v;
            }

            int excess = 0;
            for (int i = 0; i < work.Length; i++) excess += work[i];
            excess -= totalMax;

            while (excess > 0)
            {
                int min = int.MaxValue;
                for (int i = 0; i < work.Length; i++)
                {
                    if (work[i] > 0 && work[i] < min) min = work[i];
                }
                if (min == int.MaxValue) break; // 全部0。合計上限が0でない限り起きない

                for (int i = 0; i < work.Length; i++)
                {
                    if (excess == 0) break;
                    if (work[i] == min)
                    {
                        work[i]--;
                        excess--;
                    }
                }
            }

            return new StatBlock(work[0], work[1], work[2], work[3]);
        }

        /// <summary>実値 = 種族基礎 + 野生レベル + 育成で振った分。
        /// 🚧 尺度は1つだけ持つ。「HP だけ5倍」のような根拠の無い係数は置かない。</summary>
        public static StatBlock ActualStats(StatBlock baseStats, StatBlock wild, StatBlock trained)
        {
            return new StatBlock(
                baseStats.Hp + wild.Hp + trained.Hp,
                baseStats.Atk + wild.Atk + trained.Atk,
                baseStats.Def + wild.Def + trained.Def,
                baseStats.Spd + wild.Spd + trained.Spd);
        }
    }
}
