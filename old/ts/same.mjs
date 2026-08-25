#!/usr/bin/env node
/** 「同じものを見ているか」を1コマンドで突き合わせる（教訓 §5.0-③）。
 *
 *  ⭐ **見た目の話をする前に必ず打つ。**
 *  揃っていないなら、どちらの観察も正しく、話は永遠に噛み合わない。
 *
 *  AI は repo（ファイル）しか見えない。人は画面しか見ていない。
 *  この非対称は、放っておくと必ず事故になる。突き合わせるのは機械の仕事。
 */

import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('.', import.meta.url))
const LIVE_PATH = join(ROOT, '.live.json')

/** 申告が届かなくなってからこれを超えたら、そのタブは閉じたとみなす */
const STALE_MS = 15_000

const lines = []
let bad = 0

const ok = (text) => lines.push(`  ✓ ${text}`)
const ng = (text, fix) => {
  bad++
  lines.push(`  ✗ ${text}${fix ? `\n      → ${fix}` : ''}`)
}

function git(args) {
  try {
    // stderr を捨てる（コミットが1つも無いときの fatal をそのまま出さない）
    return execFileSync('git', args, {
      cwd: ROOT,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'ignore'],
    }).trim()
  } catch {
    return null
  }
}

// ── repo 側 ────────────────────────────────────────────────
lines.push('── repo')

const status = git(['status', '--porcelain'])
if (status === null) {
  ng('git リポジトリではない', 'git init する')
} else if (status === '') {
  ok('未コミットの変更なし')
} else {
  const count = status.split('\n').length
  ng(
    `未コミットの変更 ${count}件 — AI だけが見ている状態がある`,
    'git status で中身を見て、意図した変更かを確かめる',
  )
}

const head = git(['log', '-1', '--format=%h %s'])
if (head) lines.push(`    HEAD ${head}`)

// ── 実行中 ────────────────────────────────────────────────
let live = null
try {
  live = JSON.parse(readFileSync(LIVE_PATH, 'utf8'))
} catch {
  lines.push('')
  lines.push('── 実行中')
  ng('.live.json が無い', 'npm run dev を起動して、ブラウザでページを開く')
}

if (live) {
  const startedAt = new Date(live.server.startedAt).getTime() // determinism-ok: 観測用
  const now = Date.now() // determinism-ok: 観測用

  const port = live.server.port
  let serverUp = false
  try {
    await fetch(`http://localhost:${port}/`, { signal: AbortSignal.timeout(1500) })
    serverUp = true
  } catch {
    serverUp = false
  }

  lines.push('')
  lines.push(`── dev server  http://localhost:${port}/`)
  if (serverUp) {
    ok(`起動 ${live.server.startedAt}（${live.server.mode}）`)
  } else {
    ng(
      `応答しない（.live.json は ${live.server.startedAt} 起動と言っている）`,
      'npm run dev を起動し直す。古いプロセスが残っていないかも見る',
    )
  }

  const screens = Object.entries(live.screens ?? {})
  if (screens.length === 0) {
    lines.push('')
    ng('どの画面もまだ申告していない', 'ブラウザでページを開く')
  }

  for (const [name, s] of screens) {
    lines.push('')
    lines.push(`── ${name}  ${s.url}`)

    const loadedAt = new Date(s.loadedAt).getTime() // determinism-ok: 観測用
    const reportedAt = new Date(s.reportedAt).getTime() // determinism-ok: 観測用
    const age = now - reportedAt

    if (age > STALE_MS) {
      ng(
        `${Math.round(age / 1000)}秒 申告が無い — このタブは閉じている可能性がある`,
        'この画面の話をするなら、開き直してから測る',
      )
    } else if (loadedAt < startedAt) {
      ng(
        '古いコードを動かしている（dev server の起動より前に読み込まれたタブ）',
        'そのタブを再読込する',
      )
    } else {
      ok('タブは今の dev server で読み込まれている')
    }

    if (s.assets.count === 0) {
      lines.push(`    意匠 0件`)
    } else {
      ok(`意匠 ${s.assets.count}件 · 指紋 #${s.assets.fingerprint}`)
    }

    if (s.localOverrides > 0) {
      ng(
        `ローカル保存に上書きが ${s.localOverrides}件 — repo と食い違っている`,
        'その画面から書き出して repo へ寄せる',
      )
    }
    if (s.draftDiffersFromRepo) {
      ng(
        '編集中の下書きが repo と違う — まだゲームに出ていない',
        '保存を押す。押すまでは AI からは見えていない',
      )
    }

    const scene = Object.entries(s.scene ?? {})
    if (scene.length > 0) {
      const summary = scene
        .map(([k, v]) => `${k}=${Array.isArray(v) ? v.join(',') : String(v)}`)
        .join(' / ')
      lines.push(`    場面 ${summary}`)
    }
  }
}

console.log(lines.join('\n'))
console.log('')
if (bad === 0) {
  console.log('揃っている。見た目の話を進めてよい。')
  process.exit(0)
}
console.log(`${bad}件 食い違っている。ここを揃えるまで、見た目の議論は噛み合わない。`)
process.exit(1)
