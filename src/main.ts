/** 画面の器。⭐ **ホームがハブ。**各画面は ‹ でホームへ戻る。
 *
 *  ⚠️ 常時タブではない（モックの構造に合わせた）。
 *  下部の4パネルはホームにだけ出る。
 */

import { fingerprintAll } from './core/fingerprint.ts'
import { wildTotalOf } from './game/creature.ts'
import { auditNests } from './game/nest.ts'
import { auditSpecies } from './game/species.ts'
import { newGame, partyOf } from './game/state.ts'
import type { SortKey } from './game/storage.ts'
import { EMPTY_SOURCE, startLiveReporting } from './live/report.ts'
import { renderBreed } from './views/breed.ts'
import { buildHatch } from './views/hatch.ts'
import { buildHome } from './views/home.ts'
import { renderNests, type NestView } from './views/nests.ts'
import { buildDock, buildFrame, type Sky } from './views/shell.ts'
import { renderStorage } from './views/storage.ts'

// ⚠️ 表の不備をここで落とす。「型は通る・ただ効かなくなるだけ」を防ぐ数える検査
auditSpecies()
auditNests()

const WORLD_SEED = 20260815
const game = newGame(WORLD_SEED)

type ViewId = 'home' | 'nests' | 'hatch' | 'breed' | 'box'

const ui = { view: 'home' as ViewId, sort: 'wildTotal' as SortKey }
let nestView: NestView | null = null

const root = document.querySelector<HTMLElement>('#app')

function go(view: ViewId): void {
  if (ui.view === view) return
  ui.view = view
  paint()
}

const home = (): void => go('home')

/** 画面ごとの上段。⚠️ 数えられる事実だけをピルに置く（Lv も通貨も無い）。 */
const SKY: Record<ViewId, Sky> = {
  home: 'home',
  nests: 'nest',
  hatch: 'hatch',
  breed: 'breed',
  box: 'box',
}

function paint(): void {
  if (!root) return
  // ⚠️ 前の画面の待ち時間を必ず止める。放っておくと裏で戦闘が進み続ける
  nestView?.dispose()
  nestView = null

  const slots = `${game.storage.creatures.length}/${game.storage.slots}`

  if (ui.view === 'home') {
    const frame = buildFrame('home', { title: 'Egg Command Battle', badge: `BOX ${slots}` })
    frame.screen.append(buildHome(game))
    frame.element.append(
      buildDock([
        { id: 'box', label: 'BOX', count: slots, onGo: () => go('box') },
        {
          id: 'hatch',
          label: '孵化',
          count: game.eggs.length > 0 ? String(game.eggs.length) : undefined,
          onGo: () => go('hatch'),
        },
        { id: 'breed', label: '配合', onGo: () => go('breed') },
        // ⭐ 塗るのはここだけ。次にやることが1つに見える
        { id: 'nests', label: '探索', lead: true, onGo: () => go('nests') },
      ]),
    )
    root.replaceChildren(frame.element)
    return
  }

  const titles: Record<Exclude<ViewId, 'home'>, string> = {
    nests: '巣をえらぶ',
    hatch: '孵化',
    breed: '配合',
    box: 'BOX',
  }
  const badges: Record<Exclude<ViewId, 'home'>, string | undefined> = {
    nests: `編成 ${partyOf(game).length}/3`,
    hatch: `卵 ${game.eggs.length}`,
    breed: `BOX ${slots}`,
    box: slots,
  }
  const view = ui.view as Exclude<ViewId, 'home'>

  const frame = buildFrame(SKY[view], {
    back: home,
    title: titles[view],
    badge: badges[view],
  })
  root.replaceChildren(frame.element)

  if (view === 'box') {
    renderStorage(frame.screen, game, ui)
    return
  }
  if (view === 'breed') {
    renderBreed(frame.screen, game, paint)
    return
  }
  if (view === 'hatch') {
    frame.screen.append(buildHatch(game, paint))
    return
  }

  nestView = renderNests(game, paint, frame.setTitle)
  frame.screen.append(nestView.element)
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
    stage: '段F',
    view: ui.view,
    seed: WORLD_SEED,
    slots: `${game.storage.creatures.length}/${game.storage.slots}`,
    creatures: game.storage.creatures.map((c) => c.id),
    eggs: game.eggs.map((e) => e.id),
  }),
})

export {}
