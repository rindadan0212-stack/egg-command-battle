using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using EggCommand.Core;

namespace EggCommand.Sim
{
    /// <summary>ドット絵を**インデックスカラー PNG** で書き出す。
    ///
    /// ⭐ **なぜインデックスカラーか**（2026-08-22・作者の決定「PNG を正典に戻す」）:
    /// 変異＝パレットスワップなので、⚠️ RGBA で書くと**添字が失われて変異色が作れなくなる**。
    /// ⭐ Aseprite は添字色モードが一級なので、画素もパレットもそのまま編集できる。
    ///
    /// ⚠️ **外部の画像ライブラリを足さない。**PNG は
    /// 署名＋IHDR＋PLTE＋tRNS＋IDAT＋IEND の6つで書けて、
    /// 圧縮は `System.IO.Compression` の Deflate で足りる。
    /// ⭐ 依存を1つも増やさずに済む（この作品の決まり）。
    ///
    /// ⚠️ 添字0は必ず透明（<see cref="PixelSprite"/> の決まり）。tRNS で 0 番だけ alpha=0 にする。</summary>
    public static class SpritePng
    {
        /// <summary>書き出し先。⭐ **エンジンに紐づかない場所**へ置く
        /// （`unity/Assets` に置くと、Unity を出るときに一緒に消える）。
        /// ⚠️ ここは **Aseprite で描くための正典**（0番のパレットだけ）。
        /// 遊びが表示する絵は <see cref="DisplayDir"/>（別の置き場・別の役目）。</summary>
        public const string Dir = "art/sprites";

        /// <summary>⭐ **遊びが表示する絵の置き場**（作者の指示 2026-08-23
        /// 「あらかじめ差し替えた PNG をゲームに表示させる」）。
        ///
        /// ⚠️ 上の <see cref="Dir"/> と役目が違う ── そちらは0番（ふつう色）1枚だけの
        /// 「編集用の正典」、こちらは **(種族 × パレット) の全通り**を焼いた「表示用」。
        /// ⭐ ファイル名は <see cref="DisplayFileName"/> が唯一の出所
        /// （焼く側＝ここと、読む側＝Web の `SpriteManifest` が、別々に組み立てて
        /// ずれる事故を避ける）。</summary>
        public const string DisplayDir = "art/sprites/display";

        /// <summary>種族id・パレット番号 → 表示用 PNG のファイル名（拡張子つき）。
        /// ⚠️ **焼く側と読む側の両方がここを通す**（別々に文字列を組み立てない）。</summary>
        public static string DisplayFileName(string speciesId, int paletteIndex) =>
            $"{speciesId}-{paletteIndex}.png";

        public static void Run(string root)
        {
            var outDir = Path.Combine(root, Dir);
            Directory.CreateDirectory(outDir);

            int made = 0;
            var notes = new List<string>();

            foreach (var species in SpeciesTable.All)
            {
                // ⭐ 正典は**0番のパレット**（ふつうの色）。変異色は Core が持ち続ける
                //    ── 同じ絵に別の色を掛けるのが変異なので、絵は1枚でよい。
                var path = Path.Combine(outDir, species.Id + ".png");
                File.WriteAllBytes(path, Encode(species.Sprite, species.Palettes[0]));
                made++;
                notes.Add($"  {species.Id,-9} {species.Sprite.Width}x{species.Sprite.Height}"
                    + $"  色 {species.Palettes[0].Count}  変異 {species.Palettes.Count - 1} 通り"
                    + $"  {new FileInfo(path).Length} バイト");
            }

            // ⚠️ 卵の絵は **死蔵ファイル `StealStage.cs` の中**に埋まっている（2026-08-22 の棚卸し）。
            //    ⭐ Core に居ないのでここからは書き出せない。先に Core へ移すこと。
            notes.Add("  ⚠️ 卵（EggArt）は View 側（StealStage.cs）に在るので未書き出し");

            Console.WriteLine();
            Console.WriteLine($"■ ドット絵を PNG に書き出した: {made} 枚 → {Dir}/");
            foreach (var line in notes) Console.WriteLine(line);

            RunDisplay(root);

            // ⭐ 変異色を人が読める形で添える。⚠️ PNG には0番のパレットしか入らないので、
            //    ここが無いと「変異色がどこへ行ったか」が分からなくなる。
            var swatch = new StringBuilder();
            swatch.Append("# 変異色（パレット差し替え）\n");
            swatch.Append("# ⚠️ PNG が持つのは 0番（ふつう）だけ。1番以降がここ。\n");
            swatch.Append("# ⭐ 添字は PNG のパレットの並びと同じ。\n\n");
            foreach (var species in SpeciesTable.All)
            {
                swatch.Append(species.Id).Append('\n');
                for (int p = 0; p < species.Palettes.Count; p++)
                {
                    swatch.Append("  ").Append(p).Append(' ')
                        .Append(p == 0 ? "ふつう" : "変異" + p).Append(' ')
                        .Append(string.Join(" ", species.Palettes[p].Colors)).Append('\n');
                }
                swatch.Append('\n');
            }
            var swatchPath = Path.Combine(outDir, "palettes.txt");
            File.WriteAllText(swatchPath, swatch.ToString(), new UTF8Encoding(false));
            Console.WriteLine($"  変異色: {Dir}/palettes.txt");
        }

