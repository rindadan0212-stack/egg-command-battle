#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>放置の拍。⭐ Come/Face/Finish/Rest は実秒で固定。**Fight だけ可変**
    /// （2026-08-28・「見せかけの打ち合いから本物の手番制へ」の作り直し）。
    ///
    /// ⚠️ 順番は必ず Come → Face → Fight → Finish → Rest → Come…</summary>
    public enum IdlePhase
    {
        /// <summary>相手が画面外の右から飛び込む（1.0秒）。</summary>
        Come,
        /// <summary>構える。何も起きない「息を吸う」間（0.5秒）。</summary>
        Face,
        /// <summary>打ち合い。⭐ **本物の手番制**（2026-08-28）── 味方4体＋敵の5者が
        /// <see cref="Battle.GaugeRate"/> で溜まるゲージを競い、満ちた者のうち最大の者が動く
        /// （<see cref="Battle.NextActor"/> と同じ選び方）。1手＝<see cref="Idle.ActSeconds"/>
        /// （0.5秒）固定。味方が8発当てると終わる ── 敵の手が挟まるほど伸びるので、
        /// 長さは<see cref="Idle.FightMinSeconds"/>（4.0秒）以上の**可変**になる。</summary>
        Fight,
        /// <summary>とどめ。帯が0になり、相手が消える（0.4秒）。</summary>
        Finish,
        /// <summary>余韻。相手は居ない（1.1秒）。</summary>
        Rest,
    }

    /// <summary>放置の状態。ホームで編成が右へ進み続けている。</summary>
    public sealed class IdleRun
    {
        /// <summary>溜まっている EXP。⭐ これを BOX で Lv に変える。
        /// ⚠️ 2026-08-19 まで「素材」と呼んでいたもの。古い保存の <c>Materials</c> 欄は
        /// 読むときだけ拾う（<c>Snapshot</c>）。</summary>
        public int Exp;
        /// <summary>EXP の端数（1未満）。⭐ 呼び出しをまたいで持ち越す ── 1回ごとに丸めると
        /// 丸めの向きが同じほうへ寄り続けて実際の率からずれていく。
        /// ⭐ セーブにも乗せる。保存をまたいでも丸めの向きが変わらない。</summary>
        public double ExpCarry;
        /// <summary>🔴 最後に清算した時刻の**秒未満の端数**（0〜1未満・2026-08-28）。
        /// ⭐ <see cref="LastUnix"/>（整数秒）と対で「実数の時刻」を作る。
        /// ⭐ 保存する。これを捨てると、秒未満で保存した直後の続行で拍がずれる。
        /// ⚠️ これが無いと、1秒に4回覗いても拍は1秒ぶんずつしか進まず、
        /// 0.5秒ごとのはずの打撃が同じ瞬間に2発出る（実測して足した）。</summary>
        public double Fine;

        /// <summary>最後に清算した時刻。⚠️ 経過は「今 − ここ」で出す。
        /// 残り時間で持つと、見ていない間の時間が進まない（孵化器と同じ約束）。</summary>
        public long LastUnix;
        /// <summary>倒した数。⭐ 進んだ距離そのもの。背景を流す量に使う。
        /// ⚠️ 敵をちょうど8発で倒すたび1増える（<see cref="Idle.Advance"/> が唯一の出所）。</summary>
        public int Defeated;

        /// <summary>いまの拍。⭐ <see cref="Idle.Advance"/> だけが動かす。</summary>
        public IdlePhase Phase = IdlePhase.Come;
        /// <summary>いまの拍の残り秒。
        /// ⚠️ **Fight の間だけ意味が違う**（2026-08-28）── 「拍の残り」ではなく
        /// 「次の手番（<see cref="Idle.ActSeconds"/> 刻み）までの残り」。
        /// Fight の長さそのものは編成と敵の手の挟まり方で変わるので、
        /// 固定長の残り時間という形では持てない。</summary>
        public double PhaseLeft = Idle.ComeSeconds;
        /// <summary>いまの相手に、味方が当てた回数（0..<see cref="Idle.StrikeCount"/>）。
        /// ⭐ 帯（<see cref="Idle.FoeLeft"/>）はここから出す ── 唯一の出所。
        /// ⚠️ 多段（<c>effect.Repeat</c>）でも1手＝1（威力・多段はダメージだけを増やす）。</summary>
        public int Struck;
        /// <summary>いまの相手に与えた累計ダメージ。⭐ 稼ぎ（EXP）の唯一の元
        /// （<see cref="Idle.ExpPerDamage"/> で割る）。倒す・次の相手が現れるたびに 0 へ戻す。</summary>
        public int Damage;

        /// <summary>味方のゲージ（<see cref="Creature.Id"/> → 溜まり）。
        /// ⭐ <see cref="Battle.GaugeMax"/> で満ちる ── 戦闘の <see cref="Unit.Gauge"/> と同じ意味。
        /// ⚠️ Fight が始まる拍（Face → Fight）の頭で全員 0 に戻す ── 戦闘が
        /// 1戦ごとに新しい <see cref="BattleState"/> を作るのと同じ約束。</summary>
        public readonly Dictionary<string, int> Gauge = new Dictionary<string, int>();
        /// <summary>敵のゲージ。⚠️ 相手は常に1体なので、個体識別は要らない。</summary>
        public int FoeGauge;

        /// <summary>味方の残りHP割合（<c>Creature.Id</c> → 1.0が満タン）。
        /// ⭐ 敵の一撃は最大HPの<see cref="Idle.FoeDamagePercent"/>%を割合で削る。
        /// ⚠️ 未登録（初めて殴られる）なら 1.0 扱い ── 登録は「一度でも減った」ときだけ。
        /// 復帰したら 1.0 に戻す（<see cref="Idle.ReviveDue"/>）。</summary>
        public readonly Dictionary<string, double> Health = new Dictionary<string, double>();

        /// <summary>倒れている者と、その復活時刻。
        /// ⭐ **2026-08-28（本物の手番制）で、また書き込まれるようになった** ──
        /// 敵の一撃を2発（合計100%）受けると倒れ、<see cref="Idle.ReviveSeconds"/> 秒で
        /// 全快して起き上がる。</summary>
        public readonly Dictionary<string, long> DownUntil = new Dictionary<string, long>();

        /// <summary>⚠️ **もう <see cref="Idle.Advance"/> からは書かれない**（旧・敵の実数の残り HP。
        /// 帯は <see cref="Struck"/> から出す）。🔴 **消していない**。保存の形
        /// （<c>IdleSave.EnemyHp</c>）が担当外の <c>Snapshot.cs</c> にあり、そちらを触らずに
        /// 欄を消すとビルドが壊れる。⭐ 値は常に既定の 0 のまま（読んでも意味を持たない）。
        /// 次の担当が <c>Snapshot.cs</c> ごと整理してよい。</summary>
        public double EnemyHp;
        /// <summary>⚠️ 同上（未使用の欄）。旧・「溜めが届くと倒れる」の溜めそのもの。
        /// 🔴 **2026-08-28: 倒れの仕組みは結局これを使わなかった**（当たった回数＝
        /// <see cref="Struck"/> が8に届くと倒れる／味方は割合HPが尽きると倒れる、の
        /// どちらも「溜め」ではない）── <see cref="Idle.ChargeSeconds"/> ごと永久に未使用のまま。
        /// 器だけ壊さず残してある（<c>Snapshot.cs</c> 互換のため）。</summary>
        public double Charge;
        /// <summary>⚠️ 同上（いまも未使用）。旧・「次の敵が現れてから、まだ手が出ていない秒」。
        /// 拍の Come/Face がその役目を引き継いだ。</summary>
        public double Spawn;

        /// <summary>いま出ている相手の種族（<see cref="SpeciesTable.All"/> の添字）。
        /// ⭐ 見た目だけでなく、属性（<see cref="Idle.FoeElementOf"/> 経由）の元にもなる
        /// （2026-08-28・仕事2で追加）。</summary>
        public int FoeSpecies;
        /// <summary>その色（<c>Palettes</c> の添字）。⭐ ほとんど 0（通常色）、稀に色違い。</summary>
        public int FoePalette;
    }

    /// <summary>ホームの放置。
    ///
    /// 🔴 **2026-08-28 に「見せかけの打ち合い」から「本物の手番制」へ作り直した**
    /// （作者の指示）。**殴った回数が倒す。ダメージ量が払う。**
    /// - 敵は味方の8発（<see cref="StrikeCount"/>）で倒れる ── 威力は関係ない。これが拍を守る。
    /// - 稼ぎはその敵に与えた**総ダメージ**（<see cref="ExpPerDamage"/> で割る）で決まる ──
    ///   これが「速く・強い個体を編成する」動機になる。
    /// - 手番は<see cref="Battle.GaugeRate"/>/<see cref="Battle.GaugeMax"/>（戦闘と同じ式）で
    ///   決まる ── 速い個体は何度も動き、遅い個体は動かないことがある。
    /// - ⚠️ **1手の実時間は <see cref="ActSeconds"/>（0.5秒）で固定。**速度は「誰が動くか」
    ///   だけを決め、「どれだけ速く時間が進むか」は決めない。これが無いと、育つほど
    ///   手数が増えて周期が縮み、上達の報酬が「点滅が速くなること」になる
    ///   （2026-08-28 の1つ前の作り直しで直した不具合。戻さない）。
    ///
    /// ⭐ 直前（同じ日）の作り直しは「討伐の間合いを7.0秒固定にして、稼ぎを力×秒にする」
    /// だったが、これは「殴った回数と与えたダメージ」という作者の狙いとは別物だったため、
    /// 同じ日のうちにさらに作り直した。拍の骨組み（Come/Face/Fight/Finish/Rest）と
    /// 「1手＝ActSeconds 固定」の理由は引き継ぎ、**Fight の長さと稼ぎの元**だけを差し替えてある。
    ///
    /// ⚠️ ここは時計を持たない。経過秒は呼び側が渡す（孵化器と同じ）。
    /// ⚠️ **乱数は「見た目」「卵」「敵の狙い先」にだけ使う**（2026-08-28・仕事3で「敵の狙い先」が
    /// 増えた）。拍の進み方・ダメージ量・討伐そのものは乱数を使わない ── 同じ乱数の種と
    /// 同じ経過からは、いつ何を倒し・誰が狙われたかまで含めて必ず同じになる
    /// （<see cref="Rng"/> は決定論的な擬似乱数なので、種が同じなら再現できる）。
    /// </summary>
    public static class Idle
    {
        // ── 拍（テンポ） ──────────────────────────────────

        /// <summary>相手が画面外の右から飛び込む拍の長さ（秒）。</summary>
        public const double ComeSeconds = 1.0;
        /// <summary>構える拍の長さ（秒）。⚠️ 何も起きない「息を吸う」間。</summary>
        public const double FaceSeconds = 0.5;
        /// <summary>とどめの拍の長さ（秒）。帯が0になり、相手が消える。</summary>
        public const double FinishSeconds = 0.4;
        /// <summary>余韻の拍の長さ（秒）。相手は居ない。</summary>
        public const double RestSeconds = 1.1;

        /// <summary>1手（誰か1人が動くこと）にかかる実時間（秒）。⭐ **固定**（2026-08-28）。
        ///
        /// ⚠️ 速度は<see cref="PickActor"/>で「誰が動くか」だけを決める。「どれだけ速く
        /// 時間が進むか」は決めない ── ここが崩れると、育つほど手数が増えて周期が縮み、
        /// 上達の報酬が「点滅が速くなること」になる（1つ前の作り直しで直した不具合。
        /// 戻さないこと、と作者から明言されている）。</summary>
        public const double ActSeconds = 0.5;

        /// <summary>敵を倒すのに要る、味方の当たった回数。
        ///
        /// ⭐ 設計メモでは「FoeHits」と呼ばれているが、同じ意味の定数が既にあるのでそちらへ
        /// 寄せる（同じ判断を2か所に書かない）。⚠️ 威力にも多段（<c>effect.Repeat</c>）にも
        /// 関係ない ── 1手＝1（<see cref="PerformAllyStrike"/>）。多段はダメージだけを増やす。</summary>
        public const int StrikeCount = 8;

        /// <summary>Fight の最短（秒）＝味方だけが8回動いたとき（敵の手が一度も挟まらない）。
        /// ⭐ 育ってもここより短くならない ── 速度が上がるほど「敵より先に何度も動ける」に
        /// 変わるだけで、1手の実時間（<see cref="ActSeconds"/>）そのものは動かないため。</summary>
        public const double FightMinSeconds = StrikeCount * ActSeconds;

        /// <summary>1周期の最短（秒）＝7.0。⭐ 育っても短くならない下限
        /// （<see cref="FightMinSeconds"/> が下限である以上、これも下限）。</summary>
        public const double MinCycleSeconds =
            ComeSeconds + FaceSeconds + FightMinSeconds + FinishSeconds + RestSeconds;

        // ── 敵（放置専用の暫定値） ──────────────────────────

        /// <summary>敵の速度。⚠️ **暫定値**（作者「いったん暫定値で入れといて」）。
        ///
        /// 🔴 実測して決めた: <c>Games.NewGame</c> の遊び始めの編成（4体・shallow-scale の卵）の
        /// 平均速度は 112.4（種族 tamaru の基礎速度80 + 野生ロールぶん、20シード×4体＝
        /// 80サンプルの平均） ── 2026-08-28、スクラッチのハーネスで <c>Games.PartyOf</c> を
        /// 直に測った。⭐ それと同じくらいの値としてここに置く。</summary>
        public const int FoeSpeed = 112;

        /// <summary>敵の防御。⚠️ 作者「敵はステータスを持たず」── 常に0。
        /// ⭐ 定数として名前を付けておく（後で回せるように）。</summary>
        public const int FoeDefense = 0;

        /// <summary>敵の一撃が削る、狙われた味方の最大HPに対する割合（%）。
        /// ⭐ 作者「割合ダメージ」「2発くらうとダウン」＝50%。</summary>
        public const int FoeDamagePercent = 50;

        /// <summary>ダメージ1点あたり、何点でEXP1に換算するか。
        ///
        /// ⚠️ **実測して決めた**（2026-08-28）: <c>Games.NewGame</c> の遊び始めの編成
        /// （4体・shallow-scale の卵）を、実際にこの手番制のまま「見ている間」の刻み
        /// （1秒ごとに <see cref="Advance"/> を呼ぶ ── <see cref="LiveWindowSeconds"/> の
        /// 内側）で10分ぶん走らせ、8シードぶんの EXP/分を平均した
        /// （スクラッチのハーネスで <c>ExpPerDamage=46</c> のとき平均 6931 EXP/分だった）。
        /// 狙いは旧水準と同じ**約137 EXP/分**なので、<c>46 × 6931/137 ≈ 2327</c> へ逆算した。</summary>
        public const double ExpPerDamage = 2327.0;

        /// <summary>倒れてから起き上がるまでの秒。🔴 **20 → 3**（2026-08-28・作者の指示）。</summary>
        public const int ReviveSeconds = 3;

        /// <summary>敵が一撃を放つまでの秒。
        /// ⚠️ **2026-08-28（本物の手番制）で、実は使われなくなった** ── 敵がいつ動くかは
        /// 「溜め」ではなく<see cref="PickActor"/>のゲージ競争そのもので決まる。
        /// ⭐ 消さずに残す ── 器だけは前の担当から引き継いだ形のまま
        /// （<see cref="IdleRun.Charge"/> と同じ理由）。</summary>
        public const double ChargeSeconds = 4.0;

        /// <summary>まとめて清算できる上限（秒）。⚠️ 何日ぶんも一度に流し込まない。</summary>
        public const long CatchUpMax = 12 * 60 * 60;

        /// <summary>経過がこれ以下なら「見ている間」とみなし、本物の手番を回す
        /// （<see cref="LiveStep"/>）。これを超えたら期待値の近似（<see cref="AdvanceApprox"/>）へ
        /// 落とす。⭐ 目安2秒以内（作者の許可 2026-08-28）。
        /// ⚠️ 12時間ぶんを0.5秒刻みで回すと86,400手になるので、それは絶対にしない
        /// ── この定数がその境界。</summary>
        public const double LiveWindowSeconds = 2.0;

        /// <summary>清算した時刻を控える。⭐ **整数秒と端数を必ず対で置く**
        /// ── 片方だけ書くと、次の比較で時間が飛ぶか止まる。</summary>
        private static void Stamp(IdleRun run, double nowUnix)
        {
            run.LastUnix = (long)Math.Floor(nowUnix);
            run.Fine = nowUnix - run.LastUnix;
        }

        public static bool IsDown(IdleRun run, Creature creature, long nowUnix)
        {
            long until;
            return run.DownUntil.TryGetValue(creature.Id, out until) && nowUnix < until;
        }

        /// <summary>復帰の時刻を過ぎている者を、まとめて起こす（全快で戻す）。
        ///
        /// ⭐ **呼び出しの頭で1回だけ**でよい（2026-08-28）。<see cref="ReviveSeconds"/>（3秒）は
        /// <see cref="LiveWindowSeconds"/>（2秒）より長いので、1回の「見ている間」の呼び出しの
        /// **中で**倒れて起きることは起きない ── ダウンは必ず前の呼び出しまでに始まっている。
        /// だから「呼び出しの終わりの時刻で判定する」だけで、途中の刻みごとに見直す必要が無い。
        /// ⚠️ 近似（<see cref="AdvanceApprox"/>）はダウンそのものを起こさないが、**起こす方は
        /// 別**── 何時間も前に倒れていた者を、次に見たときはちゃんと起こしておく。</summary>
        private static void ReviveDue(IdleRun run, IReadOnlyList<Creature> party, long nowUnix)
        {
            foreach (var creature in party)
            {
                long until;
                if (run.DownUntil.TryGetValue(creature.Id, out until) && nowUnix >= until)
                {
                    run.DownUntil.Remove(creature.Id);
                    run.Health[creature.Id] = 1.0;
                }
            }
        }

        // ── 見た目（相手） ────────────────────────────────

        /// <summary>色違いの出る率の分母。⭐ 1/<see cref="ShinyOdds"/>。
        /// ⚠️ 「超低確率」（作者の指示 2026-08-28）── 目安は3.5秒に1体なので、
        /// 256 なら**15分眺めて1体**くらい。数を変えるならここだけ。</summary>
        public const int ShinyOdds = 256;

        /// <summary>相手を引き直す。⭐ **見た目だけ**を決める（硬さも報酬も動かさない）。
        /// ⚠️ 乱数を使う ── 作者の指示で「再現性は考えなくてよい」に変わった
        /// （2026-08-28。それまでは <c>FoeAt</c>/<c>PaletteAt</c> が倒した数から決定論で
        /// 巡らせていた。この2つは役目を終えたので削除した）。
        /// ⚠️ このクラスの他の場所は今も乱数を使わない ── **見た目だけが例外**。
        /// ⚠️ 種族が0なら 0/0 のまま（0 で割らない）。
        /// ⭐ ねらいは「種族を引いてから、その種族の持つ色数の中で色を引く」こと。
        /// 色は既定 0（通常色）。<paramref name="paletteCountOf"/> が2以上を返すときだけ、
        /// <see cref="ShinyOdds"/> 分の1で 1以上を引く。</summary>
        public static void RollFoe(IdleRun run, Rng rng, int speciesCount, Func<int, int> paletteCountOf)
        {
            if (speciesCount <= 0)
            {
                run.FoeSpecies = 0;
                run.FoePalette = 0;
                return;
            }

            run.FoeSpecies = rng.Int(0, speciesCount);

            run.FoePalette = 0;
            int paletteCount = paletteCountOf(run.FoeSpecies);
            if (paletteCount > 1 && rng.Int(0, ShinyOdds) == 0)
            {
                run.FoePalette = rng.Int(1, paletteCount);
            }
        }

        /// <summary>実際の種族表から相手を引き直す。⭐ <see cref="RollFoe"/> を呼ぶ場所を
        /// ここへ集める（初回・周期が終わって次が現れる拍の2か所から同じ形で呼ぶ）。
        /// ⚠️ <see cref="RollFoe"/> 自身は種族表を知らないままにしてある（検査がにせの
        /// 表で呼べるように）── 表を知っているのはここだけ。</summary>
        private static void RollFoeFromTable(IdleRun run, Rng rng) =>
            RollFoe(run, rng, SpeciesTable.All.Count, i => SpeciesTable.All[i].Palettes.Count);

        /// <summary>いま出ている相手の属性。⭐ **唯一の出所**（2026-08-28・仕事2で追加）。
        ///
        /// ⚠️ 種族（<see cref="Species"/>）は属性を持たない ── 属性は個体（<see cref="Creature"/>）
        /// が持つもの（<see cref="Creature.Element"/> の doc 参照）。放置の相手は
        /// <see cref="Creature"/> を作らないので、代わりに種族表が「持っているもの」＝
        /// <see cref="Migrations.ElementOf"/>（旧・種族固定だった頃の割り当て）を借りる。
        /// ⚠️ 古いセーブを読むときと同じ道を通るだけで、新しい判断を増やしてはいない。</summary>
        private static Element FoeElementOf(IdleRun run)
        {
            var all = SpeciesTable.All;
            if (run.FoeSpecies < 0 || run.FoeSpecies >= all.Count) return Element.Fire;
            return Migrations.ElementOf(all[run.FoeSpecies].Id);
        }

        /// <summary>いま出ている相手の、残りの割合（0〜1）。⭐ **帯に出すのはこれ。**
        /// Come/Face（まだ誰も殴っていない）で 1、打ち合いで8段で下がり、
        /// Finish/Rest（殴りきった）で 0。</summary>
        public static double FoeLeft(IdleRun run) =>
            Math.Clamp(1.0 - (double)run.Struck / StrikeCount, 0.0, 1.0);

        // ── 卵 ──────────────────────────────────────────

        /// <summary>この清算で得たもの。
        /// ⭐ 卵は「★ごとの個数」で返す（作るのは呼び側 ── <see cref="Idle"/> は
        /// <see cref="Game"/> を知らないので、卵そのものはここで作らない。
        /// 呼び側は <c>Games.GainIdleEggs</c> を通す）。
        /// ⚠️ `record struct` にしたいところだが、このプロジェクトの C# は 9 に固定してある
        /// （`record struct` は C# 10 から。`EggCommand.Core.csproj` は勝手に上げない）。
        /// ⭐ 素直な readonly struct にしてある ── 値は1つも変わらない。</summary>
        public readonly struct IdleGain
        {
            public readonly int Exp;
            public readonly int Star1;
            public readonly int Star2;
            public readonly int Star3;
            /// <summary>この呼び出しで実際に起きた一撃の並び。⭐ 画面はこれだけを見て演出を出す
            /// （2026-08-28・仕事6 ── 旧 <c>Strikes</c>/<c>FirstStriker</c> の置き換え。
            /// あちらは「順繰りに殴る」前提の数え方で、本物の手番制とは噛み合わない）。
            /// ⚠️ 空なら長さ0の配列。**null は返さない。**</summary>
            public readonly IdleBlow[] Blows;
            /// <summary>この呼び出しで相手が倒れたか（＝周期が1つ以上終わったか）。</summary>
            public readonly bool Finished;

            public IdleGain(int exp, int star1, int star2, int star3,
                IdleBlow[]? blows = null, bool finished = false)
            {
                Exp = exp;
                Star1 = star1;
                Star2 = star2;
                Star3 = star3;
                Blows = blows ?? Array.Empty<IdleBlow>();
                Finished = finished;
            }

            /// <summary>卵の合計。⭐ ★を問わず「何個」だけ知りたいときに。</summary>
            public int Eggs => Star1 + Star2 + Star3;
        }

        /// <summary>1回の一撃。⭐ 画面はこれだけを見て演出を組み立てる（2026-08-28・仕事6）。</summary>
        public readonly struct IdleBlow
        {
            /// <summary>殴った味方の添字（編成の中の位置）。⚠️ 敵の一撃なら -1。</summary>
            public readonly int Who;
            /// <summary>敵に殴られた味方の添字。⚠️ 味方の一撃なら -1。</summary>
            public readonly int Target;
            /// <summary>実際に与えたダメージ。⚠️ 味方の一撃のときだけ意味を持つ（敵の一撃は 0）。</summary>
            public readonly int Damage;
            /// <summary>この一撃で（狙われた味方が）倒れたか。</summary>
            public readonly bool Downed;

            public IdleBlow(int who, int target, int damage, bool downed)
            {
                Who = who;
                Target = target;
                Damage = damage;
                Downed = downed;
            }
        }

        /// <summary>周期が終わるごとに卵が出る率。⭐ 5%（作者の指示 2026-08-28）。</summary>
        public const double EggDropChance = 0.05;

        /// <summary>卵が出たときの★1の取り分。⭐ 75%（作者の指示 2026-08-28）。</summary>
        public const double EggStar1Share = 0.75;
        /// <summary>卵が出たときの★2の取り分。⭐ 20%。残り5%が★3
        /// （<see cref="RollEggStar"/> が唯一の出所 ── 3つ目の数はここに書かない）。</summary>
        public const double EggStar2Share = 0.20;

        /// <summary>1回の清算で得られる卵の上限。
        ///
        /// 🔴 **溜め込みの上限が要る**（2026-08-28）。⚠️ <see cref="Advance"/> は最大
        /// <see cref="CatchUpMax"/>（12時間）ぶんを一度に清算する。
        /// ⭐ 超えたぶんは**捨てる**（抽選そのものはそのまま回す。捨てるのは数え上げの側）。
        /// ⚠️ 毎秒呼ばれている間（画面を眺めている間）は1回の清算がせいぜい1周期ぶんなので、
        /// この上限には当たらない ── 効くのは「久しぶりに開いたとき」だけ。</summary>
        public const int MaxEggsPerCatchUp = 3;

        /// <summary>卵の★を引く。⭐ 75/20/5。</summary>
        private static int RollEggStar(Rng rng)
        {
            double roll = rng.Float();
            if (roll < EggStar1Share) return 1;
            if (roll < EggStar1Share + EggStar2Share) return 2;
            return 3;
        }

        // ── 本物の手番（見ている間） ─────────────────────────

        /// <summary>攻撃ステの計算に渡す、放置専用の「素のまま」の状態。
        /// ⚠️ 放置に状態異常は無いので、既定値（何も乗っていない）を使い回す
        /// （<see cref="Battle.AttackStatOf"/> は読むだけで書き換えないので、使い回して安全）。</summary>
        private static readonly UnitStatus NeutralStatus = new UnitStatus();

        /// <summary>味方1体の、いまの一撃ぶんのダメージ。⭐ **唯一の出所** ── 実際の一撃
        /// （<see cref="PerformAllyStrike"/>）も、12時間追いつきの期待値
        /// （<see cref="ExpectedShares"/>）も、ここを通す。
        ///
        /// ⭐ 形は <see cref="Battle.CounterStrike"/> を借りる（枠1・<c>Damage</c> 効果だけ・
        /// 多段（<c>effect.Repeat</c>）はそのまま出す）。⚠️ ただし
        /// <c>IsAlive</c> の早期終了は無い ── 放置の敵は「HPが尽きたか」ではなく
        /// 「<see cref="StrikeCount"/> 発当たったか」で倒れるので、多段の途中で相手が
        /// 死んで残りが無駄撃ちになる、という戦闘側の配慮がそもそも要らない。
        /// ⚠️ 状態は付けない・防御は常に <see cref="FoeDefense"/>（0）── 呼び出し側の約束のまま。</summary>
        private static int HitDamageOf(Creature attacker, Element foeElement)
        {
            var skill = Creatures.SkillsOf(attacker)[0];
            if (skill == null) return 0;   // ⚠️ 実際には起きない保険（枠1は種族固定で必ず在る）
            var stats = Battle.InnateStatsOf(attacker);
            double mult = Battle.ElementMultiplier(attacker.Element, foeElement);
            int total = 0;
            foreach (var effect in skill.Effects)
            {
                if (effect.Kind != EffectKind.Damage) continue;   // ⚠️ 状態は付けない（殴るだけ）
                int attackStat = Battle.AttackStatOf(stats, NeutralStatus, effect.Scale);
                int hit = Battle.DamageOf(Skills.DamagePowerOf(effect.Power), attackStat,
                    FoeDefense, mult);
                for (int shot = 0; shot < effect.Repeat; shot++) total += hit;
            }
            return total;
        }

        /// <summary>いまの手番で誰が動くかを選ぶ。⭐ 戦闘の <see cref="Battle.NextActor"/> と
        /// 同じ選び方（満ちた者のうちゲージが最大の者・超過は繰り越す）を、放置専用のゲージ
        /// （<see cref="IdleRun.Gauge"/>/<see cref="IdleRun.FoeGauge"/>）に対して行う。
        ///
        /// ⚠️ 戦闘と違うのはここだけ ── **選ぶのに使った刻みの量は、実時間には一切効かない**
        /// （実時間コストは呼び側が <see cref="ActSeconds"/> で定額払いする）。速度は
        /// 「誰が動くか」だけを決め、「どれだけ速く時間が進むか」は決めない、という
        /// クラス doc の要求そのものをここで実現している。
        ///
        /// 戻り値: 0..<c>party.Count-1</c> なら味方の添字、<c>party.Count</c> なら敵、
        /// -1 なら「立っている味方が誰も居ない（全員ダウン中か空編成）ので決められない」。</summary>
        private static int PickActor(IdleRun run, IReadOnlyList<Creature> party, long nowUnix)
        {
            var living = new List<int>();
            for (int i = 0; i < party.Count; i++)
            {
                if (!IsDown(run, party[i], nowUnix)) living.Add(i);
            }
            if (living.Count == 0) return -1;   // ⚠️ 全員ダウン中 ── 誰も殴れない

            int minTicks = Battle.TicksToAct(run.FoeGauge, FoeSpeed);
            foreach (int i in living)
            {
                int gauge = run.Gauge.TryGetValue(party[i].Id, out var g) ? g : 0;
                int spd = Creatures.StatsOf(party[i]).Spd;
                int t = Battle.TicksToAct(gauge, spd);
                if (t < minTicks) minTicks = t;
            }
            if (minTicks < 0) minTicks = 0;

            run.FoeGauge += minTicks * Battle.GaugeRate(FoeSpeed);
            foreach (int i in living)
            {
                int gauge = run.Gauge.TryGetValue(party[i].Id, out var g) ? g : 0;
                int spd = Creatures.StatsOf(party[i]).Spd;
                run.Gauge[party[i].Id] = gauge + minTicks * Battle.GaugeRate(spd);
            }

            // ⭐ 満ちた者のうち最大の者が動く。⚠️ 同着は味方を優先する
            //    （先に見た者を残す `>` の順で、味方を先に見る ── 敵だけが得をしない形）。
            int bestActor = -1;
            int bestGauge = -1;
            foreach (int i in living)
            {
                int gauge = run.Gauge[party[i].Id];
                if (gauge >= Battle.GaugeMax && gauge > bestGauge) { bestGauge = gauge; bestActor = i; }
            }
            if (run.FoeGauge >= Battle.GaugeMax && run.FoeGauge > bestGauge) bestActor = party.Count;

            // ⭐ 動いた者からゲージを引く（繰り越す） ── 戦闘と同じ流儀。
            if (bestActor == party.Count) run.FoeGauge -= Battle.GaugeMax;
            else if (bestActor >= 0) run.Gauge[party[bestActor].Id] -= Battle.GaugeMax;

            return bestActor;
        }

        /// <summary>味方1体の手番。⭐ ダメージを<see cref="IdleRun.Damage"/>へ足し、
        /// <see cref="IdleRun.Struck"/>を+1する（多段でもここは常に+1 ── クラス doc 参照）。</summary>
        private static void PerformAllyStrike(IdleRun run, IReadOnlyList<Creature> party, int index,
            List<IdleBlow> blows)
        {
            int dmg = HitDamageOf(party[index], FoeElementOf(run));
            run.Damage += dmg;
            run.Struck++;
            blows.Add(new IdleBlow(index, -1, dmg, false));
        }

        /// <summary>敵の手番。⭐ 立っている味方から乱数で1人選んで殴る（作者の指示・仕事3）。
        /// ⚠️ <paramref name="rng"/> が無ければ狙いを引けないので何も起きない
        /// （見た目・卵と同じ「省略できる」逃げ道 ── クラス doc の乱数の使い道を参照）。</summary>
        private static void PerformFoeStrike(IdleRun run, IReadOnlyList<Creature> party, Rng? rng,
            long nowUnix, List<IdleBlow> blows)
        {
            if (rng == null) return;

            var living = new List<int>();
            for (int i = 0; i < party.Count; i++)
            {
                if (!IsDown(run, party[i], nowUnix)) living.Add(i);
            }
            if (living.Count == 0) return;   // ⚠️ 全員ダウン中 ── 打つ相手が居ない

            int target = living[rng.Int(0, living.Count)];
            var creature = party[target];
            double health = run.Health.TryGetValue(creature.Id, out var h) ? h : 1.0;
            health -= FoeDamagePercent / 100.0;
            bool downed = health <= 0.0;
            if (downed)
            {
                run.DownUntil[creature.Id] = nowUnix + ReviveSeconds;
                health = 0.0;
            }
            run.Health[creature.Id] = health;
            blows.Add(new IdleBlow(-1, target, 0, downed));
        }

        /// <summary>1手ぶんを解決する。⭐ <see cref="PickActor"/>で選び、味方か敵かで実行を振り分ける。</summary>
        private static void ResolveFightTick(IdleRun run, IReadOnlyList<Creature> party, Rng? rng,
            long nowUnix, List<IdleBlow> blows)
        {
            int actor = PickActor(run, party, nowUnix);
            if (actor < 0) return;                              // 誰も動けない
            if (actor == party.Count) PerformFoeStrike(run, party, rng, nowUnix, blows);
            else PerformAllyStrike(run, party, actor, blows);
        }

        /// <summary>「見ている間」（<see cref="LiveWindowSeconds"/> 以内）の本物の手番を回す。
        ///
        /// ⭐ Come/Face/Finish/Rest は普通の秒読み。Fight だけは <see cref="ActSeconds"/> 刻みで
        /// <see cref="ResolveFightTick"/> を呼び、<see cref="IdleRun.Struck"/> が
        /// <see cref="StrikeCount"/> に届いたら即 Finish へ移る（＝そこで EXP・卵を確定させる。
        /// 「倒した拍に確定させる」という仕事4の要求そのもの）。
        /// ⚠️ <paramref name="last"/> は呼び出し前の実数時刻 ── ここから
        /// <paramref name="elapsed"/> だけ進める。ダウンの時刻はここで作る「いまどのくらい
        /// 進んだか」（<c>last + consumed</c>）から整数秒に丸めて使う。</summary>
        private static List<IdleBlow> LiveStep(IdleRun run, IReadOnlyList<Creature> party, double last,
            double elapsed, Rng? rng, out bool finished, out int star1, out int star2, out int star3)
        {
            var blows = new List<IdleBlow>();
            finished = false;
            star1 = 0; star2 = 0; star3 = 0;
            double consumed = 0.0;
            int guard = 0;
            while (elapsed - consumed > 1e-9 && guard++ < 4000)
            {
                double remain = elapsed - consumed;

                if (run.Phase == IdlePhase.Fight)
                {
                    double step = Math.Min(remain, run.PhaseLeft);
                    run.PhaseLeft -= step;
                    consumed += step;
                    if (run.PhaseLeft > 1e-9) continue;   // ⚠️ 次のtickにまだ届いていない（端数は次回へ）

                    long tickNow = (long)Math.Floor(last + consumed);
                    ResolveFightTick(run, party, rng, tickNow, blows);
                    run.PhaseLeft += ActSeconds;   // ⭐ 繰り越し（GaugeMax の超過繰り越しと同じ流儀）

                    if (run.Struck >= StrikeCount)
                    {
                        run.Defeated++;
                        finished = true;

                        // ⭐ 倒した拍で稼ぎを確定させる（仕事4）。
                        run.ExpCarry += (double)run.Damage / ExpPerDamage;
                        int gained = (int)Math.Floor(run.ExpCarry);
                        if (gained < 0) gained = 0;
                        run.ExpCarry -= gained;
                        run.Exp += gained;

                        if (rng != null && rng.Chance(EggDropChance) &&
                            star1 + star2 + star3 < MaxEggsPerCatchUp)
                        {
                            switch (RollEggStar(rng))
                            {
                                case 1: star1++; break;
                                case 2: star2++; break;
                                default: star3++; break;
                            }
                        }

                        run.Phase = IdlePhase.Finish;
                        run.PhaseLeft = FinishSeconds;
                    }
                    continue;
                }

                // ── Come/Face/Finish/Rest: 普通の秒読み ──────────────
                double stepPhase = Math.Min(remain, run.PhaseLeft);
                run.PhaseLeft -= stepPhase;
                consumed += stepPhase;
                if (run.PhaseLeft > 1e-9) continue;

                switch (run.Phase)
                {
                    case IdlePhase.Come:
                        run.Phase = IdlePhase.Face;
                        run.PhaseLeft = FaceSeconds;
                        break;
                    case IdlePhase.Face:
                        // ⭐ Fight の頭でゲージ・当たり・ダメージを0へ戻す ── 新しい相手との
                        //    新しい打ち合いが始まる（戦闘が1戦ごとに新しい BattleState を
                        //    作るのと同じ約束）。
                        run.Phase = IdlePhase.Fight;
                        run.PhaseLeft = ActSeconds;
                        run.Struck = 0;
                        run.Damage = 0;
                        run.Gauge.Clear();
                        run.FoeGauge = 0;
                        break;
                    case IdlePhase.Finish:
                        run.Phase = IdlePhase.Rest;
                        run.PhaseLeft = RestSeconds;
                        break;
                    default:   // Rest → 次の相手が現れる
                        run.Phase = IdlePhase.Come;
                        run.PhaseLeft = ComeSeconds;
                        if (rng != null) RollFoeFromTable(run, rng);
                        break;
                }
            }
            return blows;
        }

        // ── 期待値の近似（見ていない間の追いつき） ─────────────────

        /// <summary>「取り分」と「秒あたりの見込みダメージ」をまとめて出す。
        /// ⭐ <see cref="ExpectedDamagePerSecond"/> と <see cref="ExpectedFightSeconds"/> の
        /// 共通の下ごしらえ（同じ計算を2か所に書かない）。
        /// ⚠️ ダウンは無視する（作者の許可・仕事5） ── 全員が立っている前提で数える。
        /// ⚠️ 相手の属性はいま出ている個体のもの（<see cref="FoeElementOf"/>）で固定する ──
        /// 追いつく間に何体倒すかは分からないが、直近の1体で代表させる近似。</summary>
        private static (double allyShareSum, double damagePerTurn) ExpectedShares(
            IdleRun run, IReadOnlyList<Creature> party)
        {
            Element foeElement = FoeElementOf(run);
            double totalRate = Battle.GaugeRate(FoeSpeed);
            var rate = new double[party.Count];
            var hit = new double[party.Count];
            for (int i = 0; i < party.Count; i++)
            {
                int spd = Creatures.StatsOf(party[i]).Spd;
                rate[i] = Battle.GaugeRate(spd);
                totalRate += rate[i];
                hit[i] = HitDamageOf(party[i], foeElement);
            }

            double allyShareSum = 0.0, damagePerTurn = 0.0;
            if (totalRate > 1e-9)
            {
                for (int i = 0; i < party.Count; i++)
                {
                    double share = rate[i] / totalRate;
                    allyShareSum += share;
                    damagePerTurn += share * hit[i];
                }
            }
            return (allyShareSum, damagePerTurn);
        }

        /// <summary>期待ダメージ／秒。⭐ 12時間などの追いつき（<see cref="AdvanceApprox"/>）の
        /// 稼ぎの唯一の入口。「取り分 × 一撃」を全味方で足し、<see cref="ActSeconds"/> で
        /// 割って秒あたりに直す（仕事5の式そのもの）。</summary>
        public static double ExpectedDamagePerSecond(IdleRun run, IReadOnlyList<Creature> party)
        {
            var (_, damagePerTurn) = ExpectedShares(run, party);
            return damagePerTurn / ActSeconds;
        }

        /// <summary>期待 Fight 秒。⭐ 「8発に届くまでの期待手番数 × <see cref="ActSeconds"/>」。
        /// ⚠️ 誰も殴れない編成（力0・空編成）では無限大を返す ── 絶対に8発へ届かないため。</summary>
        public static double ExpectedFightSeconds(IdleRun run, IReadOnlyList<Creature> party)
        {
            var (allyShareSum, _) = ExpectedShares(run, party);
            return allyShareSum > 1e-9
                ? (StrikeCount / allyShareSum) * ActSeconds
                : double.PositiveInfinity;
        }

        /// <summary>いまの拍と Struck から、周期の中の位置を出す。⭐ <see cref="LandApprox"/> の逆変換。
        /// ⚠️ Fight の中の位置は、実際の経過ではなく <see cref="IdleRun.Struck"/> から
        /// **期待値で**復元する（<paramref name="fightSeconds"/> は「いまの編成ならこのくらい」
        /// という見積もり）。この割り切りのおかげで、<see cref="IdleRun.PhaseLeft"/> を
        /// Fight の間だけ「次の手番までの残り」という<see cref="LiveStep"/>専用の意味で
        /// 使っていても、この関数とは衝突しない（Fight では PhaseLeft を一切読まない）。</summary>
        private static double OffsetOf(IdleRun run, double fightSeconds)
        {
            switch (run.Phase)
            {
                case IdlePhase.Come: return ComeSeconds - run.PhaseLeft;
                case IdlePhase.Face: return ComeSeconds + FaceSeconds - run.PhaseLeft;
                case IdlePhase.Fight:
                    // ⚠️ `0 * Infinity` は NaN になる（力0の編成が fightSeconds に無限大を
                    //    渡してくる）── Struck が0ならそもそも進んでいないので、掛ける前に弾く。
                    if (run.Struck <= 0) return ComeSeconds + FaceSeconds;
                    return ComeSeconds + FaceSeconds + run.Struck * (fightSeconds / StrikeCount);
                case IdlePhase.Finish:
                    return ComeSeconds + FaceSeconds + fightSeconds + FinishSeconds - run.PhaseLeft;
                default:   // Rest
                    return ComeSeconds + FaceSeconds + fightSeconds + FinishSeconds + RestSeconds
                        - run.PhaseLeft;
            }
        }

        /// <summary>周期の中の位置から、拍（Phase/PhaseLeft/Struck）を組み立てて書く。
        /// ⭐ <see cref="OffsetOf"/> の逆変換。近似（<see cref="AdvanceApprox"/>）専用。
        /// ⚠️ Fight に着地したときの Struck は**期待値の目安**でしかない（実際に何発
        /// 当たったかは追っていない ── ダウンを無視するのと同じ近似の一部）。</summary>
        private static void LandApprox(IdleRun run, double within, double fightSeconds)
        {
            double fightStart = ComeSeconds + FaceSeconds;
            double fightEnd = fightStart + fightSeconds;   // fightSeconds が無限大なら常に届かない
            if (within < ComeSeconds)
            {
                run.Phase = IdlePhase.Come;
                run.PhaseLeft = ComeSeconds - within;
                run.Struck = 0;
            }
            else if (within < fightStart)
            {
                run.Phase = IdlePhase.Face;
                run.PhaseLeft = fightStart - within;
                run.Struck = 0;
            }
            else if (within < fightEnd)
            {
                run.Phase = IdlePhase.Fight;
                run.PhaseLeft = ActSeconds;
                double perStrike = fightSeconds / StrikeCount;   // 無限大なら n は 0 に落ちる
                int n = (int)((within - fightStart) / perStrike);
                run.Struck = Math.Clamp(n, 0, StrikeCount - 1);
            }
            else if (within < fightEnd + FinishSeconds)
            {
                run.Phase = IdlePhase.Finish;
                run.PhaseLeft = fightEnd + FinishSeconds - within;
                run.Struck = StrikeCount;
            }
            else
            {
                run.Phase = IdlePhase.Rest;
                run.PhaseLeft = fightEnd + FinishSeconds + RestSeconds - within;
                run.Struck = StrikeCount;
            }
        }

        /// <summary>見ていない間（<see cref="LiveWindowSeconds"/> 超）の追いつき。
        ///
        /// 🔴 **近似でよい**（作者の許可・仕事5）。⚠️ 12時間ぶんを0.5秒刻みで回すと
        /// 86,400手になるので、本物の手番は回さない。代わりに：
        /// - 稼ぎは <see cref="ExpectedDamagePerSecond"/> × 経過秒 を積むだけ。
        /// - 拍は「期待周期」（<see cref="ExpectedFightSeconds"/> を挟んだ周期）の余りへ丸める。
        /// - ダウンは無視する（起こす方＝<see cref="ReviveDue"/> は呼び出しの頭で別にやる）。
        /// - 卵は「1周期ごとに5%」のまま。周期が可変になったので、期待周期で割る。</summary>
        private static IdleGain AdvanceApprox(IdleRun run, IReadOnlyList<Creature> party, double elapsed,
            Rng? rng)
        {
            double damagePerSecond = ExpectedDamagePerSecond(run, party);
            double fightSeconds = ExpectedFightSeconds(run, party);
            double cycle = ComeSeconds + FaceSeconds + fightSeconds + FinishSeconds + RestSeconds;

            run.ExpCarry += damagePerSecond * elapsed / ExpPerDamage;
            int gained = (int)Math.Floor(run.ExpCarry);
            if (gained < 0) gained = 0;
            run.ExpCarry -= gained;
            run.Exp += gained;

            long kills;
            double landed;
            double offset = OffsetOf(run, fightSeconds);
            double total = offset + elapsed;
            if (double.IsInfinity(cycle))
            {
                // ⚠️ 力0（空編成含む）── 8発に絶対届かないので、Come/Face だけ進んで
                //    Fight に入ったらそこで止まる（`OffsetOf`/`LandApprox` が面倒を見る）。
                kills = 0;
                landed = total;
            }
            else
            {
                kills = (long)(total / cycle);
                landed = total - kills * cycle;
                if (landed >= cycle) { landed -= cycle; kills++; }
                if (landed < 0.0) landed = 0.0;
            }

            int star1 = 0, star2 = 0, star3 = 0;
            for (long i = 0; i < kills; i++)
            {
                run.Defeated++;
                if (rng != null && rng.Chance(EggDropChance) &&
                    star1 + star2 + star3 < MaxEggsPerCatchUp)
                {
                    switch (RollEggStar(rng))
                    {
                        case 1: star1++; break;
                        case 2: star2++; break;
                        default: star3++; break;
                    }
                }
            }

            if (kills > 0 && rng != null) RollFoeFromTable(run, rng);
            LandApprox(run, landed, fightSeconds);

            // ⚠️ 近似の区間はゲージ・累計ダメージを持ち越さない ── 途中から本物の手番を
            //    再開するとき、半端な位置から始めるより0から始めるほうが安全
            //    （「近似でよい」という許可の範囲内の割り切り）。
            run.Gauge.Clear();
            run.FoeGauge = 0;
            run.Damage = 0;

            return new IdleGain(gained, star1, star2, star3, Array.Empty<IdleBlow>(), kills > 0);
        }

        // ── 進める ────────────────────────────────────────

        /// <summary>経過ぶんを進める。⭐ 唯一の出所。画面はここが返した数を描くだけ。
        ///
        /// ⚠️ <paramref name="rng"/> は省略できる（既定 null）。省略すると、相手の見た目の
        /// 引き直し・卵の抽選・**敵の狙い先**（2026-08-28・仕事3で増えた）を行わない。
        /// ⭐ 遊びの中からは必ず渡すこと（<c>Game.RngIdle</c>）。
        ///
        /// ⚠️ <paramref name="nowUnix"/> は**実数**。拍は0.5秒刻みなので、整数秒で渡されると
        /// 1秒ぶんまとめて進み、打撃が同じ瞬間にまとまって出る（実測して直した）。</summary>
        /// <returns>この清算で増えた EXP と、落ちた卵（★ごとの個数）、起きた一撃の並び。</returns>
        public static IdleGain Advance(IdleRun run, IReadOnlyList<Creature> party, double nowUnix,
            Rng? rng = null)
        {
            if (run.LastUnix <= 0)
            {
                Stamp(run, nowUnix);
                run.Phase = IdlePhase.Come;
                run.PhaseLeft = ComeSeconds;
                run.Struck = 0;
                run.Damage = 0;
                run.Gauge.Clear();
                run.FoeGauge = 0;
                if (rng != null) RollFoeFromTable(run, rng);
                return new IdleGain(0, 0, 0, 0);
            }

            double last = run.LastUnix + run.Fine;
            if (nowUnix <= last)
            {
                Stamp(run, nowUnix);
                return new IdleGain(0, 0, 0, 0);
            }

            double elapsed = nowUnix - last;
            if (elapsed > CatchUpMax) elapsed = CatchUpMax;
            Stamp(run, nowUnix);

            // ⭐ まず、時間切れで起きられる者を起こす（生死の判定は整数秒でよい・既存の約束）
            ReviveDue(run, party, (long)Math.Floor(nowUnix));

            if (elapsed <= LiveWindowSeconds)
            {
                int expBefore = run.Exp;
                var blows = LiveStep(run, party, last, elapsed, rng, out bool finished,
                    out int star1, out int star2, out int star3);
                int gained = run.Exp - expBefore;
                return new IdleGain(gained, star1, star2, star3, blows.ToArray(), finished);
            }

            return AdvanceApprox(run, party, elapsed, rng);
        }

        /// <summary>EXP で Lv を1つ上げる。⭐ **1回で1レベル**。
        /// ⚠️ 一気に上限まで入れると、上げ止めどころを選べない。
        /// どこで止めるかは持ち主が決める。
        /// ⚠️ 値段は個体の**いまの Lv** で決まる（<see cref="Levels.ExpToNext"/>）。</summary>
        /// <returns>上がったなら 1、EXP か上限が足りなければ 0。</returns>
        public static int Spend(IdleRun run, Creature creature)
        {
            if (creature.Earned >= Levels.GrowMax) return 0;
            // ⚠️ 値段はその個体の**いまの Lv**（素質の合計 ＋ 育てた分）で決まる
            int cost = Levels.ExpToNext(creature);
            if (cost <= 0 || run.Exp < cost) return 0;

            int gained = Creatures.Grow(creature, 1);
            if (gained <= 0) return 0;
            run.Exp -= cost;
            return gained;
        }
    }
}
