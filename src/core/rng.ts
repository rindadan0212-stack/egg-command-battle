/** 決定論の土台。乱数はすべてここを通す。
 *
 *  ⚠️ `Math.random()` は使わない（`npm run check:determinism` で落ちる）。
 *  同じ種からは必ず同じ結果が出ること。これが崩れていると、どんな観測の仕組みを足しても
 *  「たまたま違う」を排除できない。
 *
 *  ⭐ 系統(stream)を分ける理由:
 *  乱数の消費数が変わると以降の系列が全部ずれ、較正済みの検査が無効になる。
 *  系統を分けておけば、戦闘に新しい乱数の使い手が増えても遺伝の系列はずれない。
 *
 *    const root = new Rng(20260815)
 *    const breeding = root.stream('breeding')   // 配合・遺伝・変異
 *    const battle   = root.stream('battle')     // 命中・敵AI の揺れ
 */

const U32 = 0x1_0000_0000

/** FNV-1a。系統名を種に混ぜるためだけに使う（暗号用途ではない）。 */
export function hashString(text: string): number {
  let h = 0x811c9dc5
  for (let i = 0; i < text.length; i++) {
    h ^= text.charCodeAt(i)
    h = Math.imul(h, 0x01000193)
  }
  return h >>> 0
}

/** 1つの種を4語へ広げる。sfc32 の初期化に使う。 */
function splitmix32(seed: number): () => number {
  let s = seed | 0
  return () => {
    s = (s + 0x9e37_79b9) | 0
    let t = s ^ (s >>> 16)
    t = Math.imul(t, 0x21f0_aaad)
    t ^= t >>> 15
    t = Math.imul(t, 0x735a_2d97)
    t ^= t >>> 15
    return t >>> 0
  }
}

/** sfc32。状態128bit・整数演算のみ・速い。ゲーム用途には十分な品質。 */
function sfc32(a: number, b: number, c: number, d: number): () => number {
  return () => {
    a |= 0
    b |= 0
    c |= 0
    d |= 0
    const t = (((a + b) | 0) + d) | 0
    d = (d + 1) | 0
    a = b ^ (b >>> 9)
    b = (c + (c << 3)) | 0
    c = (c << 21) | (c >>> 11)
    c = (c + t) | 0
    return t >>> 0
  }
}

export class Rng {
  readonly seed: number
  readonly #next: () => number

  constructor(seed: number) {
    this.seed = seed >>> 0
    const expand = splitmix32(this.seed)
    this.#next = sfc32(expand(), expand(), expand(), expand())
    // 初期状態の偏りを流す
    for (let i = 0; i < 12; i++) this.#next()
  }

  /** 系統を分ける。同じ (親の種, 名前) からは必ず同じ系統が出る。 */
  stream(name: string): Rng {
    return new Rng((this.seed ^ hashString(name)) >>> 0)
  }

  /** 符号なし32bit整数。 */
  u32(): number {
    return this.#next()
  }

  /** [0, 1) の実数。 */
  float(): number {
    return this.#next() / U32
  }

  /** [min, maxExclusive) の整数。棄却法で偏りを出さない。 */
  int(min: number, maxExclusive: number): number {
    const range = maxExclusive - min
    if (!Number.isInteger(min) || !Number.isInteger(maxExclusive)) {
      throw new Error(`Rng.int には整数を渡す (min=${min}, maxExclusive=${maxExclusive})`)
    }
    if (range <= 0) {
      throw new Error(`Rng.int の範囲が空 (min=${min}, maxExclusive=${maxExclusive})`)
    }
    // 端数ぶんを捨てて一様性を保つ
    const limit = Math.floor(U32 / range) * range
    let v = this.#next()
    while (v >= limit) v = this.#next()
    return min + (v % range)
  }

  /** 確率 probability で true。0.0731 のような小さい値も扱える。 */
  chance(probability: number): boolean {
    return this.float() < probability
  }

  /** 1つ選ぶ。空配列は投げる（黙って undefined を返さない）。 */
  pick<T>(items: readonly T[]): T {
    if (items.length === 0) throw new Error('Rng.pick に空配列が渡された')
    return items[this.int(0, items.length)] as T
  }

  /** 破壊的にシャッフル（Fisher-Yates）。 */
  shuffle<T>(items: T[]): T[] {
    for (let i = items.length - 1; i > 0; i--) {
      const j = this.int(0, i + 1)
      const a = items[i] as T
      const b = items[j] as T
      items[i] = b
      items[j] = a
    }
    return items
  }

  /** 重複なしで n 個取り出す。配合の「4枠から2つ抽選」で使う。 */
  sample<T>(items: readonly T[], n: number): T[] {
    if (n > items.length) {
      throw new Error(`Rng.sample: ${items.length} 個から ${n} 個は取れない`)
    }
    return this.shuffle([...items]).slice(0, n)
  }
}
