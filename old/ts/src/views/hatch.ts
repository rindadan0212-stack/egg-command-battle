/** 孵化の画面。
 *
 *  ⭐ モックでは独立した画面になっていたので分けた（前は巣の画面に間借りしていた）。
 *  ⚠️ **実時間の待ちは入れない**（企画の「やらないこと」）。押せば即孵る。
 *  待たせる仕掛けは、輪が閉じて面白いと分かってからでいい。
 */

import { paletteOf, skillsOf, speciesOf, wildTotalOf, type Creature } from '../game/creature.ts'
import type { Egg } from '../game/nest.ts'
import { speciesById } from '../game/species.ts'
import { wildTotalMaxFor, WILD_TOTAL_MAX } from '../game/stats.ts'
import { hatchEgg, type Game } from '../game/state.ts'
import { isFull } from '../game/storage.ts'
import { spriteToCanvas } from '../render/sprite.ts'

function sumOf(egg: Egg): number {
  return Object.values(egg.wild).reduce((s, v) => s + v, 0)
}

export function buildHatch(game: Game, onChange: () => void): HTMLElement {
  const wrap = document.createElement('div')
  wrap.className = 'hatch'

  function paint(): void {
    wrap.replaceChildren()

    const full = isFull(game.storage)
    if (full) {
      const warn = document.createElement('p')
      warn.className = 'warn'
      warn.textContent = '⚠️ BOX が満杯。先にどれかを逃がさないと孵せない。'
      wrap.append(warn)
    }

    if (game.eggs.length === 0) {
      const empty = document.createElement('p')
      empty.className = 'note'
      empty.textContent = '卵がない。巣へ行って奪ってくる。'
      wrap.append(empty)
      return
    }

    const grid = document.createElement('div')
    grid.className = 'eggs'

    for (const egg of game.eggs) {
      const species = speciesById(egg.speciesId)
      const slot = document.createElement('article')
      slot.className = 'eggslot'
      slot.id = `e-${egg.id}`

      const art = document.createElement('div')
      art.className = 'art'
      // ⭐ 卵のうちは中身が分からないので、姿は出さず殻だけ見せる
      const shell = document.createElement('span')
      shell.className = 'shell'
      shell.dataset['how'] = egg.how
      art.append(shell)

      const body = document.createElement('div')
      body.className = 'ebody'
      const name = document.createElement('span')
      name.className = 'name'
      name.textContent = species.name
      const how = document.createElement('span')
      how.className = 'tags'
      how.textContent = egg.how === 'defeated' ? '倒して奪った' : '掠め取った'
      const sum = document.createElement('span')
      sum.className = 'cid mono'
      sum.textContent = `${egg.id} · 素質 ${sumOf(egg)}/${WILD_TOTAL_MAX}`
      body.append(name, how, sum)

      const button = document.createElement('button')
      button.type = 'button'
      button.className = 'go'
      button.textContent = 'ふ化する'
      button.disabled = full
      button.addEventListener('click', () => {
        const born = hatchEgg(game, egg.id)
        onChange()
        paintHatched(born)
      })

      slot.append(art, body, button)
      grid.append(slot)
    }

    wrap.append(grid)
  }

  function paintHatched(born: Creature): void {
    wrap.replaceChildren()
    const species = speciesOf(born)

    const card = document.createElement('div')
    card.className = 'born'

    const line = document.createElement('p')
    line.className = 'title'
    line.textContent = `${species.name} が孵った`

    const art = document.createElement('div')
    art.className = 'art'
    art.append(spriteToCanvas(species.sprite, paletteOf(born), 6))

    const detail = document.createElement('p')
    detail.className = 'cid mono'
    const [s1, s2, s3] = skillsOf(born)
    detail.textContent =
      `${born.id} · 素質 ${wildTotalOf(born)}/${wildTotalMaxFor(born.mutationCounter)}` +
      (born.mutationCounter > 0 ? ` · ⭐ 変異${born.mutationCounter}` : '')

    const skills = document.createElement('p')
    skills.className = 'note'
    skills.textContent = `◆${s1.name} · ${s2?.name ?? '—'} · ${s3?.name ?? '—'}`

    const controls = document.createElement('div')
    controls.className = 'controls'
    const back = document.createElement('button')
    back.type = 'button'
    back.textContent = '続ける'
    back.addEventListener('click', paint)
    controls.append(back)

    card.append(line, art, detail, skills, controls)
    wrap.append(card)
  }

  paint()
  return wrap
}
