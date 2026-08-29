#!/usr/bin/env node
/** 背景の**流れる帯**（空・山・遠くの地面）を、作者の絵から組み立てる（2026-08-29）。
 *
 *  ⭐ **これが唯一の出所。**`assets/ui/paint/home-{sky,hill,far}.png` を直接編集しない
 *  ── 直すなら `assets/ui/home-src/` の作者の絵を差し替えて、この道具をもう一度走らせる。
 *
 *  使い方: node tools/bg-band.mjs
 *
 *  ── 決まり（作者の指示「ほかのものと同様に反転させて連続表示させて」）──────
 *
 *  🔴 **「元・鏡・元・鏡」の4枚を横に並べ、2枚ぶん流して先頭へ戻す。**
 *  ⭐ 継ぎ目が鏡写しなので線が合い、どこにも縫い目が見えない。
 *
 *  ⚠️ **2枚（元・鏡）ではいけない。**旧 `home-sky`/`home-hill` は2枚幅で1枚ぶん
 *  （1080px）流していた ── 流し終わった瞬間に画面は「鏡」を映しており、そこから
 *  「元」へ戻るので、**絵が左右反転してパッと切り替わる**（空は180秒ごと・山は90秒ごと）。
 *  ⭐ 4枚並べて**2枚ぶん**流せば、流し終わりの見え方が流し始めと1画素も違わない
 *  （模様の周期が「元＋鏡」＝2枚ぶんだから）── これで初めて途切れない。
 *
 *  ⚠️ **1枚の幅は画面の半分（540px＝135ドット）以上**でないとこの手は使えない。
 *  4枚のうち、流し終わりに見えているのは3枚目以降なので `幅×(4-2) >= 画面幅` が要る。
 *
 *  ── 表の読み方 ────────────────────────────────────
 *  up     … 作者の絵で「1ドット」が何画素か。⚠️ pixelizer の**画像**書き出しは4倍
 *           （`ImportScreen` が扱う**層**の書き出しは等倍なので、そちらとは別物）
 *  turn   … 反時計回りに何度回すか。⭐ `34_遠くの地面` は縦向きに描かれている
 *  shrink … ドットを何分の1に間引くか。⚠️ 作者の絵は game のドットより細かい
 *           （空は2倍・遠くの地面は5倍細かい）── **最頻色**で間引くので線が濁らない
 *  key    … この色を透明として抜く（作者の絵の白地）
 *  trim   … 上下の透明な行を落とす。⚠️ ドットの升目を壊さないよう `up*shrink` 単位
 */

