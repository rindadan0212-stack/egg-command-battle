using EggCommand.Core;

namespace EggCommand.Web;

/// <summary>押されたことを、遊びの状態へ流す。
///
/// ⭐ **ここが「画面 → 規則」の唯一の口。**⚠️ 決めごとは Core が持つので、
/// ここがするのは**呼ぶ順と、次にどの画面を出すか**だけ。
///
/// ⭐ **戦いの演出はここが持つ**（1手を3拍に割る・数は `Core.Beats`）。
/// ⚠️ ただし**描かない** ── 出すものを `Spark` で言うだけで、
/// 実際に盤へ差すのは `fx.js`（組み直しで消えないように、Blazor の外に置く）。
///
/// ⚠️ すごろくの演出（さいころが転がる・駒が1マスずつ歩く）は**まだ無い**。
/// ⭐ 結果は同じところへ着くが、途中が飛ぶ。</summary>
public static class Deeds
{
    // ── 探索 ────────────────────────────────────────

    /// <summary>巣へ潜る。⚠️ 守り手は挑むたびに作り直す（同じ巣でも顔ぶれが変わる）。</summary>
    public static void Dive(Shell s, int at)
    {
        var list = s.Game.Encounters;
        if (at < 0 || at >= list.Count) return;
        var nest = list[at].Nest;
        s.Nest_ = nest;
        s.Boss = false;
        s.Space = -1;
        s.Raid_ = Trails.Begin(Trails.OfNest(nest), Games.PartyOf(s.Game),
            Games.RaidsOn(s.Game, nest));
        s.Open_ = null;
        s.Now_Sheet = Sheet.Raid;
    }

    /// <summary>ヌシへ挑む。⭐ この画面で塗るのはここだけ（輪の目的地は1つ）。</summary>
    public static void Boss(Shell s)
    {
        s.Nest_ = null;
        s.Boss = true;
        s.Space = -1;
        Begin(s, Nests.MakeBossParty(), null, null);
    }

    // ── すごろく ───────────────────────────────────

    /// <summary>さいころを振る。
    /// ⚠️ **種は巣と進み具合から作る。**⭐ その場で引くと、画面を出入りするだけで
    /// 出目を選び直せてしまう（Unity 版と同じ決めごと）。</summary>
    public static void Roll(Shell s)
    {
        var raid = s.Raid_;
        if (raid == null || raid.Rolls <= 0 || raid.Step != RaidStep.Moved) return;
        var nest = s.Nest_;
        if (nest == null) { s.Now_Sheet = Sheet.Nests; return; }

        var rng = new Rng(0).Stream(
            $"trail:{nest.Id}:{Games.RaidsOn(s.Game, nest)}"
            + $":{raid.Rolls}:{raid.At}:{raid.Took.Count}:{raid.Beaten.Count}");
        Trails.Roll(rng, raid);

        var open = Trails.Reach(raid, raid.Pending);
        // ⚠️ 1マスも動けない ── そこで見つかる
        if (open.Count == 0) { Trails.Stuck(raid); After(s); return; }
        // ⭐ **行ける先が1つだけなら、押させずに進む**（作者の指示 2026-08-20）
        if (open.Count == 1) { Walk(s, open[0]); return; }
        s.Open_ = open;
    }

    /// <summary>光っているマスを押した。
    /// ⚠️ **道は毎回引き直す。**⭐ 光らせた時点の道筋を覚えると、間に出目が変わる
    /// 出来事（関門の「N マス進む」）が挟まったときに古い長さで動く。</summary>
    public static void Step(Shell s, int goal)
    {
        var raid = s.Raid_;
        if (raid == null || raid.Step != RaidStep.Choosing) return;
        foreach (var path in Trails.Reach(raid, raid.Pending))
        {
            if (path[path.Count - 1] != goal) continue;
            Walk(s, path);
            return;
        }
        // ⚠️ **黙って何もしないをしない。**⭐ 光っていたのに行けないのは、
        //    盤か出目が押す前と変わったということ。
        s.Open_ = null;
        s.Say = $"そのマスへ行く道が無い（出目 {raid.Pending}）";
    }

    /// <summary>関門で払う。⚠️ 減る ── その潜入のあいだ戻らない。</summary>
    public static void Pay(Shell s)
    {
        if (s.Raid_ == null || s.Raid_.Step != RaidStep.Offered) return;
        Trails.Pay(s.Raid_);
        After(s);
    }

