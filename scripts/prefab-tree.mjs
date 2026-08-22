#!/usr/bin/env node
/** Prefab の実座標を読む。⚠️ **記憶で書かない**ための道具。
 *
 *  ⭐ 骨組み（`Layouts/*.txt`）へ移すとき、数は**実物の Prefab から**取る。
 *  ⚠️ 生成器（`BuildScreenPrefabs.cs`）とは既に食い違っている
 *     （実測: 生成器 `228f,200f` / 実物 `224,200`）ので、実物のほうを読む。
 *
 *  使い方: node scripts/prefab-tree.mjs BoxScreen [--all]
 *    --all を付けなければ、隠れている部品（m_IsActive: 0）も印付きで出す。
 */

import { readFileSync } from 'fs'
import { fileURLToPath } from 'url'
import { dirname, join } from 'path'

const here = dirname(fileURLToPath(import.meta.url))
const root = join(here, '..', 'unity', 'Assets', 'Resources', 'Prefabs')

const name = process.argv[2]
if (!name) { console.error('使い方: node scripts/prefab-tree.mjs <PrefabName>'); process.exit(2) }

const text = readFileSync(join(root, name + '.prefab'), 'utf8')

/** `--- !u!1 &123` で切る。⭐ Unity の YAML は1文書=1オブジェクト。 */
const docs = []
for (const chunk of text.split(/^--- /m).slice(1)) {
  const head = chunk.match(/!u!(\d+) &(\d+)/)
  if (head) docs.push({ cls: +head[1], id: head[2], body: chunk })
}

const val = (body, key) => {
  const m = body.match(new RegExp('^\\s*' + key + ':\\s*(.*)$', 'm'))
  return m ? m[1].trim() : null
}
const pair = (body, key) => {
  const raw = val(body, key)
  if (!raw) return null
  const m = raw.match(/x:\s*(-?[\d.]+),\s*y:\s*(-?[\d.]+)/)
  return m ? { x: +m[1], y: +m[2] } : null
}
const ref = (body, key) => {
  const raw = val(body, key)
  const m = raw && raw.match(/fileID:\s*(\d+)/)
  return m && m[1] !== '0' ? m[1] : null
}

const objects = new Map()   // GameObject id → { name, active, comps:[] }
const rects = new Map()     // RectTransform id → geometry

for (const d of docs) {
  if (d.cls === 1) {
    objects.set(d.id, {
      name: val(d.body, 'm_Name') || '',
      active: val(d.body, 'm_IsActive') !== '0',
      kinds: [...d.body.matchAll(/component:\s*\{fileID:\s*(\d+)\}/g)].map(m => m[1]),
    })
  } else if (d.cls === 224) {   // RectTransform
    rects.set(d.id, {
      go: ref(d.body, 'm_GameObject'),
      father: ref(d.body, 'm_Father'),
      children: [...(val(d.body, 'm_Children') ? d.body.match(/m_Children:[\s\S]*?(?=\n  m_Father)/)[0].matchAll(/fileID:\s*(\d+)/g) : [])].map(m => m[1]),
      pos: pair(d.body, 'm_AnchoredPosition'),
      size: pair(d.body, 'm_SizeDelta'),
      aMin: pair(d.body, 'm_AnchorMin'),
      aMax: pair(d.body, 'm_AnchorMax'),
      pivot: pair(d.body, 'm_Pivot'),
    })
  }
}

/** どんな部品が付いているか。⭐ 種類（label/button/…）を当てるのに使う。 */
const classOf = new Map()
for (const d of docs) classOf.set(d.id, d.cls)
const KIND = { 114: 'script', 222: 'canvas', 223: 'canvas', 5: '?' }
const named = new Map([['UnityEngine.UI.Text', 'label'], ['UnityEngine.UI.Image', 'image'],
  ['UnityEngine.UI.Button', 'button']])

/** MonoBehaviour の型名は guid でしか分からないので、テキストから推測する。 */
const compKind = id => {
  const d = docs.find(x => x.id === id)
  if (!d) return null
  if (d.cls === 222) return 'text'          // CanvasRenderer 付随
  if (d.cls === 114) {
    if (/m_FontData|m_LineSpacing/.test(d.body)) return 'label'
    if (/m_OnClick/.test(d.body)) return 'button'
    if (/m_Sprite:|m_PreserveAspect/.test(d.body)) return 'image'
    if (/m_Content:|m_Horizontal:/.test(d.body)) return 'scroll'
    if (/m_CellSize/.test(d.body)) return 'grid'
    return 'script'
  }
  return null
}

const roots = [...rects.entries()].filter(([, r]) => !r.father).map(([id]) => id)

const say = (id, depth) => {
  const r = rects.get(id)
  const go = objects.get(r.go) || { name: '?', kinds: [] }
  const kinds = [...new Set(go.kinds.map(compKind).filter(Boolean))]
  const p = r.pos || { x: 0, y: 0 }, s = r.size || { x: 0, y: 0 }
  // ⭐ **字の大きさと寄せは実物から読む。**⚠️ 記憶で 26pt と書いて誤警報を出した
  //    ことがある（実物は 20pt・2026-08-22）。
  let type = ''
  for (const c of go.kinds) {
    const d = docs.find(x => x.id === c)
    if (!d || d.cls !== 114) continue
    const fs = val(d.body, 'm_FontSize'), al = val(d.body, 'm_Alignment')
    const col = d.body.match(/m_Color:\s*\{r:\s*([\d.]+),\s*g:\s*([\d.]+),\s*b:\s*([\d.]+),\s*a:\s*([\d.]+)/)
    if (fs) type += ` ${fs}pt 寄=${al}`
    if (col) type += ` 色=${['r', 'g', 'b'].map((_, i) => Math.round(+col[i + 1] * 255)
      .toString(16).padStart(2, '0')).join('')}/a${(+col[4]).toFixed(2)}`
  }
  const stretch = r.aMin && r.aMax && (r.aMin.x !== r.aMax.x || r.aMin.y !== r.aMax.y)
  const flags = [
    go.active ? '' : '⚠️隠',
    stretch ? `伸(${r.aMin.x},${r.aMin.y}→${r.aMax.x},${r.aMax.y})` : '',
  ].filter(Boolean).join(' ')
  console.log('  '.repeat(depth)
    + (go.name || '(無名)').padEnd(22 - depth * 2)
    + `${String(p.x).padStart(7)},${String(-p.y).padStart(7)}`
    + `  ${String(s.x).padStart(6)}x${String(s.y).padStart(5)}`
    + `  ${kinds.join('+').padEnd(14)} ${flags}${type}`)
  for (const c of r.children) if (rects.has(c)) say(c, depth + 1)
}

console.log(`${name}.prefab ── 名前 / 左,上 / 幅x高 / 部品`)
for (const r of roots) say(r, 0)
