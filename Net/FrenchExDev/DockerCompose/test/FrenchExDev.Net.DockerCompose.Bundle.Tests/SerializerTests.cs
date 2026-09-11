using Shouldly;

namespace FrenchExDev.Net.DockerCompose.Bundle.Tests;

public class ModelTests
{
    [Fact]
    public void ComposeFile_Services_CanBePopulated()
    {
        var file = new ComposeFile
        {
            Services = new Dictionary<string, ComposeService>
            {
                ["web"] = new ComposeService { Image = "nginx:latest" }
            }
        };

        file.Services.ShouldContainKey("web");
        file.Services["web"].Image.ShouldBe("nginx:latest");
    }

    [Fact]
    public void ComposeFile_AllTopLevelSections_CanBeSet()
    {
        var file = new ComposeFile
        {
            Name = "my-app",
            Services = new Dictionary<string, ComposeService>
            {
                ["web"] = new ComposeService { Image = "nginx" }
            },
            Networks = new Dictionary<string, ComposeNetwork?>
            {
                ["frontend"] = new ComposeNetwork { Driver = "bridge" }
            },
            Volumes = new Dictionary<string, ComposeVolume?>
            {
                ["data"] = new ComposeVolume { Driver = "local" }
            },
            Secrets = new Dictionary<string, ComposeSecret>
            {
                ["db_password"] = new ComposeSecret { File = "./secret.txt" }
            },
            Configs = new Dictionary<string, ComposeConfig>
            {
                ["app_config"] = new ComposeConfig { File = "./config.yaml" }
            }
        };

        file.Name.ShouldBe("my-app");
        file.Services.ShouldContainKey("web");
        file.Networks!["frontend"]!.Driver.ShouldBe("bridge");
        file.Volumes!["data"]!.Driver.ShouldBe("local");
        file.Secrets!["db_password"].File.ShouldBe("./secret.txt");
        file.Configs!["app_config"].File.ShouldBe("./config.yaml");
    }

    [Fact]
    public void ComposeFile_Extensions_CanBeSet()
    {
        var file = new ComposeFile
        {
            Extensions = new Dictionary<string, object?>
            {
                ["x-common"] = new Dictionary<string, object?> { ["image"] = "base" }
            }
        };

        file.Extensions.ShouldNotBeNull();
        file.Extensions.ShouldContainKey("x-common");
    }

    [Fact]
    public void ComposeService_Ports_CanBeConfigured()
    {
        var svc = new ComposeService
        {
            Image = "nginx",
            Ports = new List<ComposeServicePortsConfig>
            {
                new ComposeServicePortsConfig { Target = 80, Published = 8080, Protocol = "tcp" }
            }
        };

        svc.Ports.ShouldNotBeNull();
        svc.Ports!.Count.ShouldBe(1);
        svc.Ports[0].Target.ShouldBe(80);
        svc.Ports[0].Published.ShouldBe(8080);
        svc.Ports[0].Protocol.ShouldBe("tcp");
    }

    [Fact]
    public void ComposeService_Environment_CanBeSet()
    {
        var svc = new ComposeService
        {
            Image = "nginx",
            Environment = new Dictionary<string, string?>
            {
                ["FOO"] = "bar",
                ["BAZ"] = "qux"
            }
        };

        svc.Environment.ShouldNotBeNull();
        svc.Environment!["FOO"].ShouldBe("bar");
        svc.Environment["BAZ"].ShouldBe("qux");
    }

    [Fact]
    public void ComposeService_BuildConfig_CanBeSet()
    {
        var svc = new ComposeService
        {
            Build = new ComposeServiceBuildConfig
            {
                Context = ".",
                Dockerfile = "Dockerfile"
            }
        };

        svc.Build.ShouldNotBeNull();
        svc.Build!.Context.ShouldBe(".");
        svc.Build.Dockerfile.ShouldBe("Dockerfile");
    }

    [Fact]
    public void ComposeNetwork_Properties_CanBeSet()
    {
        var net = new ComposeNetwork
        {
            Name = "frontend",
            Driver = "bridge",
            Internal = true,
            Attachable = true
        };

        net.Name.ShouldBe("frontend");
        net.Driver.ShouldBe("bridge");
        net.Internal.ShouldBe(true);
        net.Attachable.ShouldBe(true);
    }

    [Fact]
    public void ComposeVolume_Properties_CanBeSet()
    {
        var vol = new ComposeVolume
        {
            Name = "data",
            Driver = "local",
            External = false
        };

        vol.Name.ShouldBe("data");
        vol.Driver.ShouldBe("local");
        vol.External.ShouldBe(false);
    }

    [Fact]
    public void ComposeSecret_Properties_CanBeSet()
    {
        var secret = new ComposeSecret
        {
            Name = "db_password",
            File = "./secret.txt"
        };

        secret.Name.ShouldBe("db_password");
        secret.File.ShouldBe("./secret.txt");
    }

    [Fact]
    public void ComposeConfig_Properties_CanBeSet()
    {
        var config = new ComposeConfig
        {
            Name = "app_config",
            File = "./config.yaml"
        };

        config.Name.ShouldBe("app_config");
        config.File.ShouldBe("./config.yaml");
    }

    [Fact]
    public void ComposeFile_NullableCollections_DefaultToNull()
    {
        var file = new ComposeFile();

        file.Services.ShouldBeNull();
        file.Networks.ShouldBeNull();
        file.Volumes.ShouldBeNull();
        file.Secrets.ShouldBeNull();
        file.Configs.ShouldBeNull();
        file.Extensions.ShouldBeNull();
    }

    [Fact]
    public void ComposeService_Healthcheck_CanBeConfigured()
    {
        var svc = new ComposeService
        {
            Image = "postgres:16",
            Healthcheck = new ComposeHealthcheck
            {
                Disable = false,
                Retries = 3
            }
        };

        svc.Healthcheck.ShouldNotBeNull();
        svc.Healthcheck!.Disable.ShouldBe(false);
        svc.Healthcheck.Retries.ShouldBe(3);
    }
}
