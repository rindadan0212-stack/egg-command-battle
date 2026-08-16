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
