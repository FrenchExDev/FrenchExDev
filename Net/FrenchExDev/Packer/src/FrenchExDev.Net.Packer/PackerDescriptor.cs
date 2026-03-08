using FrenchExDev.Net.BinaryWrapper.Attributes;

namespace FrenchExDev.Net.Packer;

[BinaryWrapper("packer", FlagPrefix = "-", FlagValueSeparator = "=", UseBoolEqualsFormat = true)]
public partial class PackerDescriptor;
