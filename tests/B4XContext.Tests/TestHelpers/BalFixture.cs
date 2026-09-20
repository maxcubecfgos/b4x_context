using System;
using System.IO;
using System.Text;

namespace B4XContext.Tests
{
    /// <summary>
    /// Builds a minimal handcrafted .bal layout binary following the exact byte
    /// layout read by BalDecoder (version, headerSkipSize, string cache, variants,
    /// indexed-key map). Used as test fixture for BalDecoder and the bundle flow.
    /// </summary>
    internal static class BalFixture
    {
        private static readonly string[] Cache =
        {
            "name", "Main", "javaType", "android.view.View", ":kids", "0",
            "android.widget.Button", "text", "Click me", "left", "top", "width", "height", "Button1"
        };

        public static byte[] Minimal()
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);

            w.Write(3);                       // version
            w.Write(0);                       // headerSkipSize

            w.Write(Cache.Length);            // string cache
            foreach (var s in Cache) WriteString(w, s);

            w.Write(1);                       // numberOfVariants
            w.Write(1.0f);                    // scale
            w.Write(320);                     // width
            w.Write(480);                     // height

            // Root view map
            BeginMap(w);
            CachedString(w, 0);               // key "name"
            Tag(w, 9); w.Write(1);            // cached "Main"
            CachedString(w, 2);               // key "javaType"
            Tag(w, 9); w.Write(3);            // cached "android.view.View"
            CachedString(w, 9); Tag(w, 1); w.Write(0);    // left
            CachedString(w, 10); Tag(w, 1); w.Write(0);   // top
            CachedString(w, 11); Tag(w, 1); w.Write(320); // width
            CachedString(w, 12); Tag(w, 1); w.Write(480); // height

            CachedString(w, 4);               // key ":kids"
            Tag(w, 3);
            CachedString(w, 5);               // key "0"
            Tag(w, 3);
            CachedString(w, 0); Tag(w, 9); w.Write(13);    // "Button1"
            CachedString(w, 2); Tag(w, 9); w.Write(6);     // "android.widget.Button"
            CachedString(w, 7); Tag(w, 9); w.Write(8);     // "Click me"
            CachedString(w, 9); Tag(w, 1); w.Write(10);    // left
            CachedString(w, 10); Tag(w, 1); w.Write(20);   // top
            CachedString(w, 11); Tag(w, 1); w.Write(100);  // width
            CachedString(w, 12); Tag(w, 1); w.Write(40);   // height
            w.Write(-1); Tag(w, 4);       // key terminator + ENDOFMAP (kid map)
            w.Write(-1); Tag(w, 4);       // key terminator + ENDOFMAP (:kids map)
            w.Write(-1); Tag(w, 4);       // key terminator + ENDOFMAP (root map)

            return ms.ToArray();
        }

        public static byte[] TruncatedEarly()
        {
            var bytes = Minimal();
            Array.Resize(ref bytes, bytes.Length / 2);
            return bytes;
        }

        public static string WriteTempBal(string dir)
        {
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "Layout.bal");
            File.WriteAllBytes(path, Minimal());
            return path;
        }

        private static void WriteString(BinaryWriter w, string s)
        {
            var b = Encoding.UTF8.GetBytes(s);
            w.Write(b.Length);
            w.Write(b);
        }

        private static void BeginMap(BinaryWriter w) { }

        private static void CachedString(BinaryWriter w, int idx) => w.Write(idx);

        private static void Tag(BinaryWriter w, byte tag) => w.Write(tag);
    }
}