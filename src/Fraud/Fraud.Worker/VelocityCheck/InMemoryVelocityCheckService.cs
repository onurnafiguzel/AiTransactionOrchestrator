using System.Collections.Concurrent;

namespace Fraud.Worker.VelocityCheck;

/// <summary>
/// In-memory velocity check (Production'da Redis ile replace edilebilir)
/// </summary>
public class InMemoryVelocityCheckService : IVelocityCheckService
{
    private readonly ConcurrentBag<RejectedTransaction> _rejectedTransactions = new();
    private readonly object _lockObject = new();
    public Task<int> GetRejectedTransactionCountAsync(string userId, int minutesWindow = 10)
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-minutesWindow);
        
        var count = _rejectedTransactions
            .Where(rt => rt.UserId == userId && rt.CreatedAt > cutoffTime)
            .Count();

        return Task.FromResult(count);
    }

    public Task RecordRejectedTransactionAsync(string userId, decimal amount, string merchant, string country)
    {
        var rejectedTx = new RejectedTransaction
        {
            Id = _rejectedTransactions.Count + 1,
            UserId = userId,
            Amount = amount,
            Merchant = merchant,
            Country = country,
            CreatedAt = DateTime.UtcNow
        };

        _rejectedTransactions.Add(rejectedTx);
        return Task.CompletedTask;
    }

    public Task CleanupOldRecordsAsync(int ageInMinutes = 1440)
    {
        lock (_lockObject)
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-ageInMinutes);
            
            // Yeni bir bag oluştur, eski olmayan kayıtları ekle
            var validRecords = _rejectedTransactions
                .Where(rt => rt.CreatedAt > cutoffTime)
                .ToList();

            // Eski bag'i temizle
            while (_rejectedTransactions.TryTake(out _)) { }

            // Yeni kayıtları ekle
            foreach (var record in validRecords)
            {
                _rejectedTransactions.Add(record);
            }
        }

        return Task.CompletedTask;
    }
}
