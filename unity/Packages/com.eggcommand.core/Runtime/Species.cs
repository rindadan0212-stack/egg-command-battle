#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>3すくみ。牙 → 羽 → 鱗 → 牙。
    /// （牙は羽を裂き / 羽は鱗をかわし / 鱗は牙を弾く）</summary>
    public enum Element
    {
        Fang,
        Plume,
        Scale,
    }

    public sealed class Species
    {
        public readonly string Id;
        public readonly string Name;
        public readonly Element Element;
        /// <summary>種族固定のスキル枠1。</summary>
        public readonly string Skill1;
        /// <summary>⚠️ 合計は <see cref="SpeciesTable.BaseTotal"/> に揃える。差は配分で出す。</summary>
        public readonly StatBlock Base;
        public readonly PixelSprite Sprite;
        /// <summary>0 = 通常。1以降が変異色（ARK と同じく変異は色変化として出る）。</summary>
        public readonly IReadOnlyList<Palette> Palettes;

        public Species(string id, string name, Element element, string skill1, StatBlock baseStats,
            PixelSprite sprite, IReadOnlyList<Palette> palettes)
        {
            Id = id;
            Name = name;
            Element = element;
            Skill1 = skill1;
            Base = baseStats;
            Sprite = sprite;
            Palettes = palettes;
        }
    }

    /// <summary>種族は「器」。中身（ステの野生レベル・スキル2/3）は種族から独立して流通する。
    ///
    /// | 種族が決めるもの | 種族から独立して流通するもの |
    /// |---|---|
    /// | 見た目（ドット + パレット） | ステの野生レベル |
    /// | 属性（3すくみ） | スキル2・3 |
    /// | スキル1（種族固定枠） | 育成で振った分 |
    /// | 基礎値の**配分** | |
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
        public static readonly Element[] Elements = { Element.Fang, Element.Plume, Element.Scale };

        public static string LabelOf(Element element)
        {
            switch (element)
            {
                case Element.Fang: return "牙";
                case Element.Plume: return "羽";
                case Element.Scale: return "鱗";
                default: throw new ArgumentOutOfRangeException(nameof(element));
            }
        }

        /// <summary>有利を取る相手。</summary>
        public static Element Beats(Element element)
        {
            switch (element)
            {
                case Element.Fang: return Element.Plume;
                case Element.Plume: return Element.Scale;
                case Element.Scale: return Element.Fang;
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
            new Species("tamaru", "タマル", Element.Scale,
                "attack-def", // 防御スケール
                new StatBlock(24, 18, 22, 16), TamaruSprite, TamaruPalettes),

            new Species("tsunoga", "ツノガ", Element.Fang,
                "attack", // 攻撃スケール・単体
                new StatBlock(22, 24, 18, 16), TsunogaSprite, TsunogaPalettes),

            new Species("haneru", "ハネル", Element.Plume,
                "attack-all", // 攻撃スケール・全体（全体なので威力は小）
                new StatBlock(20, 18, 16, 26), HaneruSprite, HaneruPalettes),

            // ⚠️ ボス専用。巣は持たないので卵からは出ない
            // ⚠️ 3すくみは 牙 → 羽 → 鱗 → 牙。鱗に有利を取るのは羽（ハネル）。
            //    ここを「牙が有利」と読み違えて検証編成を組み、測り損ねた。
            // ⚠️ 枠1は CT が無いので、大技を置くと毎回撃ててしまう。
            //    震撼（CT7 の全体大技）は枠2へ回し、ここは中程度に留める。
            new Species("nushi", "ヌシ", Element.Scale,
                "attack-def",
                new StatBlock(26, 20, 24, 10), NushiSprite, NushiPalettes),
        };

        public static IReadOnlyList<Species> All => List;

        private static readonly Dictionary<string, Species> Index = BuildIndex();

        private static Dictionary<string, Species> BuildIndex()
        {
            var map = new Dictionary<string, Species>(List.Length);
            foreach (var species in List) map.Add(species.Id, species);
            return map;
        }

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

            // 属性が3すくみを覆えているか。⚠️ 覆えていないと、有利不利が一方通行になる
            var covered = new HashSet<Element>();
            foreach (var species in List) covered.Add(species.Element);
            var missing = new List<string>();
            foreach (var element in Elements)
            {
                if (!covered.Contains(element)) missing.Add(LabelOf(element));
            }
            if (missing.Count > 0 && List.Length >= Elements.Length)
            {
                problems.Add($"使われていない属性: {string.Join(", ", missing)}");
            }

            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    "種族表の不備:\n  " + string.Join("\n  ", problems));
            }
        }
    }
}
