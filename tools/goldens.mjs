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
 *    node tools/goldens.mjs          game/goldens/*.json を更新
 *    node tools/goldens.mjs --check  既存と差が出たら 1 で落ちる（移植を触らず TS を変えた検出用）
 */

import { mkdirSync, readFileSync, writeFileSync, existsSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

// ⚠️ パスは URL のまま組み立てる。プロジェクトの場所に空白が入っているので、
//    文字列で繋ぐと %20 の扱いで壊れる（sim.mjs と同じ理由）。
const SRC = new URL('../old/ts/src/', import.meta.url)
const src = (p) => new URL(p, SRC).href
const OUT = fileURLToPath(new URL('../game/goldens/', import.meta.url))

const { Rng, hashString } = await import(src('core/rng.ts'))
const Nest = await import(src('game/nest.ts'))
const Breeding = await import(src('game/breeding.ts'))
const Battle = await import(src('game/battle.ts'))
const Ai = await import(src('game/ai.ts'))
const Steal = await import(src('game/steal.ts'))
const State = await import(src('game/state.ts'))
const Creature = await import(src('game/creature.ts'))
const Storage = await import(src('game/storage.ts'))
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

// ── 巣・卵・孵化 ────────────────────────────────────
// ⚠️ 乱数の消費順がそのまま出る。ここがずれたら以降の全部がずれる。
{
  const creature = (c) => ({
    id: c.id, speciesId: c.speciesId, wild: c.wild, trained: c.trained,
    earned: c.earned, mutationCounter: c.mutationCounter, skills23: c.skills23,
    paletteIndex: c.paletteIndex, generation: c.generation,
    actual: Creature.statsOf(c), wildTotal: Creature.wildTotalOf(c),
  })
  const eggOf = (e) => ({
    id: e.id, speciesId: e.speciesId, wild: e.wild, mutationCounter: e.mutationCounter,
    paletteIndex: e.paletteIndex, generation: e.generation, how: e.how, skills23: e.skills23,
  })

  const defenders = []
  const eggs = []
  for (const nest of Nest.NESTS) {
    const rng = new Rng(777).stream(nest.id)
    defenders.push({ nest: nest.id, units: Nest.makeNestDefenders(rng, nest).map(creature) })
    for (const how of ['defeated', 'stolen']) {
      const r = new Rng(4242).stream(nest.id + how)
      const egg = Nest.makeEgg(r, nest, how, 7)
      eggs.push({ nest: nest.id, how, egg: eggOf(egg), hatched: creature(Nest.hatch(r, egg, 'c007')) })
    }
  }

  emit('nest', {
    tiers: [0, 1, 2, 3, 4, 5, 6].map((t) => ({ tier: t, wildTotal: Nest.wildTotalForTier(t) })),
    nests: Nest.NESTS.map((n) => ({ id: n.id, name: n.name, speciesId: n.speciesId, tier: n.tier })),
    defenders,
    eggs,
    bossName: Nest.BOSS_NAME,
    boss: Nest.makeBossParty().map(creature),
  })

  // ── 配合 ──────────────────────────────────────────
  // ⭐ 較正済みの「変異 2.5%×3回」がここに乗っている
  const parents = []
  const g = State.newGame(20260816)
  const pool = [...g.storage.creatures]
  const bred = []
  // ⚠️ 12件では変異が1度も出ず（7.31%/回）、一番較正に敏感な経路が試されないままだった。
  //    変異あり・なしの両方が必ず入る件数まで増やす。
  for (let seed = 0; seed < 150; seed++) {
    const rng = new Rng(1000 + seed).stream('breed')
    const a = pool[seed % pool.length]
    const b = pool[(seed + 1) % pool.length]
    if (a.id === b.id) continue
    const outcome = Breeding.breed(rng, a, b, 100 + seed)
    bred.push({ seed: 1000 + seed, a: a.id, b: b.id, mutations: outcome.mutations, egg: eggOf(outcome.egg) })
  }
  for (const c of pool) parents.push(creature(c))

  emit('breeding', {
    inheritHigher: Breeding.INHERIT_HIGHER,
    mutationRolls: Breeding.MUTATION_ROLLS,
    mutationChance: Breeding.MUTATION_CHANCE,
    mutationStep: Breeding.MUTATION_STEP,
    mutationCounterLimit: Breeding.MUTATION_COUNTER_LIMIT,
    parents,
    bred,
  })
}

// ── ゲーム全体の進行 ────────────────────────────────
// ⭐ newGame から一連の操作までを丸ごと。系統ごとの乱数がずれていないかが出る。
{
  const snapshot = (game) => ({
    serial: game.serial,
    creatures: game.storage.creatures.map((c) => ({
      id: c.id, speciesId: c.speciesId, wild: c.wild, skills23: c.skills23,
      mutationCounter: c.mutationCounter, generation: c.generation, earned: c.earned,
    })),
    eggs: game.eggs.map((e) => ({ id: e.id, speciesId: e.speciesId, wild: e.wild, how: e.how })),
    party: [...game.party],
    partyOf: State.partyOf(game).map((c) => c.id),
  })

  const runs = []
  for (const seed of [1, 20260816, 999999]) {
    const game = State.newGame(seed)
    const steps = [{ step: 'newGame', state: snapshot(game) }]

    State.gainEgg(game, Nest.nestById('thicket-fang'), 'defeated')
    steps.push({ step: 'gainEgg', state: snapshot(game) })

    const eggId = game.eggs[0].id
    State.hatchEgg(game, eggId)
    steps.push({ step: 'hatchEgg', state: snapshot(game) })

    const ids = game.storage.creatures.map((c) => c.id)
    State.breedPair(game, ids[0], ids[1])
    steps.push({ step: 'breedPair', state: snapshot(game) })

    State.togglePartyMember(game, ids[2])
    State.togglePartyMember(game, ids[0])
    steps.push({ step: 'toggleParty', state: snapshot(game) })

    State.awardParty(State.partyOf(game), 2)
    steps.push({ step: 'awardParty', state: snapshot(game) })

    runs.push({ seed, steps })
  }

  emit('game', {
    partySize: State.PARTY_SIZE,
    storageSlots: Storage.STORAGE_SLOTS,
    trainMax: Creature.TRAIN_MAX,
    runs,
  })
}

// ── 戦闘 ────────────────────────────────────────────
// ⭐ 乱数を使わないので、同じ編成からは必ず同じ試合になる。
//    ここが1手でもずれたら、較正済みの HP3倍 / 手数2倍 が意味を失う。
{
  const matchups = []
  for (const seed of [1, 20260816]) {
    const game = State.newGame(seed)
    const allies = State.partyOf(game)

    const cases = [
      { name: 'boss', enemies: Nest.makeBossParty() },
      ...Nest.NESTS.map((n) => ({
        name: n.id,
        enemies: Nest.makeNestDefenders(new Rng(555).stream(n.id), n),
      })),
    ]

    for (const c of cases) {
      const state = Battle.createBattle(allies, c.enemies)
      // 開幕の並び。⚠️ tempo と maxHp は体数の比から決まる
      const setup = state.units.map((u) => ({
        key: u.key, name: u.name, maxHp: u.maxHp, tempo: u.tempo,
        speed: Battle.speedOf(u),
      }))

      let guard = 0
      while (state.outcome === null && guard++ < Battle.MAX_ACTIONS * 3) {
        const actor = Battle.nextActor(state)
        if (!actor) break
        const action = Ai.chooseAction(state, actor)
        Battle.performAction(state, actor, action)
      }

      matchups.push({
        seed,
        name: c.name,
        setup,
        outcome: state.outcome,
        actions: state.actions,
        logLength: state.log.length,
        // ⭐ 全部は長すぎるので、先頭40件と末尾10件を比べる（ずれれば必ずどちらかに出る）
        logHead: state.log.slice(0, 40),
        logTail: state.log.slice(-10),
        finalHp: state.units.map((u) => ({ key: u.key, hp: u.hp })),
      })
    }
  }

  emit('battle', {
    gaugeMax: Battle.GAUGE_MAX,
    gaugeBase: Battle.GAUGE_BASE,
    maxActions: Battle.MAX_ACTIONS,
    hpScale: Battle.HP_SCALE,
    elementAdvantage: Battle.ELEMENT_ADVANTAGE,
    atkSoften: Battle.ATK_SOFTEN,
    defSoften: Battle.DEF_SOFTEN,
    damageNormalize: Battle.DAMAGE_NORMALIZE,
    // 式そのものを直接
    damageOf: [
      [12, 20, 30, 1], [20, 40, 60, 1], [30, 80, 120, 1.5], [42, 10, 200, 1 / 1.5],
      [20, 0, 0, 1], [12, 200, 0, 1.5],
    ].map(([p, a, d, m]) => ({ power: p, atk: a, def: d, mult: m, out: Battle.damageOf(p, a, d, m) })),
    effectiveStat: [[10, 0, 0], [10, 30, 3], [10, -30, 3], [1, -30, 3], [0, 0, 0]]
      .map(([b, pc, t]) => ({ base: b, percent: pc, turns: t, out: Battle.effectiveStat(b, { percent: pc, turns: t }) })),
    gaugeRate: [[0, 1], [26, 1], [10, 1.5], [3, 2]]
      .map(([s, t]) => ({ speed: s, tempo: t, out: Battle.gaugeRate(s, t) })),
    lone: [[3, 1], [3, 2], [3, 3], [1, 1]]
      .map(([a, e]) => {
        const s = Battle.loneScale(a, e)
        return { allies: a, enemies: e, scale: s, hp: Battle.loneHp(s), tempo: Battle.loneTempo(s) }
      }),
    matchups,
  })
}

// ── 卵強奪の発射 ────────────────────────────────────
{
  const fields = []
  for (let tier = 1; tier <= 5; tier++) {
    for (const side of ['left', 'right']) {
      const field = Steal.makeField(tier, side)
      const launches = []
      for (let deg = -80; deg <= 80; deg += 5) {
        const run = Steal.launch(field, (deg * Math.PI) / 180, 400)
        launches.push({ deg, outcome: run.outcome, traveled: run.traveled, pathLength: run.path.length })
      }
      const solution = Steal.findSolution(field, 400, 180)
      fields.push({
        tier, side,
        height: field.height, gapFrom: field.gapFrom, gapTo: field.gapTo,
        bandTop: field.bandTop, bandBottom: field.bandBottom,
        egg: field.egg, start: field.start,
        spans: Steal.parentSpans(field),
        launches,
        solution: solution === null ? null : { traveled: solution.traveled },
      })
    }
  }
  emit('steal', {
    fieldWidth: Steal.FIELD_WIDTH,
    speedToDistance: Steal.SPEED_TO_DISTANCE,
    gapWidth: Steal.GAP_WIDTH,
    lean: Steal.LEAN,
    eggRadius: Steal.EGG_RADIUS,
    runnerRadius: Steal.RUNNER_RADIUS,
    depths: [0, 1, 2, 3, 4, 5, 6].map((t) => ({ tier: t, depth: Steal.depthForTier(t) })),
    fields,
  })
}

if (checkOnly && process.exitCode === 1) {
  console.error('TS 側が変わっている。移植を直す前に、この差が意図したものか確かめる')
} else if (checkOnly) {
  console.log('golden: TS と一致')
} else {
  console.log(`golden を書き出した → game/goldens/`)
}
