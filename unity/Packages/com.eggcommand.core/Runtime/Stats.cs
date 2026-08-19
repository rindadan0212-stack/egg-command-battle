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
        /// <summary>弱化命中。⭐ 弱化を**通す**力。⚠️ 速度から切り離した（2026-08-18）。</summary>
        Acc = 4,
        /// <summary>抵抗。⭐ 弱化を**受けない**力。</summary>
        Res = 5,
    }

    /// <summary>ステの6つ組。値の意味は文脈で変わる（基礎値・野生レベル・育成・実値）。
    ///
    /// ⚠️ 移植元（TS）は4つ組。⭐ 弱化命中・抵抗は**あとから足した**ので、
    /// 4つで作ると残り2つは 0 になる ── 較正済みの照合がそのまま通るようにするため。</summary>
    public readonly struct StatBlock : IEquatable<StatBlock>
    {
        public readonly int Hp;
        public readonly int Atk;
        public readonly int Def;
        public readonly int Spd;
        public readonly int Acc;
        public readonly int Res;

        public StatBlock(int hp, int atk, int def, int spd, int acc = 0, int res = 0)
        {
            Hp = hp;
            Atk = atk;
            Def = def;
            Spd = spd;
            Acc = acc;
            Res = res;
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
                    case StatKey.Acc: return Acc;
                    case StatKey.Res: return Res;
                    default: throw new ArgumentOutOfRangeException(nameof(key));
                }
            }
        }

        public StatBlock With(StatKey key, int value)
        {
            switch (key)
            {
                case StatKey.Hp: return new StatBlock(value, Atk, Def, Spd, Acc, Res);
                case StatKey.Atk: return new StatBlock(Hp, value, Def, Spd, Acc, Res);
                case StatKey.Def: return new StatBlock(Hp, Atk, value, Spd, Acc, Res);
                case StatKey.Spd: return new StatBlock(Hp, Atk, Def, value, Acc, Res);
                case StatKey.Acc: return new StatBlock(Hp, Atk, Def, Spd, value, Res);
                case StatKey.Res: return new StatBlock(Hp, Atk, Def, Spd, Acc, value);
                default: throw new ArgumentOutOfRangeException(nameof(key));
            }
        }

        public bool Equals(StatBlock other) =>
            Hp == other.Hp && Atk == other.Atk && Def == other.Def && Spd == other.Spd
            && Acc == other.Acc && Res == other.Res;

        public override bool Equals(object? obj) => obj is StatBlock other && Equals(other);

        public override int GetHashCode() => unchecked(
            ((((((Hp * 397) ^ Atk) * 397 ^ Def) * 397 ^ Spd) * 397 ^ Acc) * 397) ^ Res);

        public override string ToString() =>
            $"hp={Hp} atk={Atk} def={Def} spd={Spd} acc={Acc} res={Res}";
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
        public static readonly StatKey[] Keys =
            { StatKey.Hp, StatKey.Atk, StatKey.Def, StatKey.Spd, StatKey.Acc, StatKey.Res };

        /// <summary>戦闘中に強化・弱化で上下できるステ。
        ///
        /// ⚠️ <see cref="Keys"/> と**別に**持つ。HP は上下しない（最大HPが動くと割合回復が壊れる）。
        /// ⭐ 命中・抵抗も入れていない ── ここを技で動かせると、弱化の通る率を弱化で操れてしまい、
        /// 「先に弱化を通したほうが勝つ」の一手勝負に戻る。**育てて決める軸**のまま置く。</summary>
        public static readonly StatKey[] BuffKeys = { StatKey.Atk, StatKey.Def, StatKey.Spd };

        /// <summary>ステの桁。⭐ **実値だけを大きくする倍率。**
        ///
        /// ⭐ 作者の指示（2026-08-19）「全体的なステータスの数字を大きな桁にしたい。
        /// とてもよく育てた個体が10万HPになるくらいをめやすに」。
        ///
        /// ⚠️ **野生ロール（<see cref="Creature.Wild"/>）の単位は動かさない。**
        /// あれは 0〜40 の**点**で、合計上限の削り方・配合・巣の段階・Lv が全部その単位で書かれている。
        /// 動かすと移植元との照合（breeding / nest / game / steal）が丸ごと無効になる。
        /// ⭐ 桁は **種族の基礎値** と、ここで野生レベルに掛ける倍率だけで出す。
        ///
        /// ⚠️ **これと一緒に動かす定数の一覧**（片方だけ動かすと較正が丸ごと壊れる）:
        /// <list type="bullet">
        /// <item><see cref="Battle.AtkSoften"/> / <see cref="Battle.DefSoften"/></item>
        /// <item><see cref="Battle.GaugeBase"/> / <see cref="Battle.GaugeMax"/></item>
        /// <item><see cref="Battle.LandStatDivisor"/></item>
        /// <item><see cref="Battle.HpSpace"/>（AI の採点の桁）</item>
        /// <item><see cref="SpeciesTable.BaseTotal"/> / <see cref="SpeciesTable.DebuffBaseTotal"/></item>
        /// <item><see cref="Idle.EnemyHp"/>（編成の火力と直に比べる）</item>
        /// <item><see cref="Steal.SpeedToDistance"/>（盤は 0〜1 の座標なので**割り戻す**）</item>
        /// <item><see cref="Creatures.GrowthFlatOf"/>（実値の単位で配る）</item>
        /// </list>
        /// ⚠️ **この一覧が漏れていたせいで、2026-08-19 の桁上げで4件が置き去りになった**
        /// （放置の敵が瞬殺・潜入で誰でも一投・23技が採用0・執念が1/20）。
        /// ⭐ 定数を足したらここにも足すこと。</summary>
        public const int Scale = 5;

        /// <summary>1つのステに振れる野生レベルの上限。
        /// ⚠️ **点の単位**（実値ではない）。実値にするときに <see cref="Scale"/> が掛かる。</summary>
        public const int WildStatMax = 40;

        /// <summary>野生レベルの合計上限。
        /// ⭐ = <see cref="WildStatMax"/> × **3**。この比が「1体でいくつのステを伸ばせるか」を決める。
        ///
        /// ⚠️ 以前は ×2（得意を2つまで）だった。⭐ 弱化命中・抵抗を足してステが6本になり、
        /// 2つしか伸ばせないと「攻めか守りか」を選んだ時点で弱化の軸に手が届かなくなる。
        /// 3つにすると「攻め＋弱化」「守り＋抵抗」のような**組み合わせの判断**が生まれる。
        /// ⚠️ 6本のうち3本なので、まだ半分は薄いまま ── 万能は作れない。</summary>
        public const int WildTotalMax = WildStatMax * 3;

        /// <summary>変異が上限を押し上げられる回数。⚠️ ここが血統全体の天井になる。</summary>
        public const int MutationCapSteps = 20;

        public static string LabelOf(StatKey key)
        {
            switch (key)
            {
                // ⚠️ **ここが強化・弱化の名前の出所でもある**（攻撃力UP / スピードDOWN …）。
                //    画面のステ表と技の効果文で言葉が食い違わないよう、出所を1つにしてある。
                case StatKey.Hp: return "HP";
                case StatKey.Atk: return "攻撃力";
                case StatKey.Def: return "防御力";
                case StatKey.Spd: return "スピード";
                // ⚠️ **「弱化」を頭に付けたまま出す。**「命中」だけだと攻撃が当たる率と
                //    読み違える（このゲームの攻撃は必ず当たる）。⭐ 何に効く数かを名前で言い切る
                case StatKey.Acc: return "弱化命中";
                case StatKey.Res: return "弱化耐性";
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

        /// <summary>その個体の合計上限。⭐ 常に1ステ上限の3倍。
        /// この比を保つことで「得意を3つ作れる」がどの変異段階でも崩れない。
        /// ⚠️ 変異で1ステ上限が上がると合計も3倍で伸びる ── ここを2倍のまま置くと
        /// 変異するほど「3つ伸ばす」が窮屈になり、変異が特化を殺す側に回る。</summary>
        public static int WildTotalMaxFor(int mutationCounter) => WildStatMaxFor(mutationCounter) * 3;

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
        public static StatBlock ApplyTotalCap(StatBlock wild, int mutationCounter = 0) =>
            CapTo(wild, WildStatMaxFor(mutationCounter), WildTotalMaxFor(mutationCounter));

        /// <summary>上限を外から渡す版。⭐ **削り方そのもの**はここ1箇所にある。
        ///
        /// ⚠️ 上限の決め方（1ステ上限×何倍か）は遊びの調整で動く。
        /// 削り方と混ぜて書くと、倍率を変えた日に「削り方が移植元と同じか」を確かめられなくなる。
        /// ⭐ 分けてあるので、移植元の倍率を渡せば移植元の答えがそのまま出る。</summary>
        public static StatBlock CapTo(StatBlock wild, int statMax, int totalMax)
        {
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

            var capped = new StatBlock(0, 0, 0, 0);
            for (int i = 0; i < Keys.Length; i++) capped = capped.With(Keys[i], work[i]);
            return capped;
        }

        /// <summary>実値 = 種族基礎 + 野生レベル × <see cref="Scale"/> + 育成で振った分。
        ///
        /// ⚠️ **野生レベルにだけ倍率が掛かる。**種族基礎と育成した分は既に実値の単位。
        /// ⭐ ここが「点」と「実値」の境目 ── 唯一の出所。</summary>
        public static StatBlock ActualStats(StatBlock baseStats, StatBlock wild, StatBlock trained)
        {
            return new StatBlock(
                baseStats.Hp + wild.Hp * Scale + trained.Hp,
                baseStats.Atk + wild.Atk * Scale + trained.Atk,
                baseStats.Def + wild.Def * Scale + trained.Def,
                baseStats.Spd + wild.Spd * Scale + trained.Spd,
                baseStats.Acc + wild.Acc * Scale + trained.Acc,
                baseStats.Res + wild.Res * Scale + trained.Res);
        }
    }
}
