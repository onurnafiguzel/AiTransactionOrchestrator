using Microsoft.EntityFrameworkCore;
using Transaction.Application.Abstractions;
using Transaction.Domain.Users;
using Transaction.Infrastructure.Persistence;

namespace Transaction.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(TransactionDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .AnyAsync(u => u.Email == email.ToLowerInvariant(), cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await context.Users.AddAsync(user, cancellationToken);
    }

    public void Update(User user)
    {
        context.Users.Update(user);
    }
}
