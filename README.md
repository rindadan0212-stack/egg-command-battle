# 卵強奪バトル ── 遊べるビルド

**これは出先で触るための、ビルド済みのゲーム本体だけです。** ソース・検査・資料は入っていません。

| | |
|---|---|
| **遊ぶ** | https://rindadan0212-stack.github.io/egg-command-battle/app/ |
| 図鑑（技・種族の一覧） | https://rindadan0212-stack.github.io/egg-command-battle/ |

- 個人開発の**制作途中**のものです。仕様も内容も断りなく変わります。
- 保存はブラウザの中（端末ごと）に置かれます。別の端末へは持ち越されません。
- 絵と字の出所（すべて CC0 / OFL）は制作リポジトリ側の `NOTICE.md` に控えてあります。

---

## このリポジトリの作り直しかた（制作側の控え）

制作リポジトリ（`Desktop/gamedev/Egg Command Battle`）で:

1. `dotnet publish game/EggCommand.Web -c Release -o <どこか>`
2. `<どこか>/wwwroot/*` をこのリポジトリへ上書き
3. 🔴 **`index.html` の `<base href="/" />` を `<base href="/egg-command-battle/" />` に直す**
   （公開先が `/` ではなく `/egg-command-battle/` なので、直さないと
   すべての資材が github.io の**根**に取りに行って 404 になる。2026-08-29 に踏んだ）
4. 直した `index.html` を `404.html` と `app/index.html` にも写す（3枚とも同じ中身）
5. `index.html.br` / `index.html.gz` は**置かない**（3の書き換えと中身が食い違うため）
6. `_framework/` に残った古い `EggCommand.Web.<別のハッシュ>.wasm` を消す
   （`blazor.boot.json` が指しているものだけを残す）
