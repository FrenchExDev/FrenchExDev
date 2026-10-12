using FrenchExDev.Net.BinaryWrapper.Testing;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Tests;

public sealed class CommandExecutorEnvironmentTests
{
    [Theory]
    [InlineData("run")]
    [InlineData("stream")]
    [InlineData("collect")]
    public async Task Execution_PassesMergedEnvironmentToInjectedRunner(string mode)
    {
        var bindingEnvironment = new Dictionary<string, string>
        {
            ["BINDING_ONLY"] = "default", ["SHARED"] = "binding"
        };
        var commandEnvironment = new Dictionary<string, string>
        {
            ["COMMAND_ONLY"] = "specific", ["SHARED"] = "command", ["EMPTY"] = ""
        };
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("dummy"), ExecutablePath = "dummy",
            EnvironmentVariables = bindingEnvironment
        };
        var runner = new FakeProcessRunner("EVENT:ok");
        var executor = new CommandExecutor(new DictionaryBinaryResolver([binding]), runner);
        var command = new DummyRunCommand { Environment = commandEnvironment };

        switch (mode)
        {
            case "run":
                (await executor.ExecuteAsync(binding.Identifier, command)).IsSuccess.ShouldBeTrue();
                break;
            case "stream":
                await foreach (var item in executor.StreamAsync(binding.Identifier, command, new TestOutputParser()))
                    item.ShouldNotBeNull();
                break;
            case "collect":
                (await executor.ExecuteAsync(binding.Identifier, command, new TestOutputParser(), new TestCollector()))
                    .IsSuccess.ShouldBeTrue();
                break;
        }

        var environment = runner.LastSpec.ShouldNotBeNull().EnvironmentVariables;
        environment.Count.ShouldBe(4);
        environment["BINDING_ONLY"].ShouldBe("default");
        environment["COMMAND_ONLY"].ShouldBe("specific");
        environment["SHARED"].ShouldBe("command");
        environment["EMPTY"].ShouldBe("");
        bindingEnvironment["SHARED"].ShouldBe("binding");
        bindingEnvironment.Count.ShouldBe(2);
        commandEnvironment.Count.ShouldBe(3);

        bindingEnvironment["BINDING_ONLY"] = "changed";
        commandEnvironment["COMMAND_ONLY"] = "changed";
        environment["BINDING_ONLY"].ShouldBe("default");
        environment["COMMAND_ONLY"].ShouldBe("specific");
    }

    [Fact]
    public async Task Execution_UsesPlatformEnvironmentNameComparison()
    {
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("dummy"), ExecutablePath = "dummy",
            EnvironmentVariables = new Dictionary<string, string> { ["APP_MODE"] = "binding" }
        };
        var runner = new FakeProcessRunner();
        var executor = new CommandExecutor(new DictionaryBinaryResolver([binding]), runner);
        var command = new DummyRunCommand
        {
            Environment = new Dictionary<string, string> { ["app_mode"] = "command" }
        };

        await executor.ExecuteAsync(binding.Identifier, command);

        var environment = runner.LastSpec.ShouldNotBeNull().EnvironmentVariables;
        environment["app_mode"].ShouldBe("command");
        environment["APP_MODE"].ShouldBe(OperatingSystem.IsWindows() ? "command" : "binding");
        environment.Count.ShouldBe(OperatingSystem.IsWindows() ? 1 : 2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execution_SystemRunner_PassesEnvironmentToChildProcess(bool streaming)
    {
        const string key = "BINARYWRAPPER_ENVIRONMENT_TEST";
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("shell"),
            ExecutablePath = OperatingSystem.IsWindows() ? "cmd" : "/bin/sh",
            EnvironmentVariables = new Dictionary<string, string> { [key] = "binding" }
        };
        var command = new FakeCommand
        {
            CommandPath = [],
            Args = OperatingSystem.IsWindows()
                ? ["/d", "/c", "echo EVENT:%BINARYWRAPPER_ENVIRONMENT_TEST%"]
                : ["-c", "printf 'EVENT:%s\\n' \"$BINARYWRAPPER_ENVIRONMENT_TEST\""],
            Environment = new Dictionary<string, string> { [key] = "command" }
        };
        var executor = new CommandExecutor(new DictionaryBinaryResolver([binding]), new SystemProcessRunner());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        if (streaming)
        {
            var values = new List<string>();
            await foreach (var item in executor.StreamAsync(binding.Identifier, command, new TestOutputParser(), timeout.Token))
                values.Add(item.Value.Trim());
            values.ShouldBe(new[] { "command", "EXIT:0" });
        }
        else
        {
            var result = await executor.ExecuteAsync(binding.Identifier, command, timeout.Token);
            result.IsSuccess.ShouldBeTrue();
            result.Value!.StandardOutput.Trim().ShouldBe("EVENT:command");
        }
    }

    [Fact]
    public async Task GeneratedBuilder_Environment_ReachesRunner()
    {
        var binding = TestBindings.Create("dummy");
        var client = new DummyClient(binding);
        var command = await client.RunAsync(b => b.WithEnvironment(
            new Dictionary<string, string> { ["APP_MODE"] = "builder" }));
        var runner = new FakeProcessRunner();
        var executor = new CommandExecutor(new DictionaryBinaryResolver([binding]), runner);

        await executor.ExecuteAsync(binding.Identifier, command);

        runner.LastSpec.ShouldNotBeNull().EnvironmentVariables["APP_MODE"].ShouldBe("builder");
        (await client.RunAsync(b => { })).Environment.ShouldBeEmpty();
        (await client.RunAsync(b => b.WithEnvironment(null))).Environment.ShouldBeEmpty();
    }
}
