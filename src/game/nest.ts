/** 巣と卵。
 *
 *  ⭐ **強い親ほど良い卵**。これが難易度と報酬を自動で結ぶので、
 *  報酬テーブルを別に設計しなくてよい。
 *
 *  ⭐ 巣では二択:
 *  | 親を倒す | 確実に奪える。良い卵。ただし勝てる相手に限る |
 *  | 盗んで逃げる | **格上の巣でも狙える**が、失敗のリスクがある |
 *
 *  これで「まだ勝てない巣に挑む」動機が生まれ、輪の駆動力になる。
 */

import type { Rng } from '../core/rng.ts'
import type { Creature, CreatureId } from './creature.ts'
import { gachaPoolOf, type SkillId } from './skills.ts'
import { speciesById, type SpeciesId } from './species.ts'
import { applyTotalCap, STAT_KEYS, WILD_STAT_MAX, WILD_TOTAL_MAX, type StatBlock } from './stats.ts'

export interface Nest {
  readonly id: string
  readonly name: string
  readonly speciesId: SpeciesId
  /** 段階。高いほど親が強く、落とす卵も良い */
  readonly tier: number
}

/** 段階ごとの、親が持つ野生レベルの合計。
 *  ⚠️ 上限 80 に届くのは最上位だけ。そこまで行くと配合でしか伸ばせなくなる。 */
export function wildTotalForTier(tier: number): number {
  const table = [24, 38, 52, 66, WILD_TOTAL_MAX]
  return table[Math.max(0, Math.min(table.length - 1, tier - 1))] as number
}

export const NESTS: readonly Nest[] = [
  { id: 'shallow-scale', name: '浅瀬の巣', speciesId: 'tamaru', tier: 1 },
  { id: 'thicket-fang', name: '藪の巣', speciesId: 'tsunoga', tier: 2 },
  { id: 'cliff-plume', name: '崖の巣', speciesId: 'haneru', tier: 3 },
  { id: 'deep-scale', name: '深みの巣', speciesId: 'tamaru', tier: 4 },
  { id: 'peak-fang', name: '嶺の巣', speciesId: 'tsunoga', tier: 5 },
]

export function nestById(id: string): Nest {
  const nest = NESTS.find((n) => n.id === id)
  if (!nest) throw new Error(`巣の表に ${id} が無い`)
  return nest
}

/** 合計 total を4ステへ配る。偏らせたいので1〜2箇所に寄せる。 */
function spreadWild(rng: Rng, total: number): StatBlock {
  const keys = rng.shuffle([...STAT_KEYS])
  const raw: Record<string, number> = { hp: 0, atk: 0, def: 0, spd: 0 }
  let left = total
  // 上位2つに多く配り、残りを下位へ。⭐ 野生も「得意2つ」の形にする
  const shares = [0.42, 0.32, 0.16, 0.1]
  keys.forEach((key, i) => {
    const want = i === keys.length - 1 ? left : Math.round(total * (shares[i] as number))
    const give = Math.max(0, Math.min(WILD_STAT_MAX, Math.min(want, left)))
    raw[key] = give
    left -= give
  })
  return applyTotalCap(raw as unknown as StatBlock)
}

function rollSkills23(
  rng: Rng,
  speciesId: SpeciesId,
  skill1: SkillId,
): readonly [SkillId | null, SkillId | null] {
  const pool = gachaPoolOf(speciesId, skill1)
  const picked = rng.sample(pool, Math.min(2, pool.length))
  return [picked[0] ?? null, picked[1] ?? null]
}

