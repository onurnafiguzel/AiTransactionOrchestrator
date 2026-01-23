namespace Fraud.Worker.AI;

public sealed class FraudExplanationOptions
{
    public bool Enabled { get; init; } = true;
    public string Model { get; init; } = "gpt-4.1-mini";
    public int TimeoutSeconds { get; init; } = 4;
    public int MaxRetries { get; init; } = 2;
}
