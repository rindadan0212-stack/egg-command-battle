/** 実行中の状態を repo へ吐く口 + ブラウザを閉じたらサーバーも終わる仕組み。
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
  server: { startedAt: string; port: number; mode: string; autoClose: boolean }
  screens: Record<string, ScreenReport & { reportedAt: string }>
}

const MAX_BODY_BYTES = 64 * 1024

/** 最後の画面が消えてから終了するまでの猶予。
 *  ⚠️ 再読込・画面間の移動でも接続はいったん切れるので、待たずに落とすと誤爆する。 */
const GRACE_MS = 5000

/** 起動したのに1つも画面が繋がらないまま経ったら終了する。
 *  （.bat で開いたがブラウザが立ち上がらなかった場合に居座らせない） */
const STARTUP_WAIT_MS = 60_000

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
  // ⚠️ 既定では切らない。`npm run dev` は開発中に居座ってよい。
  //    .bat から開いたときだけ ECB_AUTOCLOSE=1 が入る。
  const autoClose = process.env['ECB_AUTOCLOSE'] === '1'

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
          autoClose,
        },
        screens: {},
      }

      const flush = (): void => {
        writeFileSync(outPath, JSON.stringify(state, null, 2) + '\n', 'utf8')
      }
      flush()

      // ── 画面の申告を受ける ─────────────────────────
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

      // ── ブラウザが閉じたらサーバーも終わる ──────────
      //
      // ⭐ **心拍のタイムアウトでは判定しない。**
      // ブラウザは背面タブのタイマーを絞る・凍らせるので、
      // 「一定時間申告が無い＝閉じた」は**開いているタブを殺す**（実際に踏んだ罠）。
      //
      // 代わりに接続そのものを見る。タブを閉じてもブラウザが落ちても TCP は切れるが、
      // 背面に回っただけでは切れない。
      const attached = new Set<ServerResponse>()
      let shutdownTimer: NodeJS.Timeout | null = null

      const cancelShutdown = (): void => {
        if (shutdownTimer) {
          clearTimeout(shutdownTimer)
          shutdownTimer = null
        }
      }

      const scheduleShutdown = (): void => {
        if (!autoClose || shutdownTimer) return
        shutdownTimer = setTimeout(() => {
          shutdownTimer = null
          if (attached.size > 0) return // 再読込などで戻ってきた
          server.config.logger.info('\nブラウザが閉じられました。サーバーを終了します。')
          void server.close().finally(() => process.exit(0))
        }, GRACE_MS)
      }

      server.middlewares.use('/__live/attach', (req: IncomingMessage, res: ServerResponse) => {
        res.writeHead(200, {
          'content-type': 'text/event-stream',
          'cache-control': 'no-cache',
          connection: 'keep-alive',
        })
        // 切れたら1秒後に繋ぎ直す（再読込を跨ぐため）
        res.write('retry: 1000\n\n')

        attached.add(res)
        cancelShutdown()

        const drop = (): void => {
          if (!attached.delete(res)) return
          if (attached.size === 0) scheduleShutdown()
        }
        req.on('close', drop)
        res.on('close', drop)
      })

      if (autoClose) {
        setTimeout(() => {
          if (attached.size > 0) return
          server.config.logger.info('\n画面が開かれませんでした。サーバーを終了します。')
          void server.close().finally(() => process.exit(0))
        }, STARTUP_WAIT_MS)
      }
    },
  }
}
