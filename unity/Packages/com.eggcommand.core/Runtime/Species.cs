#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>3すくみ。炎 → 木 → 水 → 炎。
    /// （炎は木を焼き / 木は水を吸い / 水は炎を消す）
    ///
    /// ⚠️ 以前は 牙 → 羽 → 鱗 だった。**輪の向きはそのまま**で名前だけ入れ替えてある
    /// （牙=炎 / 羽=木 / 鱗=水）。種族どうしの有利不利は1つも変わっていない。
    /// ⭐ 見て分かる属性にしたのは、種族が増えたときに**相性を覚えずに読めるようにする**ため。
    /// 牙と羽のどちらが強いかは覚えるしかないが、炎と木なら見た瞬間に分かる。</summary>
    public enum Element
    {
        Fire,
        Water,
        Wood,
    }

    public sealed class Species
    {
        public readonly string Id;
        public readonly string Name;
        /// <summary>種族固定のスキル枠1。</summary>
        public readonly string Skill1;
        /// <summary>⚠️ 合計は <see cref="SpeciesTable.BaseTotal"/> に揃える。差は配分で出す。</summary>
        public readonly StatBlock Base;
        public readonly PixelSprite Sprite;
        /// <summary>0 = 通常。1以降が変異色（ARK と同じく変異は色変化として出る）。</summary>
        public readonly IReadOnlyList<Palette> Palettes;
        /// <summary>この種族の卵から枠2・3 に出うる技。
        ///
        /// ⭐ **種族の行に置く。** 別表にしていたときは、種族を足すのに2か所を直す必要があり、
        /// 片方を忘れると「遊んでいる最中に投げる」形で出た（コンパイルは通る）。
        /// ⚠️ 種族ごとにプールを分けるのは、どこで卵を奪っても同じ技が出ると
        /// 「必要な技を持つ親の巣へ行く」という輪の駆動力が消えるため。</summary>
        public readonly IReadOnlyList<string> Gacha;

        public Species(string id, string name, string skill1, StatBlock baseStats,
            PixelSprite sprite, IReadOnlyList<Palette> palettes, IReadOnlyList<string> gacha)
        {
            Id = id;
            Name = name;
            Skill1 = skill1;
            Base = baseStats;
            Sprite = sprite;
            Palettes = palettes;
            Gacha = gacha;
        }
    }

    /// <summary>種族は「器」。中身（ステの野生レベル・スキル2/3）は種族から独立して流通する。
    ///
    /// | 種族が決めるもの | 種族から独立して流通するもの |
    /// |---|---|
    /// | 見た目（ドット + パレット） | ステの野生レベル |
    /// | スキル1（種族固定枠） | **属性**（3すくみ） |
    /// | 基礎値の**配分** | スキル2・3 |
    /// | | 育てた分 |
    ///
    /// ⭐ **属性は種族に固定しない**（2026-08-17）。炎のタマルも水のタマルも生まれる。
    /// 種族＝見た目と骨格、属性＝そのつど引くもの、と役割が分かれる。
    /// ⚠️ 固定していたとき、巣の守り手は種族が1つなので必ず単一属性になり、
    /// 有利属性を揃えれば**確定で勝てた**（実測 100%）。個体ごとに散れば巣の中も混ざる。
    ///
    /// ⚠️ 種族ごとに基礎値の合計を変えない。
    /// 変えると最強種族に全部が集約され、種族の多様性が「どれを使うのが得か」という
    /// 最適化問題に潰される。差は配分と属性で出す。
    ///
    /// ⚠️ スキル1がどのステで伸びるかは、そのステを二重に得にする。
    /// タマルの殻打ちは防御スケールなので、防御が「守り」と「攻め」を兼ねる。
    /// 1種族しか無かったとき、これが釣り合いの計測を丸ごと濁らせた（実測で発覚）。
    /// 種族ごとに違うステへ寄せてある。</summary>
    public static class SpeciesTable
    {
        public static readonly Element[] Elements = { Element.Fire, Element.Water, Element.Wood };

        public static string LabelOf(Element element)
        {
            switch (element)
            {
                case Element.Fire: return "炎";
                case Element.Water: return "水";
                case Element.Wood: return "木";
                default: throw new ArgumentOutOfRangeException(nameof(element));
            }
        }

        /// <summary>属性を1つ引く。⭐ 種族に関係なく等確率。
        /// ⚠️ 専用の系統（RngElement）で引くこと。既にある系統に混ぜると列がずれる。</summary>
        public static Element Roll(Rng rng) => Elements[rng.Int(0, Elements.Length)];

        /// <summary>有利を取る相手。炎 → 木 → 水 → 炎。</summary>
        public static Element Beats(Element element)
        {
            switch (element)
            {
                case Element.Fire: return Element.Wood;
                case Element.Wood: return Element.Water;
                case Element.Water: return Element.Fire;
                default: throw new ArgumentOutOfRangeException(nameof(element));
            }
        }

        /// <summary>⚠️ 全種族で揃える基礎値の合計。ここを種族ごとに変えない。</summary>
        public const int BaseTotal = 80;

        // ── 意匠 ───────────────────────────────────────────
        // 文字の格子で持つ。テキストのまま人が手で直せる。
        // 1=輪郭 2=体 3=差し色 4=目

        /// <summary>タマル — 丸い。殻を思わせる。</summary>
        private static readonly PixelSprite TamaruSprite = PixelSprite.Parse(new[]
        {
            "................",
            "................",
            ".....111111.....",
            "...1122222211...",
            "..112222222211..",
            ".11332222222211.",
            ".12332222222221.",
            ".12222222222221.",
            ".12244222244221.",
            ".12244222244221.",
            ".12222222222221.",
            ".11222222222211.",
            "..112222222211..",
            "...1122222211...",
            "....11111111....",
            "................",
        });

        /// <summary>ツノガ — 角がある。輪郭が角張っている。</summary>
        private static readonly PixelSprite TsunogaSprite = PixelSprite.Parse(new[]
        {
            "................",
            "..1..........1..",
            "..11........11..",
            "...11......11...",
            "...111....111...",
            "....11111111....",
            "...1133222211...",
            "..113322222211..",
            ".11244222244211.",
            ".11244222244211.",
            ".11222222222211.",
            "..112222222211..",
            "...1122222211...",
            "....11222211....",
            ".....111111.....",
            "................",
        });

        /// <summary>ハネル — 菱形の体に、端まで届く羽。
        /// ⚠️ 最初は体を小さくして羽を離していたが、実寸(32px)で見たら
        /// 散った点にしか見えなかった。羽を端まで繋いで面で読ませる。</summary>
        private static readonly PixelSprite HaneruSprite = PixelSprite.Parse(new[]
        {
            "................",
            "................",
            "................",
            "......1111......",
            ".....112211.....",
            "....11222211....",
            "..331122221133..",
            "3333114224113333",
            "..331122221133..",
            "....11222211....",
            ".....112211.....",
            "......1111......",
            "................",
            "................",
            "................",
            "................",
        });

        // ── 仮絵（2026-08-17）─────────────────────────────
        // ⚠️ **これは仮の絵。** 手描きのイラストに差し替える前提で置いてある。
        // ⭐ 仮でも「輪郭で見分けがつく」ことだけは守る。同じ丸を色違いで並べると、
        //    種族が増えたことが画面に出ず、増やした意味が確かめられない。

        /// <summary>ノビル — 縦に長い。首が伸びている。</summary>
        private static readonly PixelSprite NobiruSprite = PixelSprite.Parse(new[]
        {
            "......1111......",
            ".....112211.....",
            ".....124421.....",
            ".....112211.....",
            "......1221......",
            "......1221......",
            "......1331......",
            ".....112211.....",
            "....11222211....",
            "...1122222211...",
            "...1222222221...",
            "...1222222221...",
            "...1122222211...",
            "....11222211....",
            ".....111111.....",
            "................",
        });

        /// <summary>ヒラベ — 平たい。横に広がって沈んでいる。</summary>
        private static readonly PixelSprite HirabeSprite = PixelSprite.Parse(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "....11111111....",
            "..112222222211..",
            ".12233222332221.",
            "1222442222442221",
            "1222222222222221",
            ".12222222222221.",
            "..112222222211..",
            "....11111111....",
            "................",
            "................",
            "................",
            "................",
        });

        /// <summary>トゲル — 全身が棘。輪郭がぎざぎざ。</summary>
        private static readonly PixelSprite TogeruSprite = PixelSprite.Parse(new[]
        {
            "................",
            "...1..1..1..1...",
            "...11.11.11.1...",
            "....111111111...",
            "..1113322221111.",
            "1.11222222221.1.",
            ".1122442244221..",
            "1112222222222111",
            ".1122222222221..",
            "1.11222222211.1.",
            "..111222221111..",
            "...111111111....",
            "...1.11.11.11...",
            "...1..1..1..1...",
            "................",
            "................",
        });

        /// <summary>マルミ — 小さくて丸い。枠の中で余白が多い。</summary>
        private static readonly PixelSprite MarumiSprite = PixelSprite.Parse(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "......1111......",
            ".....112211.....",
            "....11222211....",
            "...1122332211...",
            "...1244224421...",
            "...1222222221...",
            "....11222211....",
            ".....112211.....",
            "......1111......",
            "................",
            "................",
            "................",
        });

        /// <summary>ヌシ — 角を持つ重い体。枠いっぱいに構える。</summary>
        private static readonly PixelSprite NushiSprite = PixelSprite.Parse(new[]
        {
            "................",
            "...1........1...",
            "..121......121..",
            "..1221....1221..",
            ".11222111122211.",
            ".12222222222221.",
            ".12233222332221.",
            ".12244222442221.",
            ".12244222442221.",
            ".12222222222221.",
            ".12222222222221.",
            ".11222222222211.",
            "..112222222211..",
            "...1122222211...",
            "....11111111....",
            "................",
        });

        private static readonly Palette[] TamaruPalettes =
        {
            new Palette("#2e2418", "#8fc96e", "#c8eaa8", "#1a1410"), // 通常
            new Palette("#1c2436", "#6e9ec9", "#a8cbea", "#101418"), // 変異・蒼
            new Palette("#361c22", "#c96e7f", "#eaa8b4", "#181012"), // 変異・紅
            new Palette("#2e2a18", "#c9bd6e", "#eae0a8", "#1a1810"), // 変異・金
        };

        private static readonly Palette[] TsunogaPalettes =
        {
            new Palette("#2a1a14", "#c97a52", "#eab48c", "#160e0a"), // 通常
            new Palette("#141a2a", "#5273c9", "#8c9eea", "#0a0e16"), // 変異・蒼
            new Palette("#2a1420", "#c95293", "#ea8cc4", "#160a12"), // 変異・紅
            new Palette("#1a2a18", "#63c952", "#98ea8c", "#0e160a"), // 変異・翠
        };

        private static readonly Palette[] HaneruPalettes =
        {
            new Palette("#241c2e", "#a98fc9", "#ded0ea", "#141018"), // 通常
            new Palette("#1c2e2a", "#8fc9bd", "#d0eae4", "#101816"), // 変異・碧
            new Palette("#2e2418", "#c9b48f", "#eae0d0", "#181410"), // 変異・砂
            new Palette("#2e1c1c", "#c98f8f", "#ead0d0", "#181010"), // 変異・灰紅
        };

        /// <summary>⚠️ 仮絵のぶんのパレット。⭐ 通常＋変異2色までに留めてある
        /// （手描きに差し替えるとき、色数が少ないほうが作り直しが軽い）。</summary>
        private static readonly Palette[] NobiruPalettes =
        {
            new Palette("#1c2e24", "#6ec99a", "#a8eac8", "#101a14"),
            new Palette("#2e1c24", "#c96e9a", "#eaa8c8", "#1a1014"),
            new Palette("#2a2e18", "#b4c96e", "#dceaa8", "#181a10"),
        };

        private static readonly Palette[] HirabePalettes =
        {
            new Palette("#182a2e", "#6eb4c9", "#a8dcea", "#101a1c"),
            new Palette("#2e2818", "#c9b06e", "#eadaa8", "#1a1810"),
            new Palette("#241c2e", "#9a6ec9", "#c8a8ea", "#141018"),
        };

        private static readonly Palette[] TogeruPalettes =
        {
            new Palette("#2e1818", "#c96e6e", "#eaa8a8", "#1a1010"),
            new Palette("#18182e", "#6e6ec9", "#a8a8ea", "#10101a"),
            new Palette("#1c2e18", "#7ec96e", "#b4eaa8", "#101a10"),
        };

        private static readonly Palette[] MarumiPalettes =
        {
            new Palette("#2e2a20", "#e0d0a8", "#f4ecd0", "#1a1810"),
            new Palette("#202a2e", "#a8d0e0", "#d0ecf4", "#10181a"),
            new Palette("#2e2028", "#e0a8c4", "#f4d0e4", "#1a1014"),
        };

        /// <summary>⚠️ ボスは重く見せたいので、明部を抑えて沈んだ色にする。</summary>
        private static readonly Palette[] NushiPalettes =
        {
            new Palette("#14100c", "#6b5a3e", "#9c8759", "#e8d48a"), // 通常（目だけ光る）
            new Palette("#0c1014", "#3e556b", "#59839c", "#8ac8e8"), // 変異・蒼
        };

        /// <summary>⚠️ スキル1 のスケール元をわざと散らしてある（防御 / 攻撃 / 攻撃だが全体攻撃）。
        /// 全種族が同じステでスケールすると、そのステだけが二重に得になる。</summary>
        private static readonly Species[] List =
        {
            new Species("tamaru", "タマル", "attack-def", // 防御スケール
                new StatBlock(24, 18, 22, 16), TamaruSprite, TamaruPalettes,
                // 守りの系統
                new[] { "def-up", "taunt", "shield", "heal-ratio", "guts", "attack", "ct-long" }),

            new Species("tsunoga", "ツノガ", "attack", // 攻撃スケール・単体
                new StatBlock(22, 24, 18, 16), TsunogaSprite, TsunogaPalettes,
                // 攻めの系統
                new[] { "atk-up", "def-down", "attack-heavy", "ct-short", "poison", "attack-def", "stun" }),

            // ⚠️ 枠1 は "attack-all" だった。⭐ **枠1＝通常攻撃**と定めたので差し替えた（2026-08-17）。
            //    全体攻撃が CT 0 で毎手番飛ぶのは通常攻撃ではないし、実害も出た
            //    （「手数」の特性が対象数ぶん効いて、毎行動 CT が3ずつ減っていた）。
            //    ⭐ 全体攻撃はガチャのプールへ移したので、枠2・3 では今までどおり手に入る。
            // ⚠️ 枠1 を素の「攻撃」にしたら**総合 16.8%** まで落ちた（実測）。
            //    全体攻撃を取り上げたぶんの埋め合わせが無く、攻撃18・防御16 の低さだけが残った。
            // ⭐ 速い種族の通常攻撃として「連撃」に寄せる。多段なので盾にも強く、
            //    速さと噛み合う。⚠️ 枠1 は他種族と重複してよい（nobiru と同じ技）。
            new Species("haneru", "ハネル", "attack-twice", // 速さで手数を稼ぐ
                new StatBlock(20, 18, 16, 26), HaneruSprite, HaneruPalettes,
                // 撹乱の系統
                // ⚠️ **ここには足さない。**既にある4種のプールは凍結（乱数で引く対象なので、
                //    足すと卵の技の列がずれて照合が落ちる）。⭐ 全体攻撃は marumi のプールへ移した
                new[] { "spd-up", "spd-down", "atk-down", "stun", "regen", "ct-long", "immune" }),

            // ── 増やしたぶん（2026-08-17）。⚠️ 絵は仮 ─────────────
            // ⭐ 基礎値の合計は全種族 80 で揃える（差は配分だけ）。
            // ⭐ 枠1のスケール元を散らす。防御スケールが増えすぎると防御が二重に得になる
            //    （実測で「防御に寄せる型」が突出していた）。新しい4種は攻撃寄りにしてある。
            // ⚠️ 新しい技は**新しい種族のプールへ**入れる。既にある4種のプールは凍結
            //    （乱数で引く対象なので、足すと卵の技がずれて照合が落ちる）。

            new Species("nobiru", "ノビル", "attack-twice", // 多段・攻撃スケール
                new StatBlock(18, 22, 16, 24), NobiruSprite, NobiruPalettes,
                new[] { "dash", "curse", "attack-thrice", "spd-up", "venom-fang", "ct-lock" }),

            // ⚠️ 最初は枠1を防御スケールにしていたら、総合勝率 81.1% で突出した。
            //    防御寄りの配分と防御スケールが重なって**防御を二重に得**にしていた
            //    （この罠は上の注意書きどおり）。攻撃スケールに変え、低い攻撃を弱点として効かせる。
            new Species("hirabe", "ヒラベ", "attack", // 攻撃スケール。硬いが攻めは細い
                new StatBlock(26, 14, 26, 14), HirabeSprite, HirabePalettes,
                new[] { "harden", "bulwark", "attack-def-twice", "heal-big", "shield-wall", "guts-deep" }),

            // ⚠️ 枠1 は "venom-fang" だった。⭐ haneru と同じ理由で差し替え。
            //    ダメージ＋高確率の毒が CT 0 で毎手番入るのは通常攻撃ではない。
            new Species("togeru", "トゲル", "attack", // 削って待つ（毒は枠2・3 で）
                new StatBlock(20, 24, 20, 16), TogeruSprite, TogeruPalettes,
                new[] { "venom-heavy", "crush", "attack-twice", "curse", "stun-heavy", "atk-up",
                    "venom-fang" }),

            // ⭐ 全体に効かせる系（鎮めの風・全体攻撃）はここが持つ。
            //    ⚠️ haneru の枠1 から外した全体攻撃の受け皿（あちらのプールは凍結されている）
            new Species("marumi", "マルミ", "attack", // 素直。支える側
                new StatBlock(24, 16, 18, 22), MarumiSprite, MarumiPalettes,
                new[] { "heal-big", "heal-miracle", "slow-all", "immune-long", "dash", "shield",
                    "attack-all" }),

            // ⚠️ ボス専用。巣は持たないので卵からは出ない
            // ⚠️ 3すくみは 炎 → 木 → 水 → 炎。水に有利を取るのは木（ハネル）。
            //    ここを読み違えて検証編成を組み、測り損ねたことがある。
            // ⚠️ 枠1は CT が無いので、大技を置くと毎回撃ててしまう。
            //    震撼（CT7 の全体大技）は枠2へ回し、ここは中程度に留める。
            new Species("nushi", "ヌシ", "attack-def",
                new StatBlock(26, 20, 24, 10), NushiSprite, NushiPalettes,
                // ⚠️ 卵は落とさないが、数える検査が「プールが無い」で落ちるので置く
                new[] { "def-up", "spd-down", "taunt", "guts", "immune", "attack-all-heavy" }),
        };

        public static IReadOnlyList<Species> All => List;

        private static readonly Dictionary<string, Species> Index = BuildIndex();

        private static Dictionary<string, Species> BuildIndex()
        {
            var map = new Dictionary<string, Species>(List.Length);
            foreach (var species in List) map.Add(species.Id, species);
            return map;
        }

        /// <summary>表にあるか。⚠️ 投げずに聞けるのは**セーブの読み込み**のためだけ。
        /// 遊びの最中は <see cref="ById"/> を使う（知らない id は投げるべき）。</summary>
        public static bool Has(string id) => Index.ContainsKey(id);

        /// <summary>読めない種族 id が来たときの置き換え先。
        /// ⚠️ 見た目も属性も変わってしまうが、**セーブが開かないよりはましだ**という判断。
        /// ⭐ 置き換えたことは <see cref="Snapshots.Load"/> が記録に残す。</summary>
        public static Species Fallback => List[0];

        /// <summary>表に無い id を黙って握りつぶさない。
        /// ⚠️ 「型は通る・テストも通る・ただ効かなくなるだけ」が一番気づけない形なので、必ず投げる。</summary>
        public static Species ById(string id)
        {
            Species? species;
            if (!Index.TryGetValue(id, out species)) throw new ArgumentException($"種族表に {id} が無い");
            return species!;
        }

        /// <summary>全部を覆うつもりの表は、数える検査を持つ。
        /// 起動時に1回走らせ、種族を足した日に黙って壊れないようにする。</summary>
        public static void Audit()
        {
            var problems = new List<string>();

            foreach (var species in List)
            {
                int total = Stats.TotalOf(species.Base);
                if (total != BaseTotal)
                {
                    problems.Add($"{species.Id}: 基礎値の合計が {total}（{BaseTotal} に揃える）");
                }
                foreach (var key in Stats.Keys)
                {
                    if (species.Base[key] < 0)
                    {
                        problems.Add($"{species.Id}: 基礎値 {key} が {species.Base[key]}");
                    }
                }
                if (species.Palettes.Count == 0)
                {
                    problems.Add($"{species.Id}: パレットが無い");
                }
                if (species.Sprite.Width != 16 || species.Sprite.Height != 16)
                {
                    problems.Add(
                        $"{species.Id}: 意匠が {species.Sprite.Width}×{species.Sprite.Height}（16×16 に揃える）");
                }

                // ⚠️ 枠1は CT が無いので毎回撃てる。大技を置くと壊れる。
                //    実際にヌシの枠1へ震撼（全体・大）を置いてしまい、決着が8行動になった。
                var first = Skills.ById(species.Skill1);
                foreach (var effect in first.Effects)
                {
                    if (effect.Kind != EffectKind.Damage) continue;
                    if (effect.Power == PowerTier.Large || effect.Power == PowerTier.Huge)
                    {
                        problems.Add(
                            $"{species.Id}: 枠1の「{first.Name}」が威力{Skills.LabelOf(effect.Power)}。" +
                            "枠1は CT が無いので 小〜中 に留める");
                    }
                }
            }

            var ids = new HashSet<string>();
            foreach (var species in List) ids.Add(species.Id);
            if (ids.Count != List.Length) problems.Add("種族 id が重複している");

            // ⚠️ 「属性が3すくみを覆えているか」はもう数えない。
            //    属性は種族ではなく個体が持つので、どの種族からも3属性すべてが生まれる。

            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    "種族表の不備:\n  " + string.Join("\n  ", problems));
            }
        }
    }
}
