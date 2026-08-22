using System;
using System.Collections.Generic;
using System.IO;
using EggCommand.Core;
using EggCommand.Sim;
using Xunit;

namespace EggCommand.Tests;

/// <summary>⭐ **Unity が書いた実物の保存を、web 側がそのまま読めるか。**
///
/// ⚠️ 計画 §6 の決めごと: 「**変換の道具を書かない。**いまのファイルをそのまま読めることを
/// 合格条件にする（変換器のバグが移植のバグに化ける）」。
///
/// ⭐ `records/save-unity.json` は**実際に遊んだ保存の写し**
/// （`AppData/LocalLow/Indie/Egg Command Battle/egg-command.json`・2026-08-22 取得）。
/// ⚠️ 作り物ではない ── 作り物だと「自分が書いた形」しか試せない。</summary>
public class SaveJsonTests
{
    private static string Real =>
        Path.Combine(AppContext.BaseDirectory, "records", "save-unity.json");

    /// <summary>⚠️ 無ければ検査が空回りする。⭐ 「合格」が「調べていない」を意味しないように。</summary>
    [Fact]
    public void 実物の保存が置いてある()
    {
        Assert.True(File.Exists(Real), $"{Real} が無い（csproj の records コピーを見る）");
        Assert.True(new FileInfo(Real).Length > 1000);
    }

    [Fact]
    public void Unityが書いた保存をそのまま読める()
    {
        var notes = new List<string>();
        var game = SaveJson.Read(File.ReadAllText(Real), notes);

        Assert.NotNull(game);
        // ⭐ 中身が届いていること（空の器を「読めた」と言わない）
        Assert.NotEmpty(game!.Storage.Creatures);
        foreach (var c in game.Storage.Creatures)
        {
            Assert.False(string.IsNullOrEmpty(c.Id));
            Assert.True(SpeciesTable.Has(c.SpeciesId), $"{c.Id} の種族 {c.SpeciesId} が表に無い");
        }
        // ⚠️ 読み替えが起きたら、何が起きたか言えること（黙って別物にしない）
        foreach (var note in notes) Assert.False(string.IsNullOrEmpty(note));
    }

    /// <summary>⚠️ 🔴 **乱数の系統は「位置」でしか表せていない。**
    /// ⭐ 並びが1つずれると、そこから後ろが全部別の運になる。
    ///
    /// ⚠️ 目で見て気づける差ではないので、**引いて比べる**しかない。</summary>
    [Fact]
    public void 読み書きしても乱数の続きが変わらない()
    {
        string json = File.ReadAllText(Real);
        var a = SaveJson.Read(json);
        var b = SaveJson.Read(SaveJson.Write(SaveJson.Read(json)!));
        Assert.NotNull(a);
        Assert.NotNull(b);

        var left = Draw(a!);
        var right = Draw(b!);
        Assert.Equal(left, right);
        // ⭐ 12系統。⚠️ 減っていたら、どこかで並びが落ちている
        Assert.Equal(12, left.Count);
    }

    /// <summary>⭐ 書いて読んで書いたら、同じ字になる（余計な揺れが無い）。</summary>
    [Fact]
    public void 書き出しが安定している()
    {
        var game = SaveJson.Read(File.ReadAllText(Real))!;
        string once = SaveJson.Write(game);
        string twice = SaveJson.Write(SaveJson.Read(once)!);
        Assert.Equal(once, twice);
    }

    /// <summary>⚠️ **版が新しすぎる保存は読まない**（上書きさせないため）。</summary>
    [Fact]
    public void 版が新しすぎたら読まない()
    {
        string json = File.ReadAllText(Real).Replace("\"Version\":1", "\"Version\":99");
        Assert.Null(SaveJson.Read(json));
    }

    /// <summary>⚠️ Unity の `JsonUtility` は **null 文字列を書けず `\"\"` にする。**
    /// ⭐ 実物にその形が入っていることを確かめておく ── 入っていなければ、
    /// この検査は「読めた」と言えても**その場合を試していない**。</summary>
    [Fact]
    public void 実物に空文字の欄が入っている()
    {
        Assert.Contains("\"ParentA\":\"\"", File.ReadAllText(Real));
    }

    /// <summary>12系統から1回ずつ引く。⚠️ 引いた**あと**の状態は捨てる（比べるのは値だけ）。</summary>
    private static List<uint> Draw(Game game)
    {
        var save = Snapshots.Save(game);
        // ⚠️ `StreamsOf` は private なので、保存された語数から系統数を出す
        int streams = save.Rng.Count / 4;
        var got = new List<uint>();
        var each = new[]
        {
            game.RngNest, game.RngEgg, game.RngHatch, game.RngSteal,
            game.RngBreed, game.RngRarity, game.RngEncounter, game.RngSlant,
            game.RngElement, game.RngTrait, game.RngBattle, game.RngPalette,
        };
        Assert.Equal(each.Length, streams);
        foreach (var rng in each) got.Add(rng.U32Value());
        return got;
    }
}
