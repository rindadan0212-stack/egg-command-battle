// 骨組みエディタ（`/edit`）専用: ディスクとの出し入れ（File System Access API）。
//
// ⚠️ **遊ぶ頁（`/app`）は使わない。**⭐ 遊びの読み書きは `save.js`（localStorage）が持つ。
// こちらが触るのは `assets/layouts/*.txt`（の写し）── エディタが選んだ
// フォルダの中の `<name>.txt` だけ。
//
// ⭐ **作者自身の別作品（`r18/1/_shared/tools/studio/STUDIO.html`）が同じ形で
//    showDirectoryPicker → getFileHandle → createWritable をやっている。**
//    ここはその形をそのまま借りる（コードは写さず、書き方だけ真似る）。

// 🔴 **フォルダの覚え直し（2026-08-25）**── `showDirectoryPicker` は毎回フルの OS
// ダイアログを開くが、この作品では骨組みの置き場所は実質1か所（`assets/layouts`）しか
// 無い。一度選んだハンドルを IndexedDB へ持たせておき、次に開いたときは
// `queryPermission` だけで（＝クリック無しで）繋ぎ直す。⚠️ 許可が薄れていたら
// （ブラウザが時間で失効させることがある）、フルの再選択ではなく `requestPermission`
// （軽い1クリックの確認）で足りるかをまず試す。

const DiskDb = {
  _open() {
    return new Promise((resolve, reject) => {
      const req = indexedDB.open('egg-disk', 1)
      req.onupgradeneeded = () => req.result.createObjectStore('handles')
      req.onsuccess = () => resolve(req.result)
      req.onerror = () => reject(req.error)
    })
  },
  async save(key, handle) {
    const db = await this._open()
    return new Promise((resolve) => {
      const tx = db.transaction('handles', 'readwrite')
      tx.objectStore('handles').put(handle, key)
      tx.oncomplete = () => resolve()
      tx.onerror = () => resolve()   // ⚠️ 覚えられなくても致命的ではない（次も選べばよい）
    })
  },
  async load(key) {
    const db = await this._open()
    return new Promise((resolve) => {
      const tx = db.transaction('handles', 'readonly')
      const req = tx.objectStore('handles').get(key)
      req.onsuccess = () => resolve(req.result || null)
      req.onerror = () => resolve(null)
    })
  },
}

