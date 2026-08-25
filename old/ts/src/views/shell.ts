/** 画面の器。⭐ **モックの骨格そのもの。**
 *
 *  ```
 *  端末（390 幅・太枠・角丸・ぼかし無しの影）
 *    ├ 上段  ‹戻る / タイトル / 右の状態ピル
 *    ├ 本体  画面ごとの中身（ここだけ縦に伸びる）
 *    └ 下段  4パネル（ホームだけに出る）
 *  ```
 *
 *  ⚠️ **ホームがハブ。**各画面は ‹ でホームへ戻る。
 *  常時タブではない（モックがそうなっている）。
 */

/** 画面ごとの地の色。⭐ どの画面にいるかが色で分かる。 */
export type Sky = 'home' | 'nest' | 'battle' | 'hatch' | 'breed' | 'box'

export interface TopBar {
  /** 押すとホームへ戻る ‹ を出すか */
  readonly back?: (() => void) | undefined
  readonly title?: string | undefined
  /** 右肩の状態ピル。数えられる事実だけを置く */
  readonly badge?: string | undefined
  /** ⭐ 画面が自分の中でスクロール層を持つとき true。
   *  ⚠️ 器と画面の**二重スクロールを避ける**ための指定。 */
  readonly layered?: boolean
}

export interface Frame {
  readonly element: HTMLElement
  /** 画面の中身を入れる場所 */
  readonly screen: HTMLElement
  /** ⚠️ 巣は中で場面が変わる（一覧 → 発射 → 戦闘）。上段の見出しも一緒に変える */
  setTitle(text: string): void
}

export function buildFrame(sky: Sky, top: TopBar): Frame {
  const element = document.createElement('div')
  element.className = 'phone'
  element.dataset['sky'] = sky

  const bar = document.createElement('header')
  bar.className = 'topbar'

  const left = document.createElement('div')
  left.className = 'slot'
  if (top.back) {
    const back = document.createElement('button')
    back.type = 'button'
    back.className = 'back'
    back.textContent = '‹'
    back.setAttribute('aria-label', 'ホームへ戻る')
    back.addEventListener('click', top.back)
    left.append(back)
  }

  const title = document.createElement('div')
  title.className = 'title'
  title.textContent = top.title ?? ''

  const right = document.createElement('div')
  right.className = 'slot end'
  if (top.badge) {
    const badge = document.createElement('span')
    badge.className = 'badge mono'
    badge.textContent = top.badge
    right.append(badge)
  }

  bar.append(left, title, right)

  const screen = document.createElement('div')
  screen.className = 'screen'
  if (top.layered) screen.dataset['layered'] = 'true'

  element.append(bar, screen)
  return {
    element,
    screen,
    setTitle(text: string) {
      title.textContent = text
    },
  }
}

export interface DockItem {
  readonly id: string
  readonly label: string
  /** ⭐ 主導線は1つだけ塗る */
  readonly lead?: boolean
  readonly count?: string | undefined
  readonly onGo: () => void
}

/** 下部の4パネル。⚠️ ホーム以外には出さない。 */
export function buildDock(items: readonly DockItem[]): HTMLElement {
  const dock = document.createElement('nav')
  dock.className = 'dock'
  for (const item of items) {
    const button = document.createElement('button')
    button.type = 'button'
    button.id = `d-${item.id}`
    button.dataset['lead'] = String(Boolean(item.lead))

    const mark = document.createElement('span')
    mark.className = 'dmark'
    mark.dataset['for'] = item.id

    const label = document.createElement('span')
    label.className = 'dlabel'
    label.textContent = item.label

    button.append(mark, label)

    if (item.count) {
      const count = document.createElement('span')
      count.className = 'dcount mono'
      count.textContent = item.count
      button.append(count)
    }

    button.addEventListener('click', item.onGo)
    dock.append(button)
  }
  return dock
}
