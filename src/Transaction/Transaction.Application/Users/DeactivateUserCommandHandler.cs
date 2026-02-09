using MediatR;
using Microsoft.Extensions.Logging;
using Transaction.Application.Abstractions;

namespace Transaction.Application.Users;

internal sealed class DeactivateUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ILogger<DeactivateUserCommandHandler> logger) : IRequestHandler<DeactivateUserCommand>
{
    public async Task Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Deactivate failed - user not found: {UserId}", request.UserId);
            throw new InvalidOperationException($"User {request.UserId} not found");
        }

        user.Deactivate(request.Reason);
        userRepository.Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User deactivated | UserId={UserId} Reason={Reason}",
            user.Id,
            request.Reason);
    }
}
