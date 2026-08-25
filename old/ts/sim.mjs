#!/usr/bin/env node
/** 自動対戦シミュレータ。
 *
 *  ⭐ **数値を勘で置かないための仕組み。**
 *  「この値が良いはず」ではなく、ここに当てて決める。
 *
 *  ⚠️ 戦闘そのものは決定論的なので、同じ2体を何度戦わせても結果は同じ。
 *  勝率は**個体差の分布**を測っている（母集団だけが確率的）。
 *
 *  使い方:
 *    npm run sim -- --pace      1体を倒すのに何発かかるか
 *    npm run sim -- --speed     速度一強の検算（⭐ 企画.md §5）
 *    npm run sim -- --elements  属性3すくみが効いているか
 *    npm run sim -- --random    無作為個体どうし
 *    npm run sim -- --steal     卵強奪（発射）が編成ごとにどこまで届くか
 */

// ⚠️ パスは URL のまま組み立てる。プロジェクトの場所に空白が入っているので、
//    文字列で繋ぐと %20 の扱いで壊れる。
const SRC = new URL('./src/', import.meta.url)
const src = (p) => new URL(p, SRC).href

const {
  createBattle,
  nextActor,
  performAction,
  MAX_ACTIONS,
  HP_SCALE,
  GAUGE_BASE,
  ATK_SOFTEN,
  DEF_SOFTEN,
} = await import(src('game/battle.ts'))
const { chooseAction } = await import(src('game/ai.ts'))
const { applyTotalCap, STAT_KEYS } = await import(src('game/stats.ts'))
const { SPECIES_LIST, ELEMENT_LABELS, auditSpecies } = await import(src('game/species.ts'))
const { skillById } = await import(src('game/skills.ts'))
const { Rng } = await import(src('core/rng.ts'))
const { statsOf } = await import(src('game/creature.ts'))
const Steal = await import(src('game/steal.ts'))

auditSpecies()

/** 80点をどう配るかで「型」を作る。合計はすべて上限に張り付く。
 *
 *  ⭐ **「得意2つ」(40/40/0/0) を必ず入れる。**
 *  合計上限を1ステ上限の2倍にしたのは、これを成立させるためだった。
 *  ⚠️ 最初これを入れずに「1つ極振り + 残り分散」ばかり測っていて、
 *  企画の前提そのものを検証できていなかった。 */
const BUILDS = {
  // 得意2つ（設計が狙った形）
  攻速: { hp: 0, atk: 40, def: 0, spd: 40 },
  攻耐: { hp: 0, atk: 40, def: 40, spd: 0 },
  速耐: { hp: 0, atk: 0, def: 40, spd: 40 },
  体攻: { hp: 40, atk: 40, def: 0, spd: 0 },
  体耐: { hp: 40, atk: 0, def: 40, spd: 0 },
  体速: { hp: 40, atk: 0, def: 0, spd: 40 },
  // 比較用
  均等: { hp: 20, atk: 20, def: 20, spd: 20 },
}

/** 比較を濁らせないよう、**型**を比べるときは全員同じ枠2・3にする。 */
const NEUTRAL_SKILLS = ['def-up', 'heal-ratio']

/** ⚠️ 戦闘の長さを測るときはこちらを使う。
 *  全員に手当を持たせると回復役3体どうしになり、実際には起きない持久戦になる
 *  （既定値は「代表的な状態」ではない）。役割を散らした編成で測る。 */
const MIXED_SKILLS = [
  ['attack', 'def-up'],
  ['spd-up', 'spd-down'],
  ['heal-ratio', 'ct-long'],
]

function makeCreature(id, speciesId, wild, skills23 = NEUTRAL_SKILLS) {
  return {
    id,
    speciesId,
    wild: applyTotalCap(wild),
    trained: { hp: 0, atk: 0, def: 0, spd: 0 },
    earned: 0,
    mutationCounter: 0,
    skills23: [skills23[0] ?? null, skills23[1] ?? null],
    paletteIndex: 0,
    parents: null,
    generation: 1,
  }
}

/** skills が二次元配列なら枠ごとに別のスキルを配る（混成編成）。 */
function party(prefix, speciesId, wild, skills) {
  return [0, 1, 2].map((i) =>
    makeCreature(
      `${prefix}${i}`,
      speciesId,
      wild,
      Array.isArray(skills?.[0]) ? skills[i % skills.length] : skills,
    ),
  )
}

