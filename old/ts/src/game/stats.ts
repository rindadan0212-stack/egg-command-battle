/** ステータス。**強さの唯一の出所**。
 *
 *  ⚠️ 実値・上限・削りの計算をここ以外に書かない。
 *  戦闘・シミュレータ・画面が全部この関数を呼ぶ。
 *  同じことを2箇所で決めると、片方だけ直しても直らない不具合になる。
 */

export const STAT_KEYS = ['hp', 'atk', 'def', 'spd'] as const
export type StatKey = (typeof STAT_KEYS)[number]
export type StatBlock = Readonly<Record<StatKey, number>>

export const STAT_LABELS: Readonly<Record<StatKey, string>> = {
  hp: 'HP',
  atk: '攻撃',
  def: '防御',
  spd: '速度',
}

/** 1つのステに振れる野生レベルの上限。 */
export const WILD_STAT_MAX = 40

/** 野生レベルの合計上限。
 *
 *  ⭐ **= WILD_STAT_MAX × 2**。この比が「1体でいくつのステを伸ばせるか」を決めている。
 *  2倍にしたのは「**得意を2つ作れる**」を保証したかったから
 *  （得意2つ × 3体 = 6枠でパーティを組む）。
 *  1.5倍なら得意1つ、3倍なら万能個体ができて「全ステSSSをなくす」意図から外れる。 */
export const WILD_TOTAL_MAX = WILD_STAT_MAX * 2

/** 変異が上限を押し上げられる回数。⚠️ ここが血統全体の天井になる。 */
export const MUTATION_CAP_STEPS = 20

/** その個体の1ステ上限。**変異1回につき +1。**
 *
 *  ⚠️ 変異で +2 されたぶんが上限で即削られると、変異の価値が消える。
 *  だから上限のほうも一緒に上がる。 */
export function wildStatMaxFor(mutationCounter: number): number {
  return WILD_STAT_MAX + Math.min(Math.max(0, mutationCounter), MUTATION_CAP_STEPS)
}

/** その個体の合計上限。⭐ **常に1ステ上限の2倍**。
 *
 *  この比を保つことで、「**得意を2つ作れる**」がどの変異段階でも崩れない。
 *  比を崩すと、変異を重ねた個体ほど均等に振らざるを得なくなる。 */
export function wildTotalMaxFor(mutationCounter: number): number {
  return wildStatMaxFor(mutationCounter) * 2
}

export function totalOf(stats: StatBlock): number {
  let sum = 0
  for (const key of STAT_KEYS) sum += stats[key]
  return sum
}

/** 合計上限を守る。**超過分は低いステから削る。**
 *
 *  ⭐ これが「何かが特化していれば何かが伸びない」を実装に落としている。
 *  高いステは残り、低いステが犠牲になるので、**特化は保たれたまま万能個体だけが禁じられる**。
 *
 *  同値のステが複数あるときは順に1ずつ削る（片方だけを掘り下げて偏らせないため）。
 */
export function applyTotalCap(wild: StatBlock, mutationCounter = 0): StatBlock {
  const statMax = wildStatMaxFor(mutationCounter)
  const totalMax = wildTotalMaxFor(mutationCounter)

  const out: Record<StatKey, number> = { hp: 0, atk: 0, def: 0, spd: 0 }
  for (const key of STAT_KEYS) {
    out[key] = Math.min(Math.max(Math.trunc(wild[key]), 0), statMax)
  }

  let excess = totalOf(out) - totalMax
  while (excess > 0) {
    let min = Infinity
    for (const key of STAT_KEYS) {
      if (out[key] > 0 && out[key] < min) min = out[key]
    }
    if (min === Infinity) break // 全部0。合計上限が0でない限り起きない

    for (const key of STAT_KEYS) {
      if (excess === 0) break
      if (out[key] === min) {
        out[key]--
        excess--
      }
    }
  }

  return out
}

/** 実値 = 種族基礎 + 野生レベル + 育成で振った分。
 *
 *  🚧 尺度は1つだけ持つ。「HP だけ5倍」のような係数は**根拠が無いので置かない**。
 *  戦闘でこの数値がどう効くか（damage 式・ゲージ進行）は段B で
 *  自動対戦シミュレータに当てて決める。
 */
export function actualStats(base: StatBlock, wild: StatBlock, trained: StatBlock): StatBlock {
  const out: Record<StatKey, number> = { hp: 0, atk: 0, def: 0, spd: 0 }
  for (const key of STAT_KEYS) {
    out[key] = base[key] + wild[key] + trained[key]
  }
  return out
}
