using System.Text.Json.Serialization;

namespace GuidedGrade.Models
{
    public class LabResults
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("results")]
        public List<TestResult> Results { get; set; } = new();

        [JsonPropertyName("deductions")]
        public List<object> Deductions { get; set; } = new();
    }

    public class TestResult
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("pointsTotal")]
        public float PointsTotal { get; set; }

        [JsonPropertyName("pointsReceived")]
        public float PointsReceived { get; set; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        // Not in JSON — used for grading form
        public float GradedPoints { get; set; }
        public string Comments { get; set; } = string.Empty;
    }
}