/** 行動を選ぶ側。
 *
 *  ⭐ **賢くしない。**勝敗を決めるのは育てた個体なので（wiki の「はじめに」）、
 *  ここは緩めてよいと決めてある。凝るほど、個体の差が AI の差に埋もれて測れなくなる。
 *
 *  ⚠️ 乱数を使わない。同じ状況からは必ず同じ手を選ぶ。
 *  そうしないと「1万回の勝率」が AI のブレを測ってしまう。
 */

import {
  actionSkill,
  damageOf,
  effectiveStat,
  elementMultiplier,
  isUsable,
  livingOf,
  type Action,
  type BattleState,
  type Unit,
} from './battle.ts'
import { speciesOf, statsOf } from './creature.ts'
import { BUFF_PERCENT, DAMAGE_POWER, TICK_PERCENT, type PowerTier } from './skills.ts'

/** ⚠️ 「たたかう」は無い。枠1（種族固定・CTなし）がその役目を兼ねる。 */
const ALL_ACTIONS: readonly Action[] = [
  { kind: 'skill', slot: 0 },
  { kind: 'skill', slot: 1 },
  { kind: 'skill', slot: 2 },
]

/** ステータスを1%動かすことの価値。 */
const BUFF_VALUE = 0.5
/** 相手の手番を1つ奪うことの価値。⭐ 行動回数は全出力への倍率なので高く見る。 */
const STUN_VALUE = 26
/** CT を1つ動かすことの価値。 */
const CT_VALUE = 6
/** 肩代わり1回ぶんの価値。 */
const TAUNT_VALUE = 7
/** ガッツ・免疫の価値（状況が読みにくいので控えめの固定値）。 */
const GUARDIAN_VALUE = 10

function estimateDamage(
  actor: Unit,
  target: Unit,
  tier: PowerTier,
  scale: 'atk' | 'def',
): number {
  const a = statsOf(actor.creature)
  const t = statsOf(target.creature)
  const attackStat =
    scale === 'atk'
      ? effectiveStat(a.atk, actor.status.atk)
      : effectiveStat(a.def, actor.status.def)
  const defenseStat = effectiveStat(t.def, target.status.def)
  const mult = elementMultiplier(
    speciesOf(actor.creature).element,
    speciesOf(target.creature).element,
  )
  return damageOf(DAMAGE_POWER[tier], attackStat, defenseStat, mult)
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
  /** その効果が誰に向くか */
  const subject = skill.target === 'self' ? actor : skill.target === 'allyLowest' ? weakest : focus

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
      case 'buff': {
        // 既に同じ向きで掛かっているなら重ねる意味が薄い
        const now = subject.status[effect.stat]
        const gain =
          now.turns > 0 && Math.sign(now.percent) === effect.sign ? 0 : BUFF_PERCENT
        score += gain * BUFF_VALUE
        break
      }
      case 'poison': {
        // ⭐ スタックするので重ね掛けにも価値がある。⚠️ 相手の残 HP で頭打ち
        const stacked = subject.status.poison.turns > 0 ? subject.status.poison.stacks : 0
        const perTurn = Math.floor((subject.maxHp * TICK_PERCENT * effect.stacks) / 100)
        const total = perTurn * effect.turns
        // 既に重なっているぶんは「上乗せ」の価値だけを見る
        score += Math.min(subject.hp, total) / (1 + stacked * 0.5)
        break
      }
      case 'regen': {
        const stacked = subject.status.regen.turns > 0 ? subject.status.regen.stacks : 0
        const perTurn = Math.floor((subject.maxHp * TICK_PERCENT * effect.stacks) / 100)
        const missing = subject.maxHp - subject.hp
        score += (Math.min(missing, perTurn * effect.turns) * 0.7) / (1 + stacked * 0.5)
        break
      }
      case 'healRatio': {
        // ⚠️ 「HPを戻す」と「敵のHPを削る」は同じ単位ではない。緊急度で割り引く
        const amount = Math.floor((subject.maxHp * effect.percent) / 100)
        const missing = subject.maxHp - subject.hp
        const urgency = 0.5 + 0.5 * (1 - subject.hp / subject.maxHp)
        score += Math.min(amount, missing) * urgency
        break
      }
      case 'shield': {
        // ⭐ 枚数ぶんの攻撃を完全に無効化する。1枚の価値は「相手の一撃ぶん」で見る
        const incoming = estimateDamage(focus, subject, '中', 'atk')
        score += subject.status.shield > 0 ? 0 : incoming * effect.count * 0.7
        break
      }
      case 'stun': {
        score += subject.status.stun > 0 ? 0 : STUN_VALUE * effect.turns
        break
      }
      case 'ct': {
        // ⚠️ 枠1には効かないので、枠2・3 が実際に動くぶんだけ価値がある
        let moved = 0
        for (let i = 1; i < subject.cooldowns.length; i++) {
          const now = subject.cooldowns[i] ?? 0
          moved += Math.abs(Math.max(0, now + effect.delta) - now)
        }
        score += moved * CT_VALUE
        break
      }
      case 'taunt': {
        // 自分より脆い味方がいるときだけ意味がある
        const mine = actor.hp / Math.max(1, actor.maxHp)
        const fragile = friends.filter((f) => f !== actor && f.hp / f.maxHp < mine).length
        score += fragile > 0 && actor.status.taunt === 0 ? effect.hits * TAUNT_VALUE : 0
        break
      }
      case 'guts': {
        // 追い詰められているときだけ価値がある
        const hurt = actor.hp / actor.maxHp < 0.5
        score += hurt && actor.status.guts === 0 ? GUARDIAN_VALUE : 0
        break
      }
      case 'immune': {
        // 既に弱化を受けているなら、掛け直しても消えないので価値は低い
        score += actor.status.immune === 0 ? GUARDIAN_VALUE : 0
        break
      }
    }
  }
  return score
}

/** ⚠️ 同点は並び順で決める。実行ごとに変わると比較にならない。 */
export function chooseAction(state: BattleState, actor: Unit): Action {
  // ⭐ 枠1（種族固定）は CT 0 なので必ず使える。既定の手はこれ
  let best: Action = { kind: 'skill', slot: 0 }
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
