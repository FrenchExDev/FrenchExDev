using FrenchExDev.Net.BinaryWrapper.Attributes;

namespace FrenchExDev.Net.Dotnet;

[BinaryWrapper("dotnet", FlagPrefix = "--", FlagValueSeparator = " ", UseBoolEqualsFormat = false)]
public partial class DotnetDescriptor;
