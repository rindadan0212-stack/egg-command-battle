using System.Text;
using EggCommand.Core;

namespace EggCommand.Web;

/// <summary>1秒ごとに帯へ送る、変わるものだけ。⭐ 組み直さずに差し替えるために在る
/// （`Clocks.Word`／`eggTap.words` と同じ役目・同じ流儀）。
///
/// ⚠️ Blazor が JS へ渡すときに JSON にする ── **プロパティ名は camelCase で届く**
/// （`FoeArt` ではなく `foeArt`）。`tap.js` 側はそちらを読む。
/// ⚠️ 座標は持たない ── 帯そのものは組み直さないので置き場所は動かない
/// （`Idle.Draw` が最初の1回だけ決めて、以後は同じ場所に居る）。</summary>
public readonly record struct IdleView(
    string? FoeArt,   // 相手の絵（"sprite/xxx-0.png"）。⚠️ 倒れて次が出るまでの間は null
                       // ── これが「倒れた」の見え方（`tap.js` 側は null を「隠す」に読む）
    double FoeLeft,    // 相手の残り（0〜1）。⭐ 出所は Core.Idle.FoeLeft のみ
    int FoeKey,        // ⭐ 相手が入れ替わったかを見分ける番号（IdleRun.Defeated をそのまま ──
                       //    倒すたびに増える。次の相手が現れたとき番号が変わっていれば「入れ替わった」）
    int Eggs,          // ⭐ この拍で増えた卵の数。0 なら `tap.js` 側は何も出さない
    string Exp,        // ⭐ 溜まっている EXP の字。ホームは組み直さないので、ここで送らないと
                       //    数だけ止まって見える（帯・敵・卵は動くのに数字だけ古いまま、を避ける）
    bool[] Down);      // ⭐ 歩く体ぶん（`Idle.Draw` が並べた添字と同じ順）。倒れているか
                       // ── 唯一の出所は Core.Idle.IsDown（2026-08-28・仕事4で追加）。
                       // ⚠️ 帯は組み直さないので、`tap.js` は「idle-walk<i>」の級（.idle-down）を
                       // 付け外しするだけ ── ここで座標や絵は決めない（決めるのは stage.css）。

/// <summary>ホームの放置の帯を、`host` の枠へ描く。
///
/// ⭐ **3つ目の `host`。**⚠️ 何体並ぶかは編成しだいなので、間隔と大きさを逆算する。
///
/// ⭐ 進んでいることは走者の歩幅の揺れで見せる。
/// ⚠️ 動きは `stage.css` が持つ（時計を1本増やさずに済む）── ⭐ ここは級を付けるだけ。
/// ⚠️ 揺れ幅は `Core.Beats.Bob` が唯一の出所。
///
/// 🔴 **旧・仮の背景（`idleground`/`tuft`）は 2026-08-28 に削除**（作者の指示）。
/// `home.txt` の作者のドット絵背景（`skyband`/`hillband`/`grass`/`ground`）が
/// もう下に敷いてあるので、仮の帯と草を重ねる必要が無くなった。
/// ⚠️ **`Core.Beats.Scroll`（旧・地面の流れる速さ）はここで使わなくなった。**
/// `Beats.cs` は担当外のファイルなので消さずに残してある（未使用のまま置いてある）。
///
/// ⚠️ ここは `Core.Idle` が決めた結果を描くだけ。勝ち負けも素材もここでは決めない
/// （決めた瞬間に第2の出所ができる）。</summary>
public static class Idle
{
    /// <summary>枠の大きさ（`home.txt` の `idle`）。</summary>
    public const float Wide = 1080f, High = 470f;
    /// <summary>地面の上端。⭐ 歩く3体の足元を置く高さに使う（`BuildScreenPrefabs` の実測）。
    /// ⚠️ 旧・仮の地面の帯（`idleground`）を消したので、帯そのものの高さ（旧 `GroundHigh`）は
    /// もう要らない ── ここは「地面の線」の意味だけ残す。</summary>
    public const float GroundTop = 396f;

