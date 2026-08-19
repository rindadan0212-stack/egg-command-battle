using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using EggCommand.Core;
using EggCommand.View;

namespace EggCommand.EditorTools
{
    /// <summary>1体を1枚で見せる札を組み立てる。⭐ **BOX と 配合 が同じここを呼ぶ。**
    ///
    /// ⚠️ 画面ごとに寸法を書くと、片方だけ直して食い違う（実際そうなっていた ──
    /// BOX には特性の働きが出るのに、配合には名前しか出ていなかった）。
    /// 違うのは**寸法だけ**なので、寸法を <see cref="Shape"/> に出して並びは1つにしてある。
    ///
    /// 並び（作者のラフ図 2026-08-18 に寄せた）:
    /// <code>
    ///   (属)          タマル              ← 名前が主役
    ///     絵          Lv 55/55
    ///                 c001 1代 変異0
    ///                 ─────────素質──強化─
    ///                  HP        33   +6
    ///                 ▲速度      37  +14
    ///   ────────────────────────────────
    ///    特性  食らいつき — …            ← 面
    ///          ┌ 技1 ┐                   ← 面
    ///   ┌ 技2 ┐      ┌ 技3 ┐
    /// </code>
    ///
    /// ⚠️ **ラフ図は配置の資料であって、意匠の指定ではない。**
    /// 線を引くのは「無いと読めない」ところだけ ── ⭐ **ステの表の横罫だけ**。
    /// 6行を左から右へ目で追う表なので、ここは線が要る。
    /// ⚠️ 絵・見出し・特性・技を枠で囲まない。囲うと全部が同じ重さになり、
    /// どこを見ればいいのか分からなくなる（囲った版で実際にそうなった）。
    /// ⭐ 代わりに **面（明度差）** で塊を作り、**余白**で離す。
    /// </summary>
    public static class BuildCreaturePanel
    {
        /// <summary>表の横罫。⚠️ 行を追うためだけの線なので、うんと薄く。</summary>
        private static readonly Color RowLine = new Color(0.18f, 0.15f, 0.11f, 0.14f);

        /// <summary>見出しの下だけ一段濃い。⭐ 「ここから数が始まる」を1本で言う。</summary>
        private static readonly Color HeadLine = new Color(0.18f, 0.15f, 0.11f, 0.34f);

        /// <summary>中身の塊の地。⚠️ 線を引かず、明度を一段落とすだけ。</summary>
        private static readonly Color BlockFace = new Color(0f, 0f, 0f, 0.05f);

        /// <summary>札の寸法。⚠️ ここ以外に数を書かない。</summary>
        public struct Shape
        {
            public float Width, Height, Line;
            public Vector4 Element, Art, Name, Lv, Sub, Trait, Skill1, Skill2, Skill3;
            /// <summary>表の左上と幅。高さは見出し＋行数から出す。</summary>
            public float TableX, TableTop, TableW;
            public float HeadHeight, RowHeight;
            /// <summary>ステを何組に分けて並べるか。⭐ 6ステを 3行×2組 にすると、
            /// 縦が 240 → 135 に縮み、横の空気も消える。
            /// ⚠️ 1 にすると 6行1列（幅の狭い札はこちら）。</summary>
            public int TableGroups;
            /// <summary>組と組のあいだ。⚠️ 0 にすると2組が1つの表に見える。</summary>
            public float GroupGap;
            /// <summary>表の列幅。⚠️ 強化の列は残り全部（合計が表の幅になる）。</summary>
            public float ColLabelW, ColWildW;
            public float CellPad, TraitTagW, TraitTagH, Pad;
            public int NameFont, LvFont, SubFont, HeadFont, LabelFont, NumberFont;
            public int TraitTagFont, TraitFont, SkillFont, SkillMetaFont;
            public float SkillNameTop, SkillNameH, SkillMetaTop, SkillMetaH, SkillMetaW;
        }

