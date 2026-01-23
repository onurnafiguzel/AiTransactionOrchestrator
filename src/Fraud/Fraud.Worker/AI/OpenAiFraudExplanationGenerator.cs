using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fraud.Worker.AI;

public sealed class OpenAiFraudExplanationGenerator(
    HttpClient http,
    IOptions<FraudExplanationOptions> options,
    ILogger<OpenAiFraudExplanationGenerator> logger)
    : IFraudExplanationGenerator
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<string> GenerateAsync(
        decimal amount,
        string currency,
        int riskScore,
        string decision,
        string merchantId,
        string correlationId,
        CancellationToken ct)
    {
        var opt = options.Value;

        if (!opt.Enabled)
            return "LLM explanation disabled by configuration.";

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
       
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(opt.TimeoutSeconds));

        var prompt = BuildPrompt(amount, currency, riskScore, decision, merchantId);

        // Responses API request shape (see docs)
        var payload = new
        {
            model = opt.Model,
            input = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "input_text", text = prompt }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(payload, JsonOpts);

        // retry loop (2 retries default)
        var maxRetries = Math.Max(0, opt.MaxRetries);
        Exception? last = null;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                req.Headers.Add("X-Correlation-Id", correlationId); // internal tracing (optional)
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);

                var body = await res.Content.ReadAsStringAsync(timeoutCts.Token);

                if (!res.IsSuccessStatusCode)
                {
                    logger.LogWarning(
                        "OpenAI Responses API failed. Status={StatusCode} Attempt={Attempt} Body={Body}",
                        (int)res.StatusCode, attempt + 1, Truncate(body, 500));

                    // retry only on 429/5xx
                    if ((int)res.StatusCode == 429 || (int)res.StatusCode >= 500)
                        throw new HttpRequestException($"OpenAI API error {(int)res.StatusCode}");

                    // non-retriable
                    throw new InvalidOperationException($"OpenAI API request failed with status {(int)res.StatusCode}");
                }

                var text = ExtractOutputText(body);
                if (string.IsNullOrWhiteSpace(text))
                    throw new InvalidOperationException("OpenAI response text was empty.");

                return text.Trim();
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                last = ex;

                var delayMs = (int)(300 * Math.Pow(2, attempt)); // 300, 600, 1200...
                logger.LogWarning(ex, "OpenAI call failed, retrying in {DelayMs}ms (attempt {Attempt}/{Max}).",
                    delayMs, attempt + 1, maxRetries + 1);

                await Task.Delay(delayMs, ct);
            }
            catch (Exception ex)
            {
                last = ex;
                break;
            }
        }

        throw new InvalidOperationException("OpenAI explanation generation failed after retries.", last);
    }

    private static string BuildPrompt(decimal amount, string currency, int riskScore, string decision, string merchantId)
        => $$"""
You are generating a short fraud decision explanation for an internal payment platform.

Facts:
- merchantId: {{merchantId}}
- amount: {{amount}} {{currency}}
- riskScore: {{riskScore}} (0-100)
- decision: {{decision}}

Requirements:
- Output 2-4 sentences.
- No policy/legal advice.
- Do not mention OpenAI or "LLM".
- Explain the decision in plain business language, referencing amount and riskScore.
""";

    private static string ExtractOutputText(string json)
    {
        // Responses API returns an "output" array with content chunks.
        // We'll defensively parse common locations.
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            return string.Empty;

        var sb = new StringBuilder();

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var c in content.EnumerateArray())
            {
                // often: { "type": "output_text", "text": "..." }
                if (c.TryGetProperty("type", out var t) && t.GetString() == "output_text"
                    && c.TryGetProperty("text", out var text))
                {
                    sb.Append(text.GetString());
                }
            }
        }

        return sb.ToString();
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "...";
}
