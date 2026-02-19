using System.Diagnostics.Metrics;

namespace BuildingBlocks.Observability;

/// <summary>
/// Custom metrics for AiTransaction system
/// </summary>
public sealed class AiTransactionMetrics
{
    private readonly Meter _meter;

    // Counters
    public Counter<long> TransactionCreatedCounter { get; }
    public Counter<long> FraudDetectedCounter { get; }
    public Counter<long> TransactionApprovedCounter { get; }
    public Counter<long> TransactionRejectedCounter { get; }
    public Counter<long> ExceptionCounter { get; }

    // Histograms
    public Histogram<double> FraudDetectionDurationHistogram { get; }
    public Histogram<double> TransactionProcessingDurationHistogram { get; }
    public Histogram<double> DatabaseQueryDurationHistogram { get; }
    public Histogram<double> CacheOperationDurationHistogram { get; }

    // Gauges
    public ObservableGauge<long> ActiveTransactionsGauge { get; }
    public ObservableGauge<long> QueueDepthGauge { get; }

    public AiTransactionMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("AiTransactionOrchestrator");

        // Initialize counters
        TransactionCreatedCounter = _meter.CreateCounter<long>(
            "transaction.created",
            description: "Number of transactions created");

        FraudDetectedCounter = _meter.CreateCounter<long>(
            "fraud.detected",
            description: "Number of fraud detections");

        TransactionApprovedCounter = _meter.CreateCounter<long>(
            "transaction.approved",
            description: "Number of transactions approved");

        TransactionRejectedCounter = _meter.CreateCounter<long>(
            "transaction.rejected",
            description: "Number of transactions rejected");

        ExceptionCounter = _meter.CreateCounter<long>(
            "exceptions.total",
            description: "Total number of exceptions");

        // Initialize histograms
        FraudDetectionDurationHistogram = _meter.CreateHistogram<double>(
            "fraud.detection.duration",
            unit: "s",
            description: "Fraud detection processing duration in seconds");

        TransactionProcessingDurationHistogram = _meter.CreateHistogram<double>(
            "transaction.processing.duration",
            unit: "s",
            description: "Transaction processing duration in seconds");

        DatabaseQueryDurationHistogram = _meter.CreateHistogram<double>(
            "database.query.duration",
            unit: "s",
            description: "Database query duration in seconds");

        CacheOperationDurationHistogram = _meter.CreateHistogram<double>(
            "cache.operation.duration",
            unit: "s",
            description: "Cache operation duration in seconds");

        // Initialize observable gauges
        ActiveTransactionsGauge = _meter.CreateObservableGauge(
            "transaction.active",
            () => new Measurement<long>(0), // Will be updated by services
            description: "Number of active transactions");

        QueueDepthGauge = _meter.CreateObservableGauge(
            "queue.depth",
            () => new Measurement<long>(0), // Will be updated by message broker
            description: "Message queue depth");
    }

    public void Dispose() => _meter?.Dispose();
}
