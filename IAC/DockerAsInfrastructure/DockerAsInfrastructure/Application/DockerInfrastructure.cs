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

    //public async Task<(bool inUse, ushort? nextAvailablePort)> IsTcpPortInUse(ushort port)
    //{
    //    var isPortInUse = await IsPortInUse(port);
    //    if (isPortInUse)
    //    {
    //        for (ushort nextPort = (ushort)(port + 1); nextPort < ushort.MaxValue; nextPort++)
    //        {
    //            var isNextPortInUse = await IsPortInUse(nextPort);
    //            if (!isNextPortInUse)
    //            {
    //                return (true, nextPort);
    //            }
    //        }
    //        return (true, null);
    //    }
    //    else
    //    {
    //        return (false, port);
    //    }
    //}

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

    public async Task<bool> IsTcpPortInUse(ushort port) => await IsPortInUse(port);

    public async Task<ushort> GetNextAvailablePort(ushort startingPort = 60000)
    {
        for (ushort port = startingPort; port < ushort.MaxValue; port++)
        {
            var inUse = await IsTcpPortInUse(port);
            if (!inUse)
            {
                return port;
            }
        }
        throw new Exception("No available ports found");
    }
}
