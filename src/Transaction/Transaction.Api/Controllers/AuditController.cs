using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Transaction.Application.Abstractions;
using Transaction.Infrastructure.Persistence.Audit;

namespace Transaction.Api.Controllers;

/// <summary>
/// Audit API Controller - Audit log verilerini sorgulamak ve raporlama yapmak için
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public sealed class AuditController(
    AuditLogQueryHelper auditQueryHelper,
    IUnitOfWork unitOfWork) : ControllerBase
{
    private readonly AuditLogQueryHelper _auditQueryHelper = auditQueryHelper;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    /// <summary>
    /// Belirli bir transaction'ın tüm değişiklik geçmişini getirir
    /// Örn: GET /api/audit/transactions/550e8400-e29b-41d4-a716-446655440000/history
    /// </summary>
    /// <param name="transactionId">Transaction ID'si</param>
    /// <returns>Tarih sırasına göre sıralanmış audit log listesi</returns>
    [HttpGet("{transactionId}/history")]
    public async Task<ActionResult> GetRecordAuditTrail(
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var auditTrail = await _auditQueryHelper.GetRecordAuditTrailAsync(
            transactionId,
            cancellationToken);

        if (auditTrail.Count == 0)
        {
            return NotFound("Bu transaction için audit kaydı bulunamadı");
        }

        return Ok(new
        {
            transactionId,
            totalChanges = auditTrail.Count,
            // Artık her audit log kaydında OldValues ve NewValues JSON formatında tüm değişiklikleri içeriyor
            changes = auditTrail.Select(a => new
            {
                a.Id,
                a.Action,
                oldValues = JsonSerializer.Deserialize<Dictionary<string, object?>>(a.OldValues ?? "{}"),
                newValues = JsonSerializer.Deserialize<Dictionary<string, object?>>(a.NewValues ?? "{}"),
                a.ChangedAt,
                a.EventOrApiName,
                a.UserId,
                a.TransactionId
            }).ToList()
        });
    }

    /// <summary>
    /// Belirli bir kullanıcının yaptığı son işlemleri getirir
    /// GET /api/audit/users/user123/activity
    /// </summary>
    /// <param name="userId">Kullanıcı ID'si</param>
    /// <param name="limit">Maksimum kaç kaydı döndüreceği</param>
    [HttpGet("users/{userId}/activity")]
    public async Task<ActionResult> GetUserActivity(
        string userId,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (limit > 1000)
            limit = 1000;

        var userActivity = await _auditQueryHelper.GetUserActivityAsync(
            userId,
            limit,
            cancellationToken);

        if (userActivity.Count == 0)
        {
            return NotFound("Bu kullanıcı için audit kaydı bulunamadı");
        }

        return Ok(new
        {
            userId,
            activityCount = userActivity.Count,
            activities = userActivity.Select(a => new
            {
                a.TableName,
                a.Action,
                oldValues = JsonSerializer.Deserialize<Dictionary<string, object?>>(a.OldValues ?? "{}"),
                newValues = JsonSerializer.Deserialize<Dictionary<string, object?>>(a.NewValues ?? "{}"),
                a.ChangedAt,
                a.EventOrApiName,
                a.TransactionId
            }).ToList()
        });
    }

    /// <summary>
    /// Belirli bir transaction'ın tüm değişikliklerini getirir
    /// Aynı SaveChanges çağrısında yapılan tüm işlemleri bir arada gösterir
    /// GET /api/audit/transaction/550e8400-e29b-41d4-a716-446655440000
    /// </summary>
    /// <param name="transactionId">Transaction ID'si (Guid)</param>
    [HttpGet("transaction/{transactionId:guid}")]
    public async Task<ActionResult> GetTransactionChanges(
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var changes = await _auditQueryHelper.GetTransactionChangesByTransactionIdAsync(
            transactionId,
            cancellationToken);

        if (changes.Count == 0)
        {
            return NotFound("Bu transaction için audit kaydı bulunamadı");
        }

        // Transaction'daki değişiklikleri tablo bazında grupla
        var groupedByTable = changes
            .GroupBy(c => c.TableName)
            .Select(g => new
            {
                tableName = g.Key,
                changeCount = g.Count(),
                changes = g.Select(c => new
                {
                    c.Action,
                    oldValues = JsonSerializer.Deserialize<Dictionary<string, object?>>(c.OldValues ?? "{}"),
                    newValues = JsonSerializer.Deserialize<Dictionary<string, object?>>(c.NewValues ?? "{}"),
                    c.ChangedAt
                }).ToList()
            }).ToList();

        return Ok(new
        {
            transactionId,
            timestamp = changes.First().ChangedAt,
            totalChanges = changes.Count,
            changedTables = groupedByTable.Count,
            changes = groupedByTable
        });
    }

    /// <summary>
    /// Belirli bir tarih aralığında transaction tablosunda yapılan tüm değişiklikleri getirir
    /// GET /api/audit/changes?startDate=2026-01-01&endDate=2026-02-22
    /// </summary>
    /// <param name="startDate">Başlangıç tarihi (yyyy-MM-dd)</param>
    /// <param name="endDate">Bitiş tarihi (yyyy-MM-dd)</param>
    [HttpGet("changes")]
    public async Task<ActionResult> GetTableChangesInDateRange(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken)
    {
        // Sadece transaction tablosu için
        const string tableName = "transaction";

        if (startDate > endDate)
        {
            return BadRequest("Başlangıç tarihi bitiş tarihinden sonra olamaz");
        }

        var changes = await _auditQueryHelper.GetTableChangesInDateRangeAsync(
            tableName,
            startDate,
            endDate,
            cancellationToken);

        // İşlem tipi bazında istatistikler
        var statistics = changes
            .GroupBy(c => c.Action)
            .Select(g => new { action = g.Key, count = g.Count() })
            .ToList();

        return Ok(new
        {
            tableName,
            dateRange = new { startDate = startDate.Date, endDate = endDate.Date },
            totalChanges = changes.Count,
            statistics,
            changes = changes.Select(c => new
            {
                c.Action,
                oldValues = JsonSerializer.Deserialize<Dictionary<string, object?>>(c.OldValues ?? "{}"),
                newValues = JsonSerializer.Deserialize<Dictionary<string, object?>>(c.NewValues ?? "{}"),
                c.ChangedAt,
                c.UserId,
                c.TransactionId
            }).ToList()
        });
    }

    /// <summary>
    /// <summary>
    /// Silinen bir transaction'ın tüm verilerini geri yüklemek için gereken bilgileri getirir
    /// DELETE işleminden sonra veri restore etmek için kullanılır
    /// GET /api/audit/transactions/550e8400-e29b-41d4-a716-446655440000/deleted-data
    /// </summary>
    /// <param name="transactionId">Transaction ID'si</param>
    [HttpGet("transactions/{transactionId:guid}/deleted-data")]
    public async Task<ActionResult> GetDeletedRecordOriginalValues(
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var deletedData = await _auditQueryHelper.GetDeletedRecordOriginalValuesAsync(
            transactionId,
            cancellationToken);

        if (deletedData.Count == 0)
        {
            return NotFound("Bu transaction için DELETE işlemi bulunamadı");
        }

        // Restore etmek için gereken veri - OldValues JSON'dan parse et
        var restoredData = deletedData
            .Select(d => new
            {
                originalData = JsonSerializer.Deserialize<Dictionary<string, object?>>(d.OldValues ?? "{}"),
                deletedAt = d.ChangedAt,
                deletedBy = d.UserId,
                source = d.EventOrApiName
            })
            .ToList();

        return Ok(new
        {
            transactionId,
            restorable = restoredData.Count,
            data = restoredData
        });
    }

    /// <summary>
    /// Belirli bir transaction'da belirli bir sütunun değişiklik geçmişini timeline olarak getirir
    /// Belirli bir field'ın nasıl evrildiğini görüntülemek için
    /// GET /api/audit/transactions/550e8400-e29b-41d4-a716-446655440000/columns/Status/history
    /// </summary>
    /// <param name="transactionId">Transaction ID'si</param>
    /// <param name="columnName">Sütun adı</param>
    [HttpGet("transactions/{transactionId:guid}/columns/{columnName}/history")]
    public async Task<ActionResult> GetColumnUpdateHistory(
        Guid transactionId,
        string columnName,
        CancellationToken cancellationToken)
    {
        var history = await _auditQueryHelper.GetColumnUpdateHistoryAsync(
            transactionId,
            columnName,
            cancellationToken);

        if (history.Count == 0)
        {
            return NotFound("Bu sütun için değişiklik kaydı bulunamadı");
        }

        // Timeline format'ında düzenle - konsolide kayıtlardan ilgili property çıkar
        var timeline = history
            .Select((h, index) => new
            {
                step = index + 1,
                oldValue = GetPropertyFromJson(h.OldValues, columnName),
                newValue = GetPropertyFromJson(h.NewValues, columnName),
                h.ChangedAt,
                h.UserId,
                h.EventOrApiName
            })
            .ToList();

        return Ok(new
        {
            transactionId,
            columnName,
            updateCount = timeline.Count,
            timeline
        });
    }

    /// <summary>
    /// JSON string'den belirli bir property değerini çıkarır
    /// </summary>
    private object? GetPropertyFromJson(string? jsonString, string propertyName)
    {
        if (string.IsNullOrEmpty(jsonString))
            return null;

        var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonString);
        return data?.TryGetValue(propertyName, out var value) == true ? value : null;
    }

    /// <summary>
    /// Genel audit istatistikleri
    /// Sistem genelinde yapılan tüm değişikliklerin özetini gösterir
    /// GET /api/audit/statistics
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult> GetAuditStatistics(CancellationToken cancellationToken)
    {
        var stats = await _auditQueryHelper.GetAuditStatisticsAsync(cancellationToken);

        return Ok(new
        {
            totalAuditRecords = stats.TotalChanges,
            uniqueTablesAffected = stats.UniqueTableCount,
            uniqueUsersInvolved = stats.UniqueUserCount,
            changesByAction = stats.ChangesByAction,
            topChangedTables = stats.TopChangedTables,
            topUsers = stats.TopUsers,
            auditLogDateRange = new
            {
                oldest = stats.OldestAuditLog,
                newest = stats.NewestAuditLog
            }
        });
    }

    /// <summary>
    /// Compliance raporu oluştur - GDPR, SOX, vb. compliance ihtiyaçları için
    /// POST /api/audit/reports/compliance
    /// </summary>
    [HttpPost("reports/compliance")]
    public async Task<ActionResult> GenerateComplianceReport(
        [FromBody] ComplianceReportRequest request,
        CancellationToken cancellationToken)
    {
        // Sadece transaction tablosu için
        const string tableName = "transaction";

        // Tarih validasyonu
        if (request.StartDate > request.EndDate)
        {
            return BadRequest("Başlangıç tarihi bitiş tarihinden sonra olamaz");
        }

        var changes = await _auditQueryHelper.GetTableChangesInDateRangeAsync(
            tableName,
            request.StartDate,
            request.EndDate,
            cancellationToken);

        // Compliance report'u oluştur
        var report = new
        {
            reportId = Guid.NewGuid(),
            generatedAt = DateTime.UtcNow,
            reportPeriod = new { request.StartDate, request.EndDate },
            tableName = tableName,
            totalTransactions = changes.Count,
            changesBreakdown = changes
                .GroupBy(c => c.Action)
                .Select(g => new { action = g.Key, count = g.Count() })
                .ToList(),
            affectedTransactions = changes
                .Select(c => c.TransactionId)
                .Distinct()
                .Count(),
            affectedUsers = changes
                .Where(c => c.UserId != null)
                .Select(c => c.UserId)
                .Distinct()
                .Count(),
            details = request.IncludeDetails ? changes
                .Select(c => new
                {
                    c.TransactionId,
                    c.Action,
                    oldValues = JsonSerializer.Deserialize<Dictionary<string, object?>>(c.OldValues ?? "{}"),
                    newValues = JsonSerializer.Deserialize<Dictionary<string, object?>>(c.NewValues ?? "{}"),
                    c.ChangedAt,
                    c.UserId,
                    c.EventOrApiName
                })
                .ToList()
                : null
        };

        return Ok(report);
    }
}

/// <summary>
/// Compliance raporu isteği
/// Transaction tablosu için compliance raporu oluşturmak için kullanılır
/// </summary>
public sealed class ComplianceReportRequest
{
    /// <summary>
    /// Rapor periyodunun başlangıcı
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Rapor periyodunun bitişi
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Detaylı bilgileri ekle (tüm değişiklikleri listelemek için)
    /// </summary>
    public bool IncludeDetails { get; set; } = false;
}