        /// <summary>🔴 **(種族 × パレット) の全通り**を「あらかじめ差し替えた PNG」として焼く
        /// （作者の指示 2026-08-23）。⚠️ ゲーム側は色を差し替えない ── ここで焼いた PNG を
        /// そのまま `&lt;img&gt;` で出す（`EggCommand.Web.LayoutDom` / `SpriteManifest`）。
        ///
        /// ⚠️ **これを走らせ忘れると、ゲームは黙って古い絵を出す**（出所が2つに割れる）。
        /// ⭐ だから `EggCommand.Tests` の `SpritePngTests` に「その場で焼き直してバイト比較する」
        /// 検査を置いてある ── 忘れたら `dotnet test` が「sim sprites を走らせてください」と
        /// 言って落ちる。</summary>
        public static void RunDisplay(string root)
        {
            var outDir = Path.Combine(root, DisplayDir);
            Directory.CreateDirectory(outDir);

            int made = 0;
            long bytes = 0;
            foreach (var species in SpeciesTable.All)
            {
                for (int p = 0; p < species.Palettes.Count; p++)
                {
                    var path = Path.Combine(outDir, DisplayFileName(species.Id, p));
                    var png = Encode(species.Sprite, species.Palettes[p]);
                    File.WriteAllBytes(path, png);
                    made++;
                    bytes += png.Length;
                }
            }
            Console.WriteLine($"■ 表示用 PNG を焼いた: {made} 枚（{bytes:N0} バイト）→ {DisplayDir}/");
        }

        // ── PNG を組む ──────────────────────────────────

        private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public static byte[] Encode(PixelSprite sprite, Palette palette)
        {
            if (sprite == null) throw new ArgumentNullException(nameof(sprite));
            if (palette == null) throw new ArgumentNullException(nameof(palette));

            using var png = new MemoryStream();
            png.Write(Signature, 0, Signature.Length);

            // IHDR ── 幅・高さ・ビット深度8・色の型3（インデックス）
            using (var ihdr = new MemoryStream())
            {
                Be32(ihdr, sprite.Width);
                Be32(ihdr, sprite.Height);
                ihdr.WriteByte(8);   // ビット深度
                ihdr.WriteByte(3);   // 色の型: インデックスカラー
                ihdr.WriteByte(0);   // 圧縮法
                ihdr.WriteByte(0);   // フィルタ法
                ihdr.WriteByte(0);   // インターレース無し
                Chunk(png, "IHDR", ihdr.ToArray());
            }

            // PLTE ── 0番は透明の席（色は何でもよいが、黒だと縁が滲んで見えるので白に寄せる）
            int colors = palette.Count + 1;
            var plte = new byte[colors * 3];
            plte[0] = 0xff; plte[1] = 0xff; plte[2] = 0xff;
            for (int i = 1; i < colors; i++)
            {
                var rgb = Rgb(palette.ColorOf((byte)i));
                plte[i * 3] = rgb.Item1;
                plte[i * 3 + 1] = rgb.Item2;
                plte[i * 3 + 2] = rgb.Item3;
            }
            Chunk(png, "PLTE", plte);

            // tRNS ── ⚠️ 0番だけ透明。⭐ 1つだけ書けば残りは不透明とみなされる
            Chunk(png, "tRNS", new byte[] { 0 });

            // IDAT ── 各行の先頭にフィルタ種別0（なし）を付けて、zlib で包む
            using var raw = new MemoryStream();
            for (int y = 0; y < sprite.Height; y++)
            {
                raw.WriteByte(0);
                for (int x = 0; x < sprite.Width; x++) raw.WriteByte(sprite.At(x, y));
            }
            Chunk(png, "IDAT", Zlib(raw.ToArray()));

            Chunk(png, "IEND", Array.Empty<byte>());
            return png.ToArray();
        }

