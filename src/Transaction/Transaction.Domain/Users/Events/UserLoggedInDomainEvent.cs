using Transaction.Domain.Common;

namespace Transaction.Domain.Users.Events;

public sealed record UserLoggedInDomainEvent(Guid UserId, DateTime LoggedInAtUtc) : DomainEvent;
