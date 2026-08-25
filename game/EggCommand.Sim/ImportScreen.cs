using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace EggCommand.Sim
{
    /// <summary>⭐ **pixelizer で起こした画面を、絵と骨組みに落とす。**
    ///
    /// 作者の指摘（2026-08-25）──「今いろいろおいてある状態を編集するより 0 から
    /// 置いていく方が作りやすそう。ピクセライザーに画面比率と画素数を揃えた
    /// キャンバスを作って、そこで比率を確認しながらアセットを作れるようにしたい」。
    ///
    /// 設計は [画面をドット絵で組む](../../wiki/開発/画面をドット絵で組む.md)。
    ///
    /// 🔴 **変換を1つも挟まない**（実測して確かめた土台）:
    /// <list type="bullet">
    /// <item>pixelizer の書き出しは**等倍**、`paint` の PNG も **1px＝1ドット** ── そのまま使える</item>
    /// <item>レイヤーの重なりは**末尾が手前**で、骨組みの並び順と同じ向き</item>
    /// <item>骨組みの座標 ＝ **ドット × 4** ── ドット単位で切るので**必ず4の倍数**になる</item>
    /// </list>
    ///
    /// ⚠️ **pixelizer 側にこの規約を1文字も置かない。**⭐ pixelizer にとっては
    /// 「9:16 の大きいキャンバスが増えた」だけ ── 名前の約束を知っているのはここだけ。</summary>
    public static class ImportScreen
    {
        /// <summary>ゲームの画面（ドット）。⚠️ [ドット絵化計画](../../wiki/開発/ドット絵化計画.md) §2。</summary>
        public const int ScreenW = 270;
        public const int ScreenH = 480;

        /// <summary>1ドットが設計 px でいくつか。</summary>
        public const int Scale = 4;

        /// <summary>絵にしない印。⚠️ 字・数・キャラは骨組みが描くので、絵にすると**二重に出る**。</summary>
        public const string GuideMark = "_";

        /// <summary>押しどころの印（`_tap-<名前>`）。</summary>
        public const string TapMark = "_tap-";

        public const string PaintDir = "assets/ui/paint";
        public const string LayoutsDir = "assets/layouts";

        /// <summary>切り出した1枚ぶん。</summary>
        private readonly struct Piece
        {
            public readonly string Name;
            /// <summary>骨組みに出す種類（`paint` / `button` / `label`）。</summary>
            public readonly string Kind;
            public readonly int X, Y, W, H;      // ドット
            public readonly byte[]? Png;         // ⚠️ 目安（`_`）のときは null

            public Piece(string name, string kind, int x, int y, int w, int h, byte[]? png)
            {
                Name = name; Kind = kind; X = x; Y = y; W = w; H = h; Png = png;
            }
        }

        /// <param name="log">言葉の出し先。⚠️ **`Console` を直に呼ばない** ── 検査が
        /// `Console.SetOut` で横取りすると、並列で走る他の検査の出力まで奪ってしまう
        /// （2026-08-25 に実際に踏んだ: `SheetRoundTripTests` が巻き添えで落ちた）。
        /// ⭐ 既定は `Console.Out` なので、打つ側の見た目は変わらない。</param>
        public static int Run(string root, string jsonPath, TextWriter? log = null)
        {
            log ??= Console.Out;
            // ⚠️ **どちらで打っても通す。**⭐ `sim` は `game/` から打つ約束（root は ".."）だが、
            //    手で打つ人はリポジトリ相対（`art/screens/…`）とカレント相対（`../art/screens/…`）を
            //    どちらも書く ── 片方しか通らないと「見つからない」で足止めされる。
            string full = jsonPath;
            if (!Path.IsPathRooted(full))
            {
                string fromRoot = Path.Combine(root, jsonPath);
                full = File.Exists(fromRoot) ? fromRoot : jsonPath;
            }
            if (!File.Exists(full))
            {
                log.WriteLine($"⛔ 見つからない: {jsonPath}");
                log.WriteLine($"   探した先: {Path.GetFullPath(Path.Combine(root, jsonPath))}");
                log.WriteLine($"             {Path.GetFullPath(jsonPath)}");
                return 1;
            }

            // ⚠️ 画面の名前はファイル名から取る（`home.pixelizer.json` → `home`）。
            string screen = Path.GetFileName(full);
            int dot = screen.IndexOf('.');
            if (dot > 0) screen = screen.Substring(0, dot);

            JsonElement doc;
            try
            {
                doc = JsonDocument.Parse(File.ReadAllText(full)).RootElement;
            }
            catch (JsonException e)
            {
                log.WriteLine($"⛔ 読めない（.pixelizer.json ではない?）: {e.Message}");
                return 1;
            }

            int w = doc.TryGetProperty("w", out var wv) ? wv.GetInt32() : 0;
            int h = doc.TryGetProperty("h", out var hv) ? hv.GetInt32() : 0;
            // 🔴 **画面の大きさが違うものを黙って通さない。**⭐ 通すと座標が全部ずれた
            //    骨組みができ、後から原因が分からなくなる。
            if (w != ScreenW || h != ScreenH)
            {
                log.WriteLine($"⛔ キャンバスが {w}×{h}。この画面は {ScreenW}×{ScreenH} で描いてください");
                log.WriteLine($"   （pixelizer の「サイズと変換」→「9:16 画面まるごと」→ {ScreenW}×{ScreenH}）");
                return 1;
            }

            if (!doc.TryGetProperty("layers", out var layers) || layers.ValueKind != JsonValueKind.Array)
            {
                log.WriteLine("⛔ layers が無い");
                return 1;
            }

            var pieces = new List<Piece>();
            var warned = new List<string>();

            foreach (var layer in layers.EnumerateArray())
            {
                string name = layer.TryGetProperty("name", out var nv) ? (nv.GetString() ?? "") : "";
                name = name.Trim();

                // ⚠️ 名前を付け忘れたレイヤーを `Layer 3.png` として書き出さない。
                //    ⭐ 黙って飛ばさず、何をすればよいかまで言う。
                if (name.Length == 0 || name.StartsWith("Layer ", StringComparison.Ordinal))
                {
                    warned.Add($"「{(name.Length == 0 ? "(名前なし)" : name)}」── 名前を部品名に変えてください（例: {screen}-bg）");
                    continue;
                }

                if (!layer.TryGetProperty("frames", out var frames)
                    || frames.ValueKind != JsonValueKind.Array || frames.GetArrayLength() == 0)
                {
                    warned.Add($"「{name}」── コマが1つも無い");
                    continue;
                }

                // ⚠️ コマ送りはこの経路では使わない（画面は動かない）。⭐ 先頭のコマだけ見る。
                string url = frames[0].GetString() ?? "";
                byte[] rgba;
                try
                {
                    rgba = DecodeDataUrl(url, w, h);
                }
                catch (Exception e)
                {
                    warned.Add($"「{name}」── 絵を読めない（{e.Message}）");
                    continue;
                }

                if (!Bounds(rgba, w, h, out int x0, out int y0, out int bw, out int bh))
                {
                    // ⚠️ 中身が全部透明。⭐ 黙って 0×0 を出さない。
                    warned.Add($"「{name}」── 中身が空（何も描かれていない）");
                    continue;
                }

                if (name.StartsWith(TapMark, StringComparison.Ordinal))
                {
                    // ⭐ 押しどころ ── 絵は出さない。場所だけ骨組みへ。
                    pieces.Add(new Piece(name.Substring(TapMark.Length), "button", x0, y0, bw, bh, null));
                }
                else if (name.StartsWith(GuideMark, StringComparison.Ordinal))
                {
                    // ⭐ 目安（字・数の入る場所）── 絵は出さない。
                    pieces.Add(new Piece(name.Substring(GuideMark.Length), "label", x0, y0, bw, bh, null));
                }
                else
                {
                    pieces.Add(new Piece(name, "paint", x0, y0, bw, bh,
                        SpritePng.EncodeRgba(bw, bh, Crop(rgba, w, x0, y0, bw, bh))));
                }
            }

            if (pieces.Count == 0)
            {
                log.WriteLine("⛔ 取り出せるレイヤーが1つもありませんでした");
                foreach (var line in warned) log.WriteLine("   ⚠️ " + line);
                return 1;
            }

            // ── 絵を書く ───────────────────────────────
            string paintDir = Path.Combine(root, PaintDir);
            Directory.CreateDirectory(paintDir);
            int wrote = 0;
            var resized = new List<string>();

            foreach (var p in pieces)
            {
                if (p.Png == null) continue;
                string path = Path.Combine(paintDir, p.Name + ".png");
                // ⚠️ **大きさが変わったら言う。**⭐ 骨組みの枠と合わなくなるので、
                //    黙って差し替えると「枠と絵が合わない」が静かに増える（段取り4 で 44箇所出た形）。
                if (File.Exists(path))
                {
                    int oldW = 0, oldH = 0;
                    try
                    {
                        var head = File.ReadAllBytes(path);
                        SpritePng.DecodeRgba(head, out oldW, out oldH, out _);
                    }
                    catch { }
                    if (oldW > 0 && (oldW != p.W || oldH != p.H))
                        resized.Add($"{p.Name}: {oldW}×{oldH} → {p.W}×{p.H}");
                }
                File.WriteAllBytes(path, p.Png);
                wrote++;
            }

            log.WriteLine($"⭐ 絵を {wrote} 枚書きました（{PaintDir}/）");
            foreach (var p in pieces)
            {
                string mark = p.Png == null ? (p.Kind == "button" ? "押しどころ" : "目安") : $"{p.W}×{p.H}";
                log.WriteLine($"   {p.Name,-16} {p.Kind,-7} ({p.X},{p.Y}) {mark}");
            }

            if (resized.Count > 0)
            {
                log.WriteLine();
                log.WriteLine("⚠️ **大きさが変わった絵があります**（骨組みの枠も直してください）:");
                foreach (var line in resized) log.WriteLine("   " + line);
            }

            if (warned.Count > 0)
            {
                log.WriteLine();
                log.WriteLine($"⚠️ 飛ばしたレイヤー（{warned.Count} 件）:");
                foreach (var line in warned) log.WriteLine("   " + line);
            }

            // ── 骨組み ─────────────────────────────────
            string layoutPath = Path.Combine(root, LayoutsDir, screen + ".txt");
            string made = BuildLayout(screen, pieces);

            log.WriteLine();
            if (File.Exists(layoutPath))
            {
                // 🔴 **既にある骨組みは絶対に上書きしない。**⚠️ 手で入れた `when=`・`use=`・
                //    微調整・注釈が消える（往復のバイト忠実と同じ姿勢）。⭐ 差分だけ言う。
                log.WriteLine($"⚠️ `{LayoutsDir}/{screen}.txt` は既にあります ── **書き換えていません**。");
                log.WriteLine("   ⭐ この座標になります（合わせたい行だけ手で直すか、エディタで動かしてください）:");
                log.WriteLine();
                foreach (var line in made.Split('\n'))
                    if (line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal))
                        log.WriteLine("   " + line);
            }
            else
            {
                Directory.CreateDirectory(Path.Combine(root, LayoutsDir));
                File.WriteAllText(layoutPath, made, new UTF8Encoding(false));
                log.WriteLine($"⭐ 骨組みの雛形を書きました: {LayoutsDir}/{screen}.txt");
                log.WriteLine("   ⚠️ `tap=` と `text=` は空のままです ── エディタ（/edit）で決めてください。");
            }

            log.WriteLine();
            log.WriteLine("⭐ 次: `sim paint-placeholder` で絵の一覧（paint-manifest.txt）を作り直してください。");
            return 0;
        }

        /// <summary>骨組みの雛形。⚠️ **座標はドット×4**（＝必ず4の倍数）。
        /// ⭐ 並びは pixelizer のレイヤー順のまま（末尾が手前 ── 骨組みと同じ向き）。</summary>
        private static string BuildLayout(string screen, List<Piece> pieces)
        {
            var sb = new StringBuilder();
            sb.Append($"# {screen} ── ⭐ pixelizer から起こした雛形（`sim import-screen`）\n");
            sb.Append("#\n");
            sb.Append("# ⚠️ **座標はドット×4**（1ドット＝設計4px）。手で書き換えるときも4の倍数を守ること。\n");
            sb.Append("# ⚠️ `tap=` と `text=` は空 ── エディタ（/edit）で決める。\n");
            sb.Append("#\n");
            // ⭐ 桁を**中身から決める**（名前が長いと列がずれるので固定幅にしない）。
            int nameW = "# 名前".Length;
            int kindW = "種類".Length + 2;
            foreach (var p in pieces)
            {
                if (p.Name.Length + 1 > nameW) nameW = p.Name.Length + 1;
                if (p.Kind.Length + 1 > kindW) kindW = p.Kind.Length + 1;
            }
            sb.Append("#  名前   種類    左   上    幅    高    付け足し\n");

            foreach (var p in pieces)
            {
                string extra = p.Kind == "paint" ? $"pic={p.Name}"
                    : p.Kind == "label" ? $"size=40 text={p.Name}"
                    : "";
                string line = Cell(p.Name, nameW)
                    + Cell(p.Kind, kindW)
                    + Cell(Num(p.X * Scale), 5)
                    + Cell(Num(p.Y * Scale), 6)
                    + Cell(Num(p.W * Scale), 6)
                    + Cell(Num(p.H * Scale), 6)
                    + extra;
                // ⚠️ 行末に空白を残さない（`button` は付け足しが空なので、
                //    詰めないと見えない空白が行末に残る）。
                sb.Append(line.TrimEnd()).Append('\n');
            }
            return sb.ToString();
        }

        private static string Num(int v) => v.ToString(CultureInfo.InvariantCulture);

        private static string Cell(string text, int width) =>
            text.Length >= width ? text + " " : text.PadRight(width);

        /// <summary>`data:image/png;base64,...` を RGBA へ。</summary>
        private static byte[] DecodeDataUrl(string url, int w, int h)
        {
            const string mark = "base64,";
            int at = url.IndexOf(mark, StringComparison.Ordinal);
            if (at < 0) throw new ArgumentException("base64 の data URL ではない");
            byte[] png = Convert.FromBase64String(url.Substring(at + mark.Length));
            SpritePng.DecodeRgba(png, out int gw, out int gh, out byte[] rgba);
            if (gw != w || gh != h) throw new ArgumentException($"レイヤーが {gw}×{gh}（キャンバスは {w}×{h}）");
            return rgba;
        }

        /// <summary>透明でない範囲（bounding box）。⭐ 何も無ければ false。</summary>
        private static bool Bounds(byte[] rgba, int w, int h, out int x0, out int y0, out int bw, out int bh)
        {
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // ⚠️ アルファ 0 だけを「無い」とみなす（薄い色は在るものとして扱う）。
                    if (rgba[(y * w + x) * 4 + 3] == 0) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < 0) { x0 = y0 = bw = bh = 0; return false; }
            x0 = minX; y0 = minY; bw = maxX - minX + 1; bh = maxY - minY + 1;
            return true;
        }

        private static byte[] Crop(byte[] rgba, int srcW, int x0, int y0, int bw, int bh)
        {
            var cut = new byte[bw * bh * 4];
            for (int y = 0; y < bh; y++)
                Array.Copy(rgba, ((y0 + y) * srcW + x0) * 4, cut, y * bw * 4, bw * 4);
            return cut;
        }
    }
}
