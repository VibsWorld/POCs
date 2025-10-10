using Marten;
using MartenPlayground.Users.Domain;
using Microsoft.AspNetCore.Mvc;

namespace MartenPlayground.Users.Controller;

public record CreateUserRequest(string Name, string MobileNumber, string Email);

public record CreateUserResponse(Guid? Id);

[ApiController]
[Route("[controller]")]
public class UserController : ControllerBase
{
    private readonly ILogger<UserController> logger;
    private readonly IDocumentSession session;

    public UserController(ILogger<UserController> logger, IDocumentSession session)
    {
        this.logger = logger;
        this.session = session;
    }

    [HttpPost(Name = "Create")]
    public async Task<CreateUserResponse> Create(CreateUserRequest request)
    {
        await Task.Delay(1);

        var user = new User
        {
            Email = "vaibhav@parcelhero.com",
            City = "New Delhi",
            Name = "Vaibhav",
            Country = "India",
            Address = new Address { AddressLine1 = "Addr Line 1" },
            Phone = "123456",
        };

        session.Store(user);
        await session.SaveChangesAsync();

        logger.LogInformation("{log}", request);
        return new CreateUserResponse(user.Id);
    }
}
