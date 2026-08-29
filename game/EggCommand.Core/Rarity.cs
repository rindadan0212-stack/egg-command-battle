#nullable enable
using System;

namespace EggCommand.Core
{
    /// <summary>卵の希少さ。★1〜★5。⭐ **見る数字はこれ1つ。**
    ///
    /// ⭐ ★が決めるのは **孵るまでの時間** と **孵る個体の素質**
    /// （<see cref="Nests.WildTotalForRarity"/>）。★が高い＝強い個体が出る。
    ///
    /// ⚠️ **以前は「★は時間だけ。強さは決めない」と決めていた。**理由は
    /// 「長く待った卵が必ず強いなら、どれを孵化器に入れるかの選択が消える（待てば良いだけ）」。
    ///
    /// ⭐ **覆せる理由: 卵に「孵さない使い道」ができた。**
    /// 孵化前の卵を強化素材として使えるので、★5は
    /// 「2時間待って強い個体」と「いま強化に使う」の二択になる。選択は消えるどころか生まれる。
    ///
    /// ⚠️ **素材の出口が実装されるまで、この二択は成立していない。**
    /// 出口を消す／作らないまま★＝強さだけを残すと、元の問題がそのまま戻る。
    /// 🚧 強化先（スキル強化）は未実装。
    /// </summary>
    public static class Rarities
    {
        public const int Max = 5;

        /// <summary>孵るまでの秒。⭐ 上ほど跳ね上げる（★5は「今日は寝かせる」の重さ）。</summary>
        public static int SecondsOf(int rarity)
        {
            switch (Clamp(rarity))
            {
                case 1: return 30;
                case 2: return 120;
                case 3: return 600;
                case 4: return 1800;
                default: return 7200;
            }
        }

        /// <summary>★を並べた文字列。⚠️ 「レア度3」と数で書かない（印のほうが速い）。</summary>
        public static string StarsOf(int rarity) => new string('★', Clamp(rarity));

        public static int Clamp(int rarity) => rarity < 1 ? 1 : rarity > Max ? Max : rarity;

        /// <summary>その卵を素材にしたとき入るスキルポイント。⭐ **3の累乗**（1/3/9/27/81）。
        ///
        /// ⭐ この形だと「★N の卵1個 ＝ ちょうど Lv(N−1)→LvN」になり、説明が1行で済む。
        /// 1つ下の★なら3個、2つ下なら9個。
        ///
        /// ⚠️ **直線（★N＝Nポイント）にしない。**★1が★5の 1/5 でしかないと、
        /// 低い★を延々と入れるほうが得になり「時間さえかければ埋まる」形に戻る
        /// （<see cref="Creatures.TrainMax"/> が上限で塞いでいるのと同じ問題）。
        /// ⭐ 累乗なら ★1 で Lv4→5 は 81個。使い道は残るが、誰も選ばない。</summary>
        public static int PointsOf(int rarity)
        {
            int points = 1;
            for (int i = 1; i < Clamp(rarity); i++) points *= SkillCosts.Step;
            return points;
        }

        /// <summary>巣の段階から希少さを引く。
        /// ⭐ 段階が中心。上下に1だけ振れるので、浅い巣でも稀に★3が出る。
        ///
        /// ⚠️ **入手経路で差を付けない**（2026-08-17 決定）。
        /// 以前は盗んだ卵を1段下げていたが、⭐ **盗むこと自体に既にコストがある** ──
        /// 同じ巣は盗むたびに関門が増えて隙間が狭まり、4回で潜入できなくなる
        /// （<see cref="Steal.RaidsToSeal"/>）。★も下げると**同じことで二重に罰する**ことになる。
        /// ⚠️ <paramref name="how"/> は記録として残すが、希少さには効かせない。</summary>
        public static int Roll(Rng rng, int tier, EggOrigin how)
        {
            int center = tier < 1 ? 1 : tier > Max ? Max : tier;
            int shift = rng.Int(-1, 2);          // -1, 0, +1
            return Clamp(center + shift);
        }

        /// <summary>時間の見せ方。⚠️ 秒をそのまま出さない（3600 は読めない）。
        ///
        /// ⭐ **〇h〇m〇s。0 の単位は出さない**（作者の指示 2026-08-28）。
        /// ⚠️ **h があるときは s を省略する**（`2h30m15s` ではなく `2h30m`）
        /// ── 秒まで出すと、待つ人には無意味な精度（★5は2時間、1秒単位で見せる意味が無い）。
        /// ⭐ h が無いときだけ s まで出す（`59m59s`／`45s`）── そこは秒が主役の長さ。</summary>
        public static string Clock(int seconds)
        {
            if (seconds < 0) seconds = 0;
            var span = TimeSpan.FromSeconds(seconds);
            int h = (int)span.TotalHours;
            int m = span.Minutes;
            int s = span.Seconds;

            if (h > 0) return m > 0 ? $"{h}h{m}m" : $"{h}h";
            if (m > 0) return s > 0 ? $"{m}m{s}s" : $"{m}m";
            return $"{s}s";
        }
    }
}
