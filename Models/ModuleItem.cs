using System;

namespace B4XContext.Models
{
    public enum ModuleItemKind
    {
        Sub,
        Variable,
        Type,
        Region
    }

    public class ModuleItem
    {
        public ModuleItemKind Kind { get; set; }
        public string Name { get; set; }
        public string Signature { get; set; }
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public string Container { get; set; } = "";
        public bool IsSelected { get; set; }

        public string Group =>
            Kind switch
            {
                ModuleItemKind.Sub => "SUBS",
                ModuleItemKind.Variable => "VARIABLES",
                ModuleItemKind.Type => "TYPES",
                _ => "REGIONS",
            };
    }
}