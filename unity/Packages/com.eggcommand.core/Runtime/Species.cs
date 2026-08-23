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

        /// <summary>⭐ **この種族の特性**（<see cref="Traits"/> の id）。
        ///
        /// ⚠️ 2026-08-21 まで**個体ごとに引いていた**（作者の指摘で戻した）。
        /// 引いていた頃の問題は、種族が「見た目と骨格」でしかなくなること ──
        /// 同じツノガでも中身が14通りあるので、⭐ **顔を見ても何をする相手か分からない**。
        /// 特性を種族に貼ると、巣を選ぶ理由（どの技袋が欲しいか）に
        /// 「どの特性が欲しいか」が重なって、1つの判断になる。
        ///
        /// ⚠️ **null にしない。**持たない種族を作ると、そこだけ特性の軸が消える。
        /// ⚠️ 種族どうしで**重ねない**（<see cref="Faults"/> が落とす）。</summary>
        public readonly string TraitId;
        /// <summary>⚠️ 合計は <see cref="SpeciesTable.BaseTotal"/> に揃える。差は配分で出す。</summary>
        public readonly StatBlock Base;
        public readonly PixelSprite Sprite;
        /// <summary>0 = 通常。1以降が変異色（ARK と同じく変異は色変化として出る）。</summary>
        public readonly IReadOnlyList<Palette> Palettes;
        /// <summary>枠2 に出うる技。⭐ **型ごと**に決める。</summary>
        public readonly SkillPool Slot2;
        /// <summary>枠3 に出うる技。⚠️ 枠2 と**別の型**にする（同じ型だと分けた意味が無い）。</summary>
        public readonly SkillPool Slot3;

        public Species(string id, string name, string skill1, string traitId, StatBlock baseStats,
            PixelSprite sprite, IReadOnlyList<Palette> palettes, SkillPool slot2, SkillPool slot3)
        {
            Id = id;
            Name = name;
            Skill1 = skill1;
            TraitId = traitId;
            Base = baseStats;
            Sprite = sprite;
            // 🔴 **解決は組み立てのときに1度だけ**（種族表は static readonly で1回しか作らない）。
            //    ⚠️ 変異パレットの null（「通常色のまま」）はここで消える ── 以降、
            //    `Palettes[i].Colors` を直に読む側（帳面・PNG書き出し・検査）は
            //    1文字も変えなくてよい。
            Palettes = Palette.ResolveGroup(palettes);
            Slot2 = slot2;
            Slot3 = slot3;
        }
    }

    /// <summary>1つの枠に出うる技。⭐ **型と中身をセットで宣言する。**
    ///
    /// ⚠️ 型を書かずに一覧だけ置くと、あとから技を足したときに
    /// 「支える枠に殴る札が混ざる」ことに気づけない。<see cref="SpeciesTable.Audit"/> が型を照合する。
    ///
    /// ⚠️ 種族ごとに中身を分けるのは、どこで卵を奪っても同じ技が出ると
    /// 「必要な技を持つ親の巣へ行く」という輪の駆動力が消えるため。</summary>
    /// <summary>1つの枠に出うる技。⭐ **その枠だけの、手で選んだ並び。**
    ///
    /// ⚠️ **型（アタック/サポート/…）で縛るのをやめた**（2026-08-19・作者の判断）。
    /// 縛っていた頃の問題:
    /// <list type="bullet">
    /// <item>新しい技は「同じ型の枠」にしか入れられない。アタックの受け皿は **6枠**、
    ///   ヒールに至っては **4枠**しか無く、技を10足すと1袋あたり +1.7〜2.5件も太った
    ///   （型を外すと受け皿は22枠になり、同じ10件が **+0.45件** で収まる）</item>
    /// <item>受け皿が足りないので**同じ技を何種族にも配る**ことになり、
    ///   56技のうち **21件が複数の袋**に居た（`heal-ratio` は4か所）。
    ///   ⚠️ 「この技が欲しいからこの巣へ」という動機を、型固定が自分で薄めていた</item>
    /// <item>4型から2つ選ぶ組み合わせは **6通りしかない**。種族が増えると顔が必ず被る</item>
    /// </list>
    ///
    /// ⭐ 型は**中身から読み取る注記**へ降りた（<see cref="Skills.FlavorOf"/>）。
    /// ⚠️ 縛りが無くなったぶん、代わりに <see cref="Skills.Faults"/> が4つ数える ──
    /// 袋の大きさ / 1技が入れる袋の数 / 枠2と枠3の重なり / 役割が1つに偏っていないか。</summary>
    public sealed class SkillPool
    {
        public readonly IReadOnlyList<string> Pool;

        public SkillPool(params string[] pool)
        {
            Pool = pool;
        }
    }

    /// <summary>種族は「器」。中身（ステの野生レベル・スキル2/3）は種族から独立して流通する。
    ///
    /// | 種族が決めるもの | 種族から独立して流通するもの |
    /// |---|---|
    /// | 見た目（ドット + パレット） | ステの野生レベル |
    /// | スキル1（種族固定枠） | **属性**（3すくみ） |
    /// | **特性**（2026-08-21・作者の指示） | 得意・不得意 |
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

        /// <summary>変わった色が出る確率。⭐ **卵1つにつき1回**（2026-08-21・作者の指示）。
        ///
        /// ⚠️ 2026-08-21 まで「配合で変異が出たときだけ」だった。⭐ そのため
        /// **巣で拾った卵は一生ふつうの色**で、色が配合の副産物でしかなかった。
        /// ⭐ 孵るたびに引くようにすると、拾った卵にも「開けてみるまで分からない」が乗る。
        ///
        /// ⚠️ 代（世代）とは関係しない ── 深い血統ほど出る、にはしない。
        /// 深いほど出るなら、それは結局「配合の副産物」に戻る。</summary>
        public const double VariantChance = 0.05;

        /// <summary>その種族の色を1つ引く。⭐ 0 は通常色、1以降が変わった色。
        ///
        /// ⚠️ **専用の系統（RngPalette）で引くこと。**⭐ 孵化の系統に混ぜると
        /// 技のガチャの列がずれて、較正済みの検査が無効になる。</summary>
        public static int RollPalette(Rng rng, string speciesId)
        {
            var species = ById(speciesId);
            if (species.Palettes.Count < 2) return 0;
            if (!rng.Chance(VariantChance)) return 0;
            return rng.Int(1, species.Palettes.Count);
        }

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

        /// <summary>⚠️ 全種族で揃える基礎値の合計。ここを種族ごとに変えない。
        ///
        /// ⭐ **80 → 120**（2026-08-19・作者の判断）。
        /// 弱化命中・弱化耐性が全種族 0 だったので、育成の同じ ＋20 が
        /// 他ステの倍の倍率（+115% 対 +52〜63%）で効いていた（実測）。
        /// ⚠️ 既存の4本は**1つも触っていない** ── 触ると戦闘の較正が丸ごと動く。
        /// 新しい 40 を弱化命中・弱化耐性へ配っただけ。
        /// ⭐ 配分は種族の性格で決めた（攻めは命中寄り・守りは耐性寄り）。合計は必ず 40。</summary>
        public const int BaseTotal = 120 * Stats.Scale;

        /// <summary>弱化命中＋弱化耐性の合計。⭐ **全種族で同じ。**差は配分で出す。
        /// ⚠️ 移植元は4本ぶん（80 × <see cref="Stats.Scale"/>）だけで、この2本は 2026-08-19 に足した。
        /// ⭐ ここを揃えておけば、移植元の4本ぶんも自動的に全種族で揃う。</summary>
        public const int DebuffBaseTotal = 40 * Stats.Scale;

        // ── 意匠 ───────────────────────────────────────────
        // 文字の格子で持つ。テキストのまま人が手で直せる。
        // 1=輪郭 2=体 3=差し色 4=目

        /// <summary>タマル — 殻をかぶった緑の子。⭐ **作者が描いたもの**（2026-08-21 に差し替え）。
        ///
        /// ⚠️ ここだけ **64×64・11色**。他の10種族は 16×16・4色のまま。
        /// ⭐ 画面はどれも同じ枠に伸ばして描くので、大きさが違っても並びは崩れない
        /// ── 変わるのは細かさだけ。
        ///
        /// ⚠️ **手で直さない。**元の絵（`S__4055055.jpg` / 512×512・8px 格子）から
        /// 落としたもの。直すなら元の絵を直して、また落とすこと。
        ///
        /// 添字: 1=体 2=殻 3=体の陰 4=殻の斑 5=殻の中間 6=印
        ///       7=刃 8=柄 9=背びれ a=目 b=光</summary>
        private static readonly PixelSprite TamaruSprite = PixelSprite.Parse(new[]
        {
            "............................22222222222222......................",
            "..........................222222222222222222....................",
            "........................222242222222224422222...................",
            ".......................22224442222222444222222..................",
            "......................2222244422222224422222222.................",
            ".....................222224444222222222222266622................",
            "....................22222244422222222222222666222...............",
            "...................2222222222222222222222666666622..............",
            "..................222222222222222222222226666666222.............",
            "..................222442222222222222222226666666222.............",
            "..................2244422222222242222222222666222224............",
            ".................222444422222224422222222226662222442...........",
            ".................222244422222222222222222222222222442...........",
            ".................222244422222222222222224422222222442...........",
            ".................222222222222242222222244422222222222...........",
            ".................222222222222444222222224222222222222...........",
            ".................442222222224444222222222222244222222...........",
            ".................44422222222444423222222232244422222............",
            "..................4442233222444333322222333244423222............",
            "..................444233322224333333222331334423332.............",
            "...................4233133222333113332331113323133..............",
            "...................23311133233111111aa311111331aa3..............",
            "....................311111333111111aaaa11111113333333...........",
            "....................1111111111111111aa1111133311111113..........",
            "....................11111111111111111111133111111111113.........",
            "....................111111111111111111113111113311113113........",
            "...................9111131111111111111111111111131131113........",
            "..................99111131111111111111111111111111111113........",
            "...................9111131111133111111111111111111111113........",
            "....................111133111111311111111111111111111113........",
            "....................111133111111131111111111111111111113........",
            "...................9111133111111113111111111111111111113........",
            "..................991111331111111131111b1111111111111113........",
            "...................9111133111111111311bb1111111111111113........",
            "....................111133111111111311bb1111111111111113........",
            "....................11113311111111133333333333333111113.........",
            "....................1111111111111111111111111111133333..........",
            "...................91111113311111111111111111111111113..........",
            "..................99111111133333333333333333333333333...........",
            "...................9111111113333333333335555555555..............",
            "....................111111113111111111112222222222..............",
            "....................111111131111311111115552222555..............",
            "....................111111311113311111112225555222..............",
            "....3............991111113111133111111112222222222..............",
            "...313...........911111113111331111111112222222222..............",
            "...3113..........111111131111331111111112222222222..............",
            "...31113..9...991111111131111331111771115552222555..............",
            "....3111399...911111111131111331111177112225555222..............",
            "....3111111333111111111113111133111177712222222222..............",
            "....3311111111111111111131111133111177712222222222..............",
            "....3311111111111111111311111113311177712222222222..............",
            ".....33111111111888888831111111388887788555222255...............",
            ".....3311111111388888883111111138888778822255552................",
            "......331111111333333333311111333333777122222222................",
            "......33111111111111111113111333113377722222222.................",
            ".......331111111111111113133333111137772222222..................",
            "........3311111111111111311111111113775552222...................",
            ".........331111111111111331111111117722225555...................",
            "..........3311111111112523111111111352222222....................",
            "...........33111111522225331111111335222222.....................",
            "............33332222522252331111113555555.......................",
            ".............3355555555555333111133333333.......................",
            ".........................333311111133333333.....................",
            "..........................333111111113333333....................",
        });

        /// <summary>キバネ — 尖った耳と、下に覗く牙。止める側の顔。</summary>
        private static readonly PixelSprite KibaneSprite = PixelSprite.Parse(new[]
        {
            "................",
            "...11......11...",
            "..1221....1221..",
            "..122211112221..",
            ".11222222222211.",
            ".12233222222221.",
            ".12222222222221.",
            ".12244222244221.",
            ".12222222222221.",
            ".12221111112221.",
            ".11222222222211.",
            "..112222222211..",
            "...1122222211...",
            "....11111111....",
            "................",
            "................",
        });

        /// <summary>イワオ — 角の立った岩。丸みが無いので重く見える。</summary>
        private static readonly PixelSprite IwaoSprite = PixelSprite.Parse(new[]
        {
            "................",
            "................",
            "..111111111111..",
            ".11222222222211.",
            ".12233222222221.",
            ".12222222222221.",
            ".12244222244221.",
            ".12222222222221.",
            ".12222222222221.",
            ".12222222222221.",
            ".11222222222211.",
            "..111111111111..",
            "................",
            "................",
            "................",
            "................",
        });

        /// <summary>ホムラ — 上へ細る炎。速さと支えの顔。</summary>
        private static readonly PixelSprite HomuraSprite = PixelSprite.Parse(new[]
        {
            "................",
            ".......11.......",
            "......1221......",
            ".....122221.....",
            "....12233221....",
            "...1222222221...",
            "..122222222221..",
            ".12244222244221.",
            ".12222222222221.",
            ".12222222222221.",
            ".11222222222211.",
            "..112222222211..",
            "...1122222211...",
            "....11111111....",
            "................",
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

        /// <summary>タマルの色。⚠️ **11色ぶん**（意匠が 64×64 になったため）。
        ///
        /// ⭐ 変異色は通常色の**色相を回しただけ**（蒼 +162° / 紅 +259° / 金 +47°）。
        /// ⚠️ 無彩色（目の黒・刃の灰）は回さない ── 回すと目が色づいて顔が濁る。
        /// ⭐ **回さない2色（7番目=刃・10番目=目）は null にして通常色を受け継ぐ**
        /// （2026-08-23・null 対応）。⚠️ 光（11番目=白）は回さないのに 4通りとも
        /// 微妙に違う値（`#fefeff` / `#fffffe` / `#fefffe` / `#fffeff`）── 意図か
        /// 事故か分からないので**そのまま残した**（勝手に統一しない）。
        /// ⭐ 手で置き直してよいが、**11色すべて**揃えること（足りないと描いた瞬間に落ちる）。</summary>
        private static readonly Palette[] TamaruPalettes =
        {
            new Palette("#00fe01", "#fefc01", "#00c862", "#8c8504", "#c3bc01", "#ff7e00", "#7f807f", "#553e00", "#b31a00", "#000000", "#fefeff"), // 通常
            new Palette("#b300fe", "#014ffe", "#c800a2", "#04348c", "#0142c3", "#00cdff", null, "#003155", "#00b397", null, "#fffffe"), // 変異・蒼
            new Palette("#fe5200", "#fe01af", "#c8a200", "#8c0467", "#c3018c", "#d000ff", null, "#550051", "#5300b3", null, "#fefffe"), // 変異・紅
            new Palette("#00fec7", "#3bfe01", "#0092c8", "#298c04", "#33c301", "#b9ff00", null, "#2a5500", "#b3a600", null, "#fffeff"), // 変異・金
        };

        private static readonly Palette[] KibanePalettes =
        {
            new Palette("#241a2e", "#9a7ec9", "#c6b0ea", "#140f1a"), // 通常
            new Palette("#1c2436", "#6e9ec9", "#a8cbea", "#101418"), // 変異・蒼
            new Palette("#361c22", "#c96e7f", "#eaa8b4", "#181012"), // 変異・紅
            new Palette("#2e2a18", "#c9bd6e", "#eae0a8", "#1a1810"), // 変異・金
        };

        private static readonly Palette[] IwaoPalettes =
        {
            new Palette("#22201c", "#8f8a7e", "#c2bdb0", "#141310"), // 通常
            new Palette("#1c2436", "#6e9ec9", "#a8cbea", "#101418"), // 変異・蒼
            new Palette("#361c22", "#c96e7f", "#eaa8b4", "#181012"), // 変異・紅
            new Palette("#2e2a18", "#c9bd6e", "#eae0a8", "#1a1810"), // 変異・金
        };

        private static readonly Palette[] HomuraPalettes =
        {
            new Palette("#2e1a14", "#e08a4e", "#f5c48c", "#1a0f0a"), // 通常
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
            // ⭐ **枠2 と枠3 は別の型から引く**（2026-08-18）。
            // ⚠️ 同じプールから2つ取っていた頃は、狙った組み合わせが 2.8〜4.8% でしか出ず、
            //    しかも「この巣からは何が来るか」が読めなかった。
            // ⭐ 型を種族の看板にすると、巣を選ぶ理由が「どの型が欲しいか」になる。
            // ⚠️ 型は技に手で書かない（Skills.TypeOf が効果から導く）。ここで嘘を書くと Audit が落とす。

            new Species("tamaru", "タマル", "attack-def", Traits.Grit, // 防御スケール
                new StatBlock(120, 90, 110, 80, 90, 110), TamaruSprite, TamaruPalettes,
                // 守り ── 固めて、癒す
                new SkillPool("shield", "guts", "harden"),
                new SkillPool("regen", "heal-big")),

            new Species("tsunoga", "ツノガ", "attack", Traits.Pursuit, // 攻撃スケール・単体
                new StatBlock(110, 120, 90, 80, 120, 80), TsunogaSprite, TsunogaPalettes,
                // 攻め ── 殴って、崩す
                new SkillPool("attack-heavy", "attack-def", "attack-def-twice", "attack-all"),
                new SkillPool("def-down", "poison", "stun", "atk-down")),

            // ⚠️ 枠1 は "attack-all" だった。⭐ **枠1＝通常攻撃**と定めたので差し替えた（2026-08-17）。
            //    全体攻撃が CT 0 で毎手番飛ぶのは通常攻撃ではないし、実害も出た
            //    （「手数」の特性が対象数ぶん効いて、毎行動 CT が3ずつ減っていた）。
            new Species("haneru", "ハネル", "attack-twice", Traits.Aim, // 速さで手数を稼ぐ
                new StatBlock(100, 90, 80, 130, 120, 80), HaneruSprite, HaneruPalettes,
                // 撹乱 ── 止めて、逃げる
                new SkillPool("spd-down", "atk-down", "ct-long", "taunt", "bulwark"),
                new SkillPool("spd-up", "immune", "ct-short", "dash")),

            // ── 増やしたぶん（2026-08-17）。⚠️ 絵は仮 ─────────────
            // ⭐ 基礎値の合計は全種族 120 で揃える（差は配分だけ）。

            new Species("nobiru", "ノビル", "attack-twice", Traits.Flurry, // 多段・攻撃スケール
                new StatBlock(90, 110, 80, 120, 110, 90), NobiruSprite, NobiruPalettes,
                // 削り ── 手数で押して、自分を速くする
                new SkillPool("attack-thrice", "pierce-strike", "venom-fang"),
                new SkillPool("dash", "spd-up", "gauge-boost", "atk-up")),

            // ⚠️ 最初は枠1を防御スケールにしていたら、総合勝率 81.1% で突出した。
            //    防御寄りの配分と防御スケールが重なって**防御を二重に得**にしていた。
            new Species("hirabe", "ヒラベ", "attack", Traits.Stubborn, // 攻撃スケール。硬いが攻めは細い
                new StatBlock(130, 70, 130, 70, 70, 130), HirabeSprite, HirabePalettes,
                // 壁 ── 立て直して、耐える
                new SkillPool("heal-big", "revive"),
                new SkillPool("shield-wall", "guts-deep", "immune-long")),

            new Species("togeru", "トゲル", "attack", Traits.Surge, // 削って待つ
                new StatBlock(100, 120, 100, 80, 130, 70), TogeruSprite, TogeruPalettes,
                // 毒 ── 弱らせて、抜く
                new SkillPool("venom-heavy", "curse", "sleep", "stun-heavy", "gauge-drain"),
                new SkillPool("attack-twice", "crush", "venom-fang")),

            new Species("marumi", "マルミ", "attack", Traits.Opener, // 素直。支える側
                new StatBlock(120, 80, 90, 110, 80, 120), MarumiSprite, MarumiPalettes,
                // 支え ── 癒して、剥がす
                new SkillPool("heal-ratio", "heal-miracle", "regen"),
                new SkillPool("slow-all", "dispel", "block", "ct-lock", "buff-steal")),

            // ── 特性が14件になったので、顔ぶれを3つ足した（2026-08-19・作者の指示）──
            // ⭐ **足した3種族が、それまで配れていなかった技10件の行き先**にもなっている。
            //    ⚠️ 既存7種族のプールは1文字も触っていない（既に較正済みのため）。
            // ⭐ それぞれ、盤面を見る新しい特性のどれかと噛み合う顔にしてある:
            //    キバネ＝止める（不意打ち）/ イワオ＝崩れてから（遺志）/ ホムラ＝速さを配る

            new Species("kibane", "キバネ", "attack-twice", Traits.Ambush, // 手数で通す
                new StatBlock(90, 110, 80, 120, 130, 70), KibaneSprite, KibanePalettes,
                // 止める ── 眠らせ、痺れさせ、縛る
                new SkillPool("stun-strike", "taunt-long", "poison-all", "stun", "sleep"),
                new SkillPool("strip-strike", "pierce-strike")),

            new Species("iwao", "イワオ", "attack-def", Traits.Legacy, // 硬さがそのまま火力
                new StatBlock(140, 85, 115, 60, 90, 110), IwaoSprite, IwaoPalettes,
                // 重い ── 一撃が遠く、代わりに深い
                new SkillPool("pierce-strike-heavy", "attack-all-twice", "attack-heavy", "crush"),
                new SkillPool("guts", "harden", "def-up", "shield")),

            new Species("homura", "ホムラ", "attack", Traits.Parting, // 素直に速い
                new StatBlock(110, 95, 80, 115, 95, 105), HomuraSprite, HomuraPalettes,
                // 配る ── 速さとゲージを味方へ
                new SkillPool("tailwind", "gauge-boost-heavy", "def-up"),
                new SkillPool("regen-heavy", "revive-heavy", "heal-ratio")),

            // ⚠️ ボス専用。巣は持たないので卵からは出ない
            // ⚠️ 枠1は CT が無いので、大技を置くと毎回撃ててしまう。
            //    震撼（CT7 の全体大技）は枠2へ回し、ここは中程度に留める。
            new Species("nushi", "ヌシ", "attack-def", Traits.Desperation,
                new StatBlock(130, 100, 120, 50, 100, 100), NushiSprite, NushiPalettes,
                new SkillPool("attack-all-heavy"),
                new SkillPool("spd-down", "taunt")),
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
        /// <summary>表の不備を投げる。⚠️ 起動時に呼ぶ。</summary>
        public static void Audit()
        {
            var problems = Faults();
            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    "種族表の不備:\n  " + string.Join("\n  ", problems));
            }
        }

        /// <summary>不備を**投げずに数える**。⭐ 帳面が貼る前に言うための口。</summary>
        /// <summary>いまの表の不備。⭐ 起動時の <see cref="Audit"/> が使う。</summary>
        public static List<string> Faults() => Faults(List, Skills.All);

        /// <summary>**渡された表**の不備。
        ///
        /// ⭐ **帳面が「貼ったらどうなるか」を先に言うための口**（2026-08-19）。
        /// ⚠️ **規則をここ以外に書き写さない。**写した瞬間から片方が古くなる
        /// ── この道具は同じ形の食い違いを何度も踏んでいる。
        /// ⭐ 世界の状態は触らない（表を引数で受けるので、検査中に遊びが影響を受けない）。</summary>
        public static List<string> Faults(IReadOnlyList<Species> table, IReadOnlyList<Skill> skillTable)
        {
            var problems = new List<string>();

            // ⭐ **特性は種族に1つずつ。**⚠️ 重ねない ── 重ねると、そのぶん
            //    どこからも手に入らない特性が増える（気づけないまま死蔵する）。
            var traitOwner = new Dictionary<string, string>();
            foreach (var species in table)
            {
                if (!Traits.Has(species.TraitId))
                {
                    problems.Add($"{species.Id}: 特性 {species.TraitId} が表に無い");
                    continue;
                }
                string? owner;
                if (traitOwner.TryGetValue(species.TraitId, out owner))
                    problems.Add($"{species.Id}: 特性 {species.TraitId} は {owner} と重なっている");
                else traitOwner.Add(species.TraitId, species.Id);
            }

            // ⚠️ **配れていない特性を黙って増やさない。**⭐ 技の Undistributed と同じ約束
            //    （表にあるのに誰も持てないものは、名指しで宣言しておく）。
            foreach (var trait in Traits.All)
            {
                bool owned = traitOwner.ContainsKey(trait.Id);
                bool parked = Traits.IsUnassigned(trait.Id);
                if (!owned && !parked)
                    problems.Add($"特性 {trait.Id} を持つ種族が無い（Traits.Unassigned にも無い）");
                if (owned && parked)
                    problems.Add($"特性 {trait.Id} は {traitOwner[trait.Id]} が持つのに未配布に載っている");
            }

            foreach (var species in table)
            {
                int total = Stats.TotalOf(species.Base);
                if (total != BaseTotal)
                {
                    problems.Add($"{species.Id}: 基礎値の合計が {total}（{BaseTotal} に揃える）");
                }

                // ⚠️ **弱化2本の合計も全種族でそろえる。**⭐ 差は配分で出す。
                //    ⚠️ ここを数えていなかったので、あとから足した2種族（イワオ 220 / ホムラ 175）が
                //    黙って揃っていなかった（2026-08-19 の監査で発覚）。
                //    合計 600 だけを見ていると、移植元の4本ぶんが種族ごとに違ってしまう。
                int pair = species.Base.Acc + species.Base.Res;
                if (pair != DebuffBaseTotal)
                {
                    problems.Add(
                        $"{species.Id}: 弱化命中＋弱化耐性が {pair}（{DebuffBaseTotal} に揃える）");
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
                // ⭐ **大きさは決め打ちの2つだけ**（2026-08-21 に 64 を足した）。
                // ⚠️ 何でも通すと、次に描いた人が 24 や 40 を持ち込み、画面のどこで
                //    どう伸びるかが種族ごとに変わる。⭐ 画面は**同じ枠に伸ばして**描くので、
                //    変わるのは細かさだけ ── だから2段階に留める。
                if (species.Sprite.Width != species.Sprite.Height
                    || (species.Sprite.Width != 16 && species.Sprite.Width != 64))
                {
                    problems.Add(
                        $"{species.Id}: 意匠が {species.Sprite.Width}×{species.Sprite.Height}"
                        + "（16×16 か 64×64 に揃える）");
                }
                // ⚠️ **姿の添字が色数を超えていないか。**⭐ 超えると、その色を描いた瞬間に
                //    Palette.ColorOf が投げる（帳面側と同じ検査をここにも置く）。
                int deepest = 0;
                for (int y = 0; y < species.Sprite.Height; y++)
                {
                    for (int x = 0; x < species.Sprite.Width; x++)
                    {
                        int at = species.Sprite.At(x, y);
                        if (at > deepest) deepest = at;
                    }
                }
                foreach (var palette in species.Palettes)
                {
                    if (deepest <= palette.Count) continue;
                    problems.Add(
                        $"{species.Id}: 姿が色 {deepest} 番を使っているのに、色が {palette.Count} つしかない");
                    break;
                }

                // ⚠️ 枠1は CT が無いので毎回撃てる。大技を置くと壊れる。
                //    実際にヌシの枠1へ震撼（全体・大）を置いてしまい、決着が8行動になった。
                // ⚠️ 技は渡された表から引く。⭐ 帳面で足したばかりの技も見つかるように
                Skill? first = null;
                foreach (var cand in skillTable) if (cand.Id == species.Skill1) { first = cand; break; }
                if (first == null)
                {
                    problems.Add($"{species.Id}: 枠1 の {species.Skill1} が技表に無い");
                    continue;
                }
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
            foreach (var species in table) ids.Add(species.Id);
            if (ids.Count != List.Length) problems.Add("種族 id が重複している");

            // ⚠️ 「属性が3すくみを覆えているか」はもう数えない。
            //    属性は種族ではなく個体が持つので、どの種族からも3属性すべてが生まれる。

            return problems;
        }
    }
}
