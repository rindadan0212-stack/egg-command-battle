/** 卵強奪の発射画面。
 *
 *  引っ張って離す（モンストのように、引いた向きと**反対**へ飛ぶ）。
 *  ⭐ 飛距離は編成のスピード合計。ここで速度に寄せた分が効く。
 *
 *  ⚠️ 状態は決定論のまま。実時間で動くのは**軌跡をなぞる描画だけ**。
 */

import { speciesOf, statsOf, type Creature } from '../game/creature.ts'
import {
  distanceFor,
  EGG_RADIUS,
  FIELD_WIDTH,
  launch,
  parentSpans,
  RUNNER_RADIUS,
  type StealField,
  type StealOutcome,
  type StealRun,
} from '../game/steal.ts'
import { spriteToCanvas } from '../render/sprite.ts'

/** 画面上の高さ。奥行きが深い巣ほど縦長に見える（幅が狭く見える）。 */
const DISPLAY_HEIGHT = 460

export interface StealView {
  readonly element: HTMLElement
  dispose(): void
}

export function renderSteal(
  field: StealField,
  party: readonly Creature[],
  parent: Creature,
  onDone: (outcome: StealOutcome) => void,
): StealView {
  const budget = distanceFor(party)
  const scale = DISPLAY_HEIGHT / field.height

  const element = document.createElement('div')
  element.className = 'steal'

  const lead = document.createElement('p')
  lead.className = 'lead'
  lead.textContent = '引っ張って離す。卵まで届けば奪える。'

  const note = document.createElement('p')
  note.className = 'note mono'
  const speeds = party.map((c) => statsOf(c).spd)
  note.textContent =
    `飛距離 ${budget}（スピード合計 ${speeds.reduce((a, b) => a + b, 0)}` +
    ` = ${speeds.join(' + ')}）`

  const canvas = document.createElement('canvas')
  canvas.className = 'stealfield'
  canvas.width = Math.round(FIELD_WIDTH * scale)
  canvas.height = Math.round(field.height * scale)
  canvas.id = 's-field'

  const hint = document.createElement('p')
  hint.className = 'note'
  hint.textContent = `親は${field.side === 'right' ? '右' : '左'}に寄っている。`

  element.append(lead, note, canvas, hint)

  const ctx = canvas.getContext('2d')
  let aim: { from: { x: number; y: number }; to: { x: number; y: number } } | null = null
  let run: StealRun | null = null
  let shown = 0
  let startedAt = 0
  let frame = 0
  let disposed = false
  let settled = false

  const parentSpecies = speciesOf(parent)
  const parentPalette = parentSpecies.palettes[parent.paletteIndex] as string[]
  const parentArt = spriteToCanvas(parentSpecies.sprite, parentPalette, 3)

  // 先頭の1体が飛ぶ。⭐ 誰を先頭に置くかが見えるので、編成が画面に出る
  const leadSpecies = speciesOf(party[0] as Creature)
  const leadArt = spriteToCanvas(
    leadSpecies.sprite,
    leadSpecies.palettes[(party[0] as Creature).paletteIndex] as string[],
    1,
  )

  function draw(): void {
    if (!ctx) return
    ctx.clearRect(0, 0, canvas.width, canvas.height)

    // 地
    ctx.fillStyle = '#1a1713'
    ctx.fillRect(0, 0, canvas.width, canvas.height)

    // 親が塞ぐ帯。⚠️ 帯の色は**当たり判定そのもの**なので、
    //    見た目を足すときも矩形の縁を動かさない
    for (const span of parentSpans(field)) {
      const x = span.from * scale
      const w = (span.to - span.from) * scale
      const y = field.bandTop * scale
      const h = (field.bandBottom - field.bandTop) * scale
      // ⚠️ 面か線かどちらか一方。二重にすると帯だけが画面で強くなりすぎる
      ctx.fillStyle = '#332c22'
      ctx.fillRect(x, y, w, h)
    }

    // ⭐ 親そのものを立たせる。棒だけでは「立ちはだかっている」に見えない
    const spans = parentSpans(field)
    const widest = spans.reduce(
      (best, s) => (s.to - s.from > best.to - best.from ? s : best),
      spans[0] ?? { from: 0, to: 0 },
    )
    if (widest.to > widest.from) {
      const cx = ((widest.from + widest.to) / 2) * scale
      const cy = ((field.bandTop + field.bandBottom) / 2) * scale
      ctx.drawImage(parentArt, Math.round(cx - parentArt.width / 2), Math.round(cy - parentArt.height / 2))
    }

    // 卵
    ctx.fillStyle = '#e8dcc0'
    ctx.strokeStyle = '#3b2f21'
    ctx.beginPath()
    ctx.ellipse(
      field.egg.x * scale,
      field.egg.y * scale,
      EGG_RADIUS * scale * 0.85,
      EGG_RADIUS * scale,
      0,
      0,
      Math.PI * 2,
    )
    ctx.fill()
    ctx.stroke()

    // 軌跡
    if (run) {
      ctx.strokeStyle = '#f0b429'
      ctx.lineWidth = 2
      ctx.beginPath()
      const upto = Math.min(shown, run.path.length - 1)
      for (let i = 0; i <= upto; i++) {
        const p = run.path[i] as { x: number; y: number }
        if (i === 0) ctx.moveTo(p.x * scale, p.y * scale)
        else ctx.lineTo(p.x * scale, p.y * scale)
      }
      ctx.stroke()
    }

    // 走る者
    const at = run
      ? (run.path[Math.min(shown, run.path.length - 1)] as { x: number; y: number })
      : field.start
    // ⚠️ 当たり判定は円。絵はその上に乗せるだけで、判定の大きさは変えない
    ctx.fillStyle = '#f0b429'
    ctx.beginPath()
    ctx.arc(at.x * scale, at.y * scale, RUNNER_RADIUS * scale, 0, Math.PI * 2)
    ctx.fill()
    ctx.drawImage(
      leadArt,
      Math.round(at.x * scale - leadArt.width / 2),
      Math.round(at.y * scale - leadArt.height / 2),
    )

    // 狙い（引いた向きと反対へ飛ぶ）
    if (aim && !run) {
      const dx = aim.from.x - aim.to.x
      const dy = aim.from.y - aim.to.y
      const len = Math.hypot(dx, dy) || 1
      ctx.strokeStyle = '#8a8377'
      ctx.setLineDash([4, 4])
      ctx.lineWidth = 2
      ctx.beginPath()
      ctx.moveTo(field.start.x * scale, field.start.y * scale)
      ctx.lineTo(
        field.start.x * scale + (dx / len) * 70,
        field.start.y * scale + (dy / len) * 70,
      )
      ctx.stroke()
      ctx.setLineDash([])
    }
  }

  /** 軌跡をなぞりきるまでの時間。距離なりに伸ばすが、上下は抑える。 */
  function traceMs(steps: number): number {
    return Math.min(2000, Math.max(700, steps * 6))
  }

  function animate(now: number): void {
    if (disposed) return
    if (run) {
      // ⚠️ **フレーム数で進めない。** 1フレームあたり n 刻みにしていたら、
      //    rAF が 2fps しか出ない環境で飛行が終わらなくなった（実機で発覚）。
      //    表示速度が機械の速さで変わるのは、そもそも同じ画面ではない。
      if (startedAt === 0) startedAt = now
      const t = (now - startedAt) / traceMs(run.path.length)
      shown = Math.min(run.path.length - 1, Math.floor(t * (run.path.length - 1)))
      if (t >= 1 && !settled) {
        settled = true
        window.setTimeout(() => {
          if (!disposed && run) onDone(run.outcome)
        }, 500)
      }
    }
    draw()
    frame = requestAnimationFrame(animate)
  }

  function pointIn(event: PointerEvent): { x: number; y: number } {
    const rect = canvas.getBoundingClientRect()
    return { x: event.clientX - rect.left, y: event.clientY - rect.top }
  }

  canvas.addEventListener('pointerdown', (event) => {
    if (run) return
    canvas.setPointerCapture(event.pointerId)
    const p = pointIn(event)
    aim = { from: p, to: p }
  })
  canvas.addEventListener('pointermove', (event) => {
    if (!aim || run) return
    aim.to = pointIn(event)
  })
  canvas.addEventListener('pointerup', () => {
    if (!aim || run) return
    const dx = aim.from.x - aim.to.x
    const dy = aim.from.y - aim.to.y
    aim = null
    // 引いた向きと反対へ。⚠️ ほとんど動かしていないときは撃たない
    if (Math.hypot(dx, dy) < 8) return
    // 上向きを 0 とした時計回りの角度
    const angle = Math.atan2(dx, -dy)
    run = launch(field, angle, budget)
    shown = 0
    startedAt = 0
    lead.textContent = '放った。'
  })

  draw()
  frame = requestAnimationFrame(animate)

  return {
    element,
    dispose() {
      disposed = true
      if (frame !== 0) cancelAnimationFrame(frame)
    },
  }
}
