using Lab_Feedback_WPF.Models;

namespace Lab_Feedback_WPF.Services
{
    public static class LlmCompletionService
    {
        public static async Task<string> CompleteAsync(
            LLMSettings settings,
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default,
            System.Text.Json.JsonElement? responseSchema = null)
        {
            switch (settings.Provider)
            {
                case LLMProvider.AzureOpenAI:
                    var azure = new AzureOpenAIService(
                        settings.AzureEndpoint,
                        settings.AzureApiKey,
                        settings.AzureDeployment);
                    return await azure.CompleteAsync(systemPrompt, userPrompt, cancellationToken);

                case LLMProvider.Ollama:
                default:
                    var ollama = new OllamaService(settings.OllamaBaseUrl, settings.SelectedModel);
                    return await ollama.CompleteAsync(systemPrompt, userPrompt, cancellationToken, responseSchema);
            }
        }
    }
}