    public static string Draw(Game game)
    {
        var sb = new StringBuilder();

        // 🔴 **旧・仮の背景（`idleground`/`tuft`）は削除**（作者の指示 2026-08-28）。
        //    ⭐ `home.txt` の作者のドット絵背景がもう下に敷いてあるので、
        //    ここで仮の帯と草を重ねる必要が無い。
        //    ⚠️ 歩く3体・敵の位置は変えていない（`GroundTop` から逆算しているだけで、
        //    消した箱には依存していなかった）。

        // ⭐ 編成ぶん並べる。⚠️ **占有する幅は変えない** ── 間隔を詰め、そのぶん縮める
        // 🔴 **`PartyOf(game, PartyKind.Idle)` を明示する**（2026-08-28・作者の報告で発覚）。
        //    ⚠️ 引数なしの既定は `PartyKind.Nest`（巣へ連れて行く編成）── 放置と巣は
        //    「別の3体を選べる」ので（`PartyKind` の doc 参照）、既定のまま呼ぶと
        //    **放置に編成したものと違う3体**が描かれる。⭐ 進める側（`Core.Idle.Advance` を
        //    呼ぶ `AppPage`）も同じ `PartyKind.Idle` を渡している ── 描く側だけ既定に
        //    流されると、見た目（ここ）と実際に戦っている面子（あちら）が食い違う
        //    （同じ「放置の編成」の出所が2つになる、というこのリポジトリで何度も踏んだ形）。
        var party = Games.PartyOf(game, PartyKind.Idle);
        int want = Math.Max(1, party.Count);
        const float Span = 130f, First = 120f, Size = 160f;
        // ⭐ 揺れ幅は `Core.Beats` が唯一の出所（動きは `stage.css` が同じ数で書く）
        const float Bob = (float)Beats.Bob;
        float step = Span * 3f / Math.Max(1, want - 1);   // ⚠️ 元は3体ぶんの幅
        float shrink = Math.Min(1f, step / Span);
        for (int i = 0; i < want; i++)
        {
            var c = party[Math.Min(i, party.Count - 1)];
            // ⚠️ **揺れは中の器に掛ける。**⭐ 外は縮めるための `scale` を持っているので、
            //    ここへ動きを足すと `transform` が丸ごと置き換わって縮みが消える。
            // ⚠️ 🔴 **揺れるぶんの天井を空けておく。**⭐ 器の高さを揺れ幅だけ足し、
            //    中の絵をそのぶん下げる ── ⚠️ 空けないと、上がった拍に
            //    絵が器の外へ出て、検査が「親の枠からはみ出し」と読む（実測 2026-08-23）。
            sb.Append("<div class=\"n\" style=\"left:")
              .Append(Px(First + step * i)).Append(";top:")
              .Append(Px(GroundTop - (Size + Bob) * shrink))
              .Append(";width:160px;height:").Append(Px(Size + Bob))
              .Append(";transform-origin:0 0;transform:scale(")
              .Append(shrink.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
              // ⭐ **`idle-walk<i>` の id を持たせる**（2026-08-28・仕事4）。⚠️ 帯は組み直さない
              //    造りなので、倒れた・起きたは `tap.js` がこの id を引いて級（`.idle-down`）を
              //    付け外しする以外に伝える道が無い（`#foe`/`#hptrack` と同じ「id は常に在り、
              //    級だけ変わる」流儀）。
              .Append(")\"><div id=\"idle-walk").Append(i)
              .Append("\" class=\"n idle-walk\" style=\"left:0;top:").Append(Px(Bob))
              .Append(";width:160px;")
              // ⚠️ 一人ずつずらす ── ⭐ 揃うと行進になり、めいめいが歩いている感じが消える
              .Append("height:160px;animation-delay:")
              .Append((i * 0.21).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))
              .Append("s\">")
              .Append(LayoutDom.Render(LayoutStore.Of("walker"), new DomFill
              {
                  Sprite = key => Creatures.SpeciesOf(c).Sprite,
                  Palette = key => Creatures.PaletteOf(c),
                  // ⭐ 味方は反転しない絵（`art`。`walker.txt` の `when=!foe` を通す）。
                  When = key => false,
                  // ⚠️ `part` を落とさない（`Incubator` と同じ理由 ── `walker.txt` の
                  //    行番号がホームの盤へ漏れて、歩く3体が選べなくなる）。
              }, "#w" + i, "", "walker"))
              .Append("</div></div>");
        }

        // ⭐ 相手。
        // 🔴 **見た目の抽選はもう `Core.Idle.Advance` の中で済んでいる**（2026-08-28・
        //    作者の指示で方針が変わった）。⚠️ 旧 `FoeAt`/`PaletteAt`（倒した数から決定論で
        //    巡らせる関数）は**削除された** ── 「乱数は使わない」だった約束はもう事実と
        //    食い違う（`RollFoe` が実際に乱数を引いている。`Core.Idle` の doc 参照）。
        //    ⭐ ここは結果（`IdleRun.FoeSpecies`/`FoePalette`）を**読むだけ**
        //    （抽選そのものを書き写さない ── 唯一の出所は `CurrentFoe`）。
        var (foe, paletteIndex) = CurrentFoe(game.Idle);

        // 🔴 **`EnemyHp` はもう読まない**（2026-08-28・`Core.Idle` の拍の作り直しに合わせた
        //    仕事1）。⚠️ `Advance` はもうこの欄を書かない ── 常に既定の 0 のままの「亡骸」
        //    になった（`IdleRun.EnemyHp` の doc 参照）。⭐ 「相手が居るか」の唯一の出所は
        //    いまの拍（唯一の出所は <see cref="FoeVisible"/>）。
        //    ⚠️ 要素そのものを省く判断は変えていない ── 以前からの理由がそのまま生きている:
        //    毎秒の差し替え（`eggTap.idle`）が「隠す・出す」を切り替えたくても**触る対象が
        //    無い**（`getElementById` が null）と、画面遷移を待つまで敵の出入りが反映されない。
        //    ⭐ 代わりに、居ないときは `idle-hidden`（`display:none`）を付けて**畳んでおく**
        //    ── 要素は常に在り、JS は級（class）を付け外しするだけで済む。
        bool visible = FoeVisible(game.Idle);
        // ⭐ **外から飛び込んでくる**（⚠️ 定位置にぽんと現れると「回復した」に見える）。
        //    ⚠️ 一度きりの動きなので、出現の拍（`Come`）のときだけ掛ける。
        // 🔴 **`Core.Idle.FoeFresh` は削除された**（2026-08-28・`Core.Idle` の作り直し）。
        //    ⭐ 代わりに拍そのもの（`IdlePhase.Come`＝相手が画面外から飛び込む拍）を
        //    直に読む ── 旧 `EnemyHp` の割合判定と同じ理由で、判断をここへ書き写さず
        //    `IdleRun.Phase` を読むだけにする。
        bool fresh = visible && game.Idle.Phase == EggCommand.Core.IdlePhase.Come;
        sb.Append("<div id=\"foe\" class=\"n")
          .Append(!visible ? " idle-hidden" : fresh ? " idle-come" : "")
          .Append("\" style=\"left:880px;top:196px;width:200px;height:200px\">")
          .Append(LayoutDom.Render(LayoutStore.Of("walker"), new DomFill
          {
              Sprite = key => foe.Sprite,
              Palette = key => foe.Palettes[paletteIndex],
              // ⭐ 敵は反転した絵（`artf`。`walker.txt` の `when=foe` を通す）。
              //    ⚠️ 反転そのものは `stage.css` の `.n.pixel.foe { scaleX(-1) }` が持つ
              //    （ここでは新しく書かない）。
              When = key => key == "foe",
              // ⚠️ `part` を落とさない（味方の駒と同じ理由）。
          }, "#foe", "", "walker"))
          .Append("</div>");
        // ⭐ 残りの体力。⚠️ 数は出さない（帯だけで足りる）
        sb.Append(Box("hptrack", 740, 176, 280, 18, "rgba(0,0,0,.18)", visible ? "" : "idle-hidden"));
        // 🔴 **割合に直してから測る**（2026-08-28 に発見した取り違え）。⚠️ 前は実数（0〜2100）を
        //    そのまま `Clamp(…, 0, 1)` に通していたので、**帯は常に満タンのまま**で、
        //    倒れる瞬間だけ空になっていた ── 残りを読むための帯が、何も伝えていなかった。
        //    ⭐ 割り算は `Core.Idle.FoeLeft` が唯一の出所（ここに書き写さない）。
        double left = EggCommand.Core.Idle.FoeLeft(game.Idle);
        // ⭐ **じわっと減らす**（`idle-drain`）。⚠️ 差し替えは毎拍（`AppPage.IdleEvery`＝250ms・
        //    間引かない）だが、打ち合いの1段は0.5秒ごとにしか動かない ── 素のままだと
        //    帯が段ごとに階段で跳ぶ ── 「削っている」ではなく「時々減る」に見える
        //    （縮む速さそのものは `stage.css` の `.idle-drain` が持つ。ここでは書かない）。
        // ⚠️ 🔴 **級で掛ける。id では掛けない。**`hpfill` は骨組みの名前でもあり
        //    （`unit.txt` の戦闘の HP 帯）、`#hpfill` と書くと**戦闘の帯にも効く**
        //    （2026-08-26 に `#ground` で踏んだのと同じ形。`StageCssTests` が見張っている）。
        sb.Append(Box("hpfill", 740, 176, (float)(280 * left), 18, "#e04f5f",
            visible ? "idle-drain" : "idle-hidden idle-drain"));
        return sb.ToString();
    }