        /// <summary>BOX の詳細（横 984）。</summary>
        public static Shape Wide()
        {
            return new Shape
            {
                // ⚠️ 高さは中身から決めた（絵 42+380=422 → 特性 446〜502 → 技2段 526〜682 → 706）。
                Width = 984f, Height = 706f, Line = 2f,
                Element = new Vector4(24f, 18f, 62f, 62f),
                // ⭐ **絵は正方形。**⚠️ 縦長の器に正方形の絵を入れると左右が余る。
                //    表が1列に戻って右の列が 240 の背丈を取り戻したので、
                //    絵も 330 → 380 に上げて左右の重さを釣り合わせる。
                Art = new Vector4(44f, 42f, 380f, 380f),
                Name = new Vector4(448f, 40f, 350f, 50f),
                // ⚠️ 122 では「Lv 148/154」（146 要る）が入らなかった。
                //    ⭐ 上限は 素質120＋育成20＋種族の基礎 なので3桁×2になりうる
                Lv = new Vector4(798f, 52f, 160f, 32f),
                Sub = new Vector4(448f, 96f, 510f, 28f),
                TableX = 448f, TableTop = 140f, TableW = 510f,
                HeadHeight = 30f, RowHeight = 35f,
                // ⭐ **6行1列。**⚠️ 配合の親札と同じ並びにする（作者の指示）。
                //    列の割り振りも親札と同じ比（見出し 0.44 / 素質 0.26 / 強化 0.30）。
                TableGroups = 1, GroupGap = 0f,
                ColLabelW = 224f, ColWildW = 133f, CellPad = 10f,
                // ⚠️ **帯は全幅。**右の列（510）へ入れると、一番長い特性
                //    「執念 — シールドが剥がれるたびゲージが溜まる」（472 要る）が入らない。
                // ⚠️ 帯の高さは中身ぴったり。⭐ 余った空きは塊の外（余白）に出す
                Trait = new Vector4(44f, 446f, 896f, 56f),
                TraitTagW = 72f, TraitTagH = 26f, Pad = 18f,
                // ⭐ **技は2段のピラミッド**（作者の指示 2026-08-19）。
                //    枠1は CT が無く必ず打てるので、上に大きく置いて主役にする。
                Skill1 = new Vector4(292f, 526f, 400f, 72f),
                Skill2 = new Vector4(44f, 610f, 424f, 72f),
                Skill3 = new Vector4(516f, 610f, 424f, 72f),
                NameFont = 38, LvFont = 22, SubFont = 19, HeadFont = 18,
                LabelFont = 22, NumberFont = 26,
                TraitTagFont = 19, TraitFont = 22, SkillFont = 26, SkillMetaFont = 20,
                SkillNameTop = 6f, SkillNameH = 32f,
                SkillMetaTop = 42f, SkillMetaH = 24f, SkillMetaW = 130f,
            };
        }

        /// <summary>配合の親札（横 460）。⭐ **並びは Wide と同じ。**絵は左、表は右。
        /// ⚠️ 幅が半分以下なので字は小さいが、**積み替えない**
        /// （積み替えると、同じ個体が画面によって別の札に見える）。</summary>
        public static Shape Narrow()
        {
            return new Shape
            {
                Width = 460f, Height = 524f, Line = 2f,
                Element = new Vector4(8f, 8f, 42f, 42f),
                Art = new Vector4(22f, 26f, 172f, 276f),
                Name = new Vector4(212f, 26f, 226f, 36f),
                Lv = new Vector4(212f, 62f, 226f, 24f),
                Sub = new Vector4(212f, 86f, 226f, 22f),
                TableX = 212f, TableTop = 118f, TableW = 226f,
                HeadHeight = 22f, RowHeight = 27f,
                // ⚠️ ここは 226 しか無いので**1組のまま**。
                //    2組にすると見出しの列が 55 になり、「▼弱化耐性」（80 要る）が入らない。
                TableGroups = 1, GroupGap = 0f,
                ColLabelW = 100f, ColWildW = 58f, CellPad = 6f,
                Trait = new Vector4(22f, 318f, 416f, 44f),
                TraitTagW = 46f, TraitTagH = 20f, Pad = 12f,
                Skill1 = new Vector4(130f, 380f, 200f, 50f),
                Skill2 = new Vector4(22f, 442f, 200f, 50f),
                Skill3 = new Vector4(238f, 442f, 200f, 50f),
                NameFont = 23, LvFont = 15, SubFont = 13, HeadFont = 13,
                LabelFont = 16, NumberFont = 18,
                TraitTagFont = 13, TraitFont = 15, SkillFont = 19, SkillMetaFont = 15,
                SkillNameTop = 2f, SkillNameH = 26f,
                SkillMetaTop = 28f, SkillMetaH = 18f, SkillMetaW = 84f,
            };
        }

