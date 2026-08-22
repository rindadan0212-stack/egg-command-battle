#nullable enable

namespace EggCommand.Core
{
    /// <summary>演出の拍。⭐ **1手を3拍に割るための数**。
    ///
    /// ⚠️ **遊びの数ではありません。**誰が先に動くかも、何手で決着するかも、
    /// ここを変えても**1つも変わりません**（刻みの数は同じで、1秒に進める数だけが変わる）。
    ///
    /// ⭐ **ここが唯一の出所。**⚠️ Unity 版（`BattleDriver`）と web 版（`Deeds`）が
    /// 別々に持っていると、同じ戦いが2つの速さで動くことになる。
    ///
    /// 1手の割り方:
    /// <list type="number">
    /// <item>名乗り — 打つ者の頭上に技名。足元に輪。体が前へ出る</item>
    /// <item>着弾 — ここで初めて <see cref="Battle.PerformAction"/> を呼ぶ。
    ///   数字が飛び、当たった体が跳ね、帯が減る</item>
    /// <item>間 — 次の手までひと呼吸</item>
    /// </list>
    /// ⚠️ 状態が変わるのは 2 の一度だけ。拍ごとに触ると出所が2つになる。</summary>
    public static class Beats
    {
        /// <summary>ゲージが満ちてから名乗るまでの溜め（秒）。
        /// ⭐ 相手の番はここが無いと「満ちた瞬間に殴られた」になり、
        /// 帯が満タンになったことを目が確かめる前に次が始まる。
        /// ⚠️ 自分の番には要らない（札が出て考える時間がそのまま溜めになる）。</summary>
        public const double Ready = 0.40;

        /// <summary>名乗りを読ませる時間（秒）。⭐ 技名が読める長さが下限。</summary>
        public const double Announce = 0.72;

        /// <summary>着弾のあとの間（秒）。⭐ 数字が飛び切るまで次を始めない。</summary>
        public const double Settle = 0.72;

        /// <summary>1秒に進める刻み。⭐ 速い者が先に満ちる様子が目で追える速さ。
        /// ⚠️ 上げすぎると結局パッと切り替わり、下げすぎると待たされる。
        /// ⚠️ **2026-08-22 に 14 → 10.5（0.75倍）**（作者の指示「ゲージが溜まる
        /// スピードをゆっくりに」）。</summary>
        public const double TicksPerSecond = 10.5;

        /// <summary>同じ体に2つ以上出るとき、1つ出すごとに上へ積む間隔。
        /// ⭐ 字の高さより広く取る（縁が触れると読みにくい）。
        /// ⚠️ 「数を減らす」方向では直さない ── 起きたことを隠すことになる。</summary>
        public const double StackStep = 46;
    }
}
