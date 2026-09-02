using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Lab_Feedback_WPF.Models;

namespace Lab_Feedback_WPF.Windows
{
    public partial class SectionGradingDialog : Window
    {
        public string SectionName => txtSectionName.Text;
        public List<RubricItem> SelectedRubricItems { get; private set; }

        private List<RubricItemViewModel> _viewModels;

        public SectionGradingDialog(List<RubricItem> allRubricItems)
        {
            InitializeComponent();

            _viewModels = allRubricItems.Select(item => new RubricItemViewModel
            {
                Item = item,
                DisplayText = $"{item.Name} ({item.MaxPoints} pts)",
                IsSelected = false
            }).ToList();

            rubricItemsList.ItemsSource = _viewModels;
            SelectedRubricItems = new List<RubricItem>();
        }

        private void Grade_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSectionName.Text))
            {
                MessageBox.Show("Please enter a section name.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedRubricItems = _viewModels
                .Where(vm => vm.IsSelected)
                .Select(vm => vm.Item)
                .ToList();

            if (SelectedRubricItems.Count == 0)
            {
                MessageBox.Show("Please select at least one rubric item.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private class RubricItemViewModel : INotifyPropertyChanged
        {
            public RubricItem Item { get; set; }
            public string DisplayText { get; set; }

            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected != value)
                    {
                        _isSelected = value;
                        OnPropertyChanged();
                    }
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
