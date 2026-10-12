namespace FrenchExDev.Net.Vos.Abstractions;

public sealed class VosLibOptions
{
    public string DefaultConfigPath { get; set; } = "config-vos.yaml";
    public string DefaultSubnet { get; set; } = "192.168.56.0/24";
    public int DefaultStartAt { get; set; } = 10;
    public FailureStrategy OnPartialFailure { get; set; } = FailureStrategy.Stop;
}

public enum FailureStrategy
{
    Stop,
    Continue,
    Rollback
}
