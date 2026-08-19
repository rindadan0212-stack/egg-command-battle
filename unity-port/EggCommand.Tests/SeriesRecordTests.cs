using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using EggCommand.Sim;

namespace EggCommand.Tests;

/// <summary>**現行の記録**。⚠️ ゴールデン（移植の証拠）とは役割が違う。
///
/// ゴールデンは TS を走らせた出力で、**移植が正しいこと**を示す。作り直さない。
/// ⚠️ 素質を4本から6本にした日（2026-08-18）に乱数の消費が変わり、
/// 卵・配合・進行・試合の**系列**はゴールデンと別物になった。
/// ⭐ そこで空いた穴 ──「実装をいじったら黙って個体や試合が変わる」── を塞ぐのがここ。
///
/// 落ちたときの読み方:
///   ⚠️ **遊びを変えた覚えが無いのに落ちた = 事故。**記録を書き直さず、実装を直す。
///   ⭐ 意図して変えたなら `dotnet run --project EggCommand.Sim -- record` で書き直し、
///      何を変えたのかを 仕様変更履歴.md に書く。</summary>
public class SeriesRecordTests
{
    private static JsonElement Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "records", "series.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"現行の記録が無い: {path}\n  `dotnet run --project EggCommand.Sim -- record`", path);
        }
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.Clone();
    }

    /// <summary>記録と、いまの実装が作る行を1文字ずつ突き合わせる。
    /// ⭐ 作る手順は <see cref="SeriesRecord"/> に1つだけ置いてあるので、
    /// 「書き出す側とテスト側で手順がずれていて何も見ていない」が起きない。</summary>
    private static void Same(string section, List<string> actual)
    {
        var expected = new List<string>();
        foreach (var row in Load().GetProperty(section).EnumerateArray())
        {
            expected.Add(JsonSerializer.Serialize(row, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }));
        }

        Assert.True(expected.Count == actual.Count,
            $"{section}: 件数が {actual.Count}（記録は {expected.Count}）");
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.True(Normalize(expected[i]) == Normalize(actual[i]),
                $"{section}[{i}]\n  いま: {actual[i]}\n  記録: {expected[i]}");
        }
    }

    /// <summary>⚠️ System.Text.Json が書き戻す形と手書きの形で空白がずれるので、そこだけ均す。</summary>
    private static string Normalize(string json) => json.Replace(" ", "").Replace("\n", "");

    [Fact]
    public void 巣の守り手の系列が変わっていない() => Same("defenders", SeriesRecord.DefenderRows());

    [Fact]
    public void 卵と孵化の系列が変わっていない() => Same("eggs", SeriesRecord.EggRows());

    [Fact]
    public void 始めたときの3体が変わっていない() => Same("games", SeriesRecord.GameRows());

    [Fact]
    public void 配合の系列が変わっていない() => Same("breeds", SeriesRecord.BreedRows());

    /// <summary>⭐ 出来事の列を丸ごと畳んだ値まで見る。1手でも変われば落ちる。
    /// ⚠️ どこが変わったかは出ないので、行動数・出来事の数・最終HP を横に置いてある。</summary>
    [Fact]
    public void 試合が丸ごと変わっていない() => Same("battles", SeriesRecord.BattleRows());
}
