#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>消えた個体の墓標。⭐ 家系図を遡るためだけに残す小さな控え
    /// （作者の指示「BOXで2世代以降のキャラクターの家系図を見られるように」）。
    ///
    /// ⚠️ **個体そのものではない**（ステの全部も技も持たない）── 名前を辿れれば足りる。
    /// 配合すると両親は消える（<see cref="Games.FusePair"/>）ので、消える**直前**に
    /// <see cref="Tombs.Bury"/> がここへ積む。消したあとでは元の個体から中身が読めない。</summary>
    public sealed class Tomb
    {
        /// <summary>消えた個体の ID。⚠️ 子の <see cref="Creature.ParentA"/>/<see cref="Creature.ParentB"/>
        /// が指す先と同じ文字列。</summary>
        public readonly string Id;
        public readonly string SpeciesId;
        public readonly Element Element;
        public readonly int Generation;
        /// <summary>⭐ さらに親をたどる糸。⚠️ その親も居なければ「不明」で止まる
        /// （<see cref="Lineage.Of"/> 参照）。</summary>
        public readonly string? ParentA;
        public readonly string? ParentB;
        /// <summary>⭐ 「どれくらい良い個体だったか」が1つの数で分かる（素質の合計）。</summary>
        public readonly int WildTotal;

        public Tomb(string id, string speciesId, Element element, int generation,
            string? parentA, string? parentB, int wildTotal)
        {
            Id = id;
            SpeciesId = speciesId;
            Element = element;
            Generation = generation;
            ParentA = parentA;
            ParentB = parentB;
            WildTotal = wildTotal;
        }

        /// <summary>消える個体から、その場で控えを作る。</summary>
        public static Tomb Of(Creature c) => new Tomb(
            c.Id, c.SpeciesId, c.Element, c.Generation, c.ParentA, c.ParentB,
            Creatures.WildTotalOf(c));
    }

    /// <summary><see cref="Game.Tombs"/> を積む・切り詰める。</summary>
    public static class Tombs
    {
        /// <summary>⭐ 上限。⚠️ **際限なく増えないように置く**（作者の指示）。
        ///
        /// 目安200件 ── 家系図が実際に遡るのは3代（自分・親2・祖父母4＝先祖は最大6件）
        /// なので、配合を重ねても直近の血統ぶんは十分残る。1件は数十バイトなので、
        /// 200件でも保存の重さには効かない（増え続けるのを止めるのが目的で、
        /// 遡れる代数そのものは <see cref="Lineage.Of"/> の <c>depth</c> が決める・
        /// こちらとは別の役目）。</summary>
        public const int Limit = 200;

        /// <summary>墓標を積む。⚠️ **消す前に呼ぶこと**（<see cref="Games.FusePair"/> 参照 ──
        /// 消したあとでは中身が読めない）。
        /// ⭐ 上限を超えたら、古いものから捨てる（末尾へ積むので、先頭が最古）。
        /// ⚠️ 捨てた先祖は、以後 <see cref="Lineage.Of"/> から「不明」と出る。</summary>
        public static void Bury(Game game, Creature creature)
        {
            game.Tombs.Add(Tomb.Of(creature));
            while (game.Tombs.Count > Limit) game.Tombs.RemoveAt(0);
        }
    }

    /// <summary>ある個体の先祖をたどる。⭐ 保管庫に居る個体 → 墓標 → その親…と辿る。
    ///
    /// ⚠️ 墓標が無い（保管庫にも居ない）先祖は「不明」（<see cref="Node.Known"/> が false）
    /// で埋める ── **黙って木を切らない**。分かる枝は、他の枝が不明でも構わず先まで出す。</summary>
    public static class Lineage
    {
        /// <summary>家系図の札1枚ぶん。⚠️ <see cref="Known"/> が false のときは「不明」
        /// ── 他の欄は既定値（<c>null</c>/<c>0</c>）のままで、読んではいけない。</summary>
        public readonly struct Node
        {
            public readonly string? Id;
            public readonly string? SpeciesId;
            public readonly Element? Element;
            public readonly int Generation;
            public readonly int WildTotal;
            public readonly bool Known;

            public Node(string? id, string? speciesId, Element? element,
                int generation, int wildTotal, bool known)
            {
                Id = id;
                SpeciesId = speciesId;
                Element = element;
                Generation = generation;
                WildTotal = wildTotal;
                Known = known;
            }
        }

        /// <summary>ある個体の先祖をたどる。
        ///
        /// ⭐ **並びは決め打ちの二分木**（自分=0／親=1,2／祖父母=3,4,5,6…・添字 <c>i</c> の親は
        /// <c>i*2+1</c>・<c>i*2+2</c>）。⚠️ 欠けている所は <see cref="Node.Known"/> が false の
        /// 空札のまま返す（詰めない）── 画面がこの並びだけで場所を決められるように。
        ///
        /// ⚠️ **輪を踏まない**（壊れた保存で親が自分を指す等）。⭐ 見た ID を <c>seen</c> に
        /// 覚えておき、二度目に出てきたら「不明」で止める（無限に辿らない）。
        ///
        /// ⚠️ **保管庫を先に見る。**まだ配合していない親は保管庫に生きて居るので、
        /// 墓標より先にそちらを見る（墓標は「消えた個体」の控えでしかない）。</summary>
        /// <param name="depth">何代さかのぼるか（⭐ 2 で親と祖父母）。</param>
        public static Node[] Of(Game game, Creature who, int depth)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (who == null) throw new ArgumentNullException(nameof(who));
            if (depth < 0) depth = 0;

            var result = new Node[(1 << (depth + 1)) - 1];
            var seen = new HashSet<string>();
            seen.Add(who.Id);
            result[0] = new Node(who.Id, who.SpeciesId, who.Element, who.Generation,
                Creatures.WildTotalOf(who), known: true);

            if (depth > 0)
            {
                Fill(game, result, 1, 1, who.ParentA, seen, depth);
                Fill(game, result, 2, 1, who.ParentB, seen, depth);
            }
            return result;
        }

        /// <summary>⚠️ 何もしなければ <c>result[index]</c> は既定値のまま
        /// （<c>Known=false</c> ＝「不明」）── <c>new Node[n]</c> の初期値がそのまま
        /// 「不明」の意味を持つので、明示的に書く分岐は無い。</summary>
        private static void Fill(Game game, Node[] result, int index, int row,
            string? id, HashSet<string> seen, int depth)
        {
            // ⚠️ 居ない（null）か、既に見た ID（輪）なら、この先は辿らない
            if (id == null || !seen.Add(id)) return;

            var live = LiveOf(game, id);
            if (live != null)
            {
                result[index] = new Node(live.Id, live.SpeciesId, live.Element,
                    live.Generation, Creatures.WildTotalOf(live), known: true);
                if (row < depth)
                {
                    Fill(game, result, index * 2 + 1, row + 1, live.ParentA, seen, depth);
                    Fill(game, result, index * 2 + 2, row + 1, live.ParentB, seen, depth);
                }
                return;
            }

            var tomb = TombOf(game, id);
            if (tomb == null) return;   // ⚠️ 墓標も無い ── ここで「不明」のまま止まる
            result[index] = new Node(tomb.Id, tomb.SpeciesId, tomb.Element,
                tomb.Generation, tomb.WildTotal, known: true);
            if (row < depth)
            {
                Fill(game, result, index * 2 + 1, row + 1, tomb.ParentA, seen, depth);
                Fill(game, result, index * 2 + 2, row + 1, tomb.ParentB, seen, depth);
            }
        }

        private static Creature? LiveOf(Game game, string id)
        {
            foreach (var c in game.Storage.Creatures) if (c.Id == id) return c;
            return null;
        }

        /// <summary>⚠️ 新しい方から探す（同じ ID の墓標が重なることは無い想定だが、
        /// 念のため後入れを優先する）。</summary>
        private static Tomb? TombOf(Game game, string id)
        {
            for (int i = game.Tombs.Count - 1; i >= 0; i--)
                if (game.Tombs[i].Id == id) return game.Tombs[i];
            return null;
        }
    }
}
