using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using DockerAsInfrastructure.Domain;

namespace DockerAsInfrastructure.Application;

public class DockerInfrastructure : IInfrastructure
{
    public ConcurrentDictionary<string, DockerInstance> Instance { get; init; } = new();

    public async Task Add(DockerInstance instance)
    {
        throw new NotImplementedException();
    }

    public async Task Add(DockerImageType imageType, DockerInstance? instance = null)
    {
        throw new NotImplementedException();
    }

    public async Task<(bool inUse, ushort? nextAvailablePort)> IsTcpPortInUse(ushort port)
    {
        var isPortInUse = await IsPortInUse(port);
        if (isPortInUse)
        {
            for (ushort nextPort = (ushort)(port + 1); nextPort < ushort.MaxValue; nextPort++)
            {
                var isNextPortInUse = await IsPortInUse(nextPort);
                if (!isNextPortInUse)
                {
                    return (true, nextPort);
                }
            }
            return (true, null);
        }
        else
        {
            return (false, port);
        }
    }

    public async Task Remove(string id)
    {
        throw new NotImplementedException();
    }

    private static Task<bool> IsPortInUse(ushort port)
    {
        IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
        IPEndPoint[] tcpListeners = ipGlobalProperties.GetActiveTcpListeners();
        return Task.FromResult(tcpListeners.Any(endpoint => endpoint.Port == port));
    }
}
