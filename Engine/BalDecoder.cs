using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Text.Json;

namespace B4XContext.Engine
{
    /// <summary>
    /// Port of the .bal decoder from core.py. Provides basic decoding of the B4X layout format.
    /// This is a behavioral port focusing on the key->tag->value reading order and cached strings table.
    /// </summary>
    public static class BalDecoder
    {
        public static string Decode(byte[] data, bool full = true)
        {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            try
            {
                int version = br.ReadInt32();
                int headerSkipSize = br.ReadInt32();
                if (headerSkipSize > 0)
                    br.ReadBytes(headerSkipSize);

                var cache = new List<string>();
                if (version >= 3)
                {
                    int totalStrings = br.ReadInt32();
                    for (int i = 0; i < totalStrings; i++)
                        cache.Add(ReadString(br));
                }

                int numberOfVariants = br.ReadInt32();
                var variants = new List<Dictionary<string, object>>();
                for (int v = 0; v < numberOfVariants; v++)
                {
                    float scale = br.ReadSingle();
                    int width = br.ReadInt32();
                    int height = br.ReadInt32();
                    variants.Add(new Dictionary<string, object> { { "scale", scale }, { "width", width }, { "height", height } });
                }

                var rawTree = ReadMapIndexedKeys(br, cache);
                var rootName = rawTree.ContainsKey("name") ? rawTree["name"]?.ToString() ?? "Activity" : "Activity";
                var clean = BuildCleanNode(rootName, rawTree, 0);

                if (full)
                {
                    var doc = new Dictionary<string, object>
                    {
                        { "balVersion", version },
                        { "variants", variants },
                        { "layoutTree", clean }
                    };
                    var opts = new JsonSerializerOptions { WriteIndented = true };
                    return JsonSerializer.Serialize(doc, opts);
                }
                else
                {
                    var lines = new List<string> { $"# Layout - version {version}, variants: {variants.Count}" };
                    FlattenLayoutOutline(clean, 0, lines);
                    return string.Join('\n', lines);
                }
            }
            catch (EndOfStreamException)
            {
                return "";
            }
        }

        private static Dictionary<string, object> ReadMapIndexedKeys(BinaryReader br, List<string> cache)
        {
            var dict = new Dictionary<string, object>();
            int guard = 0;
            while (true)
            {
                guard++;
                if (guard > 200000) throw new Exception("Iteration limit exceeded");

                int keyIdx = br.ReadInt32();
                string key = (cache != null && keyIdx >= 0 && keyIdx < cache.Count) ? cache[keyIdx] : $"[Cache {keyIdx}]";
                byte tag = br.ReadByte();
                if (tag == 4) // ENDOFMAP
                    break;

                object val = null;
                switch (tag)
                {
                    case 1: // INT
                    case 8: // SCALED_INT
                        val = br.ReadInt32();
                        break;
                    case 2: // STRING
                        val = ReadString(br);
                        break;
                    case 9: // CACHED_STRING
                        int idx = br.ReadInt32();
                        val = (cache != null && idx >= 0 && idx < cache.Count) ? cache[idx] : $"[Cache {idx}]";
                        break;
                    case 3: // MAP
                        val = ReadMapIndexedKeys(br, cache);
                        break;
                    case 5: // BOOL
                        val = br.ReadByte() == 1;
                        break;
                    case 7: // FLOAT
                        val = br.ReadSingle();
                        break;
                    case 6: // COLOR
                        byte a = br.ReadByte();
                        byte r = br.ReadByte();
                        byte g = br.ReadByte();
                        byte b = br.ReadByte();
                        val = new Dictionary<string, int> { { "a", a }, { "r", r }, { "g", g }, { "b", b } };
                        break;
                    case 11: // RECT32
                        short l = br.ReadInt16();
                        short t = br.ReadInt16();
                        short w = br.ReadInt16();
                        short h = br.ReadInt16();
                        val = new int[] { l, t, w, h };
                        break;
                    case 12: // NULL
                        val = null;
                        break;
                    default:
                        // skip unknown
                        try { br.ReadInt32(); } catch { }
                        val = null;
                        break;
                }

                dict[key] = val;
            }

            return dict;
        }

