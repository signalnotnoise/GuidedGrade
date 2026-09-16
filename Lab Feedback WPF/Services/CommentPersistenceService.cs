using System.IO;
using System.Text.Json;
using Lab_Feedback_WPF.Models;
using Microsoft.Data.Sqlite;


namespace Lab_Feedback_WPF.Services
{
    public sealed class CommentPersistenceService
    {
        private readonly string _databasePath;

        public CommentPersistenceService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appDataPath, "LabFeedbackWPF");
            Directory.CreateDirectory(folder);

            _databasePath = Path.Combine(folder, "section-comments.db");
            Initialize();
        }

        private void Initialize()
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS SectionComments (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FilePath TEXT NOT NULL,
                    SectionName TEXT NOT NULL,
                    StartLine INTEGER NOT NULL,
                    EndLine INTEGER NOT NULL,
                    SuggestedScore REAL NOT NULL,
                    Strengths TEXT NOT NULL,
                    Issues TEXT NOT NULL,
                    SuggestedCode TEXT NOT NULL,
                    Explanation TEXT NOT NULL,
                    CreatedUtc TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                );
            ";
            command.ExecuteNonQuery();

            using var indexCommand = connection.CreateCommand();
            indexCommand.CommandText = @"
                CREATE UNIQUE INDEX IF NOT EXISTS IX_SectionComments_FilePath_SectionName_StartLine_EndLine
                ON SectionComments(FilePath, SectionName, StartLine, EndLine);
            ";
            indexCommand.ExecuteNonQuery();

            EnsureReviewStatusColumn(connection);
        }

        private static void EnsureReviewStatusColumn(SqliteConnection connection)
        {
            var hasColumn = false;
            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA table_info(SectionComments);";
                using var reader = pragma.ExecuteReader();
                while (reader.Read())
                {
                    if (string.Equals(reader.GetString(1), "ReviewStatus", StringComparison.OrdinalIgnoreCase))
                    {
                        hasColumn = true;
                        break;
                    }
                }
            }

            if (hasColumn)
                return;

            using var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE SectionComments ADD COLUMN ReviewStatus TEXT NOT NULL DEFAULT 'Pending';";
            alter.ExecuteNonQuery();
        }

        public void SaveComments(string filePath, IEnumerable<SectionFeedback> comments)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            var commentList = comments?.Where(c => c != null).ToList() ?? new List<SectionFeedback>();

            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var transaction = connection.BeginTransaction();

            using (var deleteCommand = connection.CreateCommand())
            {
                deleteCommand.Transaction = transaction;
                deleteCommand.CommandText = "DELETE FROM SectionComments WHERE FilePath = @filePath";
                deleteCommand.Parameters.AddWithValue("@filePath", filePath);
                deleteCommand.ExecuteNonQuery();
            }

            foreach (var comment in commentList)
            {
                using var insertCommand = connection.CreateCommand();
                insertCommand.Transaction = transaction;
                insertCommand.CommandText = @"
                    INSERT INTO SectionComments (
                        FilePath,
                        SectionName,
                        StartLine,
                        EndLine,
                        SuggestedScore,
                        Strengths,
                        Issues,
                        SuggestedCode,
                        Explanation,
                        ReviewStatus)
                    VALUES (
                        @filePath,
                        @sectionName,
                        @startLine,
                        @endLine,
                        @suggestedScore,
                        @strengths,
                        @issues,
                        @suggestedCode,
                        @explanation,
                        @reviewStatus);
                ";

                insertCommand.Parameters.AddWithValue("@filePath", filePath);
                insertCommand.Parameters.AddWithValue("@sectionName", comment.SectionName ?? string.Empty);
                insertCommand.Parameters.AddWithValue("@startLine", comment.StartLine);
                insertCommand.Parameters.AddWithValue("@endLine", comment.EndLine);
                insertCommand.Parameters.AddWithValue("@suggestedScore", comment.SuggestedScore);
                insertCommand.Parameters.AddWithValue("@strengths", SerializeList(comment.Strengths));
                insertCommand.Parameters.AddWithValue("@issues", SerializeList(comment.Issues));
                insertCommand.Parameters.AddWithValue("@suggestedCode", comment.SuggestedCode ?? string.Empty);
                insertCommand.Parameters.AddWithValue("@explanation", comment.Explanation ?? string.Empty);
                insertCommand.Parameters.AddWithValue("@reviewStatus", comment.ReviewStatus.ToString());

                insertCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        public List<SectionFeedback> LoadComments(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return new List<SectionFeedback>();

            var result = new List<SectionFeedback>();

            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT SectionName, StartLine, EndLine, SuggestedScore, Strengths, Issues, SuggestedCode, Explanation, ReviewStatus
                FROM SectionComments
                WHERE FilePath = @filePath
                ORDER BY StartLine, EndLine;
            ";
            command.Parameters.AddWithValue("@filePath", filePath);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new SectionFeedback
                {
                    SectionName = reader.GetString(0),
                    StartLine = reader.GetInt32(1),
                    EndLine = reader.GetInt32(2),
                    SuggestedScore = reader.GetDouble(3),
                    Strengths = DeserializeList(reader.GetString(4)),
                    Issues = DeserializeList(reader.GetString(5)),
                    SuggestedCode = reader.GetString(6),
                    Explanation = reader.GetString(7),
                    ReviewStatus = ParseReviewStatus(reader.GetString(8))
                });
            }

            return result;
        }

        public void DeleteComments(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM SectionComments WHERE FilePath = @filePath";
            command.Parameters.AddWithValue("@filePath", filePath);
            command.ExecuteNonQuery();
        }

        private static string SerializeList(List<string> values)
        {
            return JsonSerializer.Serialize(values ?? new List<string>());
        }

        private static FeedbackReviewStatus ParseReviewStatus(string? value)
        {
            return Enum.TryParse<FeedbackReviewStatus>(value, true, out var status)
                ? status
                : FeedbackReviewStatus.Pending;
        }

        private static List<string> DeserializeList(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<string>();

            try
            {
                var values = JsonSerializer.Deserialize<List<string>>(json);
                return values ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
