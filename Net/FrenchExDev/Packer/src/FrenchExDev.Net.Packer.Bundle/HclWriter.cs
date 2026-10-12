using System.CodeDom.Compiler;

namespace FrenchExDev.Net.Packer.Bundle;

/// <summary>
/// Emits HCL2 native syntax (.pkr.hcl). Wraps an <see cref="IndentedTextWriter"/>
/// to produce properly indented blocks, arguments, and expressions.
/// </summary>
public sealed class HclWriter : IDisposable
{
    private readonly IndentedTextWriter _writer;
    private bool _disposed;

    public HclWriter(TextWriter writer)
    {
        _writer = new IndentedTextWriter(writer, "  ");
    }

    /// <summary>Opens a labeled block: <c>source "virtualbox-iso" "alpine" {</c></summary>
    public IDisposable Block(string type, params string[] labels)
    {
        var parts = new List<string> { type };
        foreach (var label in labels)
            parts.Add($"\"{label}\"");

        _writer.Write(string.Join(" ", parts));
        _writer.WriteLine(" {");
        _writer.Indent++;
        return new BlockScope(this);
    }

    /// <summary>Writes a string argument: <c>key = "value"</c></summary>
    public void Argument(string name, string? value)
    {
        if (value is null) return;
        _writer.WriteLine($"{name} = \"{EscapeString(value)}\"");
    }

    /// <summary>Writes an integer argument: <c>key = 42</c></summary>
    public void Argument(string name, int? value)
    {
        if (value is null) return;
        _writer.WriteLine($"{name} = {value.Value}");
    }

    /// <summary>Writes a uint argument: <c>key = 20480</c></summary>
    public void Argument(string name, uint? value)
    {
        if (value is null) return;
        _writer.WriteLine($"{name} = {value.Value}");
    }

    /// <summary>Writes a boolean argument: <c>key = true</c></summary>
    public void Argument(string name, bool? value)
    {
        if (value is null) return;
        _writer.WriteLine($"{name} = {(value.Value ? "true" : "false")}");
    }

    /// <summary>Writes a string list argument: <c>key = ["a", "b"]</c></summary>
    public void ArgumentList(string name, IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0) return;

        if (values.Count == 1)
        {
            _writer.WriteLine($"{name} = [\"{EscapeString(values[0])}\"]");
            return;
        }

        _writer.WriteLine($"{name} = [");
        _writer.Indent++;
        for (var i = 0; i < values.Count; i++)
        {
            var comma = i < values.Count - 1 ? "," : "";
            _writer.WriteLine($"\"{EscapeString(values[i])}\"{comma}");
        }
        _writer.Indent--;
        _writer.WriteLine("]");
    }

    /// <summary>Writes a string map argument: <c>key = { k1 = "v1" }</c></summary>
    public void ArgumentMap(string name, IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0) return;

        _writer.WriteLine($"{name} = {{");
        _writer.Indent++;
        foreach (var kvp in values)
            _writer.WriteLine($"{kvp.Key} = \"{EscapeString(kvp.Value)}\"");
        _writer.Indent--;
        _writer.WriteLine("}");
    }

    /// <summary>Writes a list-of-lists argument (e.g. vboxmanage): <c>key = [["a","b"], ["c","d"]]</c></summary>
    public void ArgumentListOfLists(string name, IReadOnlyList<IReadOnlyList<string>>? lists)
    {
        if (lists is null || lists.Count == 0) return;

        _writer.WriteLine($"{name} = [");
        _writer.Indent++;
        for (var i = 0; i < lists.Count; i++)
        {
            var items = lists[i];
            var joined = string.Join(", ", items.Select(s => $"\"{EscapeString(s)}\""));
            var comma = i < lists.Count - 1 ? "," : "";
            _writer.WriteLine($"[{joined}]{comma}");
        }
        _writer.Indent--;
        _writer.WriteLine("]");
    }

    /// <summary>Writes a raw HCL expression (not quoted): <c>key = var.name</c></summary>
    public void Expression(string name, string expression)
    {
        _writer.WriteLine($"{name} = {expression}");
    }

    /// <summary>Writes a single-line comment: <c>// text</c></summary>
    public void Comment(string text)
    {
        _writer.WriteLine($"// {text}");
    }

    /// <summary>Writes a blank line for readability.</summary>
    public void BlankLine()
    {
        _writer.WriteLine();
    }

    /// <summary>Writes raw text (for edge cases).</summary>
    public void Raw(string text)
    {
        _writer.Write(text);
    }

    /// <summary>Writes raw text followed by a newline.</summary>
    public void RawLine(string text)
    {
        _writer.WriteLine(text);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _writer.Flush();
        _writer.Dispose();
    }

    private static string EscapeString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private void CloseBlock()
    {
        _writer.Indent--;
        _writer.WriteLine("}");
    }

    private sealed class BlockScope : IDisposable
    {
        private readonly HclWriter _writer;
        private bool _disposed;

        public BlockScope(HclWriter writer) => _writer = writer;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _writer.CloseBlock();
        }
    }
}
