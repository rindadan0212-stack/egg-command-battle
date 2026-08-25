using System.Collections.Generic;
using System.Text.Json;
using EggCommand.Core;

namespace EggCommand.Sim
{

    /// <summary>保存を文字にする／文字から戻す。
    ///
    /// ⚠️ **Core には置けない。**Core はエンジンに触らない約束で、Unity の実行時プロファイルに
    /// `System.Text.Json` が無い。⭐ だから Unity 側は `JsonUtility`、こちら側はここ。
    ///
    /// ⚠️ 🔴 **形は Unity の `JsonUtility` に合わせる。**⭐ 同じファイルを両方が読み書きできないと、
    /// 「web で遊んだ続きを Unity で開く」も「いまの保存を web へ持ち込む」も成り立たない。
    /// 合わせるための決めごとは2つだけ:
    ///
    /// | | |
    /// |---|---|
    /// | **欄で持つ**（`IncludeFields`）| `JsonUtility` は public 欄しか見ない。⭐ `GameSave` も欄だけで書いてある |
    /// | **欄名をいじらない** | 名前の付け替え（camelCase 等）をすると、同じ物が別名になる |
    ///
    /// ⚠️ **変換の道具を書かない**（計画 §6）── いまのファイルを**そのまま**読めることが合格条件。
    /// ⭐ 実物（`records/save-unity.json`）を読む検査が `SaveJsonTests` に在る。
    ///
    /// ⚠️ 唯一の残差は **null と ""**: `JsonUtility` は null 文字列を書けず `""` にする。
    /// ⭐ 読む側（`Snapshots.ResolveSkill`）が既に両方を「無い」として扱う。</summary>
    public static class SaveJson
    {
        private static readonly JsonSerializerOptions Shape = new()
        {
            // ⭐ `GameSave` は**欄だけ**で書いてある（`JsonUtility` がそれしか見ないため）
            IncludeFields = true,
            // ⚠️ 名前を付け替えない。⭐ 既に在る保存と同じ綴りでなければ読めない
            PropertyNamingPolicy = null,
            WriteIndented = false,
        };

        public static string Write(Game game) =>
            JsonSerializer.Serialize(Snapshots.Save(game), Shape);

        /// <summary>⚠️ 壊れていたら投げる。⭐ 呼ぶ側が「無い」と「壊れている」を区別できるように。</summary>
        public static GameSave? Parse(string json) =>
            JsonSerializer.Deserialize<GameSave>(json, Shape);

        /// <summary>⚠️ 版が新しすぎるときは null（**上書きしない**ための合図）。</summary>
        public static Game? Read(string json, List<string>? notes = null) =>
            Snapshots.Load(Parse(json), notes);
    }
}
