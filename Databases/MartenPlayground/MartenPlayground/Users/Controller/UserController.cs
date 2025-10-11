using Marten;
using Marten.Linq.SoftDeletes;
using MartenPlayground.Users.Domain;
using MartenPlayground.Users.Events;
using Microsoft.AspNetCore.Mvc;

namespace MartenPlayground.Users.Controller;

public record CreateUserRequest(
    string Name,
    string Phone,
    string Email,
    Address Address,
    string City,
    string Country
);

public abstract record CreateUserResponse();

public record UserCreatedSuccessfully(Guid Id) : CreateUserResponse;

public record UnableToCreateUser(string FailtureMessage) : CreateUserResponse;

public abstract record UserResponse;

public record NotFoundResponse(string NotFoundMessage) : UserResponse;

public record UserFoundResponse(User User) : UserResponse;

[ApiController]
[Route("[controller]")]
public class UserController : ControllerBase
{
    private readonly ILogger<UserController> logger;
    private readonly IDocumentSession session;
    private readonly IQuerySession sessionQuery;

    public UserController(
        ILogger<UserController> logger,
        IDocumentSession session,
        IQuerySession sessionQuery
    )
    {
        this.logger = logger;
        this.session = session;
        this.sessionQuery = sessionQuery;
    }

    [HttpPost(Name = "Create")]
    public async Task<CreateUserResponse> Create(CreateUserRequest request)
    {
        logger.LogInformation("Create User Request Received - {log}", request);

        if (
            await sessionQuery
                .Query<User>()
                .Where(x => x.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase))
                .AnyAsync()
        )
            return new UnableToCreateUser("User already exists");

        var user = new User
        {
            Email = request.Email,
            City = request.City,
            Name = request.Name,
            Country = request.Country,
            Address = new Address
            {
                AddressLine1 = request.Address.AddressLine1,
                AddressLine2 = request.Address.AddressLine2,
                Zip = request.Address.Zip,
            },
            Phone = request.Phone,
        };

        session.Store(user);
        await session.SaveChangesAsync();

        //Event
        var userCreatedEvent = new UserCreated(user.Id, user.Name, user.Email, DateTime.Now);
        session.Events.StartStream<User>(user.Id, userCreatedEvent);
        await session.SaveChangesAsync();

        logger.LogInformation("User {log} saved successfully", request);
        return new UserCreatedSuccessfully(user.Id);
    }

    [HttpGet("GetUserById/{Id:guid}")]
    public async Task<User> Get(Guid Id) => await sessionQuery.LoadAsync<User>(Id);

    [HttpGet("GetUserFromEmail/{Email}")]
    public async Task<UserResponse> GetUserFromEmail(string Email)
    {
        var user = await sessionQuery
            .Query<User>()
            .FirstOrDefaultAsync((x => x.Email.Equals(Email, StringComparison.OrdinalIgnoreCase)));

        if (user is null || user.Id == default)
            return new NotFoundResponse($"No user found with email '{Email}'");

        //Add Event - UserQueriedByEmail
        var userQueriedByEmailEvent = new UserQueriedByEmail(Email, DateTime.Now);
        session.Events.Append(user.Id, userQueriedByEmailEvent);
        await session.SaveChangesAsync();

        return new UserFoundResponse(user);
    }

    [HttpDelete]
    public Task<bool> DeleteUserById(Guid Id)
    {
        session.Delete<User>(Id);
        //To Delete with WHERE clause basis of any sub property use DeleteWhere as shown below
        //session.DeleteWhere<User>(user => user.Id == Id);
        return Task.FromResult(true);
    }
}
