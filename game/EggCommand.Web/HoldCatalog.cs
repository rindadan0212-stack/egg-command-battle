namespace EggCommand.Web
{
    /// <summary>`hold=`（長押しで開く札）に選べる名前の一覧（骨組みエディタ P4・案7の A）。
    ///
    /// 🔴 **唯一の出所は `Shell.cs` の `public void Hold(string what, string at)` の
    /// `switch (what)`**（15個）。⚠️ ここは <see cref="TapCatalog"/> と同じ「並べただけ」の
    /// 写し ── 手で写した瞬間に2つに割れるので、`EggCommand.Tests` の `HoldCatalogTests` が
    /// `Shell.cs` のソースをテキストとして読み直し、`case "..."` を正規表現で抜き出して
    /// この配列と過不足なく一致するかを検査する（ずれたら test が落ちる）。
    ///
    /// ⚠️ **`tap=` と混ぜない。**`Shell.Tap` と `Shell.Hold` は別の switch で、同じ綴り
    /// （`s0`〜`s2`）が両方に居る ── 戦闘の技札は短押しで技を出し（`tap=s0`）、
    /// 長押しで技の詳細を開く（`hold=s0`）。`tap.js` が押下時間で分ける。
    ///
    /// ⭐ **冠が付く**（`use=` で差された部品の中では `detail-s0` のようになる）ものが
    /// 混ざっている ── 候補を出すときは <see cref="TapCrowns"/> の逆算を `tap=` と
    /// まったく同じように通す（`EditPage` の候補作りは1本に畳んである）。</summary>
    public static class HoldCatalog
    {
        public static readonly string[] Names =
        {
            // ── Shell.cs Hold() の switch（15個・出現順そのまま） ──
            // ⭐ BOX の札の技（`use=panel` で差されるので `detail-` の冠つき）
            "detail-s0", "detail-s1", "detail-s2",
            // ⭐ 戦闘の手札（battle.txt は土台なので冠なし・2026-08-29 配線）
            "s0", "s1", "s2",
            // ⭐ 配合の親札（`use=panelmini` ── pfill=親A・qfill=親B・2026-08-29 配線）
            "pfill-s0", "pfill-s1", "pfill-s2",
            "qfill-s0", "qfill-s1", "qfill-s2",
            // ⭐ 種族の札の抽選（枠1〜3）
            "skill1", "skill2", "skill3",
        };
    }
}
