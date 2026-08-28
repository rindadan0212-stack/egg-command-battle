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
}
