namespace FrenchExDev.Net.Vos.Lib.Validation;

public sealed class InstanceNameNotEmptyRule : IValidationRule<VosConfig>
{
    public IEnumerable<string> Validate(VosConfig target)
    {
        foreach (var (machineName, machine) in target.Machines)
            foreach (var instance in machine.Instances)
                if (string.IsNullOrWhiteSpace(instance.Name))
                    yield return $"Machine '{machineName}' has an instance with an empty name.";
    }
}
