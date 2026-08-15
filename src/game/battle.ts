/** 戦闘。3体同時・スピードゲージ制・スキルごとの CT。
 *
 *  ⚠️ **戦闘そのものに乱数を入れていない。**（命中率も会心も無い）
 *  ⭐ こうすると「1万回の勝率」が戦闘の運ではなく**個体差の分布**を測ることになり、
 *  速度一強の検算が濁らない。運の要素を入れるなら、入れた後で必ず測り直す。
 *
 *  ⚠️ **ゲージと CT は整数**で進める。浮動小数のドリフトを持ち込まない。
 *
 *  強さの計算はここが唯一の出所。画面もシミュレータもこの関数群を呼ぶ。
 */

import type { Rng } from '../core/rng.ts'
import { paletteOf, skillsOf, speciesOf, statsOf, type Creature } from './creature.ts'
import { DAMAGE_POWER, effectiveCt, HEAL_POWER, type Effect, type Skill } from './skills.ts'
import { ELEMENT_BEATS, type Element } from './species.ts'
import { STAT_KEYS, type StatKey } from './stats.ts'

/** ゲージが満ちる値。 */
export const GAUGE_MAX = 1000

/** 全員が持つ基礎テンポ。ゲージは `GAUGE_BASE + 速度` ずつ溜まる。
 *
 *  ⚠️ **これが無いと速度一強になる。**（実測: 速度型の勝率 100%）
 *  速度は「行動回数」という**全出力への倍率**なので、素で効かせると上限が無い。
 *  一方ダメージは `2a/(a+d)` の形で**2倍が上限**なので、攻撃はどれだけ振っても追いつけない。
 *  基礎テンポを足して比を圧縮する。
 *
 *  値は `npm run sim -- --speed` の実測に合わせて決めた（履歴.md に測定値）。
 *  ⭐ 副産物として**速度0でも止まらない**（0除算の心配も消える）。 */
export const GAUGE_BASE = 55

/** 段階の上下限。 */
export const STAGE_LIMIT = 3

/** ⚠️ 決着しない戦闘を止める上限（教訓「〜まで待つに上限を置く」）。
 *  回復役同士だと理論上終わらないので、条件で塞がずここで打ち切る。 */
export const MAX_ACTIONS = 300

/** HP の尺度。
 *
 *  段A では「根拠の無い係数は置かない」として保留した。ここで決める。
 *  **保証したいこと: 平均的な個体同士で、1体を倒すのに 5〜8 回の行動が必要。**
 *  値は `npm run sim -- --pace` の実測に合わせて決めた（履歴.md に測定値）。 */
export const HP_SCALE = 3

/** 属性の有利倍率。3すくみ。 */
export const ELEMENT_ADVANTAGE = 1.5

export type Side = 'ally' | 'enemy'

export interface Unit {
  readonly creature: Creature
  readonly side: Side
  readonly slot: number
  /** 'ally-0' のような一意の名前。ログと画面の取っ手になる */
  readonly key: string
  readonly name: string
  readonly maxHp: number
  hp: number
  gauge: number
  stages: Record<StatKey, number>
  /** スキル枠3つぶん。0 なら使える */
  cooldowns: [number, number, number]
  /** ⭐ 味方への単体攻撃をあと何回肩代わりするか。「壁」を成立させるための欄。 */
  cover: number
}

/** ⚠️ 「たたかう」は無い。**枠1（種族固定・CTなし）がその役目を兼ねる。** */
export type Action =
  | { readonly kind: 'skill'; readonly slot: number }
  /** ⭐ 巣でだけ選べる。卵を持って離脱する */
  | { readonly kind: 'steal' }

/** 盗みの成功率。
 *
 *  ⭐ **速度比で決まる。**「格上の巣でも狙えるがリスクがある」を表すのに、
 *  勝てるかどうか（火力）ではなく**逃げ切れるか**（速度）で決めるのが素直。
 *  釣り合いの計測で弱かった速度型に、固有の使い道ができる副産物もある。
 *
 *  ⚠️ 長居するほど下がる。入って早々に掠め取るのが最も成功しやすい。 */
export function stealChance(actorSpeed: number, guardSpeed: number, actions: number): number {
  const base = actorSpeed / (actorSpeed + Math.max(1, guardSpeed))
  const wary = Math.max(0.35, 1 - actions * 0.02)
  return Math.max(0.05, Math.min(0.95, base * wary))
}

