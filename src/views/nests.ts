/** 巣の画面。輪の入口。
 *
 *  ```
 *  巣を選ぶ → 発射（届けば強奪）→ 外せば戦闘（勝てば卵）→ 孵す → 保管庫
 *  ```
 *
 *  ⚠️ **入るまで親の能力は分からない。見える情報は姿だけ。**
 *  ⭐ 強奪はスピード合計で決まるので、速度に寄せた編成ほど届く。
 *     だが外れたときはその編成のまま戦うことになる ── ここが張り合い。
 */

import { wildTotalOf, type Creature } from '../game/creature.ts'
import { BOSS_NAME, makeBossParty, NESTS, type Egg, type Nest } from '../game/nest.ts'
import type { Outcome } from '../game/battle.ts'
import { effectiveCt, skillById } from '../game/skills.ts'
import { makeField } from '../game/steal.ts'
import { ELEMENT_LABELS, speciesById } from '../game/species.ts'
import { WILD_TOTAL_MAX } from '../game/stats.ts'
import { awardParty, defendersOf, gainEgg, hatchEgg, partyOf, type Game } from '../game/state.ts'
import { isFull } from '../game/storage.ts'
import { spriteToCanvas } from '../render/sprite.ts'
import { renderBattle, type BattleView } from './battle.ts'
import { renderSteal, type StealView } from './steal.ts'

export interface NestView {
  readonly element: HTMLElement
  dispose(): void
}

export function renderNests(game: Game, onChange: () => void): NestView {
  const element = document.createElement('div')
  element.className = 'nests'
  let battle: BattleView | null = null
  let steal: StealView | null = null

  function clearBattle(): void {
    battle?.dispose()
    battle = null
    steal?.dispose()
    steal = null
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
    // ⚠️ **巣に入るまで能力は分からない。見た目だけ。**
    //    段階も素質もスキルも出さない。分かるのは「どの姿の親がいるか」だけ。
    //    ⭐ 姿から種族は読めるので、「この技が欲しいならこの姿」という知識は育つ
    const tags = document.createElement('span')
    tags.className = 'tags'
    tags.textContent = `${species.name} · ${ELEMENT_LABELS[species.element]}`
    ident.append(name, tags)

    const go = document.createElement('div')
    go.className = 'controls'
    const button = document.createElement('button')
    button.type = 'button'
    button.textContent = '挑む'
    button.addEventListener('click', () => enterNest(nest))
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
      '引っ張って飛ばし、親をかわして卵まで届けば奪える。外せばそのまま戦闘になる。'

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
    // ⚠️ **文章で書かない。**「CT3の全体大技」と直書きしていて、
    //    枠1が CT0 になった後もその表示のままだった（実物と食い違う）
    const bossSkills = [species.skill1, ...(lord?.skills23 ?? [])]
    hint.textContent = bossSkills
      .map((id, slot) => {
        if (!id) return null
        const skill = skillById(id)
        const ct = effectiveCt(slot, skill)
        return `${slot === 0 ? '◆' : ''}${skill.name}${ct === 0 ? '（毎回）' : `（CT${ct}）`}`
      })
      .filter(Boolean)
      .join(' · ')
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

    // ⚠️ ボスには卵が無い。発射フェーズも無く、倒すしか道が無い
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

  /** ⭐ 巣に入るとまず発射フェーズ。届けば強奪、外せば戦闘。 */
  function enterNest(nest: Nest): void {
    clearBattle()
    element.replaceChildren()

    const party = partyOf(game)
    if (party.length === 0) {
      paintList()
      return
    }

    const heading = document.createElement('p')
    heading.className = 'lead'
    heading.textContent = nest.name
    element.append(heading)

    // 親がどちらへ寄るかだけ乱数。⚠️ 挑むたびに変わるので、同じ手は通じない
    const side = game.rng.steal.chance(0.5) ? 'left' : 'right'
    const defenders = defendersOf(game, nest)
    const parent = defenders[0] as Creature

    const view = renderSteal(makeField(nest.tier, side), party, parent, (outcome) => {
      if (outcome === 'success') {
        paintResult(nest, 'stolen', party)
        return
      }
      startBattle(nest, defenders, party, outcome)
    })
    steal = view
    element.append(view.element)
  }

  function startBattle(
    nest: Nest,
    defenders: readonly Creature[],
    party: readonly Creature[],
    why: 'blocked' | 'stalled',
  ): void {
    clearBattle()
    element.replaceChildren()

    const heading = document.createElement('p')
    heading.className = 'lead'
    heading.textContent = nest.name
    const why2 = document.createElement('p')
    why2.className = 'note'
    why2.textContent = why === 'blocked' ? '親にぶつかった。戦闘。' : '届かなかった。戦闘。'
    element.append(heading, why2)

    const view = renderBattle(party, [...defenders], (outcome) =>
      paintResult(nest, outcome === 'ally' ? 'defeated' : null, party),
    )
    battle = view
    element.append(view.element)
  }

  function paintResult(
    nest: Nest,
    got: 'defeated' | 'stolen' | null,
    party: readonly Creature[],
  ): void {
    clearBattle()
    element.replaceChildren()
    const box = document.createElement('div')
    box.className = 'result'

    const line = document.createElement('p')
    line.className = 'lead'
    const sub = document.createElement('p')
    sub.className = 'note'

    if (got) {
      const egg = gainEgg(game, nest, got)
      line.textContent =
        got === 'stolen'
          ? `掠め取った（${egg.id} · 素質 ${sumOf(egg)}）`
          : `倒して奪った（${egg.id} · 素質 ${sumOf(egg)}）`
      // ⭐ 出撃していた個体だけが育成ポイントをもらう
      awardParty(party)
      sub.textContent =
        `出撃した ${party.map((c) => c.id).join(' / ')} に育成 +1` +
        (got === 'stolen' ? '　⚠️ 掠め取った卵は素質が落ちる' : '')
      onChange()
    } else {
      line.textContent = '追い返された'
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
