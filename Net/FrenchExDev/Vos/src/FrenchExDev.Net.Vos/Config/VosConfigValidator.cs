namespace FrenchExDev.Net.Vos.Config;

/// <summary>
/// Validates a <see cref="VosConfig"/> and returns a list of errors.
/// </summary>
public static class VosConfigValidator
{
    public static List<string> Validate(VosConfig config)
    {
        var errors = new List<string>();

        // 1. Every machine must reference an existing machine type
        foreach (var (machineName, machine) in config.Machines)
        {
            if (!config.MachineTypes.ContainsKey(machine.MachineTypeName))
            {
                errors.Add(
                    $"Machine '{machineName}' references machine type '{machine.MachineTypeName}' which does not exist. " +
                    $"Available types: {string.Join(", ", config.MachineTypes.Keys)}");
            }
        }

        // 2. No duplicate instance names across all machines
        var instanceNames = new HashSet<string>();
        foreach (var (machineName, machine) in config.Machines)
        {
            foreach (var instance in machine.Instances)
            {
                if (!instanceNames.Add(instance.Name))
                {
                    errors.Add($"Duplicate instance name '{instance.Name}' in machine '{machineName}'. Instance names must be unique across all machines.");
                }
            }
        }

        // 3. Every enabled machine type must have a box
        foreach (var (typeName, machineType) in config.MachineTypes)
        {
            if (machineType.IsEnabled && string.IsNullOrWhiteSpace(machineType.Box))
            {
                // Check if any machine overrides the box
                var hasBoxOverride = config.Machines.Values
                    .Where(m => m.MachineTypeName == typeName)
                    .Any(m => !string.IsNullOrWhiteSpace(m.Box));

                if (!hasBoxOverride)
                    errors.Add($"Machine type '{typeName}' has no box defined. Set 'box' on the machine type or override it on the machine.");
            }
        }

        // 4. Instance IPs should be valid if set
        foreach (var (machineName, machine) in config.Machines)
        {
            foreach (var instance in machine.Instances)
            {
                if (instance.Ip is not null && !System.Net.IPAddress.TryParse(instance.Ip, out _))
                    errors.Add($"Instance '{instance.Name}' in machine '{machineName}' has invalid IP '{instance.Ip}'.");
            }
        }

        // 5. Instance names must not be empty
        foreach (var (machineName, machine) in config.Machines)
        {
            foreach (var instance in machine.Instances)
            {
                if (string.IsNullOrWhiteSpace(instance.Name))
                    errors.Add($"Machine '{machineName}' has an instance with an empty name.");
            }
        }

        return errors;
    }
}
