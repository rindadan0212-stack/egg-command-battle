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
