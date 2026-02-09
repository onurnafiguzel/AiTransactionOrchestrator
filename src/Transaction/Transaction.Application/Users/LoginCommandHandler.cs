using MediatR;
using Microsoft.Extensions.Logging;
using Transaction.Application.Abstractions;
using Transaction.Domain.Users;

namespace Transaction.Application.Users;

internal sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IUnitOfWork unitOfWork,
    ILogger<LoginCommandHandler> logger) : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // Find user by email
        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Login failed - user not found: {Email}", request.Email);
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        // Verify password
        if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Login failed - invalid password: {Email}", request.Email);
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        // Check if user is active
        if (user.Status != UserStatus.Active || user.IsDeleted)
        {
            logger.LogWarning("Login failed - user is not active: {Email} Status={Status}", request.Email, user.Status);
            throw new UnauthorizedAccessException("Account is not active");
        }

        // Record login
        user.RecordLogin();
        userRepository.Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Generate JWT token
        var token = jwtTokenGenerator.GenerateToken(user.Id, user.Email, user.Role.ToString());

        logger.LogInformation("User logged in successfully | UserId={UserId} Email={Email}", user.Id, user.Email);

        return new LoginResult(
            UserId: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role.ToString(),
            Token: token,
            ExpiresAtUtc: DateTime.UtcNow.AddHours(1));
    }
}