/** 巣の守り手3体。親1体 + 見張り2体（見張りは少し弱い）。 */
export function makeNestDefenders(rng: Rng, nest: Nest): Creature[] {
  const species = speciesById(nest.speciesId)
  const total = wildTotalForTier(nest.tier)

  return [0, 1, 2].map((i) => {
    // 親（0番）が最も強い。見張りは 7割ほど
    const share = i === 0 ? 1 : 0.7
    return {
      id: `${nest.id}-${i}`,
      speciesId: nest.speciesId,
      wild: spreadWild(rng, Math.round(total * share)),
      trained: { hp: 0, atk: 0, def: 0, spd: 0 },
      earned: 0,
      mutationCounter: 0,
      skills23: rollSkills23(rng, nest.speciesId, species.skill1),
      paletteIndex: 0,
      parents: null,
      generation: 1,
    }
  })
}

export interface Egg {
  readonly id: string
  readonly speciesId: SpeciesId
  readonly wild: StatBlock
  readonly mutationCounter: number
  readonly paletteIndex: number
  readonly parents: readonly [CreatureId, CreatureId] | null
  readonly generation: number
  /** どうやって手に入れたか。盗んだ卵はやや劣る */
  readonly how: 'defeated' | 'stolen' | 'bred'
  /** ⭐ **null なら孵すときにガチャで決まる**（野生の卵）。
   *  値が入っていれば配合で既に決まっている（両親の4枠から抽選済み）。
   *  ⚠️ ここを区別しないと、配合で狙って引いた技を孵化時に引き直してしまう。 */
  readonly skills23: readonly [SkillId | null, SkillId | null] | null
}

/** 親から卵を作る。
 *  ⚠️ 盗んだ卵は素質が落ちる。倒したほうが良い卵、という企画どおりにするため。 */
export function makeEgg(rng: Rng, nest: Nest, how: Egg['how'], serial: number): Egg {
  const base = wildTotalForTier(nest.tier)
  const quality = how === 'defeated' ? 1 : 0.78
  const jitter = rng.int(-3, 4)
  const total = Math.max(4, Math.round(base * quality) + jitter)

  return {
    id: `e${String(serial).padStart(3, '0')}`,
    speciesId: nest.speciesId,
    wild: spreadWild(rng, Math.min(WILD_TOTAL_MAX, total)),
    mutationCounter: 0,
    paletteIndex: 0,
    parents: null,
    generation: 1,
    how,
    skills23: null, // 野生の卵。孵すときにガチャ
  }
}

/** 孵す。⭐ 野生の卵はここで**スキル2・3のガチャを引く**。
 *  配合の卵は既に決まっているのでそのまま使う。 */
export function hatch(rng: Rng, egg: Egg, id: CreatureId): Creature {
  const species = speciesById(egg.speciesId)
  return {
    id,
    speciesId: egg.speciesId,
    wild: egg.wild,
    trained: { hp: 0, atk: 0, def: 0, spd: 0 },
    earned: 0,
    mutationCounter: egg.mutationCounter,
    skills23: egg.skills23 ?? rollSkills23(rng, egg.speciesId, species.skill1),
    paletteIndex: egg.paletteIndex,
    parents: egg.parents,
    generation: egg.generation,
  }
}

// ── ボス ─────────────────────────────────────────

/** 最後の壁。⭐ **手で書いた固定の相手**にしてある。
 *
 *  巣の守り手は挑むたびに顔ぶれが変わるが、ボスは毎回同じ。
 *  ⭐ そうしないと「**何が足りないか考えて、配合で作って、挑み直す**」という
 *  輪の駆動力が働かない（相手が毎回変わるなら対策の立てようがない）。
 *
 *  企画どおり、**速度操作・高防御・CT の長い大技**で戦略を要求する:
 *  - スキル1 は 震撼（CT7 の全体大技）。いつ来るかを数えさせる
 *  - 鈍足 で速度を奪う（＝盗みにくく、行動も回らなくなる）
 *  - 守勢 で防御を上げる（＝生半可な火力は通らない）
 *  - 属性は鱗。⚠️ 3すくみは 牙 → 羽 → 鱗 → 牙 なので、
 *    **羽（ハネル）が有利**を取れる。対策の道は用意されている
 */