    /// <summary>関門を見送る。⭐ 払わなくても入れる。</summary>
    public static void Pass(Shell s)
    {
        if (s.Raid_ == null || s.Raid_.Step != RaidStep.Offered) return;
        Trails.Pass(s.Raid_);
        After(s);
    }

    private static void Walk(Shell s, IReadOnlyList<int> path)
    {
        s.Open_ = null;
        Trails.Go(s.Raid_!, path);
        After(s);
    }

    /// <summary>1手ぶんの後始末。⭐ 雑魚に会ったか、決着したかを見る。</summary>
    private static void After(Shell s)
    {
        var raid = s.Raid_;
        if (raid == null) return;

        if (raid.Step == RaidStep.Met)
        {
            // ⭐ 雑魚戦は潜入の途中。⚠️ 卵も巣の差し替えもここでは起きない
            s.Space = raid.At;
            var nest = s.Nest_!;
            Begin(s, Steal.MobPartyOf(nest, Games.RaidsOn(s.Game, nest), raid.At),
                raid.Hp, raid.Cooldowns);
            return;
        }
        if (raid.Result == null) return;

        bool won = raid.Result == StealOutcome.Success;
        var where = s.Nest_;
        s.Raid_ = null;
        if (won)
        {
            Games.GrowParty(Games.PartyOf(s.Game));
            Games.TakeEgg(s.Game, where!, EggOrigin.Stolen);
            Games.RecordRaid(s.Game, where!);
            s.Say = "卵を奪った";
            s.Now_Sheet = Sheet.Nests;
            return;
        }
        // ⚠️ **負けたら親と戦う**（逃げられない）
        s.Space = -1;
        Begin(s, Games.DefendersOf(s.Game, where!), raid.Hp, raid.Cooldowns);
    }

    // ── 戦闘 ────────────────────────────────────────

    private static void Begin(Shell s, List<Creature> foes,
        List<int>? hp, List<int[]>? cooldowns)
    {
        // ⭐ **戦闘ごとに引く。**⚠️ 渡さないと既定の固定種が使われ、
        //    弱化が通るかが毎回同じテープになる。
        s.Fight_ = EggCommand.Core.Battle.CreateBattle(
            Games.PartyOf(s.Game), foes, s.Game.RngBattle);
        // ⭐ 潜入で負った傷と CT をそのまま持ち込む
        if (hp != null && cooldowns != null)
            EggCommand.Core.Battle.CarryIn(s.Fight_, hp, cooldowns);
        s.Now_Sheet = Sheet.Fight;
        Rewind(s);
    }

    /// <summary>拍を最初に戻す。⚠️ 🔴 **戦いの出入りで必ず呼ぶ。**
    /// ⭐ 積んだままの手（`Cast`）は**前の戦いの体**を指しているので、
    /// そのまま次の戦いへ持ち込むと、居ない者の技を打とうとする。</summary>
    private static void Rewind(Shell s)
    {
        s.Stage = Stage.Idle;
        s.Wait = 0;
        s.Ticks = 0;
        s.Cast = null;
        s.CastAim = null;
        s.Sparks.Clear();
    }


    /// <summary>あきらめる。⚠️ **負けとして畳む** ── 只で抜けられると、
    /// 不利な戦いをいつでも無かったことにできてしまう。
    /// ⭐ 勝敗は画面側で作らない（Core が持つ）。</summary>
    public static void Concede(Shell s)
    {
        s.Open = Panel.None;
        var state = s.Fight_;
        if (state == null || state.Result != null) return;
        EggCommand.Core.Battle.Concede(state);
        Settle(s);
    }

    /// <summary>1拍で何が起きたか。</summary>
    public enum Tick
    {
        /// <summary>帯が伸びただけ／溜めの最中。⭐ **画面を組み直さない。**</summary>
        Filling,
        /// <summary>誰かが打った（か、名乗った）。⭐ ここだけ組み直す。</summary>
        Acted,
        /// <summary>人の手番で止まったか、決着した。</summary>
        Stopped,
    }

    /// <summary>1手をどこまで進めたか。⭐ **Unity 版 `BattleDriver.Phase` と同じ並び。**</summary>
    public enum Stage
    {
        /// <summary>何も進めていない（ゲージのレース中）。</summary>
        Idle,
        /// <summary>満ちた。⭐ **帯が満タンになったことを目で確かめさせてから**名乗る。</summary>
        Ready,
        /// <summary>名乗った。⚠️ **まだ打っていない**（打つのは次の拍）。</summary>
        Announcing,
        /// <summary>打った。⭐ 数字が飛び切るまで次を始めない。</summary>
        Settling,
    }

