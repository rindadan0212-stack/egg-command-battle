# 同梱している第三者のソフトウェア

このリポジトリには、以下のソフトウェアをそのまま同梱しています。
それぞれ元のライセンスに従います（本体の LICENSE は適用されません）。

---

## gif.js 0.2.0

- 用途: 動く GIF の書き出し
- ファイル: `gif.js`、`gif.worker.js`、および `index.html` に埋め込んだワーカー本体
- 入手元: <https://github.com/jnordberg/gif.js>
- ライセンス: The MIT License (MIT)

```
The MIT License (MIT)

Copyright (c) 2013-2018 Johan Nordberg

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

---

# 同梱していないもの（参考）

## DotGothic16

- 用途: 見出しなどのドット風書体
- 扱い: **同梱も読み込みもしていません。** 端末に入っている場合だけ使われます
- 理由: Google Fonts から読み込むと、開くたびに IP・UA・リファラが Google へ渡ります。
  「アップロードなし・追跡なし」と書いてある以上、書体だけ例外にはできないため外しました
- 入っていない端末では `Press Start 2P` → 等幅フォント の順に落ちます
- ライセンス: SIL Open Font License 1.1
- 入手元: <https://fonts.google.com/specimen/DotGothic16>
