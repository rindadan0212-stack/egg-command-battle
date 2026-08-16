using System.Collections.Generic;
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
            var key = new Key(pixelSprite, palette, pixelsPerUnit);

            Sprite cached;
            // ⚠️ Unity の == は「破棄済み」も null と答える。再生を抜けたあとの残骸を掴まないように、
            //    在るかどうかではなく**生きているか**で見る
            if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

            var texture = ToTexture(pixelSprite, palette);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit,
                extrude: 0,
                meshType: SpriteMeshType.FullRect);

            _cache[key] = sprite;
            return sprite;
        }

        /// <summary>作った絵を取っておく場所。
        ///
        /// ⭐ 絵の**種類**は有限（種族数 × パレット数）。個体ごとではなく種類ごとに持てば、
        /// 保管庫に何体いても絵は数十枚で足りる。
        ///
        /// ⚠️ ここが無かったので、BOX や配合を開くたびに升のぶんだけ
        /// Texture2D を作っては捨てていた（保管枠は50）。
        /// Unity の Texture2D は GC 任せで消えないので、開くほど積み上がる。
        /// 種族が増えるほど効いてくる場所。</summary>
        private static readonly Dictionary<Key, Sprite> _cache = new Dictionary<Key, Sprite>();

        /// <summary>⚠️ ドット絵とパレットは表が持つ**同じ実体**を回してくるので、
        /// 中身ではなく参照で照らし合わせてよい（16×16 の中身を毎回比べない）。</summary>
        private readonly struct Key : System.IEquatable<Key>
        {
            private readonly PixelSprite _sprite;
            private readonly Palette _palette;
            private readonly float _pixelsPerUnit;

            public Key(PixelSprite sprite, Palette palette, float pixelsPerUnit)
            {
                _sprite = sprite;
                _palette = palette;
                _pixelsPerUnit = pixelsPerUnit;
            }

            public bool Equals(Key other) =>
                ReferenceEquals(_sprite, other._sprite)
                && ReferenceEquals(_palette, other._palette)
                && _pixelsPerUnit == other._pixelsPerUnit;

            public override bool Equals(object obj) => obj is Key key && Equals(key);

            public override int GetHashCode() =>
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_sprite) * 397
                ^ System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_palette)
                ^ _pixelsPerUnit.GetHashCode();
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
