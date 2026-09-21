using System.Collections.Generic;
using B4XContext.Models;

namespace B4XContext.Engine
{
    /// <summary>
    /// Turns Dart declarations into <see cref="ModuleItem"/> entries so a Dart file can use the
    /// same granular "Custom" selection as a B4X module:
    /// classes/mixins/extensions/enums/typedefs become TYPES, functions, constructors, getters,
    /// setters, operators and abstract members become SUBS, fields and top level variables become
    /// VARIABLES. Members carry their enclosing type in <see cref="ModuleItem.Container"/>.
    /// </summary>
    public static class DartItemExtractor
    {
        public static List<ModuleItem> ExtractItems(string source)
        {
            var items = new List<ModuleItem>();

            foreach (var d in DartSkeletonizer.ExtractDeclarations(source))
            {
                items.Add(new ModuleItem
                {
                    Kind = d.IsType
                        ? ModuleItemKind.Type
                        : d.IsCallable ? ModuleItemKind.Sub : ModuleItemKind.Variable,
                    Name = d.Name,
                    Signature = string.IsNullOrEmpty(d.Signature) ? d.Name : d.Signature,
                    StartLine = d.StartLine,
                    EndLine = d.EndLine,
                    Container = d.Container
                });
            }

            return items;
        }
    }
}
