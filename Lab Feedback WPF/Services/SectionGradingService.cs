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

        public SectionGradingService(GradingAssignment assignment, LLMSettings? settings = null)
        {
            _settings = settings ?? LLMSettings.Load();
            _assignment = assignment;
        }

        /// <summary>
        /// Analyzes a specific code section against relevant rubric items
        /// </summary>
        public async Task<SectionFeedback> AnalyzeSectionAsync(
            string sectionName, 
            string codeContent, 
            List<RubricItem> relevantRubricItems,
            IEnumerable<string>? identifiersToRedact = null,
            IReadOnlyList<RelatedSubmissionFile>? relatedFiles = null,
            string? runtimeExecutionReport = null)
        {
            var sanitizedCode = StudentDataSanitizer.Sanitize(codeContent, identifiersToRedact);
            var sanitizedRelated = SanitizeRelatedFiles(relatedFiles, identifiersToRedact);
            var prompt = BuildSectionPrompt(sectionName, sanitizedCode, relevantRubricItems, sanitizedRelated, runtimeExecutionReport);

            string response;

            switch (_settings.Provider)
            {
                case LLMProvider.AzureOpenAI:
                    var azureService = new AzureOpenAIService(
                        _settings.AzureEndpoint,
                        _settings.AzureApiKey,
                        _settings.AzureDeployment);

                    response = await azureService.AnalyzeCodeAsync(prompt, BuildAzureFiles(sanitizedCode, sanitizedRelated), wrapPrompt: false);
                    break;

                case LLMProvider.Ollama:
                default:
                    var ollamaService = new OllamaService(_settings.OllamaBaseUrl, _settings.SelectedModel);
                    response = await ollamaService.AnalyzeCodeAsync(BuildOllamaFiles(sanitizedCode, sanitizedRelated), prompt, wrapPrompt: false);
                    break;
            }

            return ParseFeedback(response, sectionName);
        }

        private static List<RelatedSubmissionFile> SanitizeRelatedFiles(
            IReadOnlyList<RelatedSubmissionFile>? relatedFiles,
            IEnumerable<string>? identifiersToRedact)
        {
            if (relatedFiles == null || relatedFiles.Count == 0)
                return new List<RelatedSubmissionFile>();

            return relatedFiles.Select((file, index) => new RelatedSubmissionFile
            {
                FileName = StudentDataSanitizer.SafeDisplayName(file.FileName, identifiersToRedact, index + 2),
                FilePath = file.FilePath,
                Extension = file.Extension,
                Content = StudentDataSanitizer.Sanitize(file.Content, identifiersToRedact),
                DeclaredTypes = file.DeclaredTypes
            }).ToList();
        }

        private static List<CodeFile> BuildAzureFiles(
            string sanitizedCode,
            IReadOnlyList<RelatedSubmissionFile> relatedFiles)
        {
            var files = new List<CodeFile>
            {
                new() { FileName = "target.cpp", Content = sanitizedCode }
            };

            files.AddRange(relatedFiles.Select(file => new CodeFile
            {
                FileName = file.FileName,
                Content = file.Content
            }));

            return files;
        }

        private static List<OllamaService.CodeFile> BuildOllamaFiles(
            string sanitizedCode,
            IReadOnlyList<RelatedSubmissionFile> relatedFiles)
        {
            var files = new List<OllamaService.CodeFile>
            {
                new() { Name = "target.cpp", Content = sanitizedCode }
            };

            files.AddRange(relatedFiles.Select(file => new OllamaService.CodeFile
            {
                Name = file.FileName,
                Content = file.Content
            }));

            return files;
        }

        private string BuildSectionPrompt(
            string sectionName,
            string code,
            List<RubricItem> rubricItems,
            IReadOnlyList<RelatedSubmissionFile> relatedFiles,
            string? runtimeExecutionReport)
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

            sb.AppendLine("# TARGET CODE");
            sb.AppendLine("```cpp");
            sb.AppendLine(code);
            sb.AppendLine("```");
            sb.AppendLine();

            if (relatedFiles.Count > 0)
            {
                sb.AppendLine("# SUBMISSION FILE INDEX");
                sb.AppendLine("These files exist in the same student submission. Treat them as implemented code, not as missing work.");
                foreach (var related in relatedFiles)
                {
                    var types = related.DeclaredTypes.Count > 0
                        ? $" (defines {string.Join(", ", related.DeclaredTypes)})"
                        : string.Empty;
                    sb.AppendLine($"- {related.FileName}{types}");
                }
                sb.AppendLine();

                sb.AppendLine("# RELATED FILES");
                sb.AppendLine("These files are first-class parts of the submission. Read them before judging missing classes, methods, or includes.");
                sb.AppendLine("Do NOT deduct points for functions, types, constants, or includes that are defined in these files.");
                sb.AppendLine("Do NOT say a class is missing if it is listed above or defined in a related file.");
                sb.AppendLine();

                foreach (var related in relatedFiles)
                {
                    sb.AppendLine($"=== {related.FileName} ===");
                    sb.AppendLine("```cpp");
                    sb.AppendLine(related.Content);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }

            if (!string.IsNullOrWhiteSpace(runtimeExecutionReport))
            {
                sb.AppendLine(runtimeExecutionReport.Trim());
                sb.AppendLine();
            }

            sb.AppendLine("# TASK");
            sb.AppendLine($"Analyze the '{sectionName}' implementation against the rubric.");
            sb.AppendLine("Account for related files. Do not penalize the target section for code that correctly lives in another file.");
            sb.AppendLine("If a type, class, or method is defined in a related file, it exists. Do not report it as missing.");
            sb.AppendLine("If runtime execution results are present, use them as evidence of crashes, hangs, wrong output, or weak input handling.");
            sb.AppendLine("Do not invent runtime failures that are not listed. Do not deduct for inability to execute if the submission was not runnable.");
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
