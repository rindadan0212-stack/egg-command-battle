/** 戦闘の画面。
 *
 *  ⭐ プレイヤーが見て決めるのは「**誰がいつ動くか / 何が今使えるか**」なので、
 *  ゲージと CT を画面の主役にする。HP はその次。
 *
 *  ⚠️ 敵の手番に入れている待ち時間は**見せるためだけ**のもの。
 *  ゲームの状態はそれとは無関係に決定論的に進む。
 */

import {
  actionSkill,
  activeStages,
  createBattle,
  GAUGE_MAX,
  isAlive,
  isUsable,
  livingOf,
  nextActor,
  performAction,
  skillAt,
  speedOf,
  unitPalette,
  type Action,
  type BattleEvent,
  type BattleState,
  type Unit,
} from '../game/battle.ts'
import { chooseAction } from '../game/ai.ts'
import { speciesOf, type Creature } from '../game/creature.ts'
import { ELEMENT_LABELS } from '../game/species.ts'
import { STAT_LABELS } from '../game/stats.ts'
import { spriteToCanvas } from '../render/sprite.ts'

/** 敵の手番を見せるための間。長さは体感のためだけで、状態には影響しない。 */
const ENEMY_PAUSE_MS = 420

export interface BattleView {
  readonly element: HTMLElement
  /** 画面を離れるときに呼ぶ。待ち時間を止める */
  dispose(): void
}

function describe(state: BattleState, event: BattleEvent): string {
  const nameOf = (key: string): string => {
    const unit = state.units.find((u) => u.key === key)
    return unit ? `${unit.name}(${unit.key})` : key
  }
  switch (event.kind) {
    case 'act':
      return `${nameOf(event.actor)} → ${event.skill}`
    case 'damage':
      return `　${nameOf(event.unit)} に ${event.amount} ダメージ（残 ${event.hp}）`
    case 'heal':
      return `　${nameOf(event.unit)} を ${event.amount} 回復（残 ${event.hp}）`
    case 'stage':
      return `　${nameOf(event.unit)} の${STAT_LABELS[event.stat]} 段階 ${event.now > 0 ? '+' : ''}${event.now}`
    case 'gauge':
      return `　${nameOf(event.unit)} のゲージ ${event.delta > 0 ? '+' : ''}${event.delta}`
    case 'cover':
      return `　${nameOf(event.unit)} がかばう（あと ${event.hits} 回）`
    case 'down':
      return `　${nameOf(event.unit)} が倒れた`
  }
}

