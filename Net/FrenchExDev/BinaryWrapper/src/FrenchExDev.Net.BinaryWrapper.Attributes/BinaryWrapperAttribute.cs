namespace FrenchExDev.Net.BinaryWrapper.Attributes;

/// <summary>
/// Marks a partial class as a binary wrapper descriptor.
/// The BinaryWrapper source generator reads this attribute and the associated
/// JSON help files to generate typed command classes, builders, and a client.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class BinaryWrapperAttribute : Attribute
{
    /// <summary>The binary name (e.g., "packer", "podman").</summary>
    public string BinaryName { get; }

    /// <summary>
    /// Creates a new BinaryWrapper descriptor for the specified binary.
    /// </summary>
    /// <param name="binaryName">The binary name used to match AdditionalFiles (e.g., "packer" matches "packer-*.json").</param>
    public BinaryWrapperAttribute(string binaryName)
    {
        BinaryName = binaryName;
    }

    /// <summary>
    /// The prefix for flags (e.g., "--" for GNU-style, "-" for Go-style).
    /// Default: "--"
    /// </summary>
    public string FlagPrefix { get; set; } = "--";

    /// <summary>
    /// The separator between a flag name and its value (e.g., " " or "=").
    /// Default: " "
    /// </summary>
    public string FlagValueSeparator { get; set; } = " ";

    /// <summary>
    /// When true, boolean flags are serialized as "-flag=true"/"-flag=false"
    /// instead of just "-flag" (presence/absence).
    /// Default: false
    /// </summary>
    public bool UseBoolEqualsFormat { get; set; }
}
