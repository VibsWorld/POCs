using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Testcontainers.PostgreSql;

namespace MartenPlayground.Tests.Docker;

public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer? postgreSqlContainer;

    public ushort Port()
    {
        return postgreSqlContainer!.GetMappedPublicPort(PostgreSqlBuilder.PostgreSqlPort);
    }

    public DatabaseFixture() =>
        postgreSqlContainer = new PostgreSqlBuilder()
            .WithPassword("changeit")
            .WithPortBinding(PostgreSqlBuilder.PostgreSqlPort, false)
            .Build();

    public async Task InitializeAsync() => await postgreSqlContainer!.StartAsync();

    public async Task DisposeAsync() => await postgreSqlContainer!.DisposeAsync().AsTask();
}