    /// <summary>時が進む。
    ///
    /// ⭐ **1手を3拍に割る**（Unity 版 `BattleDriver` と同じ・数は `Core.Beats`）:
    /// 名乗り → 着弾 → 間。⚠️ **状態が変わるのは着弾の一度だけ。**
    /// 拍ごとに触ると出所が2つになる。
    ///
    /// ⚠️ 割る前は押した瞬間に計算して即座に組み直していた。
    /// ⭐ 結果しか残らないので「何が起きたか」を字で説明する羽目になる。
    ///
    /// ⚠️ **人の手番では止まる** ── そこが「選ぶ」ところ。
    ///
    /// ⚠️ 🔴 **帯が伸びただけの拍で、画面を組み直さないこと。**
    /// ⭐ 組み直すと**押しどころが作り直されて触れなくなる**
    /// （Unity 版の `UnitStand.Retick` が同じ理由で分けてある）。</summary>
    /// <param name="seconds">前の拍からの間（秒）。</param>
    public static Tick Beat(Shell s, double seconds)
    {
        var state = s.Fight_;
        if (state == null) return Tick.Stopped;
        // ⚠️ **確かめている間は時が止まる。**⭐ 「あきらめますか」を読んでいるあいだに
        //    決着したら、答えた先が既に無い（それに、読む時間は考える時間でもある）。
        if (s.Open != Panel.None) return Tick.Stopped;

        // ⭐ 拍の途中。⚠️ ここで組み直さない（演出が最初からやり直しになる）
        if (s.Wait > 0) { s.Wait -= seconds; return Tick.Filling; }

        switch (s.Stage)
        {
            case Stage.Ready:
                // ⭐ 溜めが終わった。ここで初めて名乗る
                Shout(s, s.Cast!, s.CastSlot);
                s.Stage = Stage.Announcing;
                s.Wait = Beats.Announce;
                return Tick.Acted;

            case Stage.Announcing:
            {
                // ⭐ **状態が変わるのはここだけ**
                int before = state.Log.Count;
                EggCommand.Core.Battle.PerformAction(state, s.Cast!, s.CastSlot, s.CastAim);
                s.Cast = null;
                s.CastAim = null;
                Since(s, state, before);
                s.Stage = Stage.Settling;
                s.Wait = Beats.Settle;
                return Tick.Acted;
            }

            case Stage.Settling:
                s.Stage = Stage.Idle;
                // ⚠️ **決着の後始末はここ。**⭐ 打った拍で畳むと、
                //    最後の数字が飛ぶ前に画面が変わる
                Settle(s);
                return Tick.Acted;
        }

        // ⚠️ **決着は拍を通してから見る。**⭐ 先に見ると `Settling` へ来られず、
        //    戦いが終わったのに画面が戦闘のまま止まる（実測 2026-08-23）。
        if (state.Result != null) return Tick.Stopped;

        // ⭐ ここが「ゲージのレース」。⚠️ 端数を切り捨てると遅い者が永久に進まない
        s.Ticks += Beats.TicksPerSecond * seconds;
        int whole = (int)s.Ticks;
        // ⚠️ 🔴 **刻みが立たない拍では、何もしないで返す。**
        //    ⭐ ここを素通りさせると `NextActor` まで毎拍降りてきて、
        //    **画面を1秒に10回組み直す**ことになる（押しどころが触れなくなる）。
        if (whole <= 0) return Tick.Filling;
        s.Ticks -= whole;
        if (EggCommand.Core.Battle.AdvanceGauges(state, whole) > 0) return Tick.Filling;

        // ⚠️ **毒・リジェネはここで進む**（`NextActor` の中の `TickStatus`）。
        //    ⭐ 拾わないと、HP は減っているのに数字が1つも出ない。
        int ticked = state.Log.Count;
        var next = EggCommand.Core.Battle.NextActor(state);
        bool noisy = state.Log.Count > ticked;
        if (noisy) Since(s, state, ticked);

        // ⚠️ 誰も満ちていない。⭐ 組み直す理由が無い（毒が入った拍だけは出す）
        if (next == null) return noisy ? Tick.Acted : Tick.Filling;

        // ⭐ 味方の手番は人へ渡す（オートなら機械が選ぶ）
        if (next.Side == Side.Ally && !s.Auto)
            return s.Sparks.Count > 0 ? Tick.Acted : Tick.Stopped;

        int slot = Ai.ChooseAction(state, next);
        var skill = EggCommand.Core.Battle.SkillAt(next, slot);
        Unit? aim = null;
        if (skill != null && EggCommand.Core.Battle.NeedsTarget(skill))
        {
            bool ally = EggCommand.Core.Battle.TargetsAlly(skill);
            bool mine = next.Side == Side.Ally;
            // ⭐ 狙い先は**人が指したもの**（オート中も狙い先だけは人のまま）
            aim = Pick(state, ally == mine ? Side.Ally : Side.Enemy,
                mine ? (ally ? s.AimAlly : s.AimFoe) : null);
        }
        Queue(s, next, slot, aim);
        return Tick.Acted;
    }

