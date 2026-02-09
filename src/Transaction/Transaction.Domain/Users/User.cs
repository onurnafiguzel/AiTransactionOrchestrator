using Transaction.Domain.Common;
using Transaction.Domain.Users.Events;

namespace Transaction.Domain.Users;

/// <summary>
/// User Aggregate Root - DDD Pattern
/// Manages user authentication, authorization, and lifecycle
/// </summary>
public sealed class User : AggregateRoot
{
    // Identity
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public string FullName { get; private set; } = default!;

    // Authorization
    public UserRole Role { get; private set; }

    // State Management
    public UserStatus Status { get; private set; }
    public bool IsDeleted { get; private set; }

    // Audit
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }
    public DateTime? DeactivatedAtUtc { get; private set; }
    public string? DeactivationReason { get; private set; }

    private User() { } // EF Core

    /// <summary>
    /// Create new user (SignUp)
    /// </summary>
    public static User Create(string email, string passwordHash, string fullName, UserRole role = UserRole.Customer)
    {
        Guard.AgainstNullOrWhiteSpace(email, nameof(email));
        Guard.AgainstNullOrWhiteSpace(passwordHash, nameof(passwordHash));
        Guard.AgainstNullOrWhiteSpace(fullName, nameof(fullName));

        if (!IsValidEmail(email))
            throw new ArgumentException("Invalid email format", nameof(email));

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            FullName = fullName.Trim(),
            Role = role,
            Status = UserStatus.Active,
            IsDeleted = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        user.RaiseDomainEvent(new UserCreatedDomainEvent(user.Id, user.Email));
        return user;
    }

    /// <summary>
    /// Record successful login
    /// </summary>
    public void RecordLogin()
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot login - user is deleted");

        if (Status != UserStatus.Active)
            throw new InvalidOperationException($"Cannot login - user status is {Status}");

        LastLoginAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new UserLoggedInDomainEvent(Id, LastLoginAtUtc.Value));
    }

    /// <summary>
    /// Soft delete user (deactivate)
    /// </summary>
    public void Deactivate(string reason)
    {
        if (IsDeleted)
            throw new InvalidOperationException("User is already deactivated");

        Guard.AgainstNullOrWhiteSpace(reason, nameof(reason));

        IsDeleted = true;
        Status = UserStatus.Inactive;
        DeactivatedAtUtc = DateTime.UtcNow;
        DeactivationReason = reason;
        UpdatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new UserDeactivatedDomainEvent(Id, reason));
    }

    /// <summary>
    /// Update password
    /// </summary>
    public void UpdatePassword(string newPasswordHash)
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot update password - user is deleted");

        Guard.AgainstNullOrWhiteSpace(newPasswordHash, nameof(newPasswordHash));

        PasswordHash = newPasswordHash;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Promote to admin
    /// </summary>
    public void PromoteToAdmin()
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot promote deleted user");

        Role = UserRole.Admin;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
