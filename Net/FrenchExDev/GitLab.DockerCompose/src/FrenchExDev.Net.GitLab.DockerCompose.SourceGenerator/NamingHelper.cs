using System;
using System.Text;

namespace FrenchExDev.Net.GitLab.DockerCompose.SourceGenerator;

internal static class NamingHelper
{
    /// <summary>Convert Ruby snake_case to C# PascalCase.
    /// "listen_port" → "ListenPort", "smtp_enable_starttls_auto" → "SmtpEnableStarttlsAuto"</summary>
    public static string ToPascalCase(string snakeCase)
    {
        var sb = new StringBuilder();
        bool capitalizeNext = true;

        for (int i = 0; i < snakeCase.Length; i++)
        {
            var c = snakeCase[i];
            if (c == '_' || c == '-' || c == '.')
            {
                capitalizeNext = true;
                continue;
            }

            // Skip non-alphanumeric chars
            if (!char.IsLetterOrDigit(c)) continue;

            sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
            capitalizeNext = false;
        }

        var result = sb.ToString();

        // C# identifiers cannot start with a digit
        if (result.Length > 0 && char.IsDigit(result[0]))
            result = "_" + result;

        return result;
    }

    /// <summary>Convert Ruby prefix to C# class name.
    /// "nginx" → "NginxConfig", "gitlab_rails" → "GitLabRailsConfig"</summary>
    public static string PrefixToClassName(string prefix)
    {
        // Special cases for readability
        switch (prefix)
        {
            case "gitlab_rails": return "GitLabRailsConfig";
            case "gitlab_workhorse": return "GitLabWorkhorseConfig";
            case "gitlab_shell": return "GitLabShellConfig";
            case "gitlab_sshd": return "GitLabSshdConfig";
            case "gitlab_kas": return "GitLabKasConfig";
            case "gitlab_pages": return "GitLabPagesConfig";
            case "gitlab_exporter": return "GitLabExporterConfig";
            case "gitlab_ci": return "GitLabCiConfig";
            case "gitlab_backup_cli": return "GitLabBackupCliConfig";
            case "geo_secondary": return "GeoSecondaryConfig";
            case "geo_postgresql": return "GeoPostgresqlConfig";
            case "geo_logcursor": return "GeoLogcursorConfig";
            case "node_exporter": return "NodeExporterConfig";
            case "redis_exporter": return "RedisExporterConfig";
            case "postgres_exporter": return "PostgresExporterConfig";
            case "pgbouncer_exporter": return "PgbouncerExporterConfig";
            case "web_server": return "WebServerConfig";
            case "pages_nginx": return "PagesNginxConfig";
            case "registry_nginx": return "RegistryNginxConfig";
            case "mattermost_nginx": return "MattermostNginxConfig";
            case "manage_accounts": return "ManageAccountsConfig";
            case "manage_storage_directories": return "ManageStorageDirectoriesConfig";
            case "storage_check": return "StorageCheckConfig";
            case "redis_master_role": return "RedisMasterRoleConfig";
            case "redis_replica_role": return "RedisReplicaRoleConfig";
            case "redis_sentinel_role": return "RedisSentinelRoleConfig";
            case "monitoring_role": return "MonitoringRoleConfig";
            case "prometheus_monitoring": return "PrometheusMonitoringConfig";
            case "high_availability": return "HighAvailabilityConfig";
            case "gitaly_client": return "GitalyClientConfig";
            default: return ToPascalCase(prefix) + "Config";
        }
    }

    /// <summary>Map a GitLabRbValueType to a C# type string.</summary>
    public static string MapCSharpType(GitLabRbValueType valueType)
    {
        switch (valueType)
        {
            case GitLabRbValueType.String: return "string?";
            case GitLabRbValueType.Integer: return "int?";
            case GitLabRbValueType.Long: return "long?";
            case GitLabRbValueType.Boolean: return "bool?";
            case GitLabRbValueType.Float: return "double?";
            case GitLabRbValueType.StringList:
                return "global::System.Collections.Generic.List<string>?";
            case GitLabRbValueType.FloatList:
                return "global::System.Collections.Generic.List<double>?";
            case GitLabRbValueType.StringDict:
                return "global::System.Collections.Generic.Dictionary<string, string?>?";
            case GitLabRbValueType.Nil: return "string?";
            default: return "string?";
        }
    }

    internal static int CompareVersions(string a, string b)
    {
        var partsA = a.Split('.');
        var partsB = b.Split('.');
        for (var i = 0; i < Math.Max(partsA.Length, partsB.Length); i++)
        {
            var va = i < partsA.Length && int.TryParse(partsA[i], out var ia) ? ia : 0;
            var vb = i < partsB.Length && int.TryParse(partsB[i], out var ib) ? ib : 0;
            if (va != vb) return va.CompareTo(vb);
        }
        return 0;
    }
}