        /// <summary>札を用意して返す。
        ///
        /// ⭐ **既にあれば作らない。**名前で部品を拾い直して繋ぐだけにする。
        /// ⚠️ 人が Unity で動かした位置を消さないため。作り直すのは
        /// <paramref name="rebuild"/> が立っているときだけ（「画面を作り直す」）。</summary>
        public static CreaturePanel Build(Transform parent, string name, Shape s, bool rebuild)
        {
            var old = Find(parent, name);
            if (old != null && !rebuild)
            {
                var kept = Rebind(old.gameObject);
                if (kept != null) return kept;
                // ⚠️ 部品が足りない札は繋ぎ直せない。作り直すしかないので落として作る
                Debug.LogWarning($"{name}: 部品が足りないので作り直した（手で置いた位置は失われる）");
            }
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var root = Add(parent, name, 0f, 0f, s.Width, s.Height);
            var panel = root.gameObject.AddComponent<CreaturePanel>();

            // ── 絵と属性 ──────────────────────────────
            // ⚠️ 枠で囲わない。⭐ 絵そのものが十分に大きいので、囲わなくても塊に見える
            var art = Add(root, "Art", s.Art.x, s.Art.y, s.Art.z, s.Art.w);
            var artImage = art.gameObject.AddComponent<Image>();
            artImage.preserveAspect = true;
            artImage.raycastTarget = false;
            // ⚠️ 属性の丸は絵より**後**に作る。先に作ると絵の下に潜って見えない
            var element = Add(root, "Element", s.Element.x, s.Element.y, s.Element.z, s.Element.w);
            var elementImage = element.gameObject.AddComponent<Image>();
            elementImage.sprite = Ui.SkinSprite("circle");
            elementImage.raycastTarget = false;

            // ── 見出し ────────────────────────────────
            // ⭐ 主役は種族名1つ。Lv と id は大きさと濃さで引っ込める（枠で分けない）
            var speciesName = Label(Add(root, "Name", s.Name.x, s.Name.y, s.Name.z, s.Name.w),
                s.NameFont, Ui.Ink, TextAnchor.MiddleLeft);
            var lv = Label(Add(root, "Lv", s.Lv.x, s.Lv.y, s.Lv.z, s.Lv.w),
                s.LvFont, Ui.InkDim, TextAnchor.MiddleRight);
            var sub = Label(Add(root, "Sub", s.Sub.x, s.Sub.y, s.Sub.z, s.Sub.w),
                s.SubFont, Ui.InkFaint, TextAnchor.MiddleLeft);

            var rows = BuildTable(root, s);

            // ── 特性 ──────────────────────────────────
            var traitBox = Face(root, "Trait Box", s.Trait);
            float traitTop = (s.Trait.w - s.TraitTagH) / 2f;
            Label(Add(traitBox, "Tag", s.Pad, traitTop, s.TraitTagW, s.TraitTagH),
                s.TraitTagFont, Ui.InkFaint, TextAnchor.MiddleLeft).text = "特性";
            var trait = Label(Add(traitBox, "Body", s.Pad + s.TraitTagW, traitTop,
                s.Trait.z - s.Pad * 2f - s.TraitTagW, s.TraitTagH),
                s.TraitFont, Ui.AccentInk, TextAnchor.MiddleLeft);

            // ── 技 ────────────────────────────────────
            var skills = new PanelSkillBox[3];
            var places = new[] { s.Skill1, s.Skill2, s.Skill3 };
            for (int i = 0; i < skills.Length; i++)
            {
                var box = Face(root, $"Skill {i}", places[i]);
                skills[i] = new PanelSkillBox
                {
                    Root = box.gameObject,
                    Name = Label(Add(box, "Name", 0f, s.SkillNameTop, places[i].z, s.SkillNameH),
                        s.SkillFont, Ui.Ink, TextAnchor.MiddleCenter),
                    Level = Label(Add(box, "Level", s.Pad, s.SkillMetaTop, s.SkillMetaW, s.SkillMetaH),
                        s.SkillMetaFont, Ui.InkDim, TextAnchor.MiddleLeft),
                    Ct = Label(Add(box, "Ct", places[i].z - s.Pad - s.SkillMetaW,
                        s.SkillMetaTop, s.SkillMetaW, s.SkillMetaH),
                        s.SkillMetaFont, Ui.InkDim, TextAnchor.MiddleRight),
                };
            }

            var so = new SerializedObject(panel);
            so.FindProperty("_art").objectReferenceValue = artImage;
            so.FindProperty("_element").objectReferenceValue = elementImage;
            so.FindProperty("_name").objectReferenceValue = speciesName;
            so.FindProperty("_title").objectReferenceValue = lv;
            so.FindProperty("_sub").objectReferenceValue = sub;
            so.FindProperty("_trait").objectReferenceValue = trait;
            var statProp = so.FindProperty("_stats");
            statProp.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                var e = statProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Label").objectReferenceValue = rows[i].Label;
                e.FindPropertyRelative("Wild").objectReferenceValue = rows[i].Wild;
                e.FindPropertyRelative("Trained").objectReferenceValue = rows[i].Trained;
            }
            var skillProp = so.FindProperty("_skills");
            skillProp.arraySize = skills.Length;
            for (int i = 0; i < skills.Length; i++)
            {
                var e = skillProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Root").objectReferenceValue = skills[i].Root;
                e.FindPropertyRelative("Name").objectReferenceValue = skills[i].Name;
                e.FindPropertyRelative("Level").objectReferenceValue = skills[i].Level;
                e.FindPropertyRelative("Ct").objectReferenceValue = skills[i].Ct;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return panel;
        }

