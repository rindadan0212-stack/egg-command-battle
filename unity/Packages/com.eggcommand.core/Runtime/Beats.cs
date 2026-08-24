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

        // ── 告知 ────────────────────────────────────────

        /// <summary>横から伸びるまで（秒）。⭐ 動いて止まると、そこを読む。</summary>
        public const double SlideIn = 0.22;

        /// <summary>告知を出したままにする時間（秒）。
        /// ⚠️ ボタンを置かない ── 「親に見つかった」は選択ではなく**結果**なので、
        /// 押させると「押したから戦闘になった」に見えてしまう。</summary>
        public const double BannerHold = 0.95;

        // ── さいころ ────────────────────────────────────

        /// <summary>回している時間（秒）。⭐ 短く。⚠️ 長いと、振る回数ぶん待たされる。</summary>
        public const double Spin = 0.42;

        /// <summary>出目を出したまま止めておく時間（秒）。
        /// ⭐ **目を読み切るための間。**⚠️ 短いと「何が出たか分からないまま次へ行く」
        /// （2026-08-20・作者の指示）。</summary>
        public const double DiceHold = 0.95;

        /// <summary>目が切り替わる間隔（秒）。⚠️ 乱数を引かない ── 順に回すだけ。</summary>
        public const double Flick = 0.055;

        // ── 駒 ──────────────────────────────────────────

        /// <summary>1マスぶんの間（秒）。⭐ **短く。**⚠️ 長いと、出目のぶんだけ待たされる。
        ///
        /// ⭐ **これがある理由**（2026-08-20・作者の指示）: 振った瞬間に駒が飛んでいたので、
        /// **何マス進んだのかが目で追えなかった**（出目と進みが合っていないように見える）。</summary>
        public const double WalkStep = 0.13;

        /// <summary>関門を踏んだあと、余分に置く間（秒）。
        /// ⭐ **重い所で一拍おく**と、そこが重く感じる（2026-08-21 の手ざわりの調べ）。
        /// ⚠️ 長いと「詰まった」に見える。</summary>
        public const double GateBeat = 0.26;

        // ── 放置の帯 ────────────────────────────────────

        /// <summary>地面が1秒に流れる幅。⭐ 進んでいる速さの見た目。</summary>
        public const double Scroll = 90;

        /// <summary>歩幅の揺れ。⚠️ 大きいと跳ねて見えて「進んでいる」から離れる。</summary>
        public const double Bob = 6;

        /// <summary>倒した次の相手が、外から転がって来るのにかかる秒。
        /// ⚠️ 短いと結局パッと入れ替わって見える（0.7 では早すぎたので倍にした）。</summary>
        public const double EntrySeconds = 1.4;

        // ── 祝い（Fanfare） ──────────────────────────────

        /// <summary>飛び出して1度沈むまで（秒）。⭐ 卵を得た・生まれた瞬間の pop。
        /// ⚠️ Unity 版 `View.Fanfare.PopSeconds` と同じ数（web 移植 2026-08-24 時点、
        /// Unity 側はまだ private const のまま ── ここを直しても Unity は自動で追従しない）。</summary>
        public const double CheerPop = 0.42;

        /// <summary>後ろの光が1周する秒。⭐ Unity 版 `View.Fanfare.SpinSeconds` と同じ数。</summary>
        public const double CheerSpin = 6.0;
    }
}
