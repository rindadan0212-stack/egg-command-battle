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

## 自作の仮（状態異常アイコン・2026-08-23）

| | |
|---|---|
| 出所 | **自作**（このリポジトリ内で生成。外部素材ではない）|
| ライセンス | 気にしなくてよい（自作・仮） |
| 作る道具 | `scripts/gen-status-icons.mjs`（Node、外部ライブラリ無し）|

⚠️ **応急対応**（作者の指示 2026-08-23）: 戦闘の状態欄が字だと枠 320 に対し
実測 743 要る組み合わせがあり、字では入らないと分かったため、
**仮のドット絵アイコン**へ作り替えた。⭐ **いずれ本物の絵に差し替える前提。**

### 使っているもの（`Resources/UI/icon/status-*.png`）

| ファイル | 絵 | 対応する状態（`Core.StatusKind`） |
|---|---|---|
| `status-atk.png` | 剣 | 攻撃 の増減 |
| `status-def.png` | 盾（先が尖る） | 防御 の増減 |
| `status-spd.png` | 山形2段（矢羽根） | 速度 の増減 |
| `status-poison.png` | 雫 | 毒 |
| `status-regen.png` | 十字 | リジェネ |
| `status-shield.png` | 六角（先は尖らない） | シールド（盾＝防御と絵で見分ける）|
| `status-stun.png` | 星（火花） | スタン |
| `status-taunt.png` | ▲（警告） | 挑発 |
| `status-guts.png` | ハート | ガッツ |
| `status-immune.png` | 菱形 | 免疫（六角の障壁と、角の立ち方で見分ける）|
| `status-sleep.png` | Z | 睡眠 |
| `status-block.png` | × | ブロック |

⭐ 攻撃・防御・速度は**同じ絵を良い/悪いの色だけで出し分ける**
（一律 ±30% ── `wiki/効果の種類.md`）。色そのものは Web の `stage.css`
（`--good-ink` / `--danger-ink`）と Unity の `Ui.cs` の12定数のまま、新しく作っていない。

⭐ 16×16 で描いて、既存の Kenney 絵と同じ **128×128** の PNG（整数8倍）に出してある。
**白の抜き（透過）** ── 色は掛け算（Unity `Image.color` / Web `.n.icon` の `currentColor` 抱き合わせ）
で乗せるので、絵自身は白の1色しか持たない。

### ⚠️ 差し替え方

1. `scripts/gen-status-icons.mjs` の中の該当する16行（`#`＝白・`.`＝透明）を描き直す
2. `node scripts/gen-status-icons.mjs` を走らせる（同じ場所に上書きされる）
3. **本物の絵に差し替えたら**、`unity/Packages/com.eggcommand.core/Runtime/Art.cs` の
   `Placeholder` からその名前を外し、この節からも該当行を消す
   （`EggCommand.Tests/ArtTests.cs` が `Placeholder` の残り枚数を検査で数えている）
