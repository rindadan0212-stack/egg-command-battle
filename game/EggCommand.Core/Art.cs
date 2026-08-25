using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>絵の割り当て表。⭐ **「遊びの概念 → 絵の名前」の唯一の出所。**
    ///
    /// ⚠️ ここは**名前だけ**を持つ（`Resources/UI/<folder>/<name>.png`）。
    /// 実体が在るか・誰にも指されていないか（死蔵）は、ここを読んで検査する側
    /// （`EggCommand.Tests/ArtTests.cs`）の仕事 ── 表自体は File I/O を持たない
    /// （Core はエンジンからも Web からも同じものを読めることが前提のため）。
    ///
    /// ⭐ **今後、種族の立ち絵・押しどころの役割・画面の空なども、同じ形でここに足す。**
    /// 足し方: ①辞書を1つ増やす ②`All()` の列挙へ足す。これで検査（存在・死蔵）が
    /// 自動的に新顔も見るようになる ── 個別に検査を書き直さなくてよい。
    ///
    /// ⚠️ **描き方（PNG か CSS か）はここでは決めない。**Web が CSS で描いている
    /// panel / button / 空は、今回この表に載せていない ── 載せる/切り替えるのは別の段。</summary>
    public static class Art
    {
        /// <summary>表の1行。⭐ 検査用に「どの概念の、どこの、どの名前か」を持たせてある。</summary>
        public readonly struct Ref
        {
            /// <summary>何のための絵か（検査が落ちたときのログにだけ使う）。</summary>
            public readonly string Concept;
            /// <summary>`Resources/UI/` の下のどこ（例: "icon"）。</summary>
            public readonly string Folder;
            /// <summary>ファイル名（拡張子なし）。</summary>
            public readonly string Name;

            public Ref(string concept, string folder, string name)
            {
                Concept = concept;
                Folder = folder;
                Name = name;
            }
        }

        // ── 状態異常 ──────────────────────────────────
        //
        // ⭐ 応急のドット絵（自作・仮）。⚠️ 差し替え予定は
        //    `Resources/UI/NOTICE.md` の「自作の仮」節と `Placeholder` に載せてある。
        //    作り直す道具は `tools/gen-status-icons.mjs`。
        private static readonly Dictionary<StatusKind, string> StatusIcons = new Dictionary<StatusKind, string>
        {
            [StatusKind.Atk] = "status-atk",
            [StatusKind.Def] = "status-def",
            [StatusKind.Spd] = "status-spd",
            [StatusKind.Poison] = "status-poison",
            [StatusKind.Regen] = "status-regen",
            [StatusKind.Shield] = "status-shield",
            [StatusKind.Stun] = "status-stun",
            [StatusKind.Taunt] = "status-taunt",
            [StatusKind.Guts] = "status-guts",
            [StatusKind.Immune] = "status-immune",
            [StatusKind.Sleep] = "status-sleep",
            [StatusKind.Block] = "status-block",
        };

        /// <summary>状態異常 → `Resources/UI/icon/<名前>.png`。</summary>
        public static string StatusIcon(StatusKind kind)
        {
            if (StatusIcons.TryGetValue(kind, out var name)) return name;
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Art.StatusIcon に無い種類");
        }

        /// <summary>⭐ **まだ仮絵**（自作のドット絵）である名前。⚠️ 作者が差し替えたら、
        /// その名前をここから外す ── `NOTICE.md` の「残り枚数」と `ArtTests` はここを数える。</summary>
        public static readonly IReadOnlyList<string> Placeholder = new List<string>(StatusIcons.Values);

        /// <summary>⭐ **この表が指す絵、全部。**⚠️ 検査（実体があるか／死蔵か）の唯一の出所。
        ///
        /// 今後キャラクター・ボタン・背景を足すときは、専用の辞書と `StatusIcon` に当たる
        /// 引く関数を1組足したうえで、ここへ `foreach` を1つ足す。</summary>
        public static IEnumerable<Ref> All()
        {
            foreach (var pair in StatusIcons)
                yield return new Ref($"状態異常 {pair.Key}", "icon", pair.Value);
        }
    }
}