    /// <summary>いま相手が居るか。⭐ **唯一の出所** ── <see cref="Draw"/> と <see cref="Peek"/>
    /// が両方ここを呼ぶ（<see cref="CurrentFoe"/> と同じ「判断を2か所に書かない」形）。
    ///
    /// 🔴 **2026-08-28 に出所を変えた**（仕事1）。旧 `IdleRun.EnemyHp`（実数の残りHP）は
    /// `Core.Idle` の拍の作り直しで、`Advance` がもう書かない「亡骸」になった（常に既定の
    /// 0）。⭐ 「相手が居るか」の唯一の出所はいまの拍（`IdlePhase`）── **居ないのは
    /// `Rest` だけ**（作者の仕様・`IdlePhase` の doc 参照）。</summary>
    private static bool FoeVisible(IdleRun run) => run.Phase != EggCommand.Core.IdlePhase.Rest;

    /// <summary>いま出ている相手の種族とパレット。⭐ **唯一の出所** ── <see cref="Draw"/> と
    /// <see cref="Peek"/> が両方ここを呼ぶ（見た目の抽選そのものは `Core.Idle.RollFoe` が
    /// 既に済ませてある。ここは結果を安全に読むだけ）。
    /// ⚠️ 種族表の外を指していたら 0番へ倒す（壊れた保存・種族表を切り詰めた検査への備え）。</summary>
    private static (Species Foe, int PaletteIndex) CurrentFoe(IdleRun run)
    {
        var all = SpeciesTable.All;
        int speciesIndex = run.FoeSpecies >= 0 && run.FoeSpecies < all.Count ? run.FoeSpecies : 0;
        var foe = all[speciesIndex];
        int paletteIndex = run.FoePalette >= 0 && run.FoePalette < foe.Palettes.Count
            ? run.FoePalette : 0;
        return (foe, paletteIndex);
    }

