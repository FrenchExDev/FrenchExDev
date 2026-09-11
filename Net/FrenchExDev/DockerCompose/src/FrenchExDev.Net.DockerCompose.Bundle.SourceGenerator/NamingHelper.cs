using System.Text;

namespace FrenchExDev.Net.DockerCompose.Bundle.SourceGenerator;

internal static class NamingHelper
{
    public static string ToPascalCase(string snakeCase)
    {
        var sb = new StringBuilder();
        var capitalizeNext = true;
        foreach (var c in snakeCase)
        {
            if (c == '_' || c == '-' || c == '.')
            {
                capitalizeNext = true;
                continue;
            }
            sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
            capitalizeNext = false;
        }
        return sb.ToString();
    }

    public static string DefinitionToClassName(string definitionName)
    {
        var pascal = ToPascalCase(definitionName);
        return pascal switch
        {
            "Service" => "ComposeService",
            "Network" => "ComposeNetwork",
            "Volume" => "ComposeVolume",
            "Secret" => "ComposeSecret",
            "Config" => "ComposeConfig",
            "Model" => "ComposeModel",
            "Deployment" => "ComposeDeployment",
            "Development" => "ComposeDevelopment",
            "Healthcheck" => "ComposeHealthcheck",
            "ServiceHook" => "ComposeServiceHook",
            "BlkioLimit" => "ComposeBlkioLimit",
            "BlkioWeight" => "ComposeBlkioWeight",
            "GenericResources" => "ComposeGenericResources",
            "Devices" => "ComposeDevices",
            _ => "Compose" + pascal
        };
    }

    public static string MapCSharpType(PropertyModel prop)
    {
        return prop.Type switch
        {
            PropertyType.String => "string?",
            PropertyType.Integer => "int?",
            PropertyType.Number => "double?",
            PropertyType.Boolean => "bool?",
            PropertyType.StringOrBoolean => "bool?",
            PropertyType.StringOrInteger => "int?",
            PropertyType.InlineObject when prop.InlineClassName is not null =>
                $"{prop.InlineClassName}?",
            PropertyType.StringOrObject when prop.OneOfObjectClassName is not null =>
                $"{prop.OneOfObjectClassName}?",
            PropertyType.Array when prop.Items is not null => MapArrayItemType(prop.Items),
            PropertyType.Array => "global::System.Collections.Generic.List<object>?",
            PropertyType.Object => "global::System.Collections.Generic.Dictionary<string, object?>?",
            PropertyType.Ref when prop.Ref is not null => $"{DefinitionToClassName(prop.Ref)}?",
            PropertyType.ListOrDict => "global::System.Collections.Generic.Dictionary<string, string?>?",
            PropertyType.StringOrList => "global::System.Collections.Generic.List<string>?",
            PropertyType.StringOrObject => "object?",
            PropertyType.Command => "global::System.Collections.Generic.List<string>?",
            PropertyType.ServiceConfigOrSecret => "global::System.Collections.Generic.List<string>?",
            PropertyType.ExtraHosts => "global::System.Collections.Generic.List<string>?",
            PropertyType.Ulimits => "global::System.Collections.Generic.Dictionary<string, object?>?",
            _ => "object?"
        };
    }

    private static string MapArrayItemType(PropertyModel items)
    {
        if (items.Type == PropertyType.Ref && items.Ref is not null)
            return $"global::System.Collections.Generic.List<{DefinitionToClassName(items.Ref)}>?";
        if (items.Type == PropertyType.InlineObject && items.InlineClassName is not null)
            return $"global::System.Collections.Generic.List<{items.InlineClassName}>?";
        if (items.Type == PropertyType.String)
            return "global::System.Collections.Generic.List<string>?";
        if (items.Type == PropertyType.Integer || items.Type == PropertyType.StringOrInteger)
            return "global::System.Collections.Generic.List<int>?";
        if (items.Type == PropertyType.StringOrObject && items.OneOfObjectClassName is not null)
            return $"global::System.Collections.Generic.List<{items.OneOfObjectClassName}>?";

        return "global::System.Collections.Generic.List<object>?";
    }
}
