#!/usr/bin/env node
/** 画面を**数で**検査する。⭐ Unity の `InspectScreens` の web 版。
 *
 *  ⚠️ **スクショで被りを判定しない。**枠の関係は数で見る
 *  （罠: viewport 全体のスクショは読み戻すと縮んで届き、被りが見えない）。
 *
 *  ⭐ Unity 版が見ていた5つのうち、
 *  - 字の重なり / 字が枠より広い / 枠からはみ出し / 画面の外  → ここで見る
 *  - 覆われて見えない → `elementsFromPoint` で**実際の合成結果**を見る（Unity より強い）
 *
 *  ⭐ そして Unity 版に無かったものを1つ足す:
 *  - **id の重複** ── DOM では一意でなければ、検査も指し示しも効かない
 *
 *  使い方: node scripts/inspect.mjs [URL]
 */

import { chromium } from 'playwright'
import { audit } from './audit.mjs'

const URL = process.argv[2] || 'http://localhost:5817'

/** ⚠️ 実機の幅は 320〜430。⭐ 一番狭いところで測るのが要点（罠22・24・26）。 */
const SIZES = [
  { w: 320, h: 568, name: 'SE1' },
  { w: 390, h: 844, name: 'iPhone 14' },
  { w: 430, h: 932, name: '15 Pro Max' },
]

/** ブラウザの中で走る検査。⚠️ ここは DOM しか触らない。 */
const browser = await chromium.launch()
let total = 0
for (const size of SIZES) {
  const page = await browser.newPage({ viewport: { width: size.w, height: size.h } })
  await page.goto(URL, { waitUntil: 'networkidle' })
  await page.waitForFunction(() => document.querySelectorAll('#stage .n').length > 0,
    null, { timeout: 20000 }).catch(() => {})
  const bad = await page.evaluate(audit)
  const parts = await page.evaluate(() => document.querySelectorAll('#stage .n').length)
  total += bad.length
  console.log(`\n■ ${size.name} (${size.w}x${size.h})  調べた部品 ${parts}`)
  if (!bad.length) console.log('  ⭐ 不備なし')
  for (const line of bad) console.log('  ⚠️ ' + line)
  await page.close()
}
await browser.close()
console.log(`\n合計 ${total} 件`)
process.exit(total ? 1 : 0)
