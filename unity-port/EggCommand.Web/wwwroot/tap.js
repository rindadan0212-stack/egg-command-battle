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
  /** @param {object} owner .NET 側の受け口（`DotNetObjectReference`） */
  listen(owner) {
    if (this._on) document.removeEventListener('pointerup', this._on, true)
    this._on = (e) => {
      const el = e.target instanceof Element ? e.target.closest('[data-tap]') : null
      if (!el || el.disabled) return
      e.preventDefault()
      owner.invokeMethodAsync('Tapped', el.dataset.tap, el.dataset.at || '')
    }
    document.addEventListener('pointerup', this._on, true)
  },
}
