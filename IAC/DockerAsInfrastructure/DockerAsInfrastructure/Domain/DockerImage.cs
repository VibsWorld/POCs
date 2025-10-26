namespace DockerAsInfrastructure.Domain;

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

    public static ushort GetDefaultPort(DockerImageType imageType) => imageType switch
    {
        DockerImageType.Postgres => 5432,
        DockerImageType.Redis => 6379,
        DockerImageType.RabbitMq => 5672,
        DockerImageType.MongoDb => 27017,
        DockerImageType.SqlServer => 1433,
        DockerImageType.Nginx => 80,
        DockerImageType.Kafka => 9092,
        DockerImageType.Zookeeper => 2181,
        DockerImageType.RabbitMqManagement => 15672,
        DockerImageType.EventStore => 2113,
        DockerImageType.Unleash => 4242,
        DockerImageType.Wiremock => 8080,
        DockerImageType.Custom => throw new ArgumentException("Custom image type does not have a default port."),
        _ => throw new ArgumentOutOfRangeException(nameof(imageType), $"Not expected image type value: {imageType}"),
    };
}
