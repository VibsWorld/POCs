using DockerAsInfrastructure.Application;

namespace DockerAsInfrastructure.Test.Integration
{
    public class UnitTest1
    {
        [Fact]
        public async Task Test1()
        {
            var infra = new DockerInfrastructure();
            var result = await infra.IsTcpPortInUse(5432);
        }
    }
}
