/** 実行中の状態を repo へ吐く口。段0 で最初に置くもの（教訓 §5.0-②）。
 *
 *  ⭐ **これ1つで「AI は repo しか見えない / 人は画面しか見ていない」の非対称が消える。**
 *  ゲームが自分の状態を定期的に .live.json へ書き、AI と `npm run same` がそれを読む。
 *
 *  ⚠️ 制約が2つある（どちらも過去に事故った形）:
 *  1. **アセット本体を書かない。指紋と要約だけ。**
 *     実行中のアプリが repo の成果物ファイルを自動で書くと、人の作品を壊す
 *  2. **画面ごとに分けて持つ。**
 *     1つの欄に上書きすると、後から申告したほうが勝ってもう片方が見えなくなる
 *     （ゲームとギャラリーは同時に開いているのが普通）
 */

import { writeFileSync } from 'node:fs'
import { join } from 'node:path'
import type { IncomingMessage, ServerResponse } from 'node:http'
import type { Plugin } from 'vite'

/** 1画面ぶんの申告。クライアントが送ってくる形。 */
export interface ScreenReport {
  /** 'game' | 'gallery' など。⚠️ 画面ごとに欄を分ける鍵になる */
  screen: string
  url: string
  /** そのタブがコードを読み込んだ時刻。サーバ起動時刻と比べて「古いコードを動かしている」が分かる */
  loadedAt: string
  /** アセットの件数と指紋。⚠️ 本体は絶対に書かない */
  assets: { count: number; fingerprint: string }
  /** ブラウザのローカル保存に残っている上書きの件数。0 でなければ repo と食い違っている */
  localOverrides: number
  /** 編集中の下書きが repo と違うか。「作った人にしか見えていない姿」の検出 */
  draftDiffersFromRepo: boolean
  /** 今の場面の要約。⭐ AI が測るべき個体そのものの id をここに入れる */
  scene: Record<string, unknown>
}

interface LiveFile {
  server: { startedAt: string; port: number; mode: string }
  screens: Record<string, ScreenReport & { reportedAt: string }>
}

const MAX_BODY_BYTES = 64 * 1024

function readBody(req: IncomingMessage): Promise<string> {
  return new Promise((resolve, reject) => {
    let size = 0
    const chunks: Buffer[] = []
    req.on('data', (chunk: Buffer) => {
      size += chunk.length
      if (size > MAX_BODY_BYTES) {
        reject(new Error('申告が大きすぎる。アセット本体を入れていないか確認する'))
        req.destroy()
        return
      }
      chunks.push(chunk)
    })
    req.on('end', () => resolve(Buffer.concat(chunks).toString('utf8')))
    req.on('error', reject)
  })
}

export function livePlugin(): Plugin {
  const startedAt = new Date().toISOString() // determinism-ok: 観測用。ゲーム状態には入らない

  return {
    name: 'egg-live-state',
    apply: 'serve',

    configureServer(server) {
      const outPath = join(server.config.root, '.live.json')
      const state: LiveFile = {
        server: {
          startedAt,
          port: server.config.server.port ?? 0,
          mode: server.config.mode,
        },
        screens: {},
      }

      const flush = (): void => {
        writeFileSync(outPath, JSON.stringify(state, null, 2) + '\n', 'utf8')
      }
      flush()

      server.middlewares.use(
        '/__live',
        (req: IncomingMessage, res: ServerResponse, next: () => void) => {
          if (req.method !== 'POST') {
            next()
            return
          }
          readBody(req)
            .then((raw) => {
              const report = JSON.parse(raw) as ScreenReport
              if (typeof report.screen !== 'string' || report.screen === '') {
                throw new Error('screen が無い申告は受け取れない')
              }
              // ⚠️ 画面ごとの欄へ入れる。全体を上書きしない
              state.screens[report.screen] = {
                ...report,
                reportedAt: new Date().toISOString(), // determinism-ok: 観測用
              }
              flush()
              res.statusCode = 204
              res.end()
            })
            .catch((error: unknown) => {
              res.statusCode = 400
              res.end(String(error))
            })
        },
      )
    },
  }
}
