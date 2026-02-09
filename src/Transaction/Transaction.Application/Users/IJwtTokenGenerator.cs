namespace Transaction.Application.Users;

/// <summary>
/// JWT Token Generator Service Interface
/// </summary>
public interface IJwtTokenGenerator
{
    string GenerateToken(Guid userId, string email, string role);
}
