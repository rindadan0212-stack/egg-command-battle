#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>卵の見た目。⭐ **輪郭は1つ、中身は種族ごとに違う模様。**
    ///
    /// ⭐ 輪郭は作者が描いたドット絵（`assets/ui/home-src/04_たまご.png`）から
    /// **黒い縁だけを抜いた**もの。⚠️ 形はここが唯一の出所 ── 種族が増えても
    /// 輪郭は描き足さない（同じ卵の形に、違う模様が入るだけ）。
    ///
    /// ⭐ **模様は絵ではなく式**（<see cref="Paint"/>）。⚠️ 44x55 の絵を11枚
    /// 手で置くと、直すたびに11箇所を触ることになる ── 模様を足す／色を変えるが
    /// 1箇所で済む形にしてある。
    ///
    /// ⚠️ **ここは PNG を作らない。**⭐ 焼くのは `EggCommand.Sim` の `egg-art`
    /// （`assets/ui/paint/egg-&lt;種族&gt;.png`）── 画面は焼いた PNG を1枚出すだけなので、
    /// 卵1つにつき 1762 個の矩形を並べずに済む（`LayoutDom.Dots` の SVG 経路を通さない）。</summary>
    public static class EggSkins
    {
        /// <summary>添字: 1=輪郭 / 2=地 / 3=模様。⚠️ 0 は透明（`PixelSprite` の決まり）。</summary>
        public const byte Edge = 1, Shell = 2, Mark = 3;

        /// <summary>卵の形。⚠️ 1=輪郭・2=中身。⭐ 中身を模様で 2/3 に塗り分ける。</summary>
        public static readonly PixelSprite Shape = PixelSprite.Parse(new[]
        {
            "..................11111111..................",
            "................112222222211................",
            "..............1122222222222211..............",
            "............11222222222222222211............",
            "...........1222222222222222222221...........",
            "..........122222222222222222222221..........",
            ".........12222222222222222222222221.........",
            "........1222222222222222222222222221........",
            ".......122222222222222222222222222221.......",
            ".......122222222222222222222222222221.......",
            "......12222222222222222222222222222221......",
            "......12222222222222222222222222222221......",
            ".....1222222222222222222222222222222221.....",
            ".....1222222222222222222222222222222221.....",
            "....122222222222222222222222222222222221....",
            "....122222222222222222222222222222222221....",
            "...12222222222222222222222222222222222221...",
            "...12222222222222222222222222222222222221...",
            "...12222222222222222222222222222222222221...",
            "..1222222222222222222222222222222222222221..",
            "..1222222222222222222222222222222222222221..",
            ".122222222222222222222222222222222222222221.",
            ".122222222222222222222222222222222222222221.",
            ".122222222222222222222222222222222222222221.",
            ".122222222222222222222222222222222222222221.",
            ".122222222222222222222222222222222222222221.",
            "12222222222222222222222222222222222222222221",
            "12222222222222222222222222222222222222222221",
            "12222222222222222222222222222222222222222221",
            "12222222222222222222222222222222222222222221",
            "12222222222222222222222222222222222222222221",
            "12222222222222222222222222222222222222222221",
            "12222222222222222222222222222222222222222221",
            "12222222222222222222222222222222222222222221",
            "12222222222222222222222222222222222222222221",
            "12222222222222222222222222222222222222222221",
            "12222222222222222222222222222222222222222221",
            ".122222222222222222222222222222222222222221.",
            ".122222222222222222222222222222222222222221.",
            ".122222222222222222222222222222222222222221.",
            ".122222222222222222222222222222222222222221.",
            "..1222222222222222222222222222222222222221..",
            "..1222222222222222222222222222222222222221..",
            "...12222222222222222222222222222222222221...",
            "...12222222222222222222222222222222222221...",
            "....122222222222222222222222222222222221....",
            ".....1222222222222222222222222222222221.....",
            "......12222222222222222222222222222221......",
            "......12222222222222222222222222222221......",
            ".......122222222222222222222222222221.......",
            "........1222222222222222222222222221........",
            ".........11222222222222222222222211.........",
            "...........1122222222222222222211...........",
            ".............111222222222222111.............",
            "................111111111111................",
        });

        /// <summary>模様の種類。⭐ **種族の見た目に寄せる**（竜なら斑、炎ならギザギザ）。</summary>
        public enum Mode
        {
            /// <summary>斑。⭐ 作者の原画と同じ「大きめの丸が散る」形。</summary>
            Spots,
            /// <summary>腹白。⭐ 下側だけ色が変わる（ペンギンの腹）。</summary>
            Belly,
            /// <summary>横縞。</summary>
            Stripes,
            /// <summary>波。⭐ 横縞を左右に揺らしたもの（水棲）。</summary>
            Waves,
            /// <summary>ギザギザ。⭐ 上向きの山（炎・棘）。</summary>
            Zigzag,
            /// <summary>まだら。⭐ 輪郭のぼやけた渦（霊体）。</summary>
            Marble,
            /// <summary>菱形の格子。</summary>
            Diamond,
            /// <summary>ひび。⭐ 縦に割れた筋（岩）。</summary>
            Crack,
            /// <summary>無地。⭐ 上に艶だけ。⚠️ **1種は無地を残す** ──
            /// 全部に模様があると、模様そのものが情報でなくなる。</summary>
            Plain,
            /// <summary>帯と斑。⭐ ヌシ専用（帯を巻いた特別な卵）。</summary>
            Crown,

            // ── ここから下は**種族に紐付けていない**（2026-08-27・作者の指示）────────
            //
            // ⭐ **色と模様を差し替えて試すための引き出し。**⚠️ 上の10種は「その種族に見える」
            //    ことを狙って作ってあるが、こちらは**幾何学の形そのもの**で、意味を持たない。
            //    ⭐ どれも `Skin(模様, 地の色, 模様の色)` の1行で好きな色と組める。
            // ⚠️ **どの種族にも割り当てていない。**割り当てるまで画面には出ない
            //    （`sim egg-try` で見比べてから決める）。

            /// <summary>水玉。⭐ 等間隔・千鳥。⚠️ <see cref="Spots"/> と違い、
            /// 大きさも位置も揃っている（あちらは大小まちまちの斑）。</summary>
            Dots,
            /// <summary>市松。</summary>
            Check,
            /// <summary>縦縞。⚠️ <see cref="Stripes"/>（横縞）と対。</summary>
            Bars,
            /// <summary>斜め縞。</summary>
            Slant,
            /// <summary>格子。⭐ 細い線の交差（面ではなく線）。</summary>
            Lattice,
            /// <summary>星のマーク。⭐ 4つ角の星を千鳥に散らす。</summary>
            Stars,
            /// <summary>同心円。⭐ 卵の中心から外へ広がる輪。</summary>
            Rings,
            /// <summary>鱗。⭐ 弧を半分ずらして重ねる。</summary>
            Scales,
            /// <summary>三角の並び。</summary>
            Triangles,
            /// <summary>十字のマーク。</summary>
            Cross,
        }

        /// <summary>1種ぶんの意匠。</summary>
        public readonly struct Skin
        {
            public readonly Mode Look;
            /// <summary>地の色（面積の広いほう）。</summary>
            public readonly string Ground;
            /// <summary>模様の色。</summary>
            public readonly string Ink;
            public Skin(Mode look, string ground, string ink)
            {
                Look = look; Ground = ground; Ink = ink;
            }
        }

        /// <summary>輪郭の色。⚠️ 作者の原画と同じ黒 ── ⭐ 種族で変えない
        /// （形も縁も同じだから「同じ卵の別の柄」に見える）。</summary>
        public const string EdgeColor = "#000000";

        /// <summary>種族 → 意匠。⭐ **色は種族の絵の実測**（面積の広い順の上位2色）。
        /// ⚠️ 勘で決めていない ── `art/sprites/display/&lt;種族&gt;-0.png` を数えた値。</summary>
        private static readonly Dictionary<string, Skin> Table = new Dictionary<string, Skin>(StringComparer.Ordinal)
        {
            // タマル ── 紺の体に白い腹。⭐ 腹白をそのまま卵へ
            ["tamaru"] = new Skin(Mode.Belly, "#474671", "#f7f5ea"),
            // ツノガ ── 淡い霊体。⭐ 輪郭のぼやけた渦
            ["tsunoga"] = new Skin(Mode.Marble, "#fbf6e5", "#21d2c5"),
            // ハネル ── 緑の竜。⭐ 原画と同じ斑（この模様が出発点だった）
            ["haneru"] = new Skin(Mode.Spots, "#87ac5d", "#faf1c9"),
            // ノビル ── 赤い被り物に肌色の体。⭐ 太い横縞
            ["nobiru"] = new Skin(Mode.Stripes, "#fcbd8a", "#fe3b40"),
            // ヒラベ ── 青い魚。⭐ 波
            ["hirabe"] = new Skin(Mode.Waves, "#6eb4c9", "#a8dcea"),
            // トゲル ── 赤黒い棘玉。⭐ ギザギザ
            ["togeru"] = new Skin(Mode.Zigzag, "#c96e6e", "#2e1818"),
            // マルミ ── 生成りの丸。⭐ **無地**（模様を持たない1種）
            ["marumi"] = new Skin(Mode.Plain, "#e0d0a8", "#f4ecd0"),
            // キバネ ── 紫の蝙蝠。⭐ 菱形の格子
            ["kibane"] = new Skin(Mode.Diamond, "#9a7ec9", "#c6b0ea"),
            // イワオ ── 灰の岩。⭐ ひび
            ["iwao"] = new Skin(Mode.Crack, "#8f8a7e", "#22201c"),
            // ホムラ ── 橙の炎。⭐ 上向きのギザギザ（トゲルと同じ式・色と濃さで別物に見える）
            ["homura"] = new Skin(Mode.Zigzag, "#e08a4e", "#f5c48c"),
            // ヌシ ── 茶と金。⭐ 帯を巻いた特別な卵
            ["nushi"] = new Skin(Mode.Crown, "#6b5a3e", "#e8d48a"),
        };

        /// <summary>⚠️ 表に無い種族は斑・生成り（黙って落とさない）。
        /// ⭐ 種族を足したときも「模様の無い四角」ではなく卵が出る。</summary>
        public static Skin Of(string speciesId) =>
            Table.TryGetValue(speciesId, out var skin) ? skin
                : new Skin(Mode.Spots, "#e0d0a8", "#9a8f74");

        /// <summary>その種族の卵の絵の名前（`assets/ui/paint/&lt;これ&gt;.png`）。</summary>
        public static string NameOf(string speciesId) => "egg-" + speciesId;

        /// <summary>色の組。⚠️ 並びは <see cref="Edge"/>/<see cref="Shell"/>/<see cref="Mark"/> の順。</summary>
        public static Palette PaletteOf(string speciesId)
        {
            var skin = Of(speciesId);
            return new Palette(EdgeColor, skin.Ground, skin.Ink);
        }

        /// <summary>その種族の卵を1枚組む。⭐ 形は <see cref="Shape"/> のまま、
        /// 中身（添字2）だけを模様で 2/3 に塗り分ける。</summary>
        public static PixelSprite Build(string speciesId) => BuildLook(Of(speciesId).Look);

        /// <summary>模様だけを指定して1枚組む。⭐ **種族の表を通らない道**
        /// ── 見本を並べる道具（`sim egg-try`）が、まだ誰にも割り当てていない模様を
        /// 描くために要る。⚠️ 色は呼ぶ側が決める（ここは塗り分けだけ）。</summary>
        public static PixelSprite BuildLook(Mode look)
        {
            int w = Shape.Width, h = Shape.Height;
            var rows = new string[h];
            var line = new char[w];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    byte at = Shape.At(x, y);
                    if (at == Shell && Paint(look, x, y, w, h)) at = Mark;
                    // ⚠️ 添字 → 文字は `PixelSprite.Digits` が唯一の出所（0 は透明の '.'）
                    line[x] = at == 0 ? '.' : PixelSprite.Digits[at - 1];
                }
                rows[y] = new string(line);
            }
            return PixelSprite.Parse(rows);
        }

        /// <summary>その画素を模様の色で塗るか。⭐ **模様の定義はここだけ**。
        /// ⚠️ 中身かどうかは呼ぶ側が見ている（ここは形を知らない）。</summary>
        private static bool Paint(Mode look, int x, int y, int w, int h)
        {
            switch (look)
            {
                // ⭐ 大きめの丸を散らす。⚠️ 乱数で置かない ── 焼くたびに柄が変わると
                //    「同じ種族の卵」に見えなくなる。中心と半径を表で持つ。
                case Mode.Spots:
                    foreach (var spot in SpotList)
                        if (Near(x, y, spot)) return true;
                    return false;

                // ⭐ 下側。⚠️ 境目をまっすぐにしない（腹は丸い）
                case Mode.Belly:
                {
                    double t = (x - w / 2.0) / (w / 2.0);
                    return y > h * 0.46 + t * t * h * 0.10;
                }

                // ⭐ 太い横縞（6ドットごと）
                case Mode.Stripes:
                    return (y / 6) % 2 == 1;

                // ⭐ 横縞を左右に揺らす
                case Mode.Waves:
                {
                    int shift = (int)Math.Round(Math.Sin(x * 0.45) * 2.2);
                    return ((y + shift) / 5) % 2 == 1;
                }

                // ⭐ 上向きの山。⚠️ 折り返しの幅で山の数が決まる
                case Mode.Zigzag:
                {
                    int fold = Math.Abs((x % 14) - 7);
                    return ((y + fold * 2) / 7) % 2 == 1;
                }

                // ⭐ 輪郭のぼやけた渦。⚠️ 三角関数を2本かけて、繰り返しに見えないようにする
                case Mode.Marble:
                {
                    double v = Math.Sin(x * 0.33 + Math.Cos(y * 0.21) * 2.4)
                             + Math.Sin(y * 0.28 - Math.Cos(x * 0.17) * 1.8);
                    return v > 0.45;
                }

                // ⭐ 菱形の格子
                case Mode.Diamond:
                    return Math.Abs((x % 12) - 6) + Math.Abs((y % 12) - 6) <= 3;

                // ⭐ 縦に割れた筋＋枝。⚠️ **滑らかな曲線にしない** ── 三角関数で描くと
                //    血管に見えて、石が割れた線にならなかった（1x で見て分かった）。
                //    折れ線は <see cref="CrackLane"/> が持つ（段ごとに折れる・繋がる）。
                case Mode.Crack:
                {
                    int lane = CrackLane[Math.Min(y, CrackLane.Length - 1)];
                    if (Math.Abs(x - lane) <= 1) return true;
                    // ⭐ 枝は決まった段から横へ。⚠️ 左右に散らす（片側だけだと折れ曲がりに見える）
                    foreach (var branch in Branches)
                        if (y >= branch.Y && y < branch.Y + 2)
                        {
                            int far = lane + branch.Run;
                            if (x >= Math.Min(lane, far) && x <= Math.Max(lane, far)) return true;
                        }
                    return false;
                }

                // ⭐ 無地。⚠️ 艶は左上に1つだけ
                case Mode.Plain:
                {
                    double dx = (x - w * 0.32) / (w * 0.16);
                    double dy = (y - h * 0.24) / (h * 0.10);
                    return dx * dx + dy * dy <= 1.0;
                }

                // ⭐ 帯＋斑。⚠️ 帯は卵の一番太い所に巻く
                case Mode.Crown:
                {
                    double band = h * 0.52;
                    if (y >= band && y < band + 5) return true;
                    if (y >= band + 7 && y < band + 9) return true;
                    foreach (var spot in CrownList)
                        if (Near(x, y, spot)) return true;
                    return false;
                }

                // ── 種族に紐付けない幾何の模様 ──────────────────────

                // ⭐ 水玉。⚠️ 段ごとに半分ずらす（真四角に並べると格子に見える）
                case Mode.Dots:
                {
                    const int gx = 11, gy = 11, r = 3;
                    int dx = Wrap(x - (y / gy % 2) * (gx / 2), gx) - gx / 2;
                    int dy = y % gy - gy / 2;
                    return dx * dx + dy * dy <= r * r;
                }

                // ⭐ 市松
                case Mode.Check:
                    return (x / 7 + y / 7) % 2 == 0;

                // ⭐ 縦縞
                case Mode.Bars:
                    return x / 6 % 2 == 1;

                // ⭐ 斜め縞。⚠️ x+y の等値線なので 45°
                case Mode.Slant:
                    return (x + y) / 6 % 2 == 1;

                // ⭐ 格子。⚠️ 線は細く（太いと市松に見える）
                case Mode.Lattice:
                    return x % 9 < 2 || y % 9 < 2;

                // ⭐ 4つ角の星。⚠️ 5つ角は 44x55 では潰れるので使わない。
                // 🔴 **腕は先へ行くほど細らせる**（√の和＝星芒形）。⚠️ 最初は
                //    「十字＋中心のひし形」で描いたが、それだと <see cref="Cross"/> と
                //    見分けがつかなかった（1x で並べて分かった）── 腕の間が凹んで
                //    初めて「星」に見える。
                case Mode.Stars:
                {
                    const int gx = 15, gy = 15;
                    int dx = Math.Abs(Wrap(x - (y / gy % 2) * (gx / 2), gx) - gx / 2);
                    int dy = Math.Abs(y % gy - gy / 2);
                    return Math.Sqrt(dx) + Math.Sqrt(dy) <= 2.6;
                }

                // ⭐ 同心円。⚠️ 卵の真ん中から測る（升目の繰り返しではない）
                case Mode.Rings:
                {
                    double rx = x - w / 2.0, ry = y - h / 2.0;
                    return (int)(Math.Sqrt(rx * rx + ry * ry) / 5) % 2 == 1;
                }

                // ⭐ 鱗。⚠️ 弧（輪の縁）だけ塗る ── 塗り潰すと水玉と見分けがつかない
                case Mode.Scales:
                {
                    const int gx = 11, gy = 7;
                    double dx = Wrap(x - (y / gy % 2) * (gx / 2), gx) - gx / 2.0;
                    double dy = y % gy;
                    double r = Math.Sqrt(dx * dx + dy * dy);
                    return r >= gy - 1.6 && r <= gy + 0.6;
                }

                // ⭐ 上向きの三角の並び
                case Mode.Triangles:
                {
                    const int gx = 12, gy = 10;
                    int cx = Math.Abs(x % gx - gx / 2);
                    return cx <= y % gy * (gx / 2) / gy;
                }

                // ⭐ 十字のマーク
                case Mode.Cross:
                {
                    const int gx = 13, gy = 13;
                    int dx = Math.Abs(Wrap(x - (y / gy % 2) * (gx / 2), gx) - gx / 2);
                    int dy = Math.Abs(y % gy - gy / 2);
                    return (dx <= 1 && dy <= 4) || (dy <= 1 && dx <= 4);
                }
            }
            return false;
        }

        /// <summary>負の数でも 0..<paramref name="span"/>-1 に収める余り。
        /// ⚠️ C# の `%` は負を負のまま返すので、段ずらし（千鳥）でそのまま使うと
        /// 左端の1列だけ模様が欠ける。</summary>
        private static int Wrap(int value, int span) => (value % span + span) % span;

        private static bool Near(int x, int y, (int X, int Y, int R) spot) =>
            (x - spot.X) * (x - spot.X) + (y - spot.Y) * (y - spot.Y) <= spot.R * spot.R;

        /// <summary>斑の置き場（中心x, 中心y, 半径）。⭐ **作者の原画の斑を数えた位置**
        /// ── 目分量で散らすと、原画と別物の卵になる。</summary>
        private static readonly (int X, int Y, int R)[] SpotList =
        {
            (11, 19, 4), (31, 27, 5), (7, 41, 5), (33, 48, 5), (23, 8, 3),
        };

        /// <summary>ひびの筋が、その段でどの列に居るか。⭐ **上から下へ繋がる**
        /// （段ごとに独立して決めると、筋が途切れて点線になる）。
        /// ⚠️ 折れ幅の並びは決め打ち ── 乱数だと焼くたびに柄が変わる。</summary>
        private static readonly int[] CrackLane = BuildCrackLane();

        private static int[] BuildCrackLane()
        {
            int h = Shape.Height, w = Shape.Width;
            var lane = new int[h];
            int[] bend = { 1, -1, 2, -1, 1, -2, 1, 1, -1, 2, -2, 1 };
            int at = w / 2 - 2;
            for (int y = 0; y < h; y++)
            {
                // ⭐ 3段に1回だけ折れる。⚠️ 毎段折ると、ぎざぎざの飾りになって割れ目に見えない
                if (y % 3 == 0) at += bend[(y / 3) % bend.Length];
                lane[y] = at;
            }
            return lane;
        }

        /// <summary>ひびの枝（その段から横へ何ドット伸ばすか。負なら左）。</summary>
        private static readonly (int Y, int Run)[] Branches =
        {
            (13, 8), (24, -9), (34, 7), (43, -6),
        };

        /// <summary>ヌシの斑。⭐ 帯の上下に小さく散らす。</summary>
        private static readonly (int X, int Y, int R)[] CrownList =
        {
            (10, 16, 3), (32, 14, 2), (22, 44, 3), (11, 47, 2), (34, 40, 2),
        };
    }
}
