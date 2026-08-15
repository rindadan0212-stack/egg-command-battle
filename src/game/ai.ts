/** 行動を選ぶ側。
 *
 *  ⭐ **賢くしない。**勝敗を決めるのは育てた個体なので（企画.md「何が勝敗を決めるか」）、
 *  ここは緩めてよいと決めてある。凝るほど、個体の差が AI の差に埋もれて測れなくなる。
 *
 *  ⚠️ 乱数を使わない。同じ状況からは必ず同じ手を選ぶ。
 *  そうしないと「1万回の勝率」が AI のブレを測ってしまう。
 */

import {
  actionSkill,
  effectiveStat,
  elementMultiplier,
  damageOf,
  isUsable,
  livingOf,
  STAGE_LIMIT,
  type Action,
  type BattleState,
  type Unit,
} from './battle.ts'
import { speciesOf, statsOf } from './creature.ts'

const ALL_ACTIONS: readonly Action[] = [
  { kind: 'basic' },
  { kind: 'skill', slot: 0 },
  { kind: 'skill', slot: 1 },
  { kind: 'skill', slot: 2 },
]

/** 段階を1つ動かすことの価値。倒しきる算段より優先させない程度に置く。 */
const STAGE_VALUE = 14
/** 相手のゲージを戻すことの価値（1ゲージあたり）。 */
const GAUGE_VALUE = 0.03
/** 肩代わり1回ぶんの価値。 */
const COVER_VALUE = 7

function estimateDamage(actor: Unit, target: Unit, power: number, scale: 'atk' | 'def'): number {
  const a = statsOf(actor.creature)
  const t = statsOf(target.creature)
  const attackStat =
    scale === 'atk' ? effectiveStat(a.atk, actor.stages.atk) : effectiveStat(a.def, actor.stages.def)
  const defenseStat = effectiveStat(t.def, target.stages.def)
  const mult = elementMultiplier(speciesOf(actor.creature).element, speciesOf(target.creature).element)
  return damageOf(power, attackStat, defenseStat, mult)
}

function scoreOf(state: BattleState, actor: Unit, action: Action): number {
  const skill = actionSkill(actor, action)
  const foes = livingOf(state, actor.side === 'ally' ? 'enemy' : 'ally')
  const friends = livingOf(state, actor.side)
  if (foes.length === 0) return 0

  const focus = [...foes].sort((a, b) => a.hp - b.hp || a.slot - b.slot)[0] as Unit
  const weakest = [...friends].sort(
    (a, b) => a.hp / a.maxHp - b.hp / b.maxHp || a.slot - b.slot,
  )[0] as Unit

  let score = 0
  for (const effect of skill.effects) {
    switch (effect.kind) {
      case 'damage': {
        if (skill.target === 'enemyAll') {
          // ⚠️ 過剰打撃を価値に数えない。残 HP で頭打ちにする
          for (const foe of foes) {
            score += Math.min(foe.hp, estimateDamage(actor, foe, effect.power, effect.scale))
          }
        } else {
          score += Math.min(focus.hp, estimateDamage(actor, focus, effect.power, effect.scale))
        }
        break
      }
      case 'heal': {
        // ⚠️ 「HPを18戻す」と「敵のHPを8削る」は同じ単位ではない。
        // 削るのは勝利に近づき、戻すのは敗北を遅らせるだけ。
        // 素点で比べると回復が常に勝ち、戦闘が終わらなくなる（実測: 1体落とすのに62行動）。
        // 減っていない相手に撃っても意味がないので、不足分と緊急度で割り引く。
        const missing = weakest.maxHp - weakest.hp
        const urgency = 0.5 + 0.5 * (1 - weakest.hp / weakest.maxHp)
        score += Math.min(effect.power, missing) * urgency
        break
      }
      case 'stage': {
        const subject = skill.target === 'self' ? actor : focus
        const now = subject.stages[effect.stat]
        const next = Math.max(-STAGE_LIMIT, Math.min(STAGE_LIMIT, now + effect.delta))
        // 上限に張り付いていたら効果が無い
        score += next === now ? 0 : STAGE_VALUE
        break
      }
      case 'gauge': {
        score += Math.abs(effect.delta) * GAUGE_VALUE
        break
      }
      case 'cover': {
        // 自分より脆い味方がいるときだけ意味がある。
        // 「脆い」は残 HP の割合ではなく **あと何発耐えられるか** で見る
        // （割合が高くても打たれ弱ければ先に落ちる）。
        const mine = actor.hp / Math.max(1, actor.maxHp)
        const fragile = friends.filter((f) => f !== actor && f.hp / f.maxHp < mine).length
        score += fragile > 0 ? effect.hits * COVER_VALUE : 0
        break
      }
    }
  }
  return score
}

/** ⚠️ 同点は並び順で決める。実行ごとに変わると比較にならない。 */
export function chooseAction(state: BattleState, actor: Unit): Action {
  let best: Action = { kind: 'basic' }
  let bestScore = -Infinity

  for (const action of ALL_ACTIONS) {
    if (!isUsable(actor, action)) continue
    const score = scoreOf(state, actor, action)
    if (score > bestScore) {
      bestScore = score
      best = action
    }
  }
  return best
}
