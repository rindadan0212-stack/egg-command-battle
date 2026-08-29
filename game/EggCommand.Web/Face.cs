using System.Globalization;
using EggCommand.Core;

namespace EggCommand.Web;

/// <summary>1体を1枚で見せる札に値を差す。
///
/// ⭐ **骨組みが `panel` でも `panelmini` でも、ここ1つで足りる。**
/// ⚠️ 画面ごとに組み立てを書くと、同じ個体が画面によって違う顔になり、
/// 「BOX では見えるのに配合では見えない」欄が生まれる（実際そうなっていた）。
///
/// ⚠️ Unity 版の <c>CreaturePanel.Bind</c> と同じ中身。⭐ 差し替えたのは
/// 「色を Color でなく CSS の字で返す」ところだけ。</summary>
public sealed class Face
{
    private readonly Creature _c;
    private readonly Species _species;
    private readonly StatBlock _full;
    private readonly StatBlock _born;
    private readonly Skill?[] _skills;

    public Face(Creature creature)
    {
        _c = creature;
        _species = Creatures.SpeciesOf(creature);
        _full = Creatures.StatsOf(creature);
        // ⚠️ **偏りは最後に掛かる。**⭐ 「育てる前の実値」を出してから引く
        //    （掛けたあとで引くと1ずれる）。
        _born = Creatures.Slanted(
            Stats.ActualStats(_species.Base, creature.Wild, new StatBlock(0, 0, 0, 0)), creature);
        _skills = Creatures.SkillsOf(creature);
    }

    public PixelSprite Sprite => _species.Sprite;
    public Palette Palette => Creatures.PaletteOf(_c);
    /// <summary>⭐ 一覧の升で「絵のどこを見せるか」（`crop=` の節点だけが読む）。
    /// ⚠️ 出所は <see cref="SpeciesArt"/> ひとつ ── ここで数を作らない。</summary>
    public (double X, double Y) Focus => SpeciesArt.FocusOf(_species.Id, _species.Sprite);

    /// <param name="row">繰り返しの何段目か（ステの表）。</param>
    public string Text(string what, int row) => what switch
    {
        "name" => _species.Name,
        // ⚠️ Lv を主役にしない。同じ Lv でも中身はまるで別物
        "lv" => $"Lv {Levels.Of(_c)}/{Levels.MaxOf(_c)}",
        "sub" => $"{_c.Id}　{_c.Generation}代　変異{_c.MutationCounter}",
        "key" => Mark(row) + Stats.LabelOf(Stats.Keys[row]),
        "wild" => Digits(_born[Stats.Keys[row]] * Scale(row)),
        // ⚠️ 0 を「0」と書かない。⭐ 伸びている行だけが目に入るようにする
        "gain" => Gained(row) > 0 ? $"+{Digits(Gained(row))}" : "−",
        // ⭐ 無いときは「—」。⚠️ 「特性なし」とは書かない（無いことは書かなくても分かる）
        "trait" => Creatures.TraitOf(_c) is Trait t ? $"{t.Name} — {t.Gist}" : "—",
        "s0name" or "s1name" or "s2name" => Skill(what[1] - '0')?.Name ?? "",
        "s0lv" or "s1lv" or "s2lv" => $"Lv{Creatures.SkillLevelOf(_c, what[1] - '0')}",
        "s0ct" or "s1ct" or "s2ct" => Ct(what[1] - '0'),
        _ => "",
    };

    /// <summary>`when=` の答え。⭐ 空き枠は箱ごと出さない
    /// （空の箱を置くと「何か入るはず」に見える）。</summary>
    public bool Shows(string what) => what switch
    {
        "s0" => Skill(0) != null,
        "s1" => Skill(1) != null,
        "s2" => Skill(2) != null,
        _ => false,
    };

    /// <summary>地や字に掛ける色。⚠️ null なら骨組みの `ink=` のまま。</summary>
    public string? Tint(string what) => what switch
    {
        // ⭐ 属性の丸と、技の札の地は**同じ色**（戦闘と同じ約束）
        "elem" or "s0" or "s1" or "s2" => ElementCss(_c.Element),
        // ⭐ **技のラベルの字**もその個体の属性の色に（作者の指示 2026-08-29）。
        //    ⚠️ 地（上の s0/s1/s2）が既に生の `ElementCss` で塗られているので、
        //    字まで同じ薄さだと地と字が同じ色になって沈む ── 読める濃さの
        //    `ElementInk` を使う（戦闘の手札 `Sheets.Fight` と同じ関数）。
        "s0name" or "s1name" or "s2name" => ElementInk(_c.Element),
        // ⭐ 得意には緑、不得意には赤。⚠️ どちらでもない行は骨組みの色のまま
        "wild" => RowInk(),
        _ => null,
    };

    /// <summary>いま何段目を見ているか。⚠️ 繰り返しは `At` で先に届く。</summary>
    public int Row { get; set; }

    private string? RowInk()
    {
        var key = Stats.Keys[Row];
        if (key == _c.Best || key == _c.Strong) return "#1e7a38";     // Ui.GoodInk
        if (key == _c.Worst || key == _c.Weak) return "#c0303f";      // Ui.DangerInk
        return null;
    }

    /// <summary>偏りは行そのものに書く。⚠️ 別の行に「▲速度」と書くと、
    /// 表のどの行のことか目で探すことになる。
    /// ⭐ **大得意は印を2つ**（色は得意/不得意で使い切っているため）。</summary>
    private string Mark(int row)
    {
        var key = Stats.Keys[row];
        if (key == _c.Best) return "▲▲";
        if (key == _c.Strong) return "▲";
        if (key == _c.Weak) return "▼";
        if (key == _c.Worst) return "▼▼";
        return "";
    }

    /// <summary>⚠️ **HP だけは戦闘で <see cref="Battle.HpScale"/> 倍される。**
    /// ⭐ 表に素の数を出していた頃は、素質 37 の個体が戦闘では 111 で戦っていた。</summary>
    private int Scale(int row) => Stats.Keys[row] == StatKey.Hp ? Battle.HpScale : 1;

    private int Gained(int row)
    {
        var key = Stats.Keys[row];
        return (_full[key] - _born[key]) * Scale(row);
    }

    private Skill? Skill(int slot) => slot < _skills.Length ? _skills[slot] : null;

    private string Ct(int slot)
    {
        var skill = Skill(slot);
        if (skill == null) return "";
        return $"CT{Skills.EffectiveCt(slot, skill, Creatures.SkillBoostOf(_c, slot))}";
    }

    /// <summary>属性の色。⭐ 3すくみを**色**で覚えさせる（説明文を置かない）。
    /// ⚠️ 数は `View/Fx.cs` の `ElementMark` から転記。</summary>
    public static string ElementCss(Element element) => element switch
    {
        Element.Fire => "#e87a5c",
        Element.Wood => "#a8d86e",
        _ => "#6ea8d8",
    };

    /// <summary>技のラベルの字に使う、属性色を**札の上で読める濃さ**へ直したもの
    /// （作者の指示 2026-08-29「技のラベルはその個体の属性の色に」）。
    ///
    /// 🔴 **出所は必ず <see cref="ElementCss"/>。**新しい色は作らない ──
    /// ここは3色をそのまま暗くするだけ。⚠️ そのまま字の色に使うと、Wood のような
    /// 薄い色は白い札（戦闘の手札）の上でコントラスト比が 1.65:1 しか無く読めない
    /// （実測）。BOX の札（`panel.txt` の s0/s1/s2）は地そのものが `ElementCss` で
    /// 塗られている（上の `Tint` の "s0"/"s1"/"s2"）ので、字まで同じ薄さのままだと
    /// 地と字が同じ色になって沈む。
    /// ⭐ 46% まで暗くすると、白地でも属性色の地でもコントラスト比 3:1 を上回る
    /// （実測: Fire 3.31:1／Wood 4.04:1／Water 3.47:1・白地はどれも 6.6:1 以上）。</summary>
    public static string ElementInk(Element element)
    {
        string css = ElementCss(element);
        int r = Convert.ToInt32(css.Substring(1, 2), 16);
        int g = Convert.ToInt32(css.Substring(3, 2), 16);
        int b = Convert.ToInt32(css.Substring(5, 2), 16);
        return $"#{Shade(r):x2}{Shade(g):x2}{Shade(b):x2}";

        static int Shade(int channel) => (int)Math.Round(channel * 0.46);
    }

    public static string Digits(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>一覧の升1つ。⭐ ★は素質の合計から引く（生まれつきの良し悪しが縁に出る）。</summary>
    public static string Star(Creature creature) =>
        Rarities.StarsOf(Nests.RarityOfWildTotal(Stats.TotalOf(creature.Wild)));
}
