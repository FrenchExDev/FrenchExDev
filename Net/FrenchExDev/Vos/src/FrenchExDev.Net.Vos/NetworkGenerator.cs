using System.Net;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos;

/// <summary>
/// Auto-assigns IP addresses and hostnames to all instances in a <see cref="VosConfig"/>.
/// </summary>
public static class NetworkGenerator
{
    /// <summary>
    /// Assigns sequential IPs from <paramref name="subnet"/> to all enabled instances.
    /// Skips IPs that are already in use by other instances.
    /// Also generates hostnames as <c>{instance-name}.local</c> if not already set.
    /// </summary>
    /// <param name="config">The Vos configuration to modify in place.</param>
    /// <param name="subnet">Subnet in CIDR notation (e.g. 192.168.56.0/24).</param>
    /// <param name="startAt">First host number to assign (default: 10, leaving .1-.9 for gateway/host).</param>
    /// <returns>Number of IPs newly assigned.</returns>
    public static int Generate(VosConfig config, string subnet = "192.168.56.0/24", int startAt = 10)
    {
        var (baseIp, maxHost) = ParseSubnet(subnet);

        // Collect all IPs already in use (manually assigned or previously generated)
        var usedIps = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, machine) in config.Machines)
        {
            if (!machine.IsEnabled) continue;
            foreach (var instance in machine.Instances)
            {
                if (!string.IsNullOrEmpty(instance.Ip))
                    usedIps.Add(instance.Ip);
            }
        }

        var nextHost = startAt;
        var assigned = 0;

        foreach (var (_, machine) in config.Machines)
        {
            if (!machine.IsEnabled) continue;

            foreach (var instance in machine.Instances)
            {
                if (string.IsNullOrEmpty(instance.Ip))
                {
                    // Find next available IP (skip conflicts)
                    while (nextHost <= maxHost && usedIps.Contains($"{baseIp}{nextHost}"))
                        nextHost++;

                    if (nextHost > maxHost)
                        throw new InvalidOperationException(
                            $"Subnet {subnet} exhausted — no more IPs available (max host: {maxHost}).");

                    var ip = $"{baseIp}{nextHost}";
                    instance.Ip = ip;
                    usedIps.Add(ip);
                    assigned++;
                    nextHost++;
                }

                instance.Hostname ??= $"{instance.Name}.local";
            }
        }

        return assigned;
    }

    /// <summary>
    /// Shows current network assignments for all instances.
    /// </summary>
    public static IReadOnlyList<(string Name, string? Ip, string? Hostname)> Show(VosConfig config)
    {
        var results = new List<(string, string?, string?)>();

        foreach (var (_, machine) in config.Machines)
        {
            if (!machine.IsEnabled) continue;

            foreach (var instance in machine.Instances)
                results.Add((instance.Name, instance.Ip, instance.Hostname));
        }

        return results;
    }

    /// <summary>
    /// Validates that no two instances share the same IP.
    /// Returns a list of conflicts.
    /// </summary>
    public static IReadOnlyList<string> ValidateNoConflicts(VosConfig config)
    {
        var seen = new Dictionary<string, string>(); // ip → instance name
        var conflicts = new List<string>();

        foreach (var (_, machine) in config.Machines)
        {
            if (!machine.IsEnabled) continue;
            foreach (var instance in machine.Instances)
            {
                if (string.IsNullOrEmpty(instance.Ip)) continue;

                if (seen.TryGetValue(instance.Ip, out var existing))
                    conflicts.Add($"IP {instance.Ip} is assigned to both '{existing}' and '{instance.Name}'");
                else
                    seen[instance.Ip] = instance.Name;
            }
        }

        return conflicts;
    }

    private static (string BaseIp, int MaxHost) ParseSubnet(string subnet)
    {
        var slashIndex = subnet.IndexOf('/');
        var ipPart = slashIndex > 0 ? subnet.Substring(0, slashIndex) : subnet;
        var prefixLength = slashIndex > 0 ? int.Parse(subnet.Substring(slashIndex + 1)) : 24;

        if (!IPAddress.TryParse(ipPart, out var ip))
            throw new ArgumentException($"Invalid subnet: {subnet}");

        var bytes = ip.GetAddressBytes();
        var baseIp = $"{bytes[0]}.{bytes[1]}.{bytes[2]}.";

        // /24 → 254 hosts (.1-.254), /25 → 126, /26 → 62, etc.
        var hostBits = 32 - prefixLength;
        var maxHost = (1 << hostBits) - 2; // subtract network + broadcast

        return (baseIp, maxHost);
    }
}
