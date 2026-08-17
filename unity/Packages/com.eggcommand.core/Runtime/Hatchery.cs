#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>孵化器に入っている1個。</summary>
    public sealed class Incubation
    {
        public readonly Egg Egg;
        public readonly long StartUnix;
        /// <summary>⚠️ 「あと何秒」ではなく「いつ終わるか」を持つ。
        /// 残り秒で持つと、画面を見ていない間の時間が進まない。</summary>
        public long ReadyUnix;

        /// <summary>どの枠に入れたか。⭐ **入れた場所に留まる**。
        /// ⚠️ 順番に詰めると、取り出すたびに残りが左上へ動く。
        /// どこに置いたか覚えていられないし、空けておくこともできない。</summary>
        public readonly int Slot;

        public Incubation(Egg egg, long startUnix, long readyUnix, int slot = 0)
        {
            Egg = egg;
            StartUnix = startUnix;
            ReadyUnix = readyUnix;
            Slot = slot;
        }
    }

    /// <summary>孵化器。⭐ 実時間で孵る。枠は <see cref="Slots"/> 個まで。
    ///
    /// ⭐ 枠が有限なので「どの卵を先に入れるか」が選択になる。
    /// ★5を1枠に寝かせるあいだ、★1を何度も回すか。ここが待ち時間の遊び。
    ///
    /// ⚠️ 時刻は引数で受け取る。Core はエンジンにも OS にも触らない
    /// （較正済みの検査が時計に依存すると、走らせるたびに結果が変わる）。
    /// </summary>
    public static class Hatchery
    {
        public const int Slots = 5;

        public static long Now(DateTime utc) =>
            (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        public static bool HasRoom(Game game) => game.Incubating.Count < Slots;

        /// <summary>その枠に入っているもの。⚠️ 空なら null。</summary>
        public static Incubation? At(Game game, int slot)
        {
            foreach (var one in game.Incubating)
            {
                if (one.Slot == slot) return one;
            }
            return null;
        }

        /// <summary>いちばん手前の空き枠。⚠️ 無ければ -1。</summary>
        public static int FreeSlot(Game game)
        {
            for (int i = 0; i < Slots; i++)
            {
                if (At(game, i) == null) return i;
            }
            return -1;
        }

        /// <summary>孵化器へ入れる。⚠️ 枠が無ければ入れない（黙って捨てない）。
        /// <paramref name="speed"/> は所要時間の割る数。テストで即孵らせるために使う。</summary>
        /// <summary><paramref name="slot"/> が負なら手前の空き枠へ。</summary>
        public static Incubation Begin(Game game, string eggId, long nowUnix, int speed = 1, int slot = -1)
        {
            if (slot < 0) slot = FreeSlot(game);
            if (slot < 0 || slot >= Slots)
                throw new InvalidOperationException($"孵化器が満杯（{Slots}枠）。先にどれかを取り出す");
            if (At(game, slot) != null)
                throw new InvalidOperationException($"{slot} 番の枠は埋まっている");

            int index = -1;
            for (int i = 0; i < game.Eggs.Count; i++)
            {
                if (game.Eggs[i].Id == eggId) { index = i; break; }
            }
            if (index < 0) throw new ArgumentException($"{eggId} という卵は棚に無い");

            var egg = game.Eggs[index];
            game.Eggs.RemoveAt(index);

            int seconds = Rarities.SecondsOf(egg.Rarity);
            if (speed > 1) seconds = seconds / speed;
            if (seconds < 1) seconds = 1;   // ⚠️ 0 にしない。入れた瞬間に孵ると演出が出ない

            var started = new Incubation(egg, nowUnix, nowUnix + seconds, slot);
            game.Incubating.Add(started);
            return started;
        }

        public static bool IsReady(Incubation slot, long nowUnix) => nowUnix >= slot.ReadyUnix;

        public static int LeftOf(Incubation slot, long nowUnix)
        {
            long left = slot.ReadyUnix - nowUnix;
            return left < 0 ? 0 : (int)left;
        }

        /// <summary>0（入れたて）〜1（孵る）。</summary>
        public static double ProgressOf(Incubation slot, long nowUnix)
        {
            long span = slot.ReadyUnix - slot.StartUnix;
            if (span <= 0) return 1.0;
            double done = (nowUnix - slot.StartUnix) / (double)span;
            return done < 0.0 ? 0.0 : done > 1.0 ? 1.0 : done;
        }

        /// <summary>いま取り出せるものを取り出して孵す。
        /// ⚠️ まだなら何もしない（呼び側が「押せない」を出す）。</summary>
        public static Creature? Collect(Game game, string eggId, long nowUnix)
        {
            for (int i = 0; i < game.Incubating.Count; i++)
            {
                var slot = game.Incubating[i];
                if (slot.Egg.Id != eggId) continue;
                if (!IsReady(slot, nowUnix)) return null;
                if (Storages.IsFull(game.Storage))
                    throw new InvalidOperationException($"保管庫が満杯（{game.Storage.Slots}枠）。先にどれかを逃がす");

                game.Incubating.RemoveAt(i);
                string id = $"c{(++game.Serial).ToString().PadLeft(3, '0')}";
                // ⚠️ 得意・不得意と特性は別の系統で引く。hatch の系統に混ぜると
                //    技のガチャの列がずれて、較正済みの検査が無効になる
                StatKey strong, weak;
                Nests.RollSlant(game.RngSlant, out strong, out weak);
                // ⭐ 巣の卵＝**新しい特性を入れる唯一の入口**。配合の卵は既に持っている
                // ⚠️ 特性は★の低い卵には付かない（序盤に読むものを増やさない）
                var creature = Nests.Hatch(game.RngHatch, slot.Egg, id, strong, weak,
                    Traits.RollFor(game.RngTrait, slot.Egg.Rarity));
                game.Storage = Storages.Accept(game.Storage, creature);
                return creature;
            }
            return null;
        }

        /// <summary>いますぐ孵る状態にする。⭐ テスト用の抜け道。
        /// ⚠️ 遊びの中の短縮手段ではない（そのつもりなら対価を設計してから足す）。</summary>
        public static void Rush(Incubation slot, long nowUnix) => slot.ReadyUnix = nowUnix;

        /// <summary>取り出さずに戻す。⚠️ 経過は捨てる（戻して入れ直す抜け道を作らない）。</summary>
        public static void Cancel(Game game, string eggId)
        {
            for (int i = 0; i < game.Incubating.Count; i++)
            {
                if (game.Incubating[i].Egg.Id != eggId) continue;
                var slot = game.Incubating[i];
                game.Incubating.RemoveAt(i);
                game.Eggs.Add(slot.Egg);
                return;
            }
        }

        public static List<Incubation> ReadyOf(Game game, long nowUnix)
        {
            var ready = new List<Incubation>();
            foreach (var slot in game.Incubating)
            {
                if (IsReady(slot, nowUnix)) ready.Add(slot);
            }
            return ready;
        }
    }
}
