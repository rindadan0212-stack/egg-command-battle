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
  /** ⭐ **いま選べる対象が「部品」か。**null／空なら今までどおり `data-line` で探す。
   *
   * ⚠️ `EditPage` が `_of` を変えるたび呼び直す（`Scenes.Of(_of).ByPart` の値）。
   * ⭐ ポインタの listener を張り直さずに済むよう、`listen()` とは別の小さな入口にした
   * ── `_of` が変わるたびに `listen()` 全体（`setPointerCapture` まわり）を
   * 再実行する必要は無い。
   * @param {string|null} partId 部品の id（`cell` 等）。単独の骨組み／コードから描かれる
   * `unit`/`square`/`walker`/`frame` は null（それらは `use=` を通らず、
   * 差し込まれた側でも自分の `data-line` を持つ ── 今までどおりの探し方でよい）。 */
  setPart(partId) {
    this._partId = partId || null
  },

  /** ⭐ **節点の探し方を1本化。**部品を選んでいるかで `data-line` と
   * `data-part`＋`data-part-line` を切り替える。⚠️ 呼び出し側（`nodeAt` / `rering` /
   * `_drawGuide`）で個別に分岐を書くと、いつか1か所だけ直し忘れる。 */
  _selector(line) {
    return this._partId
      ? '[data-part="' + this._partId + '"][data-part-line="' + line + '"]'
      : '[data-line="' + line + '"]'
  },

  /** その節点の「いま編集している文書の中の行」。⚠️ 部品なら `data-part-line`
   * （`cell.txt` 等・自分のファイルの行）、そうでなければ `data-line`。 */
  _lineOf(node) {
    if (!node) return ''
    return this._partId ? (node.dataset.partLine || '') : (node.dataset.line || '')
  },

  /** 盤を器（列）に合わせて縮める。⚠️ 器のサイズが変わるたび呼び直す。
   * @param {string} wrapId 器の id @param {string} stageId 盤（1080x1920）の id */
  fit(wrapId, stageId) {
    const wrap = document.getElementById(wrapId)
    const stage = document.getElementById(stageId)
    if (!wrap || !stage) return
    const r = wrap.getBoundingClientRect()
    // ⚠️ 0 除算・負値を避ける（器がまだ描かれていない拍で呼ばれることがある）
    const k = Math.max(0.05, Math.min(r.width / 1080, r.height / 1920))
    const t = 'translate(-50%, -50%) scale(' + k + ')'
    stage.style.transform = t
    stage.dataset.scale = String(k)
    // ⭐ 不備の層（#edfaults）は #edstage と**同じ倍率**を持つ別の DOM（EditPage.razor
    //    参照 ── #edcap の中に置かない）。ここで一緒に合わせないと、盤を縮めたときだけ
    //    不備の枠が実物からずれる。
    const faults = document.getElementById('edfaults')
    if (faults) faults.style.transform = t
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
    let repeatIndex = ''  // ⭐③ 掴んだ複製の番号（無ければ空文字）

    // ⭐ 覆いをどけて、その真下に何が描かれているかを見る（一瞬だけ）。
    // ⚠️ **部品を選んでいるときは `data-part="<_of>"` だけを探す。**
    //    ⭐ 他の部品や土台自身の節点（`data-line` しか持たない）は拾わない
    //    ── `[data-part="X"]` は X という部品自身の節点にしか付かない
    //    （`LayoutDom.cs` が `PartId` からそのまま出す）。
    const nodeAt = (x, y) => {
      cap.style.pointerEvents = 'none'
      const el = document.elementFromPoint(x, y)
      cap.style.pointerEvents = 'auto'
      if (!(el instanceof Element)) return null
      return this._partId
        ? el.closest('[data-part="' + this._partId + '"]')
        : el.closest('[data-line]')
    }

    // ⭐ **タップで選ぶ（離したとき）だけに使う、別の探し方。**
    //
    // ⚠️ `nodeAt`（上）は「いま `_partId` として選んでいる部品の中だけ」を探す ──
    // 掴んで動かす（`down`/`move`）はこの縛りのままでよい（土台の節点を掴んで動かす
    // ときに、途中で違う部品へ迷い込むと事故る）。
    //
    // ⭐ タップ選択はモードで縛らず、**近い方を勝たせる**。`data-part` を持つ子孫は
    // 必ず `data-line` を持つ祖先より DOM で内側にいる（`Layouts.Splice` が部品の
    // 中身を「差した節点の子」として差し込むため ── `LayoutDom.cs` 参照）。
    // だから `closest('[data-part],[data-line]')` 1回で「いちばん近い出所」が取れる ──
    // 押した先が別の部品なら（②）その部品を、部品を直していて土台の節点を押したら
    // （②の逆向き）土台を、正しく指す。
    const pickAt = (x, y) => {
      cap.style.pointerEvents = 'none'
      const el = document.elementFromPoint(x, y)
      cap.style.pointerEvents = 'auto'
      if (!(el instanceof Element)) return null
      return el.closest('[data-part],[data-line]')
    }

    const down = (e) => {
      e.preventDefault()
      const node = nodeAt(e.clientX, e.clientY)
      line = node ? this._lineOf(node) : null
      // ⭐③ 掴んだ要素 **自身**の id 末尾 `#N`（`LayoutDom.One` が繰り返しの複製に付ける）。
      //    ⚠️ 入れ子の繰り返し（`card#2#1` 等）でも、末尾は必ず「いま掴んでいる節点自身の
      //    繰り返し」の番号 ── 外側の番号は先に付き、自分の番号は自分の呼び出しで
      //    最後に足されるため（`LayoutDom.cs` の `mine = suffix + "#" + index`）。
      //    ⚠️ 節点自身が `repeat=` を持つかどうかまでは JS 側で確かめない
      //    （C# 側が `_dragOrigin.Option("repeat")` で確かめる ── 出所を1つに保つ）。
      const m = node ? /#(\d+)$/.exec(node.id) : null
      repeatIndex = m ? m[1] : ''
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
        owner.invokeMethodAsync('DragStart', line, repeatIndex)
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
        // ⭐ 動かさずに離した＝いままでどおり「選ぶ」。⚠️ ここだけ `pickAt`（近い方優先）
        //    を使う ── 掴んで動かす（`nodeAt`）とは別の探し方（上の註）。
        const node = pickAt(e.clientX, e.clientY)
        if (node) this._ringTo(node); else this._ringHide()
        if (node && node.dataset.part) {
          // ⭐ 押した先が部品 ── ②「その部品のファイルへ切り替えて、その節点を選ぶ」。
          //    同じ部品を直している最中なら、C# 側で「ただの選び直し」に落ちる。
          owner.invokeMethodAsync('PickedPart', node.dataset.part, node.dataset.partLine || '-1')
        } else {
          // ⭐ `data-line` のみ ── 自前の行、または（部品を直している最中なら）
          //    土台の行。どちらかは C# 側（`Scenes.Of(_of).ByPart`）が判じる。
          owner.invokeMethodAsync('Picked', node ? (node.dataset.line || '') : '')
        }
      }
      from = null; line = null; dragging = false; repeatIndex = ''
    }

    const cancel = () => {
      // ⚠️ 途中で指が奪われた（他のジェスチャに割り込まれた等）。⭐ 動いていたなら、
      //    そこまでの分を1つの動作として確定する（宙ぶらりんにしない）。
      if (dragging) owner.invokeMethodAsync('DragEnd')
      from = null; line = null; dragging = false; repeatIndex = ''
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

  /** ⭐ 不備の **Focus**（`#edfaults` の中の `.edfault-focus`）だけ、自分で押しどころを
   * 受ける。⚠️ `.edfault-box`（弱い輪郭）は `pointer-events:none` なので、ここには
   * 一度も届かない ── 下の本体（`#edcap` 経由）へ素通しする、既存の道のまま。
   *
   * ⚠️ **`listen()` の `cap` とは別の DOM 系列**（`#edfaults` は `#edstage` の外）。
   * `pointerup` を Focus 自身に直付けする ── `elementFromPoint` に頼る `pickAt` は
   * 「盤の中で一番手前は何か」を見る仕組みなので、`#edcap` を挟まないこの層は
   * 自分の listener で拾うしかない（`#edfaults` は Razor が毎回作り直さない静的な
   * 要素なので、`listen`/`resize` と同じく最初に1回だけ張ればよい）。
   * @param {object} owner .NET 側の受け口 */
  faultsListen(owner) {
    const layer = document.getElementById('edfaults')
    if (!layer) return
    if (this._fBound) layer.removeEventListener('pointerup', this._fBound)
    const fn = (e) => {
      const el = e.target instanceof Element
        ? e.target.closest('[data-part],[data-line]') : null
      if (!el) return
      e.stopPropagation()
      if (el.dataset.part) {
        owner.invokeMethodAsync('PickedPart', el.dataset.part, el.dataset.partLine || '-1')
      } else {
        owner.invokeMethodAsync('Picked', el.dataset.line || '')
      }
    }
    this._fBound = fn
    layer.addEventListener('pointerup', fn)
  },

  /** ⭐ 未保存の直しを捨てる前に、1度だけ確かめる。⚠️ 新しい UI を作らず、
   * ブラウザ標準の `confirm` を使う（過剰な抽象化を避ける指示どおり）。
   * `EditPage.ConfirmSwitchIfDirty` からだけ呼ぶ。 */
  confirmDiscard() {
    return window.confirm('保存していない直しがあります。捨てて切り替えますか？')
  },

  /** ⭐ Ctrl+Z / Ctrl+Shift+Z（取り消し／やり直し）と、⭐② 矢印キー（ナッジ）。
   * ⚠️ **document 全体**で聞く（数値欄にフォーカスがあっても Ctrl+Z が効くように）
   * ── だから離れるとき必ず外す（`stop`）。外さないと、`/app`（遊ぶ頁）へ移っても
   * 生き残って奪い続ける。
   *
   * ⚠️②矢印キーは Ctrl+Z と**同じ場所**に足す（道を2つに割らない、の指示）。
   * ⭐ ただし矢印は「字を打っている最中」は素通し ── `input`/`textarea`/`select` に
   * 焦点があるときは、値の入力や `<select>` の選び直しを矢印キーで邪魔しない。 */
  keys(owner) {
    if (this._keyBound) document.removeEventListener('keydown', this._keyBound)
    // ⭐② 矢印 → (dx, dy) の向き（-1/0/1）。「きざみ」ぶんの掛け算は C# 側（`EditPage.Nudge`）
    //    がする ── ここは向きだけを渡す（`Dragging` の dx/dy が設計px の実量なのとは違う）。
    const ARROWS = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] }
    const fn = (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z') {
        e.preventDefault()
        owner.invokeMethodAsync(e.shiftKey ? 'Redo' : 'Undo')
        return
      }
      const dir = ARROWS[e.key]
      if (!dir) return
      // ⚠️ 字を打っている最中（数値欄・字そのもの欄・寄せ/色の <select>）は矢印を素通しする
      //    ── でないと、欄の中でカーソルを動かすつもりが節点を動かしてしまう。
      const tag = document.activeElement && document.activeElement.tagName
      if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return
      e.preventDefault()
      owner.invokeMethodAsync('Nudge', dir[0], dir[1])
    }
    this._keyBound = fn
    document.addEventListener('keydown', fn)
  },

  /** ⭐③ 木パネル「出ているものだけ」用 ── いま盤（#edstage）に実際に描かれている
   * `data-line`／`data-part`＋`data-part-line` を集める。⚠️ `when=` をここで評価し
   * 直さない（実際に盤に描かれているかで判定する ── 推測でなく実物を見る設計どおり）。
   * @returns {string[]} `"line:42"` か `"part:cell:3"` の形の一覧（`EditPage.IsVisible`
   * が組み立てるキーと同じ形 ── 出所を2つに割らない）。 */
  visibleLines() {
    const stage = document.getElementById('edstage')
    if (!stage) return []
    const out = []
    stage.querySelectorAll('[data-line]').forEach(el => out.push('line:' + el.dataset.line))
    stage.querySelectorAll('[data-part]').forEach(el =>
      out.push('part:' + el.dataset.part + ':' + el.dataset.partLine))
    return out
  },

  /** 頁を離れるときの後片付け（`EditPage.Dispose`）。⚠️ `keys()` の document 直付けの
   * listener だけは、DOM が消えても自然には外れない。 */
  stop() {
    if (this._keyBound) { document.removeEventListener('keydown', this._keyBound); this._keyBound = null }
  },

  /** 選んでいる行の輪を描き直す（木から選んだとき・数を直して盤を組み直したときに使う
   * ── そのときは指の座標が無いので、盤の中から同じ行を持つ最初の1枚を探す）。
   * ⚠️ **部品を選んでいるときは同じ探し方に切り替える**（`_selector` が唯一の出所）。
   * @param {string} line 空文字なら輪を隠す */
  rering(line) {
    const node = line ? document.querySelector('#edstage ' + this._selector(line)) : null
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

    // ⚠️ **`line:N:edge` の N も、部品を選んでいるときは `_selector` で探す。**
    //    ⭐ C# 側（`EditPage.TargetsX`/`TargetsY`）が渡す行番号は、いま編集している
    //    文書（部品なら部品自身のファイル）の中の番号 ── `rering` と同じ探し方が要る。
    const parts = token.split(':')
    const target = parts[0] === 'stage'
      ? document.getElementById('edstage')
      : document.querySelector('#edstage ' + this._selector(parts[1]))
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
