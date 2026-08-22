/** 画面が描けないときに、ブラウザの言い分を読む。⚠️ 使い捨てでなく置いておく
 *  ── 「0件」と「描けていない」の区別が付かなくなるのが一番困る。
 *  使い方: node scripts/why.mjs http://localhost:5817/box */
import { chromium } from 'playwright'
const b = await chromium.launch()
const p = await b.newPage()
p.on('console', m => console.log('[console]', m.type(), m.text().slice(0, 600)))
p.on('pageerror', e => console.log('[pageerror]', String(e).slice(0, 1200)))
await p.goto(process.argv[2], { waitUntil: 'load' })
await p.waitForTimeout(6000)
console.log('部品 =', await p.evaluate(() => document.querySelectorAll('#stage .n').length))
await b.close()
