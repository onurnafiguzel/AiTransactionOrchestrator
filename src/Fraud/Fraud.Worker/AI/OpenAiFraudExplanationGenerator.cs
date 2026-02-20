using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OpenAI;
using BuildingBlocks.Contracts.Resiliency;
using Polly;

namespace Fraud.Worker.AI;

public sealed class OpenAiFraudExplanationGenerator : IFraudExplanationGenerator
{
    private readonly IOptions<FraudExplanationOptions> _options;
    private readonly ILogger<OpenAiFraudExplanationGenerator> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;

    public OpenAiFraudExplanationGenerator(
        IOptions<FraudExplanationOptions> options,
        ILogger<OpenAiFraudExplanationGenerator> logger,
        ResiliencePipelineFactory pipelineFactory)
    {
        _options = options;
        _logger = logger;
        
        // Create custom retry pipeline for external API calls
        _resiliencePipeline = pipelineFactory.CreateCustomPipeline(
            shouldHandle: ex => 
                ex is HttpRequestException || 
                ex is TaskCanceledException ||
                ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase),
            options: new RetryOptions
            {
                MaxRetryAttempts = options.Value.MaxRetries,
                InitialDelaySeconds = 1.0,
                MaxDelaySeconds = 10.0,
                UseJitter = true
            });
    }

    public async Task<string> GenerateAsync(
        decimal amount,
        string currency,
        int riskScore,
        string decision,
        string merchantId,
        string correlationId,
        CancellationToken ct)
    {
        var opt = _options.Value;

        if (!opt.Enabled)
            return "LLM explanation disabled by configuration.";

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set.");

        try
        {
            return await _resiliencePipeline.ExecuteAsync(async token =>
            {
                var client = new ChatClient(model: "gpt-3.5-turbo", apiKey: apiKey);
                var prompt = BuildPrompt(amount, currency, riskScore, decision, merchantId);

                _logger.LogDebug("Sending OpenAI request for transaction {CorrelationId} with model {Model}.",
                    correlationId, opt.Model);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(opt.TimeoutSeconds));

                try
                {
                    // OpenAI SDK 2.8.0 API
                    var response = await client.CompleteChatAsync(
                        new List<ChatMessage>
                        {
                            new SystemChatMessage("You are a fraud detection analyst. Generate brief, clear explanations for fraud decisions."),
                            new UserChatMessage(prompt)
                        },
                        new ChatCompletionOptions
                        {
                            Temperature = 0.7f,
                            MaxOutputTokenCount = 200
                        },
                        timeoutCts.Token);

                    var explanation = response.Value.Content.FirstOrDefault()?.Text;

                    if (string.IsNullOrWhiteSpace(explanation))
                    {
                        _logger.LogWarning("OpenAI returned empty response for transaction {CorrelationId}.", correlationId);
                        throw new InvalidOperationException("OpenAI response was empty.");
                    }

                    _logger.LogInformation("Successfully generated fraud explanation for transaction {CorrelationId}.",
                        correlationId);

                    return explanation.Trim();
                }
                catch (OperationCanceledException ex)
                {
                    _logger.LogError(ex, "OpenAI request timed out after {TimeoutSeconds}s for transaction {CorrelationId}. Consider increasing TimeoutSeconds in config.",
                        opt.TimeoutSeconds, correlationId);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OpenAI API call failed for transaction {CorrelationId}. Error: {ErrorMessage}",
                        correlationId, ex.Message);
                    throw;
                }
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI explanation generation failed for transaction {CorrelationId}.", correlationId);
            throw new InvalidOperationException("Failed to generate fraud explanation via OpenAI.", ex);
        }
    }

    private static string BuildPrompt(decimal amount, string currency, int riskScore, string decision, string merchantId)
        => $$"""
            Analyze this transaction and provide a brief fraud risk assessment:

            **Transaction Details:**
            - Merchant ID: {{merchantId}}
            - Amount: {{amount}} {{currency}}
            - Risk Score: {{riskScore}}/100
            - Decision: {{decision}}

            **Requirements:**
            - Provide 2-3 sentences explaining the fraud decision
            - Reference the amount and risk score
            - Use clear, business-appropriate language
            - Do not mention OpenAI or AI models
            - Focus on why this decision was made based on risk factors
            """;
}
