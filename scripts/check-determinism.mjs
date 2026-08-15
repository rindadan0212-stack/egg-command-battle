#!/usr/bin/env node
/** 決定論を壊す書き方が紛れ込んでいないかを見る。
 *
 *  同じ種から同じ結果が出ることが崩れていると、どんな観測の仕組みを足しても
 *  「たまたま違う」を排除できない。だから機械で止める。
 *
 *  例外を置きたい行には、末尾に理由付きで書く:
 *    const now = Date.now() // determinism-ok: 表示専用。ゲーム状態には入れない
 */

import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('..', import.meta.url))
const SCAN_DIRS = ['src', 'vite-plugins']
const EXTENSIONS = ['.ts', '.tsx', '.js', '.mjs']
const EXEMPT = /\/\/\s*determinism-ok:/

const RULES = [
  {
    pattern: /\bMath\.random\s*\(/,
    message: 'Math.random() は使わない。src/core/rng.ts の Rng を通す',
  },
  {
    pattern: /\bDate\.now\s*\(/,
    message: 'Date.now() はゲーム状態に入れない。表示専用なら determinism-ok を付ける',
  },
  {
    pattern: /\bnew\s+Date\s*\(\s*\)/,
    message: '引数なしの new Date() は同上',
  },
  {
    pattern: /\bperformance\.now\s*\(/,
    message: 'performance.now() で挙動を変えない。描画専用なら determinism-ok を付ける',
  },
]

/** @param {string} dir @returns {string[]} */
function walk(dir) {
  /** @type {string[]} */
  const found = []
  let entries
  try {
    entries = readdirSync(dir)
  } catch {
    return found
  }
  for (const entry of entries) {
    const full = join(dir, entry)
    if (statSync(full).isDirectory()) {
      found.push(...walk(full))
    } else if (EXTENSIONS.some((ext) => entry.endsWith(ext))) {
      found.push(full)
    }
  }
  return found
}

/** コメントを取り除いた行を返す。
 *  ⚠️ これが無いと「Math.random() は使わない」と書いた注意書き自体を検出してしまう。
 *  @param {string[]} lines @returns {string[]} */
function stripComments(lines) {
  let inBlock = false
  return lines.map((line) => {
    let out = ''
    for (let i = 0; i < line.length; i++) {
      if (inBlock) {
        if (line.startsWith('*/', i)) {
          inBlock = false
          i++
        }
        continue
      }
      if (line.startsWith('/*', i)) {
        inBlock = true
        i++
        continue
      }
      if (line.startsWith('//', i)) break
      out += line[i]
    }
    return out
  })
}

const violations = []

for (const dir of SCAN_DIRS) {
  for (const file of walk(join(ROOT, dir))) {
    const raw = readFileSync(file, 'utf8').split(/\r?\n/)
    const code = stripComments(raw)
    code.forEach((line, index) => {
      if (EXEMPT.test(raw[index])) return
      for (const rule of RULES) {
        if (rule.pattern.test(line)) {
          violations.push({
            file: relative(ROOT, file).replaceAll('\\', '/'),
            line: index + 1,
            message: rule.message,
            source: raw[index].trim(),
          })
        }
      }
    })
  }
}

const real = violations

if (real.length === 0) {
  console.log('決定論チェック: 問題なし')
  process.exit(0)
}

console.error(`決定論チェック: ${real.length} 件`)
for (const v of real) {
  console.error(`  ${v.file}:${v.line}  ${v.message}`)
  console.error(`    ${v.source}`)
}
process.exit(1)
