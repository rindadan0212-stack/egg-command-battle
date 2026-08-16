/** スキル。枠は3つ。
 *
 *  | 枠 | 決まり方 |
 *  |---|---|
 *  | 1 | **種族固定**（配合では種族と連動して 50% でどちらかの親から）・**CT なし** |
 *  | 2・3 | 卵ガチャ または 遺伝（配合では両親の4枠から2つ抽選） |
 *
 *  ⚠️ **「たたかう」は無い。** 枠1が CT 0 なので、全スキルが CT 中でも必ず打てる札が残る。
 *
 *  ⚠️ **スキルを個別にコードで書かない。**
 *  効果のプリミティブの組み合わせをデータで表す。
 *  種類が増えても検証が掛け算にならないようにするため。
 *
 *  ⚠️ **効果の名前は画面にそのまま出す語。** 凝った名前を付けない。
 */

import type { StatKey } from './stats.ts'

export type SkillId = string

/** 誰に効くか。 */
export type Target =
  | 'enemyOne' // 敵1体
  | 'enemyAll' // 敵全体
  | 'allyLowest' // 残 HP 割合が最も低い味方（自分を含む）
  | 'self'

/** 効き目の段位。
 *
 *  ⭐ **技ごとに数値を置かない。**段位を選ぶだけにする。
 *  独立した数値が「技の数」から**効果の種類ごとに4つ**まで減り、
 *  較正は表を動かすだけで済む（勘で置いた数値が散らばらない）。
 *
 *  ⚠️ **全体に効くものは1段下げて選ぶ。**
 *  全体の「中」は単体の「中」よりずっと強いので、同じ段位にすると段位が意味を失う。 */
export const POWER_TIERS = ['小', '中', '大', '特大'] as const
export type PowerTier = (typeof POWER_TIERS)[number]

/** 攻撃の威力。 */
export const DAMAGE_POWER: Readonly<Record<PowerTier, number>> = {
  小: 12,
  中: 20,
  大: 30,
  特大: 42,
}

/** ステータス系が動かす割合（%）。⭐ **ステータスの数値そのものに掛かる。** */
export const BUFF_PERCENT: Readonly<Record<PowerTier, number>> = {
  小: 15,
  中: 25,
  大: 40,
  特大: 60,
}

/** 毒・リジェネが1行動ごとに動かす、最大HP に対する割合（%）。 */
export const TICK_PERCENT: Readonly<Record<PowerTier, number>> = {
  小: 3,
  中: 5,
  大: 8,
  特大: 12,
}

/** HP割合回復・シールドの、最大HP に対する割合（%）。 */
export const RATIO_PERCENT: Readonly<Record<PowerTier, number>> = {
  小: 15,
  中: 25,
  大: 40,
  特大: 60,
}

/** 効果のプリミティブ。
 *
 *  ⚠️ ここを増やすときは、本当に組み合わせで表せないか先に疑う。
 *  ⚠️ 持続するものの単位は **「その個体の行動回数」**。CT と同じ数え方に揃えてある。 */
export type Effect =
  // ── 攻撃 ──────────────────────────────
  /** scale が 'def' のものは「防御が高いほど強い一撃」になる */
  | { readonly kind: 'damage'; readonly power: PowerTier; readonly scale: 'atk' | 'def' }

  // ── ステータス系（数値に ±%） ──────────
  /** 攻撃力/防御力/スピードの UP・DOWN。sign が +1 で UP、-1 で DOWN */
  | {
      readonly kind: 'buff'
      readonly stat: Extract<StatKey, 'atk' | 'def' | 'spd'>
      readonly power: PowerTier
      readonly sign: 1 | -1
      readonly turns: number
    }

  // ── HP系 ──────────────────────────────
  /** 毒。1行動ごとに最大HPの割合ぶん減る */
  | { readonly kind: 'poison'; readonly power: PowerTier; readonly turns: number }
  /** リジェネ。1行動ごとに最大HPの割合ぶん回復 */
  | { readonly kind: 'regen'; readonly power: PowerTier; readonly turns: number }
  /** HP割合回復。即時 */
  | { readonly kind: 'healRatio'; readonly power: PowerTier }
  /** シールド。HP より先に減る肩代わりの点数 */
  | { readonly kind: 'shield'; readonly power: PowerTier }

  // ── 行動系 ────────────────────────────
  /** スタン。その回数ぶん手番を飛ばす */
  | { readonly kind: 'stun'; readonly turns: number }
  /** CT短縮（負）/ CT延長（正）。⚠️ **枠1には効かない**（必ず打てる札を潰さないため） */
  | { readonly kind: 'ct'; readonly delta: number }
  /** 挑発。味方への単体攻撃を、あと hits 回ぶん自分が引き受ける */
  | { readonly kind: 'taunt'; readonly hits: number }

  // ── 特殊 ──────────────────────────────
  /** ガッツ。致死のダメージを HP1 で耐える */
  | { readonly kind: 'guts'; readonly turns: number }
  /** 免疫。DOWN・毒・スタンを受けない */
  | { readonly kind: 'immune'; readonly turns: number }

