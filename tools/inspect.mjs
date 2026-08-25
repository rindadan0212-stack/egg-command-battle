#!/usr/bin/env node
/** 画面を**数で**検査する。⭐ Unity の `InspectScreens` の web 版。
 *
 *  ⚠️ **スクショで被りを判定しない。**枠の関係は数で見る
 *  （罠: viewport 全体のスクショは読み戻すと縮んで届き、被りが見えない）。
 *
 *  ⭐ Unity 版が見ていた5つのうち、
 *  - 字の重なり / 字が枠より広い / 枠からはみ出し / 画面の外  → ここで見る
 *  - 覆われて見えない → `elementsFromPoint` で**実際の合成結果**を見る（Unity より強い）
 *
 *  ⭐ そして Unity 版に無かったものを1つ足す:
 *  - **id の重複** ── DOM では一意でなければ、検査も指し示しも効かない
 *
 *  ⚠️ **枝も1枚と数える。**⭐ 開いた並べ替え・空の親枠・放置の編成は
 *  `when=` で中身が入れ替わるので、**閉じた側しか見ない検査は嘘をつく**。
 *  だから URL に状態を出してある（`?open=true` など）。
 *
 *  使い方: node tools/inspect.mjs [URL] [path ...]
 */

import { chromium } from 'playwright'
import { audit } from './audit.mjs'

const args = process.argv.slice(2)
const URL = (args[0] && args[0].startsWith('http') ? args.shift() : 'http://localhost:5817')
  .replace(/\/$/, '')

/** 調べる画面。⚠️ 足したら必ずここに入れる（入れないと「0件」が痩せる）。 */
const PAGES = args.length ? args : [
  '/',                    // 図鑑
  '/?full=true',          // 🔴 図鑑・あふれ（保管庫満杯の盤で「手に入れた種族」を見る）
  // ⚠️ **勝った段が1つも無い状態しか見ない検査は嘘をつく**
  //    （`when=beaten` の枝を一度も描かないまま「0件」と言う）。
  '/trial?won=2',         // 試練（勝った段あり）
  '/trial?won=0',         // 試練（まだ勝っていない）
  '/trial?full=true',     // 🔴 試練・あふれ（全5段クリア済み）
  '/ask',                 // 確かめる札
  '/ask?full=true',       // 🔴 確かめる札・あふれ（後ろの図鑑もあふれの盤）
  '/box',                 // BOX（畳んだ）
  '/box?open=true',          // BOX（開いた）
  '/box?full=true',       // 🔴 BOX・あふれ（保管庫 50/50・極まった個体の詳細）
  '/box?full=true&open=true', // 🔴 BOX・あふれ（並べ替えを開いた形）
  // ⚠️ `picked` は**必ず書く**。⭐ Blazor は問い合わせに無い値を
  //    型の既定（0）で上書きするので、省くと**親なしの枝しか見ない**
  //    （実測 2026-08-22: `/breed` と `/breed?picked=0` が同じ 80 部品だった）。
  '/breed?picked=2',      // 配合（親2体）
  '/breed?picked=1',      // 配合（片方だけ）
  '/breed?picked=0',      // 配合（親なし）
  '/breed?picked=2&open=true',   // 配合（開いた）
  '/breed?picked=2&full=true',   // 🔴 配合・あふれ（親2体が極まった個体）
  '/party',               // 編成（巣）
  '/party?open=true',        // 編成（巣・開いた）
  '/party?idle=true',        // 編成（放置）
  '/party?full=true',        // 🔴 編成・あふれ（Lv200・極まった個体の一覧）
  // ⚠️ 図鑑の中の2枚。⭐ **一番長いもの**を選んで見る
  //    （技の袋は 1〜5種・説明文は 1〜2行）。
  '/species?at=0',
  '/species?at=7',
  '/species?full=true',   // 🔴 種族・あふれ（特性の一言＋名乗りがいちばん長い種族）
  '/skill?at=0',
  '/skill?at=12&slot=0',
  '/skill?full=true',     // 🔴 技・あふれ（名前＋効果文がいちばん長い技・Lv5満タン）
  // ⭐ ホーム。⚠️ 空の枠と入っている枠を両方見る
  '/home?eggs=3',
  '/home?eggs=0',
  '/home?eggs=6',
  '/home?full=true',      // 🔴 ホーム・あふれ（孵化器満杯・溜まった EXP が大きい）
  // ⭐ 探索と卵の在庫。⚠️ **減ったとき**も見る
  '/nests',
  '/nests?shown=1&raids=4',
  '/nests?full=true',     // 🔴 探索・あふれ（盗んだ回数が封鎖の閾値）
  '/eggs?have=7',
  '/eggs?have=0',
  '/eggs?full=true',      // 🔴 卵の在庫・あふれ（50個・全部★5）
  // ⭐ 分解と技を鍛える。⚠️ **選んでいない状態**と**候補が0件**も見る
  '/fuse?picked=3',
  '/fuse?picked=0',
  '/fuse?empty=true',
  '/fuse?full=true',      // 🔴 分解・あふれ（まとめ選びの上限・EXP の実測が大きい）
  '/train?picked=3',
  '/train?picked=0&have=0',
  '/train?full=true',     // 🔴 技を鍛える・あふれ（卵50個・★5・まとめ選びの上限）
  // ⭐ 保存の控え。⚠️ **まだ1度も書かれていない形**も見る（字が丸ごと入れ替わる）
  '/save?size=11024&past=5',
  '/save?size=0&past=0',
  '/save?full=true',      // 🔴 保存の控え・あふれ（あふれの盤を実際に書き出した字数）
  // ⭐ **外枠付きの本体**。⚠️ 上のバーと下の帯が乗った状態で見る
  // ⚠️ 種を固定する。⭐ 毎回違う画面を撮ると、差が「直したから」なのか
  //    「引きが違うから」なのか分からなくなる。
  // ⚠️ **外枠は画面ごとに中身が変わる**（‹ が出る／右肩が押しどころになる）。
  //    ⭐ ホームだけ見ていると、残りの形を誰も見ていないことになる。
  '/app?seed=20260822',
  '/app?seed=20260822&at=nests',
  '/app?seed=20260822&at=box',
  '/app?seed=20260822&at=book',
  '/app?seed=20260822&at=trial',
  // 🔴 あふれ。⚠️ 下の帯は4タブとも常に数を出す（「50体」「50/50」）ので、
  //    どのタブでも保管庫のあふれが見える。box・breed は右肩の EXP 表示も重なる
  //    （wiki §11 が指した「EXP 19,475　44/50」の実物）。
  '/app?seed=20260822&full=true',
  '/app?seed=20260822&at=box&full=true',
  '/app?seed=20260822&at=breed&full=true',
  // ⚠️ **札は押しどころからしか開かないので、静かな検査は重なった形を見ない。**
  //    ⭐ そこで id がぶつかっていた（技の詳細の `body` と本体の器の `body`）。
  '/app?seed=20260822&open=party',
  '/app?seed=20260822&open=fuse',
  '/app?seed=20260822&open=train',
  '/app?seed=20260822&open=eggs',
  '/app?seed=20260822&open=keep',
  // ⚠️ **演出は一瞬しか出ない。**⭐ 止めた形で開いて、置き方だけ見る
  '/app?seed=20260822&open=dice',
  // ⭐ Fanfare は閉じるまで出しっぱなしなので、静かな検査でもそのまま見える。
  //    卵（★が出る方）と誕生（★が出ない方）の両方を見る。
  '/app?seed=20260822&open=fanfare',
  '/app?seed=20260822&open=fanfareborn',
  '/fight?done=true&banner=win',
  '/app?seed=20260822&at=book&open=species',
  '/app?seed=20260822&at=book&open=skill',
  // ⭐ 戦闘。⚠️ 決着した枝も見る（札が入れ替わる）
  '/fight',
  '/fight?done=true',
  '/fight?full=true',     // 🔴 戦闘・あふれ（極まった編成＋状態異常を全部背負った1体）
  // ⭐ すごろくの盤
  '/raid',
  '/raid?raids=2',
  '/raid?full=true',      // 🔴 すごろく・あふれ（さいころの数が12個を超える）
]

