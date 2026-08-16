#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>id の引っ越し表。
    ///
    /// ⭐ **種族と技の id は永久**。改名も削除もしない、が原則。
    /// セーブ（<see cref="CreatureSave"/> など）には id が**生の文字**で入っているので、
    /// 消した id を持つ個体は行き場を失う。
    ///
    /// ⚠️ 怖いのは「壊れ方が見えない」こと。壊れるのは**その個体を持っている人のセーブだけ**なので、
    /// 手元の新規プレイでは何も起きない。気づくのは出荷したあとになる。
    ///
    /// どうしても名前を変えたいときだけ、ここに 旧 → 新 を1行足す。
    /// ⚠️ **元の行を消さない。** 消すと、その版を跨いでいないセーブが読めなくなる。
    /// 表が長くなるのは正常で、短く保とうとしてはいけない。
    /// </summary>
    public static class Migrations
    {
        /// <summary>種族の 旧 → 新。⚠️ 行を消さない。</summary>
        private static readonly Dictionary<string, string> SpeciesIds = new Dictionary<string, string>
        {
            // 例: { "tamaru-old", "tamaru" },
        };

        /// <summary>技の 旧 → 新。⚠️ 行を消さない。</summary>
        private static readonly Dictionary<string, string> SkillIds = new Dictionary<string, string>
        {
        };

        /// <summary>辿る上限。⚠️ 輪（a→b→a）は書き間違いなので投げる。</summary>
        public const int MaxHops = 8;

        public static string SpeciesOf(string id) => Apply(SpeciesIds, id);

        public static string SkillOf(string id) => Apply(SkillIds, id);

        /// <summary>⭐ 何段でも辿る（a→b→c）。表に無ければそのまま返す。
        /// ⚠️ 表そのものを渡せるようにしてあるのは、**輪と多段を検査で踏むため**。
        /// 仕組みだけ作って一度も通していない状態にしない。</summary>
        public static string Apply(IReadOnlyDictionary<string, string> table, string id)
        {
            string current = id;
            for (int hop = 0; hop <= MaxHops; hop++)
            {
                string? next;
                if (!table.TryGetValue(current, out next)) return current;
                current = next!;
            }
            throw new InvalidOperationException($"id の引っ越し表が輪になっている: {id}");
        }
    }
}
