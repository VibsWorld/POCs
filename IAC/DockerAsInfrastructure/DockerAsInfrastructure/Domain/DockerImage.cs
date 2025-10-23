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
}
