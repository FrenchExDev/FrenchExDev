namespace FrenchExDev.Net.Vos.Lib.Validation;

public sealed class UniqueInstanceNameRule : IValidationRule<VosConfig>
{
    public IEnumerable<string> Validate(VosConfig target)
    {
        var seen = new HashSet<string>();
        foreach (var (machineName, machine) in target.Machines)
            foreach (var instance in machine.Instances)
                if (!seen.Add(instance.Name))
                    yield return $"Duplicate instance name '{instance.Name}' in machine '{machineName}'. Instance names must be unique across all machines.";
    }
}
