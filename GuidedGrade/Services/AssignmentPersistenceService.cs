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
            ";
            command.ExecuteNonQuery();

            using var uniqueCommand = connection.CreateCommand();
            uniqueCommand.CommandText = @"
                CREATE UNIQUE INDEX IF NOT EXISTS IX_SavedAssignments_Course_Title
                ON SavedAssignments(Course, Title);
            ";
            uniqueCommand.ExecuteNonQuery();
        }

        public void SaveAssignment(GradingAssignment assignment)
        {
            if (assignment == null)
                throw new ArgumentNullException(nameof(assignment));

            if (assignment.Rubric.Any(item => !double.IsFinite(item.MaxPoints) || item.MaxPoints <= 0 || !double.IsFinite(item.EarnedPoints))
                || !double.IsFinite(assignment.TotalMaxPoints))
                throw new ArgumentException("Rubric points must be finite and maximum points must be positive.", nameof(assignment));

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
            ";

            command.Parameters.AddWithValue("@course", course);
            command.Parameters.AddWithValue("@title", title);
            command.Parameters.AddWithValue("@requirements", assignment.Requirements ?? string.Empty);
            command.Parameters.AddWithValue("@rubricJson", JsonSerializer.Serialize(assignment.Rubric ?? new List<RubricItem>()));
            command.Parameters.AddWithValue("@updatedUtc", DateTime.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
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
                SELECT Course, Title, Requirements, RubricJson
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
                SELECT Course, Title, Requirements, RubricJson
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
                SELECT Course, Title, Requirements, RubricJson
                FROM SavedAssignments
                WHERE LOWER(Course) = LOWER(@course)
                  AND LOWER(Title) = LOWER(@title);
            ";
            command.Parameters.AddWithValue("@course", course.Trim());
            command.Parameters.AddWithValue("@title", title.Trim());

            using var reader = command.ExecuteReader();
            return reader.Read() ? MapAssignment(reader) : null;
        }

        private static GradingAssignment MapAssignment(SqliteDataReader reader)
        {
            var rubricJson = reader.IsDBNull(3) ? "[]" : reader.GetString(3);
            var rubric = JsonSerializer.Deserialize<List<RubricItem>>(rubricJson) ?? new List<RubricItem>();

            return new GradingAssignment
            {
                Course = reader.GetString(0),
                Title = reader.GetString(1),
                Requirements = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Rubric = rubric
            };
        }
    }
}
