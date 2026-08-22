#!/usr/bin/env node
/** ⚠️ **検査そのものを壊して、効きを確かめる。**
 *
 *  ⭐ このプロジェクトの決まり: 「不備 0 件」は、見つけられないだけかもしれない。
 *  道具は**わざと壊して**、ちゃんと落ちることを確かめてから信じる。
 *
 *  ⚠️ 検査の本体は `audit.mjs` を読む（写さない）。
 *
 *  使い方: node scripts/inspect-selftest.mjs [URL]
 */

import { chromium } from 'playwright'
import { audit } from './audit.mjs'

const URL = process.argv[2] || 'http://localhost:5817'

/** わざと壊す手と、そのとき出るはずの言葉。 */
const TRIALS = [
  {
    name: '字を重ねる',
    want: '字の重なり',
    // ⚠️ **兄弟どうしを重ねる。**別々の親だと `left` が同じ位置を指さないので、
    //    重ねたつもりで重ならない（2026-08-22 に一度これで空振りした）。
    wreck: () => {
      const a = document.querySelector('[id^="name#"]')
      const b = document.querySelector('[id^="kind#"]')
      for (const el of [a, b]) { el.style.top = '196px'; el.style.left = '8px'; el.style.width = '301px' }
    },
  },
  {
    name: '字を枠より広くする',
    want: '字が枠より広い',
    wreck: () => {
      const a = document.querySelector('[id^="name#"]')
      a.style.width = '20px'
      a.style.overflow = 'hidden'
      a.textContent = 'とてもとても長い名前がここに入る'
    },
  },
  {
    name: '親からはみ出させる',
    want: 'はみ出し',
    wreck: () => { document.querySelector('[id^="art#"]').style.width = '2000px' },
  },
  {
    name: 'id を重複させる',
    want: 'id の重複',
    wreck: () => {
      const cells = document.querySelectorAll('[id^="cell#"]')
      cells[1].id = cells[0].id
    },
  },
  {
    name: '画面を窓の外へ出す',
    want: '画面が窓から出ている',
    wreck: () => { document.getElementById('stage').style.transform = 'translate(0,0) scale(1)' },
  },
  {
    name: '覆いを被せる',
    want: '覆われて見えない',
    wreck: () => {
      const d = document.createElement('div')
      d.id = 'veil'
      d.className = 'n card'
      d.style.cssText = 'left:0;top:0;width:1080px;height:1920px;background:#000;position:absolute'
      document.getElementById('stage').appendChild(d)
    },
  },
]

/** ⚠️ 覆いのある画面でだけ効く試験。⭐ 「後ろは数えない・前は数える」を両方見る。 */
const VEIL_TRIALS = [
  {
    name: '覆いの後ろは数えない',
    want: null,   // ⭐ 何も出ないのが正しい
    wreck: () => {},
  },
  {
    name: '覆いの前を隠したら数える',
    want: '覆われて見えない',
    wreck: () => {
      const d = document.createElement('div')
      d.id = 'lid'
      d.className = 'n card'
      d.style.cssText = 'left:0;top:0;width:1080px;height:1920px;background:#000;position:absolute'
      document.getElementById('stage').appendChild(d)
    },
  },
]

/** ⚠️ 巻物のある画面でだけ効く試験（`/party`）。
 *  ⭐ 「切られて見えていない字」と「見えているのに覆われた字」を**両方**見る。
 *  ⚠️ 片方だけだと、巻物の中を丸ごと見ない道具になっても気づけない。 */
const SCROLL_TRIALS = [
  {
    name: '巻物から出た字は数えない',
    want: null,
    wreck: () => {},
  },
  {
    name: '巻物の中でも、見えている字を覆ったら数える',
    want: '覆われて見えない',
    wreck: () => {
      // ⭐ 1枚目の升の一言（必ず見えている）の上に蓋を置く
      const note = document.querySelector('[id^="cellA-note#0"]')
      const r = note.getBoundingClientRect()
      const s = document.getElementById('stage').getBoundingClientRect()
      const k = s.width / 1080
      const d = document.createElement('div')
      d.id = 'lid'
      d.className = 'n card'
      d.style.cssText = `left:${(r.left - s.left) / k}px;top:${(r.top - s.top) / k}px;`
        + `width:${r.width / k}px;height:${r.height / k}px;background:#000;position:absolute`
      document.getElementById('stage').appendChild(d)
    },
  },
]

/** ⚠️ 折り返す字のある画面でだけ効く試験（`/skill`）。
 *  ⭐ 「枠に何行入るか」は骨組みが決めているので、溢れたら本当の不備。 */
const WRAP_TRIALS = [
  {
    name: '折り返す字が枠より高い',
    want: '字が枠より高い',
    wreck: () => {
      const body = document.getElementById('body')
      body.textContent = 'とても長い説明文がここに入る。'.repeat(20)
    },
  },
  {
    // ⚠️ **この検査が生きていることの確認。**⭐ `.wrapped` が1つも無ければ、
    //    上の試験は「たまたま通った」だけで、道具は何も見ていない。
    name: '折り返す字がそもそも在る',
    want: null,
    wreck: () => {},
    check: () => document.querySelectorAll('#stage .n.label.wrapped').length,
  },
]

