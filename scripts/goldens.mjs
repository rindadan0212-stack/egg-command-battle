#!/usr/bin/env node
/** C# 移植の答え合わせに使う「正解」を、TS を**実際に走らせて**書き出す。
 *
 *  ⭐ 移植の正しさを目視で決めない。ここが出した JSON と C# の出力が
 *  1文字でも違えば移植の失敗であって、JSON を書き換えて合わせてはいけない。
 *
 *  ⚠️ 較正済みの数値（変異 2.5%×3回・HP3倍/手数2倍 など）は乱数の系列が
 *  1ビットも違わないことに依存している。だから系列そのものも書き出す。
 *
 *  使い方:
 *    node scripts/goldens.mjs          unity-port/goldens/*.json を更新
 *    node scripts/goldens.mjs --check  既存と差が出たら 1 で落ちる（移植を触らず TS を変えた検出用）
 */

import { mkdirSync, readFileSync, writeFileSync, existsSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

// ⚠️ パスは URL のまま組み立てる。プロジェクトの場所に空白が入っているので、
//    文字列で繋ぐと %20 の扱いで壊れる（sim.mjs と同じ理由）。
const SRC = new URL('../src/', import.meta.url)
const src = (p) => new URL(p, SRC).href
const OUT = fileURLToPath(new URL('../unity-port/goldens/', import.meta.url))

const { Rng, hashString } = await import(src('core/rng.ts'))
const { STAT_KEYS, WILD_STAT_MAX, WILD_TOTAL_MAX, MUTATION_CAP_STEPS, wildStatMaxFor, wildTotalMaxFor, totalOf, applyTotalCap, actualStats } = await import(src('game/stats.ts'))
const { SKILL_LIST, DAMAGE_POWER, BUFF_PERCENT, TICK_PERCENT, effectiveCt, isHarmful, gachaPoolOf, skillById } = await import(src('game/skills.ts'))
const { SPECIES_LIST, ELEMENTS, ELEMENT_BEATS, ELEMENT_LABELS, SPECIES_BASE_TOTAL } = await import(src('game/species.ts'))

const checkOnly = process.argv.includes('--check')

/** 並びが揺れると差分が意味を失うので、キー順を固定して書く。 */
function stable(value) {
  return JSON.stringify(value, null, 2) + '\n'
}

function emit(name, value) {
  const path = OUT + name + '.json'
  const next = stable(value)
  if (checkOnly) {
    const prev = existsSync(path) ? readFileSync(path, 'utf8') : ''
    if (prev !== next) {
      console.error(`golden がずれている: ${name}.json`)
      process.exitCode = 1
    }
    return
  }
  writeFileSync(path, next)
  console.log(`  ${name}.json`)
}

if (!checkOnly) mkdirSync(OUT, { recursive: true })

// ── 乱数 ────────────────────────────────────────────
// ⭐ ここが全部の土台。系列がずれたら較正済みの数値が全部無効になる。
{
  const seeds = [0, 1, 42, 20260815, 4294967295]
  const streams = ['', 'breeding', 'battle', 'steal', 'nest']
  const detail = []
  for (const seed of seeds) {
    for (const stream of streams) {
      // stream:'' は根の系統そのもの（stream() を通さない）
      const rng = stream === '' ? new Rng(seed) : new Rng(seed).stream(stream)
      detail.push({
        seedOfRng: rng.seed,
        seed,
        stream,
        u32: Array.from({ length: 6 }, () => rng.u32()),
        float: Array.from({ length: 4 }, () => rng.float()),
        int0to100: Array.from({ length: 6 }, () => rng.int(0, 100)),
        intNeg: Array.from({ length: 4 }, () => rng.int(-5, 5)),
        chance025: Array.from({ length: 8 }, () => rng.chance(0.025)),
        pick: Array.from({ length: 5 }, () => rng.pick(['a', 'b', 'c', 'd'])),
        shuffle: rng.shuffle([1, 2, 3, 4, 5, 6, 7, 8]),
        sample2: rng.sample([10, 20, 30, 40], 2),
      })
    }
  }
  emit('rng', {
    hashString: ['', 'a', 'breeding', 'battle', 'steal', 'nest', 'ヌシ'].map((s) => ({
      text: s,
      hash: hashString(s),
    })),
    streams: detail,
  })
}

// ── ステータス ──────────────────────────────────────
{
  const blocks = [
    { hp: 0, atk: 0, def: 0, spd: 0 },
    { hp: 40, atk: 40, def: 40, spd: 40 },
    { hp: 40, atk: 40, def: 0, spd: 0 },
    { hp: 30, atk: 30, def: 30, spd: 10 },
    { hp: 25, atk: 25, def: 25, spd: 25 },
    { hp: 1, atk: 2, def: 3, spd: 4 },
    { hp: 39, atk: 38, def: 37, spd: 36 },
    { hp: 100, atk: -5, def: 12, spd: 7 },
    { hp: 12, atk: 12, def: 11, spd: 11 },
  ]
  const caps = []
  for (const wild of blocks) {
    for (const mutation of [0, 1, 3, 7, 20, 25]) {
      caps.push({ wild, mutation, out: applyTotalCap(wild, mutation), total: totalOf(applyTotalCap(wild, mutation)) })
    }
  }
  emit('stats', {
    statKeys: [...STAT_KEYS],
    wildStatMax: WILD_STAT_MAX,
    wildTotalMax: WILD_TOTAL_MAX,
    mutationCapSteps: MUTATION_CAP_STEPS,
    maxFor: Array.from({ length: 26 }, (_, m) => ({
      mutation: m,
      statMax: wildStatMaxFor(m),
      totalMax: wildTotalMaxFor(m),
    })),
    totalOf: blocks.map((b) => ({ block: b, total: totalOf(b) })),
    applyTotalCap: caps,
    actualStats: [
      {
        base: { hp: 24, atk: 18, def: 22, spd: 16 },
        wild: { hp: 20, atk: 10, def: 30, spd: 5 },
        trained: { hp: 3, atk: 0, def: 1, spd: 2 },
        out: actualStats({ hp: 24, atk: 18, def: 22, spd: 16 }, { hp: 20, atk: 10, def: 30, spd: 5 }, { hp: 3, atk: 0, def: 1, spd: 2 }),
      },
    ],
  })
}

// ── スキル ──────────────────────────────────────────
{
  emit('skills', {
    damagePower: DAMAGE_POWER,
    buffPercent: BUFF_PERCENT,
    tickPercent: TICK_PERCENT,
    list: SKILL_LIST.map((s) => ({
      id: s.id,
      name: s.name,
      gist: s.gist,
      ct: s.ct,
      target: s.target,
      effects: s.effects,
      ctSlot0: effectiveCt(0, s),
      ctSlot1: effectiveCt(1, s),
      ctSlot2: effectiveCt(2, s),
      harmful: s.effects.map((e) => isHarmful(e)),
    })),
    gachaPools: SPECIES_LIST.map((sp) => ({
      species: sp.id,
      skill1: sp.skill1,
      pool: [...gachaPoolOf(sp.id, sp.skill1)],
    })),
  })
}

// ── 種族 ────────────────────────────────────────────
{
  emit('species', {
    elements: [...ELEMENTS],
    elementLabels: ELEMENT_LABELS,
    elementBeats: ELEMENT_BEATS,
    baseTotal: SPECIES_BASE_TOTAL,
    list: SPECIES_LIST.map((s) => ({
      id: s.id,
      name: s.name,
      element: s.element,
      skill1: s.skill1,
      base: s.base,
      baseTotal: totalOf(s.base),
      spriteWidth: s.sprite.width,
      spriteHeight: s.sprite.height,
      // ⭐ 添字色そのものを比べる。ここがずれると変異のパレットスワップが崩れる
      spriteRows: Array.from({ length: s.sprite.height }, (_, y) =>
        Array.from({ length: s.sprite.width }, (_, x) => s.sprite.pixels[y * s.sprite.width + x]).join(''),
      ),
      palettes: s.palettes.map((p) => [...p]),
      skill1Name: skillById(s.skill1).name,
    })),
  })
}

if (checkOnly && process.exitCode === 1) {
  console.error('TS 側が変わっている。移植を直す前に、この差が意図したものか確かめる')
} else if (checkOnly) {
  console.log('golden: TS と一致')
} else {
  console.log(`golden を書き出した → unity-port/goldens/`)
}
