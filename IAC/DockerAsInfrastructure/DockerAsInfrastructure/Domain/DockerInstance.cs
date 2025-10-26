using static DockerAsInfrastructure.Domain.DockerImage;

namespace DockerAsInfrastructure.Domain;

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
        IDictionary<ushort, ushort> assignedPorts,
        IDictionary<ushort, string> healthCheckHttpURIs,
        string? id = null,
        string? name = null,
        string? dns = null
    )
    {
        Id = id ?? Guid.NewGuid().ToString("N");
        Name = name ?? imageType.ToString() + "-" + Guid.NewGuid().ToString("N");
        Dns = dns ?? Name;
        AssignedPorts = assignedPorts ?? throw new Exception("Assigned ports cannot be null");
        HealthCheckHttpURIs = healthCheckHttpURIs ?? new Dictionary<ushort, string>();
    }

    public string Id { get; init; }
    public string Name { get; init; }
    public string Dns { get; init; }
    public DockerImageType ImageType { get; init; }
    public IDictionary<ushort, ushort> AssignedPorts { get; init; }
    public IDictionary<ushort, string> HealthCheckHttpURIs { get; init; }
    public InstanceStatus Status { get; set; } = InstanceStatus.Undefined;
    public List<KeyValuePair<string, string>>? EnvironmentVariables { get; set; }
}
