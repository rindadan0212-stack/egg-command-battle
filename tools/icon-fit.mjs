// 盤・帯の小物の絵（icon）を「枠の大きさ」に焼き直す道具。
//
// ⭐ **唯一の出所は `assets/ui/icon-src/`**（作者の原画・128x128）。
//    ここが吐いた物が `assets/ui/icon/` に入り、遊びが表示する。
//    ⚠️ `assets/ui/home-src/` → `tools/bg-band.mjs` → `assets/ui/paint/` と同じ形。
//
// 🔴 **なぜ焼き直すのか**（2026-08-29・作者の指示「①PNGを枠の大きさに描き直す」）:
//    小物の絵は全部 128x128px（＝32ドット＝設計128px）で描かれているのに、
//    骨組みの枠は 36〜96px しか無い。「絵は引き伸ばさない」規則があるので、
//    128px のまま中央に置かれ、**隣とぶつかって読めなくなっていた**
//    （実測: すごろくのさいころ12個は枠46px間隔に128pxが並び、白い塊に見えていた）。
//
// ⚠️ **この16枚はドット絵ではない**（実測・ブロック検出=1 ＝ 滑らかな絵）。
//    だから縮めるときは「最頻色」ではなく **面積平均**を使う
//    （`bg-band.mjs` の `shrinkMode` はドット絵用 ── ここでは濁るので使わない）。
//    ⭐ 透明の縁が黒く滲まないよう、**alpha を掛けてから**平均する（premultiplied）。
//
// ⚠️ 1枚の絵が違う大きさの枠で使われている所は、**大きさごとに別ファイル**を焼く
//    （`arrow` → `arrow` と `arrow-big`）。引き伸ばさない規則の下では、
//    1つの絵は1つの大きさしか持てない。
//
// 使い方: `node tools/icon-fit.mjs`（または `npm run icons`）

import { readFileSync, writeFileSync } from 'node:fs'
import { inflateSync, deflateSync } from 'node:zlib'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const HERE = path.dirname(fileURLToPath(import.meta.url))
const ROOT = path.resolve(HERE, '..')
const SRC_DIR = path.join(ROOT, 'assets', 'ui', 'icon-src')
const OUT_DIR = path.join(ROOT, 'assets', 'ui', 'icon')

const DOT = 4   // 1ドット = 設計 4px

// ⭐ 焼く物の一覧。`dots` は **使われている枠の実寸 ÷ 4**。
//    ⚠️ 枠の側もこの数に合わせて直してある（`assets/layouts/square.txt` /
//    `trail.txt` ── 直した行にはその旨を書いた）。
const ICONS = [
  // 矢印 ── ⚠️ 3つの枠で使われていたが、`square` の52は数字に被るので36へ寄せた。
  //         `skip` の釦だけは大きいままにしたいので、別ファイルを焼く。
  { src: 'arrow', out: 'arrow', dots: 9, note: 'trail/payarr・square/arrow（枠36）' },
  { src: 'arrow', out: 'arrow-big', dots: 15, note: 'trail/skippic（枠60）' },

  // さいころ ── ⚠️ 12個並ぶ帯（枠46→44）と、振る釦の大きい1個（枠88）で別物。
  { src: 'die', out: 'die', dots: 11, note: 'trail/die 残り数の帯（枠44）' },
  { src: 'die', out: 'die-big', dots: 22, note: 'trail/rollpic 振る釦（枠88）' },
  { src: 'die-spent', out: 'die-spent', dots: 11, note: 'trail/die 使い切った側（枠44）' },

  { src: 'goal', out: 'goal', dots: 10, note: 'trail/goalpic（枠40）' },
  { src: 'plain', out: 'plain', dots: 13, note: 'square/plain・関門の既定（枠52）' },
  { src: 'stat-atk', out: 'stat-atk', dots: 13, note: 'square/stat・gstat・paypic・purse（枠52）' },
  { src: 'stat-def', out: 'stat-def', dots: 13, note: '同上' },
  { src: 'stat-hp', out: 'stat-hp', dots: 13, note: '同上' },
  { src: 'mob', out: 'mob', dots: 24, note: 'square/mob（枠96・元から合っている）' },
]