        /// <summary>⭐ **そのままの色（RGBA）で焼く。**⚠️ 上の <see cref="Encode"/> は
        /// 種族の絵ぶよう（パレット＋添字）だが、こちらは pixelizer から来た絵のように
        /// **色を数えていない**ものを、色数の上限なしにそのまま書く
        /// （[画面をドット絵で組む](../../wiki/開発/画面をドット絵で組む.md)）。
        ///
        /// ⚠️ `rgba` は行優先で 1 画素 4 バイト（R,G,B,A）。長さは `width*height*4` ちょうど。
        /// ⭐ 圧縮・チャンク・CRC は <see cref="Encode"/> と同じ手順を使い回す（写さない）。</summary>
        public static byte[] EncodeRgba(int width, int height, byte[] rgba)
        {
            if (rgba == null) throw new ArgumentNullException(nameof(rgba));
            if (width <= 0 || height <= 0) throw new ArgumentException($"大きさが変: {width}x{height}");
            if (rgba.Length != width * height * 4)
                throw new ArgumentException($"画素の数が合わない: {rgba.Length} ≠ {width}*{height}*4");

            using var png = new MemoryStream();
            png.Write(Signature, 0, Signature.Length);

            using (var ihdr = new MemoryStream())
            {
                Be32(ihdr, width);
                Be32(ihdr, height);
                ihdr.WriteByte(8);   // ビット深度
                ihdr.WriteByte(6);   // 色の型: 真色＋アルファ（RGBA）
                ihdr.WriteByte(0);   // 圧縮法
                ihdr.WriteByte(0);   // フィルタ法
                ihdr.WriteByte(0);   // インターレース無し
                Chunk(png, "IHDR", ihdr.ToArray());
            }

            using var raw = new MemoryStream();
            for (int y = 0; y < height; y++)
            {
                raw.WriteByte(0);   // フィルタ種別0（なし）── 読む側（Unfilter）と揃える
                raw.Write(rgba, y * width * 4, width * 4);
            }
            Chunk(png, "IDAT", Zlib(raw.ToArray()));

            Chunk(png, "IEND", Array.Empty<byte>());
            return png.ToArray();
        }

        private static Tuple<byte, byte, byte> Rgb(string hex)
        {
            // ⚠️ 「#rrggbb」以外を黙って通さない。⭐ 綴り違いが灰色として出ると気づけない
            if (hex == null || hex.Length != 7 || hex[0] != '#')
                throw new ArgumentException($"色は #rrggbb で書く: 「{hex}」");
            return Tuple.Create(
                Convert.ToByte(hex.Substring(1, 2), 16),
                Convert.ToByte(hex.Substring(3, 2), 16),
                Convert.ToByte(hex.Substring(5, 2), 16));
        }

        /// <summary>zlib の包み。⚠️ `DeflateStream` は**生の deflate**しか吐かないので、
        /// ⭐ 2バイトの頭と、末尾の adler32 を自分で付ける。</summary>
        private static byte[] Zlib(byte[] data)
        {
            using var wrapped = new MemoryStream();
            wrapped.WriteByte(0x78);   // CM=8（deflate）/ CINFO=7（32KB窓）
            wrapped.WriteByte(0x01);   // FCHECK。(0x78*256 + 0x01) % 31 == 0
            using (var deflate = new DeflateStream(wrapped, CompressionLevel.Optimal, true))
            {
                deflate.Write(data, 0, data.Length);
            }
            Be32(wrapped, unchecked((int)Adler32(data)));
            return wrapped.ToArray();
        }

        private static uint Adler32(byte[] data)
        {
            uint a = 1, b = 0;
            foreach (var v in data)
            {
                a = (a + v) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }

        private static void Chunk(Stream to, string kind, byte[] body)
        {
            Be32(to, body.Length);
            var head = Encoding.ASCII.GetBytes(kind);
            to.Write(head, 0, head.Length);
            to.Write(body, 0, body.Length);

            var crcOver = new byte[head.Length + body.Length];
            Buffer.BlockCopy(head, 0, crcOver, 0, head.Length);
            Buffer.BlockCopy(body, 0, crcOver, head.Length, body.Length);
            Be32(to, unchecked((int)Crc32(crcOver)));
        }

        private static void Be32(Stream to, int value)
        {
            to.WriteByte((byte)(value >> 24));
            to.WriteByte((byte)(value >> 16));
            to.WriteByte((byte)(value >> 8));
            to.WriteByte((byte)value);
        }

        private static uint[] _crcTable;

        private static uint Crc32(byte[] data)
        {
            if (_crcTable == null)
            {
                _crcTable = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++)
                        c = (c & 1) != 0 ? 0xedb88320u ^ (c >> 1) : c >> 1;
                    _crcTable[n] = c;
                }
            }
            uint crc = 0xffffffffu;
            foreach (var v in data) crc = _crcTable[(crc ^ v) & 0xff] ^ (crc >> 8);
            return crc ^ 0xffffffffu;
        }

