using MediatR;

namespace Transaction.Application.Users;

/// <summary>
/// Deactivate User Command - Soft deletes user account
/// </summary>
public sealed record DeactivateUserCommand(
    Guid UserId,
    string Reason
) : IRequest;
