/** 保管庫の画面（段A）。
 *
 *  ⭐ 見せたいのは「**どの2つが高い個体か**」。厳選の判断はそこで決まるので、
 *  ステの棒を画面の主役にし、名前や世代は引っ込める。
 */

import { Rng } from './core/rng.ts'
import { fingerprintAll } from './core/fingerprint.ts'
import {
  paletteOf,
  skillsOf,
  speciesOf,
  statsOf,
  wildTotalOf,
  type Creature,
} from './game/creature.ts'
import { makeProvisionalRoster } from './game/provisional.ts'
import { auditSpecies, ELEMENT_LABELS } from './game/species.ts'
import { STAT_KEYS, STAT_LABELS, WILD_STAT_MAX, WILD_TOTAL_MAX } from './game/stats.ts'
import {
  accept,
  emptyStorage,
  sorted,
  SORT_KEYS,
  SORT_LABELS,
  type SortKey,
  type Storage,
} from './game/storage.ts'
import { EMPTY_SOURCE, startLiveReporting } from './live/report.ts'
import { spriteToCanvas } from './render/sprite.ts'

// ⚠️ 種族表の不備をここで落とす。「型は通る・ただ効かなくなるだけ」を防ぐ数える検査
auditSpecies()

/** 🚧 段C（卵と孵化）で本物に差し替える。種を固定してあるので毎回同じ顔ぶれが出る */
const WORLD_SEED = 20260815
const roster = makeProvisionalRoster(new Rng(WORLD_SEED).stream('provisional-roster'), 12)

let storage: Storage = roster.reduce<Storage>((acc, c) => accept(acc, c), emptyStorage())
let sortKey: SortKey = 'wildTotal'

function buildUnit(creature: Creature): HTMLElement {
  const species = speciesOf(creature)
  const actual = statsOf(creature)
  const wildTotal = wildTotalOf(creature)

  const unit = document.createElement('article')
  unit.className = 'unit'
  // 1体ずつ実寸で撮れるようにする（ギャラリーと同じ取っ手）
  unit.id = `u-${creature.id}`

  const portrait = document.createElement('div')
  portrait.className = 'portrait'
  portrait.append(spriteToCanvas(species.sprite, paletteOf(creature), 2))

  const ident = document.createElement('div')
  ident.className = 'ident'
  const name = document.createElement('span')
  name.className = 'name'
  name.textContent = species.name
  const tags = document.createElement('span')
  tags.className = 'tags'
  const mutation = creature.mutationCounter > 0 ? ` · 変異${creature.mutationCounter}` : ''
  tags.textContent = `${ELEMENT_LABELS[species.element]} · 第${creature.generation}世代${mutation}`
  const idEl = document.createElement('span')
  idEl.className = 'cid mono'
  idEl.textContent = creature.id
  ident.append(name, tags, idEl)

  const stats = document.createElement('div')
  stats.className = 'stats'
  for (const key of STAT_KEYS) {
    const level = creature.wild[key]
    const row = document.createElement('div')
    row.className = 'stat'
    // 上限に張り付いているステを目立たせる（厳選の当たりが一目で分かるように）
    row.dataset['peak'] = String(level >= WILD_STAT_MAX)

    const k = document.createElement('span')
    k.className = 'k'
    k.textContent = STAT_LABELS[key]

    const bar = document.createElement('span')
    bar.className = 'bar'
    const fill = document.createElement('i')
    fill.style.width = `${(level / WILD_STAT_MAX) * 100}%`
    bar.append(fill)

    const v = document.createElement('span')
    v.className = 'v mono'
    v.textContent = String(level)

    const real = document.createElement('span')
    real.className = 'real mono'
    real.textContent = `→ ${actual[key]}`

    row.append(k, bar, v, real)
    stats.append(row)
  }

  const foot = document.createElement('div')
  foot.className = 'foot'
  const total = document.createElement('span')
  total.className = 'total mono'
  total.dataset['capped'] = String(wildTotal >= WILD_TOTAL_MAX)
  total.textContent = `素質 ${wildTotal}/${WILD_TOTAL_MAX}`
  const skills = document.createElement('span')
  skills.className = 'skills'
  // ⭐ 枠1は種族固定。印を付けて「これは奪ってこないと手に入らない」を見せる
  const [first, second, third] = skillsOf(creature)
  skills.textContent = `◆${first.name} · ${second?.name ?? '—'} · ${third?.name ?? '—'}`
  foot.append(total, skills)

  unit.append(portrait, ident, stats, foot)
  return unit
}

function render(root: HTMLElement): void {
  const heading = document.createElement('h1')
  heading.textContent = '保管庫'

  const lead = document.createElement('p')
  lead.className = 'lead'
  lead.textContent = `${storage.creatures.length} / ${storage.slots} 枠`

  const note = document.createElement('p')
  note.className = 'note'
  note.textContent =
    '🚧 仮の個体。段C（卵と孵化）で本物に差し替える。◆ が種族固定のスキル1。'

  const controls = document.createElement('div')
  controls.className = 'controls'
  const list = document.createElement('div')
  list.className = 'roster'

  const paint = (): void => {
    list.replaceChildren(...sorted(storage, sortKey).map(buildUnit))
  }

  for (const key of SORT_KEYS) {
    const button = document.createElement('button')
    button.type = 'button'
    button.textContent = SORT_LABELS[key]
    button.dataset['on'] = String(key === sortKey)
    button.addEventListener('click', () => {
      sortKey = key
      for (const other of controls.querySelectorAll('button')) {
        other.dataset['on'] = String(other === button)
      }
      paint()
    })
    controls.append(button)
  }

  paint()
  root.append(heading, lead, note, controls, list)
}

const root = document.querySelector<HTMLElement>('#app')
if (root) render(root)

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
    stage: '段A',
    seed: WORLD_SEED,
    slots: `${storage.creatures.length}/${storage.slots}`,
    sort: sortKey,
    creatures: storage.creatures.map((c) => c.id),
  }),
})

export {}
