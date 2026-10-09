using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using GuidedGrade.Models;
using GuidedGrade.Presentation;
using UI_Framework;
using static UI_Framework.UI;

namespace GuidedGrade.Windows
{
    public sealed class LLMSettingsWindow : ReviewWindow
    {
        private LLMSettings _settings = new();

        public LLMSettingsWindow()
        {
            Title = "AI Provider settings"; Width = 720; Height = 820; MinWidth = 460; MinHeight = 400;
            LoadSettings();
            ShowView(BuildView);
        }

        private void LoadSettings()
        {
            _settings = LLMSettings.Load();
            if (!string.IsNullOrEmpty(_settings.ConfigurationWarning))
                MessageBox.Show(_settings.ConfigurationWarning, "AI credentials", MessageBoxButton.OK, MessageBoxImage.Warning);

            _provider.Value = _settings.Provider == LLMProvider.AzureOpenAI ? 1 : 0;

            // Ollama
            txtOllamaUrl.Value = _settings.OllamaBaseUrl;
            cmbOllamaModel.Value = _settings.SelectedModel;

            // Azure
            txtAzureEndpoint.Value = _settings.AzureEndpoint;
            txtAzureApiKey.Value = _settings.AzureApiKey;
            cmbAzureDeployment.Value = _settings.AzureDeployment;


            // Requirements
            txtRequirements.Value = _settings.RequirementsTemplate;
            chkExecuteSubmissions.Value = _settings.ExecuteStudentSubmissions;
            chkConfirmLocalExecution.Value = _settings.ConfirmLocalExecution;
            chkConfirmGrading.Value = _settings.ConfirmGrading;
            txtConsoleModelWait.Value = ((int)_settings.ConsoleModelWaitTimeout.TotalSeconds).ToString();
            cmbExecutionMode.Value = _settings.ExecutionMode == SubmissionExecutionMode.Local ? 1 : 0;
            txtRunnerBaseDisk.Value = _settings.RunnerBaseDisk;
            txtRunnerCredentialFile.Value = _settings.RunnerCredentialFile;
            txtRunnerWorkerFolder.Value = _settings.RunnerWorkerFolder;
            txtRunnerMemory.Value = _settings.RunnerMemoryMb.ToString();

        }

        private readonly State<string> txtOllamaUrl = new("");
        private readonly State<string> cmbOllamaModel = new("");
        private readonly State<string> txtAzureEndpoint = new("");
        private readonly State<string> txtAzureApiKey = new("");
        private readonly State<string> cmbAzureDeployment = new("");
        private readonly State<string> txtRequirements = new("");
        private readonly State<string> txtRunnerBaseDisk = new("");
        private readonly State<string> txtRunnerCredentialFile = new("");
        private readonly State<string> txtRunnerWorkerFolder = new("");
        private readonly State<string> txtRunnerMemory = new("");
        private readonly State<string> txtOllamaStatus = new("");
        private readonly State<string> txtAzureStatus = new("");
        private readonly State<int> _provider = new(0);
        private readonly State<int> cmbExecutionMode = new(0);
        private readonly State<bool> chkExecuteSubmissions = new(false);
        private readonly State<bool> chkConfirmLocalExecution = new(true);
        private readonly State<bool> chkConfirmGrading = new(true);
        private readonly State<string> txtConsoleModelWait = new("30");
        private readonly StateList<string> _models = new();
        private View BuildView()
        {
            var provider = (LLMProvider)_provider.Value;
            View fields = provider switch
            {
                LLMProvider.AzureOpenAI => VStack(
                    Text("Azure endpoint"), TextField(txtAzureEndpoint).AccessibilityLabel("Azure endpoint"),
                    Text("API key"), PasswordField(txtAzureApiKey).AccessibilityLabel("Azure API key"),
                    Text("Deployment"), TextField(cmbAzureDeployment).AccessibilityLabel("Azure deployment"),
                    Button("Test Azure connection", () => TestAzure_Click(this, new())), Text(txtAzureStatus.Value)).Spacing(8).Id("azure"),
                _ => VStack(Text("Ollama server"), TextField(txtOllamaUrl).AccessibilityLabel("Ollama server"),
                    Text("Model name"), TextField(cmbOllamaModel).AccessibilityLabel("Ollama model"),
                    Picker(_models.ToArray(), new Binding<int>(() => _models.ToList().IndexOf(cmbOllamaModel.Value), index =>
                    { if (index >= 0 && index < _models.Count) cmbOllamaModel.Value = _models[index]; })).AccessibilityLabel("Available models"),
                    Button("Refresh models", () => RefreshOllamaModels_Click(this, new())), Text(txtOllamaStatus.Value)).Spacing(8).Id("ollama")
            };
            return Scroll(VStack(
                Text("AI Provider settings").FontSize(22),
                Picker(new[] { "Ollama", "Azure OpenAI" }, _provider).AccessibilityLabel("AI provider"), fields,
                Text("Default assignment requirements"), TextEditor(txtRequirements).Height(140).AccessibilityLabel("Default requirements"),
                Toggle("Ask before grading", chkConfirmGrading),
                Text("Console model wait timeout (1â€“90 seconds)"),
                TextField(txtConsoleModelWait).AccessibilityLabel("Console model wait timeout"),
                Text("Default: 30 seconds per model decision. The overall console test remains limited to 90 seconds.").FontSize(13),
                Toggle("Build and run student programs during analysis", chkExecuteSubmissions),
                Text("Execution environment"), Picker(new[] { "VM (Hyper-V)", "Local computer" }, cmbExecutionMode).AccessibilityLabel("Execution environment"),
                Toggle("Ask before each local build or run", chkConfirmLocalExecution),
                Text("Local code uses your Windows permissions. VM failures never fall back to local automatically.").FontSize(13),
                Text("Windows template disk (.vhdx)"), TextField(txtRunnerBaseDisk).AccessibilityLabel("Windows template disk"),
                Text("Guest credential file (.xml)"), TextField(txtRunnerCredentialFile).AccessibilityLabel("Guest credential file"),
                Text("Published runner folder"), TextField(txtRunnerWorkerFolder).AccessibilityLabel("Published runner folder"),
                Text("VM memory (2048â€“16384 MB)"), TextField(txtRunnerMemory).AccessibilityLabel("VM memory"),
                HStack(Button("Save", () => Save_Click(this, new())).ButtonStyle(ButtonStyleKind.Primary), Button("Cancel", () => Cancel_Click(this, new()))).Spacing(8)
            ).Spacing(12).Padding(20));
        }

