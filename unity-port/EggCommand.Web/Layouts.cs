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

        public static EggCommand.Core.Layout Of(string id)
        {
            if (Cache.TryGetValue(id, out var found)) return found;

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
            found = Core.Layouts.Parse(id, reader.ReadToEnd());

            // ⚠️ **読んだ場で検査する。**⭐ アセットだけ直してテストを回し忘れたときに拾う
            foreach (var line in Core.Layouts.Faults(found))
                Console.Error.WriteLine("骨組み: " + line);

            Cache[id] = found;
            return found;
        }
    }
}
