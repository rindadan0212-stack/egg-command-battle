# old ── もう動かしていないもの

⚠️ **ここは博物館です。**新しいものをここに足さないでください。
⭐ 捨てずに残しているのは、**作り直せない証拠**がここにしか無いからです。

## `ts/` ── 移植元の TypeScript 版

2026-08 まで遊べたブラウザ版（Vite / port 5815）。C# へ移したあとも消していません。

🔴 **1本だけ、今も生きている線があります。**

```
npm run goldens:check      # 今の C# が TS と同じ答えを出すか
```

`tools/goldens.mjs` が `old/ts/src/game/*.ts` と `old/ts/src/core/rng.ts` を読み、
`game/goldens/*.json`（9枚）を作り直します。この9枚が **C# への移植が正しいことの唯一の証拠**で、
検査30箇所が読んでいます。⚠️ **`old/ts/src/` を消すと、その証拠を二度と作り直せません。**

⚠️ 画面まわり（`views/` `main.ts` `style.css` `gallery/` `vite.config.ts`）は動きません。
遊ぶための版ではなく、**数字の出所**として置いてあります。

## `unity/` ── Unity プロジェクトの残骸

2026-08-22 に Unity を出ました。⚠️ **このままでは Unity で開けません**
（骨組み・絵・字は `assets/` へ、規則の C# は `game/EggCommand.Core/` へ出したので、
`Resources/` と `Packages/` が空です）。`.meta` も全部捨てました。

残してあるのは、**どうやって作ったかの記録**です:

| | |
|---|---|
| `Scripts/View/` | Unity 時代の画面。🔴 `LayoutRuleTests` がまだここを読む（下記） |
| `Editor/` | 絵と Prefab を**生成した装置**（`BuildSky` `BuildButtonArt` ほか） |
| `Prefabs/` | 座標の元。中身は `assets/layouts/*.txt` へ移し終えている |
| `ProjectSettings/` `Packages/` `Scenes/` | Unity の設定 |
| `prefab2layout.py` | Prefab → 骨組み の変換を試した probe |

### ⚠️ `LayoutRuleTests` がここを見張っている

`game/EggCommand.Tests/LayoutRuleTests.cs` は `Scripts/View/*.cs` を**テキストとして**読み、
`Ui.Place(` のような「座標をコードに書く呼び出し」が無いかを見ています。
⭐ 規律そのものは今も正しいのですが、**見張る先が博物館になりました**
（今そこに座標を書く人はいません）。web 側へ向け直すかは未決です。
