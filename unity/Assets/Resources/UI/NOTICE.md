# UI の意匠

## Kenney — UI Pack: Pixel Adventure

| | |
|---|---|
| 出所 | https://kenney.nl/assets/ui-pack-pixel-adventure |
| ライセンス | **CC0 1.0（パブリックドメイン）** — 全文は `Kenney-License.txt` |
| 版 | UI Pack - Pixel Adventure (2.0) / 2024-09-04 |
| 取得したファイル | `kenney_ui-pack-pixel-adventure.zip`（0.3MB） |
| SHA256 | `0B0ED4802EBCFFF5E44E370F394BAA1D751862A5A4A7612AC4CE84E85FAA0627` |

CC0 なので**帰属表示は不要**（作者は任意での記載を歓迎している）。
商用利用・改変・再配布いずれも自由。

### 選んだ理由

⭐ 同じ「UI Pack」でも**ピクセル版**を選んだ。キャラがドット絵・字が DotGothic16 なので、
枠だけ滑らかだと1つの画面に2つの解像感が同居する。

### 使っているもの

元は `Tiles/Large tiles/Thick outline/` の 32×32 タイル。
1枚が枠と中央を持つので、**そのまま9スライスできる**（3×3に組む必要はない）。

| ファイル | 元 | 役割 |
|---|---|---|
| `panel.png` | tile_0003 | 器（行・カード）。⚠️ **器はこれ1種類** |
| `panel-hi.png` | tile_0002 | 予備（今は未使用。明るい鋼色は字の明暗が逆になる） |
| `button.png` | tile_0001 | 通常の押しどころ |
| `button-lead.png` | tile_0000 | 主導線。⭐ 1画面に1つだけ |
| `frame-danger.png` | tile_0020 | 予備（赤枠） |
| `frame-good.png` | tile_0021 | 予備（緑枠） |

### 取り込みの設定（Ui.cs が前提にしている）

- Sprite / Single / **Point フィルタ** / 圧縮なし / mipmap なし
- `spritePixelsPerUnit = 32`、**9スライスの枠 = 6px**（実測 4〜5px の枠＋わずかな内側）
- `Image.pixelsPerUnitMultiplier = 1`
  ⚠️ ここを 0.25 にすると枠が 75 単位になり、112 の押しどころが枠だけになる（実際なった）

### ⚠️ 色を掛けない

ドット絵に `Image.color` を掛けると作者が組んだ配色が濁る。
使い分けは「どの絵を貼るか」で行う。例外は「押せない」ときの減光のみ。