        /// <summary>既にある札の部品を**名前で拾い直して**繋ぐ。⚠️ 位置には一切触らない。
        ///
        /// ⭐ ここが「人が飾った札を、コードが壊さずに使う」入口。
        /// ⚠️ 1つでも欠けていたら null を返す（黙って半分だけ繋がった札を作らない）。</summary>
        private static CreaturePanel Rebind(GameObject root)
        {
            var panel = root.GetComponent<CreaturePanel>() ?? root.AddComponent<CreaturePanel>();
            var so = new SerializedObject(panel);

            var art = Grab<Image>(root, "Art");
            var element = Grab<Image>(root, "Element");
            var speciesName = Grab<Text>(root, "Name");
            var lv = Grab<Text>(root, "Lv");
            var sub = Grab<Text>(root, "Sub");
            var trait = Grab<Text>(root, "Body");
            if (art == null || element == null || speciesName == null || lv == null
                || sub == null || trait == null) return null;

            so.FindProperty("_art").objectReferenceValue = art;
            so.FindProperty("_element").objectReferenceValue = element;
            so.FindProperty("_name").objectReferenceValue = speciesName;
            so.FindProperty("_title").objectReferenceValue = lv;
            so.FindProperty("_sub").objectReferenceValue = sub;
            so.FindProperty("_trait").objectReferenceValue = trait;

            var statProp = so.FindProperty("_stats");
            statProp.arraySize = Stats.Keys.Length;
            for (int i = 0; i < Stats.Keys.Length; i++)
            {
                var label = Grab<Text>(root, $"K {i}");
                var wild = Grab<Text>(root, $"W {i}");
                var trained = Grab<Text>(root, $"G {i}");
                if (label == null || wild == null || trained == null) return null;
                var e = statProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Label").objectReferenceValue = label;
                e.FindPropertyRelative("Wild").objectReferenceValue = wild;
                e.FindPropertyRelative("Trained").objectReferenceValue = trained;
            }

            var skillProp = so.FindProperty("_skills");
            skillProp.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var box = FindDeep(root.transform, $"Skill {i}");
                if (box == null) return null;
                var e = skillProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Root").objectReferenceValue = box.gameObject;
                e.FindPropertyRelative("Name").objectReferenceValue = Grab<Text>(box.gameObject, "Name");
                e.FindPropertyRelative("Level").objectReferenceValue = Grab<Text>(box.gameObject, "Level");
                e.FindPropertyRelative("Ct").objectReferenceValue = Grab<Text>(box.gameObject, "Ct");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return panel;
        }

