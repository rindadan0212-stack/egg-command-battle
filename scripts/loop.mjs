#!/usr/bin/env node
/** ⭐ **遊びの輪が閉じているか。**探索 → 潜入 → さいころ → 移動 → 戦闘 → 帰還。
 *
 *  ⚠️ `inspect.mjs` は置かれた物、`play.mjs` は押した反応しか見ない。
 *  ⭐ ここが見るのは「**最後まで行けるか**」── 途中で進めなくなる形は、
 *  どちらの検査にも映らない。
 *
 *  ⚠️ **内部状態を直接いじらない。**遊んで辿り着ける道だけを通る。
 *  ⚠️ **同じ釦を繰り返し押さない** ── オートは入り切りの札なので、
 *  毎回押すと入れたり切ったりを繰り返す（一度やって自分で嵌まった）。
 *
 *  使い方: node scripts/loop.mjs [URL]
 */

import { chromium } from 'playwright'

const URL = (process.argv[2] || 'http://localhost:5817').replace(/\/$/, '')

const browser = await chromium.launch()
const page = await browser.newPage({ viewport: { width: 390, height: 844 } })
let bad = 0
const say = (ok, what, extra = '') => {
  if (ok) console.log(`⭐ ${what}${extra ? ' ── ' + extra : ''}`)
  else { bad++; console.log(`🔴 ${what}${extra ? ' ── ' + extra : ''}`) }
}

await page.goto(URL + '/app?seed=20260822')
await page.evaluate(() => localStorage.clear())
await page.reload()
await page.waitForFunction(() => document.querySelectorAll('#stage .n').length > 3,
  null, { timeout: 30000 }).catch(() => {})

const title = () => page.evaluate(() => document.getElementById('title')?.textContent || '')
const has = (sel) => page.evaluate((s) => !!document.querySelector(s), sel)
/** ⚠️ **押すのは force で。**⭐ 押した瞬間に画面が組み直るので、
 *  Playwright の「落ち着くまで待つ」は当てにならない（遊ぶ人は待たない）。 */
const tap = async (sel) => {
  if (!(await has(sel))) return false
  await page.click(sel, { force: true })
  await page.waitForTimeout(160)
  return true
}

// ── 探索へ ──────────────────────────────────────
await tap('[id="tab#1"]')
say((await title()) === '探索', '探索へ移れる', await title())

say(await tap('[id="card#0"]'), '巣の札を押せる')
say((await title()) !== '探索', '潜入が始まる', await title())
say(await has('#roll'), 'さいころの釦が出ている')

// ── 一巡する ────────────────────────────────────
let rolls = 0, fought = false, auto = false, done = false
for (let step = 0; step < 60; step++) {
  const now = await title()

  if (await has('#hand')) {
    fought = true
    // ⭐ オートは**一度だけ**入れる（入り切りの札なので、押すたび切り替わる）
    if (!auto) { await tap('#pick'); auto = true }
    await page.waitForTimeout(900)
    continue
  }
  if (await has('#finish')) { await tap('#finish'); continue }
  if (await has('#pay')) { await tap('#pay'); continue }

  const lit = await page.evaluate(() =>
    [...document.querySelectorAll('#stage .n.card.lead')]
      .map(e => e.id).filter(i => i.startsWith('sq#')))
  if (lit.length) { await tap(`[id="${lit[0]}"]`); continue }

  if (await has('#roll:not([disabled])')) { await tap('#roll'); rolls++; continue }
  if (now === '探索') { done = true; break }
  break
}

say(rolls > 0, 'さいころを振れた', `${rolls} 回`)
say(done, '一巡して探索へ戻れた', `戦闘 ${fought ? 'あり' : 'なし'} / 最後は「${await title()}」`)

// ── ホームへ戻れる ──────────────────────────────
await tap('[id="tab#0"]')
say((await title()) === 'EGG COMMAND', 'ホームへ戻れる', await title())

await browser.close()
console.log(bad === 0 ? '\n⭐ 遊びの輪が閉じている' : `\n🔴 ${bad} 件で止まる`)
process.exit(bad ? 1 : 0)
