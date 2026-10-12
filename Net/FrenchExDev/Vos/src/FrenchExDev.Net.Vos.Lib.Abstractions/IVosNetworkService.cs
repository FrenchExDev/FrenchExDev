namespace FrenchExDev.Net.Vos.Lib.Abstractions;

public interface IVosNetworkService
{
    Task<Res.Result<int>> GenerateAsync(string configPath, string subnet = "192.168.56.0/24", int startAt = 10, bool local = false, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyList<(string Name, string? Ip, string? Hostname)>>> ShowAsync(string configPath, CancellationToken ct = default);
}
