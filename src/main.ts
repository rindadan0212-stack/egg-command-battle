/** タイトル画面の仮置き。段0（観測基盤）が動いていることを確かめるためだけのもの。 */

import { EMPTY_SOURCE, startLiveReporting } from './live/report'

const STAGES: ReadonlyArray<readonly [string, string]> = [
  ['段0', '観測基盤（決定論・.live.json・照合コマンド・ギャラリー）'],
  ['段A', '個体と保管庫'],
  ['段B', '戦闘（3v3・スピードゲージ + CT）'],
  ['段C', '巣と卵（倒す / 盗んで逃げる）'],
  ['段D', '配合と遺伝'],
  ['段E', '輪を閉じる'],
]

const CURRENT_STAGE = '段0'

function render(root: HTMLElement): void {
  const heading = document.createElement('h1')
  heading.textContent = 'Egg Command Battle'

  const lead = document.createElement('p')
  lead.className = 'lead'
  lead.textContent = '欲しい卵を奪い、育て、配合し、理想の個体を作って強敵を倒す。'

  const note = document.createElement('p')
  note.className = 'note'
  note.textContent =
    'まだ何も無い。段0（観測基盤）が動いているかを確かめるための画面。'

  const list = document.createElement('ul')
  list.className = 'stages'
  for (const [tag, label] of STAGES) {
    const item = document.createElement('li')
    if (tag === CURRENT_STAGE) item.dataset['state'] = 'now'

    const tagEl = document.createElement('span')
    tagEl.className = 'tag'
    tagEl.textContent = tag

    const labelEl = document.createElement('span')
    labelEl.textContent = label

    item.append(tagEl, labelEl)
    list.append(item)
  }

  root.append(heading, lead, note, list)
}

const root = document.querySelector<HTMLElement>('#app')
if (root) render(root)

startLiveReporting('game', {
  ...EMPTY_SOURCE,
  // 段A 以降、ここに 種・所持個体の id・パーティの id を入れる。
  // ⭐ AI が測るべき個体そのものを、ここで名指しできるようにするのが目的。
  scene: () => ({ stage: CURRENT_STAGE }),
})

export {}
