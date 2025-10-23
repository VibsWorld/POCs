using DotNet.Testcontainers.Builders;

namespace DockerAsInfrastructure.Domain;

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
