using MartenPlayground.Users.Domain;

namespace MartenPlayground.Users.Events;

//Note that we include UserId only in single event to this bounded context as other events will have their own event stream idenfied by UserId so the context will remain attached to that UserId.
public record UserCreated(Guid UserId, string Name, string Email, DateTime UserCreatedAt);

public record UserQueriedByEmail(string Email, DateTime FetchedAt);

public record UserUpdated(User ModifiedUserValues);

public record UserPasswordChanged(DateTime PasswordChangedAt);

public record UserDeleted();
