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

  const lead = document.createElement('p')
  lead.className = 'lead'

  const note = document.createElement('p')
  note.className = 'note'
  note.textContent =
    '2体を選ぶ。各ステは独立に、高いほうの親が 55%。変異は 2.5% を3回。◆は種族固定のスキル1。'

  const preview = document.createElement('div')
  preview.className = 'breedpreview'

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

  function paint(): void {
    lead.textContent = `配合する2体を選ぶ（${picked.length}/2）`
    list.replaceChildren(...game.storage.creatures.map(buildCard))
    preview.replaceChildren()

    if (picked.length !== 2) return

    const [a, b] = picked.map((id) => game.storage.creatures.find((c) => c.id === id) as Creature)
    if (!a || !b) return
    const info = previewOf(a, b)

    const rows = document.createElement('div')
    rows.className = 'note mono'
    rows.textContent =
      `子の種族: ${info.species.join(' か ')}` +
      ` / 技の候補: ${info.skillPool.join('・') || '（親に技が無い）'}` +
      ` / 変異: ${info.mutable ? 'あり得る' : `両親とも変異${MUTATION_COUNTER_LIMIT}以上なので出ない`}`

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
      said.className = 'lead'
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
  root.append(lead, note, preview, list)
}
