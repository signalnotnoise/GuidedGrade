using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lab_Feedback_WPF.Services
{
    public sealed class ExecutionTestCase
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "case";

        [JsonPropertyName("stdin")]
        public string Stdin { get; set; } = string.Empty;

        [JsonPropertyName("input")]
        public string? Input { get; set; }

        [JsonPropertyName("rationale")]
        public string Rationale { get; set; } = string.Empty;

        public string ResolvedStdin => string.IsNullOrEmpty(Stdin) ? Input ?? string.Empty : Stdin;
    }

    public static class AdversarialInputPlanner
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static string BuildPrompt(string requirements, string sanitizedCode)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Generate stdin test cases for this student program.");
            sb.AppendLine("Include typical valid input plus cases meant to break it: empty input, invalid types,");
            sb.AppendLine("boundary values, negatives, overflow, extra lines, and very long strings.");
            sb.AppendLine("Assume the program reads from standard input.");
            sb.AppendLine();
            sb.AppendLine("Return JSON only: an array of 4 to 6 objects with keys name, stdin, rationale.");
            sb.AppendLine("Put newlines in stdin as \\n. Do not wrap the JSON in markdown.");
            sb.AppendLine();
            sb.AppendLine("# ASSIGNMENT REQUIREMENTS");
            sb.AppendLine(requirements);
            sb.AppendLine();
            sb.AppendLine("# PROGRAM SOURCE");
            sb.AppendLine(sanitizedCode);
            return sb.ToString();
        }

        public static IReadOnlyList<ExecutionTestCase> ParseOrDefault(string? llmResponse)
        {
            var parsed = TryParse(llmResponse);
            if (parsed.Count > 0)
                return parsed.Take(6).Select(Normalize).ToList();

            return DefaultCases();
        }

        public static IReadOnlyList<ExecutionTestCase> DefaultCases()
        {
            return new[]
            {
                new ExecutionTestCase { Name = "empty", Stdin = "", Rationale = "No input" },
                new ExecutionTestCase { Name = "zero", Stdin = "0\n", Rationale = "Zero / empty numeric value" },
                new ExecutionTestCase { Name = "negative", Stdin = "-1\n", Rationale = "Negative value" },
                new ExecutionTestCase { Name = "non-numeric", Stdin = "not-a-number\n", Rationale = "Invalid type" },
                new ExecutionTestCase { Name = "overflow", Stdin = "2147483648\n", Rationale = "Integer overflow" },
                new ExecutionTestCase { Name = "extra-lines", Stdin = "1\n2\n3\n4\n5\n", Rationale = "More lines than expected" }
            };
        }

        private static List<ExecutionTestCase> TryParse(string? llmResponse)
        {
            if (string.IsNullOrWhiteSpace(llmResponse))
                return new List<ExecutionTestCase>();

            var json = ExtractJsonArray(llmResponse);
            if (json == null)
                return new List<ExecutionTestCase>();

            try
            {
                var cases = JsonSerializer.Deserialize<List<ExecutionTestCase>>(json, JsonOptions);
                return cases?.Where(c => c != null).ToList() ?? new List<ExecutionTestCase>();
            }
            catch
            {
                return new List<ExecutionTestCase>();
            }
        }

        private static string? ExtractJsonArray(string text)
        {
            var start = text.IndexOf('[');
            var end = text.LastIndexOf(']');
            if (start < 0 || end <= start)
                return null;

            return text.Substring(start, end - start + 1);
        }

        private static ExecutionTestCase Normalize(ExecutionTestCase testCase)
        {
            var stdin = testCase.ResolvedStdin ?? string.Empty;
            if (stdin.Length > 32_000)
                stdin = stdin.Substring(0, 32_000);

            stdin = stdin.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);

            return new ExecutionTestCase
            {
                Name = string.IsNullOrWhiteSpace(testCase.Name) ? "case" : testCase.Name.Trim(),
                Stdin = stdin,
                Rationale = testCase.Rationale?.Trim() ?? string.Empty
            };
        }
    }
}
