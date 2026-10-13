using System.IO;
using System.Text.Json;
using GuidedGrade.Models;
using Microsoft.Data.Sqlite;


namespace GuidedGrade.Services
{
    public sealed class CommentPersistenceService
    {
        private readonly string _databasePath;

        public CommentPersistenceService()
        {
            _databasePath = Path.Combine(AppDataPaths.RoamingDirectory, "section-comments.db");
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

            using (var contextColumns = connection.CreateCommand())
            {
                contextColumns.CommandText = "SELECT COUNT(*) FROM pragma_table_info('SectionComments') WHERE name = 'ReviewContext';";
                if (Convert.ToInt64(contextColumns.ExecuteScalar()) == 0)
                {
                    using var alter = connection.CreateCommand();
                    alter.CommandText = "ALTER TABLE SectionComments ADD COLUMN ReviewContext TEXT NOT NULL DEFAULT '';";
                    alter.ExecuteNonQuery();
                }
            }

            EnsureReviewStatusColumn(connection);
            using (var metadata = connection.CreateCommand())
            {
                metadata.CommandText = "SELECT COUNT(*) FROM pragma_table_info('SectionComments') WHERE name = 'RubricReview';";
                if (Convert.ToInt64(metadata.ExecuteScalar()) == 0)
                {
                    metadata.CommandText = "ALTER TABLE SectionComments ADD COLUMN RubricReview TEXT NOT NULL DEFAULT 'null';";
                    metadata.ExecuteNonQuery();
                }
            }

            using (var pin = connection.CreateCommand())
            {
                pin.CommandText = "SELECT COUNT(*) FROM pragma_table_info('SectionComments') WHERE name = 'IsPinned';";
                if (Convert.ToInt64(pin.ExecuteScalar()) == 0)
                {
                    pin.CommandText = "ALTER TABLE SectionComments ADD COLUMN IsPinned INTEGER NOT NULL DEFAULT 0;";
                    pin.ExecuteNonQuery();
                }
            }
            using var indexCommand = connection.CreateCommand();
            indexCommand.CommandText = @"
                DROP INDEX IF EXISTS IX_SectionComments_FilePath_SectionName_StartLine_EndLine;
                DROP INDEX IF EXISTS IX_SectionComments_Context_File_Section_Lines;
                CREATE UNIQUE INDEX IF NOT EXISTS IX_SectionComments_Context_File_Section_Lines_Rubric
                ON SectionComments(FilePath, ReviewContext, SectionName, StartLine, EndLine, COALESCE(json_extract(RubricReview, '$.CriterionId'), 0), COALESCE(json_extract(RubricReview, '$.IsDeduction'), 0));
            ";
            indexCommand.ExecuteNonQuery();

            using var columns = connection.CreateCommand();
            columns.CommandText = "SELECT COUNT(*) FROM pragma_table_info('SectionComments') WHERE name = 'IsOverallReview';";
            if (Convert.ToInt64(columns.ExecuteScalar()) == 0)
            {
                using var alter = connection.CreateCommand();
                alter.CommandText = "ALTER TABLE SectionComments ADD COLUMN IsOverallReview INTEGER NOT NULL DEFAULT 0;";
                alter.ExecuteNonQuery();
            }
        }

        internal CommentPersistenceService(string databasePath)
        {
            _databasePath = databasePath;
            Initialize();
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
                deleteCommand.CommandText = "DELETE FROM SectionComments WHERE FilePath = @filePath COLLATE NOCASE";
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
                        ReviewStatus, IsOverallReview, ReviewContext, RubricReview, IsPinned)
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
                        @reviewStatus, @isOverallReview, @reviewContext, @rubricReview, @isPinned);
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
                insertCommand.Parameters.AddWithValue("@isOverallReview", comment.IsOverallReview ? 1 : 0);
                insertCommand.Parameters.AddWithValue("@reviewContext", comment.ReviewContext);
                insertCommand.Parameters.AddWithValue("@rubricReview", JsonSerializer.Serialize(comment.RubricReview));
                insertCommand.Parameters.AddWithValue("@isPinned", comment.IsPinned ? 1 : 0);

                insertCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        internal void MoveComment(string source, string destination, SectionFeedback original, SectionFeedback updated)
        {
            if (!ReviewContext.Matches(original.ReviewContext, updated.ReviewContext))
                throw new InvalidOperationException("A comment cannot move to another review context.");
            using var connection = new SqliteConnection($"Data Source={_databasePath}"); connection.Open();
            using var transaction = connection.BeginTransaction();
            using var find = connection.CreateCommand(); find.Transaction = transaction;
            find.CommandText = "SELECT Id, IsPinned FROM SectionComments WHERE FilePath=@source COLLATE NOCASE AND ReviewContext=@context AND SectionName=@name AND StartLine=@start AND EndLine=@end AND COALESCE(json_extract(RubricReview,'$.CriterionId'),0)=@criterion AND COALESCE(json_extract(RubricReview,'$.IsDeduction'),0)=@deduction";
            find.Parameters.AddWithValue("@source",source); find.Parameters.AddWithValue("@context",original.ReviewContext);
            find.Parameters.AddWithValue("@name",original.SectionName); find.Parameters.AddWithValue("@start",original.StartLine); find.Parameters.AddWithValue("@end",original.EndLine);
            find.Parameters.AddWithValue("@criterion",original.RubricReview?.CriterionId ?? 0); find.Parameters.AddWithValue("@deduction",original.RubricReview?.IsDeduction == true ? 1 : 0);
            long id; bool pinned;
            using (var reader = find.ExecuteReader())
            {
                if (!reader.Read()) throw new InvalidOperationException("This comment was cleared or changed. Reopen its review before moving it.");
                id=reader.GetInt64(0); pinned=reader.GetInt64(1)!=0;
            }
            if (pinned && (!string.Equals(source,destination,StringComparison.OrdinalIgnoreCase) || original.StartLine != updated.StartLine || original.EndLine != updated.EndLine))
                throw new InvalidOperationException("Unpin the comment before moving it.");
            using var update = connection.CreateCommand(); update.Transaction = transaction;
            update.CommandText = "UPDATE SectionComments SET FilePath=@destination, StartLine=@start, EndLine=@end, SuggestedScore=@points, Explanation=@text, Strengths=@strengths, Issues=@issues, SuggestedCode=@code, ReviewStatus=@status, RubricReview=@rubric WHERE Id=@id";
            update.Parameters.AddWithValue("@destination",destination); update.Parameters.AddWithValue("@start",updated.StartLine); update.Parameters.AddWithValue("@end",updated.EndLine);
            update.Parameters.AddWithValue("@points",updated.SuggestedScore); update.Parameters.AddWithValue("@text",updated.Explanation);
            update.Parameters.AddWithValue("@strengths",SerializeList(updated.Strengths)); update.Parameters.AddWithValue("@issues",SerializeList(updated.Issues));
            update.Parameters.AddWithValue("@code",updated.SuggestedCode); update.Parameters.AddWithValue("@status",updated.ReviewStatus.ToString());
            update.Parameters.AddWithValue("@rubric",JsonSerializer.Serialize(updated.RubricReview)); update.Parameters.AddWithValue("@id",id);
            update.ExecuteNonQuery(); transaction.Commit();
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
                SELECT SectionName, StartLine, EndLine, SuggestedScore, Strengths, Issues, SuggestedCode, Explanation, ReviewStatus, IsOverallReview, ReviewContext, RubricReview, IsPinned
                FROM SectionComments
                WHERE FilePath = @filePath COLLATE NOCASE
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
                    ReviewStatus = ParseReviewStatus(reader.GetString(8)),
                    IsOverallReview = reader.GetInt64(9) != 0,
                    ReviewContext = reader.GetString(10),
                    RubricReview = JsonSerializer.Deserialize<RubricReviewDetails>(reader.GetString(11)),
                    IsPinned = reader.GetInt64(12) != 0
                });
            }

            return result;
        }

        internal List<string> GetAllReviewedPaths()
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT DISTINCT FilePath FROM SectionComments";
            using var reader = command.ExecuteReader();
            var result = new List<string>();
            while (reader.Read()) result.Add(reader.GetString(0));
            return result;
        }

        internal List<(string FilePath, string Context)> GetReviewedFiles()
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT FilePath, ReviewContext FROM SectionComments WHERE ReviewStatus <> 'Rejected' GROUP BY FilePath, ReviewContext ORDER BY MAX(CreatedUtc) DESC, FilePath";
            using var reader = command.ExecuteReader();
            var files = new List<(string, string)>();
            while (reader.Read()) files.Add((reader.GetString(0), reader.GetString(1)));
            return files;
        }

        // Delete only contexts explicitly attributed to this course and assignment.
        // Legacy unscoped comments cannot safely be attributed and remain intact.
        internal List<string> DeleteAssignmentReviews(GradingAssignment assignment, IReadOnlyList<string>? paths = null)
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            var predicate = "CASE WHEN json_valid(ReviewContext) THEN json_extract(ReviewContext, '$[1]') END = @course AND CASE WHEN json_valid(ReviewContext) THEN json_extract(ReviewContext, '$[2]') END = @title";
            command.Parameters.AddWithValue("@course", assignment.Course);
            command.Parameters.AddWithValue("@title", assignment.Title);
            if (paths != null)
            {
                if (paths.Count == 0) return [];
                var parameters = paths.Select((path, index) => { var name = "@path" + index; command.Parameters.AddWithValue(name, path); return name; });
                predicate += " AND FilePath COLLATE NOCASE IN (" + string.Join(",", parameters) + ")";
            }
            command.CommandText = "SELECT DISTINCT FilePath FROM SectionComments WHERE " + predicate;
            var affected = new List<string>();
            using (var reader = command.ExecuteReader()) while (reader.Read()) affected.Add(reader.GetString(0));
            command.CommandText = "DELETE FROM SectionComments WHERE " + predicate;
            command.ExecuteNonQuery();
            transaction.Commit();
            return affected;
        }

        public void DeleteComments(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM SectionComments WHERE FilePath = @filePath COLLATE NOCASE";
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
