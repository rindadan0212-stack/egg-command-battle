/** 戦闘。3体同時・スピードゲージ制・スキルごとの CT。
 *
 *  ⚠️ **戦闘そのものに乱数を入れていない。**（命中率も会心も無い）
 *  ⭐ こうすると「1万回の勝率」が戦闘の運ではなく**個体差の分布**を測ることになり、
 *  釣り合いの検算が濁らない。運の要素を入れるなら、入れた後で必ず測り直す。
 *
 *  ⚠️ **ゲージと CT は整数**で進める。浮動小数のドリフトを持ち込まない。
 *
 *  強さの計算はここが唯一の出所。画面もシミュレータもこの関数群を呼ぶ。
 */

import type { Rng } from '../core/rng.ts'
import { paletteOf, skillsOf, speciesOf, statsOf, type Creature } from './creature.ts'
import {
  BUFF_PERCENT,
  DAMAGE_POWER,
  effectiveCt,
  isHarmful,
  RATIO_PERCENT,
  TICK_PERCENT,
  type Effect,
  type Skill,
} from './skills.ts'
import { ELEMENT_BEATS, type Element } from './species.ts'
import type { StatKey } from './stats.ts'

/** ゲージが満ちる値。 */
export const GAUGE_MAX = 1000

/** 全員が持つ基礎テンポ。ゲージは `GAUGE_BASE + 速度` ずつ溜まる。
 *
 *  ⚠️ **これが無いと速度一強になる。**（実測: 速度型の勝率 100%）
 *  速度は「行動回数」という**全出力への倍率**なので、素で効かせると上限が無い。
 *  一方ダメージは式で頭打ちになるので、攻撃はどれだけ振っても追いつけない。
 *  ⭐ 副産物として**速度0でも止まらない**。 */
export const GAUGE_BASE = 55

/** ⚠️ 決着しない戦闘を止める上限（教訓「〜まで待つに上限を置く」）。
 *  ⚠️ **飛ばした手番もここに数える。**全員がスタンし続ける形で止まらないように。 */
export const MAX_ACTIONS = 300

/** HP の尺度。**保証したいこと: 平均的な個体同士で、1体を倒すのに 5〜12 発。** */
export const HP_SCALE = 3

/** 属性の有利倍率。3すくみ。 */
export const ELEMENT_ADVANTAGE = 1.5

/** 攻撃・防御それぞれの効きを飽和させる定数。
 *
 *  ⭐ 値は2次元に掃引して決めた。防御側を大きく取ってあるのは、
 *  **集中攻撃のせいで防御が攻撃の約3倍の価値を持つ**ため。 */
export const ATK_SOFTEN = 20
export const DEF_SOFTEN = 110

const PARITY = 40
export const DAMAGE_NORMALIZE = (DEF_SOFTEN + PARITY) / (ATK_SOFTEN + PARITY)

export type Side = 'ally' | 'enemy'

/** ステータスに掛かる修正。⭐ **段階ではなく、ステータスの数値に対する ±%。** */
export interface Modifier {
  percent: number
  /** 残り。**その個体の行動回数**で減る（CT と同じ数え方） */
  turns: number
}

/** 持続する状態。⚠️ 数える単位は全部「その個体の行動回数」。 */
export interface UnitStatus {
  atk: Modifier
  def: Modifier
  spd: Modifier
  /** 毒。1行動ごとに最大HPの percent% 減る */
  poison: Modifier
  /** リジェネ。1行動ごとに最大HPの percent% 回復 */
  regen: Modifier
  /** シールドの残量（点）。HP より先に減る */
  shield: number
  /** 飛ばす手番の残り */
  stun: number
  /** 味方への単体攻撃を引き受ける残り回数 */
  taunt: number
  /** ガッツの残り行動数 */
  guts: number
  /** 免疫の残り行動数 */
  immune: number
}

export interface Unit {
  readonly creature: Creature
  readonly side: Side
  readonly slot: number
  readonly key: string
  readonly name: string
  readonly maxHp: number
  hp: number
  gauge: number
  status: UnitStatus
  /** スキル枠3つぶん。0 なら使える */
  cooldowns: [number, number, number]
}

/** ⚠️ 「たたかう」は無い。**枠1（種族固定・CTなし）がその役目を兼ねる。** */
export type Action =
  | { readonly kind: 'skill'; readonly slot: number }
  /** ⭐ 巣でだけ選べる。卵を持って離脱する */
  | { readonly kind: 'steal' }

/** 盗みの成功率。⭐ **速度比で決まる。**⚠️ 長居するほど下がる。 */
export function stealChance(actorSpeed: number, guardSpeed: number, actions: number): number {
  const base = actorSpeed / (actorSpeed + Math.max(1, guardSpeed))
  const wary = Math.max(0.35, 1 - actions * 0.02)
  return Math.max(0.05, Math.min(0.95, base * wary))
}

export type BattleEvent =
  | { kind: 'act'; actor: string; skill: string }
  | { kind: 'damage'; unit: string; amount: number; hp: number; absorbed: number }
  | { kind: 'heal'; unit: string; amount: number; hp: number }
  | { kind: 'buff'; unit: string; stat: StatKey; percent: number; turns: number }
  | { kind: 'poison'; unit: string; amount: number; hp: number }
  | { kind: 'regen'; unit: string; amount: number; hp: number }
  /** 持続する状態がついた（毒・リジェネなど） */
  | { kind: 'applied'; unit: string; label: string; turns: number }
  | { kind: 'shield'; unit: string; amount: number }
  | { kind: 'stun'; unit: string; turns: number }
  | { kind: 'skipped'; unit: string }
  | { kind: 'ct'; unit: string; delta: number }
  | { kind: 'taunt'; unit: string; hits: number }
  | { kind: 'guts'; unit: string }
  | { kind: 'gutsSaved'; unit: string }
  | { kind: 'immune'; unit: string }
  | { kind: 'blocked'; unit: string }
  | { kind: 'down'; unit: string }
  | { kind: 'steal'; unit: string; chance: number; ok: boolean }

/** `stolen` = 卵を持って離脱した。勝ちでも負けでもない第三の終わり方。 */
export type Outcome = 'ally' | 'enemy' | 'draw' | 'stolen'

export interface BattleState {
  readonly units: readonly Unit[]
  actions: number
  log: BattleEvent[]
  outcome: Outcome | null
  /** ⚠️ 巣での戦闘のときだけ入る。盗みの判定にだけ使う */
  stealRng: Rng | null
}

// ── 唯一の出所となる計算 ──────────────────────────────

/** 修正を掛けた実効値。⚠️ 1 未満に落とさない（速度0は割り算で壊れる）。 */
export function effectiveStat(base: number, mod: Modifier): number {
  const percent = mod.turns > 0 ? mod.percent : 0
  return Math.max(1, Math.floor((base * (100 + percent)) / 100))
}

/** 属性の倍率。牙 → 羽 → 鱗 → 牙。 */
export function elementMultiplier(attacker: Element, defender: Element): number {
  if (ELEMENT_BEATS[attacker] === defender) return ELEMENT_ADVANTAGE
  if (ELEMENT_BEATS[defender] === attacker) return 1 / ELEMENT_ADVANTAGE
  return 1
}

/** ダメージ。
 *  ⭐ `power × (A+atk) / (D+def)`。**絶対値が効く**ので特化が報われ、
 *  分子・分母とも定数で底上げしてあるので爆発も一方のステの一強も起きない。 */
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

export function ticksToAct(gauge: number, speed: number): number {
  return Math.ceil((GAUGE_MAX - gauge) / gaugeRate(speed))
}

// ── 組み立て ────────────────────────────────────────

function noMod(): Modifier {
  return { percent: 0, turns: 0 }
}

