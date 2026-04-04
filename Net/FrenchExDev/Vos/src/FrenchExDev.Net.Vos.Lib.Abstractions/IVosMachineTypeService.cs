using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Lib.Abstractions;

public interface IVosMachineTypeService
{
    // CRUD
    Task<Res.Result> AddAsync(string configPath, string name, string box, int memory = 2048, int cpus = 2, bool local = false, CancellationToken ct = default);
    Task<Res.Result> RemoveAsync(string configPath, string name, bool local = false, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyDictionary<string, VosMachineType>>> ListAsync(string configPath, CancellationToken ct = default);
    Task<Res.Result<VosMachineType>> ShowAsync(string configPath, string name, CancellationToken ct = default);
    Task<Res.Result> SetAsync(string configPath, string name, Options.MachineTypeSettings settings, bool local = false, CancellationToken ct = default);

    // VBoxManage
    Task<Res.Result> AddVboxManageAsync(string configPath, string name, IReadOnlyList<string> args, bool local = false, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyList<List<string>>>> ListVboxManageAsync(string configPath, string name, CancellationToken ct = default);
    Task<Res.Result> RemoveVboxManageAsync(string configPath, string name, int index, bool local = false, CancellationToken ct = default);
    Task<Res.Result> ClearVboxManageAsync(string configPath, string name, bool local = false, CancellationToken ct = default);

    // Provisioning
    Task<Res.Result> AddProvisioningStepAsync(string configPath, string typeName, string key, Options.VosProvisioningStepOptions? options = null, bool local = false, CancellationToken ct = default);
    Task<Res.Result> RemoveProvisioningStepAsync(string configPath, string typeName, string key, bool local = false, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyList<VosProvisioningStep>>> ListProvisioningStepsAsync(string configPath, string typeName, CancellationToken ct = default);
    Task<Res.Result> EnableProvisioningStepAsync(string configPath, string typeName, string key, bool local = false, CancellationToken ct = default);
    Task<Res.Result> DisableProvisioningStepAsync(string configPath, string typeName, string key, bool local = false, CancellationToken ct = default);
    Task<Res.Result> MoveProvisioningStepAsync(string configPath, string typeName, string key, string? before = null, string? after = null, bool local = false, CancellationToken ct = default);

    // Shared folders
    Task<Res.Result> AddSharedFolderAsync(string configPath, string typeName, string hostPath, string guestPath, string? sfType = null, bool disabled = false, bool local = false, CancellationToken ct = default);
    Task<Res.Result> RemoveSharedFolderAsync(string configPath, string typeName, int index, bool local = false, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyList<VosSharedFolder>>> ListSharedFoldersAsync(string configPath, string typeName, CancellationToken ct = default);

    // Plugins
    Task<Res.Result> AddPluginAsync(string configPath, string typeName, string name, bool local = false, CancellationToken ct = default);
    Task<Res.Result> RemovePluginAsync(string configPath, string typeName, string name, bool local = false, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyList<string>>> ListPluginsAsync(string configPath, string typeName, CancellationToken ct = default);
}
