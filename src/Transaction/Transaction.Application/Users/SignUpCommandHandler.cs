using MediatR;
using Microsoft.Extensions.Logging;
using Transaction.Application.Abstractions;
using Transaction.Domain.Users;

namespace Transaction.Application.Users;

internal sealed class SignUpCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    ILogger<SignUpCommandHandler> logger) : IRequestHandler<SignUpCommand, Guid>
{
    public async Task<Guid> Handle(SignUpCommand request, CancellationToken cancellationToken)
    {
        // Check if email already exists
        var existingUser = await userRepository.ExistsByEmailAsync(request.Email, cancellationToken);
        if (existingUser)
        {
            logger.LogWarning("SignUp failed - email already exists: {Email}", request.Email);
            throw new InvalidOperationException($"Email {request.Email} is already registered");
        }

        // Hash password
        var passwordHash = passwordHasher.HashPassword(request.Password);

        // Create user
        var user = User.Create(
            email: request.Email,
            passwordHash: passwordHash,
            fullName: request.FullName,
            role: UserRole.Customer);

        await userRepository.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User created successfully | UserId={UserId} Email={Email}",
            user.Id,
            user.Email);

        return user.Id;
    }
}
