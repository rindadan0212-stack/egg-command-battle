// 押しどころを拾って C# へ渡す。
//
// ⚠️ 画面は字を組み立てて流し込んでいるので、Blazor の `@onclick` は付けられない。
// ⭐ だから `#stage` で1つだけ拾って、押された部品に書いてある名前と番号を読む。
//
// ⚠️ **押した部品そのものとは限らない** ── 札の上の字を押すこともある。
//    ⭐ `closest` で「押しどころだと名乗っている親」まで遡る。
//
// ⚠️ 倍率合わせ（`fit`）は index.html が持っている。ここには書かない。

window.eggTap = {
  /** 指を離すまでの長さ（ms）。⭐ Unity 版 `LongPress` と同じ数。 */
  HOLD: 500,

  /** @param {object} owner .NET 側の受け口（`DotNetObjectReference`） */
  listen(owner) {
    for (const [type, fn] of this._bound || []) document.removeEventListener(type, fn, true)

    // ⚠️ **長押しは押しどころとは別の道**（`hold=`）。
    // ⭐ 短く触っても開かない ── 技の札は押しどころではないので、
    //   触っただけで開くと一覧を選ぶ指が誤爆する。
    let timer = null, held = false, from = null
    const drop = () => { if (timer) clearTimeout(timer); timer = null; from = null }

    const down = (e) => {
      held = false
      const el = e.target instanceof Element ? e.target.closest('[data-hold]') : null
      if (!el) return
      from = { x: e.clientX, y: e.clientY }
      timer = setTimeout(() => {
        timer = null
        held = true
        owner.invokeMethodAsync('Held', el.dataset.hold, el.dataset.at || '')
      }, this.HOLD)
    }
    // ⚠️ **指がずれたら長押しではない**（巻物を送っているだけのことがある）
    const move = (e) => {
      if (!from) return
      if (Math.abs(e.clientX - from.x) > 12 || Math.abs(e.clientY - from.y) > 12) drop()
    }
    const up = (e) => {
      drop()
      // ⭐ 長押しが成立したら、離した拍で押しどころを動かさない
      if (held) { held = false; e.preventDefault(); return }
      const el = e.target instanceof Element ? e.target.closest('[data-tap]') : null
      if (!el || el.disabled) return
      e.preventDefault()
      owner.invokeMethodAsync('Tapped', el.dataset.tap, el.dataset.at || '')
    }

    this._bound = [['pointerdown', down], ['pointermove', move],
      ['pointerup', up], ['pointercancel', () => drop()]]
    for (const [type, fn] of this._bound) document.addEventListener(type, fn, true)
  },

  /** 頁を離れるときの後片付け（`AppPage.Dispose`）。⚠️ `listen()` は document の**捕捉段階**
   * に直付けする（冒頭コメント参照）ので、DOM がこの頁のものでなくなっても listener
   * 自体は自然には外れない ── 外さないと、捨てられた `DotNetObjectReference` へ向けて
   * 古い購読が `invokeMethodAsync` を呼び続け、押すたびに例外（uncaught promise
   * rejection）になる（`edit.js` の `stop()` と同じ理由・同じ流儀に揃えた）。 */
  stop() {
    for (const [type, fn] of this._bound || []) document.removeEventListener(type, fn, true)
    this._bound = null
  },

  /** 帯だけ差し替える。
   *
   * ⚠️ 🔴 **画面を組み直さないために在る。**
   * ⭐ 毎秒10回組み直すと、押しどころが作り直されて**触れなくなる**
   *   （Unity 版の `UnitStand.Retick` が同じ理由で分けてある）。
   *
   * @param {Record<string, number>} bars id → 0〜1 */
  bars(bars) {
    for (const id in bars) {
      const el = document.getElementById(id)
      if (!el) continue
      // ⚠️ 🔴 **伸ばすのは「伸びた分」であって、器ではない**（2026-08-28 に実測して判明）。
      //    ⭐ 帯は2枚（`LayoutDom` の `bar`）── 名前（id）を持つのは**器**のほうで、
      //    伸びる子（`.bar-fill`）は名無し。ここで器へ幅を書いていたので、
      //    **帯の地（レール）そのものが伸び縮みして**いた（実測: 器 100% ＝ 340px
      //    なのに、中の伸びた分は前回の組み直しのまま 86% で止まっていた）。
      //    ⚠️ 遠目には「何かが伸びている」ので気づきにくい ── 数で見ないと分からない類。
      const fill = el.classList.contains('bar') ? el.querySelector('.bar-fill') : el
      if (fill) fill.style.width = (bars[id] * 100) + '%'
    }
  },

  /** 字だけ差し替える（`Clocks.Words` の1秒ごとの差し替え）。
   *
   * ⚠️ 🔴 **画面を組み直さないために在る**（`bars` と同じ理由 ── 毎秒組み直すと
   *   押しどころが作り直されて触れなくなる）。
   * ⚠️ 名前は小文字で来る（Blazor が camelCase に直す）── `At` ではなく `at`。
   *
   * @param {Array<{at: string, text: string, tint: string|null}>} words */
  words(words) {
    for (const w of words) {
      const el = document.getElementById(w.at)
      if (!el) continue
      if (el.textContent !== w.text) el.textContent = w.text
      if (w.tint) el.style.color = w.tint
    }
  },

  /** 放置の帯（相手・HP・EXP・卵）を、組み直さずに毎秒差し替える（`Idle.Peek` の出）。
   *
   * ⚠️ 🔴 **`bars`/`words` と同じ理由で在る**（毎秒 `Draw()` すると押しどころが
   *   作り直されて触れなくなる ── 2026-08-28・作者の報告で、5拍に1回の全面組み直しを
   *   丸ごとやめた。放置の帯の要素そのものは `Idle.Draw` が**常に**作るので、
   *   ここは級（class）の付け外しと、幅・字の書き換えだけで足りる）。
   * ⚠️ 名前は小文字で来る（Blazor が camelCase に直す）── `FoeArt` ではなく `foeArt`。
   *
   * @param {{foeArt: string|null, foeLeft: number, foeKey: number, eggs: number, exp: string}} view */
  idle(view) {
    const foe = document.getElementById('foe')
    if (foe) {
      if (view.foeArt) {
        foe.classList.remove('idle-hidden')
        // ⚠️ id を持つのは `walker.txt` が描く「絵の器」（`artf#foe`）であって、
        //   中の `<img>` 自身は id を持たない（`LayoutDom.Dots` は器にだけ id を振る）
        //   ── だから一段くぐって探す。
        const body = document.getElementById('artf#foe')
        const img = body && body.querySelector('img')
        if (img && img.getAttribute('src') !== view.foeArt) img.setAttribute('src', view.foeArt)
        // ⭐ **相手が入れ替わったときだけ**、飛び込みをやり直す。
        //   ⚠️ 「前に見えていたときの番号」とだけ比べる ── 隠れている間は更新しない。
        //   でないと、倒した瞬間（＝隠れる瞬間）に番号が変わり、次に見えたときには
        //   もう「同じ番号」になっていて、本来やり直したい飛び込みが起きない。
        //   ⚠️ 初回（`_foeKey` が undefined）はやり直さない ── ページを開いた瞬間、
        //   もう戦っている最中の相手にまで飛び込みを再生してしまう。
        if (this._foeKey !== undefined && view.foeKey !== this._foeKey) {
          // ⚠️ 同じ級を付け直しても animation は再生されない（`fx.js` の `_nudge` と同じ罠）。
          //   ⭐ 一度測らせて読み直す。
          foe.classList.remove('idle-come')
          void foe.offsetWidth
          foe.classList.add('idle-come')
        }
        this._foeKey = view.foeKey
      } else {
        // ⭐ これが「倒れた」の見え方。⚠️ 消さない ── 次に出るときも同じ枠を使い回す
        //   （`_foeKey` もここでは更新しない。上のコメント参照）。
        foe.classList.add('idle-hidden')
      }
    }
    for (const id of ['hptrack', 'hpfill']) {
      const el = document.getElementById(id)
      if (el) el.classList.toggle('idle-hidden', !view.foeArt)
    }
    const hpfill = document.getElementById('hpfill')
    // ⚠️ 帯の地（`hptrack`）と同じ 280px を最大に、実数のまま px で伸ばす
    //   （`Idle.Draw` が最初に置く幅の作り方と同じ ── ％ではない）。
    if (hpfill) hpfill.style.width = (view.foeLeft * 280) + 'px'

    const exp = document.getElementById('count')
    if (exp && exp.textContent !== view.exp) exp.textContent = view.exp

    if (view.eggs > 0) this._eggHop(view.eggs)
  },

  /** 卵が飛び込む。⭐ **相手と同じ弧**（`idle-come`）で来る（作者の指示 ──
   * 「敵出現時の演出を、卵も」）。⚠️ `idle-come` は着地して止まったままなので、
   * ここは自分で**留めてから片付ける**（`fx.js` の演出と同じ「作って、自分で消す」流儀）。
   *
   * ⚠️ **`#fx` の中へ差す**（`fx.js` と同じ置き場・同じ理由 ── `AppPage.razor` の
   *   `#fx` の註「Blazor は自分で描いた節点しか触らないので、ここへ差したものは
   *   組み直しで消えない」）。`#idle` は放置の帯そのものが `Draw()` で丸ごと
   *   組み直されうる（タップ・画面遷移のたび）ので、そちらへ差すと消える恐れがある。
   * ⚠️ `#fx` は `#stage` 直下（座標系 0〜1080×0〜1920）── 放置の帯（`#idle`、
   *   0〜1080×0〜472）とは基準が違うので、帯の上端ぶん（`#app-body` の 132px ＋
   *   `#idle` の 88px ＝ 220px）を足して合わせる。⚠️ 「画面の外」は `#stage` 自身の
   *   `overflow:hidden`（1080px 幅）がちょうど隠す ── 相手の飛び込みと同じ切れ方になる。
   * @param {number} many */
  _eggHop(many) {
    const yard = document.getElementById('fx')
    if (!yard) return
    for (let i = 0; i < many; i++) {
      const el = document.createElement('img')
      el.src = 'paint/nest-egg.png'
      el.alt = ''
      el.className = 'n paint idle-come'
      // ⭐ 複数まとめて出るとき（久しぶりに開いた清算など）は少しずつ間を空ける
      //   ── 重なって出ると1個に見える。
      const wait = i * 0.14
      el.style.cssText = `left:780px;top:480px;width:100px;height:125px;`
        + (wait > 0 ? `animation-delay:${wait}s` : '')
      yard.appendChild(el)
      // ⚠️ `animationend` を待たない ── `idle-come` は着地して止まったままなので発火しない。
      //   ⭐ 飛び込み（.74秒）＋ 留める間（.6秒）で自分から消す。
      setTimeout(() => el.remove(), (740 + 600) + wait * 1000)
    }
  },
}