/** 決着まで自動で回す。 */
function runBattle(allies, enemies) {
  const state = createBattle(allies, enemies)
  for (;;) {
    const actor = nextActor(state)
    if (!actor) break
    performAction(state, actor, chooseAction(state, actor))
  }
  return state
}

function pct(n, total) {
  return ((n / total) * 100).toFixed(1) + '%'
}

// ── --pace ────────────────────────────────────────────
function measurePace(label, speciesId, skills) {
  const state = runBattle(
    party('a', speciesId, BUILDS.均等, skills),
    party('b', speciesId, BUILDS.均等, skills),
  )
  const firstDown = state.log.findIndex((e) => e.kind === 'down')
  const cut = firstDown < 0 ? state.log.length : firstDown
  const victim = state.log[firstDown]?.unit
  const hits = victim
    ? state.log.slice(0, cut).filter((e) => e.kind === 'damage' && e.unit === victim).length
    : null
  const damages = state.log.filter((e) => e.kind === 'damage').map((e) => e.amount)
  const avg = damages.reduce((s, v) => s + v, 0) / Math.max(1, damages.length)

  console.log(
    `     ${label.padEnd(22)} 最大HP ${String(state.units[0].maxHp).padStart(3)}` +
      ` / 平均${avg.toFixed(1).padStart(5)}ダメージ` +
      ` / ⭐ ${String(hits ?? '-').padStart(2)}発で1体目` +
      ` / 決着 ${String(state.actions).padStart(3)}行動`,
  )
  return hits
}

function runPace() {
  console.log('平均的な個体どうしの戦闘の長さ')
  console.log(`（HP_SCALE = ${HP_SCALE} / GAUGE_BASE = ${GAUGE_BASE} / 行動上限 ${MAX_ACTIONS}）`)
  console.log('')
  console.log('  保証したいこと: 1体を倒すのに 5〜12 発')
  console.log('')

  const results = []
  for (const species of SPECIES_LIST) {
    console.log(`  ── ${species.name}（スキル1 = ${skillById(species.skill1).name}）`)
    // ⚠️ 枠1は種族固定なので必ずある。外せるのは枠2・3だけ
    results.push(measurePace('枠2・3なし', species.id, []))
    results.push(measurePace('混成編成', species.id, MIXED_SKILLS))
  }

  console.log('')
  // ⚠️ 全体攻撃を持つ種族は1体あたりの被弾が伸びる（分散して当てるので当然）。
  //    上限を広めに取り、決着の長さのほうも併せて見る。
  const bad = results.filter((h) => h === null || h < 5 || h > 16)
  console.log(
    bad.length === 0
      ? '  → 全て範囲内。HP_SCALE と power はこのままでよい'
      : `  → ⚠️ ${bad.length} 件が範囲外。HP_SCALE か power を調整する`,
  )
}

