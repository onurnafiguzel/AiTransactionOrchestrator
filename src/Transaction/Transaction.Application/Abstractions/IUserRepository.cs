using Transaction.Domain.Users;

namespace Transaction.Application.Abstractions;

/// <summary>
/// User Repository Interface
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    void Update(User user);
    Task<(List<User> Items, int TotalCount)> GetAllPagedAsync(
        int skip,
        int take,
        string? sortBy,
        string sortDirection,
        CancellationToken cancellationToken = default);
}
