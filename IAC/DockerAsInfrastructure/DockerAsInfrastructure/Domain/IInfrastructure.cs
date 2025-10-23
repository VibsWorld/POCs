using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using static DockerAsInfrastructure.Domain.DockerImage;

namespace DockerAsInfrastructure.Domain;

public interface IInfrastructure
{
    public ConcurrentDictionary<string, DockerInstance> Instance { get; init; }
    public Task Add(DockerInstance instance);
    public Task Add(DockerImageType imageType, DockerInstance? instance = null);
    public Task RemoveInstance(string id);
}

/// <summary>
///
/// </summary>
/// <param name="Id">Docker Container Id</param>
/// <param name="Name">Docker Container Name</param>
/// <param name="Ports"></param>
/// <param name="HttpPortMappingWithRelativeSourceMapping">
/// Mapping of HTTP ports to their relative source paths for testing
/// </param>
/// <param name="Status"></param>
/// <param name="EnvironmentVariables"></param>
public class DockerInstance
{
    public DockerInstance(
        DockerImageType imageType,
        string? id = null,
        string? name = null,
        string? dns = null
    )
    {
        Id = id ?? Guid.NewGuid().ToString("N");
        Name = name ?? imageType.ToString() + "-" + Guid.NewGuid().ToString("N");
        Dns = dns ?? Name;
    }

    public string Id { get; init; }
    public string Name { get; init; }
    public string Dns { get; init; }
    public DockerImageType ImageType { get; init; }
    public IDictionary<ushort, ushort>? AssignedPorts { get; }
    public IDictionary<ushort, string>? HttpPortMappingWithRelativeSourceMapping { get; }
    public InstanceStatus Status { get; set; } = InstanceStatus.Undefined;
    public List<KeyValuePair<string, string>>? EnvironmentVariables { get; set; }
}

public enum InstanceStatus
{
    Undefined,
    Created,
    Running,
    Paused,
    Restarting,
    Removing,
    Exited,
    Dead,
}

public static class DockerImage
{
    public const string Postgres = "postgres:latest";
    public const string Redis = "redis:latest";
    public const string RabbitMq = "rabbitmq:latest";
    public const string MongoDb = "mongo:latest";
    public const string SqlServer = "mcr.microsoft.com/mssql/server:2022-latest";
    public const string Nginx = "nginx:latest";
    public const string Kafka = "confluentinc/cp-kafka:latest";
    public const string Zookeeper = "confluentinc/cp-zookeeper:latest";
    public const string RabbitMqManagement = "rabbitmq:4-management";
    public const string EventStore = "eventstore/eventstore:latest";
    public const string Unleash = "unleashorg/unleash:latest";
    public const string Wiremock = "wiremock/wiremock:latest";

    public enum DockerImageType
    {
        Postgres,
        Redis,
        RabbitMq,
        MongoDb,
        SqlServer,
        Nginx,
        Kafka,
        Zookeeper,
        RabbitMqManagement,
        EventStore,
        Unleash,
        Wiremock,
        Custom,
    }
}

public static class DockerDefaultPorts
{
    public const ushort Postgres = 5432;
    public const ushort Redis = 6379;
    public const ushort RabbitMq = 5672;
    public const ushort MongoDb = 27017;
    public const ushort SqlServer = 1433;
    public const ushort Nginx = 80;
    public const ushort Kafka = 9092;
    public const ushort Zookeeper = 2181;
    public const ushort RabbitMqManagement = 15672;
    public const ushort EventStoreTcp = 1113;
    public const ushort EventStoreHttp = 2113;
    public const ushort Unleash = 4242;

    public static async Task Test()
    {
        var container = new ContainerBuilder()
            // Set the image for the container to "testcontainers/helloworld:1.3.0".
            .WithImage("testcontainers/helloworld:1.3.0")
            // Bind port 8080 of the container to a random port on the host.
            .WithPortBinding(8080, true)
            // Wait until the HTTP endpoint of the container is available.
            .WithWaitStrategy(
                Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8080))
            )
            // Build the container configuration.
            .Build();

        await container.StartAsync();

        var state = container.State;

        await Task.CompletedTask;
    }
}