export type BattleEvent =
  | { kind: 'act'; actor: string; skill: string }
  | { kind: 'damage'; unit: string; amount: number; hp: number }
  | { kind: 'heal'; unit: string; amount: number; hp: number }
  | { kind: 'stage'; unit: string; stat: StatKey; now: number }
  | { kind: 'gauge'; unit: string; delta: number }
  | { kind: 'cover'; unit: string; hits: number }
  | { kind: 'ct'; unit: string; delta: number }
  | { kind: 'down'; unit: string }
  | { kind: 'steal'; unit: string; chance: number; ok: boolean }

/** `stolen` = 卵を持って離脱した。勝ちでも負けでもない第三の終わり方。 */
export type Outcome = 'ally' | 'enemy' | 'draw' | 'stolen'

export interface BattleState {
  readonly units: readonly Unit[]
  actions: number
  log: BattleEvent[]
  outcome: Outcome | null
  /** ⚠️ 巣での戦闘のときだけ入る。戦闘本体は決定論のままにしたいので、
   *  盗みの判定にだけ使う系統を分けて持つ。 */
  stealRng: Rng | null
}

// ── 唯一の出所となる計算 ──────────────────────────────

/** 段階の倍率。0 で等倍、±3 が上下限。
 *  正負で式を分けるのは、負側が 0 に落ちないようにするため。 */
export function stageMultiplier(stage: number): number {
  const s = Math.max(-STAGE_LIMIT, Math.min(STAGE_LIMIT, stage))
  return s >= 0 ? (2 + s) / 2 : 2 / (2 - s)
}

/** 段階を掛けた実効値。⚠️ 1 未満に落とさない（速度0は割り算で壊れる）。 */
export function effectiveStat(base: number, stage: number): number {
  return Math.max(1, Math.floor(base * stageMultiplier(stage)))
}

/** 属性の倍率。牙 → 羽 → 鱗 → 牙。 */
export function elementMultiplier(attacker: Element, defender: Element): number {
  if (ELEMENT_BEATS[attacker] === defender) return ELEMENT_ADVANTAGE
  if (ELEMENT_BEATS[defender] === attacker) return 1 / ELEMENT_ADVANTAGE
  return 1
}

/** 攻撃・防御それぞれの効きを飽和させる定数。
 *
 *  ⚠️ **片方だけ飽和させると、飽和していない側のステが一強になる。**
 *  分母だけ飽和させたとき、攻撃が線形・防御が飽和になり、
 *  攻撃を持つ型が 63〜65% / 攻撃0の型が 13〜37% という偏りが出た（実測）。
 *
 *  ⭐ 値は 2次元に掃引して決めた。防御側を大きく取ってあるのは、
 *  **集中攻撃のせいで防御が攻撃の約3倍の価値を持つ**ため（狙われた1体は
 *  毎ラウンド3発受けるので防御が3回効くが、攻撃は自分の1発にしか効かない）。
 *  対称に置くと防御編成が 85% まで伸びた。
 *  掃引の記録は 履歴.md。 */
export const ATK_SOFTEN = 20
export const DEF_SOFTEN = 110

/** parity（atk = def = この値）でダメージがちょうど power になるように正規化する。
 *  ⭐ 飽和定数をいじってもスキルの power の意味が変わらないようにするため。 */
const PARITY = 40
export const DAMAGE_NORMALIZE = (DEF_SOFTEN + PARITY) / (ATK_SOFTEN + PARITY)

/** ダメージ。
 *
 *  ⚠️ **最初 `power * 2 * atk / (atk + def)` にしていて、企画の前提を壊していた。**
 *  あの形は**比だけ**で決まるので、攻撃特化(a=60) が防御特化(d=60) を殴っても
 *  比は1、つまり均等どうしと同じダメージにしかならない。
 *  結果、特化が構造的に無意味化され、実測で **均等の総合勝率 82%** に対して
 *  「得意2つ」の攻速が 22% という逆転が起きた
 *  （＝合計上限を2倍にして「得意を2つ作れる」ようにした意味が消えていた）。
 *
 *  ⭐ `power * (ATK_SOFTEN + atk) / (DEF_SOFTEN + def)` に変えた。
 *  **絶対値が効く**ので特化が報われ、分母・分子とも定数で底上げしてあるので
 *  `atk/def` のような爆発も、どちらか一方のステの一強も起きない。
 *  atk = def のとき ちょうど power になる。 */
