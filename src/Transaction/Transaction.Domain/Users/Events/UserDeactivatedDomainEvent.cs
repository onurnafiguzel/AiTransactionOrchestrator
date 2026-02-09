using Transaction.Domain.Common;

namespace Transaction.Domain.Users.Events;

public sealed record UserDeactivatedDomainEvent(Guid UserId, string Reason) : DomainEvent;
