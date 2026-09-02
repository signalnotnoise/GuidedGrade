using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Lab_Feedback_WPF.Models;

namespace Lab_Feedback_WPF.Windows
{
    public partial class LLMSettingsWindow : Window
    {
        private LLMSettings _settings;

        public LLMSettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            _settings = LLMSettings.Load();

            // Set provider
            switch (_settings.Provider)
            {
                case LLMProvider.Ollama:
                    radioOllama.IsChecked = true;
                    break;
                case LLMProvider.AzureOpenAI:
                    radioAzure.IsChecked = true;
                    break;
                case LLMProvider.OpenAI:
                    radioOpenAI.IsChecked = true;
                    break;
            }

            // Ollama
            txtOllamaUrl.Text = _settings.OllamaBaseUrl;
            cmbOllamaModel.Text = _settings.SelectedModel;

            // Azure
            txtAzureEndpoint.Text = _settings.AzureEndpoint;
            txtAzureApiKey.Password = _settings.AzureApiKey;
            cmbAzureDeployment.Text = _settings.AzureDeployment;

            // OpenAI
            txtOpenAIApiKey.Password = _settings.OpenAIApiKey;
            cmbOpenAIModel.Text = _settings.OpenAIModel;

            // Requirements
            txtRequirements.Text = _settings.RequirementsTemplate;

            UpdateProviderVisibility();
        }

        private void Provider_Changed(object sender, RoutedEventArgs e)
        {
            UpdateProviderVisibility();
        }

        private void UpdateProviderVisibility()
        {
            // Null check - controls may not be initialized yet during InitializeComponent
            if (groupOllama == null || groupAzure == null || groupOpenAI == null)
                return;

            groupOllama.Visibility = radioOllama.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            groupAzure.Visibility = radioAzure.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            groupOpenAI.Visibility = radioOpenAI.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void RefreshOllamaModels_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtOllamaStatus.Text = "Fetching available models...";
                txtOllamaStatus.Foreground = System.Windows.Media.Brushes.Yellow;

                var url = txtOllamaUrl.Text.TrimEnd('/');
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
                        cmbOllamaModel.Items.Clear();
                        foreach (var model in result.Models.OrderBy(m => m.Name))
                        {
                            cmbOllamaModel.Items.Add(model.Name);
                        }

                        if (cmbOllamaModel.Items.Count > 0)
                        {
                            cmbOllamaModel.SelectedIndex = 0;
                        }

                        txtOllamaStatus.Text = $"✓ Found {result.Models.Count} model(s)";
                        txtOllamaStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
                    }
                    else
                    {
                        txtOllamaStatus.Text = "⚠ No models found. Run 'ollama pull <model>'";
                        txtOllamaStatus.Foreground = System.Windows.Media.Brushes.Orange;
                    }
                }
                else
                {
                    txtOllamaStatus.Text = $"✗ HTTP {response.StatusCode}";
                    txtOllamaStatus.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                txtOllamaStatus.Text = $"✗ {ex.Message}";
                txtOllamaStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private async void TestAzure_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtAzureStatus.Text = "Testing connection...";
                txtAzureStatus.Foreground = System.Windows.Media.Brushes.Yellow;

                var endpoint = txtAzureEndpoint.Text.TrimEnd('/');
                var apiKey = txtAzureApiKey.Password;
                var deployment = cmbAzureDeployment.Text;

                if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(deployment))
                {
                    txtAzureStatus.Text = "✗ Please fill all fields";
                    txtAzureStatus.Foreground = System.Windows.Media.Brushes.Red;
                    return;
                }

                var azureService = new Services.AzureOpenAIService(endpoint, apiKey, deployment);

                // Simple test with minimal prompt
                var testFiles = new List<Services.CodeFile>
                {
                    new Services.CodeFile { FileName = "test.txt", Content = "int x = 1;" }
                };

                await azureService.AnalyzeCodeAsync("Test requirements", testFiles);

                txtAzureStatus.Text = "✓ Connection successful!";
                txtAzureStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
            }
            catch (Exception ex)
            {
                txtAzureStatus.Text = $"✗ {ex.Message}";
                txtAzureStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private async void TestOpenAI_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtOpenAIStatus.Text = "Testing connection...";
                txtOpenAIStatus.Foreground = System.Windows.Media.Brushes.Yellow;

                var apiKey = txtOpenAIApiKey.Password;
                var model = cmbOpenAIModel.Text;

                if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
                {
                    txtOpenAIStatus.Text = "✗ Please fill all fields";
                    txtOpenAIStatus.Foreground = System.Windows.Media.Brushes.Red;
                    return;
                }

                // TODO: Implement OpenAI test when we add OpenAI service
                txtOpenAIStatus.Text = "✓ OpenAI integration coming soon!";
                txtOpenAIStatus.Foreground = System.Windows.Media.Brushes.Yellow;
            }
            catch (Exception ex)
            {
                txtOpenAIStatus.Text = $"✗ {ex.Message}";
                txtOpenAIStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Determine provider
                if (radioOllama.IsChecked == true)
                    _settings.Provider = LLMProvider.Ollama;
                else if (radioAzure.IsChecked == true)
                    _settings.Provider = LLMProvider.AzureOpenAI;
                else if (radioOpenAI.IsChecked == true)
                    _settings.Provider = LLMProvider.OpenAI;

                // Save all settings
                _settings.OllamaBaseUrl = txtOllamaUrl.Text;
                _settings.SelectedModel = cmbOllamaModel.Text;

                _settings.AzureEndpoint = txtAzureEndpoint.Text;
                _settings.AzureApiKey = txtAzureApiKey.Password;
                _settings.AzureDeployment = cmbAzureDeployment.Text;

                _settings.OpenAIApiKey = txtOpenAIApiKey.Password;
                _settings.OpenAIModel = cmbOpenAIModel.Text;

                _settings.RequirementsTemplate = txtRequirements.Text;

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
            public List<OllamaModelInfo> Models { get; set; }
        }

        private class OllamaModelInfo
        {
            public string Name { get; set; }
        }
    }
}
