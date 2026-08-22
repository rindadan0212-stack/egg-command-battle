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

  // ── ③ 親の枠からはみ出し ────────────────────────
  for (const el of nodes) {
    const parent = el.parentElement
    if (!parent || parent === stage) continue
    const scrolls = getComputedStyle(parent).overflowY === 'auto'
    const a = el.getBoundingClientRect(), p = parent.getBoundingClientRect()
    if (a.left < p.left - 0.5 || a.right > p.right + 0.5) {
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
    const a = el.getBoundingClientRect()
    if (a.width === 0 || a.height === 0) continue
    if (a.left < s.left - 0.5 || a.right > s.right + 0.5) push(`画面の外（横）: ${el.id}`)
  }

  // ── ⑤ 字の重なり（⭐ 実際に乗っている範囲どうし）──
  const inks = nodes
    .filter(e => e.classList.contains('label') && e.textContent.trim())
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
  for (const el of nodes) {
    if (!el.classList.contains('label') || !el.textContent.trim()) continue
    const ink = inkOf(el)
    if (!ink) continue
    const cx = (ink.left + ink.right) / 2, cy = (ink.top + ink.bottom) / 2
    const top = document.elementsFromPoint(cx, cy)[0]
    if (!top) continue
    if (top !== el && !el.contains(top) && !top.contains(el)) {
      push(`覆われて見えない: ${el.id}「${el.textContent.slice(0, 12)}」← ${top.id || top.tagName}`)
    }
  }

  return bad
}
