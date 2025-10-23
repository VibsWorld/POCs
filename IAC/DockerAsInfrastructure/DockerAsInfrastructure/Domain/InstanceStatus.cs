namespace DockerAsInfrastructure.Domain;

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
