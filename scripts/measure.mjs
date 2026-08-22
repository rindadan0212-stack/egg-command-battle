#!/usr/bin/env node
/** 骨組みに書く高さを、**実物のフォントで測って**決める。
 *
 *  ⚠️ **記憶で書かない。**⭐ 一度 26pt だと思い込んで「溢れる」と誤報したことがある
 *  （実物は 20pt・2026-08-22）。
 *
 *  ⚠️ Unity の `Ui.Height` は採寸用の GameObject を作って測っていた。
 *  ⭐ web では**実際に描く道具そのもの**で測れる ── 同じフォント・同じ折り返し。
 *
 *  使い方: node scripts/measure.mjs [URL]
 */

import { chromium } from 'playwright'

const URL = (process.argv[2] || 'http://localhost:5817').replace(/\/$/, '')

const browser = await chromium.launch()
const page = await browser.newPage({ viewport: { width: 1280, height: 900 } })
await page.goto(URL + '/measure')
await page.waitForFunction(() => document.querySelectorAll('.probe').length > 0,
  null, { timeout: 30000 })
// ⚠️ フォントが届く前に測ると、代替フォントの幅で答えが出る
await page.evaluate(() => document.fonts.ready)

const rows = await page.evaluate(() => {
  const out = {}
  for (const el of document.querySelectorAll('.probe')) {
    const kind = el.dataset.kind
    const h = el.getBoundingClientRect().height
    if (!out[kind] || h > out[kind].h) out[kind] = { h, id: el.dataset.id, text: el.textContent.slice(0, 40) }
  }
  return out
})

console.log('いちばん高いもの（設計 px）:')
for (const [kind, r] of Object.entries(rows)) {
  console.log(`  ${kind.padEnd(6)} ${String(Math.ceil(r.h)).padStart(5)}  ${r.id}「${r.text}」`)
}
await browser.close()
