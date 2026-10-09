using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GuidedGrade.Services;

namespace GuidedGrade.Models
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
        [JsonIgnore]
        public string AzureApiKey { get; set; } = "";
        public string AzureDeployment { get; set; } = "";

        public string AzureApiKeyProtected { get; set; } = "";
        public string OpenAIApiKeyProtected { get; set; } = "";
        [JsonIgnore] public string ConfigurationWarning { get; set; } = "";
        // OpenAI settings
        [JsonIgnore]
        public string OpenAIApiKey { get; set; } = "";
        public string OpenAIModel { get; set; } = "gpt-4";

        // Ollama settings
        public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

        // Analysis settings
        public string RequirementsTemplate { get; set; } = "// Assignment requirements";
        public bool ExecuteStudentSubmissions { get; set; } = false;
        public SubmissionExecutionMode ExecutionMode { get; set; } = SubmissionExecutionMode.HyperV;
        public bool ConfirmLocalExecution { get; set; } = true;
        public bool ConfirmGrading { get; set; } = true;
        public int ConsoleModelWaitSeconds { get; set; } = 30;
        [JsonIgnore]
        public TimeSpan ConsoleModelWaitTimeout => TimeSpan.FromSeconds(
            ConsoleModelWaitSeconds is >= 1 and <= 90 ? ConsoleModelWaitSeconds : 30);
        public string RunnerBaseDisk { get; set; } = "";
        public string RunnerCredentialFile { get; set; } = "";
        public string RunnerWorkerFolder { get; set; } = "";
        public int RunnerMemoryMb { get; set; } = 4096;

        private static string SettingsPath => Path.Combine(AppDataPaths.RoamingDirectory, "llm-settings.json");

        /// <summary>
        /// Load settings from file or return defaults
        /// </summary>
        public static LLMSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var settings = ProtectedSettingsStore.Load(SettingsPath);
                    if (settings.MigrateLegacyLocalPaths())
                    {
                        try
                        {
                            settings.Save();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error saving migrated LLM settings: {ex.Message}");
                        }
                    }

                    return settings;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading LLM settings: {ex.Message}");
                return new LLMSettings { ConfigurationWarning = "Saved AI settings could not be loaded: " + ex.Message };
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

                ProtectedSettingsStore.Save(SettingsPath, this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving LLM settings: {ex.Message}");
                throw;
            }
        }

        internal bool MigrateLegacyLocalPaths(string? localRoot = null)
        {
            string Migrate(string path) => localRoot == null
                ? AppDataPaths.MigrateLegacyLocalPath(path)
                : AppDataPaths.MigrateLegacyPath(path, localRoot);

            var credentialFile = Migrate(RunnerCredentialFile);
            var workerFolder = Migrate(RunnerWorkerFolder);
            var changed = !string.Equals(credentialFile, RunnerCredentialFile, StringComparison.Ordinal) ||
                          !string.Equals(workerFolder, RunnerWorkerFolder, StringComparison.Ordinal);
            RunnerCredentialFile = credentialFile;
            RunnerWorkerFolder = workerFolder;
            return changed;
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
