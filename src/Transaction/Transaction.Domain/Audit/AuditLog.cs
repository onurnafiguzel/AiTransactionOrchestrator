namespace Transaction.Domain.Audit;

/// <summary>
/// Veritabanında gerçekleşen tüm değişiklikleri (Insert, Update, Delete) kaydeder
/// Compliance gereksinimlerini karşılamak ve hata ayıklama süreçlerini kolaylaştırmak için kullanılır
/// 
/// Her bir API isteği/işlem için TÜM değişen property'ler tek bir kayıtta saklanır
/// Örnek: Transaction.Status ve Transaction.Amount değişirse, iki ayrı kayıt yazılmaz, 
/// bir kayıtta her iki property'in eski ve yeni değerleri JSON formatında saklanır
/// </summary>
public sealed class AuditLog
{
    /// <summary>
    /// Primary Key - Audit kaydının benzersiz tanımlayıcısı
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Etkilenen tablonun adı (örn: "transaction")
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// İşlem tipi: "Insert", "Update" veya "Delete"
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Tüm değişen property'lerin eski değerleri
    /// JSON object formatında saklanır
    /// Örn: {"Status": "Pending", "Amount": 1000}
    /// INSERT işlemleri için null
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// Tüm değişen property'lerin yeni değerleri
    /// JSON object formatında saklanır
    /// Örn: {"Status": "Processing", "Amount": 1500}
    /// DELETE işlemleri için null
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// Değişikliğin gerçekleştiği tarih ve saat
    /// </summary>
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Transaction ID - İşlemlerin gruplandırılması ve ilişkilerinin bulunması için kullanılır
    /// Aynı transaction içindeki TÜM değişikliklerin aynı ID'ye sahip olması sağlanır
    /// </summary>
    public Guid TransactionId { get; set; }

    /// <summary>
    /// İşlemin nereden tetiklendiğini belirtir
    /// - API Endpoint: "POST /api/transactions"
    /// - Event Adı: "TransactionCreatedEvent"
    /// - Background Job: "ProcessTransactionJob"
    /// - Scheduled Task: "DailyReconciliationTask"
    /// </summary>
    public string EventOrApiName { get; set; } = string.Empty;

    /// <summary>
    /// İşlemi gerçekleştiren kullanıcının ID'si (varsa)
    /// Anonymous işlemler için null olabilir
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Değişikliğe sebep olan entity'nin tüm mevcut durumu (isteğe bağlı)
    /// Debugging ve compliance için entity'nin tamamı saklanabilir
    /// JSON formatında saklanır
    /// </summary>
    public string? EntitySnapshot { get; set; }

}
