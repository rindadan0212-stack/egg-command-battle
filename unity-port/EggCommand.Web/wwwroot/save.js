// 保存の置き場（ブラウザ側）。
//
// ⚠️ **Unity には無かった消え方が2つある**（計画 §6）:
//   1. 同じゲームを**2つのタブ**で開ける ── 両方が書いて、後勝ちで片方が消える
//   2. **閉じる瞬間に書き込みが間に合わない** ── 非同期の置き場は完了を保証しない
//
// ⭐ だからここは `localStorage`（**同期**）を主にする。
// ⚠️ IndexedDB の世代リングは**まだ作っていない**。実測で保存は 11KB、10世代でも 110KB
//    なので容量では要らず、`persist()` は生成元ごと効くので守りも効く。
//    ⭐ 要るのは「別の消え方をする場所」だが、それはブラウザの中には無い（＝書き出し）。

const KEY = 'egg:save'
const PAST = 'egg:save:past'      // 世代（新しい順）
const KEEP = [0, 300, 3600, 86400, 604800]   // 直近 / 5分 / 1時間 / 1日 / 1週間（秒）

window.eggSave = {
  /** ⭐ 書き手を1つに限る。⚠️ 取れなければ**読み取り専用**で開く。 */
  async claim() {
    if (!navigator.locks) return true        // ⚠️ 古いブラウザ ── 譲る相手も居ない
    // ⚠️ 🔴 **鍵を受け取ったところで答える。**`request` は待たずに返るので、
    //    `setTimeout(0)` で見に行くと**まだ呼ばれていない**ことがあり、
    //    常に「取れなかった」になる（実測 2026-08-22: 保存が1字も書かれなかった）。
    return await new Promise((resolve) => {
      navigator.locks.request('egg-save', { mode: 'exclusive', ifAvailable: true }, (lock) => {
        resolve(!!lock)
        // ⚠️ 解放しない（このタブが生きている間ずっと持つ）
        return lock ? new Promise(() => {}) : undefined
      }).catch(() => resolve(false))
    })
  },

  /** ⭐ 消されにくくしてもらう。⚠️ 断られても遊びは続く（保証ではない）。 */
  async persist() {
    try { return !!(navigator.storage && await navigator.storage.persist()) }
    catch { return false }
  },

  read() {
    try { return localStorage.getItem(KEY) } catch { return null }
  },

  /** @param {string} json @param {number} nowUnix */
  write(json, nowUnix) {
    try {
      const was = localStorage.getItem(KEY)
      // ⭐ 変わっていなければ触らない。⚠️ 書き込みは落ちる窓を開ける操作なので、
      //    必要のない書き込みは「安全な操作」ではない（Unity 版と同じ決めごと）。
      if (was === json) return 'same'
      if (was !== null) this._keep(was, nowUnix)
      localStorage.setItem(KEY, json)
      localStorage.setItem(KEY + ':at', String(nowUnix))
      return 'wrote'
    } catch (e) {
      return 'failed:' + (e && e.name)
    }
  },

  /** 世代を間引いて残す。⚠️ 「直近だけ」だと、少しずつ壊れていく形に耐えられない。 */
  _keep(json, nowUnix) {
    let past = []
    try { past = JSON.parse(localStorage.getItem(PAST) || '[]') } catch { past = [] }
    past.unshift({ at: Number(localStorage.getItem(KEY + ':at') || nowUnix), json })

    // ⭐ 目標の古さごとに1本だけ残す（近いものを選ぶ）
    const pick = []
    for (const age of KEEP) {
      let best = null
      for (const one of past) {
        const d = Math.abs((nowUnix - one.at) - age)
        if (!best || d < best.d) best = { d, one }
      }
      if (best && !pick.includes(best.one)) pick.push(best.one)
    }
    try { localStorage.setItem(PAST, JSON.stringify(pick)) } catch { /* 満杯なら諦める */ }
  },

  /** 残っている世代（古さの秒だけ返す）。⭐ 画面に出して確かめるため。 */
  past(nowUnix) {
    try {
      const past = JSON.parse(localStorage.getItem(PAST) || '[]')
      return past.map((o) => nowUnix - o.at)
    } catch { return [] }
  },

  /** ⭐ **本命の層**: ブラウザの外へ出す。⚠️ ここだけは別の消え方をする。 */
  download(name, json) {
    const url = URL.createObjectURL(new Blob([json], { type: 'application/json' }))
    const a = document.createElement('a')
    a.href = url
    a.download = name
    a.click()
    setTimeout(() => URL.revokeObjectURL(url), 1000)
  },

  /** いまの保存の大きさ（字数）。⭐ 画面に出して「在ること」を見えるようにする。 */
  size() {
    try { return (localStorage.getItem(KEY) || '').length } catch { return 0 }
  },

  /** ⭐ **外から1つ読む。**⚠️ 読むだけ ── 中身を確かめるのは C# 側。
   *
   * ⚠️ 🔴 **やめられたときも必ず答える。**⭐ 答えないと C# の待ちが永久に返らず、
   *   画面が黙って固まる（`change` は選ばなければ起きない）。
   *   ⚠️ `cancel` を聞かないブラウザ向けに、窓が戻ってきた拍でも見に行く。
   *
   * @returns {Promise<string|null>} 中身。やめたら null */
  async pick() {
    return await new Promise((resolve) => {
      let done = false
      const end = (v) => { if (!done) { done = true; clean(); resolve(v) } }
      const el = document.createElement('input')
      el.type = 'file'
      el.accept = '.json,application/json'
      el.style.display = 'none'
      const back = () => setTimeout(() => { if (!el.files || !el.files.length) end(null) }, 800)
      const clean = () => {
        window.removeEventListener('focus', back)
        el.remove()
      }
      el.addEventListener('cancel', () => end(null))
      el.addEventListener('change', () => {
        const file = el.files && el.files[0]
        if (!file) return end(null)
        const reader = new FileReader()
        reader.onload = () => end(String(reader.result))
        reader.onerror = () => end(null)
        reader.readAsText(file)
      })
      window.addEventListener('focus', back)
      document.body.appendChild(el)
      el.click()
    })
  },

  erase() {
    try { localStorage.removeItem(KEY); localStorage.removeItem(PAST) } catch { /* 無ければ何もしない */ }
  },
}