const browser = await chromium.launch()
const page = await browser.newPage({ viewport: { width: 390, height: 844 } })
let missed = 0

// ⭐ まず素で通ることを確かめる（壊れていないのに落ちるなら、道具のほうが壊れている）
await page.goto(URL, { waitUntil: 'networkidle' })
await page.waitForFunction(() => document.querySelectorAll('#stage .n').length > 0)
const clean = await page.evaluate(audit)
console.log(clean.length === 0
  ? '素の状態: ⭐ 不備なし'
  : `素の状態: ⚠️ ${clean.length} 件（先に直すこと）\n  ` + clean.slice(0, 3).join('\n  '))

for (const t of TRIALS) {
  await page.reload({ waitUntil: 'networkidle' })
  await page.waitForFunction(() => document.querySelectorAll('#stage .n').length > 0)
  // ⚠️ 字が届く前に測らない（代替フォントの幅で答えが出る）
  await page.evaluate(() => document.fonts.ready).catch(() => {})
  await page.evaluate(t.wreck)
  const bad = await page.evaluate(audit)
  const hit = bad.find(b => b.includes(t.want))
  if (hit) {
    console.log(`⭐ ${t.name} → 捕まえた: ${hit.slice(0, 76)}`)
  } else {
    missed++
    console.log(`🔴 ${t.name} → **素通り**（「${t.want}」が出るはず）`)
    for (const b of bad.slice(0, 2)) console.log(`     出たのは: ${b.slice(0, 70)}`)
  }
}

// ── 覆いのある画面 ────────────────────────────────
const veilUrl = URL.replace(/\/$/, '') + '/ask'
for (const t of VEIL_TRIALS) {
  await page.goto(veilUrl, { waitUntil: 'networkidle' })
  await page.waitForFunction(() => !!document.getElementById('dim'))
  await page.evaluate(t.wreck)
  const bad = await page.evaluate(audit)
  const hit = t.want ? bad.find(b => b.includes(t.want)) : null
  if (t.want === null) {
    const noise = bad.filter(b => b.includes('覆われて見えない'))
    if (noise.length === 0) console.log(`⭐ ${t.name} → 数えていない`)
    else { missed++; console.log(`🔴 ${t.name} → **${noise.length}件 数えた**: ${noise[0].slice(0,60)}`) }
  } else if (hit) {
    console.log(`⭐ ${t.name} → 捕まえた: ${hit.slice(0, 70)}`)
  } else {
    missed++
    console.log(`🔴 ${t.name} → **素通り**`)
  }
}

// ── 巻物のある画面 ────────────────────────────────
const poolUrl = URL.replace(/\/$/, '') + '/party'
for (const t of SCROLL_TRIALS) {
  await page.goto(poolUrl, { waitUntil: 'networkidle' })
  await page.waitForFunction(() => !!document.querySelector('[id^="cellA-note#0"]'))
    .catch(() => {})
  await page.evaluate(t.wreck)
  const bad = await page.evaluate(audit)
  const noise = bad.filter(b => b.includes('覆われて見えない'))
  if (t.want === null) {
    if (noise.length === 0) console.log(`⭐ ${t.name} → 数えていない`)
    else { missed++; console.log(`🔴 ${t.name} → **${noise.length}件 数えた**: ${noise[0].slice(0, 66)}`) }
  } else if (noise.length) {
    console.log(`⭐ ${t.name} → 捕まえた: ${noise[0].slice(0, 66)}`)
  } else {
    missed++
    console.log(`🔴 ${t.name} → **素通り**`)
  }
}

// ── 折り返す字のある画面 ──────────────────────────
const wrapUrl = URL.replace(/\/$/, '') + '/skill?at=0'
for (const t of WRAP_TRIALS) {
  await page.goto(wrapUrl, { waitUntil: 'networkidle' })
  await page.waitForFunction(() => !!document.getElementById('body')).catch(() => {})
  if (t.check) {
    const n = await page.evaluate(t.check)
    if (n > 0) console.log(`⭐ ${t.name} → ${n} 個ある`)
    else { missed++; console.log(`🔴 ${t.name} → **0個** ── 上の試験は何も見ていない`) }
    continue
  }
  await page.evaluate(t.wreck)
  const bad = await page.evaluate(audit)
  const hit = bad.find(b => b.includes(t.want))
  if (hit) console.log(`⭐ ${t.name} → 捕まえた: ${hit.slice(0, 66)}`)
  else { missed++; console.log(`🔴 ${t.name} → **素通り**`) }
}

await browser.close()
console.log(missed === 0
  ? `\n⭐ 検査は効いている（試した ${TRIALS.length + VEIL_TRIALS.length + SCROLL_TRIALS.length + WRAP_TRIALS.length} 件すべてが正しく動いた）`
  : `\n🔴 ${missed} 件が素通り ── この検査は、その分だけ嘘をつく`)
process.exit(missed ? 1 : 0)
