/** 保管庫。**枠は有限。** どれを逃がすかの整理が遊びになる。
 *
 *  ⭐ 50枠にしたのは、4ステぶんの専門親を数体ずつ + 世代管理の余裕が持てる下限だから。
 *  20枠だと ARK 型の「専門親を複数持つ」遊びが成立せず、
 *  100枠だと捨てる判断が生まれずリストが膨れるだけになる。
 */

import type { Creature, CreatureId } from './creature.ts'
import { statsOf, wildTotalOf } from './creature.ts'
import { STAT_KEYS, type StatKey } from './stats.ts'

export const STORAGE_SLOTS = 50

export interface Storage {
  readonly slots: number
  readonly creatures: readonly Creature[]
}

export function emptyStorage(): Storage {
  return { slots: STORAGE_SLOTS, creatures: [] }
}

export function isFull(storage: Storage): boolean {
  return storage.creatures.length >= storage.slots
}

/** ⚠️ 満杯を黙って捨てない。呼び側に「どれを逃がすか」を決めさせる。 */
export function accept(storage: Storage, creature: Creature): Storage {
  if (isFull(storage)) {
    throw new Error(`保管庫が満杯（${storage.slots}枠）。先にどれかを逃がす`)
  }
  if (storage.creatures.some((c) => c.id === creature.id)) {
    throw new Error(`${creature.id} は既に保管庫にいる`)
  }
  return { ...storage, creatures: [...storage.creatures, creature] }
}

export function release(storage: Storage, id: CreatureId): Storage {
  const next = storage.creatures.filter((c) => c.id !== id)
  if (next.length === storage.creatures.length) {
    throw new Error(`${id} は保管庫にいない`)
  }
  return { ...storage, creatures: next }
}

export const SORT_KEYS = ['wildTotal', ...STAT_KEYS, 'generation', 'mutation'] as const
export type SortKey = (typeof SORT_KEYS)[number]

export const SORT_LABELS: Readonly<Record<SortKey, string>> = {
  wildTotal: '素質合計',
  hp: 'HP',
  atk: '攻撃',
  def: '防御',
  spd: '速度',
  generation: '世代',
  mutation: '変異',
}

function sortValue(creature: Creature, key: SortKey): number {
  switch (key) {
    case 'wildTotal':
      return wildTotalOf(creature)
    case 'generation':
      return creature.generation
    case 'mutation':
      return creature.mutationCounter
    default:
      return creature.wild[key satisfies StatKey]
  }
}

/** 降順。同値は id で安定させる（並びが実行ごとに変わると比較できない）。 */
export function sorted(storage: Storage, key: SortKey): readonly Creature[] {
  return [...storage.creatures].sort((a, b) => {
    const diff = sortValue(b, key) - sortValue(a, key)
    return diff !== 0 ? diff : a.id.localeCompare(b.id)
  })
}

/** 画面に出す1行ぶんの要約。実値は毎回 statsOf で引く（保存しない）。 */
export function summarize(creature: Creature): {
  wildTotal: number
  actual: ReturnType<typeof statsOf>
} {
  return { wildTotal: wildTotalOf(creature), actual: statsOf(creature) }
}
