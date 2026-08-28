// すごろくの盤を巻く。⭐ **駒が見える所へ寄せる**（`assets/layouts/trail.txt` の決めごと）。
//
// ⚠️ **Blazor は毎回 `#board` を作り直す。**⭐ 本体は `MarkupString` を丸ごと差し替える
//    ので、巻いた位置（`scrollTop`）は組み直しのたびに 0 へ戻る ── だから
//    「覚えて戻す」ではなく、**描き終わるたびに駒から測り直して寄せる**。
//
// ⚠️ **C# から座標を受け取らない。**⭐ マスの位置は（段, 車線）から出しているので、
//    式を2か所に持つと必ずずれる（`fx.js` と同じ決めごと）。
//
// ⚠️ 巻物（`#board`）の `scrollTop` は**設計座標**（倍率を掛ける前）で数える。
//    ⭐ `getBoundingClientRect` は倍率が掛かった実寸なので混ぜない ── `offsetTop` で通す。

window.eggBoard = {
  /** 端に貼り付かせない余白（設計px）。⚠️ 0 だと行き先が縁と接して押しづらい。 */
  PAD: 40,

  /** 駒と、光っている行き先が入るところまで巻く。 */
  follow() {
    const box = document.getElementById('board')
    const piece = document.getElementById('piece')
    if (!box || !piece) return

    // ⭐ 巻物の中での上端。⚠️ `offsetParent` は「位置を持つ親」まで飛ぶので、
    //    `#ground-in` → `#ground` → `#board` と足し上げれば設計座標になる。
    const upto = (el) => {
      let y = 0
      for (let e = el; e && e !== box; e = e.offsetParent) y += e.offsetTop
      return y
    }

    const view = box.clientHeight
    const foot = upto(piece) + piece.offsetHeight   // 駒の下端

    // ⭐ **行き先の一番奥**（盤は上ほど奥なので、いちばん小さい上端）
    let head = upto(piece)
    let any = false
    for (const lit of document.querySelectorAll('[data-tap="square"].lead')) {
      const y = upto(lit)
      if (!any || y < head) { head = y; any = true }
    }

    let want
    if (!any) {
      // ⭐ 選ぶものが無い（歩いている・止まっている）── 駒を真ん中に置く
      want = (upto(piece) + foot) / 2 - view / 2
    } else if (foot - head + this.PAD * 2 <= view) {
      // ⭐ 駒も行き先も入る ── 真ん中に置く
      want = (head + foot) / 2 - view / 2
    } else {
      // 🔴 **入りきらないときは行き先を採る。**⚠️ 真ん中に置くと**両端が切れて**、
      //    肝心の「押すマス」が縁の外へ出る（実測 2026-08-26: 6マス先を選ぶ場面で
      //    行き先が上へ 16px、駒が下へ 29px はみ出していた ── 6段 × 218 = 1308 は
      //    見える高さ 1164 に入らない）。⭐ 駒は「さっきまで居た所」なので譲れる。
      want = head - this.PAD
    }

    const most = box.scrollHeight - view
    box.scrollTop = Math.max(0, Math.min(most, want))
  },
}