import { deflateSync, inflateSync } from 'node:zlib'
import { readFileSync, writeFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const HERE = path.dirname(fileURLToPath(import.meta.url))
const ROOT = path.resolve(HERE, '..')
const SRC_DIR = path.join(ROOT, 'assets', 'ui', 'home-src')
const OUT_DIR = path.join(ROOT, 'assets', 'ui', 'paint')

const DOT = 4           // 1ドット = 設計 4px（ドット絵化計画 §2）
const SCREEN = 1080     // 画面の幅（設計px）
const PANELS = 4        // 元・鏡・元・鏡

const BANDS = [
  // ⭐ 空 ── 作者が2026-08-29に描き直したもの。旧 `home-sky`（270x177ドット）と
  //    雲の大きさの比が同じ（最長の雲＝画面幅の30.0%）なので、**画面1枚ぶん**の絵。
  { out: 'home-sky', src: '33_空.png', up: 4, turn: 0, shrink: 2, trim: true,
    top: 0, secs: 180, note: '空' },

  // ⭐ 山 ── 絵は前からのもの。⚠️ 4枚並べに作り直すために、出来上がりの
  //    2枚幅 PNG の左半分を `35_山.png` として取り出した（作者の1枚ぶんの原本が
  //    残っていなかったため・2026-08-29）。
  { out: 'home-hill', src: '35_山.png', up: 1, turn: 0, shrink: 1, trim: false,
    top: 408, secs: 90, note: '山' },

  // ⭐ 遠くの地面 ── 作者が2026-08-29に差し替えた2枚目。⚠️ **1枚目と実測が違う**
  //    ── 1枚目は「1ドット=4px・縦向き・白地は不透明」、2枚目は
  //    「1ドット=8px・すでに横向き・地は最初から透明」。⭐ 実測せず前回の数値を
  //    使い回すと、間引きすぎ／回転が要らないのに回す、で絵が壊れる
  //    （`shrink` の分母は必ずブロック検出で測り直すこと）。
  //    ⚠️ 3分の1に間引く: 2分の1だと手前の `home-grass` の木と同じ大きさになり
  //    「遠く」に見えない（2・3・4分の1を並べて見比べた結果）。
  { out: 'home-far', src: '34_遠くの地面.png', up: 8, turn: 0, shrink: 3, trim: true,
    top: 420, secs: 32, note: '遠くの地面' },
]

// ── PNG を読む ────────────────────────────────────────
// ⚠️ 8bit・RGBA・非インターレースだけ。⭐ それ以外は**黙って壊さず**にここで止める。
function decodePng(buf) {
  const sig = [137, 80, 78, 71, 13, 10, 26, 10]
  for (let i = 0; i < 8; i++) if (buf[i] !== sig[i]) throw new Error('PNG ではない')
  let at = 8, w = 0, h = 0
  const idat = []
  while (at < buf.length) {
    const len = buf.readUInt32BE(at)
    const type = buf.toString('latin1', at + 4, at + 8)
    const body = buf.subarray(at + 8, at + 8 + len)
    if (type === 'IHDR') {
      w = body.readUInt32BE(0); h = body.readUInt32BE(4)
      if (body[8] !== 8) throw new Error(`bit depth ${body[8]}（8 だけ）`)
      if (body[9] !== 6) throw new Error(`color type ${body[9]}（6＝RGBA だけ）`)
      if (body[12] !== 0) throw new Error('インターレースは読めない')
    } else if (type === 'IDAT') idat.push(body)
    else if (type === 'IEND') break
    at += 12 + len
  }
  const raw = inflateSync(Buffer.concat(idat))
  const stride = w * 4
  const out = Buffer.alloc(stride * h)
  // ⚠️ 行ごとのフィルタを解く（PNG の決まり・0〜4 の5種）
  for (let y = 0; y < h; y++) {
    const kind = raw[y * (stride + 1)]
    const line = raw.subarray(y * (stride + 1) + 1, y * (stride + 1) + 1 + stride)
    const cur = out.subarray(y * stride, y * stride + stride)
    const up = y > 0 ? out.subarray((y - 1) * stride, y * stride) : null
    for (let x = 0; x < stride; x++) {
      const a = x >= 4 ? cur[x - 4] : 0
      const b = up ? up[x] : 0
      const c = up && x >= 4 ? up[x - 4] : 0
      let v = line[x]
      if (kind === 1) v += a
      else if (kind === 2) v += b
      else if (kind === 3) v += (a + b) >> 1
      else if (kind === 4) {
        const p = a + b - c
        const pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c)
        v += (pa <= pb && pa <= pc) ? a : (pb <= pc ? b : c)
      } else if (kind !== 0) throw new Error(`知らないフィルタ ${kind}`)
      cur[x] = v & 0xff
    }
  }
  return { w, h, data: out }
}

// ── PNG を書く ────────────────────────────────────────
function crc32(buf) {
  const table = crc32.table ?? (crc32.table = (() => {
    const t = new Uint32Array(256)
    for (let n = 0; n < 256; n++) {
      let c = n
      for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1
      t[n] = c >>> 0
    }
    return t
  })())
  let crc = 0xffffffff
  for (let i = 0; i < buf.length; i++) crc = table[(crc ^ buf[i]) & 0xff] ^ (crc >>> 8)
  return (crc ^ 0xffffffff) >>> 0
}