function freshStatus(): UnitStatus {
  return {
    atk: noMod(),
    def: noMod(),
    spd: noMod(),
    poison: noMod(),
    regen: noMod(),
    shield: 0,
    stun: 0,
    taunt: 0,
    guts: 0,
    immune: 0,
  }
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
    status: freshStatus(),
    cooldowns: [0, 0, 0],
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

export function isAlive(unit: Unit): boolean {
  return unit.hp > 0
}

export function livingOf(state: BattleState, side: Side): Unit[] {
  return state.units.filter((u) => u.side === side && isAlive(u))
}

export function speedOf(unit: Unit): number {
  return effectiveStat(statsOf(unit.creature).spd, unit.status.spd)
}

export function canSteal(state: BattleState): boolean {
  return state.stealRng !== null && state.outcome === null && livingOf(state, 'enemy').length > 0
}

export function fastestGuard(state: BattleState): Unit | null {
  const foes = livingOf(state, 'enemy')
  return [...foes].sort((a, b) => speedOf(b) - speedOf(a) || a.slot - b.slot)[0] ?? null
}

export function skillAt(unit: Unit, slot: number): Skill | null {
  const list = skillsOf(unit.creature)
  return list[slot] ?? null
}

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

export function needsTarget(skill: Skill): boolean {
  return skill.target === 'enemyOne'
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

/** その個体が行動する直前に、持続するものを1つ進める。
 *  ⚠️ 毒で倒れることがあるので、呼んだ側は生死を見直す。 */
function tickStatus(state: BattleState, unit: Unit): void {
  const s = unit.status

  if (s.poison.turns > 0) {
    const amount = Math.max(1, Math.floor((unit.maxHp * s.poison.percent) / 100))
    unit.hp = Math.max(0, unit.hp - amount)
    state.log.push({ kind: 'poison', unit: unit.key, amount, hp: unit.hp })
    s.poison.turns--
    if (unit.hp === 0) state.log.push({ kind: 'down', unit: unit.key })
  }
  if (s.regen.turns > 0 && isAlive(unit)) {
    const amount = Math.max(1, Math.floor((unit.maxHp * s.regen.percent) / 100))
    const before = unit.hp
    unit.hp = Math.min(unit.maxHp, unit.hp + amount)
    state.log.push({ kind: 'regen', unit: unit.key, amount: unit.hp - before, hp: unit.hp })
    s.regen.turns--
  }

  for (const key of ['atk', 'def', 'spd'] as const) {
    if (s[key].turns > 0) {
      s[key].turns--
      if (s[key].turns === 0) s[key].percent = 0
    }
  }
  if (s.guts > 0) s.guts--
  if (s.immune > 0) s.immune--
}

/** 手番を1つ消費する（行動せずに）。 */
function consumeTurn(state: BattleState, unit: Unit): void {
  unit.gauge -= GAUGE_MAX
  // ⚠️ 飛ばした手番も数える。数えないと全員スタンで止まらなくなる
  state.actions++
  state.outcome = decideOutcome(state)
}

/** 次に行動する者まで時間を進める。
 *  ⚠️ 毒で倒れた者・スタン中の者は、ここで手番を消費して次へ送る。 */
export function nextActor(state: BattleState): Unit | null {
  if (state.outcome === 'stolen') return null

  for (let guard = 0; guard < MAX_ACTIONS * 2; guard++) {
    state.outcome = decideOutcome(state)
    if (state.outcome !== null) return null

    const living = state.units.filter(isAlive)
    if (living.length === 0) return null

    let ticks = Infinity
    for (const unit of living) {
      ticks = Math.min(ticks, ticksToAct(unit.gauge, speedOf(unit)))
    }
    if (Number.isFinite(ticks) && ticks > 0) {
      for (const unit of living) unit.gauge += ticks * gaugeRate(speedOf(unit))
    }

    // ⭐ 満ちた者のうち「**内部ゲージが最も多い**」者が動く。速度ではない。
    // ⚠️ 以前は配列の並び順で決めていた。ゲージは満タンを超えて繰り越されるのに、
    // 超過ぶんが一切報われていなかった。
    let best: Unit | null = null
    for (const unit of living) {
      if (unit.gauge < GAUGE_MAX) continue
      if (!best || unit.gauge > best.gauge) best = unit
    }
    if (!best) return null

    tickStatus(state, best)
    if (!isAlive(best)) {
      consumeTurn(state, best)
      continue
    }
    if (best.status.stun > 0) {
      best.status.stun--
      state.log.push({ kind: 'skipped', unit: best.key })
      consumeTurn(state, best)
      continue
    }
    return best
  }
  return null
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
      // ⭐ 指定があればそれを狙う（プレイヤーの手番）。無ければ残 HP の低い相手から
      const sorted =
        chosen && isAlive(chosen) && chosen.side !== actor.side
          ? [chosen]
          : [...foes].sort((a, b) => a.hp - b.hp || a.slot - b.slot)
      const picked = sorted[0]
      if (!picked) return []
      // ⭐ 挑発している者がいれば、そちらへ逸らす（「壁」の実体）。
      // ⚠️ 全体攻撃は逸らさない（全員に当たるので引き受ける意味が無い）
      const guard = [...foes]
        .filter((u) => u.status.taunt > 0 && u !== picked)
        .sort((a, b) => b.status.taunt - a.status.taunt || a.slot - b.slot)[0]
      if (guard) {
        guard.status.taunt--
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

/** ダメージを通す。シールド → HP の順に減り、ガッツがあれば HP1 で止まる。 */
function dealDamage(state: BattleState, target: Unit, amount: number): void {
  let left = amount
  let absorbed = 0
  if (target.status.shield > 0) {
    absorbed = Math.min(target.status.shield, left)
    target.status.shield -= absorbed
    left -= absorbed
  }
  const before = target.hp
  target.hp = Math.max(0, target.hp - left)

  // ⭐ ガッツ: 致命傷を HP1 で耐える。⚠️ 元から1以下なら効かない（無限に粘らせない）
  if (target.hp === 0 && target.status.guts > 0 && before > 1) {
    target.hp = 1
    target.status.guts = 0
    state.log.push({ kind: 'gutsSaved', unit: target.key })
  }

  state.log.push({
    kind: 'damage',
    unit: target.key,
    amount: before - target.hp,
    hp: target.hp,
    absorbed,
  })
  if (target.hp === 0) state.log.push({ kind: 'down', unit: target.key })
}

function applyEffect(state: BattleState, actor: Unit, target: Unit, effect: Effect): void {
  // ⭐ 免疫は弱い側の効果だけを弾く
  if (isHarmful(effect) && target.status.immune > 0) {
    state.log.push({ kind: 'blocked', unit: target.key })
    return
  }

  switch (effect.kind) {
    case 'damage': {
      const actorStats = statsOf(actor.creature)
      const targetStats = statsOf(target.creature)
      const attackStat =
        effect.scale === 'atk'
          ? effectiveStat(actorStats.atk, actor.status.atk)
          : effectiveStat(actorStats.def, actor.status.def)
      const defenseStat = effectiveStat(targetStats.def, target.status.def)
      const mult = elementMultiplier(
        speciesOf(actor.creature).element,
        speciesOf(target.creature).element,
      )
      dealDamage(state, target, damageOf(DAMAGE_POWER[effect.power], attackStat, defenseStat, mult))
      break
    }
    case 'buff': {
      // ⚠️ 掛け直しは上書き。積み上げにすると青天井になる
      const percent = BUFF_PERCENT[effect.power] * effect.sign
      target.status[effect.stat] = { percent, turns: effect.turns }
      state.log.push({
        kind: 'buff',
        unit: target.key,
        stat: effect.stat,
        percent,
        turns: effect.turns,
      })
      break
    }
    case 'poison': {
      target.status.poison = { percent: TICK_PERCENT[effect.power], turns: effect.turns }
      state.log.push({ kind: 'applied', unit: target.key, label: '毒', turns: effect.turns })
      break
    }
    case 'regen': {
      target.status.regen = { percent: TICK_PERCENT[effect.power], turns: effect.turns }
      state.log.push({ kind: 'applied', unit: target.key, label: 'リジェネ', turns: effect.turns })
      break
    }
    case 'healRatio': {
      const amount = Math.max(1, Math.floor((target.maxHp * RATIO_PERCENT[effect.power]) / 100))
      const before = target.hp
      target.hp = Math.min(target.maxHp, target.hp + amount)
      state.log.push({ kind: 'heal', unit: target.key, amount: target.hp - before, hp: target.hp })
      break
    }
    case 'shield': {
      const amount = Math.max(1, Math.floor((target.maxHp * RATIO_PERCENT[effect.power]) / 100))
      // ⚠️ 重ね掛けは上書き。積むと実質無敵になる
      target.status.shield = amount
      state.log.push({ kind: 'shield', unit: target.key, amount })
      break
    }
    case 'stun': {
      target.status.stun += effect.turns
      state.log.push({ kind: 'stun', unit: target.key, turns: effect.turns })
      break
    }
    case 'ct': {
      // ⚠️ 枠1は触らない。「必ず打てる札」に CT を乗せると手が無くなる
      for (let i = 1; i < target.cooldowns.length; i++) {
        target.cooldowns[i] = Math.max(0, (target.cooldowns[i] ?? 0) + effect.delta)
      }
      state.log.push({ kind: 'ct', unit: target.key, delta: effect.delta })
      break
    }
    case 'taunt': {
      target.status.taunt = effect.hits
      state.log.push({ kind: 'taunt', unit: target.key, hits: effect.hits })
      break
    }
    case 'guts': {
      target.status.guts = effect.turns
      state.log.push({ kind: 'guts', unit: target.key })
      break
    }
    case 'immune': {
      target.status.immune = effect.turns
      state.log.push({ kind: 'immune', unit: target.key })
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

/** その者に行動させる。ゲージを引き、CT を進める。 */
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

  // ⚠️ CT は「本人の行動回数」で減る。何をしたかに関わらず1回ぶん進む
  for (let i = 0; i < actor.cooldowns.length; i++) {
    actor.cooldowns[i] = Math.max(0, (actor.cooldowns[i] ?? 0) - 1)
  }
  // ⭐ CT は技ではなく**枠**の性質。枠1は常に 0
  actor.cooldowns[action.slot] = effectiveCt(action.slot, skill)

  actor.gauge -= GAUGE_MAX
  state.actions++
  state.outcome = decideOutcome(state)
}

/** 画面で使う小物。 */
export function unitPalette(unit: Unit): readonly string[] {
  return paletteOf(unit.creature)
}

/** 画面に出す、今かかっている状態の一覧。⚠️ ここが唯一の表示用まとめ。 */
export function activeStatuses(unit: Unit): string[] {
  const s = unit.status
  const out: string[] = []
  const sign = (n: number): string => (n > 0 ? `+${n}` : `${n}`)
  if (s.atk.turns > 0) out.push(`攻撃${sign(s.atk.percent)}%`)
  if (s.def.turns > 0) out.push(`防御${sign(s.def.percent)}%`)
  if (s.spd.turns > 0) out.push(`速度${sign(s.spd.percent)}%`)
  if (s.poison.turns > 0) out.push(`毒${s.poison.turns}`)
  if (s.regen.turns > 0) out.push(`リジェネ${s.regen.turns}`)
  if (s.shield > 0) out.push(`盾${s.shield}`)
  if (s.stun > 0) out.push(`スタン${s.stun}`)
  if (s.taunt > 0) out.push(`挑発${s.taunt}`)
  if (s.guts > 0) out.push(`ガッツ${s.guts}`)
  if (s.immune > 0) out.push(`免疫${s.immune}`)
  return out
}