        /// <summary>名前で部品を1つ拾う。⚠️ 見つからなければ null（呼ぶ側が気づける）。</summary>
        private static T Grab<T>(GameObject root, string name) where T : Component
        {
            var found = FindDeep(root.transform, name);
            return found == null ? null : found.GetComponent<T>();
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name && t != root) return t;
            }
            return null;
        }

        /// <summary>ステの表。⭐ **線は横だけ。**縦罫も外枠も引かない。
        ///
        /// ⚠️ 6行を左（ステの名前）から右（数）へ目で追うので、行を追う線は要る。
        /// ⭐ 列は**右揃え**で立つので、縦の線は足しても読みやすくならない（線が増えるだけ）。</summary>
        /// <summary>ステの表。⭐ **組に分けて並べる**（<see cref="Shape.TableGroups"/>）。
        ///
        /// ⚠️ 6行1列だったときは、542 幅の表に字が 208 しか無く、
        /// 見出しと数のあいだが 120 空いていた（実測）。縦も 240 使っていた。
        /// ⭐ 3行×2組にすると、横は詰まり、縦は 135 になる。
        ///
        /// ⚠️ **並びは縦読み**（左の組に HP・攻撃力・防御力、右にスピード・弱化命中・弱化耐性）。
        /// 横読みにすると、主なステと弱化まわりが1行に混ざって比べにくい。</summary>
        private static PanelStatRow[] BuildTable(Transform root, Shape s)
        {
            int groups = s.TableGroups < 1 ? 1 : s.TableGroups;
            int perGroup = Mathf.CeilToInt(Stats.Keys.Length / (float)groups);
            float groupW = (s.TableW - s.GroupGap * (groups - 1)) / groups;

            var table = Add(root, "Table", s.TableX, s.TableTop, s.TableW,
                s.HeadHeight + s.RowHeight * perGroup);

            float wildX = s.ColLabelW;
            float trainedX = s.ColLabelW + s.ColWildW;
            float trainedW = groupW - trainedX;

            var rows = new PanelStatRow[Stats.Keys.Length];
            for (int g = 0; g < groups; g++)
            {
                float left = (groupW + s.GroupGap) * g;

                Label(Add(table, $"Head Wild {g}", left + wildX, 0f,
                        s.ColWildW - s.CellPad, s.HeadHeight),
                    s.HeadFont, Ui.InkFaint, TextAnchor.LowerRight).text = "素質";
                Label(Add(table, $"Head Trained {g}", left + trainedX, 0f,
                        trainedW - s.CellPad, s.HeadHeight),
                    s.HeadFont, Ui.InkFaint, TextAnchor.LowerRight).text = "強化";
                // ⭐ 見出しの下だけ一段濃い1本。「ここから数が始まる」
                Rule(table, $"Head Line {g}", left, s.HeadHeight - s.Line,
                    groupW, s.Line, HeadLine);

                for (int r = 0; r < perGroup; r++)
                {
                    int i = g * perGroup + r;
                    if (i >= rows.Length) break;
                    float top = s.HeadHeight + s.RowHeight * r;
                    // ⚠️ 最終行の下は引かない（表を箱にしない）
                    if (r > 0) Rule(table, $"Row Line {i}", left, top, groupW, 1f, RowLine);
                    rows[i] = new PanelStatRow
                    {
                        Label = Label(Add(table, $"K {i}", left, top,
                                s.ColLabelW - s.CellPad, s.RowHeight),
                            s.LabelFont, Ui.InkDim, TextAnchor.MiddleLeft),
                        Wild = Label(Add(table, $"W {i}", left + wildX, top,
                                s.ColWildW - s.CellPad, s.RowHeight),
                            s.NumberFont, Ui.Ink, TextAnchor.MiddleRight),
                        Trained = Label(Add(table, $"G {i}", left + trainedX, top,
                                trainedW - s.CellPad, s.RowHeight),
                            s.NumberFont, Ui.AccentInk, TextAnchor.MiddleRight),
                    };
                }
            }
            return rows;
        }

        /// <summary>塊を作る面。⚠️ 線は引かない（明度を一段落とすだけ）。</summary>
        private static RectTransform Face(Transform parent, string name, Vector4 place)
        {
            var rect = Add(parent, name, place.x, place.y, place.z, place.w);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = BlockFace;
            image.raycastTarget = false;
            return rect;
        }

        private static void Rule(Transform parent, string name,
            float x, float y, float w, float h, Color color)
        {
            var rect = Add(parent, name, x, y, w, h);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static Transform Find(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
            }
            return null;
        }

        private static RectTransform Add(Transform parent, string name,
            float left, float top, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(left, -top);
            return rect;
        }

        private static Text Label(RectTransform rect, int size, Color color, TextAnchor anchor)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.text = "";
            text.font = Ui.TheFont;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