        // ── 読み戻し（⭐ 往復が閉じているかを確かめるため）────────

        /// <summary>書いた PNG を読み戻して、元の添字とパレットに戻す。
        ///
        /// ⚠️ **これが無いと「書けた」しか言えません。**⭐ 往復が閉じることを
        /// 確かめて初めて「PNG を正典にしてよい」と言えます。
        /// ⚠️ 自分が書いた形（フィルタ0・インターレース無し・8bit）しか読みません。</summary>
        public static void Decode(byte[] png, out PixelSprite sprite, out Palette palette)
        {
            if (png == null || png.Length < 8) throw new ArgumentException("PNG が短すぎる");
            for (int i = 0; i < Signature.Length; i++)
                if (png[i] != Signature[i]) throw new ArgumentException("PNG の署名が違う");

            int at = 8;
            int width = 0, height = 0;
            byte[] plte = null;
            using var idat = new MemoryStream();

            while (at + 8 <= png.Length)
            {
                int len = (png[at] << 24) | (png[at + 1] << 16) | (png[at + 2] << 8) | png[at + 3];
                string kind = Encoding.ASCII.GetString(png, at + 4, 4);
                int body = at + 8;
                switch (kind)
                {
                    case "IHDR":
                        width = (png[body] << 24) | (png[body + 1] << 16) | (png[body + 2] << 8) | png[body + 3];
                        height = (png[body + 4] << 24) | (png[body + 5] << 16) | (png[body + 6] << 8) | png[body + 7];
                        if (png[body + 8] != 8 || png[body + 9] != 3)
                            throw new ArgumentException("8bit のインデックスカラーではない");
                        break;
                    case "PLTE":
                        plte = new byte[len];
                        Buffer.BlockCopy(png, body, plte, 0, len);
                        break;
                    case "IDAT":
                        idat.Write(png, body, len);
                        break;
                }
                at = body + len + 4;
                if (kind == "IEND") break;
            }
            if (width <= 0 || plte == null) throw new ArgumentException("IHDR か PLTE が無い");

            var rows = InflateZlib(idat.ToArray());

            var rowText = new string[height];
            for (int y = 0; y < height; y++)
            {
                int from = y * (width + 1);
                if (rows[from] != 0) throw new ArgumentException($"{y} 行目のフィルタが 0 でない");
                var line = new StringBuilder(width);
                for (int x = 0; x < width; x++) line.Append(PixelSprite.CharOf(rows[from + 1 + x]));
                rowText[y] = line.ToString();
            }
            sprite = PixelSprite.Parse(rowText);

            var colors = new string[plte.Length / 3 - 1];
            for (int i = 1; i < plte.Length / 3; i++)
                colors[i - 1] = $"#{plte[i * 3]:x2}{plte[i * 3 + 1]:x2}{plte[i * 3 + 2]:x2}";
            palette = new Palette(colors);
        }

        /// <summary>zlib の頭2バイトと末尾の adler32 を外して、生の deflate を展開する。
        /// ⚠️ <see cref="Decode"/> と <see cref="DecodeRgba"/> の共通部分（重複を避けるため切り出した）。</summary>
        private static byte[] InflateZlib(byte[] packed)
        {
            using var body = new MemoryStream(packed, 2, packed.Length - 6);
            using var inflate = new DeflateStream(body, CompressionMode.Decompress);
            using var flat = new MemoryStream();
            inflate.CopyTo(flat);
            return flat.ToArray();
        }

        // ── 手描きの原稿を読む（⚠️ 上の Decode とは別口）─────────────────

