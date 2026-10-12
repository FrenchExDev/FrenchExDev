using System.Text.Json;
using FrenchExDev.Net.Result;
using Json.Schema;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ResultUnit = FrenchExDev.Net.Result.Result;

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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private static readonly Lazy<JsonSchema?> StaticSchema = new(
        () => LoadEmbeddedSchema("traefik-v3-static.json"));

    private static readonly Lazy<JsonSchema?> DynamicSchema = new(
        () => LoadEmbeddedSchema("traefik-v3-file-provider.json"));

    // ── Existing throwing API ──────────────────────────────────────────────

    public static T Deserialize<T>(string yaml) =>
        Deserializer.Deserialize<T>(yaml);

    public static TraefikStaticConfig DeserializeStatic(string yaml) =>
        Deserialize<TraefikStaticConfig>(yaml);

    public static TraefikDynamicConfig DeserializeDynamic(string yaml) =>
        Deserialize<TraefikDynamicConfig>(yaml);

    public static string Serialize<T>(T obj) =>
        Serializer.Serialize(obj!);

    // ── JSON I/O ───────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes a Traefik config to JSON. Traefik accepts both YAML and
    /// JSON for static and dynamic configs; JSON is occasionally easier to
    /// produce from non-.NET tooling chains.
    /// </summary>
    public static string SerializeJson<T>(T obj) =>
        JsonSerializer.Serialize(obj, JsonOptions);

    /// <summary>
    /// Deserializes a Traefik config from JSON.
    /// </summary>
    public static T DeserializeJson<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions)!;

    // ── Schema-validating Try* API ─────────────────────────────────────────

    /// <summary>
    /// Deserializes a Traefik static config from YAML and validates it against
    /// the embedded JSON schema. Returns failure if the schema rejects the input
    /// or if the YAML is malformed; the typed POCO is only returned on success.
    /// </summary>
    public static Result<TraefikStaticConfig> TryDeserializeStatic(string yaml)
        => TryDeserializeWithSchema<TraefikStaticConfig>(yaml, StaticSchema.Value);

    /// <summary>
    /// Deserializes a Traefik dynamic config from YAML and validates it against
    /// the embedded JSON schema.
    /// </summary>
    public static Result<TraefikDynamicConfig> TryDeserializeDynamic(string yaml)
        => TryDeserializeWithSchema<TraefikDynamicConfig>(yaml, DynamicSchema.Value);

    /// <summary>
    /// Serializes a Traefik static config to YAML, validating against the
    /// embedded JSON schema before returning. A serializer that produces an
    /// invalid Traefik config is a bug; this surfaces it at write time rather
    /// than at Traefik's startup.
    /// </summary>
    public static Result<string> TrySerializeStatic(TraefikStaticConfig config)
        => TrySerializeWithSchema(config, StaticSchema.Value);

    /// <summary>
    /// Serializes a Traefik dynamic config to YAML, validating against the
    /// embedded JSON schema before returning.
    /// </summary>
    public static Result<string> TrySerializeDynamic(TraefikDynamicConfig config)
        => TrySerializeWithSchema(config, DynamicSchema.Value);

    // ── Async / file I/O ───────────────────────────────────────────────────

    /// <summary>
    /// Reads a Traefik static config from a file, deserializes it from YAML,
    /// and validates it against the embedded JSON schema.
    /// </summary>
    public static async Task<Result<TraefikStaticConfig>> ReadStaticFromFileAsync(
        string path, CancellationToken ct = default)
    {
        try
        {
            var yaml = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return TryDeserializeStatic(yaml);
        }
        catch (System.Exception ex)
        {
            return Result<TraefikStaticConfig>.Failure(
                new System.ComponentModel.DataAnnotations.ValidationResult(
                    $"Failed to read '{path}': {ex.Message}"));
        }
    }

    /// <summary>
    /// Reads a Traefik dynamic config from a file, deserializes it from YAML,
    /// and validates it against the embedded JSON schema.
    /// </summary>
    public static async Task<Result<TraefikDynamicConfig>> ReadDynamicFromFileAsync(
        string path, CancellationToken ct = default)
    {
        try
        {
            var yaml = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return TryDeserializeDynamic(yaml);
        }
        catch (System.Exception ex)
        {
            return Result<TraefikDynamicConfig>.Failure(
                new System.ComponentModel.DataAnnotations.ValidationResult(
                    $"Failed to read '{path}': {ex.Message}"));
        }
    }

    /// <summary>
    /// Serializes a Traefik static config and writes it to <paramref name="path"/>
    /// atomically (via a sibling .tmp file + File.Replace/File.Move). The
    /// schema is validated before any bytes are written. This is the API the
    /// Traefik file provider use case actually wants — half-written configs
    /// during a watch cycle would otherwise crash Traefik.
    /// </summary>
    public static Task<ResultUnit> WriteStaticToFileAsync(
        string path, TraefikStaticConfig config, CancellationToken ct = default)
        => WriteToFileAsyncCore(path, config, StaticSchema.Value, ct);

    /// <summary>
    /// Serializes a Traefik dynamic config and writes it to <paramref name="path"/>
    /// atomically. See <see cref="WriteStaticToFileAsync"/> for semantics.
    /// </summary>
    public static Task<ResultUnit> WriteDynamicToFileAsync(
        string path, TraefikDynamicConfig config, CancellationToken ct = default)
        => WriteToFileAsyncCore(path, config, DynamicSchema.Value, ct);

    private static async Task<ResultUnit> WriteToFileAsyncCore<T>(
        string path, T config, JsonSchema? schema, CancellationToken ct) where T : notnull
    {
        if (schema is null)
        {
            return ResultUnit.Failure();
        }

        if (!TryValidateAgainstSchema(config, schema, out _))
        {
            return ResultUnit.Failure();
        }

        var yaml = Serializer.Serialize(config!);
        var tmpPath = path + ".tmp";

        try
        {
            // Write the temp file fully (and fsync via DisposeAsync) before
            // touching the destination, then atomically rename. File.Replace
            // exists on Windows + .NET; File.Move handles the no-target case.
            await File.WriteAllTextAsync(tmpPath, yaml, ct).ConfigureAwait(false);

            const int maxAttempts = 3;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (File.Exists(path))
                    {
                        File.Replace(tmpPath, path, destinationBackupFileName: null);
                    }
                    else
                    {
                        File.Move(tmpPath, path);
                    }
                    return ResultUnit.Success();
                }
                catch (IOException) when (attempt < maxAttempts - 1)
                {
                    await Task.Delay(50, ct).ConfigureAwait(false);
                }
            }

            return ResultUnit.Failure();
        }
        catch (System.Exception)
        {
            try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { /* best effort */ }
            return ResultUnit.Failure();
        }
    }

    // ── Internals ──────────────────────────────────────────────────────────

    private static Result<T> TryDeserializeWithSchema<T>(string yaml, JsonSchema? schema) where T : notnull
    {
        if (schema is null)
        {
            return Result<T>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult(
                "Embedded JSON schema could not be loaded."));
        }

        // Step 1: parse the YAML into a JsonNode tree that preserves the
        // *original* shape — including keys the typed deserializer would
        // silently drop because of IgnoreUnmatchedProperties. YamlToJson
        // honours YAML 1.2 core scalar resolution (true/false → bool,
        // 42 → int, 3.14 → float, etc.) so JsonSchema.Net sees real types.
        System.Text.Json.Nodes.JsonNode? node;
        try
        {
            node = YamlToJson.Parse(yaml);
        }
        catch (System.Exception ex)
        {
            return Result<T>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult(
                $"YAML parse failure: {ex.Message}"));
        }

        if (node is null)
        {
            return Result<T>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult(
                "YAML document is empty."));
        }

        // Step 2: schema-validate the original YAML shape. This catches
        // typo'd keys (`additionalProperties: false` in the schema) AND
        // type errors (string where bool expected, etc.).
        try
        {
            using var doc = JsonDocument.Parse(node.ToJsonString());
            var evaluation = schema.Evaluate(doc.RootElement, new EvaluationOptions
            {
                OutputFormat = OutputFormat.List,
            });
            if (!evaluation.IsValid)
            {
                return Result<T>.Failure(BuildValidationResult(evaluation));
            }
        }
        catch (System.Exception ex)
        {
            return Result<T>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult(
                $"Schema validation failed: {ex.Message}"));
        }

        // Step 3: only after the schema is happy, deserialize into the
        // typed POCO. The schema has already vetted the shape; this is
        // just the type projection.
        try
        {
            var typed = Deserializer.Deserialize<T>(yaml);
            return Result<T>.Success(typed);
        }
        catch (System.Exception ex)
        {
            return Result<T>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult(
                $"Typed deserialization failed after schema validation: {ex.Message}"));
        }
    }

    private static bool TryValidateAgainstSchema<T>(
        T value,
        JsonSchema schema,
        out System.ComponentModel.DataAnnotations.ValidationResult? failure)
    {
        try
        {
            var jsonString = JsonSerializer.Serialize(value, JsonOptions);
            using var doc = JsonDocument.Parse(jsonString);
            var evaluation = schema.Evaluate(doc.RootElement, new EvaluationOptions
            {
                OutputFormat = OutputFormat.List,
            });

            if (!evaluation.IsValid)
            {
                failure = BuildValidationResult(evaluation);
                return false;
            }

            failure = null;
            return true;
        }
        catch (System.Exception ex)
        {
            failure = new System.ComponentModel.DataAnnotations.ValidationResult(
                $"Schema validation failed: {ex.Message}");
            return false;
        }
    }

    private static Result<string> TrySerializeWithSchema<T>(T config, JsonSchema? schema) where T : notnull
    {
        if (schema is null)
        {
            return Result<string>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult(
                "Embedded JSON schema could not be loaded."));
        }

        if (!TryValidateAgainstSchema(config, schema, out var validationFailure))
        {
            return Result<string>.Failure(validationFailure!);
        }

        var yaml = Serializer.Serialize(config!);
        return Result<string>.Success(yaml);
    }

    private static System.ComponentModel.DataAnnotations.ValidationResult BuildValidationResult(
        EvaluationResults evaluation)
    {
        var messages = new List<string>();
        CollectErrors(evaluation, messages);
        var combined = messages.Count == 0
            ? "Schema validation failed (no error details available)."
            : string.Join("; ", messages);
        return new System.ComponentModel.DataAnnotations.ValidationResult(combined);
    }

    private static void CollectErrors(EvaluationResults node, List<string> sink)
    {
        if (node.Errors is not null)
        {
            foreach (var kv in node.Errors)
            {
                sink.Add($"{node.InstanceLocation}: {kv.Value}");
            }
        }
        if (node.Details is not null)
        {
            foreach (var child in node.Details)
            {
                if (!child.IsValid) CollectErrors(child, sink);
            }
        }
    }

    private static JsonSchema? LoadEmbeddedSchema(string fileName)
    {
        var asm = typeof(TraefikSerializer).Assembly;
        // Embedded resources are namespaced as
        // "FrenchExDev.Net.Traefik.Bundle.schemas.traefik-v3-static.json".
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, System.StringComparison.Ordinal));
        if (resourceName is null) return null;

        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return JsonSchema.FromText(json);
    }
}
