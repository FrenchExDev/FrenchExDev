namespace FrenchExDev.Net.Vos.Lib.Validation;

public sealed class MachineTypeReferenceRule : IValidationRule<VosConfig>
{
    public IEnumerable<string> Validate(VosConfig target)
    {
        foreach (var (machineName, machine) in target.Machines)
        {
            if (!target.MachineTypes.ContainsKey(machine.MachineTypeName))
                yield return $"Machine '{machineName}' references machine type '{machine.MachineTypeName}' which does not exist. Available types: {string.Join(", ", target.MachineTypes.Keys)}";
        }
    }
}
