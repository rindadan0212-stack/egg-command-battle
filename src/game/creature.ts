/** 個体。
 *
 *  ⚠️ **導出できるものは保存しない**（教訓 §4.5）。
 *  スキル枠1は種族固定なので**個体に持たせない** — 持たせると種族と食い違いうる
 *  第2の出所になる。実値も同じ理由で保存せず、毎回 stats.ts で計算する。
 */

import { skillById, type Skill, type SkillId } from './skills.ts'
import { speciesById, type Species } from './species.ts'
import { actualStats, totalOf, type StatBlock } from './stats.ts'

export type CreatureId = string

export interface Creature {
  readonly id: CreatureId
  readonly speciesId: string
  /** 遺伝で決まる素質。**変えられない。**合計上限は適用済みの値だけを入れる */
  readonly wild: StatBlock
  /** 育成でプレイヤーが振った分 */
  readonly trained: StatBlock
  /** 変異カウンタ。⚠️ 両親とも20以上だと子に変異が出ない（無限強化のブレーキ） */
  readonly mutationCounter: number
  /** 枠2・3 のみ。⚠️ 枠1は種族から導出する */
  readonly skills23: readonly [SkillId | null, SkillId | null]
  /** 種族のパレット添字。変異は色変化として出る */
  readonly paletteIndex: number
  readonly parents: readonly [CreatureId, CreatureId] | null
  readonly generation: number
}

export function speciesOf(creature: Creature): Species {
  return speciesById(creature.speciesId)
}

/** 3枠ぶんのスキル。⭐ 枠1は必ず種族のもの。 */
export function skillsOf(creature: Creature): readonly [Skill, Skill | null, Skill | null] {
  const species = speciesOf(creature)
  const [second, third] = creature.skills23
  return [
    skillById(species.skill1),
    second === null ? null : skillById(second),
    third === null ? null : skillById(third),
  ]
}

/** 実値。**唯一の出所は stats.ts。**ここは種族基礎を渡すだけ。 */
export function statsOf(creature: Creature): StatBlock {
  return actualStats(speciesOf(creature).base, creature.wild, creature.trained)
}

/** 野生レベルの合計。厳選の目安として並べ替えに使う。 */
export function wildTotalOf(creature: Creature): number {
  return totalOf(creature.wild)
}

/** その個体のパレット。添字が範囲外なら黙って通常色にせず投げる。 */
export function paletteOf(creature: Creature): readonly string[] {
  const species = speciesOf(creature)
  const palette = species.palettes[creature.paletteIndex]
  if (!palette) {
    throw new Error(`${species.id} にパレット添字 ${creature.paletteIndex} が無い`)
  }
  return palette
}
