using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FrenchExDev.Net.Traefik.Bundle;

public static class TraefikSerializer
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public static T Deserialize<T>(string yaml) =>
        Deserializer.Deserialize<T>(yaml);

    public static TraefikStaticConfig DeserializeStatic(string yaml) =>
        Deserialize<TraefikStaticConfig>(yaml);

    public static TraefikDynamicConfig DeserializeDynamic(string yaml) =>
        Deserialize<TraefikDynamicConfig>(yaml);

    public static string Serialize<T>(T obj) =>
        Serializer.Serialize(obj!);
}
