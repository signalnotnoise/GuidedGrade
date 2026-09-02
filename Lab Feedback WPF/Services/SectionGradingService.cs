using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lab_Feedback_WPF.Models;

namespace Lab_Feedback_WPF.Services
{
    /// <summary>
    /// Analyzes code sections against assignment rubrics
    /// </summary>
    public class SectionGradingService
    {
        private readonly LLMSettings _settings;
        private readonly GradingAssignment _assignment;

        public SectionGradingService(GradingAssignment assignment)
        {
            _settings = LLMSettings.Load();
            _assignment = assignment;
        }

        /// <summary>
        /// Analyzes a specific code section against relevant rubric items
        /// </summary>
        public async Task<SectionFeedback> AnalyzeSectionAsync(
            string sectionName, 
            string codeContent, 
            List<RubricItem> relevantRubricItems,
            IEnumerable<string>? identifiersToRedact = null)
        {
            var anonymousName = "section";
            var sanitizedCode = StudentDataSanitizer.Sanitize(codeContent, identifiersToRedact);
            var prompt = BuildSectionPrompt(anonymousName, sanitizedCode, relevantRubricItems);

            string response;

            switch (_settings.Provider)
            {
                case LLMProvider.AzureOpenAI:
                    var azureService = new AzureOpenAIService(
                        _settings.AzureEndpoint,
                        _settings.AzureApiKey,
                        _settings.AzureDeployment);

                    var azureFiles = new List<CodeFile>
                    {
                        new CodeFile { FileName = StudentDataSanitizer.AnonymousFileName(), Content = sanitizedCode }
                    };

                    response = await azureService.AnalyzeCodeAsync(prompt, azureFiles);
                    break;

                case LLMProvider.Ollama:
                default:
                    var ollamaService = new OllamaService(_settings.OllamaBaseUrl, _settings.SelectedModel);
                    var ollamaFiles = new List<OllamaService.CodeFile>
                    {
                        new OllamaService.CodeFile { Name = StudentDataSanitizer.AnonymousFileName(), Content = sanitizedCode }
                    };

                    response = await ollamaService.AnalyzeCodeAsync(ollamaFiles, prompt);
                    break;
            }

            return ParseFeedback(response, sectionName);
        }

        private string BuildSectionPrompt(string sectionName, string code, List<RubricItem> rubricItems)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# ASSIGNMENT CONTEXT");
            sb.AppendLine(_assignment.Requirements);
            sb.AppendLine();

            sb.AppendLine($"# GRADING SECTION: {sectionName}");
            sb.AppendLine();

            sb.AppendLine("# RELEVANT RUBRIC ITEMS");
            foreach (var item in rubricItems)
            {
                sb.AppendLine($"- {item.Name}: {item.MaxPoints} points");
            }
            sb.AppendLine();

            sb.AppendLine("# SUBMITTED CODE");
            sb.AppendLine("```cpp");
            sb.AppendLine(code);
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("# TASK");
            sb.AppendLine($"Analyze the '{sectionName}' implementation against the rubric.");
            sb.AppendLine();
            sb.AppendLine("Provide feedback in this format:");
            sb.AppendLine();
            sb.AppendLine("SCORE: X/" + rubricItems.Sum(r => r.MaxPoints));
            sb.AppendLine("(Provide a numeric score based on correctness and completeness)");
            sb.AppendLine();
            sb.AppendLine("STRENGTHS:");
            sb.AppendLine("- What was done correctly");
            sb.AppendLine();
            sb.AppendLine("ISSUES:");
            sb.AppendLine("- Specific problems or missing requirements");
            sb.AppendLine("- Reference rubric items when relevant");
            sb.AppendLine();
            sb.AppendLine("SUGGESTED FIX:");
            sb.AppendLine("```cpp");
            sb.AppendLine("// Provide corrected or improved code here");
            sb.AppendLine("// Include only the code that needs to change");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("EXPLANATION:");
            sb.AppendLine("- Why these changes improve the code");

            return sb.ToString();
        }

        private SectionFeedback ParseFeedback(string response, string sectionName)
        {
            var feedback = new SectionFeedback { SectionName = sectionName };

            // Parse SCORE
            var scoreMatch = System.Text.RegularExpressions.Regex.Match(response, @"SCORE:\s*(\d+)", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (scoreMatch.Success)
            {
                feedback.SuggestedScore = int.Parse(scoreMatch.Groups[1].Value);
            }

            // Parse STRENGTHS
            var strengthsSection = ExtractSection(response, "STRENGTHS:", "ISSUES:");
            if (!string.IsNullOrEmpty(strengthsSection))
            {
                feedback.Strengths.AddRange(ParseBulletPoints(strengthsSection));
            }

            // Parse ISSUES
            var issuesSection = ExtractSection(response, "ISSUES:", "SUGGESTED FIX:");
            if (!string.IsNullOrEmpty(issuesSection))
            {
                feedback.Issues.AddRange(ParseBulletPoints(issuesSection));
            }

            // Parse SUGGESTED FIX
            var fixMatch = System.Text.RegularExpressions.Regex.Match(response, 
                @"SUGGESTED FIX:[\s\S]*?```(?:cpp)?\s*([\s\S]*?)```",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (fixMatch.Success)
            {
                feedback.SuggestedCode = fixMatch.Groups[1].Value.Trim();
            }

            // Parse EXPLANATION
            var explanationSection = ExtractSection(response, "EXPLANATION:", null);
            if (!string.IsNullOrEmpty(explanationSection))
            {
                feedback.Explanation = explanationSection.Trim();
            }

            return feedback;
        }

        private string ExtractSection(string text, string startMarker, string endMarker)
        {
            var startIndex = text.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
            if (startIndex == -1) return "";

            startIndex += startMarker.Length;

            int endIndex;
            if (!string.IsNullOrEmpty(endMarker))
            {
                endIndex = text.IndexOf(endMarker, startIndex, StringComparison.OrdinalIgnoreCase);
                if (endIndex == -1) endIndex = text.Length;
            }
            else
            {
                endIndex = text.Length;
            }

            return text.Substring(startIndex, endIndex - startIndex);
        }

        private List<string> ParseBulletPoints(string text)
        {
            var points = new List<string>();
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("-") || trimmed.StartsWith("•") || trimmed.StartsWith("*"))
                {
                    points.Add(trimmed.Substring(1).Trim());
                }
                else if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    points.Add(trimmed);
                }
            }

            return points;
        }
    }
}
