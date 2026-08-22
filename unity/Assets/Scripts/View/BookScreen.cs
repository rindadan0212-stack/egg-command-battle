using UnityEngine;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>図鑑。⭐ **手に入れた種族と、その中身を見るところ**（2026-08-22・作者の指示）。
    ///
    /// ⭐ **ここが答える問いは1つ**: 「この種族を狙うと、何が手に入るのか」。
    /// ⚠️ 特性は種族固定・枠1も種族固定なので、**巣を選ぶ前に知りたいこと**が
    /// 種族の側に集まっている。それを読む場所が今まで無かった。
    ///
    /// ⚠️ **まだ手に入れていない種族も枠だけ出す。**⭐ 出さないと「あと何種居るのか」が
    /// 分からず、集める目標にならない。⚠️ ただし中身は伏せる（名前も技も出さない）。
    ///
    /// ⚠️ 「持っている」ではなく「**手に入れたことがある**」で載せる
    /// （<see cref="Game.SpeciesSeen"/>）── 分解して枠を空けるたびに
    /// 図鑑が減るのは、集めた記録として嘘になる。
    ///
    /// ⭐ **座標はこのファイルに1つも無い。**`Assets/Resources/Layouts/book.txt` が持つ
    /// （2026-08-22・作者の指示「すべてアセットを使用することを厳格に守れば」）。
    /// ⚠️ ここに `Ui.Place` を書き戻さないこと ── 書いた瞬間に、
    /// **エンジン抜きの検査（`LayoutAssetTests`）から外れる**。</summary>
    public static class BookScreen
    {
        public static void Build(App app, RectTransform body)
        {
            var all = SpeciesTable.All;
            int at = 0;   // ⭐ いま何番目の札を組んでいるか（`At` が知らせてくる）

            LayoutView.Build("book", body, new LayoutFill
            {
                Count = key => key == "species" ? all.Count : 0,
                At = (key, i) => at = i,

                Text = key =>
                {
                    switch (key)
                    {
                        case "count":
                            return $"手に入れた種族　{Games.SeenCount(app.Game)} / {all.Count}";
                        case "name":
                            return Known(app, all[at]) ? all[at].Name : "？？？";
                        case "trait":
                            return Traits.Has(all[at].TraitId)
                                ? Traits.ById(all[at].TraitId).Name : "—";
                        case "hide":
                            return "—";
                        default: return "";
                    }
                },

                // ⭐ **知らない種族も絵は出す。**⚠️ ただし影だけ ── 何が来るかは伏せる。
                //    出さないと「空の枠」になり、壊れて見える。
                Sprite = key => key == "art" ? all[at].Sprite : null,
                Palette = key => key == "art" ? all[at].Palettes[0] : null,

                Tint = Shade(app, () => all[at]),

                // ⚠️ **押しどころは札そのもの。**⭐ 中に釦を置くと、どこを押すのか読めない。
                //    ⚠️ 手に入れていない種族は押させない（開いても伏せ字しか無い）。
                Tap = key =>
                {
                    if (key != "species") return null;
                    var species = all[at];
                    if (!Known(app, species)) return null;
                    return () => SpeciesPanel.Show(app, species);
                },

                // ⭐ `when=known` / `when=!known` で、特性名と伏せ字が入れ替わる
                When = key => key == "known" && Known(app, all[at]),
            });
        }

        private static bool Known(App app, Species species) =>
            Games.HasSeen(app.Game, species.Id);

        /// <summary>伏せてあるものを沈める色。⚠️ 骨組みは「どの色か」を知らないので、
        /// ⭐ **データ次第で変わる色だけ**をここで差す。</summary>
        private static System.Func<string, Color?> Shade(App app, System.Func<Species> now)
        {
            return key =>
            {
                var species = now();
                bool known = Known(app, species);
                if (key == "art")
                    return known ? Color.white : new Color(0f, 0f, 0f, 0.34f);
                if (known) return null;   // ⭐ 骨組みの ink= のまま
                return Ui.InkFaint;
            };
        }
    }
}
