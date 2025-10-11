namespace MartenPlayground.Tests.Docker;

using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using AutoFixture;
using MartenPlayground.Users.Controller;
using MartenPlayground.Users.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

public class UserRepositoryTests
    : IClassFixture<WebApplicationFactory<Program>>,
        IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _databaseFixture;

    private readonly WebApplicationFactory<Program> _factory;
    private readonly Fixture fixture = new();
    private PostgreSqlContainer? postgreSqlContainer;
    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public UserRepositoryTests(
        DatabaseFixture databaseFixture,
        WebApplicationFactory<Program> factory
    )
    {
        _factory = factory;
        _databaseFixture = databaseFixture;
        var port = _databaseFixture.Port();
    }

    /// <summary>
    /// Ensure that Postgres PORT 5432 is NOT IN USE before running these integrated tests as appconfig uses localhost:5432 by default
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task GetUserFromEmail_WhenProvidedNotInsertedEmail_ReturnsNotFoundMessage()
    {
        //Arrange
        var client = _factory.CreateClient();

        //Act
        var response = await client.GetAsync("/User/GetUserFromEmail/" + "test@test.com");
        var responseContent = await response.Content.ReadAsStringAsync();

        //Assert
        Assert.NotNull(responseContent);
        Assert.Contains("notFoundMessage", responseContent);
    }

    [Fact]
    public async Task Create_WithProperUserDetails_CreatesUserInRepository()
    {
        //Arrange
        var client = _factory.CreateClient();
        var user = GenerateAValidUserForInsert();

        //Act
        var response = await client.PostAsJsonAsync(
            "/User/Create",
            new CreateUserRequest(
                user.Name,
                user.Phone,
                user.Email,
                user.Address,
                user.City,
                user.Country
            )
        );
        var result = await response.Content.ReadAsStringAsync();

        //Assert
        Assert.NotNull(result);
        var resultUserCreatedSuccessfully = JsonSerializer.Deserialize<UserCreatedSuccessfully>(
            result,
            serializerOptions
        );
        Assert.NotNull(resultUserCreatedSuccessfully);
        Assert.IsType<Guid>(resultUserCreatedSuccessfully.Id);
        Assert.True(resultUserCreatedSuccessfully.Id != default);
    }

    //Intentionally Assertion Roulette as context of GET methods is same for both
    [Fact]
    public async Task GetUserById_And_GetUserFromEmail_Should_GetNewlyInsertedItems()
    {
        //Arrange
        var client = _factory.CreateClient();
        var user = GenerateAValidUserForInsert();
        var responseArrange = await client.PostAsJsonAsync(
            "/User/Create",
            new CreateUserRequest(
                user.Name,
                user.Phone,
                user.Email,
                user.Address,
                user.City,
                user.Country
            )
        );
        var userCreated = JsonSerializer.Deserialize<UserCreatedSuccessfully>(
            await responseArrange.Content.ReadAsStringAsync(),
            serializerOptions
        );
        ArgumentNullException.ThrowIfNull(userCreated);

        //Act
        var userResult = await client.GetFromJsonAsync<User>(
            "/User/GetUserById/" + userCreated!.Id
        );

        //Asserts
        Assert.NotNull(userResult);
        var resultEmailCheck = await client.GetFromJsonAsync<UserFoundResponse>(
            "/User/GetUserFromEmail/" + userResult.Email
        );
        Assert.NotNull(resultEmailCheck);
        Assert.Equivalent(userResult, resultEmailCheck.User);
    }

    [Fact]
    public async Task Update_UpdatesUserAfterUserCreation_EnsureFieldIsUpdatedProperly()
    {
        //Arrange
        var client = _factory.CreateClient();
        var user = GenerateAValidUserForInsert();
        var responseArrange = await client.PostAsJsonAsync(
            "/User/Create",
            new CreateUserRequest(
                user.Name,
                user.Phone,
                user.Email,
                user.Address,
                user.City,
                user.Country
            )
        );
        var userCreated = JsonSerializer.Deserialize<UserCreatedSuccessfully>(
            await responseArrange.Content.ReadAsStringAsync(),
            serializerOptions
        );
        ArgumentNullException.ThrowIfNull(userCreated);
        var userFromId = await client.GetFromJsonAsync<User>(
            "/User/GetUserById/" + userCreated!.Id
        );
        Assert.NotNull(userFromId);
        string nameToBeupdated = fixture.Create<string>();
        userFromId.Name = nameToBeupdated;

        //Act
        var responseUpdate = await client.PutAsJsonAsync("/User/update", userFromId);
        var responseAsContentForUpdate = await responseUpdate.Content.ReadAsStringAsync();
        Assert.NotNull(responseAsContentForUpdate);
        var userUpdateResult = JsonSerializer.Deserialize<UserUpdatedSuccessfully>(
            responseAsContentForUpdate!,
            serializerOptions
        );
        Assert.NotNull(userUpdateResult);
        var updatedUser = await client.GetFromJsonAsync<User>(
            "/User/GetUserById/" + userUpdateResult!.Id
        );
        Assert.NotNull(updatedUser);
        Assert.Equal(updatedUser.Name, nameToBeupdated);
    }

    private User GenerateAValidUserForUpdateWithoutId() =>
        fixture.Build<User>().With(x => x.Email, GenerateValidEmail()).Create();

    private User GenerateAValidUserForInsert() =>
        fixture.Build<User>().Without(x => x.Id).With(x => x.Email, GenerateValidEmail()).Create();

    private string GenerateValidEmail() =>
        $"{fixture.Create<EmailAddressLocalPart>().LocalPart}@{fixture.Create<DomainName>().Domain}";
}
