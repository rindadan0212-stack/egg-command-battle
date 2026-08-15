/** 保管庫の画面。
 *
 *  ⭐ 見せたいのは「**どの2つが高い個体か**」。厳選の判断はそこで決まるので、
 *  ステの棒を画面の主役にし、名前や世代は引っ込める。
 */

import {
  paletteOf,
  skillsOf,
  speciesOf,
  spendPoint,
  statsOf,
  TRAIN_MAX,
  unspentOf,
  wildTotalOf,
  type Creature,
} from '../game/creature.ts'
import { ELEMENT_LABELS } from '../game/species.ts'
import { STAT_KEYS, STAT_LABELS, wildStatMaxFor, wildTotalMaxFor } from '../game/stats.ts'
import { sorted, SORT_KEYS, SORT_LABELS, type SortKey, type Storage } from '../game/storage.ts'
import { spriteToCanvas } from '../render/sprite.ts'

export function buildUnitRow(creature: Creature, onTrain?: () => void): HTMLElement {
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
    const statMax = wildStatMaxFor(creature.mutationCounter)
    row.dataset['peak'] = String(level >= statMax)

    const k = document.createElement('span')
    k.className = 'k'
    k.textContent = STAT_LABELS[key]

    const bar = document.createElement('span')
    bar.className = 'bar'
    const fill = document.createElement('i')
    fill.style.width = `${(level / statMax) * 100}%`
    bar.append(fill)

    const v = document.createElement('span')
    v.className = 'v mono'
    v.textContent = String(level)

    const real = document.createElement('span')
    real.className = 'real mono'
    // 育成で振った分は素質と分けて見せる（素質は変えられない、を読ませるため）
    const put = creature.trained[key]
    real.textContent = put > 0 ? `→ ${actual[key]} (+${put})` : `→ ${actual[key]}`

    row.append(k, bar, v, real)

    // ⭐ 振れるポイントがあるときだけ + を出す。⚠️ 振ったら戻せない
    if (onTrain && unspentOf(creature) > 0) {
      const plus = document.createElement('button')
      plus.type = 'button'
      plus.className = 'train'
      plus.textContent = '+'
      plus.title = `${STAT_LABELS[key]} に育成ポイントを1振る（戻せない）`
      plus.addEventListener('click', () => {
        spendPoint(creature, key)
        onTrain()
      })
      row.append(plus)
    }

    stats.append(row)
  }

  const foot = document.createElement('div')
  foot.className = 'foot'
  const total = document.createElement('span')
  total.className = 'total mono'
  // ⚠️ 上限は変異ぶん押し上がるので、個体ごとに違う
  const cap = wildTotalMaxFor(creature.mutationCounter)
  total.dataset['capped'] = String(wildTotal >= cap)
  const left = unspentOf(creature)
  total.textContent =
    `素質 ${wildTotal}/${cap} · 育成 ${creature.earned}/${TRAIN_MAX}` +
    (left > 0 ? ` ⭐ 未使用 ${left}` : '')
  const skills = document.createElement('span')
  skills.className = 'skills'
  // ⭐ 枠1は種族固定。印を付けて「これは奪ってこないと手に入らない」を見せる
  const [first, second, third] = skillsOf(creature)
  skills.textContent = `◆${first.name} · ${second?.name ?? '—'} · ${third?.name ?? '—'}`
  foot.append(total, skills)

  unit.append(portrait, ident, stats, foot)
  return unit
}

export function renderStorage(root: HTMLElement, storage: Storage, state: { sort: SortKey }): void {
  const lead = document.createElement('p')
  lead.className = 'lead'
  lead.textContent = `${storage.creatures.length} / ${storage.slots} 枠`

  const note = document.createElement('p')
  note.className = 'note'
  note.textContent = '◆ が種族固定のスキル1。奪ってこないと手に入らない。'

  const controls = document.createElement('div')
  controls.className = 'controls'
  const list = document.createElement('div')
  list.className = 'roster'

  const paint = (): void => {
    list.replaceChildren(...sorted(storage, state.sort).map((c) => buildUnitRow(c, paint)))
  }

  for (const key of SORT_KEYS) {
    const button = document.createElement('button')
    button.type = 'button'
    button.textContent = SORT_LABELS[key]
    button.dataset['on'] = String(key === state.sort)
    button.addEventListener('click', () => {
      state.sort = key
      for (const other of controls.querySelectorAll('button')) {
        other.dataset['on'] = String(other === button)
      }
      paint()
    })
    controls.append(button)
  }

  paint()
  root.append(lead, note, controls, list)
}
