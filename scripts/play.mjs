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
// ⚠️ **落ちてくるものを受け取る**（控えの書き出しを、本当に落ちるところまで見る）
const page = await browser.newPage({ viewport: { width: 390, height: 844 }, acceptDownloads: true })
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
const parts = () => page.evaluate(() => document.querySelectorAll('#app-body .n').length)

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

// ── 分解 ─────────────────────────────────────────
// ⚠️ **押しても何も起きない釦は、置いてあるだけで「在る」ことになってしまう。**
//    ⭐ だから「数が動いたか」まで見る。
const exp = () => page.evaluate(() =>
  Number((document.getElementById('badge')?.textContent || '').replace(/\D/g, '')) || 0)
const kids = () => page.evaluate(() =>
  Number((document.querySelector('[id^="tcount#3"]')?.textContent || '0/0').split('/')[0]) || 0)

await tab(3)
// ⚠️ **一番弱いものを見た状態で分解する。**⭐ そうしないと Lv ＋1 に届かない
//    ── 始めたての4体を全部還しても、強い個体の値段には足りない（実測 38 対 40）。
//    ⚠️ これは検査の都合ではなく**遊びの形**（EXP の主な出所は放置のほう）。
await page.click('[id="cellA#3"]')
await page.waitForTimeout(120)
const expWas = await exp(), kidsWas = await kids()
await page.click('#bfuse')
await page.waitForTimeout(150)
say(await page.evaluate(() => !!document.getElementById('go-card')), '分解の札が開く')
const food = await page.evaluate(() =>
  [...document.querySelectorAll('[id^="cell-card#"]')].map(e => e.id))
say(food.length === kidsWas - 1, '候補から「見ている本人」だけ外れる', `${food.length} 体`)
for (const one of food) { await page.click(`[id="${one}"]`); await page.waitForTimeout(60) }
const armed = await page.evaluate(() => document.getElementById('go-card')?.disabled)
say(armed === false, 'えらぶと「分解する」が押せる', `disabled=${armed}`)
await page.click('#go-card')
await page.waitForTimeout(200)
say((await exp()) > expWas, '分解で EXP が増える', `${expWas} → ${await exp()}`)
say((await kids()) === kidsWas - food.length, '分解した個体は居なくなる',
  `${kidsWas} → ${await kids()}`)

// ── Lv ＋1 ───────────────────────────────────────
const lv = () => page.evaluate(() => document.getElementById('detail-lv')?.textContent || '')
const lvWas = await lv()
say((await page.evaluate(() => document.getElementById('bgrow')?.disabled)) === false,
  'EXP が足りると Lv ＋1 が押せる',
  await page.evaluate(() => document.getElementById('bgrow')?.textContent || ''))
await page.click('#bgrow')
await page.waitForTimeout(200)
say((await lv()) !== lvWas, 'Lv ＋1 で本当に上がる', `${lvWas} → ${await lv()}`)
// ⚠️ **黙って何もしないをしない。**⭐ 足りなくなったら値段を出して押せなくする
const priced = await page.evaluate(() => {
  const e = document.getElementById('bgrow')
  return { off: e?.disabled, text: e?.textContent || '' }
})
say(!priced.off || /EXP\s*[\d,]+/.test(priced.text),
  '　足りないときは値段が札に出ている', priced.text)

// ── 技を鍛える ───────────────────────────────────
await page.click('#btrain')
await page.waitForTimeout(150)
say(await page.evaluate(() => !!document.getElementById('head-card')), '技を鍛える札が開く')
await page.click('#close-card')
await page.waitForTimeout(120)
say(await page.evaluate(() => !document.getElementById('head-card')), '　閉じられる')

// ── 配合する ─────────────────────────────────────
// ⚠️ **始め直す。**⭐ 上で3体還したので、ここには親が2体残っていない。
await page.evaluate(() => localStorage.clear())
await page.goto(URL + '/app?seed=20260822')
await page.waitForFunction(() => document.querySelectorAll('#stage .n').length > 3,
  null, { timeout: 30000 }).catch(() => {})
await tab(2)
const kidsB = await kids()
await page.click('[id="cellA#0"]')
await page.waitForTimeout(80)
await page.click('[id="cellA#1"]')
await page.waitForTimeout(120)
await page.click('#go')
await page.waitForTimeout(200)
say((await kids()) === kidsB - 2, '配合すると親2体が消える', `${kidsB} → ${await kids()}`)
const born = await page.evaluate(() => document.getElementById('say')?.textContent || '')
say(born.includes('卵'), '　卵ができたと言う', born.slice(0, 40))

// ── 編成 ─────────────────────────────────────────
await tab(0)
await page.click('#party')
await page.waitForTimeout(150)
say(await page.evaluate(() => !!document.getElementById('done-card')), 'ホームから編成が開く')
// ⚠️ **始めたての放置の編成は空**（「空き（自動で埋まる）」と出ている）。
//    ⭐ だから「入れてから外す」の順で見る ── 逆にすると外すものが無い。
const inParty = () => page.evaluate(() =>
  document.querySelectorAll('#stage .n.card.lead[id^="ring-card"]').length)
const seats = await inParty()
await page.click('[id="cellA-card#0"]')
await page.waitForTimeout(150)
say((await inParty()) === seats + 1, '一覧から編成へ入れられる', `${seats} → ${await inParty()}`)
await page.click('[id="seat-card#0"]')
await page.waitForTimeout(150)
say((await inParty()) === seats, '選んでいる枠を押すと外れる', `${await inParty()}`)
await page.click('#done-card')
await page.waitForTimeout(120)
say((await title()) === 'EGG COMMAND', '「決定」で閉じる', await title())

