/** スキル。枠は3つ。
 *
 *  | 枠 | 決まり方 |
 *  |---|---|
 *  | 1 | **種族固定**（配合では種族と連動して 50% でどちらかの親から） |
 *  | 2・3 | 卵ガチャ または 遺伝（配合では両親の4枠から2つ抽選） |
 *
 *  ⭐ 枠1が種族固定なので、種族の意味が構造的に残る。
 *  「この種族のスキル1が欲しい」が卵強奪の動機を保ち続ける。
 *
 *  🚧 **今は名前だけ。** CT の値と効果は段B で決める。
 *  CT を今ここに置くと**根拠の無い数値**になる（自動対戦シミュレータに当てて初めて決まる）。
 *  効果も、少数のプリミティブの組み合わせをデータで表す形にする（段B）。
 */

export type SkillId = string

export interface Skill {
  readonly id: SkillId
  readonly name: string
  /** 何をするスキルなのかの短い説明。数値は入れない（まだ決まっていないので） */
  readonly gist: string
}

const LIST: readonly Skill[] = [
  { id: 'strike', name: '強撃', gist: '単体に大ダメージ' },
  { id: 'haste', name: '迅速', gist: '自身の速度を上げる' },
  { id: 'slow', name: '鈍足', gist: '敵の速度を下げる' },
  { id: 'guard', name: '守勢', gist: '自身の防御を上げる' },
  { id: 'mend', name: '手当', gist: '味方1体を回復' },
  { id: 'shellbash', name: '殻打ち', gist: '防御が高いほど強い一撃' },
]

export const SKILLS: ReadonlyMap<SkillId, Skill> = new Map(LIST.map((s) => [s.id, s]))

/** 知らない id を黙って握りつぶさない。表に無いものは「効かないだけ」で気づけないため。 */
export function skillById(id: SkillId): Skill {
  const skill = SKILLS.get(id)
  if (!skill) throw new Error(`スキル表に ${id} が無い`)
  return skill
}

/** 卵ガチャ（枠2・3）で出うるスキル。🚧 抽選範囲を種族ごとに分けるかは段C で決める。 */
export const GACHA_POOL: readonly SkillId[] = LIST.map((s) => s.id)
