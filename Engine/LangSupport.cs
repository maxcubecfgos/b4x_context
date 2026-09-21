using System;
using System.Collections.Generic;

namespace B4XContext.Engine
{
    public enum StructuralFamily
    {
        None,
        Generic,
        Config,
        Markdown
    }

    /// <summary>Which skeleton generator handles the language.</summary>
    public enum SkeletonKind
    {
        /// <summary>tree-sitter grammar (GrammarId) with structural fallback.</summary>
        TreeSitter,
        /// <summary>Structural line compressor only (no grammar available).</summary>
        Structural,
        /// <summary>Dedicated hand-written parser (Engine/DartSkeletonizer).</summary>
        Dart
    }

    public sealed class LangProfile
    {
        public string GrammarId { get; init; }
        public string FenceTag { get; init; }
        public string CommentPrefix { get; init; }
        public string CommentSuffix { get; init; }
        public bool IsRawOnly { get; init; }
        public bool HeaderOnly { get; init; }
        public HashSet<string> DeclKinds { get; init; } = new HashSet<string>();
        public HashSet<string> ContainerDeclKinds { get; init; } = new HashSet<string>();
        public HashSet<string> ImportKinds { get; init; } = new HashSet<string>();
        public HashSet<string> BodyKinds { get; init; } = new HashSet<string>();
        public StructuralFamily Family { get; init; } = StructuralFamily.None;
        public SkeletonKind Kind { get; init; } = SkeletonKind.TreeSitter;

        public string MakeComment(string text) => CommentPrefix + text + CommentSuffix;
        public string MakeOmitMarker(int lines) => MakeComment($"... ({lines} lines omitted) ...");
        public string MakeTruncatedMarker() => MakeComment("... (skeleton truncated) ...");
    }

    public static class LangSupport
    {
        private static readonly Dictionary<string, LangProfile> ExtMap =
            new Dictionary<string, LangProfile>(StringComparer.OrdinalIgnoreCase);

        static LangSupport()
        {
            RegisterTs();
            RegisterJs();
            RegisterPython();
            RegisterRust();
            RegisterGo();
            RegisterC();
            RegisterCsharp();
            RegisterJava();
            RegisterDart();
            RegisterRawText();
            RegisterStyleMarkup();
            RegisterXmlProject();
        }

        public static LangProfile FromExtension(string ext)
        {
            var key = (ext ?? "").TrimStart('.').ToLowerInvariant();
            if (ExtMap.TryGetValue(key, out var profile))
                return profile;
            return DefaultProfile(key);
        }

        public static LangProfile DefaultProfile(string ext)
        {
            return new LangProfile
            {
                GrammarId = null,
                FenceTag = ext,
                CommentPrefix = "//",
                CommentSuffix = "",
                IsRawOnly = false,
                HeaderOnly = false,
                Family = FamilyFor(ext),
                Kind = SkeletonKind.Structural
            };
        }

        private static StructuralFamily FamilyFor(string ext)
        {
            switch (ext)
            {
                case "lock":
                    return StructuralFamily.Generic;
                case "toml":
                case "ini":
                case "cfg":
                case "conf":
                case "env":
                case "properties":
                    return StructuralFamily.Config;
                case "md":
                case "markdown":
                    return StructuralFamily.Markdown;
                default:
                    return StructuralFamily.Generic;
            }
        }

        public static string FenceTagFor(string ext) => FromExtension(ext).FenceTag;

        private static void RegisterPython()
        {
            var decl = Decl("function_definition", "class_definition", "decorated_definition");
            RegisterBase("py", "python", "py", "# ", "", false, false,
                decl, Container("class_definition", "decorated_definition"), Imports("import_statement", "import_from_statement"), Bodies("block"));
            CopyBase("pyw", "py", "python", "py");
            CopyBase("pyi", "py", "python", "py");
        }

        private static void RegisterTs()
        {
            var decl = Decl("function_declaration", "class_declaration", "abstract_class_declaration",
                "interface_declaration", "enum_declaration", "type_alias_declaration", "method_definition",
                "internal_module", "module", "lexical_declaration");
            RegisterBase("ts", "typescript", "ts", "// ", "", false, false, decl,
                Container("class_declaration", "abstract_class_declaration", "interface_declaration",
                    "enum_declaration", "internal_module", "module"),
                Imports("import_statement"), Bodies("statement_block", "class_body", "declaration_list"));
            CopyBase("mts", "ts");
            CopyBase("cts", "ts");
            CopyBase("tsx", "ts", "tsx", "tsx");
        }

        private static void RegisterJs()
        {
            var decl = Decl("function_declaration", "class_declaration", "method_definition",
                "lexical_declaration", "generator_function_declaration");
            RegisterBase("js", "javascript", "js", "// ", "", false, false, decl,
                Container("class_declaration"), Imports("import_statement"), Bodies("statement_block", "class_body"));
            CopyBase("mjs", "js");
            CopyBase("cjs", "js");
            CopyBase("jsx", "js", null, "jsx");
        }

        private static void RegisterRust()
        {
            var decl = Decl("function_item", "struct_item", "enum_item", "trait_item", "impl_item",
                "mod_item", "type_item", "static_item", "const_item");
            RegisterBase("rs", "rust", "rs", "// ", "", false, false, decl,
                Container("struct_item", "enum_item", "trait_item", "impl_item", "mod_item"),
                Imports("use_declaration"), Bodies("block"));
        }

        private static void RegisterGo()
        {
            var decl = Decl("function_declaration", "method_declaration", "type_declaration", "type_spec", "package_clause");
            RegisterBase("go", "go", "go", "// ", "", false, false, decl,
                Container("type_declaration"), Imports("import_declaration"), Bodies("block"));
        }

        private static void RegisterC()
        {
            var decl = Decl("function_definition", "declaration", "struct_specifier", "enum_specifier",
                "union_specifier", "preproc_def");
            RegisterBase("c", "c", "c", "// ", "", false, false, decl,
                Container("struct_specifier", "enum_specifier", "union_specifier"),
                Imports("preproc_include"), Bodies("compound_statement", "field_declaration_list"));
            CopyBase("h", "c");
        }

        private static void RegisterCsharp()
        {
            var decl = Decl("namespace_declaration", "file_scoped_namespace_declaration", "class_declaration",
                "record_declaration", "struct_declaration", "interface_declaration", "enum_declaration",
                "method_declaration", "constructor_declaration", "property_declaration",
                "delegate_declaration", "event_declaration");
            RegisterBase("cs", "csharp", "cs", "// ", "", false, false, decl,
                Container("namespace_declaration", "file_scoped_namespace_declaration", "class_declaration",
                    "record_declaration", "struct_declaration", "interface_declaration", "enum_declaration"),
                Imports("using_directive"), Bodies("block", "declaration_list", "class_body", "enum_body"));
        }

        private static void RegisterJava()
        {
            var decl = Decl("class_declaration", "interface_declaration", "enum_declaration", "record_declaration",
                "annotation_type_declaration", "method_declaration", "constructor_declaration",
                "field_declaration", "static_initializer");
            RegisterBase("java", "java", "java", "// ", "", false, false, decl,
                Container("class_declaration", "interface_declaration", "enum_declaration", "record_declaration",
                    "annotation_type_declaration"),
                Imports("package_declaration", "import_declaration"),
                Bodies("class_body", "interface_body", "enum_body", "annotation_type_body", "constructor_body", "block"));
        }

        private static void RegisterDart()
        {
            // Dart has no native grammar in TreeSitter.DotNet 1.3.0 (there is no
            // tree-sitter-dart.dll in the package), so .dart files go through
            // MultiLangSkeletonizer.StructuralCompress (imports, class/mixin/enum
            // headers, function signatures, annotations).
            RegisterHandwritten("dart", "dart", StructuralFamily.Generic, SkeletonKind.Dart);
        }

        private static void RegisterRawText()
        {
            RegisterBase("json", "json", "json", "/* ", " */", true, false,
                null, null, null, null);
            CopyBase("jsonc", "json");
        }