    /// <summary>手を積む。⚠️ **ここではまだ計算しない** ── 名乗りを出して、着弾は次の拍。
    /// ⭐ 帯が満タンになったことを目で確かめさせてから名乗る。</summary>
    private static void Queue(Shell s, Unit actor, int slot, Unit? aim)
    {
        s.Cast = actor;
        s.CastSlot = slot;
        s.CastAim = aim;
        s.Stage = Stage.Ready;
        s.Wait = Beats.Ready;
    }

    /// <summary>人が技を選んだ。⚠️ 使えない枠は撥ねる（黙って別の技を撃たない）。
    /// ⭐ **押した瞬間に片付けない** ── 名乗りから始める。</summary>
    public static void Strike(Shell s, int slot)
    {
        var state = s.Fight_;
        if (state == null || state.Result != null || s.Stage != Stage.Idle) return;
        var actor = EggCommand.Core.Battle.NextActor(state);
        if (actor == null || actor.Side != Side.Ally) return;
        if (!EggCommand.Core.Battle.IsUsable(actor, slot)) return;

        var skill = EggCommand.Core.Battle.SkillAt(actor, slot);
        Unit? aim = null;
        if (skill != null && EggCommand.Core.Battle.NeedsTarget(skill))
        {
            // ⭐ 狙い先は**人が指したもの**。⚠️ 指していなければ最初の生き残り
            bool ally = EggCommand.Core.Battle.TargetsAlly(skill);
            aim = Pick(state, ally ? Side.Ally : Side.Enemy, ally ? s.AimAlly : s.AimFoe);
        }
        // ⭐ **溜めは要らない**（札が出て考える時間が、そのまま溜めになっている）
        s.Cast = actor;
        s.CastSlot = slot;
        s.CastAim = aim;
        Shout(s, actor, slot);
        s.Stage = Stage.Announcing;
        s.Wait = Beats.Announce;
    }

    /// <summary>名乗り。⭐ 頭上に技名、足元に輪、体をひと突き。</summary>
    private static void Shout(Shell s, Unit actor, int slot)
    {
        string at = Where(s, actor);
        var skill = EggCommand.Core.Battle.SkillAt(actor, slot);
        if (skill != null) s.Sparks.Add(new Spark(at, "shout", skill.Name, null, 40, 0));
        s.Sparks.Add(new Spark(at, "ring", "", Face.ElementCss(actor.Creature.Element), 0, 0));
        // ⭐ 味方は右へ、敵は左へ踏み込む
        s.Sparks.Add(new Spark(at, actor.Side == Side.Ally ? "step" : "stepf", "", null, 0, 0));
    }

