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

/** 威力の段位。
 *
 *  ⭐ **技ごとに数値を置かない。**段位を選ぶだけにする。
 *  こうすると独立した数値が「技の数」から**4つの定数**に減り、
 *  較正は表を1つ動かすだけで済む（勘で置いた数値が散らばらない）。
 *
 *  ⚠️ **全体攻撃は1段下げて選ぶ。**
 *  全体の「中」は単体の「中」よりずっと強いので、同じ段位にすると段位が意味を失う。 */
export const POWER_TIERS = ['小', '中', '大', '特大'] as const
export type PowerTier = (typeof POWER_TIERS)[number]

/** 攻撃の段位 → 威力。⭐ **ここが威力の唯一の出所。** */
export const DAMAGE_POWER: Readonly<Record<PowerTier, number>> = {
  小: 12,
  中: 20,
  大: 30,
  特大: 42,
}

/** 回復の段位 → 回復量。
 *  ⚠️ 攻撃と別の表にしてある。攻撃は攻防の式を通るが回復は素通しなので、
 *  同じ数値でも意味が違う。 */
export const HEAL_POWER: Readonly<Record<PowerTier, number>> = {
  小: 12,
  中: 20,
  大: 30,
  特大: 42,
}

/** 効果のプリミティブ。ここを増やすときは本当に組み合わせで表せないか先に疑う。 */
export type Effect =
  /** scale が 'def' のものは「防御が高いほど強い一撃」になる */
  | { readonly kind: 'damage'; readonly power: PowerTier; readonly scale: 'atk' | 'def' }
  | { readonly kind: 'heal'; readonly power: PowerTier }
  | { readonly kind: 'stage'; readonly stat: StatKey; readonly delta: number }
  /** ゲージを進める / 戻す（正で自分が早く動く、負で相手を遅らせる） */
  | { readonly kind: 'gauge'; readonly delta: number }
  /** ⭐ CT を動かす。負で短縮（自分の大技を早める）、正で延長（相手の大技を遅らせる）。
   *
   *  ⚠️ **枠1には効かない。** 枠1は「必ず打てる札」なので、
   *  そこに CT を乗せると手が無くなる戦闘が生まれる。 */
  | { readonly kind: 'ct'; readonly delta: number }
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

/** ⭐ **枠1（種族固定）の CT は常に 0。**
 *
 *  ⚠️ CT は**技ではなく枠の性質**として扱う。
 *  同じ技が、ある種族では枠1（CTなし）に、別の種族では枠2・3（CTあり）に入りうるため。
 *
 *  ⭐ これで「たたかう」が要らなくなった。
 *  全スキルが CT 中でも枠1は必ず打てるので、手が無くなる戦闘が生まれない。 */
export function effectiveCt(slot: number, skill: Skill): number {
  return slot === 0 ? 0 : skill.ct
}

const LIST: readonly Skill[] = [
  {
    id: 'strike',
    name: '強撃',
    gist: '単体に大ダメージ',
    ct: 3,
    target: 'enemyOne',
    effects: [{ kind: 'damage', power: '中', scale: 'atk' }],
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
    effects: [{ kind: 'heal', power: '中' }],
  },
  {
    id: 'shellbash',
    name: '殻打ち',
    gist: '防御が高いほど強い一撃',
    ct: 3,
    target: 'enemyOne',
    effects: [{ kind: 'damage', power: '中', scale: 'def' }],
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
    effects: [{ kind: 'damage', power: '小', scale: 'atk' }],
  },
  {
    id: 'surge',
    name: '猛り',
    gist: '自分の技の待ちを縮める',
    ct: 4,
    target: 'self',
    // ⭐ 大技を早く2度撃つための札。CT が芯の戦闘なので、それを操る手が要る
    effects: [{ kind: 'ct', delta: -2 }],
  },
  {
    id: 'snare',
    name: '絡み',
    gist: '敵1体の技の待ちを延ばす',
    ct: 5,
    target: 'enemyOne',
    // ⚠️ 枠1には効かない（相手の手を完全に奪わないため）
    effects: [{ kind: 'ct', delta: 2 }],
  },
  {
    id: 'quake',
    name: '震撼',
    gist: '敵全体に重い一撃。次が遠い',
    // ⚠️ CT が長いぶん一撃が重い。⭐ 「いつ来るか」を数えさせるための札
    ct: 7,
    target: 'enemyAll',
    effects: [{ kind: 'damage', power: '大', scale: 'atk' }],
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
  tamaru: ['guard', 'cover', 'mend', 'stall', 'strike', 'snare'],
  // 牙・攻めの系統
  tsunoga: ['haste', 'slow', 'sweep', 'guard', 'shellbash', 'surge'],
  // 羽・撹乱の系統
  haneru: ['haste', 'slow', 'stall', 'mend', 'strike', 'snare'],
  // ヌシ。⚠️ 卵は落とさないが、表に無いと数える検査が落ちる
  nushi: ['slow', 'guard', 'cover', 'stall', 'shellbash', 'surge'],
}

/** その種族の卵から出うる技。⚠️ 表に無い種族は黙って空にせず投げる。 */
export function gachaPoolOf(speciesId: string, skill1: SkillId): readonly SkillId[] {
  const pool = GACHA_POOLS[speciesId]
  if (!pool) throw new Error(`卵ガチャの表に ${speciesId} が無い`)
  return pool.filter((id) => id !== skill1)
}