export function damageOf(
  power: number,
  attackStat: number,
  defenseStat: number,
  elementMult: number,
): number {
  const raw =
    (power * DAMAGE_NORMALIZE * (ATK_SOFTEN + attackStat)) / (DEF_SOFTEN + defenseStat)
  return Math.max(1, Math.floor(raw * elementMult))
}

/** 1刻みでゲージがいくつ溜まるか。**唯一の出所。** */
export function gaugeRate(speed: number): number {
  return GAUGE_BASE + Math.max(0, speed)
}

/** そのユニットが行動できるまでの刻み数。整数で切り上げる。 */
export function ticksToAct(gauge: number, speed: number): number {
  return Math.ceil((GAUGE_MAX - gauge) / gaugeRate(speed))
}

// ── 組み立て ────────────────────────────────────────

function freshStages(): Record<StatKey, number> {
  return { hp: 0, atk: 0, def: 0, spd: 0 }
}

export function makeUnit(creature: Creature, side: Side, slot: number): Unit {
  const maxHp = statsOf(creature).hp * HP_SCALE
  return {
    creature,
    side,
    slot,
    key: `${side}-${slot}`,
    name: speciesOf(creature).name,
    maxHp,
    hp: maxHp,
    gauge: 0,
    stages: freshStages(),
    cooldowns: [0, 0, 0],
    cover: 0,
  }
}

export function createBattle(
  allies: readonly Creature[],
  enemies: readonly Creature[],
  stealRng: Rng | null = null,
): BattleState {
  return {
    units: [
      ...allies.map((c, i) => makeUnit(c, 'ally', i)),
      ...enemies.map((c, i) => makeUnit(c, 'enemy', i)),
    ],
    actions: 0,
    log: [],
    outcome: null,
    stealRng,
  }
}

/** 盗みが選べるか。巣での戦闘で、まだ敵が残っているときだけ。 */
export function canSteal(state: BattleState): boolean {
  return state.stealRng !== null && state.outcome === null && livingOf(state, 'enemy').length > 0
}

/** 見張りのうち最も速い者。逃げ切れるかはこの相手との速度比で決まる。 */
export function fastestGuard(state: BattleState): Unit | null {
  const foes = livingOf(state, 'enemy')
  return [...foes].sort((a, b) => speedOf(b) - speedOf(a) || a.slot - b.slot)[0] ?? null
}

export function isAlive(unit: Unit): boolean {
  return unit.hp > 0
}

export function livingOf(state: BattleState, side: Side): Unit[] {
  return state.units.filter((u) => u.side === side && isAlive(u))
}

export function speedOf(unit: Unit): number {
  return effectiveStat(statsOf(unit.creature).spd, unit.stages.spd)
}

/** その枠のスキル。空き枠なら null。 */
export function skillAt(unit: Unit, slot: number): Skill | null {
  const list = skillsOf(unit.creature)
  return list[slot] ?? null
}

/** 今その行動が選べるか（CT が明けているか）。 */
export function isUsable(unit: Unit, action: Action, state?: BattleState): boolean {
  if (action.kind === 'steal') return state ? canSteal(state) : false
  const skill = skillAt(unit, action.slot)
  if (!skill) return false
  // ⭐ 枠1は CT 0 なので常に使える。これが「たたかう」の代わり
  return (unit.cooldowns[action.slot] ?? 0) === 0
}

export function actionSkill(unit: Unit, action: Action): Skill {
  if (action.kind === 'steal') throw new Error('盗みはスキルではない')
  const skill = skillAt(unit, action.slot)
  if (!skill) throw new Error(`${unit.key} の枠 ${action.slot} は空`)
  return skill
}

// ── 進行 ────────────────────────────────────────────

function decideOutcome(state: BattleState): Outcome | null {
  const allies = livingOf(state, 'ally').length
  const enemies = livingOf(state, 'enemy').length
  if (allies === 0 && enemies === 0) return 'draw'
  if (enemies === 0) return 'ally'
  if (allies === 0) return 'enemy'
  if (state.actions >= MAX_ACTIONS) return 'draw'
  return null
}

