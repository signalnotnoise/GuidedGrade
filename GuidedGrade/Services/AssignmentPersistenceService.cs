using System.IO;
using System.Text.Json;
using GuidedGrade.Models;
using Microsoft.Data.Sqlite;

namespace GuidedGrade.Services
{
    public sealed class AssignmentPersistenceService
    {
        private readonly string _databasePath;
        internal string DatabasePath => _databasePath;

        public AssignmentPersistenceService()
        {
            _databasePath = Path.Combine(AppDataPaths.RoamingDirectory, "assignments.db");
            Initialize();
        }

        internal AssignmentPersistenceService(string databasePath)
        {
            _databasePath = databasePath;
            Initialize();
        }

        private void Initialize()
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS SavedAssignments (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Course TEXT NOT NULL,
                    Title TEXT NOT NULL,
                    Requirements TEXT NOT NULL,
                    RubricJson TEXT NOT NULL,
                    UpdatedUtc TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                );
                CREATE TABLE IF NOT EXISTS CourseFolderSettings (
                    Course TEXT PRIMARY KEY COLLATE NOCASE,
                    UseFolderNames INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS CourseReviewRules (Course TEXT PRIMARY KEY COLLATE NOCASE, Rules TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS AssignmentOptions (
                    Course TEXT NOT NULL, Title TEXT NOT NULL, OptionsJson TEXT NOT NULL,
                    PRIMARY KEY (Course, Title)
                );
            ";
            command.ExecuteNonQuery();

            using var uniqueCommand = connection.CreateCommand();
            uniqueCommand.CommandText = @"
                CREATE UNIQUE INDEX IF NOT EXISTS IX_SavedAssignments_Course_Title
                ON SavedAssignments(Course, Title);
            ";
            uniqueCommand.ExecuteNonQuery();
        }

        public string LoadCourseReviewRules(string course)
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _databasePath }.ToString()); connection.Open();
            using var command = connection.CreateCommand(); command.CommandText = "SELECT Rules FROM CourseReviewRules WHERE Course = @course"; command.Parameters.AddWithValue("@course", course.Trim());
            return command.ExecuteScalar() as string ?? "";
        }
        public void SaveCourseReviewRules(string course, string rules)
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _databasePath }.ToString()); connection.Open();
            using var command = connection.CreateCommand(); command.CommandText = "INSERT INTO CourseReviewRules(Course, Rules) VALUES (@course, @rules) ON CONFLICT(Course) DO UPDATE SET Rules = excluded.Rules";
            command.Parameters.AddWithValue("@course", course.Trim()); command.Parameters.AddWithValue("@rules", rules); command.ExecuteNonQuery();
        }

        public bool UseFolderNames(string? course)
        {
            if (string.IsNullOrWhiteSpace(course)) return false;
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _databasePath }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT UseFolderNames FROM CourseFolderSettings WHERE Course = @course";
            command.Parameters.AddWithValue("@course", course.Trim());
            return command.ExecuteScalar() is long value && value != 0;
        }

        public void SaveFolderNames(string course, bool enabled)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(course);
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _databasePath }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO CourseFolderSettings (Course, UseFolderNames) VALUES (@course, @enabled) ON CONFLICT(Course) DO UPDATE SET UseFolderNames = excluded.UseFolderNames";
            command.Parameters.AddWithValue("@course", course.Trim());
            command.Parameters.AddWithValue("@enabled", enabled ? 1 : 0);
            command.ExecuteNonQuery();
        }

        public void SaveAssignment(GradingAssignment assignment)
        {
            if (assignment == null)
                throw new ArgumentNullException(nameof(assignment));
            if (assignment.Deductions.Any(item => string.IsNullOrWhiteSpace(item.Rule) || !double.IsFinite(item.Points) || item.Points <= 0))
                throw new ArgumentException("Deduction rules need finite positive penalty points.", nameof(assignment));

            if (assignment.Rubric.Any(item => !double.IsFinite(item.MaxPoints) || item.MaxPoints <= 0 || !double.IsFinite(item.EarnedPoints))
                || !double.IsFinite(assignment.TotalMaxPoints))
                throw new ArgumentException("Rubric points must be finite and maximum points must be positive.", nameof(assignment));

            foreach (var path in assignment.ReviewFilePaths) BatchReviewPlan.ValidatePattern(path);
            if (!string.IsNullOrWhiteSpace(assignment.LogFilePath))
            {
                BatchReviewPlan.ValidatePattern(assignment.LogFilePath);
                if (!assignment.LogFilePath.EndsWith(".fslog", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Log path must end in .fslog.");
            }
            var course = string.IsNullOrWhiteSpace(assignment.Course) ? "General" : assignment.Course.Trim();
            var title = assignment.Title?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
                throw new InvalidOperationException("Assignment title is required.");

            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO SavedAssignments (Course, Title, Requirements, RubricJson, UpdatedUtc)
                VALUES (@course, @title, @requirements, @rubricJson, @updatedUtc)
                ON CONFLICT(Course, Title)
                DO UPDATE SET
                    Requirements = excluded.Requirements,
                    RubricJson = excluded.RubricJson,
                    UpdatedUtc = excluded.UpdatedUtc;
                INSERT INTO AssignmentOptions (Course, Title, OptionsJson)
                VALUES (@course, @title, @optionsJson)
                ON CONFLICT(Course, Title) DO UPDATE SET OptionsJson = excluded.OptionsJson;
            ";

            command.Parameters.AddWithValue("@course", course);
            command.Parameters.AddWithValue("@title", title);
            command.Parameters.AddWithValue("@requirements", assignment.Requirements ?? string.Empty);
            command.Parameters.AddWithValue("@rubricJson", JsonSerializer.Serialize(assignment.Rubric ?? new List<RubricItem>()));
            command.Parameters.AddWithValue("@updatedUtc", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("@optionsJson", JsonSerializer.Serialize(new PersistedOptions(assignment.Deductions, assignment.FeedbackOptions, assignment.ReviewFilePaths, assignment.LogFilePath)));
            using var transaction = connection.BeginTransaction();
            command.Transaction = transaction;
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        public List<GradingAssignment> GetAssignmentsByCourse(string course)
        {
            if (string.IsNullOrWhiteSpace(course))
                return GetAllAssignments();

            var result = new List<GradingAssignment>();
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Course, Title, Requirements, RubricJson,
                    (SELECT OptionsJson FROM AssignmentOptions WHERE AssignmentOptions.Course = SavedAssignments.Course AND AssignmentOptions.Title = SavedAssignments.Title)
                FROM SavedAssignments
                WHERE LOWER(Course) = LOWER(@course)
                ORDER BY Title;
            ";
            command.Parameters.AddWithValue("@course", course.Trim());

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(MapAssignment(reader));
            }

            return result;
        }

        public List<GradingAssignment> GetAllAssignments()
        {
            var result = new List<GradingAssignment>();
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Course, Title, Requirements, RubricJson,
                    (SELECT OptionsJson FROM AssignmentOptions WHERE AssignmentOptions.Course = SavedAssignments.Course AND AssignmentOptions.Title = SavedAssignments.Title)
                FROM SavedAssignments
                ORDER BY Course, Title;
            ";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(MapAssignment(reader));
            }

            return result;
        }

        public List<string> GetCourseNames()
        {
            var result = new List<string>();
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT DISTINCT Course FROM SavedAssignments ORDER BY Course;";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var course = reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(course))
                    result.Add(course);
            }

            return result;
        }

        public GradingAssignment? LoadAssignment(string course, string title)
        {
            if (string.IsNullOrWhiteSpace(course) || string.IsNullOrWhiteSpace(title))
                return null;

            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Course, Title, Requirements, RubricJson,
                    (SELECT OptionsJson FROM AssignmentOptions WHERE AssignmentOptions.Course = SavedAssignments.Course AND AssignmentOptions.Title = SavedAssignments.Title)
                FROM SavedAssignments
                WHERE LOWER(Course) = LOWER(@course)
                  AND LOWER(Title) = LOWER(@title);
            ";
            command.Parameters.AddWithValue("@course", course.Trim());
            command.Parameters.AddWithValue("@title", title.Trim());

            using var reader = command.ExecuteReader();
            return reader.Read() ? MapAssignment(reader) : null;
        }

        private GradingAssignment MapAssignment(SqliteDataReader reader)
        {
            var rubricJson = reader.IsDBNull(3) ? "[]" : reader.GetString(3);
            var rubric = JsonSerializer.Deserialize<List<RubricItem>>(rubricJson) ?? new List<RubricItem>();

            var options = reader.IsDBNull(4) ? null : JsonSerializer.Deserialize<PersistedOptions>(reader.GetString(4));
            return new GradingAssignment
            {
                Course = reader.GetString(0),
                CourseReviewRules = LoadCourseReviewRules(reader.GetString(0)),
                Title = reader.GetString(1),
                Requirements = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Rubric = rubric,
                Deductions = options?.Deductions ?? new(),
                ReviewFilePaths = options?.ReviewFilePaths ?? new(),
                LogFilePath = options?.LogFilePath ?? "",
                FeedbackOptions = options?.Feedback ?? new()
            };
        }
        private sealed record PersistedOptions(List<AssignmentDeduction> Deductions, AssignmentFeedbackOptions Feedback, List<string>? ReviewFilePaths = null, string? LogFilePath = null);
    }
}
