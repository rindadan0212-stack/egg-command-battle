/** スキル。枠は3つ。
 *
 *  | 枠 | 決まり方 |
 *  |---|---|
 *  | 1 | **種族固定**（配合では種族と連動して 50% でどちらかの親から） |
 *  | 2・3 | 卵ガチャ または 遺伝（配合では両親の4枠から2つ抽選） |
 *
 *  ⭐ 枠1が種族固定なので、種族の意味が構造的に残る。
 *
 *  ⚠️ **スキルを個別にコードで書かない。**
 *  少数のプリミティブ（ダメージ / 回復 / 段階 / ゲージ）の組み合わせをデータで表す。
 *  種類が増えても検証が掛け算にならないようにするため。
 *
 *  ⚠️ **CT は行動回数で減衰する。**使った本人が何回行動するまで再使用できないか、を表す。
 */

import type { StatKey } from './stats.ts'

export type SkillId = string

/** 誰に効くか。 */
export type Target =
  | 'enemyOne' // 敵1体（AI は残 HP の低い相手を狙う）
  | 'enemyAll' // 敵全体
  | 'allyLowest' // 残 HP 割合が最も低い味方（自分を含む）
  | 'self'

/** 効果のプリミティブ。ここを増やすときは本当に組み合わせで表せないか先に疑う。 */
export type Effect =
  /** scale が 'def' のものは「防御が高いほど強い一撃」になる */
  | { readonly kind: 'damage'; readonly power: number; readonly scale: 'atk' | 'def' }
  | { readonly kind: 'heal'; readonly power: number }
  | { readonly kind: 'stage'; readonly stat: StatKey; readonly delta: number }
  /** ゲージを進める / 戻す（正で自分が早く動く、負で相手を遅らせる） */
  | { readonly kind: 'gauge'; readonly delta: number }
  /** ⭐ 味方への単体攻撃を、あと hits 回ぶん自分が肩代わりする。
   *
   *  ⚠️ **これが無いと「壁」という役割が成立しない。**
   *  敵は最も脆い相手から狙うので、守る手段が無いと
   *  「得意2つ」の脆い個体を入れた瞬間に数的不利になる
   *  （実測: 役割分担編成の勝率が 23%）。耐久型が「自分が死なない」だけでは
   *  チームに貢献しない。 */
  | { readonly kind: 'cover'; readonly hits: number }

export interface Skill {
  readonly id: SkillId
  readonly name: string
  /** 何をするスキルなのかの短い説明 */
  readonly gist: string
  /** 使ったあと、自分が何回行動するまで使えないか。0 は毎回使える */
  readonly ct: number
  readonly target: Target
  readonly effects: readonly Effect[]
}

/** 通常攻撃。**全員がいつでも使える**ので枠を消費しない。
 *  ⭐ これが無いと、全スキルが CT 中のとき手が無くなって戦闘が止まる。 */
export const BASIC_ATTACK: Skill = {
  id: 'basic',
  name: 'たたかう',
  gist: '通常攻撃',
  ct: 0,
  target: 'enemyOne',
  effects: [{ kind: 'damage', power: 9, scale: 'atk' }],
}

const LIST: readonly Skill[] = [
  {
    id: 'strike',
    name: '強撃',
    gist: '単体に大ダメージ',
    ct: 3,
    target: 'enemyOne',
    effects: [{ kind: 'damage', power: 20, scale: 'atk' }],
  },
  {
    id: 'haste',
    name: '迅速',
    gist: '自身の速度を上げる',
    ct: 4,
    target: 'self',
    effects: [{ kind: 'stage', stat: 'spd', delta: 1 }],
  },
  {
    id: 'slow',
    name: '鈍足',
    gist: '敵の速度を下げる',
    ct: 4,
    target: 'enemyOne',
    effects: [{ kind: 'stage', stat: 'spd', delta: -1 }],
  },
  {
    id: 'guard',
    name: '守勢',
    gist: '自身の防御を上げる',
    ct: 3,
    target: 'self',
    effects: [{ kind: 'stage', stat: 'def', delta: 1 }],
  },
  {
    id: 'mend',
    name: '手当',
    gist: '傷ついた味方を回復',
    ct: 4,
    target: 'allyLowest',
    effects: [{ kind: 'heal', power: 18 }],
  },
  {
    id: 'shellbash',
    name: '殻打ち',
    gist: '防御が高いほど強い一撃',
    ct: 3,
    target: 'enemyOne',
    effects: [{ kind: 'damage', power: 20, scale: 'def' }],
  },
  {
    id: 'cover',
    name: 'かばう',
    gist: '味方への攻撃を肩代わりする',
    ct: 3,
    target: 'self',
    effects: [{ kind: 'cover', hits: 3 }],
  },
  {
    id: 'sweep',
    name: '薙ぎ',
    gist: '敵全体に小ダメージ',
    ct: 5,
    target: 'enemyAll',
    effects: [{ kind: 'damage', power: 11, scale: 'atk' }],
  },
  {
    id: 'quake',
    name: '震撼',
    gist: '敵全体に重い一撃。次が遠い',
    // ⚠️ CT が長いぶん一撃が重い。⭐ 「いつ来るか」を数えさせるための札
    ct: 7,
    target: 'enemyAll',
    effects: [{ kind: 'damage', power: 30, scale: 'atk' }],
  },
  {
    id: 'stall',
    name: '足止め',
    gist: '敵1体の行動を大きく遅らせる',
    ct: 5,
    target: 'enemyOne',
    effects: [{ kind: 'gauge', delta: -450 }],
  },
]

export const SKILLS: ReadonlyMap<SkillId, Skill> = new Map(LIST.map((s) => [s.id, s]))
export const SKILL_LIST: readonly Skill[] = LIST

/** 知らない id を黙って握りつぶさない。表に無いものは「効かないだけ」で気づけないため。 */
export function skillById(id: SkillId): Skill {
  const skill = SKILLS.get(id)
  if (!skill) throw new Error(`スキル表に ${id} が無い`)
  return skill
}

/** 卵ガチャ（枠2・3）で出うるスキル。
 *
 *  ⭐ **種族ごとにプールを分ける。**
 *  全体プールにすると、どこで卵を奪っても同じ技が出るので
 *  「必要な技を持つ親の巣へ行く」という輪の駆動力が消える。
 *  分けておくと「この技が欲しいならこの種族の巣」という知識が育つ。
 *
 *  ⚠️ 枠1（種族固定）と同じ技はプールから外してある。
 *  同じ技が2枠を占めると片方が無駄になるため。 */
export const GACHA_POOLS: Readonly<Record<string, readonly SkillId[]>> = {
  // 鱗・守りの系統
  tamaru: ['guard', 'cover', 'mend', 'stall', 'strike'],
  // 牙・攻めの系統
  tsunoga: ['haste', 'slow', 'sweep', 'guard', 'shellbash'],
  // 羽・撹乱の系統
  haneru: ['haste', 'slow', 'stall', 'mend', 'strike'],
  // ヌシ。⚠️ 卵は落とさないが、表に無いと数える検査が落ちる
  nushi: ['slow', 'guard', 'cover', 'stall', 'shellbash'],
}

/** その種族の卵から出うる技。⚠️ 表に無い種族は黙って空にせず投げる。 */
export function gachaPoolOf(speciesId: string, skill1: SkillId): readonly SkillId[] {
  const pool = GACHA_POOLS[speciesId]
  if (!pool) throw new Error(`卵ガチャの表に ${speciesId} が無い`)
  return pool.filter((id) => id !== skill1)
}
