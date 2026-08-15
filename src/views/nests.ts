/** 巣の画面。輪の入口。
 *
 *  ```
 *  巣を選ぶ → 挑む → 二択（倒す / 盗んで逃げる）→ 卵 → 孵す → 保管庫
 *  ```
 *
 *  ⭐ **強い親ほど良い卵**なので、難易度と報酬が自動で結ばれている。
 */

import type { Outcome } from '../game/battle.ts'
import { wildTotalOf, type Creature } from '../game/creature.ts'
import {
  BOSS_NAME,
  makeBossParty,
  NESTS,
  wildTotalForTier,
  type Egg,
  type Nest,
} from '../game/nest.ts'
import { skillById } from '../game/skills.ts'
import { ELEMENT_LABELS, speciesById } from '../game/species.ts'
import { WILD_TOTAL_MAX } from '../game/stats.ts'
import { awardParty, defendersOf, gainEgg, hatchEgg, partyOf, type Game } from '../game/state.ts'
import { isFull } from '../game/storage.ts'
import { spriteToCanvas } from '../render/sprite.ts'
import { renderBattle, type BattleView } from './battle.ts'

export interface NestView {
  readonly element: HTMLElement
  dispose(): void
}

export function renderNests(game: Game, onChange: () => void): NestView {
  const element = document.createElement('div')
  element.className = 'nests'
  let battle: BattleView | null = null

  function clearBattle(): void {
    battle?.dispose()
    battle = null
  }

  function buildNestRow(nest: Nest): HTMLElement {
    const species = speciesById(nest.speciesId)
    const row = document.createElement('article')
    row.className = 'nestrow'
    row.id = `n-${nest.id}`

    const art = document.createElement('div')
    art.className = 'portrait'
    art.append(spriteToCanvas(species.sprite, species.palettes[0] as string[], 2))

    const ident = document.createElement('div')
    ident.className = 'ident'
    const name = document.createElement('span')
    name.className = 'name'
    name.textContent = nest.name
    const tags = document.createElement('span')
    tags.className = 'tags'
    tags.textContent = `${species.name} · ${ELEMENT_LABELS[species.element]} · 段階${nest.tier}`
    const gain = document.createElement('span')
    gain.className = 'cid mono'
    // ⭐ 「この巣で何が手に入るか」を先に見せる。輪の駆動力はここから出る
    gain.textContent = `素質 ~${wildTotalForTier(nest.tier)}/${WILD_TOTAL_MAX} · ◆${skillById(species.skill1).name}`
    ident.append(name, tags, gain)

    const go = document.createElement('div')
    go.className = 'controls'
    const button = document.createElement('button')
    button.type = 'button'
    button.textContent = '挑む'
    button.addEventListener('click', () => startBattle(nest))
    go.append(button)

    row.append(art, ident, go)
    return row
  }

  function paintList(): void {
    clearBattle()
    element.replaceChildren()

    const lead = document.createElement('p')
    lead.className = 'lead'
    lead.textContent = '欲しい卵を奪う。'

    const note = document.createElement('p')
    note.className = 'note'
    note.textContent =
      '倒せば確実に良い卵が手に入る。盗めば格上の巣からも狙えるが、逃げ切れるかは速度しだい。'

    element.append(lead, note)

    if (game.eggs.length > 0) {
      element.append(buildEggShelf())
    }

    const list = document.createElement('div')
    list.className = 'nestlist'
    list.append(...NESTS.map(buildNestRow))
    element.append(list, buildBossRow())
  }

  /** ⭐ 輪の終点。ここで詰まったとき「何が足りないか」を考えるのが遊びの中心。 */
  function buildBossRow(): HTMLElement {
    // ⚠️ 数値を画面に直書きしない。実物から引く
    //    （変異回数を直書きしていて、ボスを調整した後もずっと古い値を出していた）
    const [lord] = makeBossParty()
    const species = speciesById(lord?.speciesId ?? 'nushi')
    const row = document.createElement('article')
    row.className = 'nestrow boss'
    row.id = 'n-boss'

    const art = document.createElement('div')
    art.className = 'portrait'
    art.append(spriteToCanvas(species.sprite, species.palettes[0] as string[], 2))

    const ident = document.createElement('div')
    ident.className = 'ident'
    const name = document.createElement('span')
    name.className = 'name'
    name.textContent = BOSS_NAME
    const tags = document.createElement('span')
    tags.className = 'tags'
    tags.textContent =
      `${species.name} · ${ELEMENT_LABELS[species.element]}` +
      ` · 変異${lord?.mutationCounter ?? 0} · 素質${lord ? wildTotalOf(lord) : 0}`
    const hint = document.createElement('span')
    hint.className = 'cid mono'
    // ⚠️ 何を要求してくる相手かを先に見せる。隠すと「運が悪かった」で終わる
    hint.textContent = `高防御 · 速度を奪う · ◆${skillById(species.skill1).name}（CT${skillById(species.skill1).ct}の全体大技）`
    ident.append(name, tags, hint)

    const go = document.createElement('div')
    go.className = 'controls'
    const button = document.createElement('button')
    button.type = 'button'
    button.textContent = '挑む'
    button.addEventListener('click', startBoss)
    go.append(button)

    row.append(art, ident, go)
    return row
  }

  function startBoss(): void {
    clearBattle()
    element.replaceChildren()

    const party = partyOf(game)
    if (party.length === 0) {
      paintList()
      return
    }

    const lead = document.createElement('p')
    lead.className = 'lead'
    lead.textContent = BOSS_NAME
    element.append(lead)

    // ⚠️ ボスからは卵を盗めない（stealRng を渡さない）。倒すしか道が無い
    const view = renderBattle(party, makeBossParty(), (outcome) => paintBossResult(outcome, party))
    battle = view
    element.append(view.element)
  }

  function paintBossResult(outcome: Outcome, party: readonly Creature[]): void {
    const box = document.createElement('div')
    box.className = 'result'

    const line = document.createElement('p')
    line.className = 'lead'
    const sub = document.createElement('p')
    sub.className = 'note'

    if (outcome === 'ally') {
      line.textContent = `${BOSS_NAME} を倒した`
      awardParty(party, 3)
      sub.textContent = `出撃した ${party.map((c) => c.id).join(' / ')} に育成 +3`
      onChange()
    } else {
      line.textContent = '届かなかった'
      sub.textContent = '何が足りなかったかを考えて、必要な血を奪いに行く。'
    }

    const back = document.createElement('div')
    back.className = 'controls'
    const button = document.createElement('button')
    button.type = 'button'
    button.textContent = '巣へ戻る'
    button.addEventListener('click', paintList)
    back.append(button)

    box.append(line, sub, back)
    element.append(box)
  }

  function buildEggShelf(): HTMLElement {
    const box = document.createElement('div')
    box.className = 'eggshelf'

    const title = document.createElement('p')
    title.className = 'shelftitle'
    title.textContent = `持っている卵 ${game.eggs.length}`
    box.append(title)

    const full = isFull(game.storage)
    if (full) {
      const warn = document.createElement('p')
      warn.className = 'note'
      warn.textContent = '⚠️ 保管庫が満杯。先にどれかを逃がさないと孵せない。'
      box.append(warn)
    }

    for (const egg of game.eggs) {
      const species = speciesById(egg.speciesId)
      const row = document.createElement('div')
      row.className = 'eggrow'
      row.id = `e-${egg.id}`

      const label = document.createElement('span')
      label.className = 'mono'
      label.textContent =
        `${egg.id} · ${species.name} · 素質 ${sumOf(egg)}/${WILD_TOTAL_MAX}` +
        `（${egg.how === 'defeated' ? '倒して奪った' : '盗んだ'}）`

      const button = document.createElement('button')
      button.type = 'button'
      button.textContent = '孵す'
      button.disabled = full
      button.addEventListener('click', () => {
        const born = hatchEgg(game, egg.id)
        onChange()
        paintHatched(born)
      })

      row.append(label, button)
      box.append(row)
    }
    return box
  }

  function sumOf(egg: Egg): number {
    return Object.values(egg.wild).reduce((s, v) => s + v, 0)
  }

  function paintHatched(born: Creature): void {
    element.replaceChildren()
    const species = speciesById(born.speciesId)

    const lead = document.createElement('p')
    lead.className = 'lead'
    lead.textContent = `${species.name} が孵った`

    const art = document.createElement('div')
    art.className = 'portrait big'
    art.append(spriteToCanvas(species.sprite, species.palettes[born.paletteIndex] as string[], 4))

    const detail = document.createElement('p')
    detail.className = 'note mono'
    const [s1, s2, s3] = [0, 1, 2].map((i) => {
      const all = [species.skill1, ...born.skills23]
      const id = all[i]
      return id ? skillById(id).name : '—'
    })
    detail.textContent = `${born.id} · 素質 ${wildTotalOf(born)}/${WILD_TOTAL_MAX} · ◆${s1} · ${s2} · ${s3}`

    const back = document.createElement('div')
    back.className = 'controls'
    const button = document.createElement('button')
    button.type = 'button'
    button.textContent = '巣へ戻る'
    button.addEventListener('click', paintList)
    back.append(button)

    element.append(lead, art, detail, back)
  }

  function startBattle(nest: Nest): void {
    clearBattle()
    element.replaceChildren()

    const party = partyOf(game)
    if (party.length === 0) {
      paintList()
      return
    }

    const lead = document.createElement('p')
    lead.className = 'lead'
    lead.textContent = nest.name
    element.append(lead)

    const view = renderBattle(
      party,
      defendersOf(game, nest),
      (outcome) => paintResult(nest, outcome, party),
      game.rng.steal,
    )
    battle = view
    element.append(view.element)
  }

  function paintResult(nest: Nest, outcome: Outcome, party: readonly Creature[]): void {
    const got = outcome === 'ally' ? 'defeated' : outcome === 'stolen' ? 'stolen' : null
    const box = document.createElement('div')
    box.className = 'result'

    const line = document.createElement('p')
    line.className = 'lead'
    const sub = document.createElement('p')
    sub.className = 'note'

    if (got) {
      const egg = gainEgg(game, nest, got)
      line.textContent = `卵を手に入れた（${egg.id} · 素質 ${sumOf(egg)}）`
      // ⭐ 出撃していた個体だけが育成ポイントをもらう
      awardParty(party)
      sub.textContent = `出撃した ${party.map((c) => c.id).join(' / ')} に育成 +1`
      onChange()
    } else {
      line.textContent = outcome === 'enemy' ? '追い返された' : '決着がつかなかった'
      sub.textContent = '育成ポイントは入らない。'
    }

    const back = document.createElement('div')
    back.className = 'controls'
    const button = document.createElement('button')
    button.type = 'button'
    button.textContent = '巣へ戻る'
    button.addEventListener('click', paintList)
    back.append(button)

    box.append(line, sub, back)
    element.append(box)
  }

  paintList()

  return {
    element,
    dispose: clearBattle,
  }
}
