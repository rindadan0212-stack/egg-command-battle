using EggCommand.Core;
using EggCommand.Sim;
using Microsoft.JSInterop;

namespace EggCommand.Web;

/// <summary>保存の出し入れ。⭐ 文字にするのは <see cref="SaveJson"/>、置き場は `save.js`。
///
/// ⚠️ **読み込みを書き出しより先に作る**（計画 §6 の手順2）。⭐ 読めないうちに書けると、
/// 作り直した空の中身で本物を潰す。
///
/// ⚠️ **「無い」と「読めない」を区別する。**⭐ 区別が付かないと、
/// 呼ぶ側が新しいゲームをその上に書き戻す（Unity 版 `SaveFile.Read` と同じ決めごと）。</summary>
public sealed class Vault
{
    private readonly IJSRuntime _js;
    /// <summary>前に書き出した中身。⭐ 同じなら書かない。</summary>
    private string? _was;

    /// <summary>このタブが書いてよいか。⚠️ 2枚目は**読み取り専用**で開く
    /// （両方が書くと、後勝ちで片方の遊んだ結果が消える）。</summary>
    public bool CanWrite { get; private set; }

    /// <summary>⚠️ **ファイルは在るのに読めなかった。**⭐ 受け取ったら書き込みを止める。</summary>
    public bool Broken { get; private set; }

    /// <summary>読み替えが起きた記録。⚠️ 黙って別の種族になっているのが一番困る。</summary>
    public List<string> Notes { get; } = new();

    public Vault(IJSRuntime js) { _js = js; }

    /// <summary>開く。⭐ 書き手の権利を取り、消されにくくしてもらってから読む。</summary>
    public async Task<Game?> Open()
    {
        CanWrite = await _js.InvokeAsync<bool>("eggSave.claim");
        if (CanWrite) await _js.InvokeAsync<bool>("eggSave.persist");

        string? json = await _js.InvokeAsync<string?>("eggSave.read");
        if (string.IsNullOrEmpty(json)) return null;   // ⭐ 初回

        try
        {
            var game = SaveJson.Read(json, Notes);
            if (game == null)
            {
                // ⚠️ **壊れてはいない。**版が新しすぎるだけ。
                //    ⭐ ここで捨てて上書きすると、直せたはずの保存が消える。
                Broken = true;
                return null;
            }
            _was = json;
            return game;
        }
        catch
        {
            Broken = true;
            return null;
        }
    }

    /// <summary>書く。⚠️ 読み取り専用のタブと、読めなかったときは**書かない**。</summary>
    /// <returns>実際に書いたか。</returns>
    public async Task<bool> Keep(Game game, long nowUnix)
    {
        if (!CanWrite || Broken) return false;
        string json = SaveJson.Write(game);
        if (json == _was) return false;
        string how = await _js.InvokeAsync<string>("eggSave.write", json, nowUnix);
        if (!how.StartsWith("failed")) { _was = json; return how == "wrote"; }
        // ⚠️ 黙って諦めない。⭐ 憶えないので次回は必ず書き直す
        _was = null;
        return false;
    }

    /// <summary>⭐ **本命の層**: ブラウザの外へ出す。</summary>
    public async Task Export(Game game) =>
        await _js.InvokeVoidAsync("eggSave.download", "egg-command.json", SaveJson.Write(game));

    /// <summary>残っている世代の古さ（秒）。⭐ 「少しずつ壊れていく」形への備えが
    /// 効いているかを、画面から見えるようにするため。</summary>
    public async Task<int[]> Past(long nowUnix) =>
        await _js.InvokeAsync<int[]>("eggSave.past", nowUnix);

    /// <summary>いまの保存の大きさ（字数）。⚠️ 0 なら**まだ1度も書かれていない**。</summary>
    public async Task<int> Size() => await _js.InvokeAsync<int>("eggSave.size");

    /// <summary>⭐ **外から1つ読み込む。**
    ///
    /// ⚠️ **読めなければ何もしない。**⭐ いま遊んでいる中身を、
    /// 中身の分からないもので置き換えない ── 取り返しがつかない。
    /// ⚠️ やめたときと読めなかったときを**区別して**返す（画面が何を言うか変わる）。</summary>
    /// <returns>読めた中身。やめたら null。⚠️ 読めなかったときは投げる。</returns>
    public async Task<Game?> Load()
    {
        string? json = await _js.InvokeAsync<string?>("eggSave.pick");
        if (string.IsNullOrEmpty(json)) return null;   // ⭐ やめた

        var game = SaveJson.Read(json, Notes)
            ?? throw new InvalidOperationException("この控えは読めません（版が新しすぎるかもしれません）");
        // ⭐ 読めたなら、こちらの「壊れている」は解ける（上書きしてよい）
        Broken = false;
        _was = null;
        return game;
    }
}
