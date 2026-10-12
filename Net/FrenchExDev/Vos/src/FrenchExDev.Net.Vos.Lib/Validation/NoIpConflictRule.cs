namespace FrenchExDev.Net.Vos.Lib.Validation;

public sealed class NoIpConflictRule : IValidationRule<VosConfig>
{
    public IEnumerable<string> Validate(VosConfig target)
    {
        var seen = new Dictionary<string, string>();
        foreach (var (_, machine) in target.Machines)
        {
            if (!machine.IsEnabled) continue;
            foreach (var instance in machine.Instances)
            {
                if (string.IsNullOrEmpty(instance.Ip)) continue;
                if (seen.TryGetValue(instance.Ip, out var existing))
                    yield return $"IP {instance.Ip} is assigned to both '{existing}' and '{instance.Name}'.";
                else
                    seen[instance.Ip] = instance.Name;
            }
        }
    }
}
