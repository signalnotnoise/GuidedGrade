using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace GuidedGrade.Services;

internal sealed record StudentGrade(double Earned, double Possible)
{
    internal double Percentage => Earned / Possible * 100;
    internal string Summary => $"{Earned:0.##} / {Possible:0.##} · {Percentage:0.#}%";
}

internal static class GradeTotals
{
    // ReviewContext encodes [student folder, course, assignment, submission root].
    // Ungraded submissions have no record and therefore no points in the total.
    internal static StudentGrade? ForCourse(IReadOnlyDictionary<string, StudentGrade> grades, string? studentFolder, string? course)
    {
        if (studentFolder == null || course == null) return null;
        double earned = 0, possible = 0;
        foreach (var (key, grade) in grades)
        {
            var context = JsonSerializer.Deserialize<string?[]>(key);
            if (context is not { Length: 4 } || !string.Equals(context[0], studentFolder, StringComparison.OrdinalIgnoreCase) || context[1] != course) continue;
            earned += grade.Earned;
            possible += grade.Possible;
        }
        return possible > 0 ? new(earned, possible) : null;
    }
}

/// <summary>Local, instructor-confirmed grades; never included in model prompts.</summary>
internal sealed class GradePersistenceService
{
    private readonly string _databasePath;
    internal GradePersistenceService(string databasePath)
    {
        _databasePath = databasePath;
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS StudentGrades (ReviewContext TEXT PRIMARY KEY, Earned REAL NOT NULL, Possible REAL NOT NULL)";
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _databasePath }.ToString());
        connection.Open();
        return connection;
    }

    internal Dictionary<string, StudentGrade> LoadAll()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT ReviewContext, Earned, Possible FROM StudentGrades";
        using var reader = command.ExecuteReader();
        var grades = new Dictionary<string, StudentGrade>(StringComparer.Ordinal);
        while (reader.Read()) grades.Add(reader.GetString(0), new(reader.GetDouble(1), reader.GetDouble(2)));
        return grades;
    }

    internal void Save(string context, StudentGrade? grade)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        if (grade is not null && (!double.IsFinite(grade.Earned) || !double.IsFinite(grade.Possible) ||
            grade.Possible <= 0 || grade.Earned < 0 || grade.Earned > grade.Possible))
            throw new ArgumentException("Grade must be between zero and a positive maximum.", nameof(grade));
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = grade is null ? "DELETE FROM StudentGrades WHERE ReviewContext = @context" :
            "INSERT INTO StudentGrades (ReviewContext, Earned, Possible) VALUES (@context, @earned, @possible) ON CONFLICT(ReviewContext) DO UPDATE SET Earned = excluded.Earned, Possible = excluded.Possible";
        command.Parameters.AddWithValue("@context", context);
        if (grade is not null)
        {
            command.Parameters.AddWithValue("@earned", grade.Earned);
            command.Parameters.AddWithValue("@possible", grade.Possible);
        }
        command.ExecuteNonQuery();
    }
}
