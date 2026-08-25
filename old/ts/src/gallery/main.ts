/** ギャラリー — ドット絵を1体ずつ実寸で切り出して撮るための画面（教訓 §5.0-⑦）。
 *
 *  ⚠️ **AI が読む画像は縮小されて届く**（過去作では ~256px）。
 *  全体スクショでは崩れ・被り・見切れが見えないので、**1点ずつ撮れる枠**を用意する。
 *  各点に `#t-<id>` を振ってあるので、Playwright の target 指定でクロップ撮影できる:
 *
 *      browser_take_screenshot({ target: '#t-tamaru-1', filename: 'tamaru-1.png' })
 *
 *  ⭐ **順序が大事** — 意匠を作り始めてからでは、作った物を検分できない期間が生まれる。
 */

import { fingerprint, fingerprintAll } from '../core/fingerprint.ts'
import { auditSpecies, ELEMENT_LABELS, SPECIES_LIST } from '../game/species.ts'
import { EMPTY_SOURCE, startLiveReporting } from '../live/report.ts'
import { spriteToCanvas, type Palette, type Sprite } from '../render/sprite.ts'

auditSpecies()

interface Plate {
  id: string
  label: string
  sprite: Sprite
  palette: Palette
  fingerprint: string
}

const SCALES = [1, 2, 4, 8] as const
const DEFAULT_SCALE = 4

/** 種族 × パレット。⭐ 変異は色変化として出るので、パレットの数だけ姿がある。 */
const PLATES: readonly Plate[] = SPECIES_LIST.flatMap((species) =>
  species.palettes.map((palette, index) => ({
    id: `${species.id}-${index}`,
    label: `${species.name}${index === 0 ? '' : ` / 変異${index}`} · ${ELEMENT_LABELS[species.element]}`,
    sprite: species.sprite,
    palette,
    fingerprint: fingerprint(
      `${species.id}:${species.sprite.width}x${species.sprite.height}:${palette.join(',')}`,
    ),
  })),
)

function buildPlate(plate: Plate, scale: number): HTMLElement {
  const wrap = document.createElement('div')
  wrap.className = 'plate'
  // ⭐ この id が「1点だけ実寸で撮る」ための取っ手になる
  wrap.id = `t-${plate.id}`

  const frame = document.createElement('div')
  frame.className = 'frame'
  frame.append(spriteToCanvas(plate.sprite, plate.palette, scale))

  const label = document.createElement('span')
  label.className = 'label'
  // ⭐ 名前で話すと食い違う。指紋を並べて出しておく
  label.textContent = `${plate.label} · ${plate.sprite.width}×${plate.sprite.height} · #${plate.fingerprint}`

  wrap.append(frame, label)
  return wrap
}

function render(root: HTMLElement): void {
  let scale: number = DEFAULT_SCALE

  const heading = document.createElement('h1')
  heading.textContent = 'ギャラリー'

  const lead = document.createElement('p')
  lead.className = 'lead'
  lead.textContent = '1体ずつ実寸で撮る。全体スクショでは破綻が見えない。'

  const note = document.createElement('p')
  note.className = 'note'
  note.textContent =
    '種族 × パレット。同じドットで色だけ差し替えたものが変異個体になる。意匠は種族の定義ファイルの文字格子を直接書き換えれば直せる。'

  const controls = document.createElement('div')
  controls.className = 'controls'
  const shelf = document.createElement('div')
  shelf.className = 'shelf'

  const paint = (): void => {
    shelf.replaceChildren(...PLATES.map((plate) => buildPlate(plate, scale)))
  }

  for (const value of SCALES) {
    const button = document.createElement('button')
    button.type = 'button'
    button.textContent = `${value}×`
    button.dataset['on'] = String(value === scale)
    button.addEventListener('click', () => {
      scale = value
      for (const other of controls.querySelectorAll('button')) {
        other.dataset['on'] = String(other === button)
      }
      paint()
    })
    controls.append(button)
  }

  paint()
  root.append(heading, lead, note, controls, shelf)
}

const root = document.querySelector<HTMLElement>('#gallery')
if (root) render(root)

// ⚠️ ゲームとギャラリーは同時に開いているのが普通なので、画面ごとに欄を分けて申告する
startLiveReporting('gallery', {
  ...EMPTY_SOURCE,
  assets: () => ({
    count: PLATES.length,
    fingerprint: fingerprintAll(PLATES.map((p) => p.fingerprint)),
  }),
  scene: () => ({ plates: PLATES.map((p) => p.id) }),
})

export {}
