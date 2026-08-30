// 盤に出す演出。⭐ **「何が起きたか」を字で説明しないための道具。**
//
// ⚠️ **Blazor に描かせない。**⭐ 画面を組み直すたびに演出が最初からやり直しになり、
//    しかも組み直しは押しどころを壊す（`罠と教訓.md`）。
//    だからここが直接 DOM へ差して、終わったら自分で片付ける。
//
// ⚠️ **座標を C# から受け取らない。**⭐ 出す先は体の名前（`a0` `f2`）で来るので、
//    実際の場所はその体の枠から測る ── `Stands.Lay` の式と2か所にしない。
//
// ⚠️ 体は `unit.txt` を `#a0` の名前で描いたもの。⭐ 絵の枠（`art#a0`）を基準にする
//    （器ではなく絵 ── 器は帯や印のぶん広く、中心が足元に寄る）。

// ⚠️ 🔴 **名前は小文字で来る。**⭐ C# 側は `Spark(At, Kind, …)` だが、
//    Blazor は JS へ渡すときに camelCase へ直す（`at` `kind` …）。
//    ⚠️ 大文字で読むと**全部 undefined**になり、静かに何も出ない
//    （実測 2026-08-23: `play` は5回呼ばれているのに盤は空だった）。

const LIVE = 'egg-fx'

