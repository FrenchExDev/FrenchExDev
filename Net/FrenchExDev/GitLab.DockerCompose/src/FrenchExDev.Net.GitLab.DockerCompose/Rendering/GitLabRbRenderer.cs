using System.Reflection;
using System.Text;

namespace FrenchExDev.Net.GitLab.DockerCompose.Rendering;

/// <summary>
/// Renders a built <see cref="GitLabOmnibusConfig"/> to Ruby syntax
/// for the <c>GITLAB_OMNIBUS_CONFIG</c> environment variable.
/// Only non-null properties are rendered.
/// </summary>
public static class GitLabRbRenderer
{
    // Mapping from C# property name on the root config to (rubyKey, isStandalone)
    private static readonly Dictionary<string, (string RubyKey, bool IsStandalone)> StandaloneUrlMap = new()
    {
        ["ExternalUrl"] = ("external_url", true),
        ["RegistryExternalUrl"] = ("registry_external_url", true),
        ["PagesExternalUrl"] = ("pages_external_url", true),
        ["MattermostExternalUrl"] = ("mattermost_external_url", true),
        ["GitlabKasExternalUrl"] = ("gitlab_kas_external_url", true),
        ["RuntimeDir"] = ("runtime_dir", true),
    };

    // Mapping from C# property name on the root config to Ruby prefix
    private static readonly Dictionary<string, string> PrefixMap = BuildPrefixMap();

    public static string Render(GitLabOmnibusConfig config)
    {
        var sb = new StringBuilder();

        // 1. Standalone URLs (single-quoted)
        RenderStandaloneUrls(sb, config);

        // 2. Per-prefix sections
        foreach (var prop in typeof(GitLabOmnibusConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (StandaloneUrlMap.ContainsKey(prop.Name)) continue;
            if (prop.Name == "Roles") continue; // handled separately

            var value = prop.GetValue(config);
            if (value is null) continue;

            if (!PrefixMap.TryGetValue(prop.Name, out var prefix))
                continue;

            sb.AppendLine();
            RenderSection(sb, prefix, value, new List<string>());
        }

        // 3. Roles (special standalone list)
        if (config.Roles is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"roles {config.Roles}");
        }

        return sb.ToString();
    }

    private static void RenderStandaloneUrls(StringBuilder sb, GitLabOmnibusConfig config)
    {
        foreach (var kvp in StandaloneUrlMap)
        {
            var prop = typeof(GitLabOmnibusConfig).GetProperty(kvp.Key);
            if (prop is null) continue;
            var value = prop.GetValue(config) as string;
            if (value is null) continue;

            sb.AppendLine($"{kvp.Value.RubyKey} '{value}'");
        }
    }

    private static void RenderSection(StringBuilder sb, string prefix, object section, List<string> keyPath)
    {
        foreach (var prop in section.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var value = prop.GetValue(section);
            if (value is null) continue;

            var rubyKey = ToSnakeCase(prop.Name);
            var currentPath = new List<string>(keyPath) { rubyKey };

            // Sub-object (another config class)
            if (IsConfigClass(prop.PropertyType))
            {
                RenderSection(sb, prefix, value, currentPath);
                continue;
            }

            // Leaf value — render as prefix['key1']['key2'] = value
            var bracketPath = string.Join("", currentPath.Select(k => $"['{k}']"));
            var rubyValue = FormatRubyValue(value);
            sb.AppendLine($"{prefix}{bracketPath} = {rubyValue}");
        }
    }

    private static string FormatRubyValue(object value)
    {
        return value switch
        {
            bool b => b ? "true" : "false",
            int i => i.ToString(),
            long l => l.ToString(),
            double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            string s => $"\"{EscapeRubyString(s)}\"",
            List<string> list => FormatStringList(list),
            List<double> list => FormatDoubleList(list),
            Dictionary<string, string?> dict => FormatStringDict(dict),
            _ => $"\"{value}\""
        };
    }

    private static string FormatStringList(List<string> list)
    {
        var items = string.Join(", ", list.Select(s => $"'{EscapeRubyString(s)}'"));
        return $"[{items}]";
    }

