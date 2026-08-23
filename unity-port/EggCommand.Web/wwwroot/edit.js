// 骨組みエディタ（`/edit`）の押しどころ。
//
// ⚠️ **`index.html` の `fit()` は `#stage`（id 固定）を全画面に合わせる。**
//    ここは別の id（`edstage` 等）を使う ── 同じ id を使うと、エディタの盤まで
//    全画面へ引き伸ばされてしまう（罠と教訓「配信の癖」と同じ「知らずに共有した」形）。
//
// ⚠️ **画面は字を組み立てて流し込んでいる**（`Sheets.*` → `LayoutDom`）ので、
//    Blazor の `@onclick` は個々の部品に付けられない。⭐ `tap.js` と同じ理由で、
//    ここも「盤の上に透明な覆いを1枚」＋「離れた指の座標から探す」で拾う。

window.eggEdit = {
  /** 盤を器（列）に合わせて縮める。⚠️ 器のサイズが変わるたび呼び直す。
   * @param {string} wrapId 器の id @param {string} stageId 盤（1080x1920）の id */
  fit(wrapId, stageId) {
    const wrap = document.getElementById(wrapId)
    const stage = document.getElementById(stageId)
    if (!wrap || !stage) return
    const r = wrap.getBoundingClientRect()
    // ⚠️ 0 除算・負値を避ける（器がまだ描かれていない拍で呼ばれることがある）
    const k = Math.max(0.05, Math.min(r.width / 1080, r.height / 1920))
    stage.style.transform = 'translate(-50%, -50%) scale(' + k + ')'
    stage.dataset.scale = String(k)
  },

  /** 器の大きさの変化を見張って、盤を追従させる。 */
  start(wrapId, stageId) {
    const wrap = document.getElementById(wrapId)
    if (!wrap) return
    if (this._ro) this._ro.disconnect()
    this._ro = new ResizeObserver(() => this.fit(wrapId, stageId))
    this._ro.observe(wrap)
    this.fit(wrapId, stageId)
  },

  /** ⭐ **透明な覆い**（`capId`）で押しどころを拾う ── 遊びの押しどころ（`data-tap`）を
   * 動かさないため（覆いが先に指を受け、下の部品には一切届かない）。
   *
   * @param {object} owner .NET 側の受け口（`DotNetObjectReference`）
   * @param {string} capId 覆いの id */
  listen(owner, capId) {
    const cap = document.getElementById(capId)
    if (!cap) return
    if (this._bound) cap.removeEventListener('pointerup', this._bound)

    const pick = (e) => {
      e.preventDefault()
      // ⭐ 覆いをどけて、その真下に何が描かれているかを見る。
      //    ⚠️ 一瞬だけ ── 覆いを外したまま戻し忘れると次から拾えなくなる。
      cap.style.pointerEvents = 'none'
      const el = document.elementFromPoint(e.clientX, e.clientY)
      cap.style.pointerEvents = 'auto'

      // ⭐ **`closest('[data-line]')` で「1本の元の行」まで遡る。**
      //    繰り返し（`repeat=`）の複製はどれも同じ行番号を持つので、
      //    何番目の升を押しても同じ節点が選ばれる。
      const node = el instanceof Element ? el.closest('[data-line]') : null
      if (node) this._ringTo(node); else this._ringHide()
      owner.invokeMethodAsync('Picked', node ? node.dataset.line : '')
    }
    this._bound = pick
    cap.addEventListener('pointerup', pick)
  },

  /** 選んでいる行の輪を描き直す（木から選んだとき・数を直して盤を組み直したときに使う
   * ── そのときは指の座標が無いので、盤の中から同じ `data-line` を持つ最初の1枚を探す）。
   * @param {string} line 空文字なら輪を隠す */
  rering(line) {
    const node = line ? document.querySelector('#edstage [data-line="' + line + '"]') : null
    if (node) this._ringTo(node); else this._ringHide()
  },

  /** ⚠️ 輪は選んでいる節点を**囲むだけ**（塗り潰さない ── この作品の約束）。 */
  _ringTo(node) {
    const ring = document.getElementById('edring')
    const stage = document.getElementById('edstage')
    if (!ring || !stage) return
    const k = Number(stage.dataset.scale || '1')
    const sr = stage.getBoundingClientRect()
    const nr = node.getBoundingClientRect()
    // ⭐ 実寸（画面の px）から、盤の中の設計 px へ戻す（盤には倍率が掛かっているため）
    ring.style.left = ((nr.left - sr.left) / k) + 'px'
    ring.style.top = ((nr.top - sr.top) / k) + 'px'
    ring.style.width = (nr.width / k) + 'px'
    ring.style.height = (nr.height / k) + 'px'
    ring.style.display = 'block'
  },

  _ringHide() {
    const ring = document.getElementById('edring')
    if (ring) ring.style.display = 'none'
  },
}
