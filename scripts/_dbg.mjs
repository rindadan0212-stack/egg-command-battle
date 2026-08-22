import { chromium } from 'playwright'
const b = await chromium.launch(); const p = await b.newPage({viewport:{width:390,height:844}})
p.on('console', m => { if (m.type()==='error') console.log('[c]', m.text().slice(0,160)) })
await p.goto(process.argv[2]); await p.waitForTimeout(6000)
console.log(await p.evaluate(() => {
  const icons = document.querySelectorAll('#stage .n.icon')
  const arts = document.querySelectorAll('#stage .icon-art')
  const one = icons[0]
  const art = arts[0]
  return JSON.stringify({
    icons: icons.length, arts: arts.length,
    first: one ? {id: one.id, cls: one.className, style: one.getAttribute('style').slice(0,120)} : null,
    art: art ? {cls: art.className, mask: getComputedStyle(art).maskImage,
                bg: getComputedStyle(art).backgroundColor,
                w: art.getBoundingClientRect().width} : null,
  })
}))
const r = await p.goto('http://localhost:5817/icon/die.png')
console.log('die.png ->', r.status(), (await r.body()).length)
await b.close()
