using MediatR;

namespace Transaction.Application.Users;

/// <summary>
/// SignUp Command - Creates new user account
/// </summary>
public sealed record SignUpCommand(
    string Email,
    string Password,
    string FullName
) : IRequest<Guid>;
