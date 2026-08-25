# 同梱しているフォント

## PixelMplus10（⭐ 2026-08-25 以降、画面で使っているのはこちら）

| | |
|---|---|
| ファイル | `PixelMplus10-Regular.ttf` / `PixelMplus10-Bold.ttf` |
| 著作権 | Copyright (C) 2002-2013 M+ FONTS PROJECT |
| ライセンス | **M+ FONT LICENSE**（全文 `MPLUS-LICENSE-PixelMplus.txt`）── 商用・非商用を問わず、改変の有無に関わらず、使用・複製・再配布が無制限に許可される。**表示義務なし・無保証** |
| 出所 | https://github.com/itouhiro/PixelMplus （`PixelMplus-20130602.zip`） |
| 収録 | JIS第1・第2水準の全漢字 ＋ Latin-1 ＋ 記号。⚠️ 縦書き非対応 |

🔴 **font-size は「10の倍数」でしか、にじみが1画素も出ない。**
（PIL で 8〜200px を総当たりして実測 ── 10,20,30,…,140 だけが中間色ゼロ。Bold も同じ）
⭐ この作品は **1ドット=設計4px** に統一したので、**40px が「絵と同じドットの太さ」になる唯一の大きさ**。
80px / 120px は 2倍 / 3倍（ドットも2倍/3倍太くなる）── 演出だけに使う。
⭐ **強調は Bold**（大きさを変えずに済む＝ドットの太さが変わらない）。

⚠️ PixelMplus**12** と DotGothic16 は 8〜200px のどこでもにじむ（輪郭が升目に乗っていない）ので**不採用**。
詳細は `wiki/開発/ドット絵化計画.md` §6。


## Mochiy Pop One（⚠️ 2026-08-25 に使用をやめた。戻せるよう残してある）

| | |
|---|---|
| ファイル | `MochiyPopOne-Regular.ttf` |
| 著作権 | Copyright 2020 The Mochiypop Project Authors |
| ライセンス | **SIL Open Font License 1.1**（全文 `OFL-MochiyPopOne.txt`） |
| 出所 | https://github.com/google/fonts/tree/main/ofl/mochiypopone |

丸くて太いポップ体。⭐ 太いので**白抜き**（白い字＋濃紺の縁）が効く。
モックが使っているのもこれ。

⚠️ DotGothic16 から替えた。器がカジュアルな丸角になったので、
字だけドットだと様式が2つ同居する。DotGothic16 は残してあるが未使用。


## DotGothic16

日本語のドットフォント。⭐ キャラがドット絵なので、字も同じ格子に乗るものを選んだ。

| | |
|---|---|
| ファイル | `DotGothic16-Regular.ttf`（Version 1.100） |
| 著作権 | Copyright 2020 The DotGothic16 Project Authors |
| ライセンス | **SIL Open Font License 1.1** |
| 出所 | https://github.com/fontworks-fonts/DotGothic16 |
| ライセンス本文 | https://scripts.sil.org/OFL |

上の情報は**フォントファイルの name テーブルから直接読み取ったもの**（推測ではない）。

### OFL で許されること・要ること

- ✅ APK に埋め込んで**販売してよい**（アプリの一部としての配布は自由）
- ✅ 改変してよい（ただし "DotGothic16" の名前は使えない）
- ⚠️ **フォント単体では売れない**
- ⚠️ **配布物にライセンス全文と著作権表示を同梱すること**

### ライセンス全文

`OFL.txt` に原文をそのまま置いてある（公式リポジトリから取得。要約や書き写しではない）。
配布物にはこれを同梱すること。

ゲーム内のクレジット表記にも上の著作権行を出す。
