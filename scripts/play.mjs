#!/usr/bin/env node
/** ⭐ **実際に触って、画面が変わることを確かめる。**
 *
 *  ⚠️ `inspect.mjs` は「置かれた物の関係」しか見ない ── **押しても何も起きない画面**でも
 *  「不備なし」と答える。⭐ ここは指の側から見る。
 *
 *  ⚠️ **内部状態を直接いじって撮らない**（撮影の規律と同じ）。
 *  遊んで辿り着ける道だけを通る。
 *
 *  使い方: node scripts/play.mjs [URL]
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

// ⚠️ **前回の保存を消してから始める。**⭐ 残っていると、
//    同じ手順でも違う中身から始まり、落ちた理由が読めない。
await page.goto(URL + '/app?seed=20260822')
await page.evaluate(() => localStorage.clear())
await page.reload()
await page.waitForFunction(() => document.querySelectorAll('#stage .n').length > 3,
  null, { timeout: 30000 }).catch(() => {})

const title = () => page.evaluate(() => document.getElementById('title')?.textContent || '')
const parts = () => page.evaluate(() => document.querySelectorAll('#body .n').length)

say((await title()) === 'EGG COMMAND', '開いた直後はホーム', await title())

/** ⭐ 下の帯の札を押す。⚠️ 名前でなく**並びの番号**で押す（骨組みが順を持つ）。 */
const tab = async (i) => {
  await page.click(`[id="tab#${i}"]`)
  await page.waitForTimeout(120)
}

for (const [i, want] of [[1, '探索'], [2, '配合'], [3, 'BOX'], [0, 'EGG COMMAND']]) {
  await tab(i)
  const got = await title()
  say(got === want, `帯の ${i} 番で「${want}」へ移る`, got)
  say((await parts()) > 3, `　その画面が描かれている`, `${await parts()} 部品`)
}

// ── 並べ替えを開く ──────────────────────────────
await tab(3)
const before = await parts()
// ⚠️ **字のほうを押す。**⭐ 遊ぶ人が押すのも字の上で、
//    拾う側は `closest` で札まで遡る（── そこを確かめることにもなる）。
await page.click('#bar-now')
await page.waitForTimeout(120)
const after = await parts()
say(after > before, '並べ替えの帯を押すと開く', `${before} → ${after}`)
await page.click('#bar-now')
await page.waitForTimeout(120)
say((await parts()) === before, 'もう一度押すと畳む', `${await parts()}`)

// ── 一覧の升を押して、見ている個体が変わる ─────────────
const lvOf = () => page.evaluate(() => document.getElementById('detail-lv')?.textContent || '')
const was = await lvOf()
await page.click('[id="cellA#3"]')
await page.waitForTimeout(120)
const now = await lvOf()
say(was !== '' && now !== '', '升を押すと詳細が出ている', `${was} → ${now}`)

// ── 配合で親を2体えらぶ ────────────────────────────
await tab(2)
await page.click('[id="cellA#0"]')
await page.waitForTimeout(100)
await page.click('[id="cellA#1"]')
await page.waitForTimeout(120)
const filled = await page.evaluate(() =>
  !!document.getElementById('pfill-name') && !!document.getElementById('qfill-name'))
say(filled, '配合で親を2体えらべる')
const go = await page.evaluate(() => document.getElementById('go')?.disabled)
say(go === false, '2体そろうと「配合する」が押せる', `disabled=${go}`)

// ── 保存 ─────────────────────────────────────────
// ⚠️ **閉じて開いても続きから始まるか。**
// ⭐ 確かめ方: 別の種で開き直しても、**前の中身のまま**なら保存が勝っている。
await page.evaluate(() => localStorage.clear())
await page.goto(URL + '/app?seed=111')
await page.waitForFunction(() => document.querySelectorAll('#stage .n').length > 3,
  null, { timeout: 30000 }).catch(() => {})
await page.click('[id="tab#3"]')
await page.waitForTimeout(400)
const who = () => page.evaluate(() => document.getElementById('detail-sub')?.textContent || '')
const first = await who()
const size = await page.evaluate(() => (localStorage.getItem('egg:save') || '').length)
say(size > 1000, '触ると保存が書かれる', `${size} 字`)

await page.goto(URL + '/app?seed=222')
await page.waitForFunction(() => document.querySelectorAll('#stage .n').length > 3,
  null, { timeout: 30000 }).catch(() => {})
await page.click('[id="tab#3"]')
await page.waitForTimeout(400)
const again = await who()
say(first !== '' && first === again, '開き直しても続きから', `${first} → ${again}`)

// ⚠️ **読めない保存を上書きしない。**⭐ 上書きしたら直せなくなる。
await page.evaluate(() => localStorage.setItem('egg:save', '{"Version":99}'))
await page.goto(URL + '/app?seed=333')
await page.waitForTimeout(3000)
const kept = await page.evaluate(() => localStorage.getItem('egg:save'))
say(kept === '{"Version":99}', '読めない保存を上書きしない', String(kept).slice(0, 30))
const told = await page.evaluate(() => document.getElementById('say')?.textContent || '')
say(told.includes('保存'), '黙って諦めず、画面に出す', told.slice(0, 40))

await browser.close()
console.log(bad === 0 ? '\n⭐ 触っても壊れない' : `\n🔴 ${bad} 件が期待どおりに動かない`)
process.exit(bad ? 1 : 0)