window.eggDisk = {
  /** 対応しているブラウザか。⚠️ **押しどころを殺す判定はここを見て C# 側がする**
   * （黙って何も起きない、にしない）。 */
  supported() {
    return 'showDirectoryPicker' in window
  },

  /** ⭐ 骨組みの入っているフォルダを選ばせる。以後そこから読み書きする。
   * ⭐ 選べたら覚える（次回以降 <see cref="reconnect"/> が使う）。
   * @returns {Promise<boolean>} 選べたか（やめたら false）。 */
  async pick() {
    if (!this.supported()) return false
    try {
      this._root = await window.showDirectoryPicker({ id: 'egg-layouts', mode: 'readwrite' })
      await DiskDb.save('layouts', this._root)
      return true
    } catch (e) {
      // ⚠️ ユーザーがキャンセルすると AbortError が飛ぶ ── これは失敗ではない
      return false
    }
  },

  /** ⭐ **クリック無しで繋ぎ直せるか試す。**⚠️ ページを開いた直後（ユーザー操作の
   * 前）に呼ぶ想定なので、`queryPermission` だけを見る ── `requestPermission` は
   * ブラウザがユーザー操作を要求するため、ここでは呼ばない（呼んでも黙って失敗する）。
   * @returns {Promise<'granted'|'prompt'|'none'>}
   *   'granted' … 繋がった（もう `this._root` が使える）
   *   'prompt'  … 覚えているが確認が要る（<see cref="reconnectConfirm"/> をボタンから呼ぶ）
   *   'none'    … 覚えていない（初回。<see cref="pick"/> でフルに選ばせる） */
  async reconnect() {
    if (!this.supported()) return 'none'
    let handle
    try { handle = await DiskDb.load('layouts') } catch (e) { return 'none' }
    if (!handle) return 'none'
    try {
      const perm = await handle.queryPermission({ mode: 'readwrite' })
      if (perm === 'granted') { this._root = handle; return 'granted' }
      return 'prompt'
    } catch (e) {
      return 'none'   // ⚠️ ハンドルが壊れている（フォルダを消した等）── 選び直しへ
    }
  },

  /** ⭐ <see cref="reconnect"/> が 'prompt' を返したときの、1クリックの確認。
   * ⚠️ **ここはボタンのクリックハンドラから呼ぶこと**（ユーザー操作の中でないと
   * `requestPermission` がブラウザに拒まれる）。フルの OS フォルダ選択は開かない。
   * @returns {Promise<boolean>} 繋がったか。 */
  async reconnectConfirm() {
    let handle
    try { handle = await DiskDb.load('layouts') } catch (e) { return false }
    if (!handle) return false
    try {
      const perm = await handle.requestPermission({ mode: 'readwrite' })
      if (perm !== 'granted') return false
      this._root = handle
      return true
    } catch (e) {
      return false
    }
  },

  /** いま選んでいるフォルダの名前。⚠️ 何も選んでいなければ空。 */
  folderName() {
    return this._root ? this._root.name : ''
  },

  /** `<name>.txt` の中身をそのまま返す。
   * @param {string} name 拡張子を除いた名前（`box` など）
   * @returns {Promise<string|null>} 読めた中身。無ければ null。 */
  async read(name) {
    if (!this._root) return null
    try {
      const fh = await this._root.getFileHandle(name + '.txt', { create: false })
      const file = await fh.getFile()
      return await file.text()
    } catch (e) {
      return null
    }
  },

  /** ⭐ **書く直前に現物を読み直し、開いたときの中身と違っていたら書かない。**
   *
   * ⚠️ 別ツール（エディタ自身の別タブ含む）がこの `.txt` を先に書き換えていた場合、
   * 古い読み込み状態のまま上書きすると**その変更が消える**（STUDIO の `edSave` と同じ守り）。
   * ⚠️ 比べるときは改行コードと末尾空白の違いを無視する ── ここで見たいのは
   * 「誰かが中身を変えたか」であって、書式の揺れではない。
   * ⚠️ **実際に書き込む文字列はここでは正規化しない**（骨組みは1バイトも動かさない約束）。
   *
   * @param {string} name 拡張子を除いた名前
   * @param {string} openedText 開いたときに読んだ中身（比較の基準）
   * @param {string} newText 書き込む中身（往復検査を通したもの）
   * @returns {Promise<'wrote'|'changed'|'failed'>} */
  async write(name, openedText, newText) {
    if (!this._root) return 'failed'
    const norm = (s) => (s || '').replace(/\r\n/g, '\n').replace(/[ \t]+$/gm, '')
    try {
      const fh = await this._root.getFileHandle(name + '.txt', { create: false })
      const onDisk = await (await fh.getFile()).text()
      if (norm(onDisk) !== norm(openedText)) return 'changed'

      // ⭐ **書けてから置き換える。**`createWritable` は入れ物（スワップ）へ書き、
      //    `close()` で初めて本物へ差し替わる ── 開いた時点で中身を捨てる
      //    `open(p,"w")` の罠（`罠と教訓.md`）をブラウザの側が避けてくれる形。
      const w = await fh.createWritable()
      await w.write(newText)
      await w.close()
      return 'wrote'
    } catch (e) {
      return 'failed'
    }
  },

  // ── 段E: 絵のフォルダ（`unity\Assets\Resources\UI\icon` 相当）────────
  //
  // ⚠️ **骨組みのフォルダ（`this._root`）とは別の掴み**（`this._art`）── 骨組みと絵は
  // 別のフォルダを選ぶ想定（案内文で `unity\Assets\Resources\UI\icon` を選ぶよう明示する
  // のは C# 側・`EditPage.razor`）。GIF は対象外（`icon` は静止画の仕組み ── `*.png` だけ拾う）。

  /** ⭐ E-3: 絵のフォルダを選ばせる。以後そこから読み書きする。
   * @returns {Promise<boolean>} 選べたか（やめたら false）。 */
  async pickArt() {
    if (!this.supported()) return false
    try {
      this._art = await window.showDirectoryPicker({ id: 'egg-art', mode: 'readwrite' })
      return true
    } catch (e) {
      return false
    }
  },

  /** いま選んでいる絵のフォルダの名前。⚠️ 何も選んでいなければ空。 */
  artFolderName() {
    return this._art ? this._art.name : ''
  },

  /** フォルダの中の `*.png` を全部、{name, dataUrl} の配列で返す（拡張子を除いた name）。
   * ⚠️ **GIF は対象外**（`art/handmade/gif/*.gif` は動く絵 ── `icon` は静止画の仕組み
   * なので一覧に出さない）。⭐ 建て直さなくてもその場で使えるように、呼ぶたび
   * ディスクを読み直す（キャッシュしない ── 一覧が小さい前提の単純さを優先）。
   * @returns {Promise<{name:string, dataUrl:string}[]>} */
  async listArt() {
    if (!this._art) return []
    const out = []
    for await (const [filename, handle] of this._art.entries()) {
      if (handle.kind !== 'file') continue
      if (!/\.png$/i.test(filename)) continue
      try {
        const file = await handle.getFile()
        const dataUrl = await this._toDataUrl(file)
        out.push({ name: filename.slice(0, -4), dataUrl })
      } catch (e) {
        // ⚠️ 1枚読めなくても残りは出す（黙って一覧ごと空にしない）。
      }
    }
    return out
  },

  /** ⭐ `<name>.png` として絵のフォルダへ書く。⚠️ **同名は黙って上書きしない**
   * ── 既に在れば `name-2`,`name-3`… とずらす（`罠と教訓.md` と同じ「黙って壊さない」作法）。
   * @param {string} name 拡張子を除いた名前
   * @param {Uint8Array|ArrayBuffer} bytes PNG の生バイト列
   * @returns {Promise<string|null>} 実際に書いた名前（失敗したら null）。 */
  async writeArt(name, bytes) {
    if (!this._art) return null
    let finalName = name
    let i = 2
    while (await this._artHas(finalName)) { finalName = name + '-' + i; i++ }
    try {
      const fh = await this._art.getFileHandle(finalName + '.png', { create: true })
      const w = await fh.createWritable()
      await w.write(bytes)
      await w.close()
      return finalName
    } catch (e) {
      return null
    }
  },

  async _artHas(name) {
    if (!this._art) return false
    try {
      await this._art.getFileHandle(name + '.png', { create: false })
      return true
    } catch (e) {
      return false
    }
  },

  /** ⭐ E-3: 「絵を取り込む」── ふつうの `&lt;input type="file" accept="image/png" multiple&gt;`
   * から選ばれた PNG を、絵のフォルダへ複製する（`writeArt` を1枚ずつ通すので、
   * 同名は黙って上書きしない・ずらす、が自動で効く）。
   * @param {HTMLInputElement} input `EditPage.razor` の `@ref` で渡された素の DOM 要素。
   * @returns {Promise<string[]>} 実際に書けた名前の配列。 */
  async importFiles(input) {
    const written = []
    const files = input && input.files ? Array.from(input.files) : []
    for (const f of files) {
      if (!/\.png$/i.test(f.name)) continue   // ⚠️ PNG 以外（GIF 含む）は取り込まない
      const bytes = new Uint8Array(await f.arrayBuffer())
      const base = f.name.slice(0, -4)
      const name = await this.writeArt(base, bytes)
      if (name) written.push(name)
    }
    if (input) input.value = ''   // ⚠️ 同じファイルを続けて選び直せるように
    return written
  },

  /** `File` → data URL（`FileReader.readAsDataURL` の Promise 包み）。 */
  _toDataUrl(file) {
    return new Promise((resolve, reject) => {
      const r = new FileReader()
      r.onload = () => resolve(r.result)
      r.onerror = () => reject(r.error)
      r.readAsDataURL(file)
    })
  },
}