    /// <summary>直前の手で起きたことを、当たった体の上に出す。
    ///
    /// ⭐ **ここが「説明文の代わり」。**⚠️ 増やすときは字数でなく**見え方**を足す。
    /// ⚠️ 同じ体に2つ以上出ることがある（殴って毒を盛って CT を伸ばす、など）。
    /// ⭐ 同じ場所に重ねると下の字が読めないので、1つ出すごとに上へ積む。
    /// ⚠️ 「数を減らす」方向では直さない ── 起きたことを隠すことになる。</summary>
    private static void Since(Shell s, BattleState state, int from)
    {
        var stacked = new Dictionary<string, int>();

        for (int i = from; i < state.Log.Count; i++)
        {
            var e = state.Log[i];
            string at = Named(state, e.Unit);
            // ⭐ 「打った」は積まない（名乗りで既に出している）
            int up = e.Kind == BattleEventKind.Act ? 0 : Stack(stacked, e.Unit);

            switch (e.Kind)
            {
                case BattleEventKind.Damage:
                    if (e.Absorbed > 0) Say(at, "◇", Ink, 54, up);
                    else if (e.Amount > 0)
                    {
                        // ⭐ 光る → 数字 → 体が跳ねる。3つ同時だから「当たった」に見える
                        // ⚠️ **光は明るいほう、数字は暗いほう。**⭐ Unity は両方 `Ui.Danger`
                        //    だが、web の空（`--sky-battle`）は明るいので、
                        //    同じ赤だと数字が地に沈む（縁取りだけで支えることになる）。
                        s.Sparks.Add(new Spark(at, "hit", "", "var(--danger)", 0, 0));
                        Say(at, Face.Digits(e.Amount), Danger, 56, up);
                        s.Sparks.Add(new Spark(at, "shock", "", null, 0, 0));
                    }
                    break;
                case BattleEventKind.Poison: Say(at, Face.Digits(e.Amount), "#b98cd8", 44, up); break;
                case BattleEventKind.Heal:
                case BattleEventKind.Regen:
                    if (e.Amount > 0)
                    {
                        s.Sparks.Add(new Spark(at, "ring", "", Good, 0, 0));
                        Say(at, "+" + Face.Digits(e.Amount), Good, 46, up);
                    }
                    break;
                case BattleEventKind.Buff:
                    Say(at, (e.Percent > 0 ? "▲" : "▼") + Stats.LabelOf(e.Stat),
                        e.Percent > 0 ? Good : Danger, 34, up);
                    break;
                case BattleEventKind.Shield: s.Sparks.Add(new Spark(at, "ring", "", Ink, 0, 0)); break;
                case BattleEventKind.Stun:
                case BattleEventKind.Skipped: Say(at, "✖", Accent, 50, up); break;
                case BattleEventKind.GutsSaved: Say(at, "1", Accent, 56, up); break;
                case BattleEventKind.Blocked: Say(at, "◇", Dim, 44, up); break;
                case BattleEventKind.Down: Say(at, "…", Faint, 48, up); break;

                // ⚠️ 以下が出ないと「効いたのか外れたのか」が読めず、
                //    弱化を持つ技が「何も起きない技」に見える。
                case BattleEventKind.Missed: Say(at, "外れ", Dim, 40, up); break;
                case BattleEventKind.Applied: Say(at, e.Label ?? "", Ink, 34, up); break;
                case BattleEventKind.Ct:
                    // ⚠️ **増える方が悪い**（待たされる）。符号ではなく色で読ませる
                    Say(at, e.Delta > 0 ? $"CT+{e.Delta}" : $"CT{e.Delta}",
                        e.Delta > 0 ? Danger : Good, 36, up);
                    break;
                case BattleEventKind.Taunt:
                    Say(at, e.Hits > 0 ? $"挑発×{e.Hits}" : "挑発", Accent, 34, up); break;
                case BattleEventKind.Guts: Say(at, "ガッツ", Accent, 34, up); break;
                case BattleEventKind.Immune: Say(at, "免疫", Good, 34, up); break;
                case BattleEventKind.Gauge:
                    Say(at, e.Amount >= 0 ? "ゲージ↑" : "ゲージ↓",
                        e.Amount >= 0 ? Good : Danger, 34, up);
                    break;
                case BattleEventKind.Sleep: Say(at, "眠り", Accent, 34, up); break;
                case BattleEventKind.Woke: Say(at, "起きた", Dim, 34, up); break;
                case BattleEventKind.Block: Say(at, "ブロック", Accent, 34, up); break;
                case BattleEventKind.Blunted: Say(at, "通らない", Dim, 38, up); break;
                case BattleEventKind.Dispelled: Say(at, e.Label ?? "解除", Accent, 34, up); break;
                case BattleEventKind.Revived:
                    s.Sparks.Add(new Spark(at, "ring", "", Good, 0, 0));
                    Say(at, "+" + Face.Digits(e.Amount), Good, 46, up);
                    break;
            }
        }

        void Say(string at, string text, string tint, int size, int up) =>
            s.Sparks.Add(new Spark(at, "say", text, tint, size, up));
    }

    // ⚠️ 色は `stage.css` の変数と同じ数。⭐ 字にする側が1つの名前で受け取れるように、
    //    ここでは CSS の値そのものを使う（`Face.ElementCss` と同じ約束）。
    private const string Ink = "var(--ink)";
    private const string Dim = "var(--ink-dim)";
    private const string Faint = "var(--ink-faint)";
    private const string Good = "var(--good-ink)";
    private const string Danger = "var(--danger-ink)";
    private const string Accent = "var(--accent-ink)";

