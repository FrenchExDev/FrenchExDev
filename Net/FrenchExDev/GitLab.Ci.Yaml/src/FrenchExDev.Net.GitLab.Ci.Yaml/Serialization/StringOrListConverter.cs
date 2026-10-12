using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace FrenchExDev.Net.GitLab.Ci.Yaml.Serialization;

/// <summary>
/// YamlDotNet type converter that handles properties accepting either a single string
/// or a list of strings (e.g., <c>script</c>, <c>before_script</c>, <c>after_script</c>).
/// When reading, a scalar string is wrapped into a single-element list.
/// When writing, a single-element list is emitted as-is (always as a list for consistency).
/// </summary>
public sealed class StringOrListConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(List<string>);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        if (parser.TryConsume<Scalar>(out var scalar))
        {
            // Single string → wrap in list
            return new List<string> { scalar.Value };
        }

        if (parser.TryConsume<SequenceStart>(out _))
        {
            var list = new List<string>();
            while (!parser.TryConsume<SequenceEnd>(out _))
            {
                if (parser.TryConsume<Scalar>(out var item))
                {
                    list.Add(item.Value);
                }
                else
                {
                    // Nested sequences (multi-line commands) — flatten to string representation
                    parser.SkipThisAndNestedEvents();
                }
            }
            return list;
        }

        // Null
        if (parser.TryConsume<NodeEvent>(out _))
            return null;

        return null;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value is not List<string> list)
        {
            emitter.Emit(new Scalar(null, null, "", ScalarStyle.Plain, true, false));
            return;
        }

        emitter.Emit(new SequenceStart(null, null, false, SequenceStyle.Block));
        foreach (var item in list)
            emitter.Emit(new Scalar(null, null, item, ScalarStyle.Any, true, false));
        emitter.Emit(new SequenceEnd());
    }
}
