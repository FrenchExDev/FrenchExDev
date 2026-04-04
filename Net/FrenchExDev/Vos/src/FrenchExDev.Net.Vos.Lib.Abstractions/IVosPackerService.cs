namespace FrenchExDev.Net.Vos.Lib.Abstractions;

public interface IVosPackerService
{
    Task<Res.Result<VosActionResult>> InitAsync(string boxName, string outputPath = ".", CancellationToken ct = default);
    Task<Res.Result<VosImageBuildResult>> BuildAsync(string projectPath, Options.VosPackerBuildOptions? options = null, CancellationToken ct = default);
}
