using UnityEngine;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>Core が持つ添字色のドット絵を、Unity の絵に変える唯一の場所。
    ///
    /// ⭐ 向きはここだけが知っていればいい。Core は「上から下」で持ち、
    /// Unity のテクスチャは「下から上」。両方を知る場所を1つに閉じ込めておかないと、
    /// 上下反転が使う側のあちこちに散らばる。
    ///
    /// ⚠️ 補間を通さない（FilterMode.Point）。ドット絵を引き伸ばすと縁がガタつく。
    /// </summary>
    public static class PixelSpriteTexture
    {
        private static readonly Color32 Transparent = new Color32(0, 0, 0, 0);

        public static Texture2D ToTexture(PixelSprite sprite, Palette palette)
        {
            var texture = new Texture2D(sprite.Width, sprite.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[sprite.Width * sprite.Height];
            for (int y = 0; y < sprite.Height; y++)
            {
                for (int x = 0; x < sprite.Width; x++)
                {
                    byte index = sprite.At(x, y);
                    // ⚠️ Core は上から下、Unity は下から上。ここで1度だけひっくり返す
                    int destination = (sprite.Height - 1 - y) * sprite.Width + x;
                    pixels[destination] = index == 0 ? Transparent : ParseHex(palette.ColorOf(index));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>そのまま貼れる形で返す。
        /// ⚠️ <paramref name="pixelsPerUnit"/> に絵の幅を渡すと、1体ぶんが world の 1 単位になる。
        /// ワールド空間に置くときは、そこから実寸へ scale で伸ばすと寸法が読みやすい。</summary>
        public static Sprite ToSprite(PixelSprite pixelSprite, Palette palette, float pixelsPerUnit = 16f)
        {
            var texture = ToTexture(pixelSprite, palette);
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit,
                extrude: 0,
                meshType: SpriteMeshType.FullRect);
        }

        /// <summary>"#rrggbb" を読む。⚠️ 読めないものを黙って黒にしない。</summary>
        private static Color32 ParseHex(string hex)
        {
            Color color;
            if (!ColorUtility.TryParseHtmlString(hex, out color))
            {
                throw new System.ArgumentException($"色として読めない: {hex}");
            }
            return color;
        }
    }
}
