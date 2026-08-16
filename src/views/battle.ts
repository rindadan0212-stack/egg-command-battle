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
  activeStatuses,
  createBattle,
  GAUGE_MAX,
  isAlive,
  isUsable,
  livingOf,
  needsTarget,
  nextActor,
  performAction,
  skillAt,
  unitPalette,
  type Action,
  type BattleEvent,
  type BattleState,
  type Outcome,
  type Unit,
} from '../game/battle.ts'
import { chooseAction } from '../game/ai.ts'
import { speciesOf, type Creature } from '../game/creature.ts'
import { effectiveCt, type Skill } from '../game/skills.ts'
import { ELEMENT_LABELS } from '../game/species.ts'
import { STAT_LABELS } from '../game/stats.ts'
import { spriteToCanvas } from '../render/sprite.ts'

/** 敵の手番を見せるための間。長さは体感のためだけで、状態には影響しない。 */
const ENEMY_PAUSE_MS = 420

export interface BattleView {
  readonly element: HTMLElement
  /** 画面を離れるときに呼ぶ。待ち時間と描画ループを止める */
  dispose(): void
}

/** その技の効き目の段位。⭐ 数値ではなく段位で見せる（企画の決め方に合わせる）。 */
function tierOf(skill: Skill): string {
  for (const effect of skill.effects) {
    if ('power' in effect) return effect.power
  }
  return ''
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
      // ⭐ 盾は威力に関係なく1撃を丸ごと無効化する
      return event.absorbed > 0
        ? `　${nameOf(event.unit)} は盾で防いだ（${event.absorbed} を無効化）`
        : `　${nameOf(event.unit)} に ${event.amount} ダメージ（残 ${event.hp}）`
    case 'heal':
      return `　${nameOf(event.unit)} を ${event.amount} 回復（残 ${event.hp}）`
    case 'buff':
      return `　${nameOf(event.unit)} の${STAT_LABELS[event.stat]} ${event.percent > 0 ? '+' : ''}${event.percent}%（${event.turns}回）`
    case 'applied':
      return `　${nameOf(event.unit)} に ${event.label}（${event.turns}回）`
    case 'poison':
      return `　${nameOf(event.unit)} が毒で ${event.amount} 減った（残 ${event.hp}）`
    case 'regen':
      return `　${nameOf(event.unit)} がリジェネで ${event.amount} 回復（残 ${event.hp}）`
    case 'shield':
      return `　${nameOf(event.unit)} に盾 ${event.amount}枚`
    case 'stun':
      return `　${nameOf(event.unit)} はスタン（${event.turns}回）`
    case 'skipped':
      return `　${nameOf(event.unit)} は動けない`
    case 'ct':
      return `　${nameOf(event.unit)} の技の待ちが ${event.delta > 0 ? '延びた' : '縮んだ'}`
    case 'taunt':
      return `　${nameOf(event.unit)} が挑発（あと ${event.hits} 回）`
    case 'guts':
      return `　${nameOf(event.unit)} にガッツ`
    case 'gutsSaved':
      return `　${nameOf(event.unit)} がガッツで耐えた`
    case 'immune':
      return `　${nameOf(event.unit)} に免疫`
    case 'blocked':
      return `　${nameOf(event.unit)} は免疫で受けなかった`
    case 'down':
      return `　${nameOf(event.unit)} が倒れた`
  }
}

export function renderBattle(
  allies: readonly Creature[],
  enemies: readonly Creature[],
  onEnd?: (outcome: Outcome) => void,
): BattleView {
  const state = createBattle(allies, enemies)
  let awaiting: Unit | null = null
  /** ⭐ 対象を選ばせている最中の行動。単体攻撃はここを経由する */
  let pending: Action | null = null
  let timer: ReturnType<typeof setTimeout> | null = null
  let disposed = false

  /** ⭐ ゲージを滑らかに見せるための表示専用の値。
   *
   *  ⚠️ **ゲームの状態は決定論のまま。**実時間で状態を進めると、
   *  同じ種から同じ結果が出るという前提が崩れる。
   *  だから「状態は整数の刻みで一気に進み、**見た目だけ**が追いつく」形にする。
   *  ⚠️ 毎フレーム DOM を作り直すとクリックが成立しないので、
   *  更新するのは棒の幅だけ。 */
  const shown = new Map<string, number>()
  const gaugeFills = new Map<string, HTMLElement>()
  let frame = 0

  const element = document.createElement('div')
  element.className = 'battle'

  // ⭐ モックの構成: 左に味方を小さくリスト、右に敵を大きく。
  //    下は白いシートで、そこがコマンドの場だと形で分かるようにする
  const arena = document.createElement('div')
  arena.className = 'arena'
  const allyRow = document.createElement('div')
  allyRow.className = 'field allies'
  const enemyRow = document.createElement('div')
  enemyRow.className = 'field enemies'
  arena.append(allyRow, enemyRow)

  const sheet = document.createElement('div')
  sheet.className = 'sheet'
  const commands = document.createElement('div')
  commands.className = 'commands'
  const logBox = document.createElement('div')
  logBox.className = 'battlelog mono'
  sheet.append(commands, logBox)

  element.append(arena, sheet)

  function buildFighter(unit: Unit): HTMLElement {
    const box = document.createElement('div')
    box.className = 'fighter'
    // 1体ずつ実寸で撮れるようにする
    box.id = `b-${unit.key}`
    box.dataset['down'] = String(!isAlive(unit))
    box.dataset['turn'] = String(awaiting === unit)

    // ⭐ 対象を選ばせている間、狙える相手を押せるようにする
    const selectable = pending !== null && unit.side === 'enemy' && isAlive(unit)
    box.dataset['selectable'] = String(selectable)
    if (selectable) {
      box.addEventListener('click', () => {
        if (!awaiting || !pending) return
        performAction(state, awaiting, pending, unit)
        pending = null
        awaiting = null
        paint()
        schedule()
      })
    }

    // ⭐ 敵は「絵」、味方は「札」。同じ形で並べると縦1本で見分けがつかない
    const big = unit.side === 'enemy'
    const art = document.createElement('div')
    art.className = 'art'
    art.append(spriteToCanvas(speciesOf(unit.creature).sprite, unitPalette(unit), big ? 4 : 2))

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
    nums.textContent = big
      ? `${Math.round((unit.hp / unit.maxHp) * 100)}%`
      : `${unit.hp}/${unit.maxHp}`

    const gauge = document.createElement('span')
    gauge.className = 'meter gauge'
    const gFill = document.createElement('i')
    // ⭐ 幅は描画ループが毎フレーム更新する。ここでは今見えている値から始める
    gFill.style.width = `${(shown.get(unit.key) ?? 0) * 100}%`
    gauge.append(gFill)
    gaugeFills.set(unit.key, gFill)

    const badges = document.createElement('span')
    badges.className = 'badges mono'
    // ⚠️ 速度の数値は出さない。ゲージの伸び方で読ませる
    badges.textContent = activeStatuses(unit).join(' ')

    if (big) box.append(art, label, nums, hp, gauge, badges)
    else box.append(art, label, hp, nums, gauge, badges)
    return box
  }

  function buildCommands(): void {
    commands.replaceChildren()
    if (state.outcome !== null) {
      const done = document.createElement('p')
      done.className = 'verdict'
      done.textContent =
        state.outcome === 'ally' ? '倒した' : state.outcome === 'enemy' ? '負けた' : '決着つかず'
      commands.append(done)
      return
    }
    if (!awaiting) return

    const who = document.createElement('span')
    who.className = 'turnof mono'
    who.textContent = `${awaiting.name}(${awaiting.key}) の番`
    commands.append(who)

    // 対象選択中は、コマンドの代わりに「誰を狙うか」を促す
    if (pending !== null) {
      const ask = document.createElement('span')
      ask.className = 'turnof'
      ask.textContent = `→ ${actionSkill(awaiting, pending).name}：狙う相手を選ぶ`
      const cancel = document.createElement('button')
      cancel.type = 'button'
      cancel.className = 'cancel'
      cancel.textContent = 'やめる'
      cancel.addEventListener('click', () => {
        pending = null
        paint()
      })
      commands.append(ask, cancel)
      return
    }

    // ⚠️ 「たたかう」は無い。枠1（CTなし）がその役目を兼ねる
    const options: Action[] = [
      { kind: 'skill', slot: 0 },
      { kind: 'skill', slot: 1 },
      { kind: 'skill', slot: 2 },
    ]
    for (const action of options) {
      if (action.kind !== 'skill' || !skillAt(awaiting, action.slot)) continue
      const skill = actionSkill(awaiting, action)
      const usable = isUsable(awaiting, action)
      const button = document.createElement('button')
      button.type = 'button'
      button.disabled = !usable
      button.title = skill.gist

      const name = document.createElement('span')
      name.textContent = skill.name
      button.append(name)

      const ct = document.createElement('span')
      ct.className = 'ct mono'
      const left = awaiting.cooldowns[action.slot] ?? 0
      const own = effectiveCt(action.slot, skill)
      // 枠1 は待ちが無いので、CT ではなく威力の段位を出す
      ct.textContent = left > 0 ? `あと${left}` : own > 0 ? `CT${own}` : tierOf(skill)
      button.append(ct)

      button.addEventListener('click', () => {
        if (!awaiting) return
        // ⭐ 単体攻撃は対象を選ばせる。自動任せだと HP の高い相手が永久に狙われない
        if (needsTarget(skill) && livingOf(state, 'enemy').length > 1) {
          pending = action
          paint()
          return
        }
        performAction(state, awaiting, action)
        awaiting = null
        paint()
        schedule()
      })
      commands.append(button)
    }

  }

  /** 表示だけを実時間で追いつかせる。⚠️ 状態には一切触れない。 */
  function animate(): void {
    for (const unit of state.units) {
      const fill = gaugeFills.get(unit.key)
      if (!fill) continue
      // ⭐ 超過ゲージは見た目 100% 止まり（内部では溜まり続けている）
      const target = isAlive(unit) ? Math.min(1, unit.gauge / GAUGE_MAX) : 0
      const now = shown.get(unit.key) ?? 0
      // ⚠️ 速く寄せると一瞬で終わって「伸びている」感じが出ない。
      //    超過ゲージの繰り越しで複数が同時に満タン付近へ並ぶので、なおさら緩める
      const next = Math.abs(target - now) < 0.004 ? target : now + (target - now) * 0.09
      shown.set(unit.key, next)
      fill.style.width = `${next * 100}%`
    }
    frame = requestAnimationFrame(animate)
  }

  function paint(): void {
    gaugeFills.clear()
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
  frame = requestAnimationFrame(animate)
  schedule()

  return {
    element,
    dispose() {
      disposed = true
      if (timer !== null) clearTimeout(timer)
      // ⚠️ 描画ループを必ず止める。放っておくと画面を離れても回り続ける
      if (frame !== 0) cancelAnimationFrame(frame)
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
