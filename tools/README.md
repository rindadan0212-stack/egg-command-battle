# tools ── 検査と道具

## 絵を描く（`draw/`）

⭐ **`絵を描く.bat`**（`http://localhost:5818`）── この作品専用のドット絵エディター。
⚠️ [pixelizer](https://github.com/rindadan0212-stack/pixelizer) から**分けたもの**で、
もう別物として育てる（[draw/README.md](draw/README.md)）。
⭐ 270×480 のキャンバスで画面を起こし、`sim import-screen` で骨組みにする。

## 検査（動いているゲームを外から叩く）

⭐ **どれも、動いているゲーム（`http://localhost:5817`）を外から叩きます。**
先に `ゲームを開く.bat` か `dotnet run --project game/EggCommand.Web` でサーバを立ててください。

| 打つもの | 何を見るか |
|---|---|
| `node tools/inspect.mjs` | 重なり・はみ出し・字切れ・押しどころの広さ |
| `node tools/inspect-selftest.mjs` | ⭐ 検査をわざと壊して、**検査が本当に見ているか**を確かめる |
| `node tools/play.mjs` | 押して反応するか |
| `node tools/loop.mjs` | 遊びの輪（卵→孵化→編成→戦闘）が閉じるか |
| `node tools/measure.mjs` | 実フォントで測った字の高さ（`/measure`） |
| `node tools/why.mjs` | 画面が何回組み直されているか |
| `node tools/shot.mjs` | 画面を撮って `records/shots/` へ |
| `npm run goldens:check` | 🔴 C# が移植元の TS と同じ答えを出すか（`old/ts/` を読む） |
| `node tools/gen-status-icons.mjs` | `assets/ui/icon/status-*.png` 12枚を作り直す（**唯一の出所**） |

⚠️ `audit.mjs` は直接打ちません（`inspect.mjs` が読む検査の本体）。
