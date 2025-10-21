using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DockerAsInfrastructure.Domain;

public interface IInfrastructure
{
    public ConcurrentDictionary<ushort, DockerInstance> DockerInstance { get; init; }

    public Task AddDockerInstance(DockerInstance instance);
    public Task RemoveDockerInstance(string containerId);
}

public class DockerInstance
{
    public string ContainerId { get; init; } = string.Empty;
    public string ContainerName { get; init; } = string.Empty;
    public ushort ContainerHostPort { get; init; }
    public Dictionary<ushort, ushort> Ports { get; set; } = [];
}

public static class DockerImageConfiguration
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
}