    private static int Stack(Dictionary<string, int> seen, string key)
    {
        seen.TryGetValue(key, out int n);
        seen[key] = n + 1;
        return n;
    }

    /// <summary>Key から `a0` `f2` を引く。⚠️ 見つからなければ味方の1体目。</summary>
    private static string Named(BattleState state, string key)
    {
        int a = 0, f = 0;
        foreach (var u in state.Units)
        {
            string at = u.Side == Side.Ally ? "a" + a++ : "f" + f++;
            if (u.Key == key) return at;
        }
        return "a0";
    }

    /// <summary>その体を指す名前（`a0` `f2`）。⚠️ **側も入れる**
    /// ── 番号だけだと味方の1体目と敵の1体目が同じ名前になる。</summary>
    private static string Where(Shell s, Unit who)
    {
        var state = s.Fight_!;
        int a = 0, f = 0;
        foreach (var u in state.Units)
        {
            string at = u.Side == Side.Ally ? "a" + a++ : "f" + f++;
            if (ReferenceEquals(u, who) || u.Key == who.Key) return at;
        }
        return "a0";
    }

    /// <summary>いまの帯の伸び具。⭐ 組み直さずに、これだけを差し替える。</summary>
    public static Dictionary<string, double> Bars(Shell s)
    {
        var bars = new Dictionary<string, double>();
        var state = s.Fight_;
        if (state == null) return bars;
        int a = 0, f = 0;
        foreach (var u in state.Units)
        {
            string at = u.Side == Side.Ally ? "a" + a++ : "f" + f++;
            bars["gauge#" + at] =
                Math.Clamp(u.Gauge / (double)EggCommand.Core.Battle.GaugeMax, 0, 1);
            bars["hpfill#" + at] =
                u.MaxHp > 0 ? Math.Clamp(u.Hp / (double)u.MaxHp, 0, 1) : 0;
        }
        return bars;
    }

    private static Unit? Pick(BattleState state, Side side, string? key)
    {
        Unit? first = null;
        foreach (var u in state.Units)
        {
            if (u.Side != side || !EggCommand.Core.Battle.IsAlive(u)) continue;
            if (u.Key == key) return u;
            first ??= u;
        }
        return first;
    }

    /// <summary>決着したら、その後始末をする。</summary>
    private static void Settle(Shell s)
    {
        var state = s.Fight_;
        if (state?.Result == null) return;
        bool won = state.Result == Outcome.Ally;
        var nest = s.Nest_;
        s.Fight_ = null;
        Rewind(s);

        // ⭐ **試練は巣ではない。**⚠️ 卵は出ない ── 出すと「試練で卵を稼ぐ」が
        //    最短経路になり、潜入も配合も回らなくなる。返るのは勝った印だけ。
        if (s.Trial_ is Trial trial)
        {
            s.Trial_ = null;
            if (won) { Games.GrowParty(Games.PartyOf(s.Game)); Games.MarkTrial(s.Game, trial.Id); }
            s.Say = won ? $"{trial.Name} に勝った" : $"{trial.Name} に負けた";
            s.Now_Sheet = Sheet.Trial;
            return;
        }

        // ⭐ 雑魚戦は潜入の途中。⚠️ 卵も巣の差し替えもここでは起きない
        if (s.Space >= 0)
        {
            s.Space = -1;
            var raid = s.Raid_;
            if (raid == null) { s.Now_Sheet = Sheet.Nests; return; }
            if (!won)
            {
                Trails.Lost(raid);
                s.Raid_ = null;
                if (nest != null) Encounters.Replace(s.Game, nest, s.Now);
                s.Say = "親に見つかった";
                s.Now_Sheet = Sheet.Nests;
                return;
            }
            // ⚠️ **傷と CT を潜入へ書き戻してから** `Beat` を呼ぶ。
            //    ⭐ 飛ばすと次の戦いが毎回満タンから始まり、
            //    「戦うほど苦しくなる」という雑魚の対価が丸ごと消える。
            EggCommand.Core.Battle.CarryOut(state, raid.Hp, raid.Cooldowns);
            Trails.Beat(raid);
            Games.GrowParty(Games.PartyOf(s.Game), Steal.MobReward);
            s.Now_Sheet = Sheet.Raid;
            return;
        }

        if (won)
        {
            Games.GrowParty(Games.PartyOf(s.Game));
            if (!s.Boss && nest != null)
            {
                // ⭐ **戦って倒したら親は失われる。**その巣にはもう挑めない。
                Games.TakeEgg(s.Game, nest, EggOrigin.Defeated);
                Encounters.Replace(s.Game, nest, s.Now);
                s.Say = "卵を手に入れた";
                s.Now_Sheet = Sheet.Nests;
                return;
            }
        }
        // ⚠️ 負けた巣も引き直す。同じ相手を叩き続ける形にしない
        if (!s.Boss && nest != null) Encounters.Replace(s.Game, nest, s.Now);
        s.Say = won ? "勝った" : "負けた";
        s.Now_Sheet = Sheet.Nests;
    }

