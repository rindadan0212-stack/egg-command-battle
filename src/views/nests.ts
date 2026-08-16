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
import { awardParty, defendersOf, gainEgg, partyOf, type Game } from '../game/state.ts'
import { spriteToCanvas } from '../render/sprite.ts'
import { renderBattle, type BattleView } from './battle.ts'
import { renderSteal, type StealView } from './steal.ts'

export interface NestView {
  readonly element: HTMLElement
  dispose(): void
}

export function renderNests(
  game: Game,
  onChange: () => void,
  setTitle: (title: string) => void = () => {},
): NestView {
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
    row.className = 'nestcard'
    row.id = `n-${nest.id}`

    // ⭐ 巣そのものを描く。カードの左を「場所」が占めるとリストに見えなくなる
    const art = document.createElement('div')
    art.className = 'nestart'
    const bowl = document.createElement('span')
    bowl.className = 'bowl'
    const who = document.createElement('span')
    who.className = 'who'
    who.append(spriteToCanvas(species.sprite, species.palettes[0] as string[], 3))
    art.append(bowl, who)

    const ident = document.createElement('div')
    ident.className = 'ident'
    // ⚠️ **巣に入るまで能力は分からない。見た目だけ。**
    //    段階も素質もスキルも出さない。分かるのは「どの姿の親がいるか」だけ。
    //    ⭐ 姿から種族は読めるので、「この技が欲しいならこの姿」という知識は育つ
    const badge = document.createElement('span')
    badge.className = 'ebadge mono'
    badge.dataset['element'] = species.element
    badge.textContent = ELEMENT_LABELS[species.element] as string
    const name = document.createElement('span')
    name.className = 'name'
    name.textContent = nest.name
    const parent = document.createElement('span')
    parent.className = 'tags'
    parent.textContent = `親：${species.name}`
    ident.append(badge, name, parent)

    const go = document.createElement('button')
    go.type = 'button'
    go.className = 'challenge'
    go.textContent = '挑戦'
    go.addEventListener('click', () => enterNest(nest))

    row.append(art, ident, go)
    return row
  }

  function paintList(): void {
    clearBattle()
    setTitle('巣をえらぶ')
    element.replaceChildren()

    // ⭐ 巣の数は増える。一覧だけスクロールさせ、遊び方の一行は下に残す
    const scroller = document.createElement('div')
    scroller.className = 'scroller'
    const list = document.createElement('div')
    list.className = 'nestlist'
    list.append(...NESTS.map(buildNestRow), buildBossRow())
    scroller.append(list)

    const hint = document.createElement('p')
    hint.className = 'hint'
    hint.textContent = '引っ張って飛ばし、親をかわして卵まで届けば奪える。外せばそのまま戦闘。'

    element.append(scroller, hint)
  }

  /** ⭐ 輪の終点。ここで詰まったとき「何が足りないか」を考えるのが遊びの中心。 */
  function buildBossRow(): HTMLElement {
    // ⚠️ 数値を画面に直書きしない。実物から引く
    const [lord] = makeBossParty()
    const species = speciesById(lord?.speciesId ?? 'nushi')
    const row = document.createElement('article')
    row.className = 'nestcard boss'
    row.id = 'n-boss'

    const art = document.createElement('div')
    art.className = 'nestart'
    const bowl = document.createElement('span')
    bowl.className = 'bowl'
    const who = document.createElement('span')
    who.className = 'who'
    who.append(spriteToCanvas(species.sprite, species.palettes[0] as string[], 3))
    art.append(bowl, who)

    const ident = document.createElement('div')
    ident.className = 'ident'
    const badge = document.createElement('span')
    badge.className = 'ebadge mono'
    badge.dataset['element'] = species.element
    badge.textContent = 'BOSS'
    const name = document.createElement('span')
    name.className = 'name'
    name.textContent = BOSS_NAME
    // ⚠️ **文章で書かない。**「CT3の全体大技」と直書きしていて、
    //    枠1が CT0 になった後もその表示のままだった（実物と食い違う）
    const hint = document.createElement('span')
    hint.className = 'tags'
    hint.textContent = [species.skill1, ...(lord?.skills23 ?? [])]
      .map((id, slot) => {
        if (!id) return null
        const skill = skillById(id)
        const ct = effectiveCt(slot, skill)
        return `${slot === 0 ? '◆' : ''}${skill.name}${ct === 0 ? '' : `(CT${ct})`}`
      })
      .filter(Boolean)
      .join(' · ')
    const body = document.createElement('span')
    body.className = 'cid mono'
    body.textContent = `変異${lord?.mutationCounter ?? 0} · 素質${lord ? wildTotalOf(lord) : 0}`
    ident.append(badge, name, hint, body)

    const go = document.createElement('button')
    go.type = 'button'
    go.className = 'challenge'
    go.textContent = '挑戦'
    go.addEventListener('click', startBoss)

    row.append(art, ident, go)
    return row
  }

  function startBoss(): void {
    clearBattle()
    setTitle(BOSS_NAME)
    element.replaceChildren()

    const party = partyOf(game)
    if (party.length === 0) {
      paintList()
      return
    }

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

  function sumOf(egg: Egg): number {
    return Object.values(egg.wild).reduce((s, v) => s + v, 0)
  }

  /** ⭐ 巣に入るとまず発射フェーズ。届けば強奪、外せば戦闘。 */
  function enterNest(nest: Nest): void {
    clearBattle()
    setTitle(nest.name)
    element.replaceChildren()

    const party = partyOf(game)
    if (party.length === 0) {
      paintList()
      return
    }

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
    setTitle(nest.name)
    element.replaceChildren()

    const why2 = document.createElement('p')
    why2.className = 'hint'
    why2.textContent = why === 'blocked' ? '親にぶつかった。' : '届かなかった。'
    element.append(why2)

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
