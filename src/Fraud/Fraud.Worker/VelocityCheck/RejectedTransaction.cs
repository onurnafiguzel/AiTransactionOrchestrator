namespace Fraud.Worker.VelocityCheck;

/// <summary>
/// Başarısız/Red flag alan işlemleri takip et
/// </summary>
public class RejectedTransaction
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Merchant { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
