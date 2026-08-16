/** 配合の画面。
 *
 *  ⭐ 見せたいのは「**この2体を掛けると何が起こりうるか**」。
 *  厳選の判断はそこで決まるので、種族・技のプール・変異の可否を先に出す。
 */

import { previewOf } from '../game/breeding.ts'
import { paletteOf, skillsOf, speciesOf, wildTotalOf, type Creature } from '../game/creature.ts'
import { MUTATION_COUNTER_LIMIT } from '../game/breeding.ts'
import { breedPair, type Game } from '../game/state.ts'
import { STAT_KEYS, STAT_LABELS, wildTotalMaxFor } from '../game/stats.ts'
import { spriteToCanvas } from '../render/sprite.ts'

export function renderBreed(root: HTMLElement, game: Game, onChange: () => void): void {
  const picked: string[] = []

  // ⭐ モックの Breed そのまま: **上に2体の枠と結果、下に相手を選ぶリスト。**
  //    「今なにを掛けようとしているか」が常に上に出ていないと、選ぶ判断ができない
  const bench = document.createElement('div')
  bench.className = 'bench'

  const preview = document.createElement('div')
  preview.className = 'breedpreview'

  const pickLabel = document.createElement('p')
  pickLabel.className = 'sheetlabel mono'
  pickLabel.textContent = 'SELECT PARTNER'

  const list = document.createElement('div')
  list.className = 'roster picker'

  function buildCard(creature: Creature): HTMLElement {
    const species = speciesOf(creature)
    const card = document.createElement('button')
    card.type = 'button'
    card.className = 'pick'
    card.id = `p-${creature.id}`
    card.dataset['on'] = String(picked.includes(creature.id))

    const art = document.createElement('span')
    art.className = 'art'
    art.append(spriteToCanvas(species.sprite, paletteOf(creature), 2))

    const body = document.createElement('span')
    body.className = 'pickbody'
    const top = document.createElement('span')
    top.className = 'name'
    top.textContent = `${species.name} ${creature.id}`
    const stats = document.createElement('span')
    stats.className = 'tags mono'
    stats.textContent = STAT_KEYS.map((k) => `${STAT_LABELS[k]}${creature.wild[k]}`).join(' ')
    const meta = document.createElement('span')
    meta.className = 'cid mono'
    const [s1, s2, s3] = skillsOf(creature)
    meta.textContent =
      `素質${wildTotalOf(creature)}/${wildTotalMaxFor(creature.mutationCounter)}` +
      ` · 第${creature.generation}世代 · 変異${creature.mutationCounter}` +
      ` · ◆${s1.name}·${s2?.name ?? '—'}·${s3?.name ?? '—'}`
    body.append(top, stats, meta)

    card.append(art, body)
    card.addEventListener('click', () => {
      const at = picked.indexOf(creature.id)
      if (at >= 0) picked.splice(at, 1)
      else if (picked.length < 2) picked.push(creature.id)
      else picked.splice(0, 1, creature.id)
      paint()
    })
    return card
  }

  /** 上の2枠。空きも枠として出す（何体選べばいいかが形で分かる） */
  function buildSlot(index: number): HTMLElement {
    const id = picked[index]
    const creature = id ? game.storage.creatures.find((c) => c.id === id) : undefined
    const slot = document.createElement('div')
    slot.className = 'bslot'
    slot.dataset['filled'] = String(Boolean(creature))
    if (!creature) {
      const hint = document.createElement('span')
      hint.className = 'tags'
      hint.textContent = `親 ${index + 1}`
      slot.append(hint)
      return slot
    }
    const species = speciesOf(creature)
    const art = document.createElement('div')
    art.className = 'art'
    art.append(spriteToCanvas(species.sprite, paletteOf(creature), 3))
    const name = document.createElement('span')
    name.className = 'name'
    name.textContent = species.name
    const meta = document.createElement('span')
    meta.className = 'cid mono'
    meta.textContent = `${creature.id} · 素質${wildTotalOf(creature)}`
    const s1 = skillsOf(creature)[0]
    const skill = document.createElement('span')
    skill.className = 'tags'
    skill.textContent = `◆${s1.name}`
    slot.append(art, name, meta, skill)
    return slot
  }

  function paint(): void {
    list.replaceChildren(...game.storage.creatures.map(buildCard))
    preview.replaceChildren()

    const cross = document.createElement('span')
    cross.className = 'cross'
    cross.textContent = '+'
    bench.replaceChildren(buildSlot(0), cross, buildSlot(1))

    if (picked.length !== 2) {
      const wait = document.createElement('p')
      wait.className = 'note'
      wait.textContent =
        '2体を選ぶ。各ステは独立に、高いほうの親が 55%。変異は 2.5% を3回。◆は種族固定のスキル1。'
      preview.append(wait)
      return
    }

    const [a, b] = picked.map((id) => game.storage.creatures.find((c) => c.id === id) as Creature)
    if (!a || !b) return
    const info = previewOf(a, b)

    const rows = document.createElement('div')
    rows.className = 'eggpreview'
    const head = document.createElement('span')
    head.className = 'sheetlabel mono'
    head.textContent = 'RESULT EGG'
    const body = document.createElement('span')
    body.className = 'note mono'
    body.textContent =
      `種族 ${info.species.join(' か ')}` +
      ` / 技 ${info.skillPool.join('・') || '（親に技が無い）'}` +
      ` / 変異 ${info.mutable ? 'あり得る' : `両親とも変異${MUTATION_COUNTER_LIMIT}以上なので出ない`}`
    rows.append(head, body)

    const go = document.createElement('div')
    go.className = 'controls'
    const button = document.createElement('button')
    button.type = 'button'
    button.dataset['on'] = 'true'
    button.textContent = '配合する'
    button.addEventListener('click', () => {
      const outcome = breedPair(game, a.id, b.id)
      picked.length = 0
      onChange()
      paint()
      const said = document.createElement('p')
      said.className = 'title'
      said.textContent =
        outcome.mutations > 0
          ? `卵ができた（${outcome.egg.id}）— ⭐ 変異 ${outcome.mutations} 回`
          : `卵ができた（${outcome.egg.id}）`
      preview.prepend(said)
    })
    go.append(button)

    preview.append(rows, go)
  }

  paint()
  root.append(bench, preview, pickLabel, list)
}