function chunk(type, data) {
  const len = Buffer.alloc(4); len.writeUInt32BE(data.length, 0)
  const typeAndData = Buffer.concat([Buffer.from(type, 'latin1'), data])
  const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(typeAndData), 0)
  return Buffer.concat([len, typeAndData, crc])
}

function encodePng(rgba, width, height) {
  const sig = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10])
  const ihdr = Buffer.alloc(13)
  ihdr.writeUInt32BE(width, 0); ihdr.writeUInt32BE(height, 4)
  ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0
  const stride = width * 4
  const raw = Buffer.alloc((stride + 1) * height)
  for (let y = 0; y < height; y++) {
    raw[y * (stride + 1)] = 0
    rgba.copy(raw, y * (stride + 1) + 1, y * stride, y * stride + stride)
  }
  return Buffer.concat([sig, chunk('IHDR', ihdr), chunk('IDAT', deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0))])
}

// ── 絵をいじる ────────────────────────────────────────
/** 反時計回りに90°（左端が下端に来る）。⚠️ 90 の倍数だけ。 */
function turnLeft(img, deg) {
  let cur = img
  for (let n = ((deg % 360) + 360) % 360; n > 0; n -= 90) {
    const { w, h, data } = cur
    const out = Buffer.alloc(w * h * 4)
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        const from = (y * w + x) * 4
        const to = ((w - 1 - x) * h + y) * 4   // 新しい幅は h
        data.copy(out, to, from, from + 4)
      }
    }
    cur = { w: h, h: w, data: out }
  }
  return cur
}

/** 指定色（不透明）を透明にする。 */
function keyOut(img, [r, g, b]) {
  const d = img.data
  let hit = 0
  for (let i = 0; i < d.length; i += 4) {
    if (d[i] === r && d[i + 1] === g && d[i + 2] === b && d[i + 3] === 255) {
      d[i] = 0; d[i + 1] = 0; d[i + 2] = 0; d[i + 3] = 0; hit++
    }
  }
  return hit
}

/** 上下の透明な行を落とす。⚠️ `unit` の倍数で切る（ドットの升目を壊さない）。 */
function trimRows(img, unit) {
  const { w, h, data } = img
  const solid = y => {
    for (let x = 0; x < w; x++) if (data[(y * w + x) * 4 + 3] !== 0) return true
    return false
  }
  let top = 0; while (top < h && !solid(top)) top++
  if (top >= h) return { ...img, trimmedTop: 0 }
  let bot = h - 1; while (bot > top && !solid(bot)) bot--
  top = Math.floor(top / unit) * unit
  bot = Math.min(h - 1, Math.ceil((bot + 1) / unit) * unit - 1)
  const nh = bot - top + 1
  return { w, h: nh, data: data.subarray(top * w * 4, (bot + 1) * w * 4), trimmedTop: top }
}

/** `block` 画素四方を1ドットにまとめる。🔴 **平均でなく最頻色**
 *  ── 平均だと中間色が生まれてドット絵の締まりが消える（pixelizer で実証済み）。
 *  ⚠️ 同数のときは**先に出た色**（走査順）── 走らせるたび同じ絵になる。 */
