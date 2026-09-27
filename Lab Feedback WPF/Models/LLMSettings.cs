using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lab_Feedback_WPF.Models
{
    /// <summary>
    /// Settings for LLM providers and models
    /// </summary>
    public class LLMSettings
    {
        public LLMProvider Provider { get; set; } = LLMProvider.Ollama;
        public string SelectedModel { get; set; } = "qwen2.5:3b";

        // Azure OpenAI settings
        public string AzureEndpoint { get; set; } = "";
        public string AzureApiKey { get; set; } = "";
        public string AzureDeployment { get; set; } = "";

        // OpenAI settings
        public string OpenAIApiKey { get; set; } = "";
        public string OpenAIModel { get; set; } = "gpt-4";

        // Ollama settings
        public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

        // Analysis settings
        public string RequirementsTemplate { get; set; } = "// Assignment requirements";
        public bool ExecuteStudentSubmissions { get; set; } = false;
        public SubmissionExecutionMode ExecutionMode { get; set; } = SubmissionExecutionMode.HyperV;
        public bool ConfirmLocalExecution { get; set; } = true;
        public string RunnerBaseDisk { get; set; } = "";
        public string RunnerCredentialFile { get; set; } = "";
        public string RunnerWorkerFolder { get; set; } = "";
        public int RunnerMemoryMb { get; set; } = 4096;

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LabFeedbackWPF",
            "llm-settings.json"
        );

        /// <summary>
        /// Load settings from file or return defaults
        /// </summary>
        public static LLMSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<LLMSettings>(json) ?? new LLMSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading LLM settings: {ex.Message}");
            }

            return new LLMSettings();
        }

        /// <summary>
        /// Save settings to file
        /// </summary>
        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving LLM settings: {ex.Message}");
                throw;
            }
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SubmissionExecutionMode
    {
        HyperV,
        Local
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LLMProvider
    {
        Ollama,
        AzureOpenAI,
        OpenAI
    }
}
