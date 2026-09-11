namespace FrenchExDev.Net.Vos.Lib.Validation;

public sealed class MachineTypeHasBoxRule : IValidationRule<VosConfig>
{
    public IEnumerable<string> Validate(VosConfig target)
    {
        foreach (var (typeName, machineType) in target.MachineTypes)
        {
            if (!machineType.IsEnabled) continue;
            if (string.IsNullOrWhiteSpace(machineType.Box))
            {
                var hasBoxOverride = target.Machines.Values
                    .Where(m => m.MachineTypeName == typeName)
                    .Any(m => !string.IsNullOrWhiteSpace(m.Box));
                if (!hasBoxOverride)
                    yield return $"Machine type '{typeName}' has no box defined.";
            }
        }
    }
}
