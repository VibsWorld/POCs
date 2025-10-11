using Marten;
using MartenPlayground.Users.Domain;
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
            .Where(x => x.Email.Equals(Email, StringComparison.OrdinalIgnoreCase))
            .ToListAsync();

        if (user.Count == 0)
            return new NotFoundResponse($"No user found with email '{Email}'");

        return new UserFoundResponse(user[0]);
    }
}