window.eggFx = {
  /** 同じ体に重ねないための段（`Core.Beats.StackStep`）。 */
  STACK: 46,

  /** ⭐ **1つ出すごとに置く間**は C# が決めて `wait`（秒）で渡してくる（`Core.Beats.PopStep`）。
   * ⚠️ ここで数えない ── 数えると、待たせる秒（`Deeds.Beat` が伸ばす `Wait`）と出所が2つになる。
   * @param {Array<{at:string,kind:string,text:string,tint:string,size:number,up:number,wait:number}>} sparks */
  play(sparks) {
    if (!sparks || !sparks.length) return
    const stage = document.getElementById('stage')
    const yard = document.getElementById('fx') || stage
    if (!stage) return
    const box = stage.getBoundingClientRect()
    // ⭐ 盤は倍率を掛けて置いてあるので、実寸へ戻して測る。
    // ⚠️ 非表示へ切り替わる拍は幅が0になり得るので、0で割らない。
    const scale = stage.offsetWidth > 0 && Number.isFinite(box.width / stage.offsetWidth)
      ? box.width / stage.offsetWidth : 1

    for (const one of sparks) {
      // ⭐ Down は消えかけの体と墓が重なる専用の拍。墓枠を基準に砂煙を
      //    出してから見せるため、通常の `art` を探すより先に分ける。
      const wait = one.wait > 0 ? one.wait : 0
      if (one.kind === 'down') { this._down(one.at, wait, stage, yard, box, scale); continue }
      // ⚠️ 絵が居なければ**出さない**（倒れた体・別の画面へ移った後）
      const art = document.getElementById('art#' + one.at)
        || document.getElementById('artf#' + one.at)
      if (!art) continue
      const r = art.getBoundingClientRect()
      const x = (r.left + r.width / 2 - box.left) / scale
      const y = (r.top + r.height / 2 - box.top) / scale
      const half = r.width / scale / 2

      // ⚠️ 🔴 **名前は小文字**（camelCase）── `Wait` ではなく `wait`
      if (one.kind === 'step' || one.kind === 'stepf') { this._nudge(art, 'fx-step', wait); continue }
      if (one.kind === 'shock') { this._nudge(art, 'fx-shock', wait); continue }

      const el = document.createElement('div')
      el.className = LIVE + ' fx-' + one.kind
      if (one.kind === 'say' || one.kind === 'shout') {
        el.textContent = one.text
        // ⭐ 頭の上。⚠️ 味方は縦に積んであるので、真上だと1つ上の帯に乗る
        el.style.cssText = `left:${x - 200}px;top:${y - half - 40 - one.up * this.STACK}px;`
          + `width:400px;font-size:${one.size || 40}px;color:${one.tint || 'var(--ink)'}`
      } else {
        // 輪と光 ── ⭐ 体の中心から広がる
        el.style.cssText = `left:${x - 60}px;top:${y - 60}px;width:120px;height:120px;`
          + `--fx-tint:${one.tint || 'var(--ink)'}`
      }
      // ⭐ 順番に出す。⚠️ 出るまでは**見えていてはいけない**
      //    ── `stage.css` が `animation-fill-mode: both` を持つので、
      //    待っている間は 0% の姿（透明）のまま留まる。
      // ⚠️ 🔴 **`cssText` より後に書く。**⭐ 上の2つの枝はどちらも `el.style.cssText = …` で
      //    **inline style を丸ごと差し替える**ので、先に書いた `animation-delay` は消える
      //    （2026-08-28 に実測。C# は正しい秒を送っていたのに、DOM に一つも残っていなかった
      //    ── 送った値だけを見て「効いている」と判じてはいけない、の実例）。
      if (wait > 0) el.style.animationDelay = wait + 's'
      yard.appendChild(el)
      el.addEventListener('animationend', () => el.remove(), { once: true })
      // ⚠️ **落ちても必ず消す。**⭐ `animationend` は画面が隠れていると来ないことがある
      //    ⚠️ 待ち（`wait`）のぶんを足さないと、遅れて出るものを**出る前に消す**
      setTimeout(() => el.remove(), 2000 + wait * 1000)
    }
  },

  /** 体をひと突き。⭐ 踏み込み（横）と、当たった跳ね（下）。
   * ⚠️ 向き（敵は裏返し）は CSS が `.foe` を見て選ぶ ── ここでは分けない。
   * ⭐ `wait` は「順番に出す」ぶんの遅れ（秒）── 数字と同じ拍で跳ねさせる。
   * ⚠️ **級（class）は待てない**ので、付けるのを遅らせる（`animation-delay` だと
   * 待っている間も級が付いていて、次の跳ねが「同じ級の付け直し」になって不発になる）。 */
  _nudge(art, name, wait) {
    // ⚠️ 待っている間に戦いが終わることがある ── ⭐ 居なくなった絵は突かない
    if (wait > 0) { setTimeout(() => { if (art.isConnected) this._nudge(art, name, 0) }, wait * 1000); return }
    art.classList.remove('fx-step', 'fx-shock')
    // ⚠️ 同じ級を付け直しても animation は再生されない。⭐ 一度測らせて読み直す
    void art.offsetWidth
    art.classList.add(name)
    setTimeout(() => art.classList.remove(name), 900)
  },

  /** 倒れた瞬間の灰色ピクセル砂煙。墓は Down の拍で C# が伏せてあり、ここだけが現す。
   * ⚠️ reduced-motion は砂煙も待ちも省いて直ちに墓を見せる。 */
  _down(at, wait, stage, yard, box, scale) {
    const grave = document.getElementById('gravea#' + at)
      || document.getElementById('gravef#' + at)
    if (!grave) return
    if (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      grave.style.opacity = '1'
      return
    }
    // ⚠️ 同じ手の複数演出を C# が順番に出す。砂煙も同じ拍まで待つ。
    if (wait > 0) {
      setTimeout(() => { if (grave.isConnected) this._down(at, 0, stage, yard, box, scale) }, wait * 1000)
      return
    }

    const r = grave.getBoundingClientRect()
    const x = (r.left + r.width / 2 - box.left) / scale
    const y = (r.top + r.height * .72 - box.top) / scale
    const dust = document.createElement('div')
    dust.className = LIVE + ' fx-down'
    const dustDuration = this._time(stage, '--death-dust-duration', 420)
    // ⭐ ランダムにしない ── 同じ Down は同じ粒の広がり方になり、再現と見比べができる。
    const bits = [[-68, 4, 0], [-44, -8, 35], [-24, 8, 70], [-4, -14, 15],
      [18, 3, 55], [42, -7, 90], [66, 7, 25], [-56, 18, 100], [30, 17, 120]]
    for (const [left, top, delay] of bits) {
      const bit = document.createElement('i')
      bit.className = 'fx-dust'
      // ⚠️ delay は全体420msの「中」でずらす。各粒が420msを越えないよう
      //    その分だけ再生時間を短くする（delay+duration=dustDuration）。
      const duration = Math.max(1, dustDuration - delay)
      bit.style.cssText = `left:${x + left}px;top:${y + top}px;animation-delay:${delay}ms;animation-duration:${duration}ms;`
      dust.appendChild(bit)
    }
    yard.appendChild(dust)
    // ⭐ 絵は既に無く、砂煙を一呼吸見せてから墓だけ置く。
    const graveDelay = this._time(stage, '--death-grave-delay', 260)
    setTimeout(() => { if (grave.isConnected) grave.style.opacity = '1' }, graveDelay)
    setTimeout(() => dust.remove(), dustDuration + 20)
  },

  /** CSSの共通時計をmsへ。戦闘だけ別の秒を持つと放置と見え方がずれる。 */
  _time(stage, name, fallback) {
    const value = getComputedStyle(stage).getPropertyValue(name).trim()
    const number = Number.parseFloat(value)
    if (!Number.isFinite(number) || number < 0) return fallback
    return value.endsWith('ms') ? number : number * 1000
  },

  /** 出しかけを片付ける。⚠️ 戦いが終わったのに数字が残っていると、次の戦いに混ざる。 */
  clear() {
    for (const el of document.querySelectorAll('.' + LIVE)) el.remove()
    for (const el of document.querySelectorAll('.fx-step,.fx-stepf,.fx-shock'))
      el.classList.remove('fx-step', 'fx-stepf', 'fx-shock')
  },
}
