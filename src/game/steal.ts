/** 卵強奪の発射フェーズ。
 *
 *  ```
 *  縦長のフィールド。一番上に卵。その手前に親が左右どちらかへ寄って立ちはだかる。
 *  一番下の自分のモンスターを1回だけ引っ張って飛ばす。
 *  卵に届けば強奪成功。親に当たるか失速したら戦闘へ。
 *  ```
 *
 *  ⭐ **飛距離は編成のスピード合計。**ここが設計の芯:
 *  強奪を狙ってスピードに寄せるほど、**失敗したときの戦闘で編成が偏って苦しくなる**。
 *  同じ資源（編成）が2つの軸に引っ張られる。
 *
 *  ⚠️ **乱数を使わない。**角度はプレイヤーの入力、それ以外は完全に決まる。
 *  親がどちらへ寄るかだけは巣ごとの乱数で決める（挑むたびに変わる）。
 */

import type { Creature } from './creature.ts'
import { statsOf } from './creature.ts'

export const FIELD_WIDTH = 160

/** 段階ごとの奥行き。
 *
 *  ⭐ **ここが「速度を積む意味」の本体。**
 *  奥が深いほど、卵まで届かせるのに距離が要る。
 *
 *  ⚠️ 最初は「隙間の幅と寄り」だけで難しさを作ろうとしたが、
 *  それだと**必要な距離が段階で変わらない**ので速度投資が報われず、
 *  代わりに角度の幅が 1〜2度まで狭まって**精密さの勝負**になってしまった（走査で発覚）。
 *  面白さの芯は編成の選択であって狙いの精度ではないので、
 *  **狙いは寛容にして、距離で分ける。** */
export function depthForTier(tier: number): number {
  const table = [190, 240, 290, 340, 390]
  return table[Math.max(0, Math.min(table.length - 1, tier - 1))] as number
}

/** スピード合計1につき飛べる距離。
 *  ⚠️ 値は `npm run sim -- --steal` の走査で決めた（wiki/開発/開発履歴.md に測定値）。 */
export const SPEED_TO_DISTANCE = 3

/** 進みの刻み。⚠️ 整数で刻んで決定論を保つ。 */
const STEP = 1

export const EGG_RADIUS = 13
export const RUNNER_RADIUS = 7

/** 親が塞ぐ帯の厚み。位置は奥行きに合わせて動く。
 *
 *  ⚠️ **卵との縦の余裕が要る。**帯を卵に近づけすぎると、
 *  隙間を抜けた後に横へ寄せきれず、**どんな飛距離でも不能**になる（走査で発覚）。 */
const BAND_THICKNESS = 30

export interface Point {
  x: number
  y: number
}

export interface StealField {
  readonly height: number
  /** 親がどちら側に寄っているか。空いているのは反対側 */
  readonly side: 'left' | 'right'
  /** 空いている隙間の範囲（x） */
  readonly gapFrom: number
  readonly gapTo: number
  /** 親が塞ぐ帯 */
  readonly bandTop: number
  readonly bandBottom: number
  readonly egg: Point
  readonly start: Point
}

/** 隙間の幅。⚠️ **狙いは寛容にする。**難しさは距離で作る。
 *
 *  ⭐ 74 → 90 に広げた根拠（`--steal` の走査）:
 *  段ごとに要るスピード合計は **どちらでも変わらない**（59 / 75 / 92 / 109 / 125）。
 *  変わるのは境目の鋭さだけで、74 では「届くが幅 1°」という帯が
 *  各段 11〜18 スピードぶん続き、そこに落ちた編成は手先を測られる。
 *  90 にするとその帯が 0〜10 に縮み、届くマスはすべて幅 2°以上になる。
 *  ⚠️ さらに 106 まで広げると帯は消えるが、狙いが完全に無意味になる。 */
export const GAP_WIDTH = 90

/** 親の寄り具合（中央からのずれ）。⚠️ まっすぐでは通らない程度に寄せる。 */
export const LEAN = 57

export function makeField(tier: number, side: 'left' | 'right'): StealField {
  const height = depthForTier(tier)
  // 親が右へ寄る＝隙間は左寄り
  const center = side === 'right' ? FIELD_WIDTH / 2 - LEAN : FIELD_WIDTH / 2 + LEAN
  const bandTop = Math.round(height * 0.36)
  return {
    height,
    side,
    gapFrom: Math.max(0, center - GAP_WIDTH / 2),
    gapTo: Math.min(FIELD_WIDTH, center + GAP_WIDTH / 2),
    bandTop,
    bandBottom: bandTop + BAND_THICKNESS,
    egg: { x: FIELD_WIDTH / 2, y: 26 },
    start: { x: FIELD_WIDTH / 2, y: height - 14 },
  }
}

/** 飛べる距離。⭐ 編成のスピード合計から決まる。 */
export function distanceFor(party: readonly Creature[]): number {
  const sum = party.reduce((total, c) => total + statsOf(c).spd, 0)
  return sum * SPEED_TO_DISTANCE
}

/** 親が占めている x の範囲（隙間の左右2枚）。 */
/** 塞がっている範囲。⚠️ **1マス幅の切れ端を返さない。**
 *  隙間が壁に接すると反対側に幅 1 の帯が残り、当たり判定には効かないのに
 *  画面には線が出る。見た目が「何かある」と言っているのに実体が無いのは嘘。 */
const MIN_SPAN = 2

export function parentSpans(field: StealField): Array<{ from: number; to: number }> {
  const out: Array<{ from: number; to: number }> = []
  if (field.gapFrom >= MIN_SPAN) out.push({ from: 0, to: field.gapFrom })
  if (FIELD_WIDTH - field.gapTo >= MIN_SPAN) out.push({ from: field.gapTo, to: FIELD_WIDTH })
  return out
}

function hitsParent(field: StealField, p: Point): boolean {
  if (p.y + RUNNER_RADIUS < field.bandTop || p.y - RUNNER_RADIUS > field.bandBottom) return false
  return parentSpans(field).some(
    (span) => p.x + RUNNER_RADIUS > span.from && p.x - RUNNER_RADIUS < span.to,
  )
}

function hitsEgg(field: StealField, p: Point): boolean {
  const dx = p.x - field.egg.x
  const dy = p.y - field.egg.y
  return dx * dx + dy * dy <= (EGG_RADIUS + RUNNER_RADIUS) ** 2
}

export type StealOutcome = 'success' | 'blocked' | 'stalled'

export interface StealRun {
  readonly outcome: StealOutcome
  /** 通った軌跡。画面がこれをなぞって描く */
  readonly path: readonly Point[]
  readonly traveled: number
}

/** 発射して結果を出す。⚠️ 角度以外に入力は無い（完全に決まる）。
 *  @param angle 上向きを 0 とし、時計回りの弧度 */
export function launch(field: StealField, angle: number, budget: number): StealRun {
  const path: Point[] = [{ ...field.start }]
  let x = field.start.x
  let y = field.start.y
  // 上向きが -y。角度は上向き基準の時計回り
  let dx = Math.sin(angle)
  let dy = -Math.cos(angle)
  let traveled = 0

  while (traveled < budget) {
    x += dx * STEP
    y += dy * STEP
    traveled += STEP

    // ⭐ 壁で跳ね返る。これがあるので、親を避けてから卵へ戻る道ができる
    if (x < RUNNER_RADIUS) {
      x = RUNNER_RADIUS
      dx = -dx
    } else if (x > FIELD_WIDTH - RUNNER_RADIUS) {
      x = FIELD_WIDTH - RUNNER_RADIUS
      dx = -dx
    }
    if (y < RUNNER_RADIUS) {
      y = RUNNER_RADIUS
      dy = -dy
    } else if (y > field.height - RUNNER_RADIUS) {
      y = field.height - RUNNER_RADIUS
      dy = -dy
    }

    const here = { x, y }
    path.push(here)

    if (hitsEgg(field, here)) return { outcome: 'success', path, traveled }
    if (hitsParent(field, here)) return { outcome: 'blocked', path, traveled }
  }
  return { outcome: 'stalled', path, traveled }
}

/** その飛距離で成功する角度が1つでもあるか（と、その角度）。
 *
 *  ⭐ 画面には出さない。**設計が解けるものになっているかを機械で確かめる**ために使う。
 *  ⚠️ 「解けない巣」を出荷したら、プレイヤーは運が悪いのだと思ってしまう。 */
export function findSolution(
  field: StealField,
  budget: number,
  samples = 720,
): { angle: number; traveled: number } | null {
  let best: { angle: number; traveled: number } | null = null
  for (let i = 0; i < samples; i++) {
    // 上向き ±80度 を走査（真下へ撃つ意味は無い）
    const angle = (-80 + (160 * i) / (samples - 1)) * (Math.PI / 180)
    const run = launch(field, angle, budget)
    if (run.outcome !== 'success') continue
    if (!best || run.traveled < best.traveled) best = { angle, traveled: run.traveled }
  }
  return best
}