        /// <summary>手描きの PNG（Aseprite 等で書き出した、8bit の RGB か RGBA）を
        /// **生の画素**（RGBA・1画素4バイト）へ読む。
        ///
        /// ⚠️ <see cref="Decode"/> は「このコードが書いた PNG」（インデックスカラー・
        /// フィルタ0固定）しか読めない。⭐ こちらは**外から来る絵**を読むための別口 ──
        /// フィルタは行ごとに変わってよい（PNG の 0〜4 すべてに対応）。
        /// ⚠️ インターレースには対応しない（対応外は例外で止める。黙って崩さない）。</summary>
        public static void DecodeRgba(byte[] png, out int width, out int height, out byte[] rgba)
        {
            if (png == null || png.Length < 8) throw new ArgumentException("PNG が短すぎる");
            for (int i = 0; i < Signature.Length; i++)
                if (png[i] != Signature[i]) throw new ArgumentException("PNG の署名が違う");

            int at = 8;
            int w = 0, h = 0, bitDepth = 0, colorType = -1, interlace = 0;
            using var idat = new MemoryStream();

            while (at + 8 <= png.Length)
            {
                int len = (png[at] << 24) | (png[at + 1] << 16) | (png[at + 2] << 8) | png[at + 3];
                string kind = Encoding.ASCII.GetString(png, at + 4, 4);
                int body = at + 8;
                switch (kind)
                {
                    case "IHDR":
                        w = (png[body] << 24) | (png[body + 1] << 16) | (png[body + 2] << 8) | png[body + 3];
                        h = (png[body + 4] << 24) | (png[body + 5] << 16) | (png[body + 6] << 8) | png[body + 7];
                        bitDepth = png[body + 8];
                        colorType = png[body + 9];
                        interlace = png[body + 12];
                        break;
                    case "IDAT":
                        idat.Write(png, body, len);
                        break;
                }
                at = body + len + 4;
                if (kind == "IEND") break;
            }

            if (w <= 0 || h <= 0) throw new ArgumentException("IHDR が無い");
            if (bitDepth != 8)
                throw new ArgumentException($"8bit 以外は読めない（ビット深度 {bitDepth}）");
            if (colorType != 2 && colorType != 6)
                throw new ArgumentException($"RGB(2) か RGBA(6) 以外は読めない（色の型 {colorType}）");
            if (interlace != 0)
                throw new ArgumentException("インターレース付き PNG は読めない");

            int channels = colorType == 6 ? 4 : 3;
            var flat = InflateZlib(idat.ToArray());
            var pixels = Unfilter(flat, w, h, channels);

            width = w;
            height = h;
            if (channels == 4)
            {
                rgba = pixels;
                return;
            }

            // RGB → RGBA（不透明として扱う）
            var withAlpha = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++)
            {
                withAlpha[i * 4] = pixels[i * 3];
                withAlpha[i * 4 + 1] = pixels[i * 3 + 1];
                withAlpha[i * 4 + 2] = pixels[i * 3 + 2];
                withAlpha[i * 4 + 3] = 255;
            }
            rgba = withAlpha;
        }

        /// <summary>PNG のスキャンライン・フィルタ（0〜4）を外し、生の画素バイト列に戻す。
        /// ⚠️ 参照実装は PNG 仕様書どおり（バイト演算は 256 で自然に折り返す＝byte のまま足すだけでよい）。</summary>
        private static byte[] Unfilter(byte[] flat, int width, int height, int bpp)
        {
            int stride = width * bpp;
            var outp = new byte[height * stride];
            int at = 0;
            for (int y = 0; y < height; y++)
            {
                if (at >= flat.Length) throw new ArgumentException($"{y} 行目の手前でデータが尽きた");
                byte filter = flat[at++];
                int rowOut = y * stride;
                int prevOut = (y - 1) * stride;
                for (int x = 0; x < stride; x++)
                {
                    byte raw = flat[at + x];
                    byte a = x >= bpp ? outp[rowOut + x - bpp] : (byte)0;
                    byte b = y > 0 ? outp[prevOut + x] : (byte)0;
                    byte c = (y > 0 && x >= bpp) ? outp[prevOut + x - bpp] : (byte)0;
                    byte value;
                    switch (filter)
                    {
                        case 0: value = raw; break;
                        case 1: value = (byte)(raw + a); break;
                        case 2: value = (byte)(raw + b); break;
                        case 3: value = (byte)(raw + (byte)((a + b) / 2)); break;
                        case 4: value = (byte)(raw + PaethPredictor(a, b, c)); break;
                        default: throw new ArgumentException($"{y} 行目のフィルタ種別 {filter} は読めない");
                    }
                    outp[rowOut + x] = value;
                }
                at += stride;
            }
            return outp;
        }

        private static byte PaethPredictor(byte a, byte b, byte c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc) return a;
            return pb <= pc ? b : c;
        }
    }
}
