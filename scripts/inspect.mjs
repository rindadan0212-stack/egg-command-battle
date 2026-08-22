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
 *  ⚠️ **枝も1枚と数える。**⭐ 開いた並べ替え・空の親枠・放置の編成は
 *  `when=` で中身が入れ替わるので、**閉じた側しか見ない検査は嘘をつく**。
 *  だから URL に状態を出してある（`?open=true` など）。
 *
 *  使い方: node scripts/inspect.mjs [URL] [path ...]
 */

import { chromium } from 'playwright'
import { audit } from './audit.mjs'

const args = process.argv.slice(2)
const URL = (args[0] && args[0].startsWith('http') ? args.shift() : 'http://localhost:5817')
  .replace(/\/$/, '')

/** 調べる画面。⚠️ 足したら必ずここに入れる（入れないと「0件」が痩せる）。 */
const PAGES = args.length ? args : [
  '/',                    // 図鑑
  '/trial',               // 試練
  '/ask',                 // 確かめる札
  '/box',                 // BOX（畳んだ）
  '/box?open=true',          // BOX（開いた）
  // ⚠️ `picked` は**必ず書く**。⭐ Blazor は問い合わせに無い値を
  //    型の既定（0）で上書きするので、省くと**親なしの枝しか見ない**
  //    （実測 2026-08-22: `/breed` と `/breed?picked=0` が同じ 80 部品だった）。
  '/breed?picked=2',      // 配合（親2体）
  '/breed?picked=1',      // 配合（片方だけ）
  '/breed?picked=0',      // 配合（親なし）
  '/breed?picked=2&open=true',   // 配合（開いた）
  '/party',               // 編成（巣）
  '/party?open=true',        // 編成（巣・開いた）
  '/party?idle=true',        // 編成（放置）
  // ⚠️ 図鑑の中の2枚。⭐ **一番長いもの**を選んで見る
  //    （技の袋は 1〜5種・説明文は 1〜2行）。
  '/species?at=0',
  '/species?at=7',
  '/skill?at=0',
  '/skill?at=12&slot=0',
  // ⭐ ホーム。⚠️ 空の枠と入っている枠を両方見る
  '/home?eggs=3',
  '/home?eggs=0',
  '/home?eggs=6',
  // ⭐ 探索と卵の在庫。⚠️ **減ったとき**も見る
  '/nests',
  '/nests?shown=1&raids=4',
  '/eggs?have=7',
  '/eggs?have=0',
  // ⭐ 分解と技を鍛える。⚠️ **選んでいない状態**と**候補が0件**も見る
  '/fuse?picked=3',
  '/fuse?picked=0',
  '/fuse?empty=true',
  '/train?picked=3',
  '/train?picked=0&have=0',
  // ⭐ **外枠付きの本体**。⚠️ 上のバーと下の帯が乗った状態で見る
  '/app',
]

/** ⚠️ 実機の幅は 320〜430。⭐ 一番狭いところで測るのが要点（罠22・24・26）。 */
const SIZES = [
  { w: 320, h: 568, name: 'SE1' },
  { w: 390, h: 844, name: 'iPhone 14' },
  { w: 430, h: 932, name: '15 Pro Max' },
]

const browser = await chromium.launch()
// ⚠️ **器は1つだけ作る。**⭐ 画面ごとに新しい器を作ると WASM を毎回落とし直す
//    （実測 2026-08-22: 33回の読み直しで6分経っても終わらなかった）。
const context = await browser.newContext({ viewport: { width: 390, height: 844 } })
const page = await context.newPage()
let total = 0
let thin = 0

for (const path of PAGES) {
  console.log(`\n━━ ${path}`)
  for (const size of SIZES) {
    await page.setViewportSize({ width: size.w, height: size.h })
    await page.goto(URL + path)
    await page.waitForFunction(() => document.querySelectorAll('#stage .n').length > 0,
      null, { timeout: 30000 }).catch(() => {})
    const bad = await page.evaluate(audit)
    const parts = await page.evaluate(() => document.querySelectorAll('#stage .n').length)
    total += bad.length
    // ⚠️ **部品が少なすぎる＝描けていない。**⭐ 「不備なし」と区別する
    if (parts < 4) { thin++; console.log(`  🔴 ${size.name}: 部品が ${parts} 個しか無い`) }
    else if (!bad.length) console.log(`  ⭐ ${size.name}: 不備なし（${parts}）`)
    else {
      console.log(`  ⚠️ ${size.name}: ${bad.length} 件（${parts}）`)
      for (const line of bad) console.log('     ' + line)
    }
  }
}
await browser.close()
console.log(`\n${PAGES.length} 画面 × ${SIZES.length} サイズ ── 不備 ${total} 件`
  + (thin ? ` / 🔴 描けていない ${thin} 件` : ''))
process.exit(total || thin ? 1 : 0)
