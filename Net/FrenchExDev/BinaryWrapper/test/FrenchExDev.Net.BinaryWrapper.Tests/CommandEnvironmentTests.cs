using FrenchExDev.Net.BinaryWrapper.Testing;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Tests;

public sealed class CommandEnvironmentTests
{
    [Fact]
    public void GeneratedCommand_DefaultEnvironment_IsEmptyThroughInterface()
    {
        ICliCommand command = new DummyRunCommand();

        command.Environment.ShouldBeEmpty();
    }

    [Fact]
    public void GeneratedCommand_CustomEnvironment_DoesNotBecomeCliArguments()
    {
        ICliCommand command = new DummyRunCommand
        {
            Environment = new Dictionary<string, string> { ["APP_MODE"] = "test" },
            Name = "task"
        };

        command.Environment["APP_MODE"].ShouldBe("test");
        command.ToArguments().ShouldBe(new[] { "--name", "task" });
        ((ICliCommand)new DummyRunCommand()).Environment.ShouldBeEmpty();
    }

    [Fact]
    public void FakeCommand_DefaultEnvironment_IsEmptyThroughInterface()
    {
        ICliCommand command = new FakeCommand();

        command.Environment.ShouldBeEmpty();
    }
}