        private static void RegisterStyleMarkup()
        {
            RegisterBase("css", "css", "css", "/* ", " */", false, true,
                Decl("rule_set", "at_rule", "media_statement"), Container("at_rule", "media_statement"), null, null);
            RegisterBase("scss", null, "scss", "/* ", " */", false, true,
                Decl("rule_set", "at_rule", "media_statement", "variable_declaration"), Container("at_rule", "media_statement"), null, null);
            RegisterBase("less", null, "less", "/* ", " */", false, true,
                Decl("rule_set", "at_rule", "media_statement", "variable_declaration"), Container("at_rule", "media_statement"), null, null);
            RegisterBase("html", "html", "html", "<!-- ", " -->", false, true,
                Decl("element", "doctype"), null, null, null);
            CopyBase("htm", "html");
        }

        private static void RegisterXmlProject()
        {
            RegisterStructural("xaml", "xml");
            RegisterStructural("csproj", "xml");
            RegisterStructural("slnx", "xml");
            RegisterStructural("sln", "text");
        }

        private static void RegisterStructural(string ext, string fenceTag)
        {
            ExtMap[ext] = new LangProfile
            {
                GrammarId = null,
                FenceTag = fenceTag,
                CommentPrefix = "//",
                CommentSuffix = "",
                IsRawOnly = false,
                HeaderOnly = false,
                Family = StructuralFamily.Generic,
                Kind = SkeletonKind.Structural
            };
        }

        /// <summary>Language with a dedicated hand-written parser (Dart).</summary>
        private static void RegisterHandwritten(string ext, string fenceTag, StructuralFamily family, SkeletonKind kind)
        {
            ExtMap[ext] = new LangProfile
            {
                GrammarId = null,
                FenceTag = fenceTag,
                CommentPrefix = "// ",
                CommentSuffix = "",
                IsRawOnly = false,
                HeaderOnly = false,
                Family = family,
                Kind = kind
            };
        }

        private static void RegisterBase(string ext, string grammarId, string fenceTag, string prefix, string suffix,
            bool rawOnly, bool headerOnly, HashSet<string> decl, HashSet<string> container,
            HashSet<string> imports, HashSet<string> bodies,
            StructuralFamily family = StructuralFamily.None)
        {
            ExtMap[ext] = new LangProfile
            {
                GrammarId = grammarId,
                FenceTag = fenceTag,
                CommentPrefix = prefix,
                CommentSuffix = suffix,
                IsRawOnly = rawOnly,
                HeaderOnly = headerOnly,
                DeclKinds = decl ?? new HashSet<string>(),
                ContainerDeclKinds = container ?? new HashSet<string>(),
                ImportKinds = imports ?? new HashSet<string>(),
                BodyKinds = bodies ?? new HashSet<string>(),
                Family = family,
                Kind = grammarId == null ? SkeletonKind.Structural : SkeletonKind.TreeSitter,
            };
        }

        private static void CopyBase(string ext, string baseExt, string grammarId = null, string fenceTag = null)
        {
            var src = ExtMap[baseExt];
            ExtMap[ext] = new LangProfile
            {
                GrammarId = grammarId ?? src.GrammarId,
                FenceTag = fenceTag ?? src.FenceTag,
                CommentPrefix = src.CommentPrefix,
                CommentSuffix = src.CommentSuffix,
                IsRawOnly = src.IsRawOnly,
                HeaderOnly = src.HeaderOnly,
                DeclKinds = new HashSet<string>(src.DeclKinds),
                ContainerDeclKinds = new HashSet<string>(src.ContainerDeclKinds),
                ImportKinds = new HashSet<string>(src.ImportKinds),
                BodyKinds = new HashSet<string>(src.BodyKinds),
                Family = src.Family,
                Kind = src.Kind,
            };
        }

        private static HashSet<string> Decl(params string[] kinds) => new HashSet<string>(kinds);
        private static HashSet<string> Container(params string[] kinds) => new HashSet<string>(kinds);
        private static HashSet<string> Imports(params string[] kinds) => new HashSet<string>(kinds);
        private static HashSet<string> Bodies(params string[] kinds) => new HashSet<string>(kinds);
    }
}