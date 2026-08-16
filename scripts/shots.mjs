#!/usr/bin/env node
/** 画面カタログ。⭐ **「ふたを開けてみたら違った」を無くすための道具。**
 *
 *  全画面 × 実機サイズを一括で撮り、1ページに並べる。
 *
 *  ```
 *  npm run shots            全部撮る
 *  npm run shots -- --open  撮ってから既定のブラウザで開く
 *  npm run shots -- --only=battle,home   画面を絞る
 *  ```
 *
 *  ⚠️ **これを出す前に「完成しました」と言わない。**
 *  数値の検査（見切れ・コントラスト）は「壊れていないこと」しか言えず、
 *  「意図と違う」は捕まえられない。それを見るのは人間の目でしかない。
 *
 *  ⚠️ 撮影は**実際に触って**行う（クリックとドラッグ）。
 *  内部状態を直接いじって撮ると、**遊んだときには辿り着けない画面**を
 *  「出来ている」と報告することになる。
 */

import { spawn } from 'node:child_process'
import { execFileSync } from 'node:child_process'
import { mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium } from 'playwright'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..')
const OUT = join(ROOT, 'shots')
const PORT = 5815
const URL = `http://localhost:${PORT}/`

/** 実機の代表。⚠️ **1つで通っても通ったことにならない。**
 *  下2つ（狭い・低い）で壊れるのがいつもの形。 */
const SIZES = [
  { w: 320, h: 568, name: 'iPhone SE' },
  { w: 360, h: 640, name: 'Android 小' },
  { w: 390, h: 844, name: 'iPhone 14' },
  { w: 430, h: 932, name: 'iPhone Pro Max' },
]

/** 撮る画面と、そこへ**実際に辿り着く手順**。 */
const SCREENS = [
  { id: 'home', name: 'ホーム', go: async () => {} },
  { id: 'box', name: 'BOX', go: async (p) => p.locator('#d-box').click() },
  { id: 'hatch', name: '孵化', go: async (p) => p.locator('#d-hatch').click() },
  {
    id: 'breed',
    name: '配合',
    go: async (p) => {
      await p.locator('#d-breed').click()
      await p.waitForTimeout(250)
      // 2体選んだ状態。⭐ 空の画面ではなく「使っている最中」を撮る
      await p.locator('.pick').nth(0).click()
      await p.locator('.pick').nth(1).click()
    },
  },
  { id: 'nests', name: '巣をえらぶ', go: async (p) => p.locator('#d-nests').click() },
  {
    id: 'steal',
    name: '発射',
    go: async (p) => {
      await p.locator('#d-nests').click()
      await p.waitForTimeout(250)
      await p.locator('.nestcard .challenge').first().click()
    },
  },
  {
    id: 'battle',
    name: '戦闘',
    go: async (p) => {
      await p.locator('#d-nests').click()
      await p.waitForTimeout(250)
      await p.locator('.nestcard .challenge').first().click()
      await p.waitForTimeout(500)
      // わざと外して戦闘へ。⚠️ 内部状態を書き換えず、本当に引っ張る
      const box = await p.locator('#s-field').boundingBox()
      if (!box) throw new Error('発射のフィールドが見つからない')
      await p.mouse.move(box.x + box.width / 2, box.y + box.height * 0.7)
      await p.mouse.down()
      await p.mouse.move(box.x + box.width / 2 + 60, box.y + box.height * 0.9, { steps: 6 })
      await p.mouse.up()
      // 軌跡をなぞりきって結果が出るまで待つ
      await p.waitForSelector('.sheet', { timeout: 20000 })
    },
  },
]

const args = process.argv.slice(2)
const only = args.find((a) => a.startsWith('--only='))?.slice('--only='.length)
const wanted = only ? new Set(only.split(',')) : null
const screens = wanted ? SCREENS.filter((s) => wanted.has(s.id)) : SCREENS

function serverAlive() {
  try {
    const out = execFileSync('netstat', ['-ano'], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] })
    return out.split(/\r?\n/).some((l) => l.includes(`:${PORT}`) && /LISTENING/i.test(l))
  } catch {
    return false
  }
}

/** ⚠️ 既に上がっているサーバーは**落とさない**（作業中の画面を殺さないため）。
 *  自分で起こしたときだけ、自分で落とす。 */
