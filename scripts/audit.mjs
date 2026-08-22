/** 画面の検査（ブラウザの中で走る本体）。
 *  ⚠️ **ここが唯一の出所。**`inspect.mjs` も試験も、これを読んで使う
 *  ── 写すと「比べているつもりで別のものを比べる」ことになる。
 */
export function audit() {
  const stage = document.getElementById('stage')
  if (!stage) return ['#stage が無い']
  const bad = []
  const push = (s) => { if (bad.length < 40) bad.push(s) }
  const nodes = [...stage.querySelectorAll('.n')]

  // ⭐ 字が実際に乗っている範囲。⚠️ 枠で比べると嘘の重なりが出る
  //    （Unity 版の `InkOf` と同じ理由。幅800の枠に「BOX」の107しか描かれない）
  const inkOf = (el) => {
    if (!el.firstChild || el.firstChild.nodeType !== 3) return null
    const r = document.createRange()
    r.selectNodeContents(el)
    const rects = [...r.getClientRects()]
    if (!rects.length) return null
    return {
      left: Math.min(...rects.map(x => x.left)),
      right: Math.max(...rects.map(x => x.right)),
      top: Math.min(...rects.map(x => x.top)),
      bottom: Math.max(...rects.map(x => x.bottom)),
    }
  }
  const hits = (a, b) =>
    !(a.right <= b.left + 0.5 || b.right <= a.left + 0.5 ||
      a.bottom <= b.top + 0.5 || b.bottom <= a.top + 0.5)

  // ⭐ **層。**いちばん上の覆いより前か後ろか。
  // ⚠️ 層をまたぐ比較は意味を持たない（同時に見えない）。
  const veils = [...stage.querySelectorAll('.veil')]
  const front = veils.length ? veils[veils.length - 1] : null
  const above = (el) => {
    if (!front) return true
    return !!(front.compareDocumentPosition(el) & Node.DOCUMENT_POSITION_FOLLOWING)
  }

  // ── ⓪ 画面そのものが viewport から出ていないか ────
  // ⚠️ **これが無いと、他の検査が全部無意味になる。**
  //    2026-08-22 に実測: 中央寄せが効かず stage が left=345 に出ていたのに、
  //    「stage の中に収まっているか」しか見ていなかったので 0件 と報告した。
  //    ⭐ 基準そのものがずれていないかを、最初に見る。
  {
    const s0 = stage.getBoundingClientRect()
    if (s0.left < -0.5 || s0.top < -0.5 ||
        s0.right > innerWidth + 0.5 || s0.bottom > innerHeight + 0.5) {
      push(`画面が窓から出ている: stage ${Math.round(s0.left)},${Math.round(s0.top)}`
        + ` 〜 ${Math.round(s0.right)},${Math.round(s0.bottom)}`
        + ` / 窓 ${innerWidth}x${innerHeight}`)
    }
  }

  // ── ① id の重複（⭐ Unity 版に無い検査）────────────
  const seen = new Map()
  for (const el of stage.querySelectorAll('[id]')) {
    seen.set(el.id, (seen.get(el.id) || 0) + 1)
  }
  for (const [id, n] of seen) {
    if (n > 1) push(`id の重複: 「${id}」が ${n} 個 ── DOM では一意でなければ指し示せない`)
  }

  // ── ② 字が枠より広い ────────────────────────────
  for (const el of nodes) {
    if (!el.classList.contains('label') && el.tagName !== 'BUTTON') continue
    if (!el.textContent.trim()) continue
    if (el.scrollWidth > el.clientWidth + 1) {
      push(`字が枠より広い: ${el.id}「${el.textContent.slice(0, 14)}」`
        + ` 要る ${el.scrollWidth} / 枠 ${el.clientWidth}`)
    }
  }

  // ── ②b 折り返す字が枠より高い ──────────────
  // ⚠️ **折り返す字だけ見る。**⭐ 1行の字は Unity がわざと枠の外へ描くので
  //    （`VerticalWrapMode.Overflow`）、そこを数えると偽の警報になる。
  //    ⭐ 折り返す字は「何行入るか」を骨組みが決めているので、溢れたら本当の不備。
  for (const el of nodes) {
    if (!el.classList.contains('wrapped') || !el.textContent.trim()) continue
    if (el.scrollHeight > el.clientHeight + 1) {
      push(`字が枠より高い: ${el.id}「${el.textContent.slice(0, 14)}」`
        + ` 要る ${el.scrollHeight} / 枠 ${el.clientHeight}`)
    }
  }

  // ── ③ 親の枠からはみ出し ────────────────────────
  for (const el of nodes) {
    const parent = el.parentElement
    if (!parent || parent === stage) continue
    // ⚠️ **中を知らないと宣言した枠（host）は、中身の高さも知らない。**
    //    ⭐ 盤の高さはマスの段数で決まるので、骨組みには書けない
    //    （その外側の巻物が溢れを受け止める）。
    const style = getComputedStyle(parent)
    const scrolls = style.overflowY === 'auto' || parent.classList.contains('host')
    // ⚠️ **横へ溢れてよいのは、親が切っているときだけ。**
    //    ⭐ 放置の地面は画面幅の2倍あり、左へ流して見せる ── 実物も
    //    `RectMask2D` で切っている（「検査が画面外と言うのはこの帯のこと。意図どおり」）。
    //    ⚠️ 切っていない親からはみ出したら、それは本当に見えない場所へ出ている。
    const cuts = style.overflowX !== 'visible'
    const a = el.getBoundingClientRect(), p = parent.getBoundingClientRect()
    if (!cuts && (a.left < p.left - 0.5 || a.right > p.right + 0.5)) {
      push(`親の枠から横へはみ出し: ${el.id}`)
    }
    // ⭐ 巻物の中は縦に溢れてよい（それが巻物）
    if (!scrolls && (a.top < p.top - 0.5 || a.bottom > p.bottom + 0.5)) {
      push(`親の枠から縦へはみ出し: ${el.id}`)
    }
  }

  // ── ④ 画面の外 ─────────────────────────────────
  const s = stage.getBoundingClientRect()
  for (const el of nodes) {
    if (el.closest('.scroll') && el.closest('.scroll') !== el) continue
    // ⚠️ **切られている中は「外」ではない。**⭐ 切る親（`host` の枠など）の中で
    //    はみ出したものは、画面へは出てこない ── 実物の `RectMask2D` と同じ。
    const cut = el.parentElement && el.parentElement !== stage
      && getComputedStyle(el.parentElement).overflowX !== 'visible'
    if (cut) continue
    const a = el.getBoundingClientRect()
    if (a.width === 0 || a.height === 0) continue
    if (a.left < s.left - 0.5 || a.right > s.right + 0.5) push(`画面の外（横）: ${el.id}`)
  }

  /** 巻物に切られていて、そもそも見えていないか。
   *
   *  ⚠️ `getBoundingClientRect` は**切られる前**の位置を返すので、
   *  巻物の外へ出た札を「下の野に覆われている」と数えてしまう
   *  （実測 2026-08-22: 編成の一覧で 4段目の「Lv 37」が「決定」に覆われたと報告された）。
   *  ⭐ 巻物の外へ出ているのは**動かせば見える**ので不備ではない。 */
  const clipped = el => {
    let a = el.getBoundingClientRect()
    for (let p = el.parentElement; p && p !== stage; p = p.parentElement) {
      const o = getComputedStyle(p).overflowY
      if (o !== 'auto' && o !== 'scroll' && o !== 'hidden') continue
      const r = p.getBoundingClientRect()
      if (a.bottom <= r.top + 0.5 || a.top >= r.bottom - 0.5) return true
      if (a.right <= r.left + 0.5 || a.left >= r.right - 0.5) return true
      // ⚠️ **半分だけ見えているものも数えない。**⭐ 覆いの検査は
      //    真ん中の1点を見るので、真ん中が巻物の外なら判定できない
      //    （実測 2026-08-22: 盤の下端のマスが「下の帯に覆われている」と出た）。
      const cy = (a.top + a.bottom) / 2, cx = (a.left + a.right) / 2
      if (cy < r.top || cy > r.bottom || cx < r.left || cx > r.right) return true
    }
    return false
  }

  // ── ⑤ 字の重なり（⭐ 実際に乗っている範囲どうし）──
  // ⚠️ **層をまたいで比べない。**覆いの前と後ろは同時に見えないので、
  //    重なっていても不備ではない（実測 2026-08-22: 覆いの下の「—」と
  //    札の「あきらめますか」を重なりとして数えていた）。
  const inks = nodes
    .filter(e => e.classList.contains('label') && e.textContent.trim())
    // ⚠️ 🔴 **覆いの後ろは、そもそも見えていない。**
    //    ⭐ だから**後ろどうしも比べない** ── 比べていた頃は、札を2枚重ねた画面で
    //    「見えていない画面」と「見えていない札」の重なりを 14 件報告した
    //    （実測 2026-08-22 `/app?at=book&open=skill`）。
    //    ⚠️ 後ろの画面は、札を出さない形で別に検査してある。
    .filter(e => above(e))
    .filter(e => !clipped(e))
    .map(e => ({ el: e, ink: inkOf(e) }))
    .filter(x => x.ink)
  for (let i = 0; i < inks.length; i++) {
    for (let j = i + 1; j < inks.length; j++) {
      if (!hits(inks[i].ink, inks[j].ink)) continue
      push(`字の重なり: ${inks[i].el.id}「${inks[i].el.textContent.slice(0, 10)}」`
        + ` × ${inks[j].el.id}「${inks[j].el.textContent.slice(0, 10)}」`)
    }
  }

  // ── ⑥ 押しどころが小さい ────────────────────────
  for (const el of nodes) {
    if (el.tagName !== 'BUTTON' && !el.dataset.tap) continue
    const a = el.getBoundingClientRect()
    // ⚠️ 実機の px でなく、設計の px で見る（外枠を縮めているため）
    const k = s.width / 1080
    if (a.height / k < 111.5) {
      push(`押しどころが小さい: ${el.id} 高さ ${(a.height / k).toFixed(0)}（112 以上）`)
    }
  }

  // ── ⑦ 覆われて見えない（⭐ 実際の合成結果を見る）──
  // ⚠️ **覆いより後ろは見ない。**覆いが出ていれば後ろが隠れるのは当たり前で、
  //    そこを数えると本物の不備が 72件 の中に埋もれる（実測 2026-08-22）。
  //    ⭐ Unity 版の「層」と同じ考え ── いちばん上の覆いから先だけを見る。
  // ⚠️ **ここは⑧と同じ1回の当たり判定を使う。**⭐ `elementsFromPoint` は重いので、
  //    2周すると画面が増えたときに検査そのものが終わらなくなる（実測 2026-08-22）。

  // ── ⑧ 地に沈んで見えない ────────────────────────
  //
  // 🔴 **置いてあることと、見えることは別。**⚠️ 検査は⑦まで「配置」しか見ておらず、
  //    すごろくの帯を白い札にしたとき、上の**白い字と白い絵が全部消えた**のに
  //    0件と答えた（実測 2026-08-22）。
  //
  // ⭐ ここで見るのは**読めるかどうか**ではなく「**消えていないか**」。
  //    ⚠️ この作品は薄墨（#636980）を白の上に置く場面が多く、
  //    読みやすさの基準（4.5:1）で測ると本物でない指摘が並ぶ。
  //    ⭐ だから線は 2.0:1 ── これを下回るものは**事実上見えない**。
  const rgb = (s) => {
    const m = /rgba?\(([\d.]+),\s*([\d.]+),\s*([\d.]+)(?:,\s*([\d.]+))?\)/.exec(s || '')
    return m ? { r: +m[1], g: +m[2], b: +m[3], a: m[4] === undefined ? 1 : +m[4] } : null
  }
  const lum = (c) => {
    const f = (v) => { v /= 255; return v <= .03928 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4 }
    return .2126 * f(c.r) + .7152 * f(c.g) + .0722 * f(c.b)
  }
  const over = (top, back) => ({          // ⭐ 半透明は下の色と混ぜてから比べる
    r: top.r * top.a + back.r * (1 - top.a),
    g: top.g * top.a + back.g * (1 - top.a),
    b: top.b * top.a + back.b * (1 - top.a),
    a: 1,
  })
  const ratio = (a, b) => {
    const la = lum(a), lb = lum(b)
    return (Math.max(la, lb) + .05) / (Math.min(la, lb) + .05)
  }
  const paper = rgb(getComputedStyle(document.body).backgroundColor) || { r: 255, g: 255, b: 255, a: 1 }

  for (const el of nodes) {
    const isIcon = el.classList.contains('icon')
    const isText = el.classList.contains('label') && el.textContent.trim()
    if (!isIcon && !isText) continue
    if (!above(el) || clipped(el)) continue

    const box = isText ? inkOf(el) : el.getBoundingClientRect()
    if (!box || box.right - box.left < 1 || box.bottom - box.top < 1) continue
    const x = (box.left + box.right) / 2, y = (box.top + box.bottom) / 2

    // ⭐ **当たり判定は1回だけ。**⚠️ ⑦（覆われている）と⑧（地に沈んでいる）で
    //    別々に呼ぶと、画面が増えたときに検査そのものが終わらなくなる。
    const stack = document.elementsFromPoint(x, y)
    if (!stack.length) continue

    // ── ⑦ 覆われて見えない ──
    const top = stack[0]
    if (isText && top !== el && !el.contains(top) && !top.contains(el)) {
      push(`覆われて見えない: ${el.id}「${el.textContent.slice(0, 12)}」← ${top.id || top.tagName}`)
      continue                                // ⚠️ 隠れているものの明暗は問わない
    }

    // ── ⑧ 地に沈んで見えない ──
    // ⚠️ 絵は `color` で染めた抱き合わせ。⭐ 字と同じく `color` を見ればよい
    const ink = rgb(getComputedStyle(el).color)
    if (!ink || ink.a === 0) continue
    let ground = paper
    const paints = []
    for (const under of stack.slice(stack.indexOf(el) + 1)) {
      const c = rgb(getComputedStyle(under).backgroundColor)
      if (c && c.a > 0) paints.push(c)
      if (c && c.a >= 1) break                // ⭐ 不透明に当たったらそこで止まる
    }
    for (let i = paints.length - 1; i >= 0; i--) ground = over(paints[i], ground)

    const seen = ratio(over(ink, ground), ground)
    if (seen < 2.0) {
      push(`地に沈んで見えない: ${el.id}`
        + (isText ? `「${el.textContent.slice(0, 10)}」` : '（絵）')
        + ` 差 ${seen.toFixed(2)}:1`)
    }
  }

  return bad
}
