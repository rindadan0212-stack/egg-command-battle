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

  /** アウトラインのキーボード移動後、再描画された選択行へ焦点を戻す。
   *  行番号は C# 側の現在文書だけから渡され、見つからない（検索で隠れた等）場合は何もしない。 */
  focusTreeLine(line) {
    const row = document.querySelector('[data-tree-line="' + String(line) + '"]')
    if (row && typeof row.focus === 'function') row.focus()
  },

  /** 未保存状態をブラウザ離脱警告へ同期する。内部の文書切替は C# 側の確認を使い、
   *  こちらはタブを閉じる・再読み込み・外部URLへ移る経路だけを守る。 */
  setDirty(dirty) {
    this._dirty = !!dirty
    if (this._beforeUnloadBound) return
    this._beforeUnloadBound = (e) => {
      if (!this._dirty) return
      e.preventDefault()
      e.returnValue = ''
    }
    window.addEventListener('beforeunload', this._beforeUnloadBound)
  },

  /** ⭐ **C# を呼ぶ唯一の出所**（2026-08-29 監査 E-1）。
   *
   * 🔴 **頁を離れた後に届く呼び出しを、静かに捨てる。**⚠️ `EditPage.Dispose` は
   * `stop()` を待たずに受け口（`DotNetObjectReference`）を捨てるので、その隙に届いた
   * 呼び出しは「破棄済み」で失敗する ── `.catch` が無いと unhandled promise rejection
   * になって、利用者には何も言えないまま browser の console だけが汚れる。
   * ⚠️ 実際に踏めるのは document へ直に張ったもの（道具箱の掴み・ホイール・キー）──
   * DOM が消えても生き延びるため。
   *
   * ⭐ 24箇所へ個別に `.catch` を書かない ── 1つに畳んで「呼び方」を1か所にする
   * （書き忘れが二度と起きない形）。 */
  _call(owner, name, ...args) {
    try {
      const p = owner.invokeMethodAsync(name, ...args)
      if (p && typeof p.catch === 'function') p.catch(() => { /* 破棄済み ── 黙って捨てる */ })
      return p
    } catch {
      return null   // ⚠️ 受け口ごと消えていた（同期で投げる）── これも黙って捨てる
    }
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

  /** 盤の表示を、いまの `_dotPx`（1ドット＝画面上で何 CSS px か）で合わせ直す。
   *
   * ⭐ E1-2（2026-08-25・ドット絵化計画 §11-5）: 以前は器のサイズから毎回 `fitK` を
   * 割り出して半端倍率を掛けていた（`fitK * zoom`）── ドットが不揃いに見えて判断を
   * 誤る（計画の指摘どおり）。⭐ いまは **`_dotPx` が唯一の出所**。設計側の1升は
   * 常に4px（`EditPage._step` の既定と同じ）なので、実効倍率 `k = _dotPx / 4` は
   * 常に「整数 ÷ 4」── 2進浮動小数点でも割り切れる（4 は2の冪）ので、器のサイズが
   * どんな半端な値でも `k` 自体には丸め誤差が乗らない。
   * ⚠️ **器のサイズはここでは見ない。**器が変わっても（脇のパネルを畳む等）
   * `_dotPx` は変えない ── 既定を選び直したいときは `autoDotPx()` を別途呼ぶ
   * （`EditPage.ZoomReset`）。
   * @param {string} wrapId 器の id @param {string} stageId 盤（1080x1920）の id */
  /** ⭐ 盤の周りに敷く余白（設計px・2026-08-29）。
   *
   * 🔴 **負の座標へ動かした節点を見るために要る。**⚠️ 編集中は盤外へ仮置きできるため、
   * 余白が無いと、拡大したときに `#edscroll` が左上より手前へスクロールできず、
   * **いちばん見たい不備（画面の外へ出た節点）ほど画面に出せなかった**。
   * ⭐ 設計px で持つ（画面px でなく）ので、どの倍率でも「盤の外側64ドットぶん」という
   * 同じ広さが見える ── 倍率ごとに見える範囲が変わらない。 */
  _pad: 256,

  /** ⭐ **動かしたくない設計座標を、盤の中へ丸める**（2026-08-29 監査 D-2）。
   *
   * 🔴 **約束できるのは「盤の上の点」だけ。**⚠️ 余白のうんと外（実測: 盤の右 1859px の
   * 何も無い所）を指したまま倍率を変えると、必要なスクロール量が可動域を超えて
   * 届かない ── 余白をいくら広げても、余白自身が同じ倍率で伸び縮みするので原理的に
   * 閉じない。⭐ だから約束の範囲を盤に限る: 盤の外を指していたら、いちばん近い
   * 盤の縁を保つ（見ている物＝盤は動かない）。⚠️ 盤の上を指している普段の操作では
   * 丸めは何もしない（＝いつもどおり指の下が1pxも動かない）。 */
  _anchorIn(wx, wy) {
    return {
      x: Math.min(1080, Math.max(0, wx)),
      y: Math.min(1920, Math.max(0, wy)),
    }
  },

  /** ⭐ 器（`#edscroll`）の内寸。⚠️ 無ければ 0 ── 呼び手は `Math.max` で使うので、
   * 測れない拍では `_pad` だけが効く（変な値で盤が消えない）。 */
  _viewport() {
    const scroll = document.getElementById('edscroll')
    return scroll
      ? { w: scroll.clientWidth, h: scroll.clientHeight }
      : { w: 0, h: 0 }
  },

  fit(wrapId, stageId) {
    // ⭐ setDotPx から引数無しで呼び直せるよう、最後に使った id を覚えておく。
    this._wrapId = wrapId
    this._stageId = stageId
    const wrap = document.getElementById(wrapId)
    const stage = document.getElementById(stageId)
    if (!wrap || !stage) return
    const k = (this._dotPx || 4) / 4
    // 🔴 **箱を変える前後で「器の真ん中に何が見えていたか」を保つ**（2026-08-29 監査 D-2）。
    //    ⚠️ 下で余白（`_pad`）の広さを器の大きさに合わせて変えるので、何もしないと
    //    盤が画面上で飛ぶ（器を広げ狭めするたび、見ていた場所を見失う）。
    //    🔴 **保つのは「盤の左上」ではなく「器の真ん中の設計座標」。**⚠️ 左上を保つと、
    //    倍率が下がったときに盤だけが小さくなって**画面の外へ抜ける**（実測: 既定倍率を
    //    選び直した拍に、盤が器の上へ 481px 逃げて何も見えなくなった）。
    //    ⭐ 中身は `zoomTo` と同じ2段測り ── ①変える前に「器の真ん中はどの設計座標か」、
    //    ②変えた後に「その設計座標がどこへ来たか」を実 DOM から読み、差だけ戻す。
    const scroll = document.getElementById('edscroll')
    const sr = scroll ? scroll.getBoundingClientRect() : null
    const was = scroll ? stage.getBoundingClientRect() : null
    // ⚠️ 変える前の倍率は `this._dotPx`（`setDotPx` が先に書き換える）からは読めない
    //    ── 前回 `fit()` が使った値を覚えておく。初回は今回の値と同じ扱い（＝素通し）。
    const kWas = this._lastK || (this._dotPx || 4) / 4
    const cx = sr ? sr.left + sr.width / 2 : 0
    const cy = sr ? sr.top + sr.height / 2 : 0
    // ⚠️ 盤の外（余白のうんと外）は約束の外 ── 縁へ丸める（`_anchorIn` の註）。
    const w = this._anchorIn(
      was ? (cx - was.left) / kWas : 0,
      was ? (cy - was.top) / kWas : 0)
    const wx = w.x, wy = w.y
    // ⭐ Pass B: 盤を包む固定サイズの箱（`.edstagewrap`）を、見た目の実サイズ
    //    （1080*k × 1920*k）にする。⚠️ `#edstage`/`#edfaults`/`#edgrid` は
    //    左上基準（`transform-origin:0 0`）へ変えてある（CSS 側）ので、
    //    ここで箱のサイズを合わせないと右・下にはみ出す／余白が空く。
    // ⭐ 2026-08-29: そこへ `_pad` ぶんの余白を足す。⚠️ 箱は**両側**ぶん大きくし、
    //    盤とその上の層は `translate` で右下へずらす ── `transform-origin:0 0` なので
    //    translate は親の座標系（画面px）で効き、後ろの `scale` に巻き込まれない。
    //    ⚠️ 輪・道しるべ・バンド・その場の入力欄は実 DOM の `getBoundingClientRect`
    //    差分で置いている（`_ringTo` 等）ので、ここを動かしても自動で付いてくる。
    // 🔴 **余白は「器1つぶん」を下限にする**（2026-08-29 監査 D-2）。
    //    ⚠️ 直しの経路はスクロール1本しかなく、スクロールは 0〜(中身−器) で必ず頭打ちに
    //    なる。⭐ 指の下の点を動かさないためには「盤の左上を器の右下へ送れる」だけの
    //    余地が要る ── それがちょうど器1つぶん（下限が半分だと、器に収まっている
    //    状態からの1段目で頭打ちに当たって実測 x が135pxずれた）。
    //    ⭐ 余地はただの空白（スクロールできる範囲）なので、見た目は何も変わらない。
    //    ⚠️ これで箱は必ず器より大きくなる → `.edstagewrap` の `margin:auto` による
    //    中央寄せは**二度と効かない**。中央寄せは `center()` が明示的に置く
    //    （場合分けが消えて、どの倍率でも同じ式で済む）。
    const view = this._viewport()
    const px = Math.max(this._pad * k, view.w)
    const py = Math.max(this._pad * k, view.h)
    const stagewrap = document.getElementById('edstagewrap')
    if (stagewrap) {
      stagewrap.style.width = (1080 * k + 2 * px) + 'px'
      stagewrap.style.height = (1920 * k + 2 * py) + 'px'
    }
    const t = 'translate(' + px + 'px,' + py + 'px) scale(' + k + ')'
    stage.style.transform = t
    stage.dataset.scale = String(k)
    // ⭐ 不備の層（#edfaults）は #edstage と**同じ倍率**を持つ別の DOM（EditPage.razor
    //    参照 ── #edcap の中に置かない）。ここで一緒に合わせないと、盤を縮めたときだけ
    //    不備の枠が実物からずれる。
    const faults = document.getElementById('edfaults')
    if (faults) faults.style.transform = t
    // ⭐ Pass B: 格子も同じ倍率（盤の設計 px そのままの層なので、間隔の換算は要らない）。
    const grid = document.getElementById('edgrid')
    if (grid) grid.style.transform = t
    // ⭐ 器の真ん中に見えていた設計座標を、また真ん中へ戻す（上の `was` の註）。
    if (scroll && was) {
      const now = stage.getBoundingClientRect()
      scroll.scrollLeft += (now.left + wx * k) - cx
      scroll.scrollTop += (now.top + wy * k) - cy
    }
    this._lastK = k
  },

  /** ⭐ 盤を器の中央へ置く（2026-08-29 監査 D-1/D-2）。
   *
   * ⚠️ 以前は `.edstagewrap { margin:auto }` に任せていたが、余白を器1つぶんに広げた
   * ので箱は常に器より大きく、`margin:auto` は二度と効かない。⭐ 代わりにここが
   * 唯一の中央寄せ ── 実 DOM を測って差をスクロールへ入れるだけなので、
   * 倍率にも余白の広さにも依らない（`zoomTo` と同じ流儀）。
   *
   * 🔴 **これが初回表示の「盤が見えない」を塞ぐ。**⚠️ 余白の中に盤が浮いているので、
   * スクロール 0 のままだと画面には余白しか出ない（監査 D-1: 盤の下端が 88px 切れた）。 */
  center() {
    const scroll = document.getElementById('edscroll')
    const stage = document.getElementById(this._stageId || 'edstage')
    if (!scroll || !stage) return
    const sr = scroll.getBoundingClientRect()
    const r = stage.getBoundingClientRect()
    scroll.scrollLeft += (r.left + r.width / 2) - (sr.left + sr.width / 2)
    scroll.scrollTop += (r.top + r.height / 2) - (sr.top + sr.height / 2)
  },

  /** ⭐ E1-2: 「1ドット＝画面上で何pxか」を変える。⚠️ ここでは `fit()` を呼び直すだけ
   * （盤の実効倍率だけが変わる ── `RefreshView` は要らない）。選択の輪の再フィットは
   * `EditPage.ApplyDotPx` が `RingRefresh()`（旗を立てるだけ）で描画拍へ回す。
   * @param {number} dotPx 整数のみ（1/2/3/4/6/8）。段の妥当性は C# 側（`DotPxSteps`）で
   * 済ませてから渡す ── ここでは信じて使う。 */
  setDotPx(dotPx) {
    this._dotPx = dotPx
    if (this._wrapId && this._stageId) this.fit(this._wrapId, this._stageId)
  },

  /** ⭐ 倍率を変えても「指の下の1点」を画面上で動かさない（2026-08-29）。
   *
   * 🔴 **予測して計算しない ── 変えた後にもう一度実 DOM を測る。**
   * ⚠️ 器の中央寄せとスクロールの頭打ちを式で先回りしようとすると、場合分けを
   * 両方とも抱え込むことになる。⭐ だから2段で測る ── ①変える前に「指の下は
   * どの設計座標か」を読み、②変えた後に「その設計座標がいまどこに来たか」を読む。
   * 差だけスクロールへ戻せば、どんな置かれ方でも必ず合う
   * （この作品の「実 DOM から読む」流儀のまま ── `_drawGuide` と同じ考え方）。
   *
   * 🔴 **測り直すだけでは足りない**（2026-08-29 監査 D-2）。⚠️ 直しの経路はスクロール
   * 1本きりで、スクロールは 0〜(中身−器) で頭打ちになる ── 必要な差がその外なら
   * **黙って切り捨てられる**（実測: 器に収まっている状態からの1段目で x が135px、
   * 右下端まで送った状態からの縮小で x −668 / y −1088 ずれた）。⭐ だから `fit()` が
   * 余白を器1つぶん確保して、頭打ちに当たらないようにしてある（`fit` の註）。
   *
   * ⚠️ 段（1/2/3/4/6/8）の妥当性は C# 側（`EditPage.DotPxSteps`）で済ませてから渡す
   * ── ここでは信じて使う（`setDotPx` と同じ約束）。
   * @param {number} dotPx 新しい「1ドット＝画面上で何px」
   * @param {number} cx 動かしたくない点の実画面 x。**負なら器の中央**（釦での段送り用）
   * @param {number} cy 同 y */
  zoomTo(dotPx, cx, cy) {
    const scroll = document.getElementById('edscroll')
    const stage = document.getElementById(this._stageId || 'edstage')
    // ⚠️ まだ盤が無い拍（段階制の最初）は、今までどおり寸法を合わせるだけ。
    if (!scroll || !stage) { this.setDotPx(dotPx); return }
    const sr = scroll.getBoundingClientRect()
    const ax = cx >= 0 ? cx : sr.left + sr.width / 2
    const ay = cy >= 0 ? cy : sr.top + sr.height / 2
    const before = stage.getBoundingClientRect()
    const k1 = (this._dotPx || 4) / 4
    // ⚠️ 盤の外（余白のうんと外）は約束の外 ── 縁へ丸める（`_anchorIn` の註）。
    const w = this._anchorIn((ax - before.left) / k1, (ay - before.top) / k1)
    const wx = w.x, wy = w.y
    this.setDotPx(dotPx)   // ⭐ fit() が同じ拍で寸法と transform を書き換える（同期）
    const after = stage.getBoundingClientRect()
    const k2 = (this._dotPx || 4) / 4
    scroll.scrollLeft += (after.left + wx * k2) - ax
    scroll.scrollTop += (after.top + wy * k2) - ay
  },

  /** ⭐ E1-2: 「器に収まる最大の整数 `dotPx`」を選ぶ（既定値・`EditPage.ZoomReset` の中身）。
   * ⚠️ 計画 §3 の `P = floor(min(実幅/270, 実高/480))` と同じ考え方 ── ただし段は
   * 1/2/3/4/6/8 の6つだけ（Aseprite 風の丸い刻み）。器が測れない・0以下なら 4
   * （初期値と同じ・変な値で盤が消えるのを避ける）。
   *
   * 🔴 **余白（`_pad`）は数えない**（2026-08-29 監査 D-1 への答え）。⚠️ 数えると
   * 「余白ごと器に収まる倍率」になり、器 1400x1000 で 2 → **1**（盤が 270x480 の
   * 極小）まで落ちる ── 「既定が小さすぎる」は元々の不満そのもので、直しが不満を
   * 増やしては本末転倒。⭐ 余白はスクロールできる遊びであって「収める対象」ではない。
   * 監査が見た「盤の下端が 88px 切れる」は、選んだ倍率のせいではなく**余白の中に
   * 盤が浮いたまま置かれていた**せい ── `center()` で盤を器の中央に置けば消える
   * （盤自体はこの倍率で器に収まっている、というこの関数の約束は守られる）。
   * @param {string} wrapId 器の id @returns {number} 選んだ dotPx。 */
  autoDotPx(wrapId) {
    const steps = [1, 2, 3, 4, 6, 8]
    const wrap = document.getElementById(wrapId)
    if (!wrap) return 4
    const r = wrap.getBoundingClientRect()
    if (r.width <= 0 || r.height <= 0) return 4
    const limit = 4 * Math.min(r.width / 1080, r.height / 1920)
    let best = steps[0]
    for (const s of steps) if (s <= limit) best = s
    return best
  },

  /** ⭐ E2: いま選んでいる層（`EditLayers.Token` と同じ語彙: ""/"paint"/"dynamic"/"tap"/
   * "container"）。⚠️ **「薄くする」と「触れなくする」は別々の軸**（計画「片方だけだと
   * 必ず不満が出る」）── 「薄くする」は Razor が `#edstage` の `data-layerfilter` 属性を
   * 直接持つ（CSS だけで完結・`EditPage.razor` の `_layer` と再描画のたびに揃う）。
   * ここは「触れなくする」（`_layerOk`）の判定用に、JS 側の状態だけを更新する。
   * @param {string|null} token 空文字/null なら「すべて」（掛けない）。 */
  setLayer(token) {
    this._layer = token || null
  },

  /** ⭐ E2: 「触れなくする」の判定そのもの。⚠️ 層を選んでいなければ常に true
   * （素通し）。`el` は `data-layer` を持つ節点（`.n`）であること。 */
  _layerOk(el) {
    return !this._layer || (el instanceof Element && el.dataset.layer === this._layer)
  },

  /** ⭐ 段階4a: 「落とした点の真下の行 → その子として入れる入れ物の行」の対応表。
   * 🔴 **判断は C# 側**（`EditPage.IsContainer`/`ContainerAt`/`DropTargetCsv`）── ここは
   * 引くだけ。入れ物かどうかの規則も、どの祖先が当たるかも JS には持たせない
   * （持たせると規則が2か所に割れる ── 帯の潜り検査が「テストだけが知る規則」だった
   * のと同じ失敗を繰り返さない）。
   * @param {string} csv `真下の行:入れ物の行` をカンマで並べた字。 */
  setDrops(csv) {
    this._drops = new Map()
    for (const pair of String(csv || '').split(',')) {
      if (!pair) continue
      const at = pair.indexOf(':')
      if (at > 0) this._drops.set(pair.slice(0, at), pair.slice(at + 1))
    }
  },

  /** ⭐ **点の真下に在る節点を探す、唯一の出所**（2026-08-29 に3つの写しを畳んだ）。
   *
   * ⚠️ 以前は同じ手順が3か所（掴み用の `nodeAt`・タップ用の `pickAt`・落とし先の
   * `_dropNodeAt`）に写してあり、**1か所だけ直すと次にずれる**形だった ── 実際、
   * 不備の板を素通りする手当てを入れる段でそれが問題になった（監査 A-2）。
   * ⭐ 違うのは「何を探すか」（`sel`）だけなので、そこだけ引数にして中身を1つにする。
   *
   * 🔴 **不備の板（`#edfaults`）は素通りする。**⚠️ 板は盤より手前（z-index 1）に居て
   * `pointer-events:auto`、しかも `OriginAttrs` が板自身に `data-line`／`data-part` を
   * 付ける。⭐ 板の形（`Focus`）は仕様上「実体の外」へはみ出すので、**画面上は何も
   * 無い所を指したのに遠くの節点が当たる**（落とし先の判定では、見えていない入れ物の
   * 子として1行挿さる）。板を押して選ぶ道は `faultsListen` が別に持っている。 */
  _hitAt(x, y, sel) {
    const cap = document.getElementById('edcap')
    if (cap) cap.style.pointerEvents = 'none'
    const stack = document.elementsFromPoint(x, y)
    if (cap) cap.style.pointerEvents = 'auto'
    for (const el of stack) {
      if (!(el instanceof Element)) continue
      if (el.closest('#edfaults')) continue
      const found = el.closest(sel)
      if (found && !found.closest('#edfaults') && this._layerOk(found)) return found
    }
    return null
  },

  /** ⭐ 掴む／落とすときの探し方 ── 「いま直している文書の節点」だけ。
   * ⚠️ 部品を直しているときは、その部品の節点（`data-part="<_of>"`）だけを見る
   * （途中で別の部品や土台へ迷い込むと事故る）。 */
  _nodeSel() {
    return this._partId ? '[data-part="' + this._partId + '"]' : '[data-line]'
  },

  /** ⭐ 段階4a: 落とし先の判定に使う（`listen()` の閉包の外から呼ぶため object に置く）。 */
  _dropNodeAt(x, y) {
    return this._hitAt(x, y, this._nodeSel())
  },

  /** 器の大きさの変化を見張って、盤を追従させる。 */
  start(wrapId, stageId) {
    const wrap = document.getElementById(wrapId)
    if (!wrap) return
    if (this._ro) this._ro.disconnect()
    this._ro = new ResizeObserver(() => this.fit(wrapId, stageId))
    this._ro.observe(wrap)
    this.fit(wrapId, stageId)
    // ⭐ 最初の1回だけ盤を器の中央へ置く（2026-08-29 監査 D-1）。⚠️ 余白の中に盤が
    //    浮いているので、スクロール 0 のままだと画面に余白しか出ない。⭐ 2回目以降は
    //    置かない ── 盤が出たり消えたりするたび（段階制）見ていた場所へ戻されると、
    //    かえって作業を邪魔する（`fit()` 自身が居場所を保つので放っておいてよい）。
    if (!this._centered) { this._centered = true; this.center() }
    // ⭐ Pass B 拡大パンの後始末（2026-08-24 の実測バグ修正）:
    //    輪／ホバーは `#edscroll` の**外**（`#edwrap` 直下）に居るので、`#edscroll` を
    //    スクロールしても一緒には動かない ── 節点だけがスクロールで動いて、輪が置き去りに
    //    なる（縦120スクロールで輪が節点から120ずれるのを Playwright で確認）。⭐ だから
    //    `#edscroll` のスクロールを拾って、最後に描いた選択（`_selCsv`/`_selPrimary`）と
    //    ホバー（`_hoverLine`）を実 DOM の今の位置で描き直す。⚠️ scroll は bubble しないので
    //    **capture 相**で document に付ける（`#edscroll` が段階制で後から生まれても効く）。
    if (!this._scrollBound) {
      // ⚠️ scroll ハンドラで**直接**描き直す（rAF で間引かない）── 輪の描き直しは
      //    数個の div の付け替えだけで軽く、scroll イベント自体ブラウザが1描画に間引く。
      //    ⭐ rAF に頼ると「描画が無いと rAF が回らない」実行環境で追従が止まる。
      this._scrollBound = (e) => {
        if (!(e.target instanceof Element) || e.target.id !== 'edscroll') return
        if (this._selCsv) this.reselect(this._selCsv, this._selPrimary, this._selMismatch)
        if (this._hoverLine) this.hoverOn(this._hoverLine)
        // ⭐ D-3: その場の入力欄も、輪・ホバーと同じ理由でスクロール追従が要る。
        if (this._editFocusLine) this.editAt(this._editFocusLine)
      }
      document.addEventListener('scroll', this._scrollBound, true)
    }
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
    let banding = false   // ⭐④ 節点の無い所で down したか（ラバーバンドの候補）
    let bandFrom = null   // バンドの起点（実画面座標）
    let bandActive = false // ⭐④ PLAY を超えて実際にバンドを描き始めたか
    let lastHover = ''    // ⭐ Pass B 盤→木: 直前に木へ知らせた行（間引くための記憶）
    // ⭐ パン（2026-08-29）── 掴み始めたときのスクロール位置を覚えておく。
    //    ⚠️ 「今の位置から積む」のではなく起点からの累計にする（`Dragging` と同じ理由 ──
    //    端で止まったぶんが積み重なって指と盤がずれるのを防ぐ）。
    let panning = null    // {x, y, sl, st}｜null＝パンしていない

    // ⭐ 覆いをどけて、その真下に何が描かれているかを見る（一瞬だけ）。
    // ⚠️ **部品を選んでいるときは `data-part="<_of>"` だけを探す。**
    //    ⭐ 他の部品や土台自身の節点（`data-line` しか持たない）は拾わない
    //    ── `[data-part="X"]` は X という部品自身の節点にしか付かない
    //    （`LayoutDom.cs` が `PartId` からそのまま出す）。
    // ⭐ E2: 「触れなくする」── 層を選んでいるときは、その層でない節点を素通りして、
    //    下に重なっている一致する節点を探す（`elementsFromPoint` は重なり順で全部返す）。
    //    ⚠️ 一致するものが1つも無ければ null（`nodeAt` の呼び出し元は「押しどころが無い」
    //    と同じ扱いにする ── だからラバーバンド／囲んで作るの候補になる）。
    // ⚠️ 中身は `_hitAt`（唯一の出所）── ここは「何を探すか」を渡すだけ。
    const nodeAt = (x, y) => this._hitAt(x, y, this._nodeSel())

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
    // ⚠️ 中身は `_hitAt`（唯一の出所）── こちらは文書で縛らず「いちばん近い出所」を探す。
    const pickAt = (x, y) => this._hitAt(x, y, '[data-part],[data-line]')

    // ⭐ **パンは器（`#edscroll`）で拾う**（2026-08-29 監査 D-3）。
    //    🔴 以前は覆い（`#edcap`＝盤そのもの 1080x1920）の中だけで拾っていた ──
    //    余白（`_pad`）は「盤の外へ出た節点を見に行く」ために足したのに、
    //    **その余白の上でこそ掴んで動かせなかった**（dotPx=8 なら片側512画面px の死角）。
    //    ⭐ ホイール（`#edwrap`）と扱いを揃える。
    //    ⚠️ document の**捕捉相**に張る ── `#edscroll` は段階制で後から生まれるし、
    //    捕捉相なら覆いの `down` より先に見て `stopPropagation` で止められる
    //    （盤の上で始めても「掴んで動かす」に化けない）。
    //    ⭐ 割り当ては Tiled / Aseprite と同じ（中ボタンドラッグ・Space+ドラッグ）。
    const panDown = (e) => {
      // 🔴 **押した釦を見る**（監査 C-2）。⚠️ Space 中の**右**クリックまで拾うと、
      //    文脈メニューが開いて `pointerup` が届かず、パンが掴んだまま残る ──
      //    次の左ドラッグが選択でなくパンになり、選択が1回黙って消える。
      if (!(e.button === 1 || (this._space && e.button === 0))) return
      if (!(e.target instanceof Element) || !e.target.closest('#edscroll')) return
      const scroll = document.getElementById('edscroll')
      if (!scroll) return
      e.preventDefault()
      e.stopPropagation()
      panning = { x: e.clientX, y: e.clientY, sl: scroll.scrollLeft, st: scroll.scrollTop, id: e.pointerId }
      try { scroll.setPointerCapture(e.pointerId) } catch { /* 捕まえられなくても続ける */ }
    }
    const panMove = (e) => {
      if (!panning) return
      e.stopPropagation()
      // ⭐ 起点からの累計で置き直す（指の動きと盤の動きが1:1）。
      //    ⚠️ 「今の位置から積む」のではなく起点から ── 端で止まったぶんが積み重なって
      //    指と盤がずれるのを防ぐ（`Dragging` と同じ理由）。
      const scroll = document.getElementById('edscroll')
      if (scroll) {
        scroll.scrollLeft = panning.sl - (e.clientX - panning.x)
        scroll.scrollTop = panning.st - (e.clientY - panning.y)
      }
    }
    /** ⚠️ パンを畳む唯一の出所（`pointerup`/`pointercancel`/Escape が共有）。 */
    const panStop = () => {
      if (!panning) return false
      const scroll = document.getElementById('edscroll')
      try { if (scroll) scroll.releasePointerCapture(panning.id) } catch { /* 既に外れていてもよい */ }
      panning = null
      return true
    }
    const panUp = (e) => {
      if (!panning) return
      e.stopPropagation()
      panStop()
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
      // ⭐④ 節点の無い所で down したら、ラバーバンドの候補にする（up で PLAY を超えて
      //    いたかどうかで、本当にバンドを引いたかを見分ける ── move と同じ「遊び」）。
      banding = line === null
      bandFrom = banding ? { x: e.clientX, y: e.clientY } : null
      bandActive = false
      // ⚠️ 失敗しても以降を止めない（`releasePointerCapture` と同じ扱い）。
      //    捕まえ損ねても、この後の move/up は cap 自身に直接届く分には困らない
      //    ── 困るのは「盤の外まで指が出た」ときだけで、それは実使用では稀。
      try { cap.setPointerCapture(e.pointerId) } catch { /* 捕まえられなくても続ける */ }
    }

    const move = (e) => {
      // ⚠️ パン中はここへ来ない（`panMove` が捕捉相で止める）── 万一届いても触らない。
      if (panning) return
      if (banding) {
        const dx = e.clientX - bandFrom.x
        const dy = e.clientY - bandFrom.y
        if (!bandActive) {
          // 🔴 数px揺れただけではバンドにしない。PLAY を超えて初めて描き始める。
          if (Math.abs(dx) <= PLAY && Math.abs(dy) <= PLAY) return
          bandActive = true
        }
        this._bandDraw(bandFrom.x, e.clientX, bandFrom.y, e.clientY)
        return
      }
      if (!from) {
        // ⭐ Pass B 盤→木: 押してもバンドもしていない、ただの通りすがりの移動。
        //    ⚠️ 掴んで動かす（`nodeAt`）と同じ探し方（部品を選んでいるときはその部品の
        //    中だけ）。下の節点が変わったときだけ木へ知らせる（間引く）。
        const node = nodeAt(e.clientX, e.clientY)
        const hoverLine = node ? this._lineOf(node) : ''
        if (hoverLine !== lastHover) {
          lastHover = hoverLine
          this._call(owner, 'HoverLine', hoverLine)
        }
        return
      }
      if (line === null) return
      const dx = e.clientX - from.x
      const dy = e.clientY - from.y
      if (!dragging) {
        // 🔴 数px揺れただけでは動かさない。PLAY を超えて初めて「掴んで動かす」を始める。
        if (Math.abs(dx) <= PLAY && Math.abs(dy) <= PLAY) return
        dragging = true
        this._call(owner, 'DragStart', line, repeatIndex)
      }
      // ⭐ **k で割る。**盤には倍率が掛かっているので、指の実画面移動量を
      //    設計 px（骨組みの Left/Top と同じ単位）へ戻す。
      this._call(owner, 'Dragging', dx / k, dy / k)
    }

    const up = (e) => {
      // 🔴 **この覆いで始まっていない指は、触らない**（2026-08-24 の実測バグ修正）。
      //    ⚠️ 覆い（`#edcap`）は盤いっぱいに広がっているので、**道具箱から掴んできた指**
      //    （`paletteListen` の掴み）が盤の上で離れると、この `up` にも届く。すると
      //    「動かさずに離した＝タップで選ぶ」の枝へ落ち、`pickAt` が拾った先が部品だと
      //    **別の骨組みファイルへ切り替わって**しまう ── 落としたはずの節点が
      //    「今開いている文書」から消えたように見える（実測: 盤の節点は増えず、
      //    木には別ファイルの名前が並んだ）。
      //    ⭐ `from` は覆いの `down` でだけ立つ ── 立っていなければ他所の指。
      // ⚠️ パン中はここへ来ない（`panUp` が捕捉相で止める）── 万一届いても触らない
      //    （選ぶ・掴むの状態には最初から触っていない）。
      if (panning) return
      if (!from) return
      try { cap.releasePointerCapture(e.pointerId) } catch { /* 既に外れていてもよい */ }
      const additive = (e.shiftKey || e.ctrlKey || e.metaKey) ? 'add' : ''

      // ⭐④ バンドが実際に「遊び」を超えて描かれていたら、交差した節点を集めて終わる
      //    （タップ選択の分岐へは落とさない ── 別の動作として扱う）。
      //
      // 🔴 E2: **押しどころの層のときだけ「囲む＝作る」**（計画 §11-6）。他の層では
      //    今までどおり「囲む＝選ぶ」（`BandSelect`）── 既存のラバーバンドと衝突しない
      //    よう、層で分岐を切り分ける（同じ「囲む」ジェスチャの**先**だけを変える）。
      if (banding && bandActive) {
        const rect = {
          left: Math.min(bandFrom.x, e.clientX), right: Math.max(bandFrom.x, e.clientX),
          top: Math.min(bandFrom.y, e.clientY), bottom: Math.max(bandFrom.y, e.clientY),
        }
        this._bandHide()
        if (this._layer === 'tap') {
          const stage = document.getElementById('edstage')
          if (stage) {
            const sr = stage.getBoundingClientRect()
            const k2 = Number(stage.dataset.scale || '1')
            const left = (rect.left - sr.left) / k2
            const top = (rect.top - sr.top) / k2
            const width = (rect.right - rect.left) / k2
            const height = (rect.bottom - rect.top) / k2
            this._call(owner, 'CreateTapAt', left, top, width, height)
          }
        } else {
          const lines = this._bandCollect(rect)
          this._call(owner, 'BandSelect', lines.join(','), additive)
        }
        banding = false; bandFrom = null; bandActive = false
        from = null; line = null; dragging = false; repeatIndex = ''
        return
      }
      banding = false; bandFrom = null; bandActive = false

      if (dragging) {
        this._call(owner, 'DragEnd')
      } else {
        // ⭐ 動かさずに離した＝「選ぶ」。
        //
        // 🔴 **一度押しは、いま開いている文書の節点しか選ばない**（2026-08-29）。
        //    ⚠️ 前は `pickAt`（近い方優先）で、部品の上を押すと**その場で別の
        //    骨組みファイルへ切り替わって**いた ── 位置を直そうと指を置いただけで
        //    文書が飛び、取り消しの控えも消える（`ApplyOf` が `_undo.Clear()`）。
        //    誤って踏むと「確認 → 切り替え → 選び直し」で5手が消える事故だった。
        // ⭐ 部品の中を押しても、その祖先＝**この文書で動かせる節点**（`use=` の行や
        //    `host` の枠）が選ばれる ── Godot がインスタンス化した部分シーンを
        //    1クリックでインスタンスの根として選ぶのと同じ型。Figma も1クリックは
        //    最外・掘るのはダブルクリック、で揃っている。
        // ⭐ 部品そのものを直したいときは**ダブルクリック**（下の `dblclick`）か、
        //    右の「部品を開く」釦。掘る操作を明示にした。
        const node = nodeAt(e.clientX, e.clientY)
        if (node) this._ringTo(node); else this._ringHide()
        if (node && node.dataset.part) {
          // ⭐ 押した先が部品 ── ②「その部品のファイルへ切り替えて、その節点を選ぶ」。
          //    同じ部品を直している最中なら、C# 側で「ただの選び直し」に落ちる。
          this._call(owner, 'PickedPart', node.dataset.part, node.dataset.partLine || '-1', additive)
        } else {
          // ⭐ `data-line` のみ ── 自前の行、または（部品を直している最中なら）
          //    土台の行。どちらかは C# 側（`Scenes.Of(_of).ByPart`）が判じる。
          this._call(owner, 'Picked', node ? (node.dataset.line || '') : '', additive)
        }
      }
      from = null; line = null; dragging = false; repeatIndex = ''
    }

    const cancel = () => {
      // ⭐ パンは何も確定するものが無い（スクロール位置は既にその場で反映済み）。
      //    ⚠️ 指の捕まえは必ず外す（`panStop` が唯一の出所 ── 監査 C-3）。
      if (panStop()) return
      // ⚠️ 途中で指が奪われた（他のジェスチャに割り込まれた等）。⭐ 動いていたなら、
      //    そこまでの分を1つの動作として確定する（宙ぶらりんにしない）。
      if (dragging) this._call(owner, 'DragEnd')
      if (banding) this._bandHide()
      banding = false; bandFrom = null; bandActive = false
      from = null; line = null; dragging = false; repeatIndex = ''
    }

    // ⭐ Pass B 盤→木: 指が盤の覆いから出たら、木のハイライトも解く
    //    （出たままだと、もう盤の上に無い節点が木にハイライトされ続けて嘘になる）。
    const leave = () => {
      if (lastHover !== '') {
        lastHover = ''
        this._call(owner, 'HoverLine', '')
      }
    }

    // ⭐ ダブルクリックは「1段掘る」。⚠️ `#edcap` が指を先に取るので、ここ（cap 側）で
    // 拾う ── 下にある節点は `pickAt`（近い方優先＝部品の節点まで見る探し方）で見る。
    //
    // ⭐ 行き先は2つ。**同じ文書の節点なら D-3 のその場の文字入力**（label/button）、
    //    **別の文書＝部品の節点ならその部品ファイルへ切り替える**（2026-08-29）。
    //    ⚠️ 対象が排他なので衝突しない ── 部品の中身は、いま開いている文書の
    //    行を持たない（`data-part` しか持たない）。
    // ⭐ 一度押しが「同じ文書だけ」になったぶんの逃げ道がこれ（Figma の deep select と
    //    同じ割り当て）。切り替えは既存の `PickedPart` を通す ── 未保存の直しがあれば
    //    `ConfirmSwitchIfDirty` が確かめる道に、そのまま乗る。
    const dblclick = (e) => {
      const node = pickAt(e.clientX, e.clientY)
      if (!node) return
      const sameDoc = this._partId ? (node.dataset.part === this._partId) : !node.dataset.part
      if (!sameDoc) {
        if (node.dataset.part)
          this._call(owner, 'PickedPart', node.dataset.part, node.dataset.partLine || '-1', '')
        return
      }
      const line = this._lineOf(node)
      if (line === '') return
      this._call(owner, 'BeginTextEdit', line)
    }

    // ⭐ Escape（`keys`）から呼べるように、掴みの取り消しを外へ出しておく
    //    （2026-08-29）。⚠️ 新しい戻し方は作らない ── 既にある `cancel`（指が奪われた
    //    ときと同じ後始末）をそのまま使う。⭐ 掴んでいなければ false を返して、
    //    呼び手（Escape）が「では選択を解く」へ進めるようにする。
    this._cancelDrag = () => {
      // 🔴 **パンも止める**（2026-08-29 監査 C-3）。⚠️ 見ていなかったので Escape が
      //    「掴んでいない」と判断し、代わりに選択を解いていた（パンは続いたまま）。
      if (panStop()) return true
      if (!from && !dragging && !banding) return false
      cancel()
      return true
    }

    // ⭐ **Ctrl+ホイールで、指の下を動かさずに1段ずつ拡大・縮小**（2026-08-29）。
    //    ⚠️ `{ passive: false }` ＋ `preventDefault` の両方が要る ── 付けないと
    //    ブラウザのページ拡大が先に効いて、盤ではなく画面全体が伸びる。
    //    ⚠️ 段の一覧（`DotPxSteps`）は C# が持つ ── ここは**向きだけ**を渡す
    //    （矢印キーのナッジで JS が向きだけを渡すのと同じ役割分担・出所を割らない）。
    //    ⭐ Ctrl 無しのホイールには触らない（今までどおり `#edscroll` が縦に流れる。
    //    Shift+ホイールの横流しもブラウザ既定のまま ── Tiled / Aseprite と同じ）。
    //    ⚠️ 覆い（`#edcap`）でなく `#edwrap` に張る ── 余白（`_pad`）の上でも効かせたい。
    const wheelOn = document.getElementById('edwrap')
    if (this._wheelBound && this._wheelOn)
      this._wheelOn.removeEventListener('wheel', this._wheelBound)
    if (wheelOn) {
      this._wheelBound = (e) => {
        if (!e.ctrlKey && !e.metaKey) return
        e.preventDefault()
        this._call(owner, 'ZoomStepAt', e.deltaY < 0 ? 1 : -1, e.clientX, e.clientY)
      }
      this._wheelOn = wheelOn
      wheelOn.addEventListener('wheel', this._wheelBound, { passive: false })
    }

    this._bound = [['pointerdown', down], ['pointermove', move], ['pointerup', up],
      ['pointercancel', cancel], ['pointerleave', leave], ['dblclick', dblclick]]
    for (const [t, f] of this._bound) cap.addEventListener(t, f)

    // ⭐ パンだけは document の捕捉相へ（上の `panDown` の註 ── 余白の上でも効かせる）。
    //    ⚠️ 張り直しのたびに前のものを必ず外す（`listen` は盤が出るたび呼ばれる）。
    if (this._panBound) for (const [t, f] of this._panBound) document.removeEventListener(t, f, true)
    this._panBound = [['pointerdown', panDown], ['pointermove', panMove],
      ['pointerup', panUp], ['pointercancel', panUp]]
    for (const [t, f] of this._panBound) document.addEventListener(t, f, true)
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
      this._call(owner, 'ResizeStart', handle)
    }
    const move = (e) => {
      if (!handle || !from) return
      const dx = (e.clientX - from.x) / k
      const dy = (e.clientY - from.y) / k
      this._call(owner, 'Resizing', dx, dy)
    }
    const up = () => {
      if (handle) this._call(owner, 'ResizeEnd')
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
      // ⚠️③④ 不備一覧・Focus からの選択は常に単独選択（additive でない）。
      if (el.dataset.part) {
        this._call(owner, 'PickedPart', el.dataset.part, el.dataset.partLine || '-1', '')
      } else {
        this._call(owner, 'Picked', el.dataset.line || '', '')
      }
    }
    this._fBound = fn
    layer.addEventListener('pointerup', fn)
  },

  /** ⭐ D-2: 道具箱（種類パレット）から盤へ掴んで落とす。⚠️ pointer events で統一
   * （drag&drop は使わない ── この頁は既に pointer で統一されている）。
   *
   * ⭐ 押しただけ（`PLAY` 以内で離した）は何もしない ── ネイティブの `click` が
   * ボタンの上でそのまま起きるので、既存の `@onclick`（`EditPage.AddKind`）が
   * 今までどおり拾う（ここでは `preventDefault` を呼ばない）。
   *
   * @param {object} owner .NET 側の受け口
   * @param {string} paletteId 「足す」の釦が並ぶ行の id（`.edadd-btn` の親）。 */
  paletteListen(owner, paletteId) {
    const row = document.getElementById(paletteId)
    if (!row) return
    if (this._pBound) for (const [t, f] of this._pBound) row.removeEventListener(t, f)

    const PLAY = 12   // ⭐ tap.js/edit.js の他の掴みと同じ「遊び」のしきい値
    let btn = null, kind = null, from = null, dragging = false

    const down = (e) => {
      const el = e.target instanceof Element ? e.target.closest('.edadd-btn') : null
      if (!el) return
      btn = el
      kind = el.dataset.kind
      from = { x: e.clientX, y: e.clientY }
      dragging = false
      // ⚠️ ここでは e.preventDefault() しない ── 動かさずに離したときのネイティブ
      //    click（既存の @onclick → AddKind）をそのまま残す。
      //
      // 🔴 **`setPointerCapture` は使わない**（2026-08-24 の実測バグ修正）。
      //    ⚠️ 釦が指を掴み続けると、盤の上で離しても `pointerup`＋`click` が**釦へ引き戻される**
      //    ── ブラウザが釦の click を出し、`@onclick`（`AddKind`）が**既定の位置にもう1つ**
      //    作ってしまう（実測: 1回落として2つできた／取り消しも2）。
      //    ⭐ 掴んでいる間だけ document で move/up を聞けば、指が盤へ出ても追える。
      //    こうすると mousedown（釦）と mouseup（盤）が別の要素になるので、
      //    ブラウザは釦の click を出さない ── 二重に作られる道が根から消える。
      document.addEventListener('pointermove', move)
      document.addEventListener('pointerup', up)
      document.addEventListener('pointercancel', cancel)
    }
    /** ⚠️ 掴みが終わったら必ず document から降りる（付けっぱなしにしない）。 */
    const unhook = () => {
      document.removeEventListener('pointermove', move)
      document.removeEventListener('pointerup', up)
      document.removeEventListener('pointercancel', cancel)
    }
    // ⭐ 頁を離れるときにも降ろせるように外へ出す（2026-08-29 監査 C-5）。
    //    ⚠️ 掴んでいる最中に頁を離れると、離した拍に**破棄済みの受け口**へ
    //    `AddKindAt` が飛ぶ（`stop()` の対象外だった唯一の document 直付け）。
    this._palUnhook = () => { unhook(); btn = null; kind = null; from = null; dragging = false; this._ghostHide() }
    const move = (e) => {
      if (!from || !kind) return
      const dx = e.clientX - from.x, dy = e.clientY - from.y
      if (!dragging) {
        // 🔴 数px揺れただけでは動かさない。PLAY を超えて初めて「掴んで動かす」を始める。
        if (Math.abs(dx) <= PLAY && Math.abs(dy) <= PLAY) return
        dragging = true
      }
      this._ghostShow(e.clientX, e.clientY, kind)
      // ⭐ 段階4a: 落とすと「子」になる入れ物を、既存のホバー枠で光らせる
      //    （新しい層は増やさない ── 木→盤のホバーと同じ `#edhover`）。
      //    ⚠️ 見せるだけ。実際にどこへ入るかは、離した拍に C# が決め直す。
      //    ⚠️ `_drops` は C# が配るまで無い（初回描画より前に掴めることは無いが、
      //    無ければ「入れ物なし」と同じ扱いにする ── 例外で掴みが死なないように）。
      const over = this._dropNodeAt(e.clientX, e.clientY)
      const target = (over && this._drops) ? this._drops.get(this._lineOf(over)) : undefined
      if (target !== undefined) this.hoverOn(target); else this.hoverOff()
    }
    const up = (e) => {
      unhook()   // ⚠️ 掴みの間だけの document 聞き耳を降ろす（`down` の註）
      if (dragging) {
        this._ghostHide()
        this.hoverOff()   // ⭐ 段階4a: 落とし先の強調を必ず消す（作っても作らなくても）
        const stage = document.getElementById('edstage')
        // ⭐ 覆い越しに下を見る（`listen()` の `nodeAt`/`pickAt` と同じ理由 ──
        //    ここは覆いを挟まないので、そのまま `elementFromPoint` でよい）。
        const el = document.elementFromPoint(e.clientX, e.clientY)
        const overBoard = !!(stage && el && el.closest('#edstage'))
        if (overBoard) {
          // ⭐ 落とした画面座標 → 設計座標。#edstage の左上を原点に、実効倍率 k で割る
          //    （拡大・スクロールが効いていても getBoundingClientRect は画面基準なので
          //    このままで正しい ── 段階2の掴んで動かすと同じ理屈）。
          const r = stage.getBoundingClientRect()
          const k = Number(stage.dataset.scale || '1')
          const left = (e.clientX - r.left) / k
          const top = (e.clientY - r.top) / k
          // ⭐ 段階4a: 真下に在った節点の行も渡す（入れ物なら「その子」として入る）。
          //    ⚠️ 入れ物かどうかを決めるのは C# 側 ── ここは「何の上で離したか」だけ言う。
          const over = this._dropNodeAt(e.clientX, e.clientY)
          const line = over ? this._lineOf(over) : ''
          const hit = line === '' ? -1 : Number(line)
          this._call(owner, 'AddKindAt', kind, left, top, Number.isFinite(hit) ? hit : -1)
        }
        // ⚠️ 盤の外で離したら何もしない（作らない）。
      }
      // ⚠️ dragging が false（動かさずに離した）ならここでは何もしない ── ブラウザの
      //    ネイティブ click が @onclick（AddKind）を今までどおり起こす。
      btn = null; kind = null; from = null; dragging = false
    }
    const cancel = () => {
      unhook()
      this._ghostHide()
      btn = null; kind = null; from = null; dragging = false
    }

    // ⚠️ 釦の上で始まる `pointerdown` だけを列（`row`）で聞く。動かしている間の
    //    move/up は `down` が document へ付ける（釦に指を縛らないため ── 上の註）。
    this._pBound = [['pointerdown', down]]
    this._pRow = row   // ⭐ `stop()` が外せるように覚えておく（2026-08-29 監査 C-5）
    for (const [t, f] of this._pBound) row.addEventListener(t, f)
  },

  /** ⭐ D-2: ゴースト（作ろうとしている四角の輪郭）を指へ合わせる。⚠️ `#edghost` は
   * `position:fixed`（viewport 座標）── ドラッグの起点はパレット（盤の外）なので、
   * `#edwrap` の `overflow:hidden` に巻き込まれない場所に置いてある。
   * @param {number} x,y 実画面座標（指の位置＝四角の中心にする）。
   * @param {string} kind 掴んだ種類（既定寸法の見た目合わせに使う ── `EditPage.DefaultSize`
   *   と同じ数、`icon` だけ 64x64。C# 側と二重の出所になるが、こちらは見た目だけの
   *   ゴースト ── 実際に作る大きさの唯一の出所は `AddKindAt`/`DefaultSize` のまま）。 */
  _ghostShow(x, y, kind) {
    const g = document.getElementById('edghost')
    if (!g) return
    const stage = document.getElementById('edstage')
    const k = Number((stage && stage.dataset.scale) || '1')
    // ⚠️ `EditPage.DefaultSize` と同じ振り分けにする（2026-08-29 監査 G-2）── 以前は
    //    `icon` しか見ておらず、**`背景`（paint）を掴むと 300x120 のゴーストが出て
    //    離すと 64x64 が生まれる**（見せた形と作る形が違う）。
    const tiny = kind === 'icon' || kind === 'paint'
    const small = kind === 'label' || kind === 'button'
    const w = (tiny ? 64 : 300) * k
    const h = (tiny ? 64 : small ? 90 : 120) * k
    g.style.left = (x - w / 2) + 'px'
    g.style.top = (y - h / 2) + 'px'
    g.style.width = w + 'px'
    g.style.height = h + 'px'
    g.style.display = 'block'
  },

  _ghostHide() {
    const g = document.getElementById('edghost')
    if (g) g.style.display = 'none'
  },

  /** ⭐ 未保存の直しを捨てる前に、1度だけ確かめる。⚠️ 新しい UI を作らず、
   * ブラウザ標準の `confirm` を使う（過剰な抽象化を避ける指示どおり）。
   * `EditPage.ConfirmSwitchIfDirty` からだけ呼ぶ。 */
  confirmDiscard() {
    return window.confirm('保存していない直しがあります。捨てて切り替えますか？')
  },

  /** ⭐ 確認で「やめる」を選んだとき、select の見た目を C# 側の値へ戻す。
   * ⚠️ Blazor は render tree に差分が無いと DOM を触らない（value 属性は変わっていない）
   * ので、C# から StateHasChanged しても戻らない ── ここで実 DOM の value を書き戻す。 */
  revert(id, value) {
    const el = document.getElementById(id)
    if (el) el.value = value
  },

  /** ⭐ 切替（checkbox）版の `revert`。⚠️ `checked` は `value` とは別の属性なので
   * 上の `revert` では戻せない。使う理由は `revert` と同じ ── Blazor はモデルの値が
   * 変わらないと実 DOM を触らないので、「打った字と書いた字が違う」ときに
   * 見た目だけが打った姿で残る（`when=` の `!` で実際に踏んだ ── 監査 B-2）。 */
  revertCheck(id, on) {
    const el = document.getElementById(id)
    if (el) el.checked = !!on
  },

  /** ⭐ Ctrl+Z / Ctrl+Shift+Z（取り消し／やり直し）と、⭐② 矢印キー（ナッジ）、
   * ⭐ 段階3: Delete キー（選択を消す）、⭐ 2026-08-29: Shift+矢印（粗いナッジ）・
   * Ctrl+D（複製）・Escape（選択を解く）。
   * ⚠️ **document 全体**で聞く（数値欄にフォーカスがあっても Ctrl+Z が効くように）
   * ── だから離れるとき必ず外す（`stop`）。外さないと、`/app`（遊ぶ頁）へ移っても
   * 生き残って奪い続ける。
   *
   * ⚠️②矢印キー・段階3 Delete キーは Ctrl+Z と**同じ場所**に足す（道を2つに割らない、
   * の指示）。⭐ ただし矢印・Delete は「字を打っている最中」は素通し ── `input`/
   * `textarea`/`select` に焦点があるときは、値の入力や `<select>` の選び直しを
   * 邪魔しない（Delete は欄の中で字を消す操作とぶつかる）。 */
  keys(owner) {
    if (this._keyBound) document.removeEventListener('keydown', this._keyBound)
    // ⭐② 矢印 → (dx, dy) の向き（-1/0/1）。「きざみ」ぶんの掛け算は C# 側（`EditPage.Nudge`）
    //    がする ── ここは向きだけを渡す（`Dragging` の dx/dy が設計px の実量なのとは違う）。
    const ARROWS = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] }
    const fn = (e) => {
      // ⚠️ 字を打っている最中（数値欄・字そのもの欄・寄せ/色の <select>・D-3 のその場
      //    入力欄）は矢印・Delete・**Ctrl+Z も**素通しする ── でないと、欄の中で
      //    カーソルを動かす／字を消す／打ち間違いをブラウザ既定の undo で戻すつもりが、
      //    節点を動かす／消す／盤の取り消しのほうへ暴発する。
      //    🔴 D-3 で実測: Ctrl+Z だけこの判定より**前**にあり、`typing` を素通ししていなかった
      //    （矢印・Delete は既に素通し済みだった ── ここだけ判定の位置を先頭へ揃える）。
      const tag = document.activeElement && document.activeElement.tagName
      const typing = tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT'
      // V2 のアウトラインは矢印・Space を自分で扱う。ここでも受けると、行を移るだけで
      // 選択中の節点までナッジされ、Alt+矢印では並べ替えと移動が同時に起きる。
      const inTree = !!(document.activeElement && document.activeElement.getAttribute
        && document.activeElement.getAttribute('role') === 'treeitem')
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z') {
        if (typing) return
        e.preventDefault()
        this._call(owner, e.shiftKey ? 'Redo' : 'Undo')
        return
      }
      // ⭐ 段階3: Delete キーで選択を消す（確認ダイアログは出さない ── undo が安全網）。
      if (e.key === 'Delete') {
        if (typing) return
        e.preventDefault()
        this._call(owner, 'DeleteSelected')
        return
      }
      // ⭐ Ctrl+D で複製（2026-08-29）。⚠️ `preventDefault` を忘れない ── ブラウザの
      //    「ブックマークに追加」が開く。⭐ 既にある `DuplicateSelected` を呼ぶだけ
      //    （道を2つに割らない ── 右の釦と同じ出所）。
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'd') {
        if (typing) return
        e.preventDefault()
        this._call(owner, 'DuplicateSelected')
        return
      }
      // ツリー自身が扱うキーはページスクロール等の既定動作だけ止める。
      // 伝播は止めず、Blazor 側の TreeKeyDown にはそのまま届ける。
      if (inTree && (e.key === ' ' || e.key === 'Spacebar' || e.key === 'ArrowUp'
        || e.key === 'ArrowDown' || e.key === 'Home' || e.key === 'End')) {
        e.preventDefault()
        return
      }
      // ⭐ **Space を押している間はパン**（2026-08-29・`listen` の `down` が見る）。
      //    ⚠️ 字を打っている最中と、釦に焦点があるときは素通しする ── 欄に空白を
      //    打てなくなる／Space で釦を押せなくなるのを避ける（矢印・Delete と同じ配慮）。
      //    ⚠️ `preventDefault` が要る ── 付けないと頁が1画面ぶん飛ぶ（ブラウザ既定）。
      if (e.key === ' ' || e.code === 'Space') {
        if (typing || tag === 'BUTTON' || inTree) return
        e.preventDefault()
        this._setSpace(true)
        return
      }
      // ⭐ Escape ── 掴んでいる最中なら取り消し、そうでなければ選択を解く（2026-08-29）。
      //    ⚠️ 掴んでいる最中の `cancel()` は「そこまでを1つの動作として確定する」
      //    （既存の作法・宙ぶらりんにしない）── ここで新しい戻し方を作らない。
      if (e.key === 'Escape') {
        if (typing) return
        e.preventDefault()
        if (this._cancelDrag && this._cancelDrag()) return
        this._call(owner, 'Picked', '', '')
        return
      }
      const dir = ARROWS[e.key]
      if (!dir) return
      if (typing || inTree) return
      e.preventDefault()
      // ⭐ Shift+矢印は粗いナッジ（きざみの4倍＝既定で4ドット）。⚠️ 倍率の掛け算は
      //    C# 側（`EditPage.Nudge`）が持つ ── ここは向きと「粗いか」だけを渡す
      //    （`Dragging` の dx/dy が実量なのとは違う、既存の役割分担のまま）。
      this._call(owner, 'Nudge', dir[0] * (e.shiftKey ? 4 : 1), dir[1] * (e.shiftKey ? 4 : 1))
    }
    this._keyBound = fn
    document.addEventListener('keydown', fn)

    // ⭐ Space を離したらパンをやめる（2026-08-29）。⚠️ 窓から焦点が外れたときも下ろす
    //    ── 押したまま alt+tab すると `keyup` が届かず、戻ったとき「押しっぱなし」の
    //    まま盤が選べなくなる（実際に踏みやすい）。
    if (this._keyUpBound) document.removeEventListener('keyup', this._keyUpBound)
    if (this._blurBound) window.removeEventListener('blur', this._blurBound)
    if (this._menuBound) document.removeEventListener('contextmenu', this._menuBound)
    this._keyUpBound = (e) => {
      if (e.key === ' ' || e.code === 'Space') this._setSpace(false)
    }
    this._blurBound = () => this._setSpace(false)
    // ⭐ 文脈メニューが開いたら Space を下ろす（2026-08-29 監査 C-2）。⚠️ メニューが
    //    開くと `keyup` が届かず、閉じたあとも「押しっぱなし」のままになる ──
    //    `blur` と同じ型の取りこぼし。
    this._menuBound = () => this._setSpace(false)
    document.addEventListener('keyup', this._keyUpBound)
    window.addEventListener('blur', this._blurBound)
    document.addEventListener('contextmenu', this._menuBound)

    // ⭐ 木の行を掴めるようにする最後の一押し（2026-08-29 監査 中3）。
    // ⚠️ Blazor の `@ondragstart` は C# 側へ知らせるだけで、`DragEventArgs` からは
    //    `dataTransfer` へ書けない。Firefox は `dragstart` で `setData` が呼ばれないと
    //    **掴み始めない**（Chromium と WebKit は寛容なので気づけない）── ここで生の
    //    催しを拾って1行だけ書いておく。⭐ 委譲なので行が増えても張り直さなくてよい。
    if (this._dragBound) document.removeEventListener('dragstart', this._dragBound, true)
    this._dragBound = (e) => {
      const row = e.target && e.target.closest && e.target.closest('.edrow')
      if (!row || !e.dataTransfer) return
      try { e.dataTransfer.setData('text/plain', row.textContent || 'row') } catch (_) { }
      e.dataTransfer.effectAllowed = 'move'
    }
    document.addEventListener('dragstart', this._dragBound, true)
  },

  /** ⭐ Space の上げ下げを1か所で持つ（2026-08-29）。⚠️ 見た目（掴む手の形）も
   * ここで一緒に切り替える ── 状態と見た目が2か所に散らないように。 */
  _setSpace(on) {
    if (this._space === on) return
    this._space = on
    const cap = document.getElementById('edcap')
    if (cap) cap.classList.toggle('edcap-pan', on)
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
    // ⭐ Pass B: 拡大パンのスクロール追従も外す（`keys` と同じ document 直付けの後始末）。
    if (this._scrollBound) { document.removeEventListener('scroll', this._scrollBound, true); this._scrollBound = null }
    // ⭐ 2026-08-29: Space パン（keyup/blur）と Ctrl+ホイールも同じ理由で外す
    //    ── document/window/`#edwrap` へ直に張ったものは、DOM が消えても自然には外れない。
    if (this._keyUpBound) { document.removeEventListener('keyup', this._keyUpBound); this._keyUpBound = null }
    if (this._blurBound) { window.removeEventListener('blur', this._blurBound); this._blurBound = null }
    if (this._menuBound) { document.removeEventListener('contextmenu', this._menuBound); this._menuBound = null }
    // ⭐ 木の行の掴み始め（`setData`）も document 直付けなので、同じく降ろす。
    if (this._dragBound) { document.removeEventListener('dragstart', this._dragBound, true); this._dragBound = null }
    if (this._wheelBound && this._wheelOn) {
      this._wheelOn.removeEventListener('wheel', this._wheelBound)
      this._wheelBound = null; this._wheelOn = null
    }
    // ⭐ パン（document の捕捉相）も同じ理由で外す（2026-08-29 監査 D-3 で足したもの）。
    if (this._panBound) {
      for (const [t, f] of this._panBound) document.removeEventListener(t, f, true)
      this._panBound = null
    }
    // ⭐ 2026-08-29 監査 C-5 の後片付け ──
    //    ⚠️ 器の見張り（ResizeObserver）を放すこと。放さないと外れた `#edwrap` を
    //    掴んだままになり、頁を離れても `fit()` が呼ばれ続ける。
    if (this._ro) { this._ro.disconnect(); this._ro = null }
    // ⚠️ 道具箱を掴んだまま頁を離れたときの document 直付けも降ろす（`_palUnhook` の註）。
    if (this._palUnhook) { this._palUnhook(); this._palUnhook = null }
    if (this._pBound && this._pRow) {
      for (const [t, f] of this._pBound) this._pRow.removeEventListener(t, f)
      this._pBound = null; this._pRow = null
    }
    // ⚠️ 外れた盤の節点を持ち続けない（次に開いたとき古い DOM を指したままになる）。
    if (this._taggedNodes) this._taggedNodes = null
    this._space = false
    // ⭐ 次に開いたときは、また盤を中央へ置く（`start()` の `_centered`）。
    //    ⚠️ 前回の倍率も忘れる ── 残すと、次に開いた最初の `fit()` が古い倍率を
    //    基準に位置を戻そうとして盤が飛ぶ（`_lastK` の註）。
    this._centered = false
    this._lastK = 0
    this._dirty = false
    if (this._beforeUnloadBound) {
      window.removeEventListener('beforeunload', this._beforeUnloadBound)
      this._beforeUnloadBound = null
    }
  },

  /** 選んでいる行の輪を描き直す（木から選んだとき・数を直して盤を組み直したときに使う
   * ── そのときは指の座標が無いので、盤の中から同じ行を持つ最初の1枚を探す）。
   * ⚠️ **部品を選んでいるときは同じ探し方に切り替える**（`_selector` が唯一の出所）。
   * @param {string} line 空文字なら輪を隠す */
  rering(line) {
    const node = line ? document.querySelector('#edstage ' + this._selector(line)) : null
    if (node) this._ringTo(node); else this._ringHide()
  },

  /** ⭐②④ 段階2 Pass A: 選択の輪の多重化（`EditPage.RingRefresh` から呼ばれる ──
   * 既存の全 `rering` 直接呼び出しの置き換え先）。
   *
   * ⚠️ **`#edring`（8つの掴みどころ）は選択がちょうど1つのときだけ**主の節点に出す
   * （<see cref="rering"/> に一本化 ── 2つ以上は曖昧な resize を避けて隠す）。
   * ⭐ 2つ以上のときは `#edsel` へ、選択中それぞれの輪郭だけの枠を作り直す（数は多くて
   * 数十 ── 毎回作り直してよい）。
   * @param {string} linesCsv 選択中の行番号（部品なら part-line）の csv。空文字なら無選択。
   * @param {string} primaryLine 主たる選択の行番号（空文字なら無し）。
   * @param {boolean} [mismatch] ⭐ E1-5: 主の節点が「枠と絵が合わない」節点か。選択が
   * ちょうど1つのときだけ効く（`#edring` の色を変える ── 新しい色は増やさず、
   * 不備の輪郭と同じ警告色）。 */
  reselect(linesCsv, primaryLine, mismatch) {
    // ⭐ Pass B: スクロール追従（`start` の scroll ハンドラ）が、指の座標なしで同じ選択を
    //    描き直せるよう、最後に描いた選択を覚えておく。
    // ⭐ E1-5: `_selMismatch` も一緒に覚える ── 覚えないと、スクロール追従が引数無しで
    //    呼び直したときに `mismatch` が undefined に落ち、輪の警告色がスクロールのたびに
    //    消えてしまう。
    this._selCsv = linesCsv; this._selPrimary = primaryLine; this._selMismatch = !!mismatch
    const lines = linesCsv ? linesCsv.split(',').filter(s => s !== '') : []

    // ⭐ E2: 名札「選んだものだけ」用のタグ付け直し（`.edtagged`）。⚠️ 前回タグ付けした
    //    節点は、盤が組み直っていることがあるので `classList` ではなく毎回集め直す
    //    （消えた節点への参照が残っても実害は無いが、集め直すほうが単純）。
    if (this._taggedNodes) for (const n of this._taggedNodes) n.classList.remove('edtagged')
    this._taggedNodes = []
    for (const line of lines) {
      const node = document.querySelector('#edstage ' + this._selector(line))
      if (node) { node.classList.add('edtagged'); this._taggedNodes.push(node) }
    }

    const sel = document.getElementById('edsel')
    if (sel) sel.innerHTML = ''

    const ring = document.getElementById('edring')
    if (ring) ring.classList.toggle('edring-warn', !!mismatch && lines.length === 1)

    if (lines.length <= 1) {
      // ⭐ 1つ（または0）── 今までどおり #edring に一本化。#edsel は空のまま。
      this.rering(lines.length === 1 ? lines[0] : '')
      return
    }

    // ⭐ 2つ以上 ── #edring は隠し、#edsel に各節点の輪郭を描く。
    this._ringHide()
    if (!sel) return
    const wrap = document.getElementById('edwrap')
    if (!wrap) return
    const wr = wrap.getBoundingClientRect()
    for (const line of lines) {
      const node = document.querySelector('#edstage ' + this._selector(line))
      if (!node) continue
      const nr = node.getBoundingClientRect()
      const box = document.createElement('div')
      box.className = 'edselbox' + (line === primaryLine ? ' edselbox-primary' : '')
      box.style.left = (nr.left - wr.left) + 'px'
      box.style.top = (nr.top - wr.top) + 'px'
      box.style.width = nr.width + 'px'
      box.style.height = nr.height + 'px'
      sel.appendChild(box)
    }
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

  /** ⭐ Pass B 木→盤: 木の行にマウスを乗せたとき、対応する盤の節点を薄い枠で強調する。
   * ⚠️ **選択の輪（`#edring`/`#edsel`）とは別レイヤ・別関数**（`EditPage.TreeHoverOn`
   * からだけ呼ぶ）── 混ぜない、の指示どおり。`_ringTo` と同じ置き方（盤の外の層・
   * 実寸 screen px・`getBoundingClientRect` の差分）なので、拡大＋スクロールでも
   * 同じ理由でそのまま正しく追従する。
   * @param {string|number} line 対応する行（部品なら part-line）。 */
  hoverOn(line) {
    this._hoverLine = line   // ⭐ Pass B: スクロール追従で描き直せるよう覚える（reselect と同じ）
    const node = document.querySelector('#edstage ' + this._selector(line))
    const hover = document.getElementById('edhover')
    if (!hover) return
    if (!node) { hover.style.display = 'none'; return }
    const wrap = document.getElementById('edwrap')
    if (!wrap) return
    const wr = wrap.getBoundingClientRect()
    const nr = node.getBoundingClientRect()
    hover.style.left = (nr.left - wr.left) + 'px'
    hover.style.top = (nr.top - wr.top) + 'px'
    hover.style.width = nr.width + 'px'
    hover.style.height = nr.height + 'px'
    hover.style.display = 'block'
  },

  hoverOff() {
    this._hoverLine = null   // ⭐ Pass B: スクロール追従の対象から外す
    const hover = document.getElementById('edhover')
    if (hover) hover.style.display = 'none'
  },

  /** ⭐ D-3: その場の文字入力欄（`#edtextedit`）を、対象節点の実位置へ合わせる。
   * ⚠️ **輪と同じ作法**（`_ringTo` と同じ ── 盤の外の層・実寸 screen px・
   * `getBoundingClientRect` の差分）。⭐ 最初の1回だけ焦点を移して全選択する
   * （`_editFocusLine` で二重に奪わない ── 毎描画のあとに呼ばれても安全にするため）。
   * @param {string} line 対象の行（部品なら part-line）。 */
  editAt(line) {
    const box = document.getElementById('edtextedit')
    if (!box) return
    const node = document.querySelector('#edstage ' + this._selector(line))
    const wrap = document.getElementById('edwrap')
    if (!node || !wrap) { box.style.display = 'none'; return }
    const wr = wrap.getBoundingClientRect()
    const nr = node.getBoundingClientRect()
    box.style.left = (nr.left - wr.left) + 'px'
    box.style.top = (nr.top - wr.top) + 'px'
    box.style.width = nr.width + 'px'
    box.style.height = nr.height + 'px'
    box.style.display = 'block'
    if (this._editFocusLine !== line) {
      this._editFocusLine = line
      const input = document.getElementById('edtexteditinput')
      if (input) { input.focus(); input.select() }
    }
  },

  /** 入力欄の「いま打っている値」を読む（`EditPage.CommitTextEdit` から）。
   * ⚠️ 打っている最中は Blazor の双方向バインディングを使わない（キー入力のたびに
   * 再描画を起こさないため）── 確定するその1回だけ、実 DOM の値を読みに来る。 */
  editValue() {
    const input = document.getElementById('edtexteditinput')
    return input ? input.value : ''
  },

  /** 入力欄を閉じる（確定・取り消しの両方から呼ぶ）。 */
  editEnd() {
    this._editFocusLine = null
    const box = document.getElementById('edtextedit')
    if (box) box.style.display = 'none'
  },

  /** ⭐ Pass B 変更点の明滅: 「確定した1動作」の直後に、その節点を一瞬だけ光らせる。
   * ⚠️ ドラッグ中の毎フレームからは呼ばない（`EditPage` 側 ── `Apply`/`Nudge`/
   * `ApplyHistory`/揃える等間隔/`DragEnd`/`ResizeEnd` の後だけ）。
   * ⭐ 塗らない（`edflash` は輪郭のパルスだけ・CSS アニメ）。連続で呼ばれても安全なように、
   * 一度クラスを外してから reflow を挟んで付け直す（同じクラスを連続して付けても
   * アニメが再始動しないブラウザ既定の挙動への対策）。
   * @param {string|number} line 光らせる行（部品なら part-line）。 */
  flash(line) {
    this.flashLines(String(line))
  },

  /** ⭐ まとめ移動・揃える等間隔用の複数行版。@param {string} csv 行番号の csv。 */
  flashLines(csv) {
    const lines = csv ? String(csv).split(',').filter(s => s !== '') : []
    for (const line of lines) {
      const node = document.querySelector('#edstage ' + this._selector(line))
      if (!node) continue
      node.classList.remove('edflash')
      void node.offsetWidth   // ⭐ reflow を挟んで、除去を確定させる（再始動できるように）
      node.classList.add('edflash')
    }
  },

  /** ⭐④ ドラッグ枠（ラバーバンド）を、その瞬間だけ見せる（`#edwrap` 基準の実寸 ──
   * `_ringTo` と同じ置き方）。@param {number} x1,x2,y1,y2 実画面座標（順不同でよい）。 */
  _bandDraw(x1, x2, y1, y2) {
    const band = document.getElementById('edband')
    const wrap = document.getElementById('edwrap')
    if (!band || !wrap) return
    const wr = wrap.getBoundingClientRect()
    const left = Math.min(x1, x2), top = Math.min(y1, y2)
    band.style.left = (left - wr.left) + 'px'
    band.style.top = (top - wr.top) + 'px'
    band.style.width = Math.abs(x2 - x1) + 'px'
    band.style.height = Math.abs(y2 - y1) + 'px'
    // ⭐ E2: 押しどころの層では「作る」プレビュー（Tiled `createobjecttool.cpp` と同じ
    //    順序 ── ドラッグ中は半透明のプレビュー）。⚠️ 選ぶ（ラバーバンド）とは
    //    見た目を変える（`.edband-create`）── 同じ枠でも「作る」と「選ぶ」を区別する。
    band.classList.toggle('edband-create', this._layer === 'tap')
    band.style.display = 'block'
  },

  _bandHide() {
    const band = document.getElementById('edband')
    if (band) band.style.display = 'none'
  },

  /** ⭐④ ドラッグ枠と交差する節点を集める（バンドを離したとき）。⚠️ 候補は「通常文書
   * なら `[data-line]`、部品文書なら `[data-part="<_of>"]`」── `nodeAt` と同じ絞り方
   * （別の部品へ迷い込まない）。行番号が数（≥0）のものだけ・重複は行番号で除外。
   * @param {{left:number, top:number, right:number, bottom:number}} rect 実画面座標の矩形
   * @returns {number[]} 交差した行番号の一覧（重複無し）。 */
  _bandCollect(rect) {
    const stage = document.getElementById('edstage')
    if (!stage) return []
    const nodes = stage.querySelectorAll(this._partId ? '[data-part="' + this._partId + '"]' : '[data-line]')
    const seen = new Set()
    const out = []
    nodes.forEach(el => {
      // ⭐ E2: 「触れなくする」はラバーバンドにも掛かる ── 層を選んでいるときは、
      //    その層でない節点をバンドの交差判定から外す（`nodeAt`/`pickAt` と同じ判定）。
      if (!this._layerOk(el)) return
      const lineStr = this._lineOf(el)
      if (lineStr === '') return
      const n = Number(lineStr)
      if (!Number.isFinite(n) || n < 0 || seen.has(n)) return
      const r = el.getBoundingClientRect()
      const overlap = !(r.right < rect.left || rect.right < r.left || r.bottom < rect.top || rect.bottom < r.top)
      if (!overlap) return
      seen.add(n)
      out.push(n)
    })
    return out
  },

  /** ⭐ **掴んでいる最中の読み出し**（`#edstat`・2026-08-29）。
   *
   * ⚠️ D-4 の「左/上/幅/高は既定で畳む」（微調整のときだけ開く）は**そのまま**。
   * 🔴 畳んだせいで「動かしている最中は座標がどこにも出ない」という欠落だけを埋める
   * ── 吸い付きの線は出るのに、何px動いたかは離して欄を開くまで分からなかった。
   * ⭐ 字は C# が組む（単位の切り替えを含む ── 出所は `EditPage.StatText` 1つ）。
   * `guide` と同じ拍で呼ばれる、同じ「一時的な道具」。
   * @param {string|null} text 空・null なら消す。 */
  stat(text) {
    const el = document.getElementById('edstat')
    if (!el) return
    if (!text) { el.style.display = 'none'; el.textContent = ''; return }
    el.textContent = text
    el.style.display = 'block'
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
