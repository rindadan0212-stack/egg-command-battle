#nullable enable
using System;

namespace EggCommand.Core
{
    /// <summary>卵の希少さ。★1〜★5。
    ///
    /// ⭐ 希少さが決めるのは**孵るまでの時間だけ**。強さは決めない。
    /// ⚠️ 強さと結び付けると「長く待った卵が必ず強い」になり、
    /// どれを孵化器に入れるかという選択が消える（待てば良いだけになる）。
    /// 素質は <see cref="Egg.Wild"/> が別に持っていて、こちらは巣の段階で決まる。
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

        /// <summary>巣の段階から希少さを引く。
        /// ⭐ 段階が中心。上下に1だけ振れるので、浅い巣でも稀に★3が出る。
        /// ⚠️ 盗んだ卵は1段下がる（倒したほうが良い、という約束をここでも守る）。</summary>
        public static int Roll(Rng rng, int tier, EggOrigin how)
        {
            int center = tier < 1 ? 1 : tier > Max ? Max : tier;
            int shift = rng.Int(-1, 2);          // -1, 0, +1
            if (how == EggOrigin.Stolen) shift -= 1;
            return Clamp(center + shift);
        }

        /// <summary>時間の見せ方。⚠️ 秒をそのまま出さない（3600 は読めない）。</summary>
        public static string Clock(int seconds)
        {
            if (seconds < 0) seconds = 0;
            var span = TimeSpan.FromSeconds(seconds);
            return span.TotalHours >= 1.0
                ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
                : $"{span.Minutes:00}:{span.Seconds:00}";
        }
    }
}
