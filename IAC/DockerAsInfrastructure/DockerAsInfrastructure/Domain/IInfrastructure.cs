using System.Collections.Concurrent;

namespace DockerAsInfrastructure.Domain;

public interface IInfrastructure
{
    public ConcurrentDictionary<string, DockerInstance> Instance { get; init; }
    public Task Add(DockerInstance instance);
    public Task Add(DockerImageType imageType, DockerInstance? instance = null);
    public Task Remove(string id);
    public Task<(bool inUse, ushort? nextAvailablePort)> IsTcpPortInUse(ushort port);
}
