using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Text.Json;
using Transaction.Domain.Audit;
using Transaction.Infrastructure.Observability;
using TransactionEntity = Transaction.Domain.Transactions.Transaction;

namespace Transaction.Infrastructure.Persistence.Interceptors;

/// <summary>
/// SaveChangesInterceptor kullanarak veritabanında gerçekleşen tüm değişiklikleri otomatik olarak audit log'a kaydeder
/// Insert, Update ve Delete işlemlerini yakalar ve detaylı bilgileri saklar
/// </summary>
public sealed class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string? _defaultSource;

    // Audit log kayıtlarının sonsuz döngüye girmesini önlemek için bu flag'i kullanıyoruz
    private static readonly AsyncLocal<bool> IsAuditingInProgress = new();

    public AuditInterceptor(IHttpContextAccessor httpContextAccessor, string? defaultSource = "UnknownSource")
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _defaultSource = defaultSource;
    }

    /// <summary>
    /// SaveChangesAsync çağrılmadan ÖNCESİ tetiklenir
    /// Bu interceptor'da SavingChangesAsync override ediyoruz çünkü bu noktada
    /// değişikliklere henüz erişebiliriz (SavedChangesAsync çok geçtir)
    /// </summary>
    /// <param name="eventData">SaveChanges event bilgilerini içerir</param>
    /// <param name="result">SaveChanges'in varsayılan sonucu</param>
    /// <param name="cancellationToken">İşlemi iptal etmek için kullanılan token</param>
    /// <returns>Değiştirilmiş sonuç vb.</returns>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {       
        // Audit ediliyorsa, tekrar audit etme (sonsuz döngü problemini çöz)
        if (IsAuditingInProgress.Value)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        var context = eventData.Context;
        if (context == null)
        {            
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        try
        {
            // Audit işlemini başlatıyoruz
            IsAuditingInProgress.Value = true;

            // Değişiklikleri yakalamak (Save ÖNCESİ state'i kapat)
            var auditLogs = CaptureChanges(context);

            // Eğer herhangi bir değişiklik yoksa, hemen dön
            if (auditLogs.Count == 0)
            {
                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }

            // Async SaveAuditLogs işlemini senkron çalıştır (SavedChanges hata fırlatabakıyor)
            SaveAuditLogsSynchronously(context, auditLogs);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AUDIT] Audit etme başarısız: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            // Audit işlemini sonlandırıyoruz
            IsAuditingInProgress.Value = false;
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// ChangeTracker kullanarak tüm değişen entity'leri yakalar
    /// Her entity için Insert, Update veya Delete işlemine göre audit kaydı oluşturur
    /// </summary>
    /// <param name="context">DbContext instance'ı</param>
    /// <returns>Oluşturulan AuditLog listesi</returns>
    private List<AuditLog> CaptureChanges(DbContext context)
    {
        var auditLogs = new List<AuditLog>();
       
        var source = GetEventOrApiName();

        var entries = context.ChangeTracker.Entries().ToList();

        foreach (var entry in entries)
        {
            if (entry.Entity is AuditLog)
            {
                continue;
            }
           
            if (!(entry.Entity is TransactionEntity))
            {
                continue;
            }

            if (entry.State == EntityState.Unchanged || entry.State == EntityState.Detached)
            {
                continue;
            }
           
            AuditLog? auditLog = entry.State switch
            {
                EntityState.Added => CreateAuditLogForInsert(entry, source),
                EntityState.Modified => CreateAuditLogForUpdate(entry, source),
                EntityState.Deleted => CreateAuditLogForDelete(entry, source),
                _ => null
            };

            if (auditLog != null)
            {
                auditLogs.Add(auditLog);
            }
        }

        return auditLogs;
    }

    /// <summary>
    /// INSERT işlemi için tek bir audit log kaydı oluşturur
    /// Yeni eklenen TÜM property'ler NewValues JSON object'inde saklanır
    /// </summary>
    private AuditLog? CreateAuditLogForInsert(
        EntityEntry entry,
        string source)
    {
        // Sadece transaction entity'sini track ettiğimiz için tableName sabit
        const string tableName = "transaction";
        var userId = GetCurrentUserId();
        var now = DateTime.UtcNow;
        var transactionId = ((TransactionEntity)entry.Entity).Id;

        // Tüm değişen property'leri JSON formatında topla
        var newValues = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            // Navigation properties ve computed columns'ı atla
            if (IsNavigationProperty(property) || property.Metadata.ValueGenerated != ValueGenerated.Never)
            {
                continue;
            }

            var value = property.CurrentValue;
            newValues[property.Metadata.Name] = value;
        }

        // Eğer değişen property yoksa null döndür
        if (newValues.Count == 0)
        {
            return null;
        }

        return new AuditLog
        {
            Id = Guid.NewGuid(),
            TableName = tableName,
            Action = "Insert",
            OldValues = null, // INSERT'te eski değer yoktur
            NewValues = JsonSerializer.Serialize(newValues),
            ChangedAt = now,
            TransactionId = transactionId,
            EventOrApiName = source,
            UserId = userId,
            EntitySnapshot = SerializeValue(entry.Entity) // Tüm entity'nin snapshot'ı
        };
    }

    /// <summary>
    /// UPDATE işlemi için tek bir audit log kaydı oluşturur
    /// Değişen property'ler OldValues ve NewValues JSON object'lerinde saklanır
    /// Değişmeyen property'ler audit log'a dahil edilmez
    /// </summary>
    private AuditLog? CreateAuditLogForUpdate(
        EntityEntry entry,
        string source)
    {
        // Sadece transaction entity'sini track ettiğimiz için tableName sabit
        const string tableName = "transaction";
        var userId = GetCurrentUserId();
        var now = DateTime.UtcNow;
        var transactionId = ((TransactionEntity)entry.Entity).Id;

        // Değişen property'leri topla
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            // Navigation properties ve computed columns'ı atla
            if (IsNavigationProperty(property) || property.Metadata.ValueGenerated != ValueGenerated.Never)
            {
                continue;
            }

            var oldValue = property.OriginalValue;
            var newValue = property.CurrentValue;

            // Sadece değişen property'leri kaydet
            if (!Equals(oldValue, newValue))
            {
                oldValues[property.Metadata.Name] = oldValue;
                newValues[property.Metadata.Name] = newValue;
            }
        }

        // Eğer değişen property yoksa null döndür
        if (oldValues.Count == 0 || newValues.Count == 0)
        {
            return null;
        }

        return new AuditLog
        {
            Id = Guid.NewGuid(),
            TableName = tableName,
            Action = "Update",
            OldValues = JsonSerializer.Serialize(oldValues),
            NewValues = JsonSerializer.Serialize(newValues),
            ChangedAt = now,
            TransactionId = transactionId,
            EventOrApiName = source,
            UserId = userId,
            EntitySnapshot = SerializeValue(entry.Entity) // Tüm entity'nin yeni durumu
        };
    }

    /// <summary>
    /// DELETE işlemi için tek bir audit log kaydı oluşturur
    /// Tüm property'ler OldValues JSON object'inde saklanır
    /// </summary>
    private AuditLog? CreateAuditLogForDelete(
        EntityEntry entry,
        string source)
    {
        // Sadece transaction entity'sini track ettiğimiz için tableName sabit
        const string tableName = "transaction";
        var userId = GetCurrentUserId();
        var now = DateTime.UtcNow;
        var transactionId = ((TransactionEntity)entry.Entity).Id;

        // Silinen entity'nin tüm property'lerini topla
        var oldValues = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            // Navigation properties ve computed columns'ı atla
            if (IsNavigationProperty(property) || property.Metadata.ValueGenerated != ValueGenerated.Never)
            {
                continue;
            }

            var value = property.OriginalValue;
            oldValues[property.Metadata.Name] = value;
        }

        // Eğer property yoksa null döndür
        if (oldValues.Count == 0)
        {
            return null;
        }

        return new AuditLog
        {
            Id = Guid.NewGuid(),
            TableName = tableName,
            Action = "Delete",
            OldValues = JsonSerializer.Serialize(oldValues),
            NewValues = null, // DELETE'te yeni değer yoktur
            ChangedAt = now,
            TransactionId = transactionId,
            EventOrApiName = source,
            UserId = userId,
            EntitySnapshot = SerializeValue(entry.Entity) // Silinen entity'nin tüm durumu
        };
    }

    /// <summary>
    /// Bir property'nin navigation property olup olmadığını kontrol eder
    /// </summary>
    private bool IsNavigationProperty(PropertyEntry property)
    {
        try
        {
            // Foreign key ve navigation check
            return property.Metadata.IsForeignKey() || 
                   (property.Metadata.PropertyInfo?.PropertyType.IsClass ?? false && 
                    property.Metadata.PropertyInfo?.PropertyType != typeof(string));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Bir değeri string formatına dönüştürür
    /// Complex types JSON formatında serialize edilir
    /// </summary>
    private string? SerializeValue(object? value)
    {
        if (value == null)
        {
            return null;
        }

        // Primitive types için ToString kullan
        if (value is string stringValue)
        {
            return stringValue;
        }

        if (value is int or long or double or decimal or bool or DateTime or Guid)
        {
            return value.ToString();
        }

        // Complex types'lar için JSON serialize et
        try
        {
            return JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            return value.ToString();
        }
    }

    /// <summary>
    /// HTTP context'ten API endpoint veya event adını alır
    /// HttpContext yoksa default value döner
    /// </summary>
    private string GetEventOrApiName()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                var method = httpContext.Request.Method;
                var path = httpContext.Request.Path.Value;
                return $"{method} {path}";
            }
        }
        catch
        {
            // Logger eklensen burada log yapılabilir
        }

        var ambientSource = AmbientContext.AuditSource.Current;
        if (!string.IsNullOrWhiteSpace(ambientSource))
        {
            return ambientSource;
        }

        return _defaultSource ?? "Unknown";
    }

    /// <summary>
    /// HttpContext'ten current user ID'sini alır
    /// User claims'den "sub" (subject) claim'ini arar
    /// </summary>
    private string? GetCurrentUserId()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User != null)
            {
                var userId = httpContext.User.FindFirst("sub")?.Value ??
                             httpContext.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value ??
                             httpContext.User.Identity?.Name;

                if (!string.IsNullOrWhiteSpace(userId))
                {
                    return userId;
                }
            }
        }
        catch
        {
            // Logger eklensen burada log yapılabilir
        }

        return AmbientContext.AuditUser.Current;
    }

    /// <summary>
    /// Oluşturulan AuditLog kayıtlarını veritabanına senkron kaydeder
    /// SavingChangesAsync içinde çağrılır, bu yüzden senkron versiyonu gerekli
    /// </summary>
    private void SaveAuditLogsSynchronously(
        DbContext context,
        List<AuditLog> auditLogs)
    {
        if (auditLogs.Count == 0)
        {
            return;
        }

        try
        {
            // Audit log kayıtlarını DbSet'e ekle
            foreach (var auditLog in auditLogs)
            {
                context.Set<AuditLog>().Add(auditLog);
            }

            // Veritabanına kaydet (bu SaveChanges öncesi çağrıldığı için sorun olmaz)
            // IsAuditingInProgress flag'i sayesinde bu kayıtların kendileri audit edilmeyecek
        }
        catch (Exception ex)
        {
            // Audit log'un başarısızlığı ana işlemi engellememeli
            System.Diagnostics.Debug.WriteLine($"Audit log kaydı başarısız: {ex.Message}");
        }
    }
}
