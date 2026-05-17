namespace BookFinder.Application.Configuration;

public sealed class AiProviderOptions
{
    // Active provider. Only "Gemini" is wired up; see Program.cs comments for others.
    public string Provider { get; set; } = "Gemini";

    // --- Gemini (Google.GenAI) ---
    public string GeminiApiKey { get; set; } = string.Empty;
    public string GeminiModel { get; set; } = "gemini-2.5-flash";

    // --- OpenAI (uncomment when adding Microsoft.Extensions.AI.OpenAI) ---
    // public string OpenAiApiKey { get; set; } = string.Empty;
    // public string OpenAiModel { get; set; } = "gpt-4o-mini";

    // --- Azure OpenAI (uncomment when adding Azure.AI.OpenAI) ---
    // public string AzureOpenAiEndpoint { get; set; } = string.Empty;
    // public string AzureOpenAiApiKey { get; set; } = string.Empty;
    // public string AzureOpenAiDeploymentName { get; set; } = string.Empty;
}