let spawned = null
async function ensureServer() {
  if (serverAlive()) return false
  // ⚠️ Windows で `npm.cmd` を spawn すると EINVAL、`shell:true` は非推奨警告。
  //    vite を node で直に起こすのが一番素直。
  spawned = spawn(process.execPath, [join(ROOT, 'node_modules', 'vite', 'bin', 'vite.js')], {
    cwd: ROOT,
    stdio: 'ignore',
  })
  for (let i = 0; i < 60; i++) {
    await new Promise((r) => setTimeout(r, 300))
    if (serverAlive()) return true
  }
  throw new Error('dev サーバーが上がらなかった')
}

async function main() {
  const started = await ensureServer()
  rmSync(OUT, { recursive: true, force: true })
  mkdirSync(OUT, { recursive: true })

  const browser = await chromium.launch()
  const shots = []
  const problems = []

  for (const size of SIZES) {
    const context = await browser.newContext({
      viewport: { width: size.w, height: size.h },
      deviceScaleFactor: 2,
      hasTouch: true,
      isMobile: true,
    })
    const page = await context.newPage()

    for (const screen of screens) {
      const file = `${screen.id}-${size.w}x${size.h}.png`
      try {
        await page.goto(URL)
        await page.waitForTimeout(700)
        await screen.go(page)
        await page.waitForTimeout(600)
        await page.screenshot({ path: join(OUT, file) })
        // ⭐ 見た目と一緒に「壊れていないか」の数値も残す。両方あって初めて判断できる
        const check = await page.evaluate(audit)
        shots.push({ screen: screen.id, name: screen.name, size, file, check })
        if (check.length > 0) problems.push(`${screen.name} ${size.w}x${size.h}: ${check.join(' / ')}`)
      } catch (error) {
        shots.push({ screen: screen.id, name: screen.name, size, file: null, check: [String(error.message)] })
        problems.push(`${screen.name} ${size.w}x${size.h}: 撮れなかった（${error.message}）`)
      }
    }
    await context.close()
  }

  await browser.close()
  writeFileSync(join(OUT, 'index.html'), buildIndex(shots), 'utf8')

  if (started) {
    execFileSync('node', [join(ROOT, 'scripts', 'stop.mjs')], { stdio: 'inherit' })
  }

  console.log(`\n撮った枚数: ${shots.filter((s) => s.file).length} / ${shots.length}`)
  if (problems.length > 0) {
    console.log('\n⚠️ 数値の検査で引っかかったもの:')
    for (const p of problems) console.log(`  ${p}`)
  } else {
    console.log('数値の検査（見切れ・横切れ・文字切れ・タップ域）は全部通っている')
  }
  console.log(`\n見るのはこれ:\n  ${join(OUT, 'index.html')}`)
  console.log('⚠️ 数値が通っていることと、意図どおりであることは別。目で見て判断する。')

  if (args.includes('--open')) {
    spawn('cmd.exe', ['/c', 'start', '', join(OUT, 'index.html')], {
      detached: true,
      stdio: 'ignore',
    }).unref()
  }
}

