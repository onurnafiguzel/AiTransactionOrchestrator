using Transaction.Domain.Common;

namespace Transaction.Domain.Users.Events;

public sealed record UserCreatedDomainEvent(Guid UserId, string Email) : DomainEvent;
