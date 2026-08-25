using System.IO;
using UnityEngine;
using UnityEditor;

namespace EggCommand.EditorTools
{
    /// <summary>画面を1枚 PNG に落とす。
    ///
    /// ⚠️ Unity MCP の screenshot は**このプロジェクトでは真っ白になる**（2026-08-16 実測）。
    /// カメラ指定の撮影経路に乗ると Screen Space - Overlay の Canvas が写らず、
    /// カメラ経由でも中身が出てこなかった。ここでカメラを直接描かせて読み戻す。
    ///
    /// ⭐ 「完成＝ユーザーが見たとき」なので、見せる手段は壊れたままにしない。
    /// </summary>
    public static class Capture
    {
        private const int Width = 1080;
        private const int Height = 1920;

        // ⚠️ %#s（Ctrl+Shift+S）は Unity 標準の「名前を付けて保存」。奪うと、
        //    保存のつもりで押した人が撮影＋エクスプローラ起動を食らう。
        //    ⭐ 近い打ち方のまま1つずらす（Ctrl+Alt+Shift+S）
        [MenuItem("Egg Command/画面を1枚撮る %#&s")]
        public static void Shot()
        {
            string path = Save(null);
            Debug.Log($"撮った: {path}");
            EditorUtility.RevealInFinder(path);
        }

        /// <summary>撮って PNG のパスを返す。MCP からも呼べるように static にしてある。</summary>
        public static string Save(string fileName)
        {
            var camera = Camera.main;
            if (camera == null) throw new System.InvalidOperationException("Main Camera が無い");

            var renderTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;

            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;

            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
            texture.Apply();

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;

            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
            Directory.CreateDirectory(directory);
            string name = string.IsNullOrEmpty(fileName) ? "shot.png" : fileName;
            string path = Path.Combine(directory, name);
            File.WriteAllBytes(path, texture.EncodeToPNG());

            Object.DestroyImmediate(texture);
            renderTexture.Release();
            Object.DestroyImmediate(renderTexture);
            return path;
        }

        /// <summary>撮ったうえで「何色あるか」を返す。
        /// ⭐ ドット絵が補間されていないかは、見た目でなくここで判る
        /// （点でしか塗っていなければ、パレットの色数＋背景しか出ない）。</summary>
        public static string CountColors(int x, int y, int size)
        {
            var camera = Camera.main;
            var renderTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
            texture.Apply();
            camera.targetTexture = null;
            RenderTexture.active = null;

            var seen = new System.Collections.Generic.Dictionary<int, int>();
            for (int py = y; py < y + size; py++)
            {
                for (int px = x; px < x + size; px++)
                {
                    var c = texture.GetPixel(px, py);
                    int key = (Mathf.RoundToInt(c.r * 255) << 16)
                            | (Mathf.RoundToInt(c.g * 255) << 8)
                            | Mathf.RoundToInt(c.b * 255);
                    if (!seen.ContainsKey(key)) seen[key] = 0;
                    seen[key]++;
                }
            }

            Object.DestroyImmediate(texture);
            renderTexture.Release();
            Object.DestroyImmediate(renderTexture);
            return $"色数={seen.Count}";
        }
    }
}
