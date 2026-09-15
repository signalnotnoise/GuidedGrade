using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Lab_Feedback_WPF.Services
{
    /// <summary>
    /// Service for analyzing code using local Ollama models.
    /// Ollama provides a local LLM inference server compatible with OpenAI-style APIs.
    /// </summary>
    public class OllamaService
    {
        private readonly string _baseUrl;
        private readonly string _model;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Creates a new Ollama service instance.
        /// </summary>
        /// <param name="baseUrl">Ollama server URL (default: http://localhost:11434)</param>
        /// <param name="model">Model name (e.g., "qwen2.5:3b", "codellama:7b")</param>
        public OllamaService(string baseUrl = "http://localhost:11434", string model = "qwen2.5:3b")
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _model = model;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(15) // Large models on CPU can take time
            };
        }

        /// <summary>
        /// Analyzes code files against requirements using the local Ollama model.
        /// </summary>
        /// <param name="files">List of code files with content</param>
        /// <param name="requirements">Assignment requirements</param>
        /// <returns>Formatted feedback from the model</returns>
        public async Task<string> AnalyzeCodeAsync(List<CodeFile> files, string requirements, bool wrapPrompt = true)
        {
            var prompt = wrapPrompt ? BuildAnalysisPrompt(files, requirements) : requirements;
            return await CompleteAsync(
                "You are an expert programming instructor providing constructive feedback on anonymous submitted code. " +
                "Focus on correctness, code quality, and meeting requirements. " +
                "Related headers and source files in the prompt are part of the same submission; do not claim those types or files are missing. " +
                "Do not request, infer, or mention student names, IDs, emails, file paths, or other personal data. " +
                "Follow the output format requested in the user prompt.",
                prompt);
        }

        internal OllamaService(HttpClient client, string model)
        {
            _baseUrl = "http://localhost:11434";
            _model = model;
            _httpClient = client;
        }

        public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default, JsonElement? responseSchema = null)
        {
            var request = new OllamaChatRequest
            {
                Model = _model,
                Messages = new List<OllamaMessage>
                {
                    new OllamaMessage { Role = "system", Content = systemPrompt },
                    new OllamaMessage { Role = "user", Content = userPrompt }
                },
                Stream = false,
                Format = responseSchema,
                Options = responseSchema.HasValue ? new { temperature = 0 } : null
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var jsonContent = JsonSerializer.Serialize(request, jsonOptions);
            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync($"{_baseUrl}/api/chat", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var chatResponse = JsonSerializer.Deserialize<OllamaChatResponse>(responseBody, jsonOptions);

            return chatResponse?.Message?.Content ?? "No response from model.";
        }

        /// <summary>
        /// Checks if Ollama server is running and the model is available.
        /// </summary>
        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
                if (!response.IsSuccessStatusCode)
                    return false;

                var body = await response.Content.ReadAsStringAsync();

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var tags = JsonSerializer.Deserialize<OllamaTagsResponse>(body, jsonOptions);

                // Check if our model is in the list
                var modelExists = tags?.Models?.Any(m => m.Name == _model) ?? false;

                // Debug output
                System.Diagnostics.Debug.WriteLine($"Ollama availability check:");
                System.Diagnostics.Debug.WriteLine($"  Looking for model: {_model}");
                System.Diagnostics.Debug.WriteLine($"  Available models: {string.Join(", ", tags?.Models?.Select(m => m.Name) ?? new List<string>())}");
                System.Diagnostics.Debug.WriteLine($"  Model found: {modelExists}");

                return modelExists;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ollama availability check failed: {ex.Message}");
                return false;
            }
        }

        private string BuildAnalysisPrompt(List<CodeFile> files, string requirements)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# ASSIGNMENT REQUIREMENTS");
            sb.AppendLine(requirements);
            sb.AppendLine();

            sb.AppendLine("# SUBMITTED CODE");
            var index = 1;
            foreach (var file in files)
            {
                sb.AppendLine($"=== {StudentDataSanitizer.SafeDisplayName(file.Name, index: index)} ===");
                sb.AppendLine(StudentDataSanitizer.Sanitize(file.Content));
                sb.AppendLine();
                index++;
            }

            sb.AppendLine("# TASK");
            sb.AppendLine("Analyze the submitted code against the assignment requirements.");
            sb.AppendLine();
            sb.AppendLine("Provide feedback in the following format:");
            sb.AppendLine();
            sb.AppendLine("STRENGTHS:");
            sb.AppendLine("- List what was done well");
            sb.AppendLine("- Note correct implementations");
            sb.AppendLine();
            sb.AppendLine("ISSUES:");
            sb.AppendLine("- List problems, bugs, or missing requirements");
            sb.AppendLine("- Be specific about what needs fixing");
            sb.AppendLine();
            sb.AppendLine("SUGGESTIONS:");
            sb.AppendLine("- Provide specific, actionable improvement recommendations");
            sb.AppendLine("- Focus on learning opportunities");
            sb.AppendLine();
            sb.AppendLine("SCORE: X/100");
            sb.AppendLine("- Provide an overall score based on correctness and quality");

            return sb.ToString();

        }

        #region DTOs

        public class CodeFile
        {
            public string Name { get; set; }
            public string Content { get; set; }
        }

        private class OllamaChatRequest
        {
            public JsonElement? Format { get; set; }
            public object? Options { get; set; }
            public string Model { get; set; }
            public List<OllamaMessage> Messages { get; set; }
            public bool Stream { get; set; }
        }

        private class OllamaMessage
        {
            public string Role { get; set; }
            public string Content { get; set; }
        }

        private class OllamaChatResponse
        {
            public string Model { get; set; }
            public OllamaMessage Message { get; set; }
        }

        private class OllamaTagsResponse
        {
            public List<OllamaModel> Models { get; set; }
        }

        private class OllamaModel
        {
            public string Name { get; set; }
        }

        #endregion
    }
}
