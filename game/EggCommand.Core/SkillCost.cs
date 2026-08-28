#nullable enable

namespace EggCommand.Core
{
    /// <summary>スキルレベルの値段。⭐ **卵の出口はここ1つ。**
    ///
    /// ⭐ 孵化前の卵を素材として食わせるとポイントが溜まり、レベルが上がる。
    /// これが「★＝強さ」を成立させている支え ── ★5 は
    /// 「2時間待って強い個体」と「いまスキルを1段上げる」の**二択**になる。
    /// ⚠️ この出口を消すと、正典が避けた「待てば良いだけ」が戻る（<see cref="Rarities"/> 参照）。
    ///
    /// ⭐ **値段も入るポイントも同じ <see cref="Step"/> の累乗。**
    /// だから「★N の卵1個 ＝ ちょうど Lv(N−1)→LvN」が式として成り立つ。
    /// ⚠️ 表を2つ持つと、片方だけ直したときに этот 対応が黙って崩れる。
    /// </summary>
    public static class SkillCosts
    {
        /// <summary>1段ごとの倍率。⚠️ ここを 1〜2 に下げると低い★を延々入れるのが得になる。</summary>
        public const int Step = 3;

        /// <summary>Lv から Lv+1 に上げるのに要るポイント。⭐ ★(Lv+1) の卵1個ぶん。</summary>
        public static int CostOf(int level)
        {
            int cost = 1;
            for (int i = 0; i < level; i++) cost *= Step;
            return cost;
        }

        /// <summary>Lv に達するまでに積んだ総ポイント。</summary>
        public static int TotalFor(int level)
        {
            int total = 0;
            for (int lv = 1; lv < level; lv++) total += CostOf(lv);
            return total;
        }

        /// <summary>そのポイントで到達しているレベル。⭐ **導出。**レベルは保存しない。
        /// ⚠️ 🔴 2026-08-27 追記: **上限は技ごと**（<see cref="Skills.MaxLevelOf"/>）。
        /// 呼ぶ側が技を知っているなら渡すこと ── 省くと「どの技も5まで育つ」という
        /// 古い前提のまま計算する（<see cref="Skills.MaxLevel"/> は全体の天井であって、
        /// 個々の技の上限ではない）。</summary>
        public static int LevelOf(int points, int maxLevel = Skills.MaxLevel)
        {
            int level = 1;
            while (level < maxLevel && points >= TotalFor(level + 1)) level++;
            return level;
        }

        /// <summary>次の段までに、あと何ポイント要るか。⚠️ 上限なら 0。
        /// ⚠️ <paramref name="maxLevel"/> は <see cref="LevelOf"/> と同じ注意。</summary>
        public static int ToNext(int points, int maxLevel = Skills.MaxLevel)
        {
            int level = LevelOf(points, maxLevel);
            if (level >= maxLevel) return 0;
            int need = TotalFor(level + 1) - points;
            return need < 0 ? 0 : need;
        }

        /// <summary>⚠️ <paramref name="maxLevel"/> は <see cref="LevelOf"/> と同じ注意。</summary>
        public static bool IsMaxed(int points, int maxLevel = Skills.MaxLevel) =>
            LevelOf(points, maxLevel) >= maxLevel;

        /// <summary>⚠️ 値段と卵のポイントが食い違っていないか。
        /// ⭐ 守りたい約束は「★N の卵1個で Lv(N−1)→LvN がちょうど埋まる」。</summary>
        public static void Audit()
        {
            var problems = new System.Collections.Generic.List<string>();
            for (int level = 1; level < Skills.MaxLevel; level++)
            {
                int cost = CostOf(level);
                int fromEgg = Rarities.PointsOf(level + 1);
                if (cost != fromEgg)
                {
                    problems.Add(
                        $"Lv{level}→{level + 1} は {cost} 要るのに、★{level + 1} の卵は {fromEgg} しか入らない");
                }
            }
            // ⚠️ 上限を超えて溜め続けられないこと（溜めたぶんが黙って消えるのを防ぐ）
            if (LevelOf(TotalFor(Skills.MaxLevel)) != Skills.MaxLevel)
            {
                problems.Add("最大レベルに必要なポイントを積んでも最大レベルにならない");
            }

            if (problems.Count > 0)
            {
                throw new System.InvalidOperationException(
                    "スキルレベルの値段の不備:" + System.Environment.NewLine + "  " +
                    string.Join(System.Environment.NewLine + "  ", problems));
            }
        }
    }
}
