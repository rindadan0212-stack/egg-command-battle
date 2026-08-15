/** 画面の器。保管庫と戦闘を切り替える。 */

import { fingerprintAll } from './core/fingerprint.ts'
import { Rng } from './core/rng.ts'
import { wildTotalOf } from './game/creature.ts'
import { makeProvisionalRoster } from './game/provisional.ts'
import { auditSpecies } from './game/species.ts'
import { accept, emptyStorage, type SortKey, type Storage } from './game/storage.ts'
import { EMPTY_SOURCE, startLiveReporting } from './live/report.ts'
import { renderBattle, type BattleView } from './views/battle.ts'
import { renderStorage } from './views/storage.ts'

// ⚠️ 種族表の不備をここで落とす。「型は通る・ただ効かなくなるだけ」を防ぐ数える検査
auditSpecies()

/** 🚧 段C（卵と孵化）で本物に差し替える。種を固定してあるので毎回同じ顔ぶれが出る */
const WORLD_SEED = 20260815
const roster = makeProvisionalRoster(new Rng(WORLD_SEED).stream('provisional-roster'), 12)
const storage: Storage = roster.reduce<Storage>((acc, c) => accept(acc, c), emptyStorage())

const VIEWS = [
  ['storage', '保管庫'],
  ['battle', '戦闘'],
] as const
type ViewId = (typeof VIEWS)[number][0]

const ui = { view: 'storage' as ViewId, sort: 'wildTotal' as SortKey }
let battleView: BattleView | null = null

const root = document.querySelector<HTMLElement>('#app')

function buildNav(): HTMLElement {
  const nav = document.createElement('nav')
  nav.className = 'viewnav'
  for (const [id, label] of VIEWS) {
    const button = document.createElement('button')
    button.type = 'button'
    button.textContent = label
    button.dataset['on'] = String(id === ui.view)
    button.addEventListener('click', () => {
      if (ui.view === id) return
      ui.view = id
      paint()
    })
    nav.append(button)
  }
  return nav
}

function paint(): void {
  if (!root) return
  // ⚠️ 前の画面の待ち時間を必ず止める。放っておくと裏で戦闘が進み続ける
  battleView?.dispose()
  battleView = null

  const heading = document.createElement('h1')
  heading.textContent = 'Egg Command Battle'

  root.replaceChildren(heading, buildNav())

  if (ui.view === 'storage') {
    renderStorage(root, storage, ui)
    return
  }

  // 🚧 段C で「巣に挑む」から入るようにする。今は保管庫の上位3体で固定の相手と戦う
  const party = [...storage.creatures]
    .sort((a, b) => wildTotalOf(b) - wildTotalOf(a) || a.id.localeCompare(b.id))
    .slice(0, 3)
  const foes = makeProvisionalRoster(new Rng(WORLD_SEED).stream('provisional-foes'), 3)

  const note = document.createElement('p')
  note.className = 'note'
  note.textContent =
    '🚧 素質の高い3体で固定の相手と戦う。段C で「巣に挑む」から入るようになる。'
  root.append(note)

  battleView = renderBattle(party, foes)
  root.append(battleView.element)
}

paint()

startLiveReporting('game', {
  ...EMPTY_SOURCE,
  assets: () => ({
    count: storage.creatures.length,
    fingerprint: fingerprintAll(
      storage.creatures.map((c) => `${c.id}:${c.speciesId}:${wildTotalOf(c)}`),
    ),
  }),
  // ⭐ ここが §5.0 の肝。AI が測るべき個体そのものを名指しできるようにする
  scene: () => ({
    stage: '段B',
    view: ui.view,
    seed: WORLD_SEED,
    slots: `${storage.creatures.length}/${storage.slots}`,
    creatures: storage.creatures.map((c) => c.id),
  }),
})

export {}
