/** 画面の器。巣・保管庫を切り替える。 */

import { fingerprintAll } from './core/fingerprint.ts'
import { wildTotalOf } from './game/creature.ts'
import { auditNests } from './game/nest.ts'
import { auditSpecies } from './game/species.ts'
import { newGame } from './game/state.ts'
import type { SortKey } from './game/storage.ts'
import { EMPTY_SOURCE, startLiveReporting } from './live/report.ts'
import { renderBreed } from './views/breed.ts'
import { renderNests, type NestView } from './views/nests.ts'
import { renderStorage } from './views/storage.ts'

// ⚠️ 表の不備をここで落とす。「型は通る・ただ効かなくなるだけ」を防ぐ数える検査
auditSpecies()
auditNests()

const WORLD_SEED = 20260815
const game = newGame(WORLD_SEED)

const VIEWS = [
  ['nests', '巣'],
  ['breed', '配合'],
  ['storage', '保管庫'],
] as const
type ViewId = (typeof VIEWS)[number][0]

const ui = { view: 'nests' as ViewId, sort: 'wildTotal' as SortKey }
let nestView: NestView | null = null

const root = document.querySelector<HTMLElement>('#app')
let storageButton: HTMLButtonElement | null = null

function storageLabel(): string {
  return `保管庫 ${game.storage.creatures.length}`
}

function buildNav(): HTMLElement {
  const nav = document.createElement('nav')
  nav.className = 'viewnav'
  for (const [id, label] of VIEWS) {
    const button = document.createElement('button')
    button.type = 'button'
    button.textContent = id === 'storage' ? storageLabel() : label
    button.dataset['on'] = String(id === ui.view)
    button.addEventListener('click', () => {
      if (ui.view === id) return
      ui.view = id
      paint()
    })
    if (id === 'storage') storageButton = button
    nav.append(button)
  }
  return nav
}

/** ⚠️ 巣の流れの途中で画面ごと作り直すと、戦闘や結果表示が飛ぶ。
 *  数だけ差し替える。 */
function refreshCounts(): void {
  if (storageButton) storageButton.textContent = storageLabel()
}

function paint(): void {
  if (!root) return
  // ⚠️ 前の画面の待ち時間を必ず止める。放っておくと裏で戦闘が進み続ける
  nestView?.dispose()
  nestView = null

  const heading = document.createElement('h1')
  heading.textContent = 'Egg Command Battle'
  root.replaceChildren(heading, buildNav())

  if (ui.view === 'storage') {
    renderStorage(root, game, ui)
    return
  }

  if (ui.view === 'breed') {
    renderBreed(root, game, refreshCounts)
    return
  }

  nestView = renderNests(game, refreshCounts)
  root.append(nestView.element)
}

paint()

startLiveReporting('game', {
  ...EMPTY_SOURCE,
  assets: () => ({
    count: game.storage.creatures.length,
    fingerprint: fingerprintAll(
      game.storage.creatures.map((c) => `${c.id}:${c.speciesId}:${wildTotalOf(c)}`),
    ),
  }),
  // ⭐ ここが §5.0 の肝。AI が測るべき個体そのものを名指しできるようにする
  scene: () => ({
    stage: '段C',
    view: ui.view,
    seed: WORLD_SEED,
    slots: `${game.storage.creatures.length}/${game.storage.slots}`,
    creatures: game.storage.creatures.map((c) => c.id),
    eggs: game.eggs.map((e) => e.id),
  }),
})

export {}
