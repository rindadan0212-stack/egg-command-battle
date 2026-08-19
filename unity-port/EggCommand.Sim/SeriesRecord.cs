#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using EggCommand.Core;

namespace EggCommand.Sim
{
    /// <summary>**現行の記録**を書き出す。⚠️ ゴールデン（移植の証拠）とは別物。
    ///
    /// ゴールデンは TS を走らせた出力で、**移植が正しいことの証拠**なので作り直さない。
    /// 一方これは「いまの実装が、いまと同じ個体・同じ試合を出し続けているか」を見るための記録。
    /// ⭐ 素質を4本から6本にした日（2026-08-18）に、乱数の消費が変わって
    /// 卵・配合・進行・試合の系列がゴールデンと別になった。そこで空いた穴を埋めるのがここ。
    ///
    /// ⚠️ **意図して遊びを変えたときだけ**作り直す（`dotnet run --project EggCommand.Sim -- record`）。
    /// 作り直したら、何を変えたから作り直したのかを 仕様変更履歴.md に書く。
    /// ⚠️ 落ちたのに理由が言えないなら、それは事故なので作り直してはいけない。</summary>
    public static class SeriesRecord
    {
        /// <summary>⭐ 記録の中身を作る側。テストは同じ手順を踏んで突き合わせる。
        /// ⚠️ こことテストで手順がずれたら何も検査していないのと同じなので、
        /// **手順そのもの**をこのクラスに置いて両方から呼ぶ。</summary>
        public static string Build()
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"note\": \"現行の記録。移植の証拠ではない。sim record で作り直す\",\n");

            sb.Append("  \"defenders\": [\n");
            AppendList(sb, DefenderRows());
            sb.Append("  ],\n");

            sb.Append("  \"eggs\": [\n");
            AppendList(sb, EggRows());
            sb.Append("  ],\n");

            sb.Append("  \"games\": [\n");
            AppendList(sb, GameRows());
            sb.Append("  ],\n");

            sb.Append("  \"breeds\": [\n");
            AppendList(sb, BreedRows());
            sb.Append("  ],\n");

            sb.Append("  \"battles\": [\n");
            AppendList(sb, BattleRows());
            sb.Append("  ]\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        public static string Write(string path)
        {
            string full = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, Build(), new UTF8Encoding(false));
            return full;
        }

        // ── 手順（テストと共有する） ───────────────────────

        /// <summary>⚠️ ゴールデンと同じ種・同じ系統名を使う。
        /// ⭐ 同じ入口で比べておくと、系列が「変わった」のか「呼び方が変わった」のかを混同しない。</summary>
        public static readonly string[] NestIds =
            { "shallow-scale", "thicket-fang", "cliff-plume", "deep-scale", "peak-fang" };

        public static readonly int[] GameSeeds = { 1, 20260816 };

        /// ⚠️ **ゴールデンの12対戦と同じ数だけ持つ。**9件しか無かった頃は、
        /// (20260816 × thicket-fang / cliff-plume / peak-fang) の3件が
        /// **ゴールデンにも記録にも無い**状態だった ── ゴールデン側はその3件を
        /// 属性違いとして飛ばすので、どちらからも見られていなかった。
        public static readonly (int Seed, string Name)[] Matchups =
        {
            (1, "shallow-scale"), (1, "thicket-fang"), (1, "cliff-plume"),
            (1, "deep-scale"), (1, "peak-fang"), (1, "boss"),
            (20260816, "shallow-scale"), (20260816, "thicket-fang"), (20260816, "cliff-plume"),
            (20260816, "deep-scale"), (20260816, "peak-fang"), (20260816, "boss"),
        };

        public static List<string> DefenderRows()
        {
            var rows = new List<string>();
            foreach (var nestId in NestIds)
            {
                var nest = Nests.ById(nestId);
                var units = Nests.MakeDefenders(new Rng(777).Stream(nestId), nest);
                foreach (var unit in units)
                {
                    rows.Add($"{{\"nest\":\"{nestId}\",\"id\":\"{unit.Id}\",\"species\":\"{unit.SpeciesId}\","
                        + $"\"wild\":{Block(unit.Wild)},\"skills\":{Skills23(unit.Skill2, unit.Skill3)}}}");
                }
            }
            return rows;
        }

        public static List<string> EggRows()
        {
            var rows = new List<string>();
            foreach (var nestId in NestIds)
            {
                foreach (var how in new[] { EggOrigin.Stolen, EggOrigin.Defeated })
                {
                    var rng = new Rng(4242).Stream(nestId + how);
                    var egg = Nests.MakeEgg(rng, Nests.ById(nestId), how, 7);
                    var hatched = Nests.Hatch(rng, egg, "c007");
                    rows.Add($"{{\"nest\":\"{nestId}\",\"how\":\"{how}\",\"wild\":{Block(egg.Wild)},"
                        + $"\"mutationCounter\":{egg.MutationCounter},\"generation\":{egg.Generation},"
                        + $"\"skills\":{Skills23(hatched.Skill2, hatched.Skill3)},"
                        + $"\"trait\":{Text(hatched.TraitId)}}}");
                }
            }
            return rows;
        }