// ── PNG を読む ────────────────────────────────────────
// ⚠️ `bg-band.mjs` の読み手は RGBA（色の型6）だけ。小物の絵は**索引色（型3）**なので
//    ここは PLTE / tRNS も解く。⭐ 出す側は常に RGBA に揃える（縮めると中間色が出る）。
function decodePng(buf) {
  const sig = [137, 80, 78, 71, 13, 10, 26, 10]
  for (let i = 0; i < 8; i++) if (buf[i] !== sig[i]) throw new Error('PNG ではない')
  let at = 8, w = 0, h = 0, type = -1
  let plte = null, trns = null
  const idat = []
  while (at < buf.length) {
    const len = buf.readUInt32BE(at)
    const kind = buf.toString('latin1', at + 4, at + 8)
    const body = buf.subarray(at + 8, at + 8 + len)
    if (kind === 'IHDR') {
      w = body.readUInt32BE(0); h = body.readUInt32BE(4)
      if (body[8] !== 8) throw new Error(`bit depth ${body[8]}（8 だけ）`)
      type = body[9]
      if (type !== 3 && type !== 6 && type !== 2) throw new Error(`color type ${type}（2/3/6 だけ）`)
      if (body[12] !== 0) throw new Error('インターレースは読めない')
    } else if (kind === 'PLTE') plte = Buffer.from(body)
    else if (kind === 'tRNS') trns = Buffer.from(body)
    else if (kind === 'IDAT') idat.push(Buffer.from(body))
    else if (kind === 'IEND') break
    at += 12 + len
  }
  const bpp = type === 3 ? 1 : (type === 2 ? 3 : 4)
  const stride = w * bpp
  const raw = inflateSync(Buffer.concat(idat))
  const flat = Buffer.alloc(stride * h)
  for (let y = 0; y < h; y++) {
    const filter = raw[y * (stride + 1)]
    const line = raw.subarray(y * (stride + 1) + 1, y * (stride + 1) + 1 + stride)
    const cur = flat.subarray(y * stride, y * stride + stride)
    const up = y > 0 ? flat.subarray((y - 1) * stride, y * stride) : null
    for (let x = 0; x < stride; x++) {
      const a = x >= bpp ? cur[x - bpp] : 0
      const b = up ? up[x] : 0
      const c = up && x >= bpp ? up[x - bpp] : 0
      let v = line[x]
      if (filter === 1) v += a
      else if (filter === 2) v += b
      else if (filter === 3) v += (a + b) >> 1
      else if (filter === 4) {
        const p = a + b - c
        const pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c)
        v += (pa <= pb && pa <= pc) ? a : (pb <= pc ? b : c)
      } else if (filter !== 0) throw new Error(`知らないフィルタ ${filter}`)
      cur[x] = v & 0xff
    }
  }
  // ⭐ どの型で来ても RGBA に開く（以降の処理を1本にする）
  const data = Buffer.alloc(w * h * 4)
  for (let i = 0; i < w * h; i++) {
    if (type === 3) {
      const idx = flat[i]
      data[i * 4] = plte[idx * 3]
      data[i * 4 + 1] = plte[idx * 3 + 1]
      data[i * 4 + 2] = plte[idx * 3 + 2]
      data[i * 4 + 3] = trns && idx < trns.length ? trns[idx] : 255
    } else if (type === 2) {
      data[i * 4] = flat[i * 3]
      data[i * 4 + 1] = flat[i * 3 + 1]
      data[i * 4 + 2] = flat[i * 3 + 2]
      data[i * 4 + 3] = 255
    } else {
      data[i * 4] = flat[i * 4]
      data[i * 4 + 1] = flat[i * 4 + 1]
      data[i * 4 + 2] = flat[i * 4 + 2]
      data[i * 4 + 3] = flat[i * 4 + 3]
    }
  }
  return { w, h, data }
}

