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

await browser.close()
console.log(missed === 0
  ? '\n⭐ 検査は効いている（わざと壊した5件すべてを捕まえた）'
  : `\n🔴 ${missed} 件が素通り ── この検査は、その分だけ嘘をつく`)
process.exit(missed ? 1 : 0)
