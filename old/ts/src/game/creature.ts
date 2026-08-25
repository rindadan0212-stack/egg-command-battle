/** 個体。
 *
 *  ⚠️ **導出できるものは保存しない**（教訓 §4.5）。
 *  スキル枠1は種族固定なので**個体に持たせない** — 持たせると種族と食い違いうる
 *  第2の出所になる。実値も同じ理由で保存せず、毎回 stats.ts で計算する。
 */

import { skillById, type Skill, type SkillId } from './skills.ts'
import { speciesById, type Species } from './species.ts'
import { actualStats, totalOf, type StatBlock, type StatKey } from './stats.ts'

export type CreatureId = string

export interface Creature {
  readonly id: CreatureId
  readonly speciesId: string
  /** 遺伝で決まる素質。**変えられない。**合計上限は適用済みの値だけを入れる */
  readonly wild: StatBlock
  /** 育成でプレイヤーが振った分。
   *  ⚠️ 個体の中でここと `earned` だけが書き換わる。素質は変えられない */
  trained: StatBlock
  /** 戦闘で得た育成ポイントの総数（振った分 + 未使用）。 */
  earned: number
  /** 変異カウンタ。⚠️ 両親とも20以上だと子に変異が出ない（無限強化のブレーキ） */
  readonly mutationCounter: number
  /** 枠2・3 のみ。⚠️ 枠1は種族から導出する */
  readonly skills23: readonly [SkillId | null, SkillId | null]
  /** 種族のパレット添字。変異は色変化として出る */
  readonly paletteIndex: number
  readonly parents: readonly [CreatureId, CreatureId] | null
  readonly generation: number
}

/** 育成ポイントの上限。
 *
 *  ⭐ 戦闘に勝つ（または盗みに成功する）と、出撃していた個体が +1 もらう。
 *  「**連れ出す**」ことが育成に直結するので、強い個体を使うほど伸びる。
 *  ⚠️ 上限があるので、時間さえかければ素質差を埋められる、にはならない
 *  （素質＝厳選の成果が勝敗を決める、という軸を守るため）。 */
export const TRAIN_MAX = 20

export function speciesOf(creature: Creature): Species {
  return speciesById(creature.speciesId)
}

export function spentOf(creature: Creature): number {
  return totalOf(creature.trained)
}

/** まだ振っていない育成ポイント。 */
export function unspentOf(creature: Creature): number {
  return creature.earned - spentOf(creature)
}

/** 戦闘の報酬。⚠️ 上限を超えて溜めない。 */
export function award(creature: Creature, amount: number): void {
  creature.earned = Math.min(TRAIN_MAX, creature.earned + amount)
}

/** 1点を振る。⚠️ 戻せない（取り返しがつかないほうが判断に重みが出る）。 */
export function spendPoint(creature: Creature, key: StatKey): void {
  if (unspentOf(creature) <= 0) throw new Error(`${creature.id} に振れる育成ポイントが無い`)
  creature.trained = { ...creature.trained, [key]: creature.trained[key] + 1 }
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