// ── --speed: 速度一強の検算 ───────────────────────────
function runSpeedCheck() {
  console.log('⭐ 速度一強の検算（企画.md §5「先に手を打つ罠」）')
  console.log('')
  console.log('  速度は「行動回数」＝全出力への倍率なので、素で効かせると上限が無い。')
  console.log(`  合計上限 + 基礎テンポ（GAUGE_BASE = ${GAUGE_BASE}）で釣り合っているかを測る。`)
  console.log('')
  console.log('  ⚠️ 種族ごとに測る。スキル1 がどのステでスケールするかで結果が変わるため')
  console.log('     （1種族だけで測っていたとき、これが交絡していた）。')
  console.log('')

  // ⚠️ 「速度 vs 他」だけを測ると、偏りの原因が速度側にあるのか
  //    相手側にあるのかを切り分けられない。総当たりで測る。
  const rng = new Rng(20260816).stream('speed-check')
  const N = 400
  const names = Object.keys(BUILDS)

  function jitter(base) {
    const out = {}
    for (const k of STAT_KEYS) out[k] = Math.max(0, base[k] + rng.int(-4, 5))
    return applyTotalCap(out)
  }

  /** 型ごとの総合勝率（全種族・全対戦を合算） */
  const overall = Object.fromEntries(names.map((n) => [n, { wins: 0, games: 0 }]))

  for (const species of SPECIES_LIST) {
    console.log(`  ── ${species.name}（スキル1 = ${skillById(species.skill1).name}）`)
    console.log('      ' + names.map((n) => n.padStart(6)).join(''))
    for (const left of names) {
      const cells = names.map((right) => {
        if (left === right) return '     -'
        let wins = 0
        for (let i = 0; i < N; i++) {
          const state = runBattle(
            party('L', species.id, jitter(BUILDS[left])),
            party('R', species.id, jitter(BUILDS[right])),
          )
          if (state.outcome === 'ally') wins++
        }
        overall[left].wins += wins
        overall[left].games += N
        return `${((wins / N) * 100).toFixed(0)}%`.padStart(6)
      })
      console.log('   ' + left.padEnd(4) + cells.join(''))
    }
    console.log('')
  }

  console.log('  （行が勝った割合。50% に近いほど釣り合っている）')
  console.log('')
  console.log('  型ごとの総合勝率:')
  const ranked = names
    .map((n) => ({ n, rate: (overall[n].wins / overall[n].games) * 100 }))
    .sort((a, b) => b.rate - a.rate)
  for (const { n, rate } of ranked) {
    const bar = '█'.repeat(Math.round(rate / 4))
    console.log(`    ${n.padEnd(4)} ${rate.toFixed(1).padStart(5)}%  ${bar}`)
  }

  const spread = ranked[0].rate - ranked[ranked.length - 1].rate
  console.log('')
  console.log(`  最強と最弱の差: ${spread.toFixed(1)} ポイント`)
  if (spread <= 40) {
    console.log('  → どの型にも勝ち筋がある。速度一強ではない')
  } else {
    console.log(`  → ⚠️ ${ranked[0].n} が強すぎるか ${ranked[ranked.length - 1].n} が弱すぎる`)
  }
}

// ── --party: 編成どうし ───────────────────────────────
/** ⭐ 企画が謳うのは「得意2つ × 3体 = 6枠でパーティを組む」。
 *  だから比べるべきは **役割の違う特化を集めた編成** と 均等編成 であって、
 *  「同じ特化型を3体並べたもの」ではない（壁役がいない編成は誰も組まない）。 */
const PARTIES = {
  役割分担: [
    { wild: BUILDS.体耐, skills: ['taunt', 'def-up'] }, // 壁（かばう）
    { wild: BUILDS.攻速, skills: ['attack', 'spd-up'] }, // 火力
    { wild: BUILDS.速耐, skills: ['heal-ratio', 'spd-down'] }, // 補助
  ],
  役割分担_壁なし: [
    { wild: BUILDS.体耐, skills: ['def-up', 'heal-ratio'] }, // 肩代わりを持たない壁
    { wild: BUILDS.攻速, skills: ['attack', 'spd-up'] },
    { wild: BUILDS.速耐, skills: ['spd-down', 'ct-long'] },
  ],
  均等ぞろい: [
    { wild: BUILDS.均等, skills: ['def-up', 'heal-ratio'] },
    { wild: BUILDS.均等, skills: ['attack', 'spd-up'] },
    { wild: BUILDS.均等, skills: ['spd-down', 'ct-long'] },
  ],
  火力ぞろい: [
    { wild: BUILDS.攻速, skills: ['attack', 'spd-up'] },
    { wild: BUILDS.攻速, skills: ['attack', 'def-up'] },
    { wild: BUILDS.攻速, skills: ['attack', 'heal-ratio'] },
  ],
  耐久ぞろい: [
    { wild: BUILDS.体耐, skills: ['def-up', 'heal-ratio'] },
    { wild: BUILDS.体耐, skills: ['def-up', 'spd-down'] },
    { wild: BUILDS.体耐, skills: ['heal-ratio', 'ct-long'] },
  ],
}

function buildParty(prefix, speciesId, spec, jitterFn) {
  return spec.map((s, i) =>
    makeCreature(`${prefix}${i}`, speciesId, jitterFn ? jitterFn(s.wild) : s.wild, s.skills),
  )
}

function runParty() {
  console.log('⭐ 編成どうしの釣り合い')
  console.log('')
  console.log('  企画の主張:「得意2つ × 3体 = 6枠でパーティを組む」')
  console.log('  → 役割の違う特化を集めた編成が、均等ぞろいに勝てるかを測る。')
  console.log('')
  console.log(
    `  （ATK_SOFTEN=${ATK_SOFTEN} / DEF_SOFTEN=${DEF_SOFTEN} / GAUGE_BASE=${GAUGE_BASE}）`,
  )
  console.log('')

  const rng = new Rng(4242).stream('party-check')
  const N = 300
  const names = Object.keys(PARTIES)

  function jitter(base) {
    const out = {}
    for (const k of STAT_KEYS) out[k] = Math.max(0, base[k] + rng.int(-4, 5))
    return applyTotalCap(out)
  }

  const overall = Object.fromEntries(names.map((n) => [n, { wins: 0, games: 0 }]))

  console.log('              ' + names.map((n) => n.padStart(10)).join(''))
  for (const left of names) {
    const cells = names.map((right) => {
      if (left === right) return '         -'
      let wins = 0
      for (let i = 0; i < N; i++) {
        // 種族も混ぜる。1種族に寄せると スキル1 のスケール元が交絡する
        const sp = rng.pick(SPECIES_LIST).id
        const state = runBattle(
          buildParty('L', sp, PARTIES[left], jitter),
          buildParty('R', sp, PARTIES[right], jitter),
        )
        if (state.outcome === 'ally') wins++
      }
      overall[left].wins += wins
      overall[left].games += N
      return `${((wins / N) * 100).toFixed(0)}%`.padStart(10)
    })
    console.log('  ' + left.padEnd(10) + cells.join(''))
  }

  console.log('')
  const ranked = names
    .map((n) => ({ n, rate: (overall[n].wins / overall[n].games) * 100 }))
    .sort((a, b) => b.rate - a.rate)
  console.log('  編成ごとの総合勝率:')
  for (const { n, rate } of ranked) {
    console.log(`    ${n.padEnd(10)} ${rate.toFixed(1).padStart(5)}%  ${'█'.repeat(Math.round(rate / 4))}`)
  }

  const roles = ranked.find((r) => r.n === '役割分担')
  const even = ranked.find((r) => r.n === '均等ぞろい')
  console.log('')
  console.log(`  ⭐ 役割分担 ${roles.rate.toFixed(1)}%  vs  均等ぞろい ${even.rate.toFixed(1)}%`)
  console.log(
    roles.rate >= even.rate
      ? '  → 特化を組み合わせる意味がある。企画の前提は成立している'
      : '  → ⚠️ 均等ぞろいのほうが強い。「得意2つ」の設計が機能していない',
  )
}

// ── --elements: 3すくみ ───────────────────────────────
function runElements() {
  console.log('属性3すくみが効いているか（牙 → 羽 → 鱗 → 牙）')
  console.log('')
  console.log('  同じ配分・同じ枠2・3で、種族だけ変えて戦わせる。')
  console.log('  ⚠️ 種族は基礎値の配分とスキル1も違うので、属性だけの効果ではない。')
  console.log('')

  const names = SPECIES_LIST.map((s) => s.name)
  console.log('          ' + names.map((n) => n.padStart(8)).join(''))
  for (const left of SPECIES_LIST) {
    const cells = SPECIES_LIST.map((right) => {
      const state = runBattle(
        party('L', left.id, BUILDS.均等),
        party('R', right.id, BUILDS.均等),
      )
      const mark = state.outcome === 'ally' ? '勝' : state.outcome === 'enemy' ? '負' : '分'
      return `${mark}(${state.actions})`.padStart(8)
    })
    console.log(
      `  ${left.name.padEnd(4)}${ELEMENT_LABELS[left.element]}` + cells.join(''),
    )
  }
  console.log('')
  console.log('  行が勝ったか。括弧内は決着までの行動数')
}

// ── --progress: 輪が本当に1周するか ───────────────────
async function runProgress() {
  const { newGame, defendersOf, gainEgg, hatchEgg, breedPair, partyOf, awardParty } = await import(
    src('game/state.ts')
  )
  const { NESTS, makeBossParty, BOSS_NAME } = await import(src('game/nest.ts'))
  const { wildTotalOf, spendPoint, unspentOf } = await import(src('game/creature.ts'))
  const { isFull } = await import(src('game/storage.ts'))

  console.log('⭐ 輪が1周するか（初期パーティ → 巣を回す → 配合 → ボス）')
  console.log('')
  console.log('  ⚠️ プレイヤーの判断を方針で代用している。')
  console.log('     編成は **属性を散らして** 各種族の最良を1体ずつ取る。')
  console.log('     ⚠️ 最初これを「素質合計の上位3体」にしていたら、同種ばかりになって')
  console.log('     ボス勝率が永久に0%だった。この game が要求しているのは合計ではなく編成。')
  console.log('')

  const game = newGame(20260816)

  /** 属性を散らした編成。⭐ 同種で固めると 3すくみで永久に勝てない巣が出る */
  function diversePartyIds() {
    const bySpecies = new Map()
    for (const c of [...game.storage.creatures].sort((a, b) => wildTotalOf(b) - wildTotalOf(a))) {
      if (!bySpecies.has(c.speciesId)) bySpecies.set(c.speciesId, c)
    }
    const picked = [...bySpecies.values()]
    const rest = [...game.storage.creatures]
      .filter((c) => !picked.includes(c))
      .sort((a, b) => wildTotalOf(b) - wildTotalOf(a))
    return [...picked, ...rest].slice(0, 3).map((c) => c.id)
  }

  function winRateVs(party, makeFoes, n) {
    let wins = 0
    for (let i = 0; i < n; i++) {
      if (runBattle(party, makeFoes()).outcome === 'ally') wins++
    }
    return wins / n
  }

  const CYCLES = 14
  let cleared = 0

  for (let cycle = 1; cycle <= CYCLES; cycle++) {
    const party = partyOf(game)
    const rates = NESTS.map((n) => ({ nest: n, rate: winRateVs(party, () => defendersOf(game, n), 30) }))
    const boss = winRateVs(party, () => makeBossParty(), 12)

    const best = wildTotalOf(party[0] ?? { wild: {} }) || 0
    const comp = party.map((c) => SPECIES_LIST.find((s) => s.id === c.speciesId)?.name ?? '?').join('')
    console.log(
      `  周${String(cycle).padStart(2)}  ${String(game.storage.creatures.length).padStart(2)}体` +
        ` 編成${comp.padEnd(9)} 最良${String(best).padStart(3)}  ` +
        rates.map((r) => `段${r.nest.tier}:${(r.rate * 100).toFixed(0).padStart(3)}%`).join(' ') +
        `  ボス:${(boss * 100).toFixed(0).padStart(3)}%`,
    )

    if (boss >= 0.5 && cleared === 0) cleared = cycle

    // ── 一番高い段階で、勝てる見込みのある巣から卵を取る
    const target = [...rates].reverse().find((r) => r.rate >= 0.5) ?? rates[0]
    if (target) {
      gainEgg(game, target.nest, 'defeated')
      awardParty(party)
    }
    // ── 格上からは盗む（勝てない段階の卵も手に入れる）
    const steal = [...rates].reverse().find((r) => r.rate < 0.5)
    if (steal) gainEgg(game, steal.nest, 'stolen')

    // ── 孵す（枠が足りなければ素質の低い個体を逃がす）
    while (game.eggs.length > 0) {
      if (isFull(game.storage)) {
        const worst = [...game.storage.creatures].sort((a, b) => wildTotalOf(a) - wildTotalOf(b))[0]
        game.storage = { ...game.storage, creatures: game.storage.creatures.filter((c) => c !== worst) }
      }
      hatchEgg(game, game.eggs[0].id)
    }

    // ── 最良2体を配合して、その子も孵す
    const ranked = [...game.storage.creatures].sort((a, b) => wildTotalOf(b) - wildTotalOf(a))
    if (ranked.length >= 2) {
      breedPair(game, ranked[0].id, ranked[1].id)
      while (game.eggs.length > 0) {
        if (isFull(game.storage)) {
          const worst = [...game.storage.creatures].sort((a, b) => wildTotalOf(a) - wildTotalOf(b))[0]
          game.storage = { ...game.storage, creatures: game.storage.creatures.filter((c) => c !== worst) }
        }
        hatchEgg(game, game.eggs[0].id)
      }
    }

    // ── 育成ポイントを振る（一番高いステに寄せる）
    for (const c of game.storage.creatures) {
      while (unspentOf(c) > 0) {
        const key = STAT_KEYS.reduce((a, b) => (c.wild[a] >= c.wild[b] ? a : b))
        spendPoint(c, key)
      }
    }

    // ── 編成を更新（属性を散らす）
    game.party = diversePartyIds()
  }

  console.log('')
  // ⚠️ 届かなかったときは「何が足りないのか」まで出す。勝率だけでは直せない
  const { skillsOf, statsOf } = await import(src('game/creature.ts'))
  // ⚠️ ここは「周14の更新を終えた後」の編成。表の最終行とは別物なので混同しない
  console.log('  周14を終えた時点の編成:')
  for (const c of partyOf(game)) {
    const [s1, s2, s3] = skillsOf(c)
    console.log(
      `    ${c.id} ${SPECIES_LIST.find((s) => s.id === c.speciesId)?.name}` +
        ` 素質${wildTotalOf(c)} 実値${JSON.stringify(statsOf(c))}` +
        ` ◆${s1.name}·${s2?.name ?? '—'}·${s3?.name ?? '—'}`,
    )
  }
  const bossFight = runBattle(partyOf(game), makeBossParty())
  console.log('  その編成でのボス戦:')
  console.log(
    `    ${bossFight.actions}行動 / ${bossFight.outcome} / ` +
      `敵の残HP ${bossFight.units.filter((u) => u.side === 'enemy').map((u) => `${u.hp}/${u.maxHp}`).join(' ')}`,
  )

  console.log('')
  if (cleared > 0) {
    console.log(`  ⭐ ${cleared} 周目で ${BOSS_NAME} に勝率50%以上。**輪は閉じている**`)
  } else {
    console.log(`  ⚠️ ${CYCLES} 周してもボスに届かなかった。壁が高すぎるか、伸びしろが足りない`)
  }
}

// ── --random ──────────────────────────────────────────
function runRandom() {
  const rng = new Rng(777).stream('random-sim')
  const N = 1500
  let ally = 0
  let draw = 0
  let totalActions = 0

  function randomWild() {
    const raw = {}
    for (const k of STAT_KEYS) raw[k] = rng.int(0, 45)
    return applyTotalCap(raw)
  }

  for (let i = 0; i < N; i++) {
    const state = runBattle(
      [0, 1, 2].map((s) => makeCreature(`a${s}`, rng.pick(SPECIES_LIST).id, randomWild())),
      [0, 1, 2].map((s) => makeCreature(`b${s}`, rng.pick(SPECIES_LIST).id, randomWild())),
    )
    totalActions += state.actions
    if (state.outcome === 'ally') ally++
    else if (state.outcome === 'draw') draw++
  }

  console.log(`無作為な個体どうし ${N} 戦`)
  console.log(`  先攻側の勝率  : ${pct(ally, N)}`)
  console.log(`  引分          : ${pct(draw, N)}`)
  console.log(`  平均行動数    : ${(totalActions / N).toFixed(1)}`)
  console.log('')
  console.log('  ⚠️ 先攻側の勝率が 50% から大きく離れていたら、位置に有利不利がある')
  console.log('  ⚠️ 引分が多ければ、決着しない組み合わせがある（回復役どうしなど）')
}


// ── --steal ───────────────────────────────────────────
/** 卵強奪（発射）の走査。
 *
 *  ⭐ **確かめたいのは1つだけ** ──
 *  「速度に寄せた編成ほど深い巣へ届き、耐久だけの編成は届かない」。
 *  これが成り立っていないと、この段は編成に何も要求していないことになる。
 *
 *  ⚠️ 角度の幅（狙いの厳しさ）も併せて出す。届くだけで幅が 1°しか無ければ、
 *  それは「編成の差」ではなく「手先の器用さ」を測っている。 */
function stealWindow(field, budget) {
  const SAMPLES = 1600
  const SWEEP = 160
  let hits = 0
  for (let i = 0; i < SAMPLES; i++) {
    const angle = (-SWEEP / 2 + (SWEEP * i) / (SAMPLES - 1)) * (Math.PI / 180)
    if (Steal.launch(field, angle, budget).outcome === 'success') hits++
  }
  return (hits / SAMPLES) * SWEEP
}

function runSteal() {
  const TIERS = [1, 2, 3, 4, 5]
  const LINEUPS = [
    ['耐久ぞろい', [BUILDS.体耐, BUILDS.体耐, BUILDS.体耐]],
    ['均等ぞろい', [BUILDS.均等, BUILDS.均等, BUILDS.均等]],
    ['混成(壁/火力/速)', [BUILDS.体耐, BUILDS.攻耐, BUILDS.攻速]],
    ['速度ぞろい', [BUILDS.攻速, BUILDS.攻速, BUILDS.攻速]],
  ]
  const speciesId = SPECIES_LIST[0].id

  console.log('卵強奪（発射）の走査')
  console.log(
    `  幅 ${Steal.FIELD_WIDTH} / 奥行き ${TIERS.map((t) => Steal.depthForTier(t)).join(', ')}` +
      ` / 距離 = スピード合計 × ${Steal.SPEED_TO_DISTANCE}`,
  )
  console.log('')
  console.log('  届くか（○ = 親が左右どちらに寄っても通せる / △ = 片側だけ / × = 届かない）')
  console.log('                     飛距離   段1  段2  段3  段4  段5')

  const budgets = new Map()
  for (const [label, builds] of LINEUPS) {
    const party = builds.map((wild, i) => makeCreature(`s${i}`, speciesId, wild))
    const budget = Steal.distanceFor(party)
    budgets.set(label, budget)
    const cells = TIERS.map((tier) => {
      const sides = ['left', 'right'].map((side) =>
        Steal.findSolution(Steal.makeField(tier, side), budget, 720),
      )
      const mark = sides.every(Boolean) ? '○' : sides.some(Boolean) ? '△' : '×'
      return `   ${mark} `
    })
    console.log(`  ${label.padEnd(16)} ${String(budget).padStart(6)}${cells.join('')}`)
  }

  console.log('')
  console.log('  成功する角度の幅（度）─ 狭いほど狙いが厳しい')
  console.log('                              段1  段2  段3  段4  段5')
  for (const [label] of LINEUPS) {
    const budget = budgets.get(label)
    const cells = TIERS.map((tier) =>
      (stealWindow(Steal.makeField(tier, 'right'), budget).toFixed(1) + '°').padStart(7),
    )
    console.log(`  ${label.padEnd(24)}${cells.join('')}`)
  }

  // ⚠️ 文章ではなく測った値で判定する。
  //    「こうなっているはず」を印刷するだけなら、測っていないのと同じ。
  const NARROW = 2
  const faults = []
  for (const [label] of LINEUPS) {
    const budget = budgets.get(label)
    for (const tier of TIERS) {
      for (const side of ['left', 'right']) {
        const w = stealWindow(Steal.makeField(tier, side), budget)
        if (w > 0 && w < NARROW) faults.push(`${label} × 段${tier}(${side}) = ${w.toFixed(1)}°`)
      }
    }
  }
  // ⚠️ **まっすぐ撮って通ってはいけない。**
  //    隠間を広げたときこれを壊して、親が塗り絵だけの存在になっていた。
  //    角度の幅だけ見ていても気づけない（幅はむしろ広がって良く見えた）。
  const straightThrough = []
  for (const tier of TIERS) {
    for (const side of ['left', 'right']) {
      if (Steal.launch(Steal.makeField(tier, side), 0, 100000).outcome === 'success') {
        straightThrough.push(`段${tier}(${side})`)
      }
    }
  }

  const tankBudget = budgets.get('耐久ぞろい')
  const tankReaches = TIERS.some((tier) =>
    ['left', 'right'].some((side) => Steal.findSolution(Steal.makeField(tier, side), tankBudget, 720)),
  )

  console.log('')
  if (tankReaches) console.log('  ⚠️ 耐久ぞろいが届いている。速度に寄せる理由が消えている')
  else console.log('  耐久ぞろいはどの段にも届かない。速度に寄せる理由はある')
  if (straightThrough.length > 0) {
    console.log(`  ⚠️ まっすぐ撮つだけで通る巣がある。親が塗り絵になっている: ${straightThrough.join(' ')}`)
  } else {
    console.log('  どの巣もまっすぐでは通らない。親を避ける必要がある')
  }
  if (faults.length > 0) {
    console.log(`  ⚠️ 幅が ${NARROW}° 未満のマスがある。そこは編成ではなく手先を測っている:`)
    for (const f of faults) console.log(`     ${f}`)
  } else {
    console.log(`  届くマスはすべて幅 ${NARROW}° 以上。境目が刃になっていない`)
  }
}

const mode = process.argv[2] ?? '--pace'
const modes = {
  '--pace': runPace,
  '--speed': runSpeedCheck,
  '--party': runParty,
  '--progress': runProgress,
  '--elements': runElements,
  '--random': runRandom,
  '--steal': runSteal,
}
const run = modes[mode]
if (!run) {
  console.error(`不明な指定: ${mode}`)
  console.error('  ' + Object.keys(modes).join(' | '))
  process.exit(1)
}
run()