    // ── 孵化 ────────────────────────────────────────

    /// <summary>孵化器の枠を押した。⭐ 空いていれば在庫を開き、孵っていれば取り出す。</summary>
    public static void Slot(Shell s, int at)
    {
        var found = Hatchery.At(s.Game, at);
        if (found == null) { s.Aim = at; s.Open = Panel.Eggs; return; }
        if (!Hatchery.IsReady(found, s.Now)) return;
        var born = Games.HatchEgg(s.Game, found.Egg.Id);
        s.Say = $"{Creatures.SpeciesOf(born).Name} が孵った";
    }

    /// <summary>在庫から卵を選んだ。⚠️ 枠が無ければ入れない（黙って捨てない）。</summary>
    public static void Warm(Shell s, int at)
    {
        if (at < 0 || at >= s.Game.Eggs.Count) return;
        var egg = s.Game.Eggs[at];
        try { Hatchery.Begin(s.Game, egg.Id, s.Now, slot: s.Aim); }
        catch (Exception e) { s.Say = e.Message; }
        s.Open = Panel.None;
    }

    // ── 分解 ────────────────────────────────────────

    /// <summary>分解の候補（⭐ **見ている本人を外した**並び）。
    /// ⚠️ 出撃中も候補に出す ── 外していた頃は、手持ちが少ない序盤に
    /// 1体も選べず何もできなかった。⭐ 出撃中は升に印で出す。</summary>
    public static IReadOnlyList<Creature> Food(Shell s)
    {
        string? mine = s.PickedOne()?.Id;
        var list = new List<Creature>();
        foreach (var c in s.Sorted()) if (c.Id != mine) list.Add(c);
        return list;
    }

    /// <summary>分解する個体を選んだ／外した。
    /// ⚠️ **上限を超えたら黙って古いものを押し出さない** ── 選び直しだと分かるように撥ねる。</summary>
    public static void Mark(Shell s, int at)
    {
        var pool = Food(s);
        if (at < 0 || at >= pool.Count) return;
        string id = pool[at].Id;
        if (s.Melts.Remove(id)) return;
        if (s.Melts.Count >= Games.PickAtOnce)
        {
            s.Say = $"一度に分解できるのは {Games.PickAtOnce} 体まで";
            return;
        }
        s.Melts.Add(id);
    }

    /// <summary>分解する。⚠️ **戻せない**（＝「逃がす」の代わりでもある）。
    /// ⭐ 数え方も削除も Core が1回で持つ（<see cref="Games.Dissolve"/>）。</summary>
    public static void Melt(Shell s)
    {
        if (s.Melts.Count == 0) return;
        int got = Games.Dissolve(s.Game, new List<string>(s.Melts));
        // ⚠️ **消えた個体を指したままにしない。**⭐ 見る先だけでなく**配合の親**も外す
        //    ── 残すと、次に「配合する」を押した瞬間に「保管庫にいない」で落ちる。
        if (s.Picked != null && s.Melts.Contains(s.Picked)) s.Picked = null;
        if (s.ParentA != null && s.Melts.Contains(s.ParentA)) s.ParentA = null;
        if (s.ParentB != null && s.Melts.Contains(s.ParentB)) s.ParentB = null;
        s.Melts.Clear();
        s.Open = Panel.None;
        s.Say = $"EXP ＋{Face.Digits(got)}";
    }

    // ── 技を鍛える ──────────────────────────────────

