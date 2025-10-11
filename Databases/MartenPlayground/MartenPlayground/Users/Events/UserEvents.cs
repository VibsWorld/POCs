using MartenPlayground.Users.Domain;

namespace MartenPlayground.Users.Events;

//Note that we include UserId only in single event to this bounded context as other events will have their own event stream idenfied by UserId so the context will remain attached to that UserId.
public record UserCreated(Guid UserId, string Name, string Email, DateTime UserCreatedAt);

public record UserUpdated(User User);

public record UserQueriedByEmail(string Email, DateTime FetchedAt);

public record UserAddressDetailsModified(Address Address);

public record UserRolesAdded(string[] Roles);

public record UserRoleRemoved(string Role);

public record UserWalletBalanceAdjusted(decimal Amount);

public record UserDeleted();
