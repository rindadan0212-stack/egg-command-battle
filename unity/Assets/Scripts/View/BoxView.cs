using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>保管庫。⭐ 並びは Assets/Resources/Prefabs/BoxScreen.prefab が持つ。</summary>
    public sealed class BoxView : MonoBehaviour
    {
        [SerializeField] private GameObject _detail;
        [SerializeField] private Image _art;
        [SerializeField] private Image _element;
        [SerializeField] private Text _name;
        [SerializeField] private Text _id;
        [SerializeField] private Text _point;
        [SerializeField] private Text _level;
        [SerializeField] private Text _slant;
        /// <summary>特性。⭐ **名前だけでは何も伝わらない**ので働きも並べる。
        /// ⚠️ 無ければ空にする（「特性なし」と書かない ── 無いことは書かなくても分かる）。</summary>
        [SerializeField] private Text _trait;
        [SerializeField] private StatRow[] _stats;      // 4本。HP/ATK/DEF/SPD の順
        [SerializeField] private Text[] _skills;        // 3枠
        [SerializeField] private Text[] _skillCts;
        [SerializeField] private Button _party;
        [SerializeField] private Text _partyLabel;
        [SerializeField] private Button _release;
        [SerializeField] private Button[] _spend;       // 4本。ステと同じ順
        [SerializeField] private Button[] _sortTabs;    // 7枚
        [SerializeField] private RectTransform _grid;
        [SerializeField] private CreatureCell _cell;

        /// <summary><paramref name="food"/> は餌に選んである個体（無ければ null）。
        /// ⭐ 一覧を押すのは「見る」だけ。餌にするかどうかは詳細の札で決める
        /// （押すたびに意味が変わると、何が起きるか分からない画面になる）。</summary>
        /// <param name="onTrain">技を鍛える札を押した。⭐ 卵を素材にする画面を開く。</param>
        public void Bind(Game game, Creature creature, SortKey sort, IReadOnlyList<Creature> sorted,
            Action<SortKey> onSort, Action<string> onPick, Action onParty, Action onRelease,
            Creature food, Action onMarkFood, Action onFeed, Action onGrow, Action onTrain)
        {
            bool has = creature != null;
            if (_detail != null) _detail.SetActive(has);

            for (int i = 0; i < _sortTabs.Length && i < Storages.SortKeys.Length; i++)
            {
                var key = Storages.SortKeys[i];
                var tab = _sortTabs[i];
                if (tab == null) continue;
                var label = tab.GetComponentInChildren<Text>();
                if (label != null) label.text = Storages.LabelOf(key);
                // ⚠️ 色を掛けず絵を差し替える（掛けると押せない札と見分けが付かない）
                var plate = tab.GetComponent<Image>();
                if (plate != null) plate.sprite = Ui.SkinSprite(sort == key ? "button-lead" : "button");
                tab.onClick.RemoveAllListeners();
                tab.onClick.AddListener(() => onSort(key));
            }

            // ⭐ 選んでいる個体と、食わせる相手の両方に印を付ける
            CellGrid.Fill(_grid, _cell, sorted,
                id => (creature != null && id == creature.Id) || (food != null && id == food.Id),
                onPick);

            if (!has) return;

            var species = Creatures.SpeciesOf(creature);
            if (_art != null)
            {
                _art.sprite = PixelSpriteTexture.ToSprite(species.Sprite, Creatures.PaletteOf(creature));
                _art.preserveAspect = true;
            }
            if (_element != null) _element.color = ElementMark.ColorOf(creature.Element);
            if (_name != null) _name.text = species.Name;
            if (_id != null) _id.text = creature.Id;

            // ⚠️ Lv を主役にしない。ARK と同じで、同じ Lv でも中身はまるで別物。
            //    見るべきは下の4本。ここは添え物として小さく置く
            if (_level != null) _level.text = $"Lv {Levels.Of(creature)} / {Levels.MaxOf(creature)}";
            if (_slant != null)
            {
                _slant.text = creature.Strong == null || creature.Weak == null
                    ? ""
                    : $"▲{Stats.LabelOf(creature.Strong.Value)}  ▼{Stats.LabelOf(creature.Weak.Value)}";
            }
            // ⭐ 世代と変異。⚠️ 並べ替えの札には在るのに、詳細のどこにも数が無かった。
            //    ⭐ 変異は「これ以上増えない」ことが判断に効くので上限も併記する
            if (_point != null)
            {
                _point.gameObject.SetActive(true);
                _point.text = $"{creature.Generation}代  変異{creature.MutationCounter}";
                _point.color = creature.MutationCounter > 0 ? Ui.Accent : Ui.InkDim;
            }

            if (_trait != null)
            {
                var trait = Creatures.TraitOf(creature);
                _trait.text = trait == null ? "" : $"{trait.Name} — {trait.Gist}";
            }

            // ⭐ ステ振りの4枠を「餌にする」「合成」の2枠に置き換える。
            //    Prefab の位置はそのまま使えるので、器を作り直さない
            bool isFood = food != null && food.Id == creature.Id;
            bool canFeed = food != null && !isFood && !Levels.IsMaxed(creature);
            Repurpose(0, isFood ? "餌を外す" : "餌にする", true, isFood, onMarkFood);
            // ⚠️ 「個体を選んでください」と書かない。⭐ 伸びる数だけ出す
            Repurpose(1, canFeed ? $"合成 ＋{Levels.FeedValueOf(food)}" : "合成", canFeed, false, onFeed);

            // ⭐ 放置で溜めた素材で育てる。1回で1レベル
            bool canGrow = !Levels.IsMaxed(creature)
                && game.Idle.Materials >= Core.Idle.MaterialPerLevel;
            // ⚠️ 「素材が足りません」と書かない。⭐ 要る数を出せば足りる
            Repurpose(2, $"そだてる ●{Core.Idle.MaterialPerLevel}", canGrow, canGrow, onGrow);

            // ⭐ 卵の「孵さない使い道」への入口。⚠️ 棚に卵が1個も無いなら押させない
            //    （押しても何も選べない画面が開くだけになる）
            bool canTrain = game.Eggs.Count > 0 && HasRoom(creature);
            Repurpose(3, "技を鍛える", canTrain, false, onTrain);

            for (int i = 4; i < _spend.Length; i++)
            {
                if (_spend[i] != null) _spend[i].gameObject.SetActive(false);
            }

            var stats = Creatures.StatsOf(creature);
            for (int i = 0; i < _stats.Length && i < Stats.Keys.Length; i++)
            {
                var key = Stats.Keys[i];
                var row = _stats[i];
                if (row == null) continue;
                if (row.Label != null) row.Label.text = Stats.LabelOf(key);
                if (row.Value != null)
                {
                    // ⭐ この画面の主役。素質＋育てた分の内訳を出す
                    int trained = creature.Trained[key];
                    row.Value.text = trained > 0
                        ? $"{stats[key]}  ({creature.Wild[key]}+{trained})"
                        : $"{stats[key]}  ({creature.Wild[key]})";
                    row.Value.color = key == creature.Strong ? Ui.Good
                        : key == creature.Weak ? Ui.Danger : Ui.Ink;
                }
                // ⚠️ 60 で割っていたので、上限(40)まで育てても 67% までしか伸びなかった。
                //    ⭐ 目盛りは**その個体の上限**（変異で伸びる）。満タンが満タンに見えること
                if (row.Bar != null)
                {
                    float cap = Stats.WildStatMaxFor(creature.MutationCounter);
                    row.Bar.fillAmount = Mathf.Clamp01(creature.Wild[key] / Mathf.Max(1f, cap));
                }
            }

            var skills = Creatures.SkillsOf(creature);
            for (int i = 0; i < _skills.Length; i++)
            {
                var skill = i < skills.Length ? skills[i] : null;
                if (_skills[i] != null)
                {
                    _skills[i].text = skill == null ? "—" : skill.Name;
                    _skills[i].color = skill == null ? Ui.InkFaint : Ui.Ink;
                }
                if (i < _skillCts.Length && _skillCts[i] != null)
                {
                    // ⭐ **レベルは常に出す。**出さないと「鍛えられる」ことに気づけない。
                    // ⚠️ CT は枠1 だけ 0 固定なので出さない（そこは通常攻撃）
                    if (skill == null) _skillCts[i].text = "";
                    else
                    {
                        var boost = Creatures.SkillBoostOf(creature, i);
                        int ct = Skills.EffectiveCt(i, skill, boost);
                        int level = Creatures.SkillLevelOf(creature, i);
                        _skillCts[i].text = i == 0 ? $"Lv{level}" : $"Lv{level} CT{ct}";
                    }
                    _skillCts[i].color = skill != null && Creatures.SkillLevelOf(creature, i) > 1
                        ? Ui.Accent : Ui.InkDim;
                }
            }

            bool inParty = Games.IsInParty(game, creature.Id);
            if (_partyLabel != null) _partyLabel.text = inParty ? "出撃中" : "出撃";
            if (_party != null)
            {
                var plate = _party.GetComponent<Image>();
                if (plate != null) plate.sprite = Ui.SkinSprite(inParty ? "button-lead" : "button");
                _party.onClick.RemoveAllListeners();
                _party.onClick.AddListener(() => onParty());
            }
            if (_release != null)
            {
                _release.onClick.RemoveAllListeners();
                _release.onClick.AddListener(() => onRelease());
            }

        }

        /// <summary>まだ鍛えられる枠が1つでもあるか。⚠️ 全部上限なら入口を閉じる。</summary>
        private static bool HasRoom(Creature creature)
        {
            var skills = Creatures.SkillsOf(creature);
            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i] != null && !SkillCosts.IsMaxed(creature.SkillPoints[i])) return true;
            }
            return false;
        }

        /// <summary>ステ振りだった枠を別の押しどころに使い回す。
        /// ⚠️ ステ振りは外した — 上限も対価も無い ＋1 は選択になっていなかった。
        /// 育てた分は得意の方向へ自動で乗る（<see cref="Creatures.Grow"/>）。</summary>
        private void Repurpose(int index, string labelText, bool usable, bool lead, Action onTap)
        {
            if (index >= _spend.Length || _spend[index] == null) return;
            var button = _spend[index];
            button.gameObject.SetActive(true);
            var text = button.GetComponentInChildren<Text>();
            if (text != null) text.text = labelText;
            var plate = button.GetComponent<Image>();
            // ⚠️ 色を掛けず絵を差し替える（掛けると押せない札と見分けが付かない）
            // ⚠️ **押せない札は灰（button-off）。**ここで button のままにしていたので、
            //    餌を選んでいないのに「合成」が押せるように見えていた（Ui.Tappable と食い違い）
            if (plate != null)
                plate.sprite = Ui.SkinSprite(!usable ? "button-off" : lead ? "button-lead" : "button");
            button.interactable = usable;
            var label = button.transform.Find("Label");
            if (label != null)
            {
                var ink = label.GetComponent<Text>();
                if (ink != null) ink.color = usable ? Ui.Ink : Ui.InkFaint;
            }
            button.onClick.RemoveAllListeners();
            if (usable && onTap != null) button.onClick.AddListener(() => onTap());
        }
    }
}
