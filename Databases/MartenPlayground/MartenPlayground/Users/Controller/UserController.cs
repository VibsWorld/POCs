using Marten;
using MartenPlayground.Users.Domain;
using Microsoft.AspNetCore.Mvc;

namespace MartenPlayground.Users.Controller;

public record CreateUserRequest(string Name, string MobileNumber, string Email);

public abstract record CreateUserResponse();

public record SuccessUserResponse(Guid Id) : CreateUserResponse;

public record FailureUserResponse(string FailtureMessage) : CreateUserResponse;

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
        if (
            await session
                .Query<User>()
                .Where(x => x.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase))
                .AnyAsync()
        )
            return new FailureUserResponse("User already exists");

        var user = new User
        {
            Email = "vaibhav@parcelhero.com",
            City = "New Delhi",
            Name = "Vaibhav",
            Country = "India",
            Address = new Address
            {
                AddressLine1 = "Addr Line 1",
                AddressLine2 = "test",
                Zip = "Sample Zip",
            },
            Phone = "123456",
        };

        session.Store(user);
        await session.SaveChangesAsync();

        logger.LogInformation("{log}", request);
        return new SuccessUserResponse(user.Id);
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
