using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using EggCommand.Core;

namespace EggCommand.Web
{
    /// <summary>骨組みを読む。⭐ **唯一の出所は Unity 側の `Assets/Resources/Layouts`**
    /// （csproj が埋め込む。ここへ写さない）。
    ///
    /// ⚠️ HTTP で取りに行かない ── dev サーバが 200 を返しながら **0 バイト**を返す形があった
    /// （2026-08-22 に実測）。⭐ 埋め込みなら配信先が何であっても同じものが読める。</summary>
    public static class LayoutStore
    {
        private static readonly Dictionary<string, EggCommand.Core.Layout> Cache = new Dictionary<string, Layout>();

        /// <summary>⭐ **エディタ専用**（`/edit`）の差し替え。ディスクから読んだ生の字で、
        /// 次の <see cref="Of"/> の出所を一時的に埋め込み資源から差し替える。
        ///
        /// ⚠️ **遊ぶ頁（`/app`）はこれを一切呼ばない。**⭐ 呼ぶのはエディタ（`EditPage`）だけ
        /// ── 呼ばれない限り、`Of` は今までどおり埋め込みだけを読む。
        /// ⚠️ `text` に null を渡すと差し替えを解いて埋め込みへ戻る。</summary>
        private static readonly Dictionary<string, string> Overrides = new Dictionary<string, string>();

        public static void SetOverride(string id, string? text)
        {
            if (text == null) Overrides.Remove(id); else Overrides[id] = text;
            // 🔴 **`id` だけでなく、キャッシュを丸ごと捨てる。**
            //    ⚠️ 実測 2026-08-23: `Cache.Remove(id)` だけだと、`id` が部品
            //    （`cell` 等）で、その部品を `use=` で差している土台（`box` 等）が
            //    既に解決済みでキャッシュに載っているとき、土台の中に**古い部品が
            //    焼き込まれたまま**残る ── `Resolve`/`Splice` は差した瞬間の部品の中身を
            //    「インライン展開」するので、部品だけを読み直させても土台には効かない。
            //    ⭐ どの土台がどの部品を差しているかを逆引きせず、単純にキャッシュを
            //    空にする ── 骨組みは数十行の小さな字なので、直後の1回だけ余分に
            //    Parse+Resolve しても実用上のコストにならない（エディタの操作でしか呼ばれない）。
            Cache.Clear();
        }

        public static EggCommand.Core.Layout Of(string id)
        {
            if (Cache.TryGetValue(id, out var found)) return found;

            string text;
            if (Overrides.TryGetValue(id, out var overridden))
            {
                text = overridden;
            }
            else
            {
                var asm = typeof(LayoutStore).Assembly;
                // ⚠️ 名前は「アセンブリ名.Layouts.<id>.txt」になる
                string path = asm.GetName().Name + ".Layouts." + id + ".txt";
                using var stream = asm.GetManifestResourceStream(path);
                if (stream == null)
                {
                    // ⚠️ 黙って空を返さない。⭐ 何が無いのかを名前ごと言う
                    var had = string.Join(", ", asm.GetManifestResourceNames());
                    throw new FileNotFoundException($"骨組みが無い: {path}（在るもの: {had}）");
                }
                using var reader = new StreamReader(stream);
                text = reader.ReadToEnd();
            }

            // ⭐ `use=` を先に差し替える（Unity 版と同じ約束）
            found = Core.Layouts.Resolve(Core.Layouts.Parse(id, text), Of);

            // ⚠️ **読んだ場で検査する。**⭐ アセットだけ直してテストを回し忘れたときに拾う
            foreach (var line in Core.Layouts.Faults(found))
                Console.Error.WriteLine("骨組み: " + line);

            Cache[id] = found;
            return found;
        }
    }
}
