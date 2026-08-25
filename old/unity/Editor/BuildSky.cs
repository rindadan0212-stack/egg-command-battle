using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace EggCommand.EditorTools
{
    /// <summary>地の絵（空→砂）を PNG に書き出す。
    ///
    /// ⭐ 出所は移植元の src/style.css `.phone[data-sky=...]`。
    /// 数字を発明していない。CSS の linear-gradient の停止位置をそのまま写す。
    ///
    /// ⚠️ 走らせるのは色を変えたいときだけ。出来た PNG は普通のアセットなので、
    /// 気に入らなければ Editor で差し替えても、絵で描き直してもよい。
    /// </summary>
    public static class BuildSky
    {
        private const string Dir = "Assets/Resources/UI";
        private const int Height = 512;

        /// <summary>(位置 0..1, 色) の並び。CSS の停止位置と同じ。</summary>
        private static readonly Dictionary<string, (float, string)[]> Skies = new Dictionary<string, (float, string)[]>
        {
            { "sky-home",   new[] { (0f, "8fd8f7"), (0.44f, "bdebff"), (1f, "ffe7b8") } },
            { "sky-nest",   new[] { (0f, "bdebff"), (0.62f, "dff6e4"), (1f, "ffe7b8") } },
            { "sky-battle", new[] { (0f, "a8e4f7"), (0.40f, "cff0ff"), (0.62f, "ffe7b8"), (1f, "ffe7b8") } },
            { "sky-hatch",  new[] { (0f, "ffe7b8"), (0.46f, "fff3d6"), (1f, "dff1fa") } },
            { "sky-breed",  new[] { (0f, "efe6ff"), (1f, "efe6ff") } },
            { "sky-box",    new[] { (0f, "dff1fa"), (1f, "dff1fa") } },
        };

        [MenuItem("Egg Command/地の絵を書き出す")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Resources", "UI");

            foreach (var pair in Skies)
            {
                var stops = pair.Value;
                // ⚠️ 横1px だと Unity の圧縮で縞が出る。4px 幅にして圧縮も切る
                var tex = new Texture2D(4, Height, TextureFormat.RGBA32, false);
                for (int y = 0; y < Height; y++)
                {
                    // ⚠️ CSS は上が 0。テクスチャは下が 0。ここで反転する
                    float t = 1f - (y / (float)(Height - 1));
                    var color = Sample(stops, t);
                    for (int x = 0; x < 4; x++) tex.SetPixel(x, y, color);
                }
                tex.Apply();

                string path = $"{Dir}/{pair.Key}.png";
                // ⚠️ **既にある絵は上書きしない。**この道具は下敷きを1度作るためのもので、
                //    描き直したあとに走らせると手で描いた空が一瞬で戻っていた。
                //    ⭐ 他の書き出し道具（BuildScreenPrefabs など）と同じ約束に揃える
                if (System.IO.File.Exists(path))
                {
                    Object.DestroyImmediate(tex);
                    continue;
                }
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);

                var im = (TextureImporter)AssetImporter.GetAtPath(path);
                im.textureType = TextureImporterType.Sprite;
                im.spriteImportMode = SpriteImportMode.Single;
                im.filterMode = FilterMode.Bilinear;   // ⚠️ Point だと段になる
                im.wrapMode = TextureWrapMode.Clamp;   // ⚠️ 端が反対側の色を拾わないように
                im.mipmapEnabled = false;
                im.textureCompression = TextureImporterCompression.Uncompressed;
                im.SaveAndReimport();
            }
            AssetDatabase.Refresh();
            Debug.Log($"地の絵を {Skies.Count} 枚 書き出した: {Dir}/sky-*.png");
        }

        /// <summary>CSS の linear-gradient と同じ補間（停止点のあいだを直線で結ぶ）。</summary>
        private static Color Sample((float, string)[] stops, float t)
        {
            for (int i = 0; i < stops.Length - 1; i++)
            {
                var (a, ca) = stops[i];
                var (b, cb) = stops[i + 1];
                if (t > b) continue;
                float k = b <= a ? 0f : Mathf.InverseLerp(a, b, t);
                return Color.Lerp(Hex(ca), Hex(cb), k);
            }
            return Hex(stops[stops.Length - 1].Item2);
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
