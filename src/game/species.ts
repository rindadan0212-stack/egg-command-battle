/** 種族は「器」。中身（ステの野生レベル・スキル2/3）は種族から独立して流通する。
 *
 *  | 種族が決めるもの | 種族から独立して流通するもの |
 *  |---|---|
 *  | 見た目（ドット + パレット） | ステの野生レベル |
 *  | 属性（3すくみ） | スキル2・3 |
 *  | スキル1（種族固定枠） | 育成で振った分 |
 *  | 基礎値の**配分** | |
 *
 *  ⚠️ **種族ごとに基礎値の合計を変えない。**
 *  変えると最強種族に全部が集約され、種族の多様性が「どれを使うのが得か」という
 *  最適化問題に潰される。差は**配分と属性**で出す。
 */

import { parseSprite, type Palette, type Sprite } from '../render/sprite.ts'
import type { SkillId } from './skills.ts'
import { STAT_KEYS, totalOf, type StatBlock } from './stats.ts'

/** 3すくみ。牙 → 羽 → 鱗 → 牙。
 *  （牙は羽を裂き / 羽は鱗をかわし / 鱗は牙を弾く） */
export const ELEMENTS = ['fang', 'plume', 'scale'] as const
export type Element = (typeof ELEMENTS)[number]

export const ELEMENT_LABELS: Readonly<Record<Element, string>> = {
  fang: '牙',
  plume: '羽',
  scale: '鱗',
}

/** 有利を取る相手。 */
export const ELEMENT_BEATS: Readonly<Record<Element, Element>> = {
  fang: 'plume',
  plume: 'scale',
  scale: 'fang',
}

/** ⚠️ 全種族で揃える基礎値の合計。ここを種族ごとに変えない。 */
export const SPECIES_BASE_TOTAL = 80

export type SpeciesId = string

export interface Species {
  readonly id: SpeciesId
  readonly name: string
  readonly element: Element
  /** 種族固定のスキル枠1。 */
  readonly skill1: SkillId
  /** ⚠️ 合計は SPECIES_BASE_TOTAL に揃える。差は配分で出す */
  readonly base: StatBlock
  readonly sprite: Sprite
  /** 0 = 通常。1以降が変異色（ARK と同じく変異は色変化として出る）。 */
  readonly palettes: readonly Palette[]
}

/** 🚧 段A の1種族目。段E で3〜4種に増やす。
 *  意匠は文字の格子で持つ。テキストのまま人が手で直せて、HMR で即ギャラリーに出る。 */
const TAMARU_SPRITE = parseSprite([
  '................',
  '................',
  '.....111111.....',
  '...1122222211...',
  '..112222222211..',
  '.11332222222211.',
  '.12332222222221.',
  '.12222222222221.',
  '.12244222244221.',
  '.12244222244221.',
  '.12222222222221.',
  '.11222222222211.',
  '..112222222211..',
  '...1122222211...',
  '....11111111....',
  '................',
])

/** 1=輪郭 2=体 3=明部 4=目 */
const TAMARU_PALETTES: readonly Palette[] = [
  ['#2e2418', '#8fc96e', '#c8eaa8', '#1a1410'], // 通常
  ['#1c2436', '#6e9ec9', '#a8cbea', '#101418'], // 変異・蒼
  ['#361c22', '#c96e7f', '#eaa8b4', '#181012'], // 変異・紅
  ['#2e2a18', '#c9bd6e', '#eae0a8', '#1a1810'], // 変異・金
]

const LIST: readonly Species[] = [
  {
    id: 'tamaru',
    name: 'タマル',
    element: 'scale',
    skill1: 'shellbash',
    base: { hp: 24, atk: 18, def: 22, spd: 16 },
    sprite: TAMARU_SPRITE,
    palettes: TAMARU_PALETTES,
  },
]

export const SPECIES: ReadonlyMap<SpeciesId, Species> = new Map(LIST.map((s) => [s.id, s]))
export const SPECIES_LIST: readonly Species[] = LIST

/** 表に無い id を黙って握りつぶさない。
 *  ⚠️ 「型は通る・テストも通る・ただ効かなくなるだけ」が一番気づけない形なので、必ず投げる。 */
export function speciesById(id: SpeciesId): Species {
  const species = SPECIES.get(id)
  if (!species) throw new Error(`種族表に ${id} が無い`)
  return species
}

/** 全部を覆うつもりの表は、数える検査を持つ（教訓 §4.2）。
 *  起動時に1回走らせ、種族を足した日に黙って壊れないようにする。 */
export function auditSpecies(): void {
  const problems: string[] = []

  for (const species of LIST) {
    const total = totalOf(species.base)
    if (total !== SPECIES_BASE_TOTAL) {
      problems.push(`${species.id}: 基礎値の合計が ${total}（${SPECIES_BASE_TOTAL} に揃える）`)
    }
    for (const key of STAT_KEYS) {
      if (!Number.isInteger(species.base[key]) || species.base[key] < 0) {
        problems.push(`${species.id}: 基礎値 ${key} が ${species.base[key]}`)
      }
    }
    if (species.palettes.length === 0) {
      problems.push(`${species.id}: パレットが無い`)
    }
  }

  const ids = new Set(LIST.map((s) => s.id))
  if (ids.size !== LIST.length) problems.push('種族 id が重複している')

  // 属性が3すくみを覆えているか（種族が増えたとき片寄りに気づけるように数える）
  const covered = new Set(LIST.map((s) => s.element))
  const missing = ELEMENTS.filter((e) => !covered.has(e))
  if (missing.length > 0 && LIST.length >= ELEMENTS.length) {
    problems.push(`使われていない属性: ${missing.join(', ')}`)
  }

  if (problems.length > 0) {
    throw new Error(`種族表の不備:\n  ${problems.join('\n  ')}`)
  }
}
