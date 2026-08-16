/** ホーム。⭐ **輪のハブ。**
 *
 *  モックの Home そのまま: 編成3体を三角に置き、リーダーを手前に大きく。
 *  下に4パネル。探索だけ塗って主導線にする。
 *
 *  ⭐ **編成が画面の主役。**強奪の飛距離も戦闘も編成で決まるのに、
 *  今までは保管庫の「出撃中」でしか見えなかった。
 */

import { paletteOf, speciesOf, statsOf, type Creature } from '../game/creature.ts'
import { BOSS_NAME } from '../game/nest.ts'
import { distanceFor } from '../game/steal.ts'
import { partyOf, type Game } from '../game/state.ts'
import { spriteToCanvas } from '../render/sprite.ts'

function buildStanding(creature: Creature, scale: number, role: string): HTMLElement {
  const box = document.createElement('div')
  box.className = 'stand'
  box.id = `h-${creature.id}`

  const tag = document.createElement('span')
  tag.className = 'tag'
  tag.textContent = speciesOf(creature).name

  const art = document.createElement('div')
  art.className = 'art'
  art.append(spriteToCanvas(speciesOf(creature).sprite, paletteOf(creature), scale))

  const sub = document.createElement('span')
  sub.className = 'sub mono'
  sub.textContent = role

  box.append(tag, art, sub)
  return box
}

export function buildHome(game: Game): HTMLElement {
  const wrap = document.createElement('div')
  wrap.className = 'home'

  const party = partyOf(game)

  // ⭐ モックが「NOW EVENT」を置いていた場所に、**輪の目的地**を置く。
  //    企画の駆動力は「この壁を越えるには何が要るか」なので、
  //    ホームで常に壁の名前が見えているのが素直。
  //    ⚠️ 期間限定イベントは作らない（企画の「やらないこと」）。
  const goal = document.createElement('div')
  goal.className = 'goal'
  const goalLabel = document.createElement('span')
  goalLabel.className = 'glabel mono'
  goalLabel.textContent = 'GOAL'
  const goalName = document.createElement('span')
  goalName.className = 'gname'
  goalName.textContent = `${BOSS_NAME} を倒す`
  goal.append(goalLabel, goalName)
  wrap.append(goal)

  const stage = document.createElement('div')
  stage.className = 'stage'

  // ⚠️ 雲は飾りだが、地の色だけだと縦に間延びする。位置は固定で置く
  const cloudA = document.createElement('span')
  cloudA.className = 'cloud a'
  const cloudB = document.createElement('span')
  cloudB.className = 'cloud b'
  stage.append(cloudA, cloudB)

  if (party.length === 0) {
    const empty = document.createElement('p')
    empty.className = 'note'
    empty.textContent = 'BOX で3体を「出撃」にすると、ここに並ぶ。'
    stage.append(empty)
  } else {
    const [lead, second, third] = party
    // ⭐ 三角配置。手前のリーダーを一番大きく
    if (second) {
      const left = buildStanding(second, 6, '02')
      left.classList.add('side', 'left')
      stage.append(left)
    }
    if (third) {
      const right = buildStanding(third, 6, '03')
      right.classList.add('side', 'right')
      stage.append(right)
    }
    if (lead) {
      const center = buildStanding(lead, 10, 'LEADER')
      center.classList.add('leader')
      stage.append(center)
    }
  }

  // 台座。モックと同じ「楕円 + その下の帯」
  const ground = document.createElement('span')
  ground.className = 'ground'
  const soil = document.createElement('span')
  soil.className = 'soil'
  stage.append(ground, soil)

  wrap.append(stage)

  // ⭐ 編成の総スピードは飛距離そのもの。ホームで見えることに意味がある
  const facts = document.createElement('div')
  facts.className = 'facts'
  const spd = party.reduce((sum, c) => sum + statsOf(c).spd, 0)
  for (const [label, value] of [
    ['編成', `${party.length}/3`],
    ['スピード合計', String(spd)],
    ['飛距離', party.length > 0 ? String(distanceFor(party)) : '—'],
  ] as const) {
    const cell = document.createElement('div')
    cell.className = 'fact'
    const k = document.createElement('span')
    k.className = 'k mono'
    k.textContent = label
    const v = document.createElement('span')
    v.className = 'v'
    v.textContent = value
    cell.append(k, v)
    facts.append(cell)
  }
  wrap.append(facts)

  return wrap
}
