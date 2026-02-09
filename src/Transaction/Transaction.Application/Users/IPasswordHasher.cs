namespace Transaction.Application.Users;

/// <summary>
/// Password Hasher Service Interface
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}
