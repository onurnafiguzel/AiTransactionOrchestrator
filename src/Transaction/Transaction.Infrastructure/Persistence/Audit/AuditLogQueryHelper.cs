using Microsoft.EntityFrameworkCore;
using Transaction.Domain.Audit;

namespace Transaction.Infrastructure.Persistence.Audit;

/// <summary>
/// AuditLog tablosundan sorgulama yapmak için helper classW
/// Yaygın audit log sorgulamalarını kolaylaştırır
/// </summary>
public sealed class AuditLogQueryHelper
{
    private readonly TransactionDbContext _context;

    public AuditLogQueryHelper(TransactionDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Belirli bir transaction'ın tüm değişikliklerini timeline formatında getirir
    /// </summary>
    /// <param name="transactionId">Transaction ID'si</param>
    /// <returns>Tarih sırasına göre sıralanmış audit log listesi</returns>
    public async Task<List<AuditLog>> GetRecordAuditTrailAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .Where(a => a.TransactionId == transactionId)
            .OrderBy(a => a.ChangedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Belirli bir kullanıcının yaptığı tüm işlemleri getirir
    /// </summary>
    /// <param name="userId">Kullanıcı ID'si</param>
    /// <param name="limit">Maksimum sonuç sayısı</param>
    /// <returns>Son işlemlerden başlayarak sıralanmış audit log listesi</returns>
    public async Task<List<AuditLog>> GetUserActivityAsync(
        string userId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.ChangedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Belirli bir transaction'ın tüm değişikliklerini getirir
    /// Aynı SaveChanges çağrısında yapılan tüm işlemleri gruplar
    /// </summary>
    /// <param name="transactionId">Transaction ID'si</param>
    /// <returns>Transaction içindeki tüm audit log kayıtları</returns>
    public async Task<List<AuditLog>> GetTransactionChangesByTransactionIdAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .Where(a => a.TransactionId == transactionId)
            .OrderBy(a => a.TableName)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Belirli bir tablo için belirli bir tarih aralığında yapılan değişiklikleri getirir
    /// </summary>
    /// <param name="tableName">Tablo adı</param>
    /// <param name="startDate">Başlangıç tarihi</param>
    /// <param name="endDate">Bitiş tarihi</param>
    /// <returns>Tarih aralığında yapılan tüm değişiklikler</returns>
    public async Task<List<AuditLog>> GetTableChangesInDateRangeAsync(
        string tableName,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .Where(a => a.TableName == tableName)
            .Where(a => a.ChangedAt >= startDate && a.ChangedAt <= endDate)
            .OrderBy(a => a.ChangedAt)
            .ThenBy(a => a.Action)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Belirli bir sütunun tüm değişikliklerini getirir (konsolide audit kayıtlarından)
    /// Belirli bir sütunun değişiklik geçmişini bulmak için gereken kayıtları filtreler
    /// </summary>
    /// <param name="tableName">Tablo adı</param>
    /// <param name="columnName">Sütun adı</param>
    /// <returns>Belirtilen sütunun değişikliğini içeren tüm audit log kayıtları</returns>
    public async Task<List<AuditLog>> GetColumnChangesAsync(
        string tableName,
        string columnName,
        CancellationToken cancellationToken = default)
    {
        // Konsolide audit kayıtlarında ColumnName yoktur
        // OldValues/NewValues JSON de belirtilen column'ı içeren kayıtları getir
        return await _context.AuditLogs
            .Where(a => a.TableName == tableName 
                && (a.OldValues != null && a.OldValues.Contains($"\"{columnName}\"") 
                    || a.NewValues != null && a.NewValues.Contains($"\"{columnName}\"")))
            .OrderByDescending(a => a.ChangedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// DELETE işlemi sonrası silinen bir transaction'ın tüm verilerini restore bilgileri ile getirir
    /// </summary>
    /// <param name="transactionId">Transaction ID'si</param>
    /// <returns>Restore için gerekli tüm eski değerler (OldValues JSON'dan)</returns>
    public async Task<List<AuditLog>> GetDeletedRecordOriginalValuesAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .Where(a => a.TransactionId == transactionId
                && a.Action == "Delete")
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Belirli bir transaction'daki belirli bir sütunun değişiklik geçmişini timeline format'ında getirir
    /// Konsolide audit kayıtlarından ilgili column'ı içeren kayıtları filtreler
    /// </summary>
    /// <param name="transactionId">Transaction ID'si</param>
    /// <param name="columnName">Sütun adı</param>
    /// <returns>Sütunun her güncelleme adımını içeren audit kayıtları</returns>
    public async Task<List<AuditLog>> GetColumnUpdateHistoryAsync(
        Guid transactionId,
        string columnName,
        CancellationToken cancellationToken = default)
    {
        // Konsolide audit kayıtlarında ColumnName yoktur
        // OldValues/NewValues JSON'da belirtilen column'ı içeren Update kayıtlarını getir
        return await _context.AuditLogs
            .Where(a => a.TransactionId == transactionId
                && (a.OldValues != null && a.OldValues.Contains($"\"{columnName}\"") 
                    || a.NewValues != null && a.NewValues.Contains($"\"{columnName}\"")))
            .OrderBy(a => a.ChangedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Belirli bir API endpoint'inde yapılan tüm değişiklikleri getirir
    /// </summary>
    /// <param name="apiEndpoint">API endpoint'i (örn: "POST /api/transactions")</param>
    /// <param name="since">Son kaç gündeki işlemler</param>
    /// <returns>Endpoint'ten triggerenen tüm audit log kayıtları</returns>
    public async Task<List<AuditLog>> GetAuditsByApiEndpointAsync(
        string apiEndpoint,
        int sinceLastDays = 7,
        CancellationToken cancellationToken = default)
    {
        var startDate = DateTime.UtcNow.AddDays(-sinceLastDays);

        return await _context.AuditLogs
            .Where(a => a.EventOrApiName.Contains(apiEndpoint))
            .Where(a => a.ChangedAt >= startDate)
            .OrderByDescending(a => a.ChangedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Tüm INSERT işlemleri için audit log kayıtlarını getirir
    /// Yeni eklenen kayıtların ne olduğunu görüntülemek için kullanılır
    /// </summary>
    /// <param name="tableName">Tablo adı</param>
    /// <param name="sinceLastDays">Son kaç gündeki işlemler</param>
    public async Task<List<AuditLog>> GetInsertedRecordsAsync(
        string tableName,
        int sinceLastDays = 7,
        CancellationToken cancellationToken = default)
    {
        var startDate = DateTime.UtcNow.AddDays(-sinceLastDays);

        return await _context.AuditLogs
            .Where(a => a.TableName == tableName && a.Action == "Insert")
            .Where(a => a.ChangedAt >= startDate)
            .OrderByDescending(a => a.ChangedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// İstatistikleri raporlama amacıyla aggregated data getirir
    /// </summary>
    /// <returns>Audit log istatistikleri</returns>
    public async Task<AuditStatistics> GetAuditStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        var allLogs = await _context.AuditLogs.ToListAsync(cancellationToken);

        var tables = allLogs.Select(a => a.TableName).Distinct().Count();
        var users = allLogs.Where(a => a.UserId != null).Select(a => a.UserId).Distinct().Count();

        var changesByAction = allLogs
            .GroupBy(a => a.Action)
            .ToDictionary(g => g.Key, g => g.Count());

        var changesByTable = allLogs
            .GroupBy(a => a.TableName)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .ToDictionary(g => g.Key, g => g.Count());

        var changesByUser = allLogs
            .Where(a => a.UserId != null)
            .GroupBy(a => a.UserId!)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => new { Key = g.Key ?? "Unknown", Count = g.Count() })
            .ToDictionary(x => x.Key, x => x.Count);

        return new AuditStatistics
        {
            TotalChanges = allLogs.Count,
            UniqueTableCount = tables,
            UniqueUserCount = users,
            ChangesByAction = changesByAction,
            TopChangedTables = changesByTable,
            TopUsers = changesByUser,
            OldestAuditLog = allLogs.Min(a => a.ChangedAt),
            NewestAuditLog = allLogs.Max(a => a.ChangedAt)
        };
    }
}

/// <summary>
/// Audit log istatistikleri DTO
/// </summary>
public sealed class AuditStatistics
{
    /// <summary>
    /// Toplam değişiklik sayısı
    /// </summary>
    public int TotalChanges { get; set; }

    /// <summary>
    /// Kaç farklı tabloda değişiklik yapılmış
    /// </summary>
    public int UniqueTableCount { get; set; }

    /// <summary>
    /// Kaç farklı kullanıcı değişiklik yapmış
    /// </summary>
    public int UniqueUserCount { get; set; }

    /// <summary>
    /// İşlem tipi bazında değişiklik sayıları
    /// Key: "Insert", "Update", "Delete"
    /// Value: Yazılışı sayı
    /// </summary>
    public Dictionary<string, int> ChangesByAction { get; set; } = new();

    /// <summary>
    /// En çok değişen tablolar (top 10)
    /// </summary>
    public Dictionary<string, int> TopChangedTables { get; set; } = new();

    /// <summary>
    /// En aktif kullanıcılar (top 10)
    /// </summary>
    public Dictionary<string, int> TopUsers { get; set; } = new();

    /// <summary>
    /// En eski audit log kaydının tarihi
    /// </summary>
    public DateTime OldestAuditLog { get; set; }

    /// <summary>
    /// En yeni audit log kaydının tarihi
    /// </summary>
    public DateTime NewestAuditLog { get; set; }
}
