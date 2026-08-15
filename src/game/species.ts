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
 *
 *  ⚠️ **スキル1がどのステで伸びるかは、そのステを二重に得にする。**
 *  タマルの殻打ちは防御スケールなので、防御が「守り」と「攻め」を兼ねる。
 *  1種族しか無かったとき、これが釣り合いの計測を丸ごと濁らせた（実測で発覚）。
 *  種族ごとに違うステへ寄せてある。
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

// ── 意匠 ───────────────────────────────────────────
// 文字の格子で持つ。テキストのまま人が手で直せて、HMR で即ギャラリーに出る。
// 1=輪郭 2=体 3=差し色 4=目

/** タマル — 丸い。殻を思わせる。 */
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

/** ツノガ — 角がある。輪郭が角張っている。 */
const TSUNOGA_SPRITE = parseSprite([
  '................',
  '..1..........1..',
  '..11........11..',
  '...11......11...',
  '...111....111...',
  '....11111111....',
  '...1133222211...',
  '..113322222211..',
  '.11244222244211.',
  '.11244222244211.',
  '.11222222222211.',
  '..112222222211..',
  '...1122222211...',
  '....11222211....',
  '.....111111.....',
  '................',
])

/** ハネル — 菱形の体に、端まで届く羽。
 *  ⚠️ 最初は体を小さくして羽を離していたが、実寸(32px)で見たら
 *  散った点にしか見えなかった。羽を端まで繋いで面で読ませる。 */
const HANERU_SPRITE = parseSprite([
  '................',
  '................',
  '................',
  '......1111......',
  '.....112211.....',
  '....11222211....',
  '..331122221133..',
  '3333114224113333',
  '..331122221133..',
  '....11222211....',
  '.....112211.....',
  '......1111......',
  '................',
  '................',
  '................',
  '................',
])

/** ヌシ — 角を持つ重い体。枠いっぱいに構える。 */
const NUSHI_SPRITE = parseSprite([
  '................',
  '...1........1...',
  '..121......121..',
  '..1221....1221..',
  '.11222111122211.',
  '.12222222222221.',
  '.12233222332221.',
  '.12244222442221.',
  '.12244222442221.',
  '.12222222222221.',
  '.12222222222221.',
  '.11222222222211.',
  '..112222222211..',
  '...1122222211...',
  '....11111111....',
  '................',
])

const TAMARU_PALETTES: readonly Palette[] = [
  ['#2e2418', '#8fc96e', '#c8eaa8', '#1a1410'], // 通常
  ['#1c2436', '#6e9ec9', '#a8cbea', '#101418'], // 変異・蒼
  ['#361c22', '#c96e7f', '#eaa8b4', '#181012'], // 変異・紅
  ['#2e2a18', '#c9bd6e', '#eae0a8', '#1a1810'], // 変異・金
]

const TSUNOGA_PALETTES: readonly Palette[] = [
  ['#2a1a14', '#c97a52', '#eab48c', '#160e0a'], // 通常
  ['#141a2a', '#5273c9', '#8c9eea', '#0a0e16'], // 変異・蒼
  ['#2a1420', '#c95293', '#ea8cc4', '#160a12'], // 変異・紅
  ['#1a2a18', '#63c952', '#98ea8c', '#0e160a'], // 変異・翠
]

const HANERU_PALETTES: readonly Palette[] = [
  ['#241c2e', '#a98fc9', '#ded0ea', '#141018'], // 通常
  ['#1c2e2a', '#8fc9bd', '#d0eae4', '#101816'], // 変異・碧
  ['#2e2418', '#c9b48f', '#eae0d0', '#181410'], // 変異・砂
  ['#2e1c1c', '#c98f8f', '#ead0d0', '#181010'], // 変異・灰紅
]

/** ⚠️ ボスは重く見せたいので、明部を抑えて沈んだ色にする。 */
const NUSHI_PALETTES: readonly Palette[] = [
  ['#14100c', '#6b5a3e', '#9c8759', '#e8d48a'], // 通常（目だけ光る）
  ['#0c1014', '#3e556b', '#59839c', '#8ac8e8'], // 変異・蒼
]

/** ⚠️ スキル1 のスケール元をわざと散らしてある（防御 / 攻撃 / 攻撃だが全体攻撃）。
 *  全種族が同じステでスケールすると、そのステだけが二重に得になる。 */
const LIST: readonly Species[] = [
  {
    id: 'tamaru',
    name: 'タマル',
    element: 'scale',
    skill1: 'shellbash', // 防御スケール
    base: { hp: 24, atk: 18, def: 22, spd: 16 },
    sprite: TAMARU_SPRITE,
    palettes: TAMARU_PALETTES,
  },
  {
    id: 'tsunoga',
    name: 'ツノガ',
    element: 'fang',
    skill1: 'strike', // 攻撃スケール・単体
    base: { hp: 22, atk: 24, def: 18, spd: 16 },
    sprite: TSUNOGA_SPRITE,
    palettes: TSUNOGA_PALETTES,
  },
  {
    id: 'haneru',
    name: 'ハネル',
    element: 'plume',
    skill1: 'sweep', // 攻撃スケール・全体
    base: { hp: 20, atk: 18, def: 16, spd: 26 },
    sprite: HANERU_SPRITE,
    palettes: HANERU_PALETTES,
  },
  {
    // ⚠️ ボス専用。巣は持たないので卵からは出ない
    id: 'nushi',
    name: 'ヌシ',
    // ⚠️ 3すくみは 牙 → 羽 → 鱗 → 牙。鱗に有利を取るのは **羽（ハネル）**。
    //    ここを「牙が有利」と読み違えて検証編成を組み、測り損ねた。
    element: 'scale',
    skill1: 'quake', // CT7 の全体大技
    base: { hp: 26, atk: 20, def: 24, spd: 10 },
    sprite: NUSHI_SPRITE,
    palettes: NUSHI_PALETTES,
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
    if (species.sprite.width !== 16 || species.sprite.height !== 16) {
      problems.push(
        `${species.id}: 意匠が ${species.sprite.width}×${species.sprite.height}（16×16 に揃える）`,
      )
    }
  }

  const ids = new Set(LIST.map((s) => s.id))
  if (ids.size !== LIST.length) problems.push('種族 id が重複している')

  // 属性が3すくみを覆えているか。⚠️ 覆えていないと、有利不利が一方通行になる
  const covered = new Set(LIST.map((s) => s.element))
  const missing = ELEMENTS.filter((e) => !covered.has(e))
  if (missing.length > 0 && LIST.length >= ELEMENTS.length) {
    problems.push(`使われていない属性: ${missing.join(', ')}`)
  }

  if (problems.length > 0) {
    throw new Error(`種族表の不備:\n  ${problems.join('\n  ')}`)
  }
}
