using System;
using System.IO;
using UnityEngine;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>保存の置き場。⭐ 変換は <see cref="Snapshots"/>、文字にするのはここ。
    ///
    /// ⚠️ PlayerPrefs は使わない。中身が見えず、壊れたときに直せない。
    /// ファイルなら開いて読める（Android でも persistentDataPath は書ける）。
    /// </summary>
    public static class SaveFile
    {
        private const string Name = "egg-command.json";

        public static string Path => System.IO.Path.Combine(Application.persistentDataPath, Name);

        /// <summary>書き出す。⚠️ 直接上書きしない。
        /// 書いている途中で落ちると、遊んだ結果が丸ごと消える。</summary>
        public static void Write(Game game)
        {
            try
            {
                string json = JsonUtility.ToJson(Snapshots.Save(game));
                string temp = Path + ".tmp";
                File.WriteAllText(temp, json);
                // ⭐ 出来上がってから置き換える。落ちても前の保存が残る
                if (File.Exists(Path)) File.Delete(Path);
                File.Move(temp, Path);
            }
            catch (Exception error)
            {
                // ⚠️ 黙って諦めない。保存できていないことに気づけないほうが困る
                Debug.LogError($"保存に失敗した: {error.Message}");
            }
        }

        /// <summary>読み込む。⚠️ 無い・壊れている・版が違うなら null（新しく始める）。</summary>
        public static Game Read()
        {
            try
            {
                if (!File.Exists(Path)) return null;
                var save = JsonUtility.FromJson<GameSave>(File.ReadAllText(Path));

                // ⭐ 置き換えが起きたら残る。⚠️ 黙って別の種族になっているのが一番困る
                var notes = new System.Collections.Generic.List<string>();
                var game = Snapshots.Load(save, notes);
                if (game == null) Debug.LogWarning("保存の版が新しすぎる。作り直して始める");
                foreach (string note in notes) Debug.LogWarning($"保存の読み替え: {note}");
                return game;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"保存が読めない（作り直して始める）: {error.Message}");
                return null;
            }
        }

        public static void Erase()
        {
            try { if (File.Exists(Path)) File.Delete(Path); }
            catch (Exception error) { Debug.LogError($"保存を消せない: {error.Message}"); }
        }
    }
}