        private static string ReadString(BinaryReader br)
        {
            int len = br.ReadInt32();
            if (len < 0) return null;
            if (len == 0) return string.Empty;
            var bytes = br.ReadBytes(len);
            return Encoding.UTF8.GetString(bytes);
        }

        private static Dictionary<string, object> BuildCleanNode(string name, Dictionary<string, object> raw, int variantIndex = 0)
        {
            var kids = new List<Dictionary<string, object>>();
            if (raw.TryGetValue(":kids", out var kidsRaw) && kidsRaw is Dictionary<string, object> kraw)
            {
                var keys = kraw.Keys.ToList();
                keys.Sort((a, b) =>
                {
                    bool ad = int.TryParse(a, out var ai);
                    bool bd = int.TryParse(b, out var bi);
                    if (ad && bd) return ai.CompareTo(bi);
                    return 0;
                });
                foreach (var k in keys)
                {
                    if (kraw[k] is Dictionary<string, object> childRaw)
                    {
                        var childName = childRaw.ContainsKey("name") ? childRaw["name"]?.ToString() ?? k : k;
                        kids.Add(BuildCleanNode(childName, childRaw, variantIndex));
                    }
                }
            }

            var pos = ResolvePosition(raw, variantIndex);
            string javaType = raw.ContainsKey("javaType") ? raw["javaType"]?.ToString() : raw.ContainsKey("type") ? raw["type"]?.ToString() : "?";
            if (javaType != null) javaType = javaType.TrimStart('.');

            var node = new Dictionary<string, object>
            {
                { "name", name },
                { "type", javaType },
                { "left", pos.ContainsKey("left") ? pos["left"] : 0 },
                { "top", pos.ContainsKey("top") ? pos["top"] : 0 },
                { "width", pos.ContainsKey("width") ? pos["width"] : 0 },
                { "height", pos.ContainsKey("height") ? pos["height"] : 0 },
                { "visible", raw.ContainsKey("visible") ? raw["visible"] : true }
            };

            var extra = new[] { "text", "hint", "tag", "drawable", "eventName", "hanchor", "vanchor" };
            foreach (var ex in extra)
            {
                if (raw.ContainsKey(ex)) node[ex] = raw[ex];
            }

            if (kids.Any()) node["kids"] = kids;
            return node;
        }

        private static Dictionary<string, int> ResolvePosition(Dictionary<string, object> node, int variantIndex = 0)
        {
            var dict = new Dictionary<string, int>();
            string variantKey = $"variant{variantIndex}";
            if (node.TryGetValue(variantKey, out var v) && v is Dictionary<string, object> vdict)
            {
                dict["left"] = GetInt(vdict, "left", GetInt(node, "left", 0));
                dict["top"] = GetInt(vdict, "top", GetInt(node, "top", 0));
                dict["width"] = GetInt(vdict, "width", GetInt(node, "width", 0));
                dict["height"] = GetInt(vdict, "height", GetInt(node, "height", 0));
            }
            else
            {
                dict["left"] = GetInt(node, "left", 0);
                dict["top"] = GetInt(node, "top", 0);
                dict["width"] = GetInt(node, "width", 0);
                dict["height"] = GetInt(node, "height", 0);
            }
            return dict;
        }

        private static int GetInt(Dictionary<string, object> d, string k, int def)
        {
            if (d.TryGetValue(k, out var v) && v is int vi) return vi;
            return def;
        }

        private static void FlattenLayoutOutline(Dictionary<string, object> node, int depth, List<string> outLines)
        {
            string indent = new string(' ', depth * 2);
            var w = node.ContainsKey("width") ? node["width"] : "?";
            var h = node.ContainsKey("height") ? node["height"] : "?";
            var x = node.ContainsKey("left") ? node["left"] : 0;
            var y = node.ContainsKey("top") ? node["top"] : 0;
            outLines.Add($"{indent}- {node["name"]} ({node.GetValueOrDefault("type", "?")}) {w}x{h} @ ({x},{y})");
            if (node.TryGetValue("kids", out var kidsObj) && kidsObj is List<Dictionary<string, object>> kids)
            {
                foreach (var child in kids) FlattenLayoutOutline(child, depth + 1, outLines);
            }
        }
    }
}
