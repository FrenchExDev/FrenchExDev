using FrenchExDev.Net.Vos.VosFile;

namespace FrenchExDev.Net.Vos.VosFile.Tests;

public class VosSchemaVersionTests
{
    [Fact]
    public void Current_is_1()
    {
        VosSchemaVersion.Current.ShouldBe(1);
    }

    [Fact]
    public void Current_is_const_int()
    {
        // Verify it is a compile-time constant (usable in attributes, switch cases, etc.)
        const int version = VosSchemaVersion.Current;
        version.ShouldBe(1);
    }
}
