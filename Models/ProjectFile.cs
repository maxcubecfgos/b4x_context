using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace B4XContext.Models
{
    public enum FileMode
    {
        Skeleton,
        Full,
        Custom
    }

    public class ProjectFile : INotifyPropertyChanged
    {
        public string Path { get; set; }
        public string Name => System.IO.Path.GetFileName(Path);
        public string Directory => System.IO.Path.GetDirectoryName(Path) ?? "";
        public string Kind { get; set; } = "file";
        public ObservableCollection<ModuleItem> Items { get; } = new ObservableCollection<ModuleItem>();

        public bool IsCodeFile => Kind == "bas" || Kind == "b4a" || Kind == "b4j" || Kind == "b4i";

        private bool _included = true;
        private FileMode _mode = FileMode.Skeleton;
        private int _estimatedTokens = 0;
        private bool _isExpanded;

        public bool Included
        {
            get => _included;
            set { if (_included == value) return; _included = value; OnPropertyChanged(); }
        }

        public FileMode Mode
        {
            get => _mode;
            set { if (_mode == value) return; _mode = value; OnPropertyChanged(); }
        }

        public int EstimatedTokens
        {
            get => _estimatedTokens;
            set { if (_estimatedTokens == value) return; _estimatedTokens = value; OnPropertyChanged(); }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { if (_isExpanded == value) return; _isExpanded = value; OnPropertyChanged(); }
        }

        public ProjectFile(string path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Items.CollectionChanged += OnItemsChanged;
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

        private void OnItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(HasSubs));
            OnPropertyChanged(nameof(HasVariables));
            OnPropertyChanged(nameof(HasTypes));
            OnPropertyChanged(nameof(HasRegions));
            OnPropertyChanged(nameof(ItemsSubs));
            OnPropertyChanged(nameof(ItemsVariables));
            OnPropertyChanged(nameof(ItemsTypes));
            OnPropertyChanged(nameof(ItemsRegions));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}