using System.Collections.Generic;
using System.Linq;
using EggCommand.Core;
using EggCommand.Web;
using Xunit;

namespace EggCommand.Tests;

/// <summary>骨組みエディタの属性表（<see cref="EditAttrs"/>）が、`Core.Layouts.Options`
/// （付け足しの唯一の語彙）から**ずれていない**ことを固定する。
///
/// ⭐ `wiki/開発/web移行計画.md` §11-3「Core に新しい付け足しが増えた日に、テストが落ちて
/// 判断を迫る」── `Layouts.Options` の各 key は「表（<see cref="EditAttrs.All"/>）に
/// 載っている」か「意図して外した一覧（<see cref="EditAttrs.Excluded"/>）に載っている」の
/// **どちらか一方**でなければならない（両方・どちらでもない、は落とす）。
///
/// ⚠️ `EditAttrs.cs` は `EggCommand.Web` プロジェクトに置いてあるが、`dotnet test` が
/// Web（Blazor WASM）を建てないという既存の約束（`SeriesRecord.cs`/`Determinism.cs`/
/// `SaveJson.cs` と同じ）を守るため、`EggCommand.Tests.csproj` の `&lt;Compile Include&gt;`
/// でファイルそのものを直接コンパイルしている（ProjectReference は張らない）。</summary>
public class EditAttrsTests
{
    /// <summary>🔴 これが「ずれない検査」の本体。</summary>
    [Fact]
    public void Optionsの全部が表か除外一覧のどちらか一方に載っている()
    {
        var tableKeys = EditAttrs.All.Select(a => a.Key).ToHashSet();
        var excludedKeys = EditAttrs.Excluded.Keys.ToHashSet();

        foreach (var key in Layouts.Options)
        {
            bool inTable = tableKeys.Contains(key);
            bool inExcluded = excludedKeys.Contains(key);
            Assert.True(inTable || inExcluded, $"「{key}=」が表にも除外一覧にも無い（判断が漏れている）");
            Assert.False(inTable && inExcluded, $"「{key}=」が表と除外一覧の両方に載っている");
        }
    }

    /// <summary>⚠️ 逆向き ── 表・除外一覧に、`Layouts.Options` に無い綴りが紛れていないか
    /// （消えた・綴り違いの key を放置しない。この検査が無いと片方だけ直して食い違う）。</summary>
    [Fact]
    public void 表と除外一覧にOptionsに無いキーが無い()
    {
        var known = new HashSet<string>(Layouts.Options);
        foreach (var a in EditAttrs.All)
            Assert.Contains(a.Key, known);
        foreach (var key in EditAttrs.Excluded.Keys)
            Assert.Contains(key, known);
    }