/** 次に行動する者まで時間を進める。決着していれば null を返し、outcome を確定させる。
 *  ⚠️ 同時に満ちたときは並び順で決める（実行ごとに変わると比較できない）。 */
export function nextActor(state: BattleState): Unit | null {
  // ⚠️ 離脱は勝敗の判定より優先する。上書きすると「盗んだのに負け」になる
  if (state.outcome === 'stolen') return null
  state.outcome = decideOutcome(state)
  if (state.outcome !== null) return null

  const living = state.units.filter(isAlive)
  if (living.length === 0) return null

  // 誰かが満ちるまで時間を進める
  let ticks = Infinity
  for (const unit of living) {
    ticks = Math.min(ticks, ticksToAct(unit.gauge, speedOf(unit)))
  }
  if (Number.isFinite(ticks) && ticks > 0) {
    for (const unit of living) unit.gauge += ticks * gaugeRate(speedOf(unit))
  }

  // ⭐ 満ちた者のうち「**内部ゲージが最も多い**」者が動く。速度ではない。
  //
  // ⚠️ 以前は「配列の並び順」で決めていた。根拠が無いうえ、
  // ゲージは満タンを超えて溜まり越されるのに（`gauge -= GAUGE_MAX`）、
  // **超過ぶんが一切報われていなかった**。
  // 速く動いて余分に溜めた者が先に動く、が筋。
  let best: Unit | null = null
  for (const unit of living) {
    if (unit.gauge < GAUGE_MAX) continue
    if (!best || unit.gauge > best.gauge) best = unit
  }
  return best
}

/** その行動が「敵1体を選ぶ」ものか。⭐ プレイヤーに選ばせる必要があるかの判定。 */
export function needsTarget(skill: Skill): boolean {
  return skill.target === 'enemyOne'
}

function targetsOf(state: BattleState, actor: Unit, skill: Skill, chosen?: Unit | null): Unit[] {
  const foes = livingOf(state, actor.side === 'ally' ? 'enemy' : 'ally')
  const friends = livingOf(state, actor.side)

  switch (skill.target) {
    case 'self':
      return [actor]
    case 'enemyAll':
      return foes
    case 'enemyOne': {
      // ⭐ 指定があればそれを狙う（プレイヤーの手番）。
      // ⚠️ 指定が無いとき（AI）だけ「残 HP の低い相手から」に落ちる。
      // 自動任せだけにしていたら、HP の高いボスが眷属を全滅させるまで
      // 一度も狙われず、**総ダメージ0** になっていた（実測）。
      const sorted =
        chosen && isAlive(chosen) && chosen.side !== actor.side
          ? [chosen]
          : [...foes].sort((a, b) => a.hp - b.hp || a.slot - b.slot)
      const picked = sorted[0]
      if (!picked) return []
      // ⭐ かばっている者がいれば、そちらへ逸らす（「壁」の実体）。
      //    ⚠️ 全体攻撃は逸らさない。全員に当たるので肩代わりの意味が無い。
      const guard = [...foes]
        .filter((u) => u.cover > 0 && u !== picked)
        .sort((a, b) => b.cover - a.cover || a.slot - b.slot)[0]
      if (guard) {
        guard.cover--
        return [guard]
      }
      return [picked]
    }
    case 'allyLowest': {
      const sorted = [...friends].sort(
        (a, b) => a.hp / a.maxHp - b.hp / b.maxHp || a.slot - b.slot,
      )
      return sorted.length > 0 ? [sorted[0] as Unit] : []
    }
  }
}

function applyEffect(state: BattleState, actor: Unit, target: Unit, effect: Effect): void {
  switch (effect.kind) {
    case 'damage': {
      const actorStats = statsOf(actor.creature)
      const targetStats = statsOf(target.creature)
      const attackStat =
        effect.scale === 'atk'
          ? effectiveStat(actorStats.atk, actor.stages.atk)
          : effectiveStat(actorStats.def, actor.stages.def)
      const defenseStat = effectiveStat(targetStats.def, target.stages.def)
      const mult = elementMultiplier(
        speciesOf(actor.creature).element,
        speciesOf(target.creature).element,
      )
      // ⭐ 威力は段位から引く。技ごとに数値を持たせない
      const amount = damageOf(DAMAGE_POWER[effect.power], attackStat, defenseStat, mult)
      target.hp = Math.max(0, target.hp - amount)
      state.log.push({ kind: 'damage', unit: target.key, amount, hp: target.hp })
      if (target.hp === 0) state.log.push({ kind: 'down', unit: target.key })
      break
    }
    case 'heal': {
      const before = target.hp
      target.hp = Math.min(target.maxHp, target.hp + HEAL_POWER[effect.power])
      state.log.push({
        kind: 'heal',
        unit: target.key,
        amount: target.hp - before,
        hp: target.hp,
      })
      break
    }
    case 'stage': {
      const now = Math.max(
        -STAGE_LIMIT,
        Math.min(STAGE_LIMIT, target.stages[effect.stat] + effect.delta),
      )
      target.stages[effect.stat] = now
      state.log.push({ kind: 'stage', unit: target.key, stat: effect.stat, now })
      break
    }
    case 'gauge': {
      target.gauge = Math.max(0, target.gauge + effect.delta)
      state.log.push({ kind: 'gauge', unit: target.key, delta: effect.delta })
      break
    }
    case 'cover': {
      target.cover = effect.hits
      state.log.push({ kind: 'cover', unit: target.key, hits: effect.hits })
      break
    }
    case 'ct': {
      // ⚠️ 枠1は触らない。「必ず打てる札」に CT を乗せると手が無くなる戦闘が生まれる
      for (let i = 1; i < target.cooldowns.length; i++) {
        target.cooldowns[i] = Math.max(0, (target.cooldowns[i] ?? 0) + effect.delta)
      }
      state.log.push({ kind: 'ct', unit: target.key, delta: effect.delta })
      break
    }
  }
}

/** 盗んで逃げる。成功なら卵を持って離脱、失敗なら見張りの一撃をもらう。 */
function attemptSteal(state: BattleState, actor: Unit): void {
  const rng = state.stealRng
  const guard = fastestGuard(state)
  if (!rng || !guard) throw new Error('ここでは盗めない')

  const chance = stealChance(speedOf(actor), speedOf(guard), state.actions)
  const ok = rng.chance(chance)
  state.log.push({ kind: 'steal', unit: actor.key, chance, ok })

  if (ok) {
    state.outcome = 'stolen'
    return
  }
  // ⚠️ 失敗にはちゃんと代償を置く。無料で何度も試せると二択が二択でなくなる
  applyEffect(state, guard, actor, { kind: 'damage', power: '小', scale: 'atk' })
  actor.gauge = 0
  state.actions++
  state.outcome = decideOutcome(state)
}

/** その者に行動させる。ゲージを引き、CT を進める。
 *  `chosen` を渡すと単体攻撃の対象を指定できる（プレイヤーの手番）。 */
export function performAction(
  state: BattleState,
  actor: Unit,
  action: Action,
  chosen?: Unit | null,
): void {
  if (!isUsable(actor, action, state)) {
    throw new Error(`${actor.key} は今その行動を選べない`)
  }
  if (action.kind === 'steal') {
    attemptSteal(state, actor)
    return
  }
  const skill = actionSkill(actor, action)
  state.log.push({ kind: 'act', actor: actor.key, skill: skill.name })

  for (const target of targetsOf(state, actor, skill, chosen)) {
    for (const effect of skill.effects) {
      applyEffect(state, actor, target, effect)
    }
  }

  // ⚠️ CT は「本人の行動回数」で減る。何をしたかに関わらず1回ぶん進む。
  //    共通の時間軸ではないので、速い個体ほど CT 明けも早く来る。
  for (let i = 0; i < actor.cooldowns.length; i++) {
    actor.cooldowns[i] = Math.max(0, (actor.cooldowns[i] ?? 0) - 1)
  }
  // ⭐ CT は技ではなく**枠**の性質。枠1は常に 0
  actor.cooldowns[action.slot] = effectiveCt(action.slot, skill)

  actor.gauge -= GAUGE_MAX
  state.actions++
  state.outcome = decideOutcome(state)
}

/** 画面で使うための小物。パレットを引いて絵を出すため。 */
export function unitPalette(unit: Unit): readonly string[] {
  return paletteOf(unit.creature)
}

/** 段階が付いている項目だけ返す（画面表示用）。 */
export function activeStages(unit: Unit): Array<[StatKey, number]> {
  return STAT_KEYS.filter((k) => unit.stages[k] !== 0).map((k) => [k, unit.stages[k]])
}
