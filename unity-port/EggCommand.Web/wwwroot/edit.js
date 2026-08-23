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

  /** ⭐ **透明な覆い**（`capId`）で「選ぶ」と「掴んで動かす」の両方を拾う。
   *
   * ⚠️ **`pointerup` だけでは足りない。**掴んで動かすには down/move/up/cancel の
   * 4つ全部が要る。⭐ ただし押しただけ（動かさず離した）は、いままでどおり
   * 「選ぶ」に落とす ── `tap.js` と同じ「遊び」のしきい値（12px）を使う。
   *
   * @param {object} owner .NET 側の受け口（`DotNetObjectReference`）
   * @param {string} capId 覆いの id */
  listen(owner, capId) {
    const cap = document.getElementById(capId)
    if (!cap) return
    if (this._bound) for (const [t, f] of this._bound) cap.removeEventListener(t, f)

    const PLAY = 12   // ⭐ tap.js の「遊び」としきい値を揃える（同じ作法にする指示）

    let from = null       // 押し始めの実画面座標
    let line = null       // 押した先の行（null＝押しどころが無い場所）
    let dragging = false  // PLAY を超えて実際に「掴んで動かす」を始めたか
    let k = 1

    // ⭐ 覆いをどけて、その真下に何が描かれているかを見る（一瞬だけ）。
    const nodeAt = (x, y) => {
      cap.style.pointerEvents = 'none'
      const el = document.elementFromPoint(x, y)
      cap.style.pointerEvents = 'auto'
      return el instanceof Element ? el.closest('[data-line]') : null
    }

    const down = (e) => {
      e.preventDefault()
      const node = nodeAt(e.clientX, e.clientY)
      line = node ? node.dataset.line : null
      from = { x: e.clientX, y: e.clientY }
      dragging = false
      const stage = document.getElementById('edstage')
      k = Number((stage && stage.dataset.scale) || '1')
      // ⚠️ 失敗しても以降を止めない（`releasePointerCapture` と同じ扱い）。
      //    捕まえ損ねても、この後の move/up は cap 自身に直接届く分には困らない
      //    ── 困るのは「盤の外まで指が出た」ときだけで、それは実使用では稀。
      try { cap.setPointerCapture(e.pointerId) } catch { /* 捕まえられなくても続ける */ }
    }

    const move = (e) => {
      if (!from || line === null) return
      const dx = e.clientX - from.x
      const dy = e.clientY - from.y
      if (!dragging) {
        // 🔴 数px揺れただけでは動かさない。PLAY を超えて初めて「掴んで動かす」を始める。
        if (Math.abs(dx) <= PLAY && Math.abs(dy) <= PLAY) return
        dragging = true
        owner.invokeMethodAsync('DragStart', line)
      }
      // ⭐ **k で割る。**盤には倍率が掛かっているので、指の実画面移動量を
      //    設計 px（骨組みの Left/Top と同じ単位）へ戻す。
      owner.invokeMethodAsync('Dragging', dx / k, dy / k)
    }

    const up = (e) => {
      try { cap.releasePointerCapture(e.pointerId) } catch { /* 既に外れていてもよい */ }
      if (dragging) {
        owner.invokeMethodAsync('DragEnd')
      } else {
        // ⭐ 動かさずに離した＝いままでどおり「選ぶ」。
        const node = nodeAt(e.clientX, e.clientY)
        if (node) this._ringTo(node); else this._ringHide()
        owner.invokeMethodAsync('Picked', node ? node.dataset.line : '')
      }
      from = null; line = null; dragging = false
    }

    const cancel = () => {
      // ⚠️ 途中で指が奪われた（他のジェスチャに割り込まれた等）。⭐ 動いていたなら、
      //    そこまでの分を1つの動作として確定する（宙ぶらりんにしない）。
      if (dragging) owner.invokeMethodAsync('DragEnd')
      from = null; line = null; dragging = false
    }

    this._bound = [['pointerdown', down], ['pointermove', move], ['pointerup', up], ['pointercancel', cancel]]
    for (const [t, f] of this._bound) cap.addEventListener(t, f)
  },

  /** ⭐ **8つの掴みどころ**（`#edring` の中・盤の外の層）で大きさを変える。
   *
   * ⚠️ `listen()`（覆い＝盤の中）とは**別の DOM 系列**なので、押しどころが
   * ぶつからない ── 掴みどころの真上を押せば必ずこちらが先に拾う
   * （盤の外の層のほうが DOM で後ろ＝画面では手前に描かれる）。 */
  resize(owner) {
    const ring = document.getElementById('edring')
    if (!ring) return
    if (this._rbound) for (const [t, f] of this._rbound) ring.removeEventListener(t, f)

    let handle = null, from = null, k = 1

    const down = (e) => {
      const el = e.target instanceof Element ? e.target.closest('[data-handle]') : null
      if (!el) return
      e.preventDefault()
      handle = el.dataset.handle
      from = { x: e.clientX, y: e.clientY }
      const stage = document.getElementById('edstage')
      k = Number((stage && stage.dataset.scale) || '1')
      // ⚠️ 🔴 **`ResizeStart` の呼び出しより先に置かない。**捕まえ損ねる（例外を投げる）
      //    ことがあっても、C# 側を必ず起動させる ── ここで止まると掴みどころが
      //    無反応のまま固まる（`listen()` の cap と同じ理由で try/catch）。
      try { el.setPointerCapture(e.pointerId) } catch { /* 捕まえられなくても続ける */ }
      owner.invokeMethodAsync('ResizeStart', handle)
    }
    const move = (e) => {
      if (!handle || !from) return
      const dx = (e.clientX - from.x) / k
      const dy = (e.clientY - from.y) / k
      owner.invokeMethodAsync('Resizing', dx, dy)
    }
    const up = () => {
      if (handle) owner.invokeMethodAsync('ResizeEnd')
      handle = null; from = null
    }

    this._rbound = [['pointerdown', down], ['pointermove', move], ['pointerup', up], ['pointercancel', up]]
    for (const [t, f] of this._rbound) ring.addEventListener(t, f)
  },

  /** ⭐ Ctrl+Z / Ctrl+Shift+Z。⚠️ **document 全体**で聞く（数値欄にフォーカスが
   * あっても効くように）── だから離れるとき必ず外す（`stop`）。外さないと、
   * `/app`（遊ぶ頁）へ移っても生き残って Ctrl+Z を奪い続ける。 */
  keys(owner) {
    if (this._keyBound) document.removeEventListener('keydown', this._keyBound)
    const fn = (e) => {
      if (!(e.ctrlKey || e.metaKey) || e.key.toLowerCase() !== 'z') return
      e.preventDefault()
      owner.invokeMethodAsync(e.shiftKey ? 'Redo' : 'Undo')
    }
    this._keyBound = fn
    document.addEventListener('keydown', fn)
  },

  /** 頁を離れるときの後片付け（`EditPage.Dispose`）。⚠️ `keys()` の document 直付けの
   * listener だけは、DOM が消えても自然には外れない。 */
  stop() {
    if (this._keyBound) { document.removeEventListener('keydown', this._keyBound); this._keyBound = null }
  },

  /** 選んでいる行の輪を描き直す（木から選んだとき・数を直して盤を組み直したときに使う
   * ── そのときは指の座標が無いので、盤の中から同じ `data-line` を持つ最初の1枚を探す）。
   * @param {string} line 空文字なら輪を隠す */
  rering(line) {
    const node = line ? document.querySelector('#edstage [data-line="' + line + '"]') : null
    if (node) this._ringTo(node); else this._ringHide()
  },

  /** ⚠️ 輪は選んでいる節点を**囲むだけ**（塗り潰さない ── この作品の約束）。
   *
   * ⭐ **輪（と掴みどころ）は盤の外の層に居る**（`EditPage.razor` 参照）。
   * だから実寸（screen px）をそのまま置けばよく、`k` で割る必要が無い
   * ── 割ると、盤の外に居るのに二重に縮めることになる。 */
  _ringTo(node) {
    const ring = document.getElementById('edring')
    const wrap = document.getElementById('edwrap')
    if (!ring || !wrap) return
    const wr = wrap.getBoundingClientRect()
    const nr = node.getBoundingClientRect()
    ring.style.left = (nr.left - wr.left) + 'px'
    ring.style.top = (nr.top - wr.top) + 'px'
    ring.style.width = nr.width + 'px'
    ring.style.height = nr.height + 'px'
    ring.style.display = 'block'
  },

  _ringHide() {
    const ring = document.getElementById('edring')
    if (ring) ring.style.display = 'none'
  },

  /** ⭐ 吸い付いた線を、その瞬間だけ見せる。
   *
   * ⚠️ 座標は C# 側で計算し直さない ── C# は「どの行の・どの辺に吸い付いたか」
   * （`"line:42:right"` / `"stage:centerx"`）だけを渡し、こちらが実 DOM の
   * `getBoundingClientRect` から実寸を読む。⭐ 骨組みの Left/Top は**親からの相対**
   * なので、絶対位置を C# 側で作り直すには祖先を全部たどる必要があるが、
   * DOM は既にそれをやった結果（画面上の実位置）を持っている ── 二重に計算しない。
   *
   * @param {string|null} gx 縦線（x が揃った）の道しるべ
   * @param {string|null} gy 横線（y が揃った）の道しるべ */
  guide(gx, gy) {
    this._drawGuide('edguidex', gx, true)
    this._drawGuide('edguidey', gy, false)
  },

  _drawGuide(id, token, vertical) {
    const el = document.getElementById(id)
    if (!el) return
    // ⚠️ 「きざみ」に吸い付いただけでは線を引かない（唯一の直線が無い ── 格子なので）。
    //    数はちゃんと吸い付く。見せる線が無いだけ。
    if (!token || token === 'step') { el.style.display = 'none'; return }

    const parts = token.split(':')
    const target = parts[0] === 'stage'
      ? document.getElementById('edstage')
      : document.querySelector('#edstage [data-line="' + parts[1] + '"]')
    const edge = parts[0] === 'stage' ? parts[1] : parts[2]
    const wrap = document.getElementById('edwrap')
    if (!target || !wrap) { el.style.display = 'none'; return }

    const tr = target.getBoundingClientRect()
    const wr = wrap.getBoundingClientRect()
    if (vertical) {
      const x = edge === 'left' ? tr.left : edge === 'right' ? tr.right : (tr.left + tr.right) / 2
      el.style.left = (x - wr.left) + 'px'
      el.style.top = '0px'
      el.style.height = wr.height + 'px'
    } else {
      const y = edge === 'top' ? tr.top : edge === 'bottom' ? tr.bottom : (tr.top + tr.bottom) / 2
      el.style.top = (y - wr.top) + 'px'
      el.style.left = '0px'
      el.style.width = wr.width + 'px'
    }
    el.style.display = 'block'
  },
}
