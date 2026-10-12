using System.Net;

namespace FrenchExDev.Net.Vos.Lib.Validation;

public sealed class InstanceIpFormatRule : IValidationRule<VosConfig>
{
    public IEnumerable<string> Validate(VosConfig target)
    {
        foreach (var (machineName, machine) in target.Machines)
            foreach (var instance in machine.Instances)
                if (instance.Ip is not null && !IPAddress.TryParse(instance.Ip, out _))
                    yield return $"Instance '{instance.Name}' in machine '{machineName}' has invalid IP '{instance.Ip}'.";
    }
}