    /// <summary>いまの放置の状態を、毎拍の差し替え（<see cref="IdleView"/>）用に切り出す。
    /// ⚠️ 2026-08-28（仕事2）から「毎拍」＝`AppPage.IdleEvery`（250ms・1秒に4回）。
    /// ⭐ 描く側（<see cref="Draw"/>）と**同じ出所**（<see cref="CurrentFoe"/>）から作る
    /// ── 「相手が誰か」の読み方をここと `Draw` の2か所に書かない。
    ///
    /// ⚠️ <paramref name="eggsJustNow"/> は呼び側（`AppPage.BeatIdle`）が
    /// `Core.Idle.Advance` の戻り値からそのまま渡す ── ここでは進めない（読むだけ）。
    /// ⚠️ <paramref name="nowUnix"/> は<see cref="EggCommand.Core.Idle.IsDown"/>にそのまま渡す
    /// だけ（2026-08-28・仕事4で追加）── 整数秒でよい（復活は3秒単位。<c>Core.Idle</c> の
    /// doc 参照）。呼び側は時計を2つ持たず <c>Shell.Now</c> を渡す。</summary>
    public static IdleView Peek(Game game, int eggsJustNow, long nowUnix)
    {
        var run = game.Idle;
        string? foeArt = null;
        // 🔴 **`EnemyHp` はもう読まない**（`Draw` と同じ理由 ── 唯一の出所は
        //    <see cref="FoeVisible"/>、判断を2か所に書かない）。
        if (FoeVisible(run))
        {
            var (foe, paletteIndex) = CurrentFoe(run);
            // ⭐ 絵の名前（"sprite/xxx-N.png"）の作り方は `SpriteManifest.StemOf` が唯一の出所
            //    （`LayoutDom.Dots` と同じ口 ── 名前の作り方をここへ書き写さない）。
            string? stem = SpriteManifest.StemOf(foe.Sprite, foe.Palettes[paletteIndex]);
            if (stem != null && SpriteManifest.Exists(stem)) foeArt = "sprite/" + stem + ".png";
        }
        // ⚠️ 「EXP {数}」の書式は `Sheets.Home` の `count` 束縛と同じもの。⭐ 本来は1本化したい
        //    ところだが、`Sheets.cs` はこの仕事の担当外ファイルなので、ここでは書式だけを
        //    素直に写す（判断＝書式そのものは1行で、割り算や既定値のような分岐を持たない）。
        // ⭐ 仕事4: 倒れているかは <see cref="Draw"/> が並べた編成（`PartyKind.Idle`）と
        //    同じ添字で読む ── `idle-walk<i>` の i と揃える（`Draw` の2つ目の理由と同じ、
        //    「読み方をここと Draw の2か所に書かない」を歩く体にも適用）。
        var party = Games.PartyOf(game, PartyKind.Idle);
        var down = new bool[party.Count];
        for (int i = 0; i < party.Count; i++)
            down[i] = EggCommand.Core.Idle.IsDown(run, party[i], nowUnix);
        return new IdleView(foeArt, EggCommand.Core.Idle.FoeLeft(run), run.Defeated, eggsJustNow,
            "EXP " + Face.Digits(run.Exp), down);
    }

    private static string Box(string id, float x, float y, float w, float h, string paint,
        string also = "") =>
        $"<div id=\"{id}\" class=\"n{(also.Length > 0 ? " " + also : "")}\""
        + $" style=\"left:{Px(x)};top:{Px(y)};width:{Px(w)};"
        + $"height:{Px(h)};background:{paint}\"></div>";

    private static string Px(float v) =>
        v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "px";
}
