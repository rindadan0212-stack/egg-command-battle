/** 🚧 仮の個体づくり。**段C（卵と孵化）で本物に差し替えたら、このファイルごと消す。**
 *  跡地を残さないよう、参照しているのは画面の初期化だけにしてある。
 *
 *  目的は「個体を作ること」ではなく、**保管庫の画面が実物で検分できる状態**にすること。
 *  合計上限の削りが特化を保つかも、ここで作った個体を見れば目で確かめられる。
 */

import type { Rng } from '../core/rng.ts'
import type { Creature } from './creature.ts'
import { GACHA_POOL, type SkillId } from './skills.ts'
import { SPECIES_LIST } from './species.ts'
import { applyTotalCap, STAT_KEYS, WILD_STAT_MAX, WILD_TOTAL_MAX, type StatKey } from './stats.ts'

/** 「得意を2つ作れる」が本当に作れるかを目で見たいので、狙いを分けて生成する。 */
const BUILDS = ['specialist', 'duo', 'even'] as const

function rollWild(rng: Rng): Record<StatKey, number> {
  const build = rng.pick(BUILDS)
  const keys = rng.shuffle([...STAT_KEYS])
  const raw: Record<StatKey, number> = { hp: 0, atk: 0, def: 0, spd: 0 }

  if (build === 'specialist') {
    raw[keys[0] as StatKey] = rng.int(WILD_STAT_MAX - 6, WILD_STAT_MAX + 1)
    raw[keys[1] as StatKey] = rng.int(0, 20)
    raw[keys[2] as StatKey] = rng.int(0, 12)
    raw[keys[3] as StatKey] = rng.int(0, 8)
  } else if (build === 'duo') {
    raw[keys[0] as StatKey] = rng.int(26, WILD_STAT_MAX + 1)
    raw[keys[1] as StatKey] = rng.int(22, WILD_STAT_MAX + 1)
    raw[keys[2] as StatKey] = rng.int(0, 14)
    raw[keys[3] as StatKey] = rng.int(0, 10)
  } else {
    for (const key of STAT_KEYS) raw[key] = rng.int(12, 26)
  }

  // ⚠️ 上限は必ずここを通す。画面側で別に丸めない（強さの出所を2箇所にしないため）
  return applyTotalCap(raw) as Record<StatKey, number>
}

function rollSkills23(rng: Rng, skill1: SkillId): readonly [SkillId | null, SkillId | null] {
  // ⚠️ 枠1（種族固定）と同じスキルを枠2・3に出さない。
  // 出すと同じ技が2枠を占めて片方が無駄になる。
  // 🚧 本番でも同じ扱いにするかは 課題.md「仕様の空白」に上げてある（段C で決める）
  const pool = GACHA_POOL.filter((id) => id !== skill1)
  const picked = rng.sample(pool, 2)
  // 枠が埋まっていない個体も出しておく（画面が欠けたときに崩れないか見るため）
  const filled = rng.chance(0.8)
  return [picked[0] ?? null, filled ? (picked[1] ?? null) : null]
}

export function makeProvisionalRoster(rng: Rng, count: number): Creature[] {
  const out: Creature[] = []

  for (let i = 0; i < count; i++) {
    const species = rng.pick(SPECIES_LIST)
    const mutationCounter = rng.chance(0.35) ? rng.int(1, 21) : 0
    const paletteIndex =
      mutationCounter > 0 && species.palettes.length > 1 ? rng.int(1, species.palettes.length) : 0

    out.push({
      id: `c${String(i + 1).padStart(3, '0')}`,
      speciesId: species.id,
      wild: rollWild(rng),
      trained: { hp: 0, atk: 0, def: 0, spd: 0 },
      mutationCounter,
      skills23: rollSkills23(rng, species.skill1),
      paletteIndex,
      parents: null,
      generation: rng.int(1, 6),
    })
  }

  return out
}

/** 画面の目盛りに使う。ここを画面側で直書きしない。 */
export const WILD_SCALE = { statMax: WILD_STAT_MAX, totalMax: WILD_TOTAL_MAX }