// ── PNG を書く ────────────────────────────────────────
function crc32(buf) {
  const table = crc32.table ?? (crc32.table = (() => {
    const t = new Int32Array(256)
    for (let n = 0; n < 256; n++) {
      let c = n
      for (let k = 0; k < 8; k++) c = (c & 1) ? (0xedb88320 ^ (c >>> 1)) : (c >>> 1)
      t[n] = c
    }
    return t
  })())
  let c = -1
  for (let i = 0; i < buf.length; i++) c = table[(c ^ buf[i]) & 0xff] ^ (c >>> 8)
  return (c ^ -1) >>> 0
}

function chunk(type, data) {
  const head = Buffer.alloc(4)
  head.writeUInt32BE(data.length, 0)
  const body = Buffer.concat([Buffer.from(type, 'latin1'), data])
  const tail = Buffer.alloc(4)
  tail.writeUInt32BE(crc32(body), 0)
  return Buffer.concat([head, body, tail])
}

function encodePng(rgba, width, height) {
  const stride = width * 4
  const raw = Buffer.alloc((stride + 1) * height)
  for (let y = 0; y < height; y++) {
    raw[y * (stride + 1)] = 0   // ⚠️ フィルタ無し（小さい絵なので圧縮より読みやすさ）
    rgba.copy(raw, y * (stride + 1) + 1, y * stride, y * stride + stride)
  }
  const ihdr = Buffer.alloc(13)
  ihdr.writeUInt32BE(width, 0)
  ihdr.writeUInt32BE(height, 4)
  ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0
  return Buffer.concat([
    Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
    chunk('IHDR', ihdr),
    chunk('IDAT', deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ])
}

// ── 面積平均で縮める ──────────────────────────────────
// ⚠️ **alpha を掛けてから平均する。**⭐ 掛けずに色だけ平均すると、透明な画素が持つ
//    「見えない色」（多くは黒や白）が縁に混ざり、輪郭に暗い/明るい線が出る。
function resize(img, outW, outH) {
  const out = Buffer.alloc(outW * outH * 4)
  const sx = img.w / outW, sy = img.h / outH
  for (let y = 0; y < outH; y++) {
    const y0 = y * sy, y1 = (y + 1) * sy
    for (let x = 0; x < outW; x++) {
      const x0 = x * sx, x1 = (x + 1) * sx
      let r = 0, g = 0, b = 0, a = 0, wsum = 0
      for (let py = Math.floor(y0); py < Math.ceil(y1); py++) {
        const cy = Math.min(y1, py + 1) - Math.max(y0, py)
        if (cy <= 0) continue
        for (let px = Math.floor(x0); px < Math.ceil(x1); px++) {
          const cx = Math.min(x1, px + 1) - Math.max(x0, px)
          if (cx <= 0) continue
          const wgt = cx * cy
          const o = (py * img.w + px) * 4
          const al = img.data[o + 3] / 255
          r += img.data[o] * al * wgt
          g += img.data[o + 1] * al * wgt
          b += img.data[o + 2] * al * wgt
          a += img.data[o + 3] * wgt
          wsum += wgt
        }
      }
      const o = (y * outW + x) * 4
      if (wsum <= 0) continue
      const alpha = a / wsum
      // ⭐ 掛けた分を戻す（premultiplied → straight）
      const un = alpha > 0 ? (255 / alpha) : 0
      out[o] = Math.min(255, Math.round((r / wsum) * un))
      out[o + 1] = Math.min(255, Math.round((g / wsum) * un))
      out[o + 2] = Math.min(255, Math.round((b / wsum) * un))
      out[o + 3] = Math.round(alpha)
    }
  }
  return { w: outW, h: outH, data: out }
}

// ── 走る ──────────────────────────────────────────────
let made = 0
for (const it of ICONS) {
  const src = decodePng(readFileSync(path.join(SRC_DIR, it.src + '.png')))
  const side = it.dots * DOT
  const small = resize(src, side, side)
  writeFileSync(path.join(OUT_DIR, it.out + '.png'), encodePng(small.data, side, side))
  console.log(`■ ${it.out}.png  ${src.w}x${src.h} → ${side}x${side}（${it.dots}ドット） ${it.note}`)
  made++
}
console.log(`\n${made}枚 焼いた。⚠️ このあと目録を作り直すこと:`)
console.log('  dotnet run --project game/EggCommand.Sim -- icon-manifest')
