// 骨組みエディタ（`/edit`）専用: ディスクとの出し入れ（File System Access API）。
//
// ⚠️ **遊ぶ頁（`/app`）は使わない。**⭐ 遊びの読み書きは `save.js`（localStorage）が持つ。
// こちらが触るのは `unity/Assets/Resources/Layouts/*.txt`（の写し）── エディタが選んだ
// フォルダの中の `<name>.txt` だけ。
//
// ⭐ **作者自身の別作品（`r18/1/_shared/tools/studio/STUDIO.html`）が同じ形で
//    showDirectoryPicker → getFileHandle → createWritable をやっている。**
//    ここはその形をそのまま借りる（コードは写さず、書き方だけ真似る）。

window.eggDisk = {
  /** 対応しているブラウザか。⚠️ **押しどころを殺す判定はここを見て C# 側がする**
   * （黙って何も起きない、にしない）。 */
  supported() {
    return 'showDirectoryPicker' in window
  },

  /** ⭐ 骨組みの入っているフォルダを選ばせる。以後そこから読み書きする。
   * @returns {Promise<boolean>} 選べたか（やめたら false）。 */
  async pick() {
    if (!this.supported()) return false
    try {
      this._root = await window.showDirectoryPicker({ id: 'egg-layouts', mode: 'readwrite' })
      return true
    } catch (e) {
      // ⚠️ ユーザーがキャンセルすると AbortError が飛ぶ ── これは失敗ではない
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
}
