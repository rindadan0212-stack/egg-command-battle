using System;
using System.Collections.Generic;
using EggCommand.Core;

namespace EggCommand.Web;

/// <summary>1秒ごとに差し替える時計の字1つぶん。⚠️ Blazor が JSON にして JS へ渡す ──
/// **プロパティ名は camelCase で届く**（`At` ではなく `at`）。`tap.js` 側はそちらを読む。</summary>
public readonly record struct Word(string At, string Text, string? Tint);

/// <summary>画面に出ている時計の字の、唯一の出所。
///
/// ⭐ **`Incubator.Draw`（孵化器の卵）と `Sheets.Wilds`（巣）が、以前はそれぞれ
/// 独立に「残り秒 → 字・色」を組み立てていた。**1秒ごとの差し替え（`Words`）を
/// 素直に足すと、判断の出所が3か所目になるところだった ── 🔴 同じ判断を2か所に
/// 書かないため、先にここへ1本化してから、差し替えもここから作る。
///
/// ⚠️ 巣の「残り秒」「割合」は、減っていく帯（`card-track`）の判断とも同じ ──
/// そちらも `NestLeftOf`/`NestRatioOf` を読む（`Sheets.Wilds` 側で二重に計算しない）。</summary>
public static class Clocks
{
    /// <summary>たまごの残り時間の字。⭐ 孵っていれば「孵った」。</summary>
    public static string EggText(Shell s, Incubation? egg) => egg == null ? ""
        : Hatchery.IsReady(egg, s.Now) ? "孵った" : Rarities.Clock(Hatchery.LeftOf(egg, s.Now));

    /// <summary>その字の色。⚠️ 孵ったら緑（既存の #8ef06a／#ffffff をそのまま使う）。</summary>
    public static string EggTint(Shell s, Incubation? egg) =>
        egg != null && Hatchery.IsReady(egg, s.Now) ? "#8ef06a" : "#ffffff";

    /// <summary>巣の残り秒。⚠️ 期限を持たない巣（時刻を渡さずに始めた保存）は null
    /// ── 0 を「もう切れた」と読まない（`Sheets.Wilds` の元の注記と同じ理由）。</summary>
    public static int? NestLeftOf(Shell s, int i)
    {
        var e = s.Game.Encounters[i];
        return e.UntilUnix <= 0 ? null : Encounters.LeftOf(e, s.Now);
    }

    /// <summary>0（消える）〜1（出たて）。⭐ 帯（`card-track`）と、字を赤くする閾値の両方がここを読む。</summary>
    public static double NestRatioOf(Shell s, int i)
    {
        if (NestLeftOf(s, i) is not int left) return 0;
        int whole = Encounters.SecondsFor(s.Game.Encounters[i].Nest.Tier);
        return whole <= 0 ? 0 : Math.Clamp(left / (double)whole, 0, 1);
    }

    /// <summary>巣の残り時間の字。</summary>
    public static string NestText(Shell s, int i) =>
        NestLeftOf(s, i) is int n ? Rarities.Clock(n) : "";

    /// <summary>巣の残り時間の色。⭐ 残りがこの割合を切ったら赤くする（数字を読ませずに急かす）。</summary>
    public static string? NestTint(Shell s, int i) =>
        NestRatioOf(s, i) <= 0.25 ? "#c0303f" : null;

    /// <summary>いま画面に出ている時計ぜんぶ。⭐ 1秒ごとの差し替え（`eggTap.words`）専用の口。
    ///
    /// ⚠️ **ホームならたまごだけ・探索なら巣だけ**を返す（無い id を送っても JS 側で
    /// 無害だが、無駄を出さない）。呼ぶ側（`AppPage.BeatIdle`）が既に「札・戦闘・
    /// すごろく・演出が出ていない」を確かめてから呼ぶので、ここでは Now_Sheet だけ見る。</summary>
    public static Word[] Words(Shell s)
    {
        if (s.Now_Sheet == Sheet.Home)
        {
            var words = new List<Word>();
            for (int i = 0; i < Hatchery.Slots; i++)
            {
                var egg = Hatchery.At(s.Game, i);
                if (egg == null) continue;   // ⚠️ 空き枠には時計が無い（送っても無駄）
                words.Add(new Word("clock#" + i, EggText(s, egg), EggTint(s, egg)));
            }
            return words.ToArray();
        }
        if (s.Now_Sheet == Sheet.Nests)
        {
            var words = new Word[s.Game.Encounters.Count];
            for (int i = 0; i < words.Length; i++)
                words[i] = new Word("card-left#" + i, NestText(s, i), NestTint(s, i));
            return words;
        }
        return Array.Empty<Word>();
    }
}
