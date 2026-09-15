using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lab_Feedback_WPF.Services
{
    public class AzureOpenAIService
    {
        private readonly string _endpoint;
        private readonly string _apiKey;
        private readonly string _deploymentName;
        private readonly HttpClient _httpClient;

        public AzureOpenAIService(string endpoint, string apiKey, string deploymentName)
        {
            _endpoint = endpoint.TrimEnd('/');
            _apiKey = apiKey;
            _deploymentName = deploymentName;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
        }

        public async Task<string> AnalyzeCodeAsync(
            string requirements,
            List<CodeFile> files,
            CancellationToken cancellationToken = default,
            bool wrapPrompt = true)
        {
            try
            {
                Debug.WriteLine("=== Azure OpenAI Analysis Started ===");

                var prompt = wrapPrompt ? BuildAnalysisPrompt(requirements, files) : requirements;
                return await CompleteAsync(
                    "You are a code review assistant for a programming instructor. Analyze anonymous submitted code against requirements and provide constructive, specific feedback. Related headers and source files in the prompt are part of the same submission; do not claim those types or files are missing. Do not request, infer, or mention student names, IDs, emails, file paths, or other personal data. Follow the output format requested in the user prompt.",
                    prompt,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Azure OpenAI Exception: {ex.Message}");
                return $"Error analyzing code: {ex.Message}";
            }
        }

        public async Task<string> CompleteAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var requestBody = new
                {
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    temperature = 0.3,
                    max_tokens = 2000
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"{_endpoint}/openai/deployments/{_deploymentName}/chat/completions?api-version=2024-02-15-preview";
                Debug.WriteLine($"Request URL: {url}");

                var response = await _httpClient.PostAsync(url, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Azure OpenAI Error: {response.StatusCode} - {error}");
                    return $"Error: {response.StatusCode}\n{error}";
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson);

                var feedback = result?.Choices?[0]?.Message?.Content ?? "No response received";

                Debug.WriteLine("=== Azure OpenAI Analysis Complete ===");
                return feedback;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Azure OpenAI Exception: {ex.Message}");
                return $"Error analyzing code: {ex.Message}";
            }
        }

        private string BuildAnalysisPrompt(string requirements, List<CodeFile> files)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# ASSIGNMENT REQUIREMENTS");
            sb.AppendLine(requirements);
            sb.AppendLine();
            sb.AppendLine("# SUBMITTED CODE");
            sb.AppendLine();

            var index = 1;
            foreach (var file in files)
            {
                sb.AppendLine($"=== {StudentDataSanitizer.SafeDisplayName(file.FileName, index: index)} ===");
                sb.AppendLine(StudentDataSanitizer.Sanitize(file.Content));
                sb.AppendLine();
                index++;
            }

            sb.AppendLine("# TASK");
            sb.AppendLine("Analyze the code against the requirements. Provide:");
            sb.AppendLine("1. STRENGTHS: What was done well");
            sb.AppendLine("2. ISSUES: Problems, bugs, or missing requirements");
            sb.AppendLine("3. SUGGESTIONS: Specific improvements");
            sb.AppendLine("4. SCORE: Overall score out of 100");

            return sb.ToString();
        }

        private class ChatCompletionResponse
        {
            [JsonPropertyName("choices")]
            public Choice[]? Choices { get; set; }
        }

        private class Choice
        {
            [JsonPropertyName("message")]
            public Message? Message { get; set; }
        }

        private class Message
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }
        }
    }

    public class CodeFile
    {
        public string FileName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