// ── 長押し（⭐ 押しどころとは別の道）──────────────
// ⚠️ **短く触っても開かない** ── 技の札は押しどころではないので、
//    触っただけで開くと一覧を選ぶ指が誤爆する。
const hold = async (sel, ms = 700) => {
  const box = await page.evaluate((s) => {
    const el = document.querySelector(s)
    if (!el) return null
    const r = el.getBoundingClientRect()
    return { x: r.left + r.width / 2, y: r.top + r.height / 2 }
  }, sel)
  if (!box) return false
  await page.mouse.move(box.x, box.y)
  await page.mouse.down()
  await page.waitForTimeout(ms)
  await page.mouse.up()
  await page.waitForTimeout(200)
  return true
}
await tab(3)
await page.click('#detail-s0')
await page.waitForTimeout(200)
say(await page.evaluate(() => !document.getElementById('steps-card')),
  '技の札は短く触っても開かない')
say(await hold('#detail-s0'), '技の札を長押しできる')
say(await page.evaluate(() => !!document.getElementById('steps-card')), '　技の詳細が開く')
await page.click('#close-card')
await page.waitForTimeout(150)
say(await page.evaluate(() => !document.getElementById('steps-card')), '　閉じられる')

// ── 図鑑と試練（右肩とホームの入口）────────────────
await tab(0)
await page.click('#extra')
await page.waitForTimeout(150)
say((await title()) === '図鑑', '右肩から図鑑へ', await title())
// ⭐ 見たことのある種族だけ押せる。⚠️ 伏せてある札を押しても開かない
const known = await page.evaluate(() =>
  [...document.querySelectorAll('#stage [id^="cell#"]')].find(e => e.dataset.tap)?.id)
if (known) {
  await page.click(`[id="${known}"]`)
  await page.waitForTimeout(200)
  say(await page.evaluate(() => !!document.getElementById('s2head-card')), '種族の札が開く')
  // ⭐ 技の詳細は**種族の札の上にも出る**。⚠️ 閉じたら種族の札へ戻る
  say(await hold('[id="s1chip-card#0"]'), '　抽選の技を長押しできる')
  say(await page.evaluate(() => !!document.getElementById('steps-card')), '　技の詳細が重なる')
  // ⚠️ **重ねた札は名前がぶつかる**（どちらにも `close` と `dim` が在る）。
  //    ⭐ 下の札は名前をずらして描く ── 重なると検査も指し示しも効かない。
  const twice = await page.evaluate(() => {
    const seen = new Map()
    for (const el of document.querySelectorAll('#stage [id]'))
      seen.set(el.id, (seen.get(el.id) || 0) + 1)
    return [...seen].filter(([, n]) => n > 1).map(([id]) => id)
  })
  say(twice.length === 0, '　重ねても id がぶつからない', twice.join(' '))
  await page.click('#close-card')
  await page.waitForTimeout(200)
  say(await page.evaluate(() =>
    !document.getElementById('steps-card') && !!document.getElementById('s2head-card')),
    '　閉じると種族の札へ戻る')
  await page.click('#close-card')
  await page.waitForTimeout(150)
}
await page.click('#back')
await page.waitForTimeout(150)
say((await title()) === 'EGG COMMAND', '‹ でホームへ戻れる', await title())
await page.click('#trial')
await page.waitForTimeout(150)
say((await title()) === '試練', 'ホームから試練へ', await title())
// ⚠️ 試練は**巣ではない** ── 勝っても負けても試練の一覧へ帰るのが決まり
await page.click('[id="card#0"]')
await page.waitForTimeout(300)
say((await title()) !== '試練', '段を押すと戦いが始まる', await title())
await page.click('#back').catch(() => {})

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

// ── 控えの出し入れ（⭐ ブラウザの外へ出す唯一の口）────────
await page.evaluate(() => localStorage.clear())
await page.goto(URL + '/app?seed=20260822')
await page.waitForFunction(() => document.querySelectorAll('#stage .n').length > 3,
  null, { timeout: 30000 }).catch(() => {})
await page.click('#keep')
await page.waitForTimeout(300)
say(await page.evaluate(() => !!document.getElementById('out-card')), '保存の控えが開く')
say(/\d/.test(await page.evaluate(() =>
  document.getElementById('where-card')?.textContent || '')), '　いまの保存の大きさが出る',
  await page.evaluate(() => document.getElementById('where-card')?.textContent || ''))

// ⭐ 書き出す。⚠️ ブラウザの外へ出る唯一の道なので、**本当に落ちるか**まで見る
const down = page.waitForEvent('download', { timeout: 15000 })
await page.click('#out-card')
const file = await down.catch(() => null)
say(!!file, '書き出すと控えが落ちる', file ? await file.suggestedFilename() : '落ちてこない')
if (file) {
  const body = await (await import('node:fs/promises')).readFile(await file.path(), 'utf8')
  say(body.includes('"Seed"'), '　中身は保存そのもの', body.slice(0, 40))
}

// ⭐ 読み込む。⚠️ **作者の実物**（Unity で遊んだ保存）を、画面から通す
await page.click('#keep').catch(() => {})
await page.waitForTimeout(200)
const chooser = page.waitForEvent('filechooser', { timeout: 15000 })
await page.click('#in-card')
await (await chooser).setFiles('unity-port/records/save-unity.json')
await page.waitForTimeout(800)
const loaded = await page.evaluate(() => document.getElementById('say')?.textContent || '')
say(loaded.includes('読み込み'), '控えから読み込める', loaded.slice(0, 30))
await page.click('[id="tab#3"]')
await page.waitForTimeout(300)
say((await kids()) > 4, '　中身が入れ替わっている', `${await kids()} 体`)

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