function shrinkMode(img, block) {
  if (block === 1) return img
  const { w, h, data } = img
  if (w % block || h % block) throw new Error(`${w}x${h} は ${block} で割り切れない`)
  const nw = w / block, nh = h / block
  const out = Buffer.alloc(nw * nh * 4)
  for (let by = 0; by < nh; by++) {
    for (let bx = 0; bx < nw; bx++) {
      const seen = new Map()
      for (let y = 0; y < block; y++) {
        for (let x = 0; x < block; x++) {
          const at = ((by * block + y) * w + bx * block + x) * 4
          const key = data[at] * 16777216 + data[at + 1] * 65536 + data[at + 2] * 256 + data[at + 3]
          seen.set(key, (seen.get(key) ?? 0) + 1)
        }
      }
      let best = 0, bestN = -1
      for (const [key, n] of seen) if (n > bestN) { best = key; bestN = n }
      const at = (by * nw + bx) * 4
      out[at] = Math.floor(best / 16777216) & 0xff
      out[at + 1] = Math.floor(best / 65536) & 0xff
      out[at + 2] = Math.floor(best / 256) & 0xff
      out[at + 3] = best & 0xff
      if (out[at + 3] === 0) { out[at] = 0; out[at + 1] = 0; out[at + 2] = 0 }
    }
  }
  return { w: nw, h: nh, data: out }
}

/** 元・鏡・元・鏡 …… を横に並べる。 */
function tile(img, panels) {
  const { w, h, data } = img
  const nw = w * panels
  const out = Buffer.alloc(nw * h * 4)
  for (let p = 0; p < panels; p++) {
    const flip = p % 2 === 1
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        const from = (y * w + (flip ? w - 1 - x : x)) * 4
        const to = (y * nw + p * w + x) * 4
        data.copy(out, to, from, from + 4)
      }
    }
  }
  return { w: nw, h, data: out }
}

// ── 走らせる ──────────────────────────────────────────
const lines = []
for (const band of BANDS) {
  const raw = decodePng(readFileSync(path.join(SRC_DIR, band.src)))
  let img = turnLeft(raw, band.turn)
  const keyed = band.key ? keyOut(img, band.key) : 0
  const unit = band.up * band.shrink
  let trimmed = 0
  if (band.trim) { const t = trimRows(img, unit); trimmed = t.trimmedTop ?? 0; img = t }
  const panel = shrinkMode(img, unit)
  const band4 = tile(panel, PANELS)

  // 🔴 4枚並べが成り立つ条件（doc の「1枚の幅は画面の半分以上」）
  const panelPx = panel.w * DOT
  if (panelPx * (PANELS - 2) < SCREEN)
    throw new Error(`${band.out}: 1枚が ${panelPx}px しかなく、${PANELS}枚では流し切れない`)

  writeFileSync(path.join(OUT_DIR, band.out + '.png'), encodePng(band4.data, band4.w, band4.h))
  const rollPx = panelPx * 2
  console.log(`⭐ ${band.out}.png  ${band4.w}x${band4.h}ドット  `
    + `（1枚 ${panel.w}x${panel.h} × ${PANELS}枚）  ${band.note}`)
  if (keyed) console.log(`   白地を抜いた: ${keyed} 画素`)
  if (trimmed) console.log(`   上を落とした: ${trimmed} 画素`)
  lines.push({ band, panel, band4, rollPx })
}

console.log('\n── assets/layouts/home.txt に書く行 ──')
for (const { band, band4 } of lines) {
  const name = band.out.replace('home-', '') + 'band'
  console.log(`  ${name.padEnd(8)} paint   0    ${String(band.top).padEnd(5)}`
    + `${String(band4.w * DOT).padEnd(5)}${String(band4.h * DOT).padEnd(6)}`
    + `pic=${band.out} roll=${band.out.replace('home-', '')}`)
}
console.log('\n── game/EggCommand.Web/wwwroot/stage.css に書く動き ──')
for (const { band, rollPx } of lines) {
  const key = band.out.replace('home-', '')
  console.log(`  .roll-${key} { animation: bg-roll-${key} ${band.secs}s linear infinite; }`)
  console.log(`  @keyframes bg-roll-${key} { from { transform: translateX(0); }`
    + ` to { transform: translateX(-${rollPx}px); } }   /* ${Math.round(rollPx / band.secs)} px/秒 */`)
}
console.log('\n⚠️ 大きさを変えたら `dotnet run --project game/EggCommand.Sim -- paint-placeholder` で'
  + ' paint-manifest.txt を書き直すこと')
