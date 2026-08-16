using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using EggCommand.Core;

namespace EggCommand.Tests;

/// <summary>TS を実際に走らせて書き出した「正解」を読む。
///
/// ⭐ 移植の正しさを目視で決めない。ここが落ちたら移植の失敗であって、
/// golden を書き換えて合わせてはいけない（`node scripts/goldens.mjs` が唯一の出所）。
/// </summary>
internal static class Golden
{
    private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "goldens");

    public static JsonElement Load(string name)
    {
        string path = Path.Combine(Dir, name + ".json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"golden が無い: {path}\n  先に `node scripts/goldens.mjs` を走らせる", path);
        }
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.Clone();
    }

    // ── TS の文字列 → C# の列挙 ───────────────────────
    // ⚠️ ここが移植の境界。TS が増えた語を C# が知らなければ、黙って通さず投げる。

    public static StatKey StatKey(string s) => s switch
    {
        "hp" => Core.StatKey.Hp,
        "atk" => Core.StatKey.Atk,
        "def" => Core.StatKey.Def,
        "spd" => Core.StatKey.Spd,
        _ => throw new ArgumentException($"知らないステ: {s}"),
    };

    public static Element Element(string s) => s switch
    {
        "fang" => Core.Element.Fang,
        "plume" => Core.Element.Plume,
        "scale" => Core.Element.Scale,
        _ => throw new ArgumentException($"知らない属性: {s}"),
    };

    public static Target Target(string s) => s switch
    {
        "enemyOne" => Core.Target.EnemyOne,
        "enemyAll" => Core.Target.EnemyAll,
        "allyLowest" => Core.Target.AllyLowest,
        "self" => Core.Target.Self,
        _ => throw new ArgumentException($"知らない対象: {s}"),
    };

    public static PowerTier PowerTier(string s) => s switch
    {
        "小" => Core.PowerTier.Small,
        "中" => Core.PowerTier.Medium,
        "大" => Core.PowerTier.Large,
        "特大" => Core.PowerTier.Huge,
        _ => throw new ArgumentException($"知らない段位: {s}"),
    };

    public static EffectKind EffectKind(string s) => s switch
    {
        "damage" => Core.EffectKind.Damage,
        "buff" => Core.EffectKind.Buff,
        "poison" => Core.EffectKind.Poison,
        "regen" => Core.EffectKind.Regen,
        "healRatio" => Core.EffectKind.HealRatio,
        "shield" => Core.EffectKind.Shield,
        "stun" => Core.EffectKind.Stun,
        "ct" => Core.EffectKind.Ct,
        "taunt" => Core.EffectKind.Taunt,
        "guts" => Core.EffectKind.Guts,
        "immune" => Core.EffectKind.Immune,
        _ => throw new ArgumentException($"知らない効果: {s}"),
    };

    public static DamageScale DamageScale(string s) => s switch
    {
        "atk" => Core.DamageScale.Atk,
        "def" => Core.DamageScale.Def,
        _ => throw new ArgumentException($"知らないスケール元: {s}"),
    };

    public static EggOrigin Origin(string s) => s switch
    {
        "defeated" => EggOrigin.Defeated,
        "stolen" => EggOrigin.Stolen,
        "bred" => EggOrigin.Bred,
        _ => throw new ArgumentException($"知らない入手経路: {s}"),
    };

    public static Outcome Result(string s) => s switch
    {
        "ally" => Core.Outcome.Ally,
        "enemy" => Core.Outcome.Enemy,
        "draw" => Core.Outcome.Draw,
        _ => throw new ArgumentException($"知らない決着: {s}"),
    };

    public static FieldSide Side(string s) => s switch
    {
        "left" => FieldSide.Left,
        "right" => FieldSide.Right,
        _ => throw new ArgumentException($"知らない寄り: {s}"),
    };

    public static StealOutcome Steal(string s) => s switch
    {
        "success" => StealOutcome.Success,
        "blocked" => StealOutcome.Blocked,
        "stalled" => StealOutcome.Stalled,
        _ => throw new ArgumentException($"知らない発射結果: {s}"),
    };

    public static BattleEventKind Event(string s) => s switch
    {
        "act" => BattleEventKind.Act,
        "damage" => BattleEventKind.Damage,
        "heal" => BattleEventKind.Heal,
        "buff" => BattleEventKind.Buff,
        "poison" => BattleEventKind.Poison,
        "regen" => BattleEventKind.Regen,
        "applied" => BattleEventKind.Applied,
        "shield" => BattleEventKind.Shield,
        "stun" => BattleEventKind.Stun,
        "skipped" => BattleEventKind.Skipped,
        "ct" => BattleEventKind.Ct,
        "taunt" => BattleEventKind.Taunt,
        "guts" => BattleEventKind.Guts,
        "gutsSaved" => BattleEventKind.GutsSaved,
        "immune" => BattleEventKind.Immune,
        "blocked" => BattleEventKind.Blocked,
        "down" => BattleEventKind.Down,
        _ => throw new ArgumentException($"知らない出来事: {s}"),
    };

    /// <summary>戦闘の出来事を1つ突き合わせる。⚠️ 種類ごとに見る項目が違う。</summary>
    public static void SameEvent(JsonElement expected, BattleEvent actual, string where)
    {
        var kind = Event(expected.GetProperty("kind").GetString()!);
        Assert.True(kind == actual.Kind, $"{where}: 種類が {actual.Kind}（期待 {kind}）");

        // act だけ対象の項目名が actor
        string unitKey = expected.TryGetProperty("actor", out var actor)
            ? actor.GetString()!
            : expected.GetProperty("unit").GetString()!;
        Assert.True(unitKey == actual.Unit, $"{where}: 対象が {actual.Unit}（期待 {unitKey}）");

        if (expected.TryGetProperty("skill", out var skill))
            Assert.True(skill.GetString() == actual.Label, $"{where}: 技が {actual.Label}");
        if (expected.TryGetProperty("label", out var label))
            Assert.True(label.GetString() == actual.Label, $"{where}: 札が {actual.Label}");
        if (expected.TryGetProperty("amount", out var amount))
            Assert.True(amount.GetInt32() == actual.Amount, $"{where}: 量が {actual.Amount}（期待 {amount.GetInt32()}）");
        if (expected.TryGetProperty("hp", out var hp))
            Assert.True(hp.GetInt32() == actual.Hp, $"{where}: HP が {actual.Hp}（期待 {hp.GetInt32()}）");
        if (expected.TryGetProperty("absorbed", out var absorbed))
            Assert.True(absorbed.GetInt32() == actual.Absorbed, $"{where}: 吸収が {actual.Absorbed}");
        if (expected.TryGetProperty("stat", out var stat))
            Assert.True(StatKey(stat.GetString()!) == actual.Stat, $"{where}: ステが {actual.Stat}");
        if (expected.TryGetProperty("percent", out var percent))
            Assert.True(percent.GetInt32() == actual.Percent, $"{where}: %が {actual.Percent}");
        if (expected.TryGetProperty("turns", out var turns))
            Assert.True(turns.GetInt32() == actual.Turns, $"{where}: 残りが {actual.Turns}");
        if (expected.TryGetProperty("delta", out var delta))
            Assert.True(delta.GetInt32() == actual.Delta, $"{where}: 増減が {actual.Delta}");
        if (expected.TryGetProperty("hits", out var hits))
            Assert.True(hits.GetInt32() == actual.Hits, $"{where}: 回数が {actual.Hits}");
    }

    /// <summary>スキル2・3 の並び。JSON は [a, b]（null あり）。</summary>
    public static void SameSkills23(JsonElement array, string? skill2, string? skill3, string where)
    {
        string? a = array[0].ValueKind == JsonValueKind.Null ? null : array[0].GetString();
        string? b = array[1].ValueKind == JsonValueKind.Null ? null : array[1].GetString();
        Assert.True(a == skill2, $"{where}: 枠2 が {skill2}（期待 {a}）");
        Assert.True(b == skill3, $"{where}: 枠3 が {skill3}（期待 {b}）");
    }

    public static StatBlock Block(JsonElement e) => new(
        e.GetProperty("hp").GetInt32(),
        e.GetProperty("atk").GetInt32(),
        e.GetProperty("def").GetInt32(),
        e.GetProperty("spd").GetInt32());

    public static List<int> Ints(JsonElement array)
    {
        var list = new List<int>();
        foreach (var item in array.EnumerateArray()) list.Add(item.GetInt32());
        return list;
    }

    public static List<string> Strings(JsonElement array)
    {
        var list = new List<string>();
        foreach (var item in array.EnumerateArray()) list.Add(item.GetString()!);
        return list;
    }
}
