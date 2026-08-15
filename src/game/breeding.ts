/** 配合と遺伝。ARK 準拠。
 *
 *  | 要素 | 仕様 |
 *  |---|---|
 *  | 種族 | 50% でどちらかの親。**スキル1 はその種族のもの**（連動） |
 *  | ステ | **各ステ独立にロール。高いほうの親が 55%** |
 *  | 変異 | **2.5% を3回**振る。当たるごとに +2 と変異カウンタ +1 |
 *  | ブレーキ | 親どちらも変異カウンタ 20 以上なら変異しない |
 *  | スキル2・3 | 両親の4枠から2つ抽選（枠1と重なるものは除く） |
 *  | 合計上限 | 変異ぶん押し上げた上限で、超過は低いステから削る |
 *
 *  ⭐ **ステごとに独立ロールする**のが厳選の中毒性の源。
 *  「専門化した親を複数持って組み合わせる」遊びがここから生まれる。
 */

import type { Rng } from '../core/rng.ts'
import { skillsOf, speciesOf, type Creature } from './creature.ts'
import type { Egg } from './nest.ts'
import { gachaPoolOf, type SkillId } from './skills.ts'
import { speciesById } from './species.ts'
import { applyTotalCap, STAT_KEYS, type StatBlock, type StatKey } from './stats.ts'

/** 高いほうの親から取る確率。ARK 準拠。 */
export const INHERIT_HIGHER = 0.55

/** 変異の判定回数と1回あたりの確率。
 *
 *  ⭐ **2.5% を3回**振ると、ARK の公表値がそのまま出る:
 *  1回以上 = 1 - 0.975³ = **7.31%** / ちょうど2回 = 3×0.025²×0.975 = **0.183%**
 *  / 3回 = 0.025³ = **0.00156%**。
 *  個別に確率を置くより素直で、値が食い違う余地が無い。 */
export const MUTATION_ROLLS = 3
export const MUTATION_CHANCE = 0.025

/** 1回の変異で上がるレベル。 */
export const MUTATION_STEP = 2

/** ⚠️ 無限強化のブレーキ。**省略禁止。**
 *  親のどちらかがこの値未満でなければ、子に変異は出ない。 */
export const MUTATION_COUNTER_LIMIT = 20

export function canBreed(a: Creature, b: Creature): boolean {
  return a.id !== b.id
}

/** 変異が出うるか。 */
export function mutationAllowed(a: Creature, b: Creature): boolean {
  return a.mutationCounter < MUTATION_COUNTER_LIMIT || b.mutationCounter < MUTATION_COUNTER_LIMIT
}

/** 両親の6枠（種族スキル2 + 枠2・3が4）から、子の枠2・3を決める。
 *  ⚠️ 子の枠1（種族スキル）と重なるものは外す。同じ技が2枠を占めると片方が無駄になる。 */
function inheritSkills(
  rng: Rng,
  a: Creature,
  b: Creature,
  childSkill1: SkillId,
  childSpeciesId: string,
): readonly [SkillId | null, SkillId | null] {
  const pool = [...a.skills23, ...b.skills23].filter(
    (id): id is SkillId => id !== null && id !== childSkill1,
  )
  const unique = [...new Set(pool)]

  if (unique.length >= 2) {
    const picked = rng.sample(unique, 2)
    return [picked[0] ?? null, picked[1] ?? null]
  }

  // ⚠️ 親から2つ取れないときは、子の種族のプールから補う。
  //    空き枠のまま返すと、配合を重ねるほど技が痩せていく
  const fallback = gachaPoolOf(childSpeciesId, childSkill1).filter((id) => !unique.includes(id))
  const need = 2 - unique.length
  const extra = rng.sample(fallback, Math.min(need, fallback.length))
  const all = [...unique, ...extra]
  return [all[0] ?? null, all[1] ?? null]
}

export interface BreedOutcome {
  readonly egg: Egg
  /** この配合で出た変異の回数（0〜3） */
  readonly mutations: number
}

export function breed(rng: Rng, a: Creature, b: Creature, serial: number): BreedOutcome {
  if (!canBreed(a, b)) throw new Error('同じ個体どうしは配合できない')

  // ── 種族（スキル1 と連動する）
  const childSpecies = rng.chance(0.5) ? speciesOf(a) : speciesOf(b)

  // ── ステ: 各ステ独立に、高いほうの親が 55%
  const wild: Record<StatKey, number> = { hp: 0, atk: 0, def: 0, spd: 0 }
  for (const key of STAT_KEYS) {
    const high = a.wild[key] >= b.wild[key] ? a : b
    const low = high === a ? b : a
    wild[key] = (rng.chance(INHERIT_HIGHER) ? high : low).wild[key]
  }

  // ── 変異: 2.5% を3回。当たったステに +2
  let mutations = 0
  if (mutationAllowed(a, b)) {
    for (let i = 0; i < MUTATION_ROLLS; i++) {
      if (!rng.chance(MUTATION_CHANCE)) continue
      mutations++
      const key = rng.pick(STAT_KEYS)
      wild[key] += MUTATION_STEP
    }
  }

  const mutationCounter = Math.max(a.mutationCounter, b.mutationCounter) + mutations

  // ⚠️ 上限は変異ぶん押し上げたうえで掛ける。
  //    素の上限で掛けると、変異で足した +2 が即削られて価値が消える
  const capped = applyTotalCap(wild as unknown as StatBlock, mutationCounter)

  // ── 色: 変異が出たらパレットが変わる
  const paletteIndex =
    mutations > 0 && childSpecies.palettes.length > 1
      ? rng.int(1, childSpecies.palettes.length)
      : pickParentPalette(rng, a, b, childSpecies.id)

  const egg: Egg = {
    id: `e${String(serial).padStart(3, '0')}`,
    speciesId: childSpecies.id,
    wild: capped,
    mutationCounter,
    paletteIndex,
    parents: [a.id, b.id],
    generation: Math.max(a.generation, b.generation) + 1,
    how: 'bred',
    // ⭐ 配合の卵はここで技が決まる。孵すときに引き直さない
    skills23: inheritSkills(rng, a, b, childSpecies.skill1, childSpecies.id),
  }
  return { egg, mutations }
}

/** 変異が出なかったときの色。同じ種族の親から引き継ぐ。 */
function pickParentPalette(rng: Rng, a: Creature, b: Creature, speciesId: string): number {
  const sameSpecies = [a, b].filter((c) => c.speciesId === speciesId)
  if (sameSpecies.length === 0) return 0
  const source = sameSpecies.length === 1 ? sameSpecies[0] : rng.pick(sameSpecies)
  const index = source?.paletteIndex ?? 0
  const palettes = speciesById(speciesId).palettes.length
  return Math.min(index, palettes - 1)
}

/** 画面で「この2体を配合すると何が起こりうるか」を見せるための要約。 */
export function previewOf(
  a: Creature,
  b: Creature,
): { species: string[]; skillPool: string[]; mutable: boolean } {
  const speciesNames = [...new Set([speciesOf(a).name, speciesOf(b).name])]
  const pool = [...new Set([...skillsOf(a).slice(1), ...skillsOf(b).slice(1)])]
    .filter((s): s is NonNullable<typeof s> => s !== null)
    .map((s) => s.name)
  return { species: speciesNames, skillPool: pool, mutable: mutationAllowed(a, b) }
}
