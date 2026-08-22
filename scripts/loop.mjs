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
/** ⭐ **指と同じやり方で押す** ── 場所を測って、そこを突く。
 *
 * ⚠️ オートで戦っているあいだ、画面は**1手ごとに丸ごと組み直る**（Unity 版も同じ）。
 * ⭐ 遊ぶ人は困らない ── 拾うのは `document` の `pointerup` で、
 *   離した瞬間に**その場所に在る物**を `closest` で辿るため。
 * ⚠️ しかし Playwright の `click` は「掴んだ物が落ち着くまで」待つので、
 *   組み直りが続くと**永久に押せない**（実測: 30秒 retry して諦めた）。
 * ⭐ だから掴まずに座標を突く。**検査の都合で作りを曲げない。** */
const poke = async (sel) => {
  const box = await page.evaluate((s) => {
    const el = document.querySelector(s)
    if (!el) return null
    const r = el.getBoundingClientRect()
    return { x: r.left + r.width / 2, y: r.top + r.height / 2 }
  }, sel)
  if (!box) return false
  await page.mouse.click(box.x, box.y)
  await page.waitForTimeout(200)
  return true
}

// ── 探索へ ──────────────────────────────────────
await tap('[id="tab#1"]')
say((await title()) === '探索', '探索へ移れる', await title())

say(await tap('[id="card#0"]'), '巣の札を押せる')
say((await title()) !== '探索', '潜入が始まる', await title())
say(await has('#roll'), 'さいころの釦が出ている')

// ── 一巡する ────────────────────────────────────
/** ⭐ **入っているかは画面に聞く。**⚠️ 自分で数えていた頃は、押した回数と
 *  実際の状態がずれて、入れたり切ったりを繰り返して嵌まった。 */
const autoOn = () => page.evaluate(() =>
  (document.getElementById('pick')?.textContent || '').includes('ON'))

let rolls = 0, fought = false, done = false
for (let step = 0; step < 60; step++) {
  const now = await title()

  if (await has('#hand')) {
    fought = true
    // ⭐ オートは**一度だけ**入れる（入り切りの札なので、押すたび切り替わる）
    if (!(await autoOn())) await tap('#pick')
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

// ── もう1つの輪: 試練 ───────────────────────────
// ⚠️ **試練は巣ではない。**⭐ 勝っても負けても**試練の一覧へ帰る**のが決まり
//    ── 巣の後始末（卵・引き直し）が動いたら、そこが混ざっている印。
await tap('#trial')
say((await title()) === '試練', '試練へ入れる', await title())
say(await tap('[id="card#0"]'), '段の札を押せる')
say((await title()) !== '試練', '戦いが始まる', await title())

// ── あきらめる（⚠️ 取り返しがつかないので一度だけ確かめる）──────
// ⚠️ ここは**戦いが動いている最中**に押す（`poke` を使う理由がこれ）
say(await poke('#give'), '「あきらめる」を押せる')
say(await has('#stop-card'), '　確かめが出る')
// ⭐ 確かめている間は時が止まる（読む時間は考える時間でもある）
await poke('#stop-card')
say(!(await has('#stop-card')) && (await title()) !== '試練', '「やめる」で戦いへ戻る', await title())
await poke('#give')
await poke('#go-card')
say((await title()) === '試練', 'あきらめると負けとして畳まれる', await title())

// ⭐ もう一度入って、今度は最後まで戦う
say(await tap('[id="card#0"]'), 'もう一度挑める')

let back = false
for (let step = 0; step < 90; step++) {
  if (await has('#finish')) { await tap('#finish'); continue }
  if (await has('#hand')) {
    if (!(await autoOn())) await tap('#pick')
    await page.waitForTimeout(900)
    continue
  }
  if ((await title()) === '試練') { back = true; break }
  break
}
say(back, '決着したら試練の一覧へ帰る', await title())
// ⚠️ **卵は出ない。**⭐ 出すと「試練で卵を稼ぐ」が最短経路になる
const said = await page.evaluate(() => document.getElementById('say')?.textContent || '')
say(!said.includes('卵'), '　卵は出ない', said.slice(0, 30))

await browser.close()
console.log(bad === 0 ? '\n⭐ 遊びの輪が閉じている' : `\n🔴 ${bad} 件で止まる`)
process.exit(bad ? 1 : 0)
