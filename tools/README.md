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
| `node tools/bg-band.mjs` | ホームの**流れる背景**（空・山・遠くの地面）を作り直す（**唯一の出所**） |
| `node tools/icon-fit.mjs` | 盤・帯の**小物の絵**を枠の大きさに焼き直す（原画は `assets/ui/icon-src/`・**唯一の出所**） |

⚠️ `audit.mjs` は直接打ちません（`inspect.mjs` が読む検査の本体）。

## 流れる背景を足す・差し替える（`bg-band.mjs`）

1. 作者の絵を `assets/ui/home-src/` へ置く（pixelizer の**画像**書き出しは4倍。回っていてもよい）
2. `tools/bg-band.mjs` の `BANDS` に1行足す（`up`＝1ドットが何画素 / `turn`＝回す角度 /
   `shrink`＝何分の1に間引くか / `key`＝抜く白地の色 / `top`＝置く高さ / `secs`＝1周の秒数）
3. `node tools/bg-band.mjs` ── 出来上がった **`home.txt` の行と `stage.css` の動きを刷る**ので、そのまま写す
4. `dotnet run --project game/EggCommand.Sim -- paint-placeholder`（大きさの目録を書き直す）
5. `dotnet test` ── 幅・目録・流す距離が食い違っていればここで落ちる
   （`StageCssTests.流れる背景は輪が閉じている` / `PicFrameSizeTests`）

🔴 **帯は「元・鏡」を4枚並べ、2枚ぶん流して先頭へ戻す。**⚠️ 2枚幅で1枚ぶん流すと、
戻る瞬間に**絵が左右反転してパッと切り替わる**（2026-08-29 まで実際にそうだった）。
理由は `bg-band.mjs` の冒頭に書いてあります。
