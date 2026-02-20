using Microsoft.EntityFrameworkCore;
using Transaction.Application.Abstractions;
using Transaction.Domain.Users;

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

    public async Task<(List<User> Items, int TotalCount)> GetAllPagedAsync(
        int skip,
        int take,
        string? sortBy,
        string sortDirection,
        CancellationToken cancellationToken = default)
    {
        var query = context.Users.AsNoTracking();

        // Apply sorting
        query = sortBy?.ToLower() switch
        {
            "email" => sortDirection == "asc"
                ? query.OrderBy(u => u.Email)
                : query.OrderByDescending(u => u.Email),
            "fullname" or "name" => sortDirection == "asc"
                ? query.OrderBy(u => u.FullName)
                : query.OrderByDescending(u => u.FullName),
            "createdat" or "created" => sortDirection == "asc"
                ? query.OrderBy(u => u.CreatedAtUtc)
                : query.OrderByDescending(u => u.CreatedAtUtc),
            _ => sortDirection == "asc"
                ? query.OrderBy(u => u.UpdatedAtUtc)
                : query.OrderByDescending(u => u.UpdatedAtUtc)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
