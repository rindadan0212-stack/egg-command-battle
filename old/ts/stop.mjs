#!/usr/bin/env node
/** 開発サーバーを確実に落とす。
 *
 *  ⚠️ Windows では `npm run dev` を止めても **vite の子プロセスが孤児として残る**
 *  （実際に PID 26140 が 5815 を握ったまま残った）。
 *  親を止めただけで「落とした」と思い込むと、次の起動が
 *  `strictPort` で失敗するか、古いコードを配り続ける。
 *
 *  だからポートを握っている当人を探して落とす。
 */

import { execFileSync } from 'node:child_process'

const PORT = 5815

function sh(cmd, args) {
  try {
    return execFileSync(cmd, args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] })
  } catch {
    return ''
  }
}

/** そのポートを LISTENING で握っている PID を集める。 */
function listeningPids() {
  const out = sh('netstat', ['-ano'])
  const pids = new Set()
  for (const line of out.split(/\r?\n/)) {
    if (!line.includes(`:${PORT}`)) continue
    if (!/LISTENING/i.test(line)) continue
    const pid = line.trim().split(/\s+/).pop()
    if (pid && pid !== '0') pids.add(pid)
  }
  return [...pids]
}

const pids = listeningPids()

if (pids.length === 0) {
  console.log(`ポート ${PORT} は既に解放されている`)
  process.exit(0)
}

for (const pid of pids) {
  sh('taskkill', ['/PID', pid, '/T', '/F'])
  console.log(`PID ${pid} を終了した`)
}

const left = listeningPids()
if (left.length === 0) {
  console.log(`ポート ${PORT} を解放した`)
  process.exit(0)
}

console.error(`まだ ${left.join(', ')} が ${PORT} を握っている`)
process.exit(1)