    private static string FormatDoubleList(List<double> list)
    {
        var items = string.Join(", ", list.Select(d =>
            d.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return $"[{items}]";
    }

    private static string FormatStringDict(Dictionary<string, string?> dict)
    {
        if (dict.Count == 0) return "{}";

        var entries = dict.Select(kvp =>
            $"\"{EscapeRubyString(kvp.Key)}\" => \"{EscapeRubyString(kvp.Value ?? "")}\"");

        if (dict.Count <= 3)
            return "{ " + string.Join(", ", entries) + " }";

        var sb = new StringBuilder();
        sb.AppendLine("{");
        foreach (var entry in entries)
            sb.AppendLine($"  {entry},");
        sb.Append("}");
        return sb.ToString();
    }

    private static string EscapeRubyString(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static bool IsConfigClass(Type type)
    {
        var actual = Nullable.GetUnderlyingType(type) ?? type;
        return actual.IsClass && actual.Namespace == "FrenchExDev.Net.GitLab.DockerCompose"
               && actual != typeof(string);
    }

    private static string ToSnakeCase(string pascalCase)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < pascalCase.Length; i++)
        {
            var c = pascalCase[i];
            if (char.IsUpper(c) && i > 0)
            {
                // Don't insert underscore between consecutive uppercase (e.g., "DB" → "db")
                if (!char.IsUpper(pascalCase[i - 1]) ||
                    (i + 1 < pascalCase.Length && char.IsLower(pascalCase[i + 1])))
                    sb.Append('_');
            }
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static Dictionary<string, string> BuildPrefixMap()
    {
        // Build reverse map from C# property name → Ruby prefix
        // by using the metadata or by converting property names back
        var map = new Dictionary<string, string>();
        foreach (var prop in typeof(GitLabOmnibusConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var typeName = (Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType).Name;
            if (!typeName.EndsWith("Config")) continue;

            // Convert "NginxConfig" → "nginx", "GitLabRailsConfig" → "gitlab_rails"
            var prefix = ToSnakeCase(typeName.Replace("Config", ""));
            map[prop.Name] = prefix;
        }

        // Fix known special cases where ToSnakeCase doesn't produce the right prefix
        map["GitlabRails"] = "gitlab_rails";
        map["GitlabWorkhorse"] = "gitlab_workhorse";
        map["GitlabShell"] = "gitlab_shell";
        map["GitlabSshd"] = "gitlab_sshd";
        map["GitlabKas"] = "gitlab_kas";
        map["GitlabPages"] = "gitlab_pages";
        map["GitlabExporter"] = "gitlab_exporter";
        map["GitlabCi"] = "gitlab_ci";
        map["GitlabBackupCli"] = "gitlab_backup_cli";
        map["GitalyClient"] = "gitaly_client";
        map["GeoLogcursor"] = "geo_logcursor";
        map["PagesNginx"] = "pages_nginx";
        map["RegistryNginx"] = "registry_nginx";
        map["MattermostNginx"] = "mattermost_nginx";
        map["RedisMasterRole"] = "redis_master_role";
        map["RedisReplicaRole"] = "redis_replica_role";
        map["RedisSentinelRole"] = "redis_sentinel_role";
        map["MonitoringRole"] = "monitoring_role";
        map["PrometheusMonitoring"] = "prometheus_monitoring";
        map["ManageAccounts"] = "manage_accounts";
        map["ManageStorageDirectories"] = "manage_storage_directories";
        map["HighAvailability"] = "high_availability";
        map["StorageCheck"] = "storage_check";
        map["WebServer"] = "web_server";
        map["NodeExporter"] = "node_exporter";
        map["RedisExporter"] = "redis_exporter";
        map["PostgresExporter"] = "postgres_exporter";
        map["PgbouncerExporter"] = "pgbouncer_exporter";
        map["GeoSecondary"] = "geo_secondary";
        map["GeoPostgresql"] = "geo_postgresql";
        map["OmnibusGitconfig"] = "omnibus_gitconfig";

        return map;
    }
}
