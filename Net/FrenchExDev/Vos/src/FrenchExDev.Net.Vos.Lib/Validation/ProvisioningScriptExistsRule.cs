namespace FrenchExDev.Net.Vos.Lib.Validation;

public sealed class ProvisioningScriptExistsRule(IFileSystem fileSystem, string baseDir) : IValidationRule<VosConfig>
{
    public IEnumerable<string> Validate(VosConfig target)
    {
        foreach (var (_, machineType) in target.MachineTypes)
        {
            if (!machineType.IsEnabled) continue;
            var provPath = machineType.ProvisioningPath ?? "provisioning";

            foreach (var step in machineType.Provisioning)
            {
                if (!step.Enabled) continue;
                var ext = step.Extension ?? "sh";
                var version = step.Version;
                var path = version is not null
                    ? Path.Combine(baseDir, provPath, version, $"{step.Key}.{ext}")
                    : Path.Combine(baseDir, provPath, $"{step.Key}.{ext}");

                if (!fileSystem.GetFile(path).Exists)
                    yield return $"Provisioning script not found: '{path}' (step '{step.Key}').";
            }
        }
    }
}