    /// <summary>⚠️ 表の中で key が重複していないか（後勝ちで欄が2つ出る事故を防ぐ）。</summary>
    [Fact]
    public void 表のキーは重複しない()
    {
        var keys = EditAttrs.All.Select(a => a.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    /// <summary>⚠️ 除外一覧はすべて理由（空でない字）を持つこと ── 「なぜ外したか」を
    /// 書かずに黙って外さない。</summary>
    [Fact]
    public void 除外一覧は理由を持つ()
    {
        foreach (var (key, reason) in EditAttrs.Excluded)
            Assert.False(string.IsNullOrWhiteSpace(reason), $"{key}: 除外の理由が空");
    }

    /// <summary>⭐ 選択肢（Choice）は空であってはいけない（選べない選択肢は意味が無い）。</summary>
    [Fact]
    public void 選択肢は空でない()
    {
        foreach (var a in EditAttrs.All.Where(a => a.Kind == AttrKind.Choice))
        {
            Assert.NotNull(a.Choices);
            Assert.True(a.Choices!.Count > 0, $"{a.Key}: 選択肢が空");
        }
    }

    /// <summary>⭐ 日本語ラベルを持つこと（作者の Unity 版に倣う指示 ── 英字の key を
    /// そのまま画面に出さない）。</summary>
    [Fact]
    public void ラベルは空でない()
    {
        foreach (var a in EditAttrs.All)
            Assert.False(string.IsNullOrWhiteSpace(a.Label), $"{a.Key}: ラベルが空");
    }

    /// <summary>⭐ `Layouts.Kinds`（骨組みが知っている全種類）のうち、どれか1つには
    /// 必ず効くこと ── 「どの種類にも絶対に出ない欄」が表に紛れ込んでいないか。
    /// ⚠️ `gap` は種類でなく `repeat=` の有無で決まるので、両方（無し／有り）を試す
    /// （でないと「種類だけでは絶対に効かない」を誤って落とす）。</summary>
    [Fact]
    public void 各属性はどれかの種類には効く()
    {
        foreach (var a in EditAttrs.All)
        {
            bool appliesToAny = Layouts.Kinds.Any(kind =>
                a.AppliesTo(Node(kind)) || a.AppliesTo(Node(kind, ("repeat", "x"))));
            Assert.True(appliesToAny, $"{a.Key}: どの種類にも効かない（表から浮いている）");
        }
    }

    private static LayoutNode Node(string kind, params (string Key, string Value)[] options)
    {
        var dict = new Dictionary<string, string>();
        foreach (var (k, v) in options) dict[k] = v;
        return new LayoutNode("t", kind, 0, 0, 10, 10, dict, new List<LayoutNode>());
    }

    // ── AppliesTo が LayoutDom.cs の実装どおりに種類を選り分けていることの裏取り ──

    [Theory]
    [InlineData("size", "label")]
    [InlineData("size", "button")]
    [InlineData("text", "label")]
    [InlineData("text", "button")]
    [InlineData("anchor", "label")]
    [InlineData("ink", "label")]
    [InlineData("ink", "box")]
    [InlineData("ink", "veil")]
    [InlineData("wrap", "label")]
    [InlineData("lead", "card")]
    [InlineData("lead", "button")]
    [InlineData("foe", "pixel")]
    [InlineData("crisp", "icon")]
    [InlineData("turn", "icon")]
    [InlineData("turn", "button")]
    public void 対象の種類には効く(string key, string kind)
    {
        var attr = EditAttrs.For(key);
        Assert.NotNull(attr);
        Assert.True(attr!.AppliesTo(Node(kind)), $"{key}: {kind} に効くはずが効かない");
    }

    [Theory]
    [InlineData("size", "box")]
    [InlineData("text", "box")]
    [InlineData("text", "icon")]
    [InlineData("anchor", "button")]
    [InlineData("anchor", "box")]
    [InlineData("wrap", "button")]
    [InlineData("foe", "icon")]
    [InlineData("foe", "label")]
    [InlineData("crisp", "pixel")]
    [InlineData("turn", "label")]
    public void 効かない種類には出さない(string key, string kind)
    {
        var attr = EditAttrs.For(key);
        Assert.NotNull(attr);
        Assert.False(attr!.AppliesTo(Node(kind)), $"{key}: {kind} に出てはいけない");
    }

    /// <summary>`gap` は種類でなく `repeat=` の有無で決まる（既存の実装をそのまま踏襲）。</summary>
    [Fact]
    public void gapはrepeatを持つ節点だけに出る()
    {
        var attr = EditAttrs.For("gap");
        Assert.NotNull(attr);
        Assert.False(attr!.AppliesTo(Node("card")));
        Assert.True(attr.AppliesTo(Node("card", ("repeat", "x"))));
    }

    /// <summary>⭐ `Min &lt; Max`（数の範囲が壊れていないか）。</summary>
    [Fact]
    public void 数の範囲はMinがMax未満()
    {
        foreach (var a in EditAttrs.All.Where(a => a.Kind == AttrKind.Number))
            Assert.True(a.Min < a.Max, $"{a.Key}: Min({a.Min}) が Max({a.Max}) 未満でない");
    }

    // ── ⭐ P4（2026-08-29・案7の三分類）── A/B/C の線引きを固定する ────────

    /// <summary>⭐ A: 繰り返しの**形**（列数・段の高さ・上限）は表に載っていて、
    /// `repeat=` を持つ節点でだけ出る。⚠️ `gap` と同じ条件（種類ではなく `repeat=` の有無）。</summary>
    [Theory]
    [InlineData("cols")]
    [InlineData("rows")]
    [InlineData("max")]
    public void 繰り返しの形はrepeatを持つ節点だけに出る(string key)
    {
        var attr = EditAttrs.For(key);
        Assert.NotNull(attr);
        Assert.Equal(AttrKind.Number, attr!.Kind);
        Assert.False(attr.AppliesTo(Node("card")), $"{key}: repeat= の無い節点に出てはいけない");
        Assert.True(attr.AppliesTo(Node("card", ("repeat", "x"))), $"{key}: repeat= があれば出る");
    }

    /// <summary>🔴 `cols` の最小は 1 ── 0 を書けてしまうと
    /// <see cref="FaultKind.InvalidCols"/> の不備を**エディタ自身が作れる**ことになる。</summary>
    [Fact]
    public void 列数は1未満にできない()
    {
        var attr = EditAttrs.For("cols");
        Assert.NotNull(attr);
        Assert.Equal(1f, attr!.Min);
    }

    /// <summary>⭐ `max` の最小は **0**（2026-08-29 監査 B-3）。
    ///
    /// ⚠️ `cols` と違い、`max=` は「書いていない」が正しい状態でもある（巻物の中なら
    /// 上限は要らない ── 外なら <see cref="FaultKind.RepeatMissingMax"/> が別に言う）。
    /// 読む側の既定も 0（`Layouts.DeepOf` の `Number("max", 0)`）。
    /// 🔴 最小を 1 にすると、欄が 0 を見せているのに「−」で 1 に**増え**、
    /// 無かった `max=` が書かれてしまう（`EditPage.AttrField` は 0 で付け足しごと消す）。</summary>
    [Fact]
    public void 上限は0で消せる()
    {
        var attr = EditAttrs.For("max");
        Assert.NotNull(attr);
        Assert.Equal(0f, attr!.Min);
    }

    /// <summary>🔴 **開けてはいけないものを開けたら落ちる杭**（案7の B・C）。
    ///
    /// ⚠️ B（`bind`/`repeat`/`use`）はコードが読む名前で、綴りを変えても骨組みの検査は
    /// 何も言わない ── 不備0件のまま遊びだけが黙って壊れる。
    /// ⚠️ C（`flow`/`dock`/`roll`/`grow`）は1つで周りの意味まで書き換える。
    /// ⭐ どちらも「見せるだけ」（<see cref="EditAttrs.Chips"/>）に留めるのが P4 の決定。</summary>
    [Theory]
    [InlineData("bind")]
    [InlineData("repeat")]
    [InlineData("use")]
    [InlineData("flow")]
    [InlineData("dock")]
    [InlineData("roll")]
    [InlineData("grow")]
    public void 見せるだけの付け足しは表に載せない(string key)
    {
        Assert.Null(EditAttrs.For(key));
        Assert.Contains(key, EditAttrs.Excluded.Keys);
        Assert.Contains(key, EditAttrs.Chips.Keys);
    }

    /// <summary>⚠️ 逆向き ── 札（<see cref="EditAttrs.Chips"/>）の綴りが
    /// `Layouts.Options` から浮いていないか、表と二重になっていないか。</summary>
    [Fact]
    public void 札は除外一覧の中だけにある()
    {
        var known = new HashSet<string>(Layouts.Options);
        var tableKeys = EditAttrs.All.Select(a => a.Key).ToHashSet();
        foreach (var (key, why) in EditAttrs.Chips)
        {
            Assert.Contains(key, known);
            Assert.Contains(key, EditAttrs.Excluded.Keys);
            Assert.DoesNotContain(key, tableKeys);
            Assert.False(string.IsNullOrWhiteSpace(why), $"{key}: 札の一言が空");
        }
    }

    /// <summary>⭐ 専用の欄を持つ4つ（A）は、札にも出さない ── 直せるものを
    /// 「直せません」の灰色の札で見せると嘘になる。</summary>
    [Theory]
    [InlineData("tap")]
    [InlineData("hold")]
    [InlineData("when")]
    [InlineData("pic")]
    public void 専用の欄を持つ付け足しは札にしない(string key)
    {
        Assert.Contains(key, EditAttrs.Excluded.Keys);
        Assert.DoesNotContain(key, EditAttrs.Chips.Keys);
    }
}
