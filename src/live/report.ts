/** 実行中の状態を .live.json へ申告する側（教訓 §5.0-②）。
 *
 *  ⚠️ **アセット本体を送らない。指紋と要約だけ。**
 *  ⚠️ 開発時のみ動く。本番ビルドでは何もしない。
 */

/** そのタブがコードを読み込んだ時刻。サーバ起動時刻と比べて
 *  「古いコードを動かしている」が分かる。今回いちばん効く1行。 */
const LOADED_AT = new Date().toISOString() // determinism-ok: 観測用。ゲーム状態には入らない

const INTERVAL_MS = 3000

export interface LiveSource {
  /** アセットの件数と指紋。⚠️ 本体は入れない */
  assets: () => { count: number; fingerprint: string }
  /** ローカル保存に残っている上書きの件数。0 でなければ repo と食い違っている */
  localOverrides: () => number
  /** 編集中の下書きが repo と違うか。「作った人にしか見えていない姿」の検出 */
  draftDiffersFromRepo: () => boolean
  /** 今の場面の要約。⭐ AI が測るべき個体そのものの id をここに入れる */
  scene: () => Record<string, unknown>
}

function snapshot(screen: string, source: LiveSource): string {
  return JSON.stringify({
    screen,
    url: location.href,
    loadedAt: LOADED_AT,
    assets: source.assets(),
    localOverrides: source.localOverrides(),
    draftDiffersFromRepo: source.draftDiffersFromRepo(),
    scene: source.scene(),
  })
}

/** 申告を始める。
 *
 *  ⚠️ **中身が変わらなくても毎回送る。**
 *  「変わったときだけ送る」にすると通信は減るが、
 *  **「変化が無い」と「タブが死んだ」が区別できなくなる**。
 *  実測: 保管庫の画面は内容が変わらないので一度も申告せず、
 *  生きているタブが `npm run same` で「閉じている」と報告された。
 *  生存確認のほうが、書き込み回数を惜しむより価値がある。
 */
export function startLiveReporting(screen: string, source: LiveSource): void {
  if (!import.meta.env.DEV) return

  const send = (): void => {
    let body: string
    try {
      body = snapshot(screen, source)
    } catch (error) {
      console.warn('[live] 申告を作れなかった', error)
      return
    }
    void fetch('/__live', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body,
      keepalive: true,
    }).catch(() => {
      // dev server が落ちているだけ。ゲームを止める理由にはしない
    })
  }

  send()
  setInterval(send, INTERVAL_MS)
  document.addEventListener('visibilitychange', send)
  attachLifeline()
}

/** サーバーに「この画面はまだ開いている」を接続そのもので示す。
 *
 *  ⭐ **心拍では代用できない。** ブラウザは背面タブのタイマーを絞る・凍らせるので、
 *  「一定時間申告が無い＝閉じた」は**開いているタブを殺す**。
 *  接続なら、タブを閉じてもブラウザが落ちても切れるが、背面に回っただけでは切れない。
 *
 *  切れた側（サーバー）が、.bat から起動されたときだけ自分を終了する。
 *  普通の `npm run dev` では何も起きない。
 */
function attachLifeline(): void {
  const source = new EventSource('/__live/attach')
  // 切断は EventSource が自分で繋ぎ直す。ここで騒がない
  source.onerror = () => {}
}

/** まだ中身が無い段でも申告できるようにする既定値。段A 以降で実物に差し替える。 */
export const EMPTY_SOURCE: LiveSource = {
  assets: () => ({ count: 0, fingerprint: '000000' }),
  localOverrides: () => 0,
  draftDiffersFromRepo: () => false,
  scene: () => ({}),
}