export interface Skill {
  readonly id: SkillId
  readonly name: string
  /** 何をするスキルなのかの短い説明 */
  readonly gist: string
  /** 使ったあと、自分が何回行動するまで使えないか。⚠️ 枠1では常に 0 扱い */
  readonly ct: number
  readonly target: Target
  readonly effects: readonly Effect[]
}

/** ⭐ **枠1（種族固定）の CT は常に 0。**
 *
 *  ⚠️ CT は**技ではなく枠の性質**として扱う。
 *  同じ技が、ある種族では枠1（CTなし）に、別の種族では枠2・3（CTあり）に入りうるため。 */
export function effectiveCt(slot: number, skill: Skill): number {
  return slot === 0 ? 0 : skill.ct
}

/** 弱い側の効果か（免疫が防ぐ対象）。 */
export function isHarmful(effect: Effect): boolean {
  if (effect.kind === 'buff') return effect.sign < 0
  return effect.kind === 'poison' || effect.kind === 'stun'
}

const LIST: readonly Skill[] = [
  // ── 攻撃 ──────────────────────────────
  {
    id: 'attack',
    name: '攻撃',
    gist: '敵1体にダメージ',
    ct: 3,
    target: 'enemyOne',
    effects: [{ kind: 'damage', power: '中', scale: 'atk' }],
  },
  {
    id: 'attack-heavy',
    name: '強攻撃',
    gist: '敵1体に大きなダメージ。次が遠い',
    ct: 6,
    target: 'enemyOne',
    effects: [{ kind: 'damage', power: '大', scale: 'atk' }],
  },
  {
    id: 'attack-all',
    name: '全体攻撃',
    gist: '敵全体にダメージ',
    // ⚠️ 全体なので1段下げて「小」
    ct: 5,
    target: 'enemyAll',
    effects: [{ kind: 'damage', power: '小', scale: 'atk' }],
  },
  {
    id: 'attack-all-heavy',
    name: '全体強攻撃',
    gist: '敵全体に大きなダメージ。次がとても遠い',
    ct: 7,
    target: 'enemyAll',
    effects: [{ kind: 'damage', power: '大', scale: 'atk' }],
  },
  {
    id: 'attack-def',
    name: '防御依存攻撃',
    gist: '防御力が高いほど強い一撃',
    ct: 3,
    target: 'enemyOne',
    effects: [{ kind: 'damage', power: '中', scale: 'def' }],
  },

  // ── ステータス系 ──────────────────────
  {
    id: 'atk-up',
    name: '攻撃力UP',
    gist: '自分の攻撃力を上げる',
    ct: 4,
    target: 'self',
    effects: [{ kind: 'buff', stat: 'atk', power: '中', sign: 1, turns: 3 }],
  },
  {
    id: 'atk-down',
    name: '攻撃力DOWN',
    gist: '敵1体の攻撃力を下げる',
    ct: 4,
    target: 'enemyOne',
    effects: [{ kind: 'buff', stat: 'atk', power: '中', sign: -1, turns: 3 }],
  },
  {
    id: 'def-up',
    name: '防御力UP',
    gist: '自分の防御力を上げる',
    ct: 4,
    target: 'self',
    effects: [{ kind: 'buff', stat: 'def', power: '中', sign: 1, turns: 3 }],
  },
  {
    id: 'def-down',
    name: '防御力DOWN',
    gist: '敵1体の防御力を下げる',
    ct: 4,
    target: 'enemyOne',
    effects: [{ kind: 'buff', stat: 'def', power: '中', sign: -1, turns: 3 }],
  },
  {
    id: 'spd-up',
    name: 'スピードUP',
    gist: '自分のスピードを上げる',
    ct: 4,
    target: 'self',
    effects: [{ kind: 'buff', stat: 'spd', power: '中', sign: 1, turns: 3 }],
  },
  {
    id: 'spd-down',
    name: 'スピードDOWN',
    gist: '敵1体のスピードを下げる',
    ct: 4,
    target: 'enemyOne',
    effects: [{ kind: 'buff', stat: 'spd', power: '中', sign: -1, turns: 3 }],
  },

  // ── HP系 ──────────────────────────────
  {
    id: 'poison',
    name: '毒',
    gist: '敵1体が行動するたびに削れる',
    ct: 5,
    target: 'enemyOne',
    effects: [{ kind: 'poison', power: '中', turns: 4 }],
  },
  {
    id: 'regen',
    name: 'リジェネ',
    gist: '味方1体が行動するたびに回復する',
    ct: 5,
    target: 'allyLowest',
    effects: [{ kind: 'regen', power: '中', turns: 4 }],
  },
  {
    id: 'heal-ratio',
    name: 'HP割合回復',
    gist: '味方1体の HP を最大値の割合ぶん回復',
    ct: 4,
    target: 'allyLowest',
    effects: [{ kind: 'healRatio', power: '中' }],
  },
  {
    id: 'shield',
    name: 'シールド',
    gist: '味方1体に、HP より先に減る盾を張る',
    ct: 4,
    target: 'allyLowest',
    effects: [{ kind: 'shield', power: '中' }],
  },

  // ── 行動系 ────────────────────────────
  {
    id: 'stun',
    name: 'スタン',
    gist: '敵1体の手番を飛ばす',
    ct: 6,
    target: 'enemyOne',
    effects: [{ kind: 'stun', turns: 1 }],
  },
  {
    id: 'ct-short',
    name: 'CT短縮',
    gist: '自分の技の待ちを縮める',
    ct: 4,
    target: 'self',
    effects: [{ kind: 'ct', delta: -2 }],
  },
  {
    id: 'ct-long',
    name: 'CT延長',
    gist: '敵1体の技の待ちを延ばす',
    ct: 5,
    target: 'enemyOne',
    effects: [{ kind: 'ct', delta: 2 }],
  },
  {
    id: 'taunt',
    name: '挑発',
    gist: '味方への攻撃を自分が引き受ける',
    ct: 3,
    target: 'self',
    effects: [{ kind: 'taunt', hits: 3 }],
  },

  // ── 特殊 ──────────────────────────────
  {
    id: 'guts',
    name: 'ガッツ',
    gist: '致命傷を HP1 で耐える',
    ct: 6,
    target: 'self',
    effects: [{ kind: 'guts', turns: 3 }],
  },
  {
    id: 'immune',
    name: '免疫',
    gist: 'DOWN・毒・スタンを受けなくなる',
    ct: 5,
    target: 'self',
    effects: [{ kind: 'immune', turns: 3 }],
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
 *
 *  ⚠️ 枠1（種族固定）と同じ技は `gachaPoolOf` が外す。 */
export const GACHA_POOLS: Readonly<Record<string, readonly SkillId[]>> = {
  // 鱗・守りの系統
  tamaru: ['def-up', 'taunt', 'shield', 'heal-ratio', 'guts', 'attack', 'ct-long'],
  // 牙・攻めの系統
  tsunoga: ['atk-up', 'def-down', 'attack-heavy', 'ct-short', 'poison', 'attack-def', 'stun'],
  // 羽・撹乱の系統
  haneru: ['spd-up', 'spd-down', 'atk-down', 'stun', 'regen', 'ct-long', 'immune'],
  // ヌシ。⚠️ 卵は落とさないが、表に無いと数える検査が落ちる
  nushi: ['def-up', 'spd-down', 'taunt', 'guts', 'immune', 'attack-all-heavy'],
}

/** その種族の卵から出うる技。⚠️ 表に無い種族は黙って空にせず投げる。 */
export function gachaPoolOf(speciesId: string, skill1: SkillId): readonly SkillId[] {
  const pool = GACHA_POOLS[speciesId]
  if (!pool) throw new Error(`卵ガチャの表に ${speciesId} が無い`)
  return pool.filter((id) => id !== skill1)
}
