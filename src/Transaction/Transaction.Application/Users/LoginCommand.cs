using MediatR;

namespace Transaction.Application.Users;

/// <summary>
/// Login Command - Authenticates user and returns JWT token
/// </summary>
public sealed record LoginCommand(
    string Email,
    string Password
) : IRequest<LoginResult>;

public sealed record LoginResult(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string Token,
    DateTime ExpiresAtUtc
);