export const BOSS_NAME = '淵のヌシ'

export function makeBossParty(): Creature[] {
  const unit = (
    i: number,
    speciesId: SpeciesId,
    wild: StatBlock,
    skills23: readonly [SkillId, SkillId],
    mut = 0,
  ): Creature => ({
    id: `boss-${i}`,
    speciesId,
    wild: applyTotalCap(wild, mut),
    trained: { hp: 0, atk: 0, def: 0, spd: 0 },
    earned: 0,
    mutationCounter: mut,
    skills23,
    paletteIndex: 0,
    parents: null,
    generation: 1,
  })

  return [
    // ⭐ 変異を4回重ねた個体という扱い。上限が 44/88 に上がるので、
    //    ボス専用の例外ルールを足さずに強くできる。
    //
    // ⚠️ 最初 変異20（素質120）で作っていたが、**輪が閉じなかった**。
    // プレイヤーは素質80で頭打ちになり、変異は 7.31% でしか出ないので
    // 14周回しても勝率0%（実測）。さらに眷属2体を両方とも回復役にしていたため、
    // 素質120のパーティでも **186HP のボスに11ダメージ**しか通らなかった。
    //
    // ⭐ 企画は「ボスは単純なステータス勝負ではなく戦略を要求する存在」と言っている。
    // ステの壁は「素質80のパーティが**編成で**越えられる」高さに置き、
    // 難しさは 震撼(CT7の全体大技) / 鈍足 / 守勢 と、眷属の**役割の噛み合い**で作る。
    // ⚠️ 数値は掃引して決めた。ステを 0.85 倍する前は**どの編成も勝てなかった**。
    //    この戦闘系は集中攻撃と回復のせいで**崖**があり、
    //    1.00 で全滅 / 0.85 で越えられる、と急に切り替わる（実測）。
    // ⚠️ HP を厚くしすぎると、**ボスは最後にしか狙われない**ので
    //    「一度も触れないまま負ける」になる（残 HP 最少から狙う仕様のため）。
    //    敵3体の総 HP がパーティの出せる総ダメージを超えないところに置く。
    // ⭐ 震撼は枠2へ。枠1は CT が無いので、大技はここに置いて CT7 を効かせる
    unit(0, 'nushi', { hp: 16, atk: 22, def: 21, spd: 3 }, ['attack-all-heavy', 'spd-down'], 4),
    // 壁。
    // ⚠️ 回復役にしない（2枚重ねると持久戦になって削り切れない）。
    // ⚠️ **かばうも持たせない。** 敵側のかばうは高防御と噛み合って
    //    「ボスに一度も触れない」状態を作る（実測: 総ダメージ0のまま敗北）。
    //    これは「戦略を要求する」ではなく「無理」なので外した。
    unit(1, 'tamaru', { hp: 16, atk: 16, def: 21, spd: 3 }, ['def-up', 'attack-def']),
    // 撹乱。速い。⭐ ここを先に落とせるかが最初の関門
    unit(2, 'haneru', { hp: 11, atk: 24, def: 4, spd: 24 }, ['spd-up', 'spd-down']),
  ]
}

/** 巣の表に抜けが無いか数える検査。 */
export function auditNests(): void {
  const problems: string[] = []
  const ids = new Set(NESTS.map((n) => n.id))
  if (ids.size !== NESTS.length) problems.push('巣の id が重複している')
  for (const nest of NESTS) {
    // 存在しない種族を指していないか（指していると孵した瞬間に落ちる）
    const species = speciesById(nest.speciesId)
    if (gachaPoolOf(nest.speciesId, species.skill1).length === 0) {
      problems.push(`${nest.id}: 卵ガチャのプールが空`)
    }
    if (nest.tier < 1) problems.push(`${nest.id}: 段階が ${nest.tier}`)
  }
  if (problems.length > 0) throw new Error(`巣の表の不備:\n  ${problems.join('\n  ')}`)
}
