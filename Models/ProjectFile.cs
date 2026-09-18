using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace B4XContext.Models
{
    public enum FileMode
    {
        Skeleton,
        Full,
        Custom
    }

    public class ProjectFile
    {
        public string Path { get; set; }
        public string Name => System.IO.Path.GetFileName(Path);
        public string Directory => System.IO.Path.GetDirectoryName(Path) ?? "";
        public bool Included { get; set; } = true;
        public FileMode Mode { get; set; } = FileMode.Skeleton;
        public int EstimatedTokens { get; set; } = 0;
        public string Kind { get; set; } = "file";
        public ObservableCollection<ModuleItem> Items { get; } = new ObservableCollection<ModuleItem>();
        public bool IsExpanded { get; set; }

        public ProjectFile(string path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public bool HasItems => Items.Count > 0;

        public IEnumerable<ModuleItem> ItemsSubs => Items.Where(i => i.Kind == ModuleItemKind.Sub);
        public IEnumerable<ModuleItem> ItemsVariables => Items.Where(i => i.Kind == ModuleItemKind.Variable);
        public IEnumerable<ModuleItem> ItemsTypes => Items.Where(i => i.Kind == ModuleItemKind.Type);
        public IEnumerable<ModuleItem> ItemsRegions => Items.Where(i => i.Kind == ModuleItemKind.Region);

        public bool HasSubs => Items.Any(i => i.Kind == ModuleItemKind.Sub);
        public bool HasVariables => Items.Any(i => i.Kind == ModuleItemKind.Variable);
        public bool HasTypes => Items.Any(i => i.Kind == ModuleItemKind.Type);
        public bool HasRegions => Items.Any(i => i.Kind == ModuleItemKind.Region);

        public void ResetCustom()
        {
            foreach (var item in Items) item.IsSelected = false;
            Mode = FileMode.Skeleton;
        }
    }
}