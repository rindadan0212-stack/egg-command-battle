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

        /// <summary>属性を種族に固定していた頃の割り当て（2026-08-17 に個体側へ移した）。
        ///
        /// ⭐ いまは**属性を持たない個体・卵は存在しない**。ここが効くのは2つだけ:
        /// 1. 属性を持たない古いセーブを読むとき（その個体の見え方が変わらないように）
        /// 2. 移植元との照合（入力を移植元と同じ形に戻すため）
        ///
        /// ⚠️ 新しい種族をここへ足さない。足すと「その種族の属性」という考え方が復活する。
        /// 表に無い種族は炎（3すくみのどれかであればよく、どれでも同じ）。</summary>
        private static readonly Dictionary<string, Element> LegacyElements = new Dictionary<string, Element>
        {
            { "tamaru", Element.Water },
            { "tsunoga", Element.Fire },
            { "haneru", Element.Wood },
            { "nushi", Element.Water },
        };

        public static Element ElementOf(string speciesId)
        {
            Element element;
            return LegacyElements.TryGetValue(speciesId, out element) ? element : Element.Fire;
        }

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
