using FrenchExDev.Net.Vos.Config;
using FrenchExDev.Net.Vos.Infra.Vagrant;

namespace FrenchExDev.Net.Vos.Tests;

public class VosBackendTests
{
    [Fact]
    public void VagrantBackend_Name()
    {
        new VagrantBackend().Name.ShouldBe("vagrant");
    }

    [Fact]
    public void VagrantBackend_SupportsAllActions()
    {
        var backend = new VagrantBackend();
        backend.SupportedActions.ShouldContain("up");
        backend.SupportedActions.ShouldContain("halt");
        backend.SupportedActions.ShouldContain("destroy");
        backend.SupportedActions.ShouldContain("reload");
        backend.SupportedActions.ShouldContain("provision");
        backend.SupportedActions.ShouldContain("status");
        backend.SupportedActions.ShouldContain("ssh");
        backend.SupportedActions.ShouldContain("suspend");
        backend.SupportedActions.ShouldContain("resume");
        backend.SupportedActions.ShouldContain("snapshot-save");
        backend.SupportedActions.ShouldContain("snapshot-restore");
    }
}