        public static List<string> GameRows()
        {
            var rows = new List<string>();
            foreach (int seed in GameSeeds)
            {
                var game = Games.NewGame(seed);
                foreach (var c in game.Storage.Creatures)
                {
                    rows.Add($"{{\"seed\":{seed},\"id\":\"{c.Id}\",\"species\":\"{c.SpeciesId}\","
                        + $"\"wild\":{Block(c.Wild)},\"skills\":{Skills23(c.Skill2, c.Skill3)},"
                        + $"\"strong\":{Text(c.Strong?.ToString())},\"weak\":{Text(c.Weak?.ToString())},"
                        + $"\"element\":\"{c.Element}\",\"trait\":{Text(c.TraitId)}}}");
                }
            }
            return rows;
        }

        public static List<string> BreedRows()
        {
            var rows = new List<string>();
            var game = Games.NewGame(20260816);
            var pool = new List<Creature>(game.Storage.Creatures);
            foreach (int seed in new[] { 7, 1035, 20260816 })
            {
                var outcome = Breeding.Breed(new Rng(seed).Stream("breed"), pool[0], pool[1], 100);
                rows.Add($"{{\"seed\":{seed},\"mutations\":{outcome.Mutations},"
                    + $"\"species\":\"{outcome.Egg.SpeciesId}\",\"wild\":{Block(outcome.Egg.Wild)},"
                    + $"\"mutationCounter\":{outcome.Egg.MutationCounter},"
                    + $"\"palette\":{outcome.Egg.PaletteIndex},\"generation\":{outcome.Egg.Generation},"
                    + $"\"skills\":{Skills23(outcome.Egg.Skill2, outcome.Egg.Skill3)}}}");
            }
            return rows;
        }

        public static List<string> BattleRows()
        {
            var rows = new List<string>();
            foreach (var (seed, name) in Matchups)
            {
                var state = Replay(seed, name);
                var hp = new List<string>();
                foreach (var unit in state.Units) hp.Add(unit.Hp.ToString(CultureInfo.InvariantCulture));
                rows.Add($"{{\"seed\":{seed},\"name\":\"{name}\",\"outcome\":\"{state.Result}\","
                    + $"\"actions\":{state.Actions},\"logLength\":{state.Log.Count},"
                    + $"\"finalHp\":[{string.Join(",", hp)}],\"digest\":\"{Digest(state)}\"}}");
            }
            return rows;
        }

        /// <summary>⚠️ 戦闘に乱数は無いので、同じ編成からは必ず同じ試合になる。
        /// ⭐ ここが記録と食い違ったら、戦闘か AI か個体のどれかが動いている。</summary>
        public static BattleState Replay(int seed, string name)
        {
            var game = Games.NewGame(seed);
            var allies = Games.PartyOf(game);
            var enemies = name == "boss"
                ? Nests.MakeBossParty()
                : Nests.MakeDefenders(new Rng(555).Stream(name), Nests.ById(name));

            var state = Battle.CreateBattle(allies, enemies);
            int guard = 0;
            while (state.Result == null && guard++ < Battle.MaxActions * 3)
            {
                var actor = Battle.NextActor(state);
                if (actor == null) break;
                Battle.PerformAction(state, actor, Ai.ChooseAction(state, actor));
            }
            return state;
        }

        /// <summary>出来事の列を1つの短い文字列へ畳む。
        /// ⭐ 1手でも変わればここが変わる。⚠️ どこが変わったかは出ないので、
        /// 行動数・出来事の数・最終HP を横に並べて置いている（当たりを付ける手掛かり）。</summary>
        public static string Digest(BattleState state)
        {
            // FNV-1a 64bit。⚠️ 暗号用途ではない（取り違えを見つけるためだけ）
            ulong hash = 14695981039346656037UL;
            foreach (var e in state.Log)
            {
                string line = $"{e.Kind}|{e.Unit}|{e.Label}|{e.Amount}|{e.Hp}|{e.Absorbed}"
                    + $"|{e.Stat}|{e.Percent}|{e.Turns}|{e.Delta}|{e.Hits}\n";
                foreach (char ch in line)
                {
                    hash ^= ch;
                    hash *= 1099511628211UL;
                }
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        // ── 書き出しの小物 ────────────────────────────────

        private static void AppendList(StringBuilder sb, List<string> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                sb.Append("    ").Append(rows[i]);
                sb.Append(i == rows.Count - 1 ? "\n" : ",\n");
            }
        }

        private static string Block(StatBlock b) =>
            $"[{b.Hp},{b.Atk},{b.Def},{b.Spd},{b.Acc},{b.Res}]";

        private static string Skills23(string? a, string? b) => $"[{Text(a)},{Text(b)}]";

        private static string Text(string? s) => s == null ? "null" : $"\"{s}\"";
    }
}
