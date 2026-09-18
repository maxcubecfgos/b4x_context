using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace B4XContext.Models
{
    public enum ModuleItemKind
    {
        Sub,
        Variable,
        Type,
        Region
    }

    public class ModuleItem : INotifyPropertyChanged
    {
        public ModuleItemKind Kind { get; set; }
        public string Name { get; set; }
        public string Signature { get; set; }
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public string Container { get; set; } = "";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
        }

        public string Group =>
            Kind switch
            {
                ModuleItemKind.Sub => "SUBS",
                ModuleItemKind.Variable => "VARIABLES",
                ModuleItemKind.Type => "TYPES",
                _ => "REGIONS",
            };

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}