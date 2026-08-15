/** ゲーム全体の状態。**唯一の出所。**
 *
 *  ⚠️ 乱数は系統を分けて持つ。片方で消費数が変わっても、もう片方の列がずれない
 *  （較正済みの検査を無効にしないため）。
 */

import { Rng } from '../core/rng.ts'
import { breed, type BreedOutcome } from './breeding.ts'
import { award, type Creature } from './creature.ts'
import { hatch, makeEgg, makeNestDefenders, nestById, type Egg, type Nest } from './nest.ts'
import { accept, emptyStorage, isFull, release, type Storage } from './storage.ts'

export interface Game {
  readonly seed: number
  storage: Storage
  /** 手に入れてまだ孵していない卵 */
  eggs: Egg[]
  /** 通し番号。id を一意にするためだけに使う */
  serial: number
  /** 系統ごとの乱数 */
  readonly rng: {
    readonly nest: Rng
    readonly egg: Rng
    readonly hatch: Rng
    readonly steal: Rng
    readonly breed: Rng
  }
}

export function newGame(seed: number): Game {
  const root = new Rng(seed)
  const game: Game = {
    seed,
    storage: emptyStorage(),
    eggs: [],
    serial: 0,
    rng: {
      nest: root.stream('nest'),
      egg: root.stream('egg'),
      hatch: root.stream('hatch'),
      steal: root.stream('steal'),
      breed: root.stream('breed'),
    },
  }

  // 最初の3体。一番浅い巣の卵を孵したところから始める
  const first = nestById('shallow-scale')
  for (let i = 0; i < 3; i++) {
    const egg = makeEgg(game.rng.egg, first, 'defeated', ++game.serial)
    game.storage = accept(game.storage, hatch(game.rng.hatch, egg, `c${String(game.serial).padStart(3, '0')}`))
  }
  return game
}

/** 巣の守り手。⚠️ 挑むたびに作り直す（同じ巣でも顔ぶれが変わる）。 */
export function defendersOf(game: Game, nest: Nest): Creature[] {
  return makeNestDefenders(game.rng.nest, nest)
}

export function gainEgg(game: Game, nest: Nest, how: Egg['how']): Egg {
  const egg = makeEgg(game.rng.egg, nest, how, ++game.serial)
  game.eggs.push(egg)
  return egg
}

/** 孵す。⚠️ 保管庫が満杯なら孵さない（黙って捨てない）。 */
export function hatchEgg(game: Game, eggId: string): Creature {
  if (isFull(game.storage)) {
    throw new Error(`保管庫が満杯（${game.storage.slots}枠）。先にどれかを逃がす`)
  }
  const index = game.eggs.findIndex((e) => e.id === eggId)
  if (index < 0) throw new Error(`${eggId} という卵は持っていない`)
  const [egg] = game.eggs.splice(index, 1)
  const creature = hatch(
    game.rng.hatch,
    egg as Egg,
    `c${String(++game.serial).padStart(3, '0')}`,
  )
  game.storage = accept(game.storage, creature)
  return creature
}

export function releaseCreature(game: Game, id: string): void {
  game.storage = release(game.storage, id)
}

export function creatureById(game: Game, id: string): Creature {
  const found = game.storage.creatures.find((c) => c.id === id)
  if (!found) throw new Error(`${id} は保管庫にいない`)
  return found
}

/** 配合する。卵は保管庫ではなく卵の棚に入る（孵すまでが1手間）。 */
export function breedPair(game: Game, aId: string, bId: string): BreedOutcome {
  const outcome = breed(
    game.rng.breed,
    creatureById(game, aId),
    creatureById(game, bId),
    ++game.serial,
  )
  game.eggs.push(outcome.egg)
  return outcome
}

/** 戦闘の報酬。⭐ 出撃していた個体だけがもらう（連れ出すことが育成に直結する）。 */
export function awardParty(party: readonly Creature[], amount = 1): void {
  for (const creature of party) award(creature, amount)
}