export function renderBattle(
  allies: readonly Creature[],
  enemies: readonly Creature[],
  onEnd?: (outcome: 'ally' | 'enemy' | 'draw') => void,
): BattleView {
  const state = createBattle(allies, enemies)
  let awaiting: Unit | null = null
  let timer: ReturnType<typeof setTimeout> | null = null
  let disposed = false

  const element = document.createElement('div')
  element.className = 'battle'

  const enemyRow = document.createElement('div')
  enemyRow.className = 'field enemies'
  const allyRow = document.createElement('div')
  allyRow.className = 'field allies'
  const commands = document.createElement('div')
  commands.className = 'commands'
  const logBox = document.createElement('div')
  logBox.className = 'battlelog mono'

  element.append(enemyRow, allyRow, commands, logBox)

  function buildFighter(unit: Unit): HTMLElement {
    const box = document.createElement('div')
    box.className = 'fighter'
    // 1体ずつ実寸で撮れるようにする
    box.id = `b-${unit.key}`
    box.dataset['down'] = String(!isAlive(unit))
    box.dataset['turn'] = String(awaiting === unit)

    const art = document.createElement('div')
    art.className = 'art'
    art.append(spriteToCanvas(speciesOf(unit.creature).sprite, unitPalette(unit), 2))

    const label = document.createElement('span')
    label.className = 'fname'
    label.textContent = `${unit.name} ${ELEMENT_LABELS[speciesOf(unit.creature).element]}`

    const hp = document.createElement('span')
    hp.className = 'meter hp'
    const hpFill = document.createElement('i')
    hpFill.style.width = `${(unit.hp / unit.maxHp) * 100}%`
    hp.append(hpFill)

    const nums = document.createElement('span')
    nums.className = 'nums mono'
    nums.textContent = `${unit.hp}/${unit.maxHp}`

    const gauge = document.createElement('span')
    gauge.className = 'meter gauge'
    const gFill = document.createElement('i')
    gFill.style.width = `${Math.min(100, (unit.gauge / GAUGE_MAX) * 100)}%`
    gauge.append(gFill)

    const badges = document.createElement('span')
    badges.className = 'badges mono'
    const parts = activeStages(unit).map(
      ([stat, n]) => `${STAT_LABELS[stat]}${n > 0 ? '+' : ''}${n}`,
    )
    if (unit.cover > 0) parts.push(`かばう${unit.cover}`)
    parts.push(`速${speedOf(unit)}`)
    badges.textContent = parts.join(' ')

    box.append(art, label, hp, nums, gauge, badges)
    return box
  }

  function buildCommands(): void {
    commands.replaceChildren()
    if (state.outcome !== null) {
      const done = document.createElement('p')
      done.className = 'verdict'
      done.textContent =
        state.outcome === 'ally' ? '勝った' : state.outcome === 'enemy' ? '負けた' : '決着つかず'
      commands.append(done)
      return
    }
    if (!awaiting) return

    const who = document.createElement('span')
    who.className = 'turnof mono'
    who.textContent = `${awaiting.name}(${awaiting.key}) の番`
    commands.append(who)

    const options: Action[] = [
      { kind: 'basic' },
      { kind: 'skill', slot: 0 },
      { kind: 'skill', slot: 1 },
      { kind: 'skill', slot: 2 },
    ]
    for (const action of options) {
      if (action.kind === 'skill' && !skillAt(awaiting, action.slot)) continue
      const skill = actionSkill(awaiting, action)
      const usable = isUsable(awaiting, action)
      const button = document.createElement('button')
      button.type = 'button'
      button.disabled = !usable
      button.title = skill.gist

      const name = document.createElement('span')
      name.textContent = skill.name
      button.append(name)

      if (action.kind === 'skill') {
        const ct = document.createElement('span')
        ct.className = 'ct mono'
        const left = awaiting.cooldowns[action.slot] ?? 0
        ct.textContent = left > 0 ? `あと${left}` : `CT${skill.ct}`
        button.append(ct)
      }

      button.addEventListener('click', () => {
        if (!awaiting) return
        performAction(state, awaiting, action)
        awaiting = null
        paint()
        schedule()
      })
      commands.append(button)
    }
  }

  function paint(): void {
    enemyRow.replaceChildren(...state.units.filter((u) => u.side === 'enemy').map(buildFighter))
    allyRow.replaceChildren(...state.units.filter((u) => u.side === 'ally').map(buildFighter))
    buildCommands()
    logBox.replaceChildren(
      ...state.log.slice(-9).map((event) => {
        const line = document.createElement('div')
        line.textContent = describe(state, event)
        return line
      }),
    )
    logBox.scrollTop = logBox.scrollHeight
  }

  /** 次に動く者まで進める。味方の番なら止まって入力を待つ。 */
  function schedule(): void {
    if (disposed) return
    const actor = nextActor(state)
    if (!actor) {
      awaiting = null
      paint()
      if (state.outcome) onEnd?.(state.outcome)
      return
    }
    if (actor.side === 'ally') {
      awaiting = actor
      paint()
      return
    }
    // 敵の番。見せるために間を置く（状態の進み方には影響しない）
    timer = setTimeout(() => {
      if (disposed) return
      performAction(state, actor, chooseAction(state, actor))
      paint()
      schedule()
    }, ENEMY_PAUSE_MS)
  }

  paint()
  schedule()

  return {
    element,
    dispose() {
      disposed = true
      if (timer !== null) clearTimeout(timer)
    },
  }
}

/** 画面の申告に使う要約。 */
export function battleSummary(state: BattleState): Record<string, unknown> {
  return {
    actions: state.actions,
    outcome: state.outcome ?? 'なし',
    ally: livingOf(state, 'ally').map((u) => `${u.key}:${u.hp}`),
    enemy: livingOf(state, 'enemy').map((u) => `${u.key}:${u.hp}`),
  }
}
