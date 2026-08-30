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
        // ⭐ 2026-08-30: 作者支給の16枚を Pixelizer で分割・ドット化した実絵。
        //    Atk/Def/Spd だけは強化・弱体で矢印の向きまで変わるので、側も名前に含める。
        private static readonly Dictionary<StatusKind, string> StatusIcons = new Dictionary<StatusKind, string>
        {
            [StatusKind.Atk] = "status-atk-up",
            [StatusKind.Def] = "status-def-up",
            [StatusKind.Spd] = "status-spd-up",
            [StatusKind.Poison] = "status-poison",
            [StatusKind.Regen] = "status-regen",
            [StatusKind.Shield] = "status-shield",
            [StatusKind.Stun] = "status-stun",
            [StatusKind.Taunt] = "status-taunt",
            [StatusKind.Guts] = "status-guts",
            [StatusKind.Immune] = "status-immune",
            [StatusKind.Sleep] = "status-sleep",
            [StatusKind.Block] = "status-block",
            // ⭐ 2026-08-27 に足した4つ
            [StatusKind.Seal] = "status-seal",
            [StatusKind.Anchor] = "status-anchor",
            [StatusKind.Invincible] = "status-invincible",
            [StatusKind.Counter] = "status-counter",
        };

        private static readonly Dictionary<StatusKind, string> NegativeStatusIcons = new Dictionary<StatusKind, string>
        {
            [StatusKind.Atk] = "status-atk-down",
            [StatusKind.Def] = "status-def-down",
            [StatusKind.Spd] = "status-spd-down",
        };

        /// <summary>状態異常 → `Resources/UI/icon/<名前>.png`。</summary>
        public static string StatusIcon(StatusKind kind)
        {
            if (StatusIcons.TryGetValue(kind, out var name)) return name;
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Art.StatusIcon に無い種類");
        }

        /// <summary>状態の種類と良悪 → 絵。攻防速の弱体だけ下向き矢印へ分ける。</summary>
        public static string StatusIcon(StatusKind kind, bool good)
        {
            if (!good && NegativeStatusIcons.TryGetValue(kind, out var negative)) return negative;
            return StatusIcon(kind);
        }

        /// <summary>⭐ **まだ仮絵**（自作のドット絵）である名前。⚠️ 作者が差し替えたら、
        /// その名前をここから外す ── `NOTICE.md` の「残り枚数」と `ArtTests` はここを数える。</summary>
        public static readonly IReadOnlyList<string> Placeholder = new List<string>
        {
            "status-anchor", "status-invincible", "status-counter",
        };

        /// <summary>⭐ **この表が指す絵、全部。**⚠️ 検査（実体があるか／死蔵か）の唯一の出所。
        ///
        /// 今後キャラクター・ボタン・背景を足すときは、専用の辞書と `StatusIcon` に当たる
        /// 引く関数を1組足したうえで、ここへ `foreach` を1つ足す。</summary>
        public static IEnumerable<Ref> All()
        {
            foreach (var pair in StatusIcons)
                yield return new Ref($"状態異常 {pair.Key}", "icon", pair.Value);
            foreach (var pair in NegativeStatusIcons)
                yield return new Ref($"状態異常 {pair.Key}（弱体）", "icon", pair.Value);
        }
    }
}
