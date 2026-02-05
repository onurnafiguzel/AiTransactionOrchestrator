namespace Fraud.Worker.VelocityCheck;

public interface IVelocityCheckService
{
    /// <summary>
    /// Son N dakika içinde belirli bir kullanıcının kaç kez red flag aldığını hesapla
    /// </summary>
    Task<int> GetRejectedTransactionCountAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Red flag alan işlemi logla
    /// </summary>
    Task RecordRejectedTransactionAsync(string userId, decimal amount, string merchant, string country, CancellationToken ct = default);

    /// <summary>
    /// Eski kayıtları temizle (cleanup)
    /// </summary>
    Task CleanupOldRecordsAsync(int ageInMinutes = 1440, CancellationToken ct = default);
}