    /// <summary>鍛える卵を選んだ／外した。⚠️ **上限を超える卵は受け取らない**
    /// ── 受け取ると超えた分が黙って消える（2時間待った★5が蒸発する）。</summary>
    public static void Feed_(Shell s, int at)
    {
        var eggs = s.Game.Eggs;
        if (at < 0 || at >= eggs.Count) return;
        string id = eggs[at].Id;
        if (s.Feeds.Remove(id)) return;

        var one = s.PickedOne();
        if (one == null) return;
        int points = one.SkillPoints[s.Slot_];
        int room = SkillCosts.TotalFor(Skills.MaxLevel) - points;
        int gain = 0;
        foreach (var e in eggs) if (s.Feeds.Contains(e.Id)) gain += Rarities.PointsOf(e.Rarity);
        int worth = Rarities.PointsOf(eggs[at].Rarity);

        if (s.Feeds.Count >= Games.PickAtOnce)
        {
            s.Say = $"一度に入れられるのは {Games.PickAtOnce} 個まで";
            return;
        }
        if (worth > room - gain) { s.Say = "その卵を入れると上限を超える"; return; }
        s.Feeds.Add(id);
    }

    /// <summary>入れる。⭐ 入る順も削除も Core が1回で持つ
    /// （<see cref="Games.FeedEggsToSkill"/>）。</summary>
    public static void Feed(Shell s)
    {
        var one = s.PickedOne();
        if (one == null || s.Feeds.Count == 0) return;
        int got = Games.FeedEggsToSkill(s.Game, one.Id, s.Slot_, new List<string>(s.Feeds));
        s.Feeds.Clear();
        s.Open = Panel.None;
        s.Say = got > 0 ? $"技が ＋{got}" : "入らなかった";
    }

    /// <summary>溜めた EXP で Lv を1つ上げる。⭐ **1回で1レベル**
    /// ── 一気に上限まで入れると、上げ止めどころを選べない。</summary>
    public static void Grow(Shell s)
    {
        var one = s.PickedOne();
        if (one == null) return;
        // ⚠️ **黙って何もしないをしない。**⭐ 足りないのか上限なのかを言う
        if (Core.Idle.Spend(s.Game.Idle, one) > 0) { s.Say = $"Lv {Levels.Of(one)} になった"; return; }
        s.Say = one.Earned >= Levels.GrowMax ? "これ以上は育たない"
            : $"EXP が {Face.Digits(Levels.ExpToNext(one))} 要る";
    }

    // ── 配合 ────────────────────────────────────────

    /// <summary>配合する。⚠️ **2体が卵に還る**（両親は失われる）。</summary>
    public static void Breed(Shell s)
    {
        if (s.ParentA == null || s.ParentB == null) { s.Say = "親を2体えらぶ"; return; }
        var born = Games.FusePair(s.Game, s.ParentA, s.ParentB);
        s.ParentA = null;
        s.ParentB = null;
        // ⚠️ 見ていた個体が親だったなら、見る先も外す
        s.Picked = null;
        s.Say = $"{SpeciesTable.ById(born.Egg.SpeciesId).Name} の卵ができた"
            + $"（{Rarities.StarsOf(born.Egg.Rarity)}）";
    }

    // ── 編成 ────────────────────────────────────────

    /// <summary>巣の編成を切り替える。⚠️ 3つとも中身は別。</summary>
    public static void Team(Shell s, int at)
    {
        if (at < 0 || at >= Games.NestPartySlots) return;
        s.Game.NestParty = at;
    }

    /// <summary>選んでいる枠を押した ── ⭐ **外す**。
    /// ⚠️ 空き枠は押しても何も起きない（一覧から入れるのが道）。</summary>
    public static void Drop(Shell s, int at)
    {
        var kind = s.IdleParty ? PartyKind.Idle : PartyKind.Nest;
        var roster = Games.RosterOf(s.Game, kind);
        if (at < 0 || at >= roster.Count) return;
        Games.TogglePartyMember(s.Game, roster[at], kind);
    }

    // ── 試練 ────────────────────────────────────────

    /// <summary>試練へ挑む。⭐ **顔ぶれは毎回まったく同じ**。
    /// ⚠️ 巣の欄を空にする ── 空にしないと決着のときに巣の後始末が動く。</summary>
    public static void Trial(Shell s, int at)
    {
        var all = Trials.All;
        if (at < 0 || at >= all.Count) return;
        s.Nest_ = null;
        s.Boss = false;
        s.Space = -1;
        s.Trial_ = all[at];
        Begin(s, Trials.PartyOf(all[at]), null, null);
    }
}