        private async void RefreshOllamaModels_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtOllamaStatus.Value = "Fetching available models...";


                var url = txtOllamaUrl.Value.TrimEnd('/');
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var response = await client.GetAsync($"{url}/api/tags");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<OllamaTagsResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result?.Models != null && result.Models.Any())
                    {
                        _models.Clear();
                        foreach (var model in result.Models.OrderBy(m => m.Name))
                        {
                            _models.Add(model.Name);
                        }

                        if (_models.Count > 0)
                        {
                            cmbOllamaModel.Value = _models[0];
                        }

                        txtOllamaStatus.Value = $"Found {result.Models.Count} model(s)";

                    }
                    else
                    {
                        txtOllamaStatus.Value = "No models found. Run 'ollama pull <model>'";

                    }
                }
                else
                {
                    txtOllamaStatus.Value = $"HTTP {response.StatusCode}";

                }
            }
            catch (Exception ex)
            {
                txtOllamaStatus.Value = ex.Message;

            }
        }

        private async void TestAzure_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtAzureStatus.Value = "Testing connection...";


                var endpoint = txtAzureEndpoint.Value.TrimEnd('/');
                var apiKey = txtAzureApiKey.Value;
                var deployment = cmbAzureDeployment.Value;

                if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(deployment))
                {
                    txtAzureStatus.Value = "Please fill all fields";

                    return;
                }

                var azureService = new Services.AzureOpenAIService(endpoint, apiKey, deployment);

                // Simple test with minimal prompt
                var testFiles = new List<Services.CodeFile>
                {
                    new Services.CodeFile { FileName = "test.txt", Content = "int x = 1;" }
                };

                await azureService.AnalyzeCodeAsync("Test requirements", testFiles);

                txtAzureStatus.Value = "Connection successful!";

            }
            catch (Exception ex)
            {
                txtAzureStatus.Value = ex.Message;

            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _settings.Provider = (LLMProvider)_provider.Value;

                // Save all settings
                _settings.OllamaBaseUrl = txtOllamaUrl.Value;
                _settings.SelectedModel = cmbOllamaModel.Value;

                _settings.AzureEndpoint = txtAzureEndpoint.Value;
                _settings.AzureApiKey = txtAzureApiKey.Value;
                _settings.AzureDeployment = cmbAzureDeployment.Value;


                _settings.RequirementsTemplate = txtRequirements.Value;
                _settings.ExecuteStudentSubmissions = chkExecuteSubmissions.Value == true;
                if (!int.TryParse(txtRunnerMemory.Value, out var memory) || memory is < 2048 or > 16384)
                    throw new InvalidOperationException("VM memory must be between 2048 and 16384 MB.");
                if (!int.TryParse(txtConsoleModelWait.Value, out var waitSeconds) || waitSeconds is < 1 or > 90)
                    throw new InvalidOperationException("Console model wait timeout must be between 1 and 90 seconds.");
                _settings.ConsoleModelWaitSeconds = waitSeconds;
                _settings.ConfirmGrading = chkConfirmGrading.Value;
                _settings.ConfirmLocalExecution = chkConfirmLocalExecution.Value == true;
                _settings.ExecutionMode = cmbExecutionMode.Value == 1 ? SubmissionExecutionMode.Local : SubmissionExecutionMode.HyperV;
                _settings.RunnerBaseDisk = txtRunnerBaseDisk.Value.Trim();
                _settings.RunnerCredentialFile = txtRunnerCredentialFile.Value.Trim();
                _settings.RunnerWorkerFolder = txtRunnerWorkerFolder.Value.Trim();
                _settings.RunnerMemoryMb = memory;

                _settings.Save();

                MessageBox.Show("Settings saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private class OllamaTagsResponse
        {
            public List<OllamaModelInfo>? Models { get; set; }
        }

        private class OllamaModelInfo
        {
            public required string Name { get; set; }
        }
    }
}
