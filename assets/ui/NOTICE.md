# UI の意匠

## Hyper Casual UI - Free（MadFireOn）

| | |
|---|---|
| 出所 | https://swapnilrane24.itch.io/hyper-casual-ui-free |
| ライセンス | **CC0 1.0 Universal** |
| 取得したファイル | `HyperCasual Game UI.zip`（759 kB / PNG 73 + SVG 1） |

⚠️ **zip にライセンス文が入っていない。**上記ページの表記
（"You're free to use these game assets in any project, personal or commercial.
There's no need to ask permission before using these."）が唯一の出所。
CC0 なので帰属表示は不要だが、出荷前にページの表記を控えておくこと。

### 選んだ理由と、⚠️ 妥協した点

⭐ モック（`参考/モック_タマゴハンター/`）の様式は
「丸角＋鮮やかなフラット色＋**濃紺の太い輪郭線**＋ハードシャドウ」。
この素材は前3つを満たすが、**濃紺の輪郭線を持っていない**（実物を展開して確認済み）。

CC0 の範囲で太枠を持つセットは見つからなかったため、
**枠を諦めて素材をそのまま使う**とユーザーが判断した（2026-08-16）。
⚠️ したがって**モックと完全には一致しない**。輪郭が要るなら有償セットの再調査から。

### 使っているもの

| ファイル | 元 | 役割 |
|---|---|---|
| `panel.png` | whiteBG | 器（白い丸角）。⚠️ 器はこれ1種類 |
| `tile.png` | cornerSquare | 絵を載せる枠（BOX の詳細など） |
| `pill.png` | roundEdgeRect | 名札・数値のピル |
| `circle.png` / `circle-outline.png` | circle / borderCircle | 円形アイコンの地と枠 |
| `button-lead.png` | yellowRectNormal | 主導線。⭐ 1画面に1つだけ |
| `button.png` | blueRectNormal | 通常の押しどころ |
| `button-good.png` / `button-danger.png` | greenRectNormal / pinkRectNormal | 良い側 / 危ない側 |
| `button-off.png` | greyRectNormal | 押せないとき |

### ⚠️ 取り込みの注意

- 影が**下方向だけ**入っているので、9スライスの下辺は影を含む太さにする
- 白い器には枠が無い。**淡い地の上では輪郭が消える**ので、地の色は器と差を付ける
- ドット絵ではないので Point フィルタにしない（Bilinear のまま）

---

## Board Game Icons 1.1（Kenney）

| | |
|---|---|
| 出所 | https://kenney.nl/assets/board-game-icons |
| ライセンス | **CC0 1.0 Universal**（zip 内 `License.txt` に明記）|
| 取得したファイル | `kenney_board-game-icons.zip`（1.04 MB）|
| 取得日 | 2026-08-20 |

⚠️ **zip には `.swf` と `.url` が入っている。**
`Vector/overview.swf` / `Visit Kenney.url` / `Visit Patreon.url` の3つは
**取り込んでいない**（PNG 以外は入れない）。取り込む前に PNG の頭 8 バイトも確かめてある。

⭐ 絵は**白の抜き（透過）**。色は Unity 側で `Image.color` を掛けて出す。
128px 版を使用（`PNG/Double (128px)/`）。

### 使っているもの（`Resources/UI/icon/`）

| ファイル | 元 | 役割 |
|---|---|---|
| `die-1`〜`die-6` | dice_1〜dice_6 | さいころの出目 |
| `die` / `die-spent` | dice / dice_empty | ⭐ **残りの回数**（使うと空になる）|
| `stat-atk` | sword | 壁＝攻撃 |
| `stat-hp` | suit_hearts | 床＝HP |
| `stat-def` | shield | 重圧＝防御 |
| `locked` | lock_closed | ⚠️ **通れない**（字で書かない）|
| `mob` | skull | 敵のマス |
| `goal` | flag_triangle | 卵（ゴール）|
| `plain` | token | 何も無いマス |
| `up` / `down` | direction_n / direction_s | ステが上がる / 下がる |
| `pawn` | pawn | 駒（予備）|

⭐ **上の帯と盤で同じ絵を使う。**「壁 ＝ 剣の絵 ＝ 攻撃」を字で説明せず、
同じ絵が両方に出ていることで結び付ける。

---

## 作者支給の状態アイコン・墓（2026-08-30取り込み）

| | |
|---|---|
| 出所 | 作者がこのプロジェクト用に支給した生成画像3枚 |
| ライセンス | 作者支給素材として扱う |
| 作る道具 | `tools/prepare-user-art.mjs` + `tools/draw/` の Pixelizer |

4×4の状態絵を1枚ずつ中央正方形で切り、Pixelizer の OKLab 減色・平均縮小・
孤立点除去・輪郭補正を通した。ゲーム用は16×16、確認・再利用用は
`art/status-pixel/` に32×32を残す。墓2枚は元から透過済みの完成ドット絵なので、
再減色・再透過せず原画PNGをそのまま使う（表示時だけ `image-rendering: pixelated`）。

### 使っているもの（`Resources/UI/icon/status-*.png`）

| ファイル | 絵 | 対応する状態（`Core.StatusKind`） |
|---|---|---|
| `status-atk-up/down.png` | 剣＋上下矢印 | 攻撃 の増減 |
| `status-def-up/down.png` | 盾＋上下矢印 | 防御 の増減 |
| `status-spd-up/down.png` | 靴＋上下矢印 | 速度 の増減 |
| `status-poison.png` | 雫 | 毒 |
| `status-regen.png` | 十字 | リジェネ |
| `status-shield.png` | 光の障壁 | シールド |
| `status-stun.png` | 回る星 | スタン |
| `status-taunt.png` | 怒り印 | 挑発 |
| `status-guts.png` | 火 | ガッツ |
| `status-immune.png` | 力こぶ | 免疫 |
| `status-sleep.png` | Z | 睡眠 |
| `status-block.png` | 白い× | ブロック |
| `status-seal.png` | 目に× | 封印 |
| `status-anchor/invincible/counter.png` | 錨・星盾・往復矢印 | 固定・無敵・反撃（コード生成の仮絵）|

攻撃・防御・速度は強化と弱体を別絵にした。全色を絵自身が持つため、Web は
`natural=yes` のアイコンへ単色 mask を重ねない。残りターン数だけ良悪の文字色を使う。

`grave-ally.png` と `grave-foe.png` は戦闘と放置帯で倒れた体と置き換える。
味方は丸い正面碑、敵は割れた暗い碑で、色だけに頼らず区別する。

### ⚠️ 差し替え方

1. 原画は `art/source/user-2026-08-29/` の3枚（SHA-256を生成器が検証する）
2. Pixelizer を localhost で開き、`node tools/prepare-user-art.mjs` を走らせる
3. 固定・無敵・反撃を作者絵へ差し替えたら、`game/EggCommand.Core/Art.cs` の
   `Placeholder` からその名前を外す
   （`EggCommand.Tests/ArtTests.cs` が `Placeholder` の残り枚数を検査で数えている）
