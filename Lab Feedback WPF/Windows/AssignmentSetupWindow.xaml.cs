using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Lab_Feedback_WPF.Models;
using Lab_Feedback_WPF.Services;

namespace Lab_Feedback_WPF.Windows
{
    public partial class AssignmentSetupWindow : Window
    {
        private readonly AssignmentPersistenceService _assignmentPersistenceService = new();
        public GradingAssignment Assignment { get; private set; }
        private ObservableCollection<RubricItem> _rubricItems;

        public AssignmentSetupWindow()
        {
            InitializeComponent();
            _rubricItems = new ObservableCollection<RubricItem>();
            dgRubric.ItemsSource = _rubricItems;

            _rubricItems.CollectionChanged += (s, e) => UpdateTotalPoints();
            LoadSavedCourseNames();
        }

        public AssignmentSetupWindow(GradingAssignment existingAssignment) : this()
        {
            if (existingAssignment != null)
            {
                cmbCourse.Text = existingAssignment.Course;
                txtTitle.Text = existingAssignment.Title;
                txtRequirements.Text = existingAssignment.Requirements;

                foreach (var item in existingAssignment.Rubric)
                {
                    _rubricItems.Add(item);
                }
            }
        }

        private void LoadSavedCourseNames()
        {
            var courses = _assignmentPersistenceService.GetCourseNames();
            cmbSavedCourses.ItemsSource = courses;

            if (courses.Count > 0)
            {
                cmbSavedCourses.SelectedIndex = 0;
                cmbCourse.Text = courses[0];
            }
        }

        private void SavedCourses_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbSavedCourses.SelectedItem is string selectedCourse)
            {
                cmbCourse.Text = selectedCourse;
            }
        }

        private void LoadSaved_Click(object sender, RoutedEventArgs e)
        {
            var course = cmbCourse.Text.Trim();
            if (string.IsNullOrWhiteSpace(course))
            {
                MessageBox.Show("Enter a course name before loading saved assignments.", "Course Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var savedAssignments = _assignmentPersistenceService.GetAssignmentsByCourse(course);
            if (savedAssignments.Count == 0)
            {
                MessageBox.Show($"No saved assignments found for course '{course}'.", "No Results", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selected = savedAssignments.FirstOrDefault();
            if (selected == null)
                return;

            cmbCourse.Text = selected.Course;
            txtTitle.Text = selected.Title;
            txtRequirements.Text = selected.Requirements;
            _rubricItems.Clear();
            foreach (var item in selected.Rubric)
            {
                _rubricItems.Add(item);
            }

            LoadSavedCourseNames();
            MessageBox.Show($"Loaded {savedAssignments.Count} saved assignment(s) for {selected.Course}.", "Saved Assignment Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddRubricItem_Click(object sender, RoutedEventArgs e)
        {
            _rubricItems.Add(new RubricItem("New Method", 1));
            UpdateTotalPoints();
        }

        private void RemoveRubricItem_Click(object sender, RoutedEventArgs e)
        {
            if (dgRubric.SelectedItem is RubricItem selected)
            {
                _rubricItems.Remove(selected);
                UpdateTotalPoints();
            }
        }

        private void ParseRubric_Click(object sender, RoutedEventArgs e)
        {
            var parseWindow = new Window
            {
                Title = "Parse Rubric from Text",
                Width = 600,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30))
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var textBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 48)),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 204, 204)),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Text = "Paste rubric here. Format:\nBST constructor 1\nPush (empty tree) 3\n..."
            };
            Grid.SetRow(textBox, 0);
            grid.Children.Add(textBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            Grid.SetRow(buttonPanel, 1);

            var parseButton = new Button { Content = "Parse", Width = 100, Margin = new Thickness(5) };
            parseButton.Click += (s, ev) =>
            {
                try
                {
                    var lines = textBox.Text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    var parsed = 0;

                    foreach (var rawLine in lines)
                    {
                        var line = rawLine.Trim();
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (tokens.Length < 2)
                            continue;

                        var gradeText = tokens[^1];
                        if (!double.TryParse(gradeText.Trim(), out double points) || !double.IsFinite(points) || points <= 0)
                            continue;

                        var name = string.Join(" ", tokens.Take(tokens.Length - 1)).Trim();
                        if (string.IsNullOrWhiteSpace(name))
                            continue;

                        _rubricItems.Add(new RubricItem(name, points));
                        parsed++;
                    }

                    MessageBox.Show($"Parsed {parsed} rubric items.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateTotalPoints();
                    parseWindow.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error parsing rubric: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            var cancelButton = new Button { Content = "Cancel", Width = 100, Margin = new Thickness(5) };
            cancelButton.Click += (s, ev) => parseWindow.Close();

            buttonPanel.Children.Add(parseButton);
            buttonPanel.Children.Add(cancelButton);
            grid.Children.Add(buttonPanel);

            parseWindow.Content = grid;
            parseWindow.ShowDialog();
        }

        private void UpdateTotalPoints()
        {
            var total = _rubricItems.Sum(r => r.MaxPoints);
            txtTotalPoints.Text = $"Total: {total} points";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                MessageBox.Show("Please enter an assignment title.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_rubricItems.Count == 0)
            {
                MessageBox.Show("Please add at least one rubric item.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_rubricItems.Any(item => !double.IsFinite(item.MaxPoints) || item.MaxPoints <= 0)
                || !double.IsFinite(_rubricItems.Sum(item => item.MaxPoints)))
            {
                MessageBox.Show("Rubric points must be finite positive numbers.", "Validation");
                return;
            }

            Assignment = new GradingAssignment
            {
                Course = string.IsNullOrWhiteSpace(cmbCourse.Text) ? "General" : cmbCourse.Text.Trim(),
                Title = txtTitle.Text.Trim(),
                Requirements = txtRequirements.Text,
                Rubric = _rubricItems.ToList()
            };

            _assignmentPersistenceService.SaveAssignment(Assignment);
            LoadSavedCourseNames();

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
