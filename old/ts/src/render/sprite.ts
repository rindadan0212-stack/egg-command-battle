/** 添字色（index color）のドット絵。
 *
 *  ⭐ **変異＝パレットスワップ**。絵は1つだけ持ち、色の組だけ差し替える。
 *  これで1体ぶんのドットから変異個体が無限に作れる（ARK と同じ手法）。
 *
 *  ⚠️ 拡大は「1画素 → scale×scale の矩形」を直接塗る。
 *  画像を引き伸ばすと整数でない拡縮で縁がガタつくため、補間の経路に載せない。
 */

/** 添字0は必ず透明。1以降がパレットの色を指す。 */
export interface Sprite {
  readonly width: number
  readonly height: number
  readonly pixels: Uint8Array
}

export type Palette = readonly string[]

/** '.' を透明、'1'〜'9' をパレットの添字として読む。 */
export function parseSprite(rows: readonly string[]): Sprite {
  const height = rows.length
  if (height === 0) throw new Error('parseSprite: 行が無い')
  const width = (rows[0] as string).length

  const pixels = new Uint8Array(width * height)
  rows.forEach((row, y) => {
    if (row.length !== width) {
      throw new Error(`parseSprite: ${y} 行目の幅が ${row.length}（期待 ${width}）`)
    }
    for (let x = 0; x < width; x++) {
      const ch = row[x] as string
      pixels[y * width + x] = ch === '.' ? 0 : Number(ch)
    }
  })

  return { width, height, pixels }
}

/** 実寸 scale 倍で canvas に描く。補間を通さない。 */
export function drawSprite(
  ctx: CanvasRenderingContext2D,
  sprite: Sprite,
  palette: Palette,
  scale: number,
): void {
  const { width, height, pixels } = sprite
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const index = pixels[y * width + x] as number
      if (index === 0) continue
      const color = palette[index - 1]
      if (color === undefined) {
        throw new Error(`drawSprite: パレットに添字 ${index} が無い`)
      }
      ctx.fillStyle = color
      ctx.fillRect(x * scale, y * scale, scale, scale)
    }
  }
}

/** 撮影しやすいよう、1体ぶんを独立した canvas にして返す。 */
export function spriteToCanvas(sprite: Sprite, palette: Palette, scale: number): HTMLCanvasElement {
  const canvas = document.createElement('canvas')
  canvas.width = sprite.width * scale
  canvas.height = sprite.height * scale
  const ctx = canvas.getContext('2d')
  if (!ctx) throw new Error('2D コンテキストを取れなかった')
  ctx.imageSmoothingEnabled = false
  drawSprite(ctx, sprite, palette, scale)
  return canvas
}
