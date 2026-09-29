using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

namespace GuidedGrade.Models
{
    public class FileSystemItem : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isSelected;
        private bool _isCheckedForAnalysis;

        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public bool IsSolution =>
            !IsDirectory &&
            (FullPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
             || FullPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase));
        public ObservableCollection<FileSystemItem> Children { get; set; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public bool IsCheckedForAnalysis
        {
            get => _isCheckedForAnalysis;
            set
            {
                if (_isCheckedForAnalysis != value)
                {
                    _isCheckedForAnalysis = value;
                    OnPropertyChanged(nameof(IsCheckedForAnalysis));
                }
            }
        }

        public FileSystemItem(string path, bool isDirectory)
        {
            FullPath = path;
            Name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(Name))
                Name = path; // For root directories
            IsDirectory = isDirectory;
            Children = new ObservableCollection<FileSystemItem>();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
