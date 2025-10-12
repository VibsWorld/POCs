using Marten.Events.Aggregation;

namespace MartenPlayground.Users.Events;

public class UserDashboardStats
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public int TotalTimesUserEmailQueried { get; set; } = 0;
    public DateTime? UserLastEmailQueryAttempt { get; set; }
    public int TotalTimesAddressModified { get; set; } = 0;
    public int TotalTimesUserRolesModified { get; set; } = 0;
    public decimal CurrentTotalWalletBalance { get; set; } = 0;
}

public class UserDashboardViewProjection : SingleStreamProjection<UserDashboardStats, Guid>
{
    public UserDashboardStats Create(UserCreated @event) =>
        new()
        {
            Id = @event.UserId,
            Name = @event.Name,
            Email = @event.Email,
        };

    public void Apply(UserDashboardStats view, UserQueriedByEmail @event)
    {
        view.UserLastEmailQueryAttempt = @event.FetchedAt;
        view.TotalTimesUserEmailQueried++;
    }

    public void Apply(UserDashboardStats view, UserAddressDetailsModified @event) =>
        view.TotalTimesAddressModified++;

    public void Apply(UserDashboardStats view, UserRolesAdded @event) =>
        view.TotalTimesUserRolesModified++;

    public void Apply(UserDashboardStats view, UserRoleRemoved @event) =>
        view.TotalTimesUserRolesModified++;

    public void Apply(UserDashboardStats view, UserWalletBalanceAdjusted @event) =>
        view.CurrentTotalWalletBalance += @event.Amount;
}