/** ⚠️ 実機の幅は 320〜430。⭐ 一番狭いところで測るのが要点（罠22・24・26）。 */
const SIZES = [
  { w: 320, h: 568, name: 'SE1' },
  { w: 390, h: 844, name: 'iPhone 14' },
  { w: 430, h: 932, name: '15 Pro Max' },
]

const browser = await chromium.launch()
// ⚠️ **器は1つだけ作る。**⭐ 画面ごとに新しい器を作ると WASM を毎回落とし直す
//    （実測 2026-08-22: 33回の読み直しで6分経っても終わらなかった）。
const context = await browser.newContext({ viewport: { width: 390, height: 844 } })
const page = await context.newPage()
let total = 0
let thin = 0

for (const path of PAGES) {
  console.log(`\n━━ ${path}`)
  for (const size of SIZES) {
    await page.setViewportSize({ width: size.w, height: size.h })
    await page.goto(URL + path)
    await page.waitForFunction(() => document.querySelectorAll('#stage .n').length > 0,
      null, { timeout: 30000 }).catch(() => {})
    // ⚠️ 🔴 **字が届く前に測らない。**⭐ 代替フォントの幅で答えが出るので、
    //    **置きが温まっているかどうかで件数が変わる**（実測 2026-08-22:
    //    同じ画面が、単独なら 0件・通しなら 10件）。
    await page.evaluate(() => document.fonts.ready).catch(() => {})
    const bad = await page.evaluate(audit)
    const parts = await page.evaluate(() => document.querySelectorAll('#stage .n').length)
    total += bad.length
    // ⚠️ **部品が少なすぎる＝描けていない。**⭐ 「不備なし」と区別する
    if (parts < 4) { thin++; console.log(`  🔴 ${size.name}: 部品が ${parts} 個しか無い`) }
    else if (!bad.length) console.log(`  ⭐ ${size.name}: 不備なし（${parts}）`)
    else {
      console.log(`  ⚠️ ${size.name}: ${bad.length} 件（${parts}）`)
      for (const line of bad) console.log('     ' + line)
    }
  }
}
await browser.close()
console.log(`\n${PAGES.length} 画面 × ${SIZES.length} サイズ ── 不備 ${total} 件`
  + (thin ? ` / 🔴 描けていない ${thin} 件` : ''))
process.exit(total || thin ? 1 : 0)
