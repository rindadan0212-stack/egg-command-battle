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
                       // ── `tap.js` は null を「砂煙のあと墓へ替える」に読む
    double FoeLeft,    // 相手の残り（0〜1）。⭐ 出所は Core.Idle.FoeLeft のみ
    int FoeKey,        // ⭐ 相手が入れ替わったかを見分ける番号（IdleRun.Defeated をそのまま ──
                       //    倒すたびに増える。次の相手が現れたとき番号が変わっていれば「入れ替わった」）
    int Eggs,          // ⭐ この拍で増えた卵の数。0 なら `tap.js` 側は何も出さない
    string Exp,        // ⭐ 溜まっている EXP の字。ホームは組み直さないので、ここで送らないと
                       //    数だけ止まって見える（帯・敵・卵は動くのに数字だけ古いまま、を避ける）
    bool[] Down);      // ⭐ 歩く体ぶん（`Idle.Draw` が並べた添字と同じ順）。倒れているか
                       // ── 唯一の出所は Core.Idle.IsDown（2026-08-28・仕事4で追加）。
                       // ⚠️ 帯は組み直さないので、`tap.js` は「idle-walk<i>」の級（.idle-down）を
                       // 付け外しするだけ ── 砂煙→墓の順は stage.css が1本で持つ。

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
    /// <summary>足元を置く高さ（`home.txt` の `idle` の枠から見た相対）。
    ///
    /// 🔴 **作者の地面の絵から逆算した数**（2026-08-30・作者の指示「地面から浮いている」）。
    /// ⚠️ 旧 396 は**仮の地面の帯**があった頃の数で、その帯を消した（2026-08-28）あとも
    /// 残っていた ── だから体が草より 50px ほど高い所に立っていた。
    /// ⭐ 実測: `assets/ui/paint/home-ground.png` は 90 ドット目から草が生え、
    /// **100 ドット目で全幅が埋まる**（＝草の面）。絵は設計 y=228 から 4px/ドットで描かれるので
    /// 草の面は **228 + 100×4 = 628**。枠（`idle`）は y=196 から始まるので 628−196 = 432、
    /// そこから 8px 沈めて（草に埋もれさせて）**440**。</summary>
    public const float GroundTop = 440f;

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
        // 🔴 **`Size` は `walker.txt` の枠と同じ数**（2026-08-30・160→192）。⚠️ 片方だけ
        //    変えると、器と絵の大きさが食い違って余白か食み出しになる。
        // 🔴 **4体でも重ねない。**⚠️ 旧式は「3体ぶんの幅 390」を人数で割っていたため、
        //    4体では歩幅 130 に対して絵が 192、隣同士が 62px ずつ重なっていた。
        // ⭐ 3体以下の見慣れた間隔は保ち、4体以上だけ絵幅を基準にする。右側には敵との
        //    16px の間を残す。`home.txt` の host は画面内へ戻したので、ここは画面座標そのもの。
        const float Span = 130f, First = 40f, Size = 192f;
        const float FoeLeftX = 824f, FoeGap = 16f;
        // ⭐ 揺れ幅は `Core.Beats` が唯一の出所（動きは `stage.css` が同じ数で書く）
        const float Bob = (float)Beats.Bob;
        float familiarStep = Span * 3f / Math.Max(1, want - 1); // 3体以下は従来の間隔
        float roomStep = (FoeLeftX - FoeGap - First - Size) / Math.Max(1, want - 1);
        float step = want >= 4 ? Math.Min(Size, roomStep) : familiarStep;
        // 5体以上へ拡張されても隣同士を重ねず、同じ範囲へ縮めて収める。
        float shrink = Math.Min(1f, step / Size);
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
              .Append(";width:").Append(Px(Size)).Append(";height:").Append(Px(Size + Bob))
              .Append(";transform-origin:0 0;transform:scale(")
              .Append(shrink.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
              // ⭐ **`idle-walk<i>` の id を持たせる**（2026-08-28・仕事4）。⚠️ 帯は組み直さない
              //    造りなので、倒れた・起きたは `tap.js` がこの id を引いて級（`.idle-down`）を
              //    付け外しする以外に伝える道が無い（`#foe`/`#hptrack` と同じ「id は常に在り、
              //    級だけ変わる」流儀）。
              .Append(")\"><div id=\"idle-walk").Append(i)
              .Append("\" class=\"n idle-walk\" style=\"left:0;top:").Append(Px(Bob))
              .Append(";width:").Append(Px(Size)).Append(";")
              // ⚠️ 一人ずつずらす ── ⭐ 揃うと行進になり、めいめいが歩いている感じが消える
              .Append("height:").Append(Px(Size)).Append(";animation-delay:")
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
              .Append(DeathOverlay("grave-ally"))
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
        //    ⭐ 居ない拍も要素は残し、`idle-down` で砂煙→墓へ替える。次の相手が来たら
        //    JS は同じ級を外して絵へ戻すので、帯を組み直さずに済む。
        bool visible = FoeVisible(game.Idle);
        // ⭐ **外から飛び込んでくる**（⚠️ 定位置にぽんと現れると「回復した」に見える）。
        //    ⚠️ 一度きりの動きなので、出現の拍（`Come`）のときだけ掛ける。
        // 🔴 **`Core.Idle.FoeFresh` は削除された**（2026-08-28・`Core.Idle` の作り直し）。
        //    ⭐ 代わりに拍そのもの（`IdlePhase.Come`＝相手が画面外から飛び込む拍）を
        //    直に読む ── 旧 `EnemyHp` の割合判定と同じ理由で、判断をここへ書き写さず
        //    `IdleRun.Phase` を読むだけにする。
        bool fresh = visible && game.Idle.Phase == EggCommand.Core.IdlePhase.Come;
        // 🔴 **味方と同じ地面の線・同じ大きさに揃えた**（2026-08-30・作者の指示
        //    「位置ずれ。小さい」）。⚠️ 旧 `top:196px` は味方（`GroundTop` から逆算）と
        //    別の数で、しかも器が 200 なのに絵は 128 だったので、**浮いた上に小さかった**。
        // ⭐ `FoeSize` は味方と同じ `Size`（`walker.txt` の枠）── 器の下端＝足元なので、
        //    `GroundTop - FoeSize` に置けば味方と足並みが揃う。
        const float FoeSize = 192f;
        sb.Append("<div id=\"foe\" class=\"n")
          .Append(!visible ? " idle-down" : fresh ? " idle-come" : "")
          .Append("\" style=\"left:").Append(Px(FoeLeftX))
          .Append(";top:").Append(Px(GroundTop - FoeSize))
          .Append(";width:").Append(Px(FoeSize))
          .Append(";height:").Append(Px(FoeSize)).Append("\">")
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
          .Append(DeathOverlay("grave-foe"))
          .Append("</div>");
        // ⭐ 残りの体力。⚠️ 数は出さない（帯だけで足りる）
        // 🔴 **相手の真上へ載せ直した**（2026-08-30・作者の指示「HPバーがずれている」）。
        //    ⚠️ 旧 (740,176) は相手（旧 880〜1080）の**左へ 140px ずれた**位置で、
        //    どの体の帯なのかが読めなかった。⭐ 相手の器に幅を合わせ（`FoeSize`）、
        //    足元（`GroundTop`）から体の高さぶん上がった所のさらに上へ置く。
        const float BarHigh = 18f, BarGap = 26f;
        float barTop = GroundTop - FoeSize - BarGap - BarHigh;
        sb.Append(Box("hptrack", FoeLeftX, barTop, FoeSize, BarHigh, "rgba(0,0,0,.18)",
            visible ? "" : "idle-hidden"));
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
        sb.Append(Box("hpfill", FoeLeftX, barTop, (float)(FoeSize * left), BarHigh, "#e04f5f",
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

    /// <summary>倒れた体と同じ192角に重ねる砂煙と墓。表示順と時間はCSSだけが持つ。</summary>
    private static string DeathOverlay(string grave) =>
        "<span class=\"idle-death-dust\" aria-hidden=\"true\">"
        + "<i></i><i></i><i></i><i></i><i></i></span>"
        + $"<img class=\"n paint idle-grave\" src=\"paint/{grave}.png\" alt=\"\" />";

    private static string Px(float v) =>
        v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "px";
}