/** 画面の中で機械が判定できることだけ。⚠️ これは「壊れていない」しか言えない。 */
function audit() {
  const phone = document.querySelector('.phone')
  if (!phone) return ['.phone が無い']
  const p = phone.getBoundingClientRect()
  const bad = []
  const seen = new Set()
  const push = (m) => {
    if (!seen.has(m)) {
      seen.add(m)
      bad.push(m)
    }
  }

  if (document.documentElement.scrollHeight > document.documentElement.clientHeight + 1) {
    push('ページ自体がスクロールする')
  }

  for (const el of phone.querySelectorAll('*')) {
    const r = el.getBoundingClientRect()
    const cs = getComputedStyle(el)
    if (!r.width || !r.height || cs.visibility === 'hidden') continue

    // 横に切れている（縦持ちの画面で横に動くのは事故）。
    // ⚠️ `data-bleed` が付いた箱は「わざと外へ出して切っている」という宣言なので見逃す。
    //    ただし**中身は見る**（宣言が本物のはみ出しを覆い隠さないように）。
    if (
      el.scrollWidth > el.clientWidth + 1 &&
      cs.overflowX !== 'visible' &&
      el.dataset.bleed !== 'true'
    ) {
      push(`横に切れている: ${el.className || el.tagName}`)
    }
    // 器の外へ出ている。スクロール層と切り落とし層の中は除く
    if (r.right > p.right + 1 || r.left < p.left - 1 || r.bottom > p.bottom + 1 || r.top < p.top - 1) {
      let inside = false
      let n = el.parentElement
      while (n && n !== phone) {
        const s = getComputedStyle(n)
        if (s.overflowY === 'auto' || s.overflow === 'hidden') {
          inside = true
          break
        }
        n = n.parentElement
      }
      if (!inside) push(`はみ出し: ${el.tagName}.${el.className}`)
    }
    if (el.tagName === 'BUTTON' && (r.height < 43.5 || r.width < 43.5)) {
      push(`タップ域が 44px 未満: ${el.className || el.textContent.trim().slice(0, 8)}`)
    }
  }

  for (const el of phone.querySelectorAll('.title,.name,.fname,.dlabel,.gname,.lead')) {
    if (el.scrollWidth > el.clientWidth + 1) push(`文字が途切れる: ${el.className}`)
  }
  return bad
}

function buildIndex(shots) {
  const byScreen = new Map()
  for (const s of shots) {
    if (!byScreen.has(s.screen)) byScreen.set(s.screen, [])
    byScreen.get(s.screen).push(s)
  }
  const rows = [...byScreen.values()]
    .map((group) => {
      const title = group[0].name
      const cells = group
        .map((s) => {
          const problems = s.check.length
            ? `<ul class="bad">${s.check.map((c) => `<li>${esc(c)}</li>`).join('')}</ul>`
            : '<p class="ok">機械の検査は通っている</p>'
          const img = s.file
            ? `<img src="${s.file}" alt="${esc(title)} ${s.size.w}x${s.size.h}">`
            : '<div class="missing">撮れなかった</div>'
          return `<figure>
  <figcaption>${s.size.w}×${s.size.h}<span>${esc(s.size.name)}</span></figcaption>
  ${img}
  ${problems}
</figure>`
        })
        .join('\n')
      return `<section><h2>${esc(title)}</h2><div class="row">${cells}</div></section>`
    })
    .join('\n')

  return `<!doctype html>
<meta charset="utf-8">
<title>画面カタログ — Egg Command Battle</title>
<style>
  :root { color-scheme: light; }
  body { margin: 0; padding: 24px 28px 60px; background: #f4f1ea; color: #2b3350;
         font: 14px/1.6 "Noto Sans JP", "Yu Gothic UI", system-ui, sans-serif; }
  h1 { font-size: 20px; margin: 0 0 4px; }
  .lead { margin: 0 0 28px; font-size: 13px; color: #5c6480; }
  h2 { font-size: 15px; margin: 30px 0 10px; }
  .row { display: flex; gap: 18px; overflow-x: auto; padding-bottom: 8px; align-items: flex-start; }
  figure { margin: 0; flex: none; width: 240px; }
  figcaption { font: 11px/1.4 ui-monospace, Consolas, monospace; color: #5c6480; margin-bottom: 6px;
               display: flex; justify-content: space-between; gap: 8px; }
  figcaption span { color: #8e93a8; }
  img { display: block; width: 100%; border: 2px solid #2b3350; background: #fff; }
  .missing { display: grid; place-items: center; height: 300px; border: 2px dashed #b04a3a; color: #b04a3a; }
  .ok { font-size: 11px; color: #4a7a52; margin: 6px 0 0; }
  .bad { font-size: 11px; color: #b04a3a; margin: 6px 0 0; padding-left: 16px; }
</style>
<h1>画面カタログ</h1>
<p class="lead">
  全画面 × 実機サイズ。⚠️ 下に出ている検査は「壊れていない」ことしか言えない。
  <strong>意図どおりかは目で見て判断する。</strong>
</p>
${rows}
`
}

function esc(s) {
  return String(s).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c])
}

main().catch((error) => {
  console.error(error)
  if (spawned) execFileSync('node', [join(ROOT, 'scripts', 'stop.mjs')], { stdio: 'inherit' })
  process.exit(1)
})
