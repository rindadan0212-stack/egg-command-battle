/** 画面が落ち着かないときに、ブラウザの言い分を読む。
 *  ⭐ **推測しない。**組み直しの回数を数える。
 *  使い方: node scripts/why.mjs http://localhost:5817/app */
import { chromium } from 'playwright'
const b = await chromium.launch()
const p = await b.newPage({ viewport: { width: 390, height: 844 } })
p.on('pageerror', e => console.log('[pageerror]', String(e).slice(0, 400)))
await p.goto(process.argv[2] || 'http://localhost:5817/app?seed=20260822')
await p.waitForTimeout(5000)

const has = s => p.evaluate(x => !!document.querySelector(x), s)
const title = () => p.evaluate(() => document.getElementById('title')?.textContent || '')

await p.click('[id="tab#1"]', { force: true }); await p.waitForTimeout(200)
await p.click('[id="card#0"]', { force: true }); await p.waitForTimeout(300)
console.log('潜入:', await title())

for (let i = 0; i < 30 && !(await has('#hand')); i++) {
  const lit = await p.evaluate(() =>
    [...document.querySelectorAll('#stage .n.card.lead')].map(e => e.id).filter(x => x.startsWith('sq#')))
  if (lit.length) await p.click(`[id="${lit[0]}"]`, { force: true })
  else if (await has('#pay')) await p.click('#pay', { force: true })
  else if (await has('#roll:not([disabled])')) await p.click('#roll', { force: true })
  else break
  await p.waitForTimeout(150)
}
console.log('いま:', await title(), '/ 戦闘の札:', await has('#hand'))

const churn = await p.evaluate(() => new Promise(done => {
  let n = 0
  const o = new MutationObserver(ms => { for (const m of ms) n += m.addedNodes.length })
  o.observe(document.getElementById('app-body'), { childList: true, subtree: true })
  setTimeout(() => { o.disconnect(); done(n) }, 1000)
}))
console.log('1秒に足された節点 =', churn)
await b.close()
