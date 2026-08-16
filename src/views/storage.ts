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
import { partyOf, togglePartyMember, type Game } from '../game/state.ts'
import { sorted, SORT_KEYS, SORT_LABELS, type SortKey } from '../game/storage.ts'
import { spriteToCanvas } from '../render/sprite.ts'

export interface RowActions {
  readonly onTrain: () => void
  readonly inParty: boolean
  readonly onToggleParty: () => void
}

export function buildUnitRow(creature: Creature, actions?: RowActions): HTMLElement {
  const onTrain = actions?.onTrain
  const species = speciesOf(creature)
  const actual = statsOf(creature)
  const wildTotal = wildTotalOf(creature)

  const unit = document.createElement('article')
  unit.className = 'unit'
  // 1体ずつ実寸で撮れるようにする（ギャラリーと同じ取っ手）
  unit.id = `u-${creature.id}`
  if (actions?.inParty) unit.dataset['party'] = 'true'

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

  if (actions) {
    const sortie = document.createElement('button')
    sortie.type = 'button'
    sortie.className = 'sortie'
    sortie.dataset['on'] = String(actions.inParty)
    sortie.textContent = actions.inParty ? '出撃中' : '出撃'
    // ⭐ 連れ出した個体だけが育成ポイントをもらうので、ここが育成の入口でもある
    sortie.title = '巣やボスへ連れて行く3体に入れる'
    sortie.addEventListener('click', actions.onToggleParty)
    ident.append(sortie)
  }

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

/** ⭐ モックの Box そのまま: **上に選んだ1体の詳細、下にアイコンのグリッド。**
 *
 *  ⚠️ 前は全個体のカードを縦に積んでいたが、50枠まで増えると一覧できない。
 *  「どれを逃がすか」は並べて比べる作業なので、グリッド のほうが向いている。 */
export function renderStorage(root: HTMLElement, game: Game, state: { sort: SortKey }): void {
  const storage = game.storage
  let picked: string | null = storage.creatures[0]?.id ?? null

  const detail = document.createElement('div')
  detail.className = 'boxdetail'

  const controls = document.createElement('div')
  controls.className = 'controls sorts'

  const grid = document.createElement('div')
  grid.className = 'boxgrid'

  function paint(): void {
    const party = partyOf(game).map((c) => c.id)
    const list = sorted(storage, state.sort)
    if (picked !== null && !list.some((c) => c.id === picked)) picked = list[0]?.id ?? null
    const current = list.find((c) => c.id === picked) ?? null

    detail.replaceChildren()
    if (current) {
      detail.append(
        buildUnitRow(current, {
          onTrain: paint,
          inParty: party.includes(current.id),
          onToggleParty: () => {
            togglePartyMember(game, current.id)
            paint()
          },
        }),
      )
    } else {
      const empty = document.createElement('p')
      empty.className = 'note'
      empty.textContent = 'BOX が空。巣へ行って卵を奪ってくる。'
      detail.append(empty)
    }

    grid.replaceChildren(
      ...list.map((c) => {
        const cell = document.createElement('button')
        cell.type = 'button'
        cell.className = 'cell'
        cell.id = `g-${c.id}`
        cell.dataset['on'] = String(c.id === picked)
        cell.dataset['party'] = String(party.includes(c.id))
        cell.append(spriteToCanvas(speciesOf(c).sprite, paletteOf(c), 2))
        const n = document.createElement('span')
        n.className = 'cnum mono'
        // ⭐ 並べ替えの基準になっている値を出す。並びの理由が見えないと選べない
        n.textContent = String(wildTotalOf(c))
        cell.append(n)
        cell.addEventListener('click', () => {
          picked = c.id
          paint()
        })
        return cell
      }),
    )
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
  root.append(detail, controls, grid)
}
