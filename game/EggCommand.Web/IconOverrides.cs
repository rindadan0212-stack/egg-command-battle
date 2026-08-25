using System;
using System.Collections.Generic;

namespace EggCommand.Web
{
    /// <summary>骨組みエディタ（`/edit`）専用: `IconManifest`（ビルド時の埋め込み一覧）に
    /// まだ無い絵の名前 → data URL の対応表（段E ── 自作 PNG を建て直さずにその場で使う）。
    ///
    /// ⭐ **`LayoutStore` と同じ静的な形**（`LayoutDom` から見える必要があり、
    /// インスタンスを引き回さないため）。⚠️ だからこそ `LayoutStore` と同じ理由で、
    /// **頁を離れるとき必ず `Clear()` する**（`EditPage.ClearOverrides` の中 ──
    /// `Dispose`/`RegisterLocationChangingHandler` の両方がそこを通る）。
    /// ⚠️ 遊ぶ画面（`/app`）はこの表を一切呼ばない ── `LayoutDom` が空のときの
    /// 振る舞いを今までと1バイトも変えないための約束（`TryGet` が false を返すだけ）。
    ///
    /// ⭐ 登録するのは「まだビルドに入っていない絵」だけ（呼び出し側 = `EditPage` が
    /// `IconManifest.Exists` で絞る）── 既にある28枚を base64 で二重に抱えない。</summary>
    public static class IconOverrides
    {
        private static readonly Dictionary<string, string> _map = new(StringComparer.Ordinal);

        /// <summary>登録済みの名前（「絵を選ぶ」小窓の一覧に、まだ在るゲームの絵と
        /// 合わせて出すため）。</summary>
        public static IEnumerable<string> Names => _map.Keys;

        /// <summary>⭐ 絵のフォルダから読んだ data URL を登録する（同じ名前なら差し替え）。</summary>
        public static void Set(string name, string dataUrl) => _map[name] = dataUrl;

        /// <summary>⚠️ `LayoutDom` がここを通るたび呼ぶ ── 見つからなければ、今までどおり
        /// `IconManifest.Exists` → `icon-missing`（「？」）の道へ落ちる。</summary>
        public static bool TryGet(string name, out string? dataUrl) => _map.TryGetValue(name, out dataUrl);

        /// <summary>🔴 頁を離れるとき必ず呼ぶ（`EditPage.ClearOverrides`）。</summary>
        public static void Clear() => _map.Clear();
    }
}
