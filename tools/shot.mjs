#!/usr/bin/env node
/** 画面を1枚ずつ撮る。⭐ **数の検査で言えないこと**（意図と違う・読みにくい）だけのため。
 *
 *  ⚠️ **これで被りを判定しない。**viewport 全体のスクショは読み戻すと縮んで届き、
 *  被りもクリッピングも見えない（罠・2026-05-23）。⭐ そこは `inspect.mjs` が数で見る。
 *
 *  使い方: node scripts/shot.mjs [URL] [path ...]
 *  出力: records/shots/<名前>.png
 */

import { chromium } from 'playwright'
import { mkdirSync } from 'fs'
import { fileURLToPath } from 'url'
import { dirname, join } from 'path'

const here = dirname(fileURLToPath(import.meta.url))
const out = join(here, '..', 'records', 'shots')
mkdirSync(out, { recursive: true })

const args = process.argv.slice(2)
const URL = (args[0] && args[0].startsWith('http') ? args.shift() : 'http://localhost:5817')
  .replace(/\/$/, '')
const PAGES = args.length ? args : ['/', '/trial', '/ask', '/box', '/breed?picked=2', '/party']

const browser = await chromium.launch()
const context = await browser.newContext({
  viewport: { width: 390, height: 844 },
  deviceScaleFactor: 3,   // ⭐ 実寸に近い解像度で残す
})
const page = await context.newPage()

for (const path of PAGES) {
  await page.goto(URL + path)
  await page.waitForFunction(() => document.querySelectorAll('#stage .n').length > 3,
    null, { timeout: 30000 }).catch(() => {})
  const name = (path === '/' ? 'book' : path.slice(1)).replace(/[?&=]/g, '_')
  const file = join(out, name + '.png')
  await page.screenshot({ path: file })
  console.log(`⭐ ${path} → ${file}`)
}

await browser.close()
