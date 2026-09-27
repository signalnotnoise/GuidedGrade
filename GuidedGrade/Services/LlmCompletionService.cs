using GuidedGrade.Models;

namespace GuidedGrade.Services
{
    public static class LlmCompletionService
    {
        public static async Task<string> CompleteAsync(
            LLMSettings settings,
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default,
            System.Text.Json.JsonElement? responseSchema = null,
            LlmJobPriority priority = LlmJobPriority.General, string? jobTitle = null)
        {
            switch (settings.Provider)
            {
                case LLMProvider.AzureOpenAI:
                    var azure = new AzureOpenAIService(
                        settings.AzureEndpoint,
                        settings.AzureApiKey,
                        settings.AzureDeployment);
                    return await azure.CompleteAsync(systemPrompt, userPrompt, cancellationToken, priority, jobTitle);

                case LLMProvider.OpenAI:
                    throw new NotSupportedException("OpenAI is not yet supported. Select Ollama or Azure OpenAI in AI Provider settings.");

                case LLMProvider.Ollama:
                default:
                    var ollama = new OllamaService(settings.OllamaBaseUrl, settings.SelectedModel);
                    return await ollama.CompleteAsync(systemPrompt, userPrompt, cancellationToken, responseSchema, priority, jobTitle);
            }
        }
    }
}
