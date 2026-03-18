using FrenchExDev.Net.DockerCompose.Bundle.Serialization;
using Shouldly;

namespace FrenchExDev.Net.DockerCompose.Bundle.Tests;

public class SerializerTests
{
    [Fact]
    public void Deserialize_MinimalYaml_Succeeds()
    {
        var yaml = "services:\n  web:\n    image: nginx:latest\n";
        var result = ComposeSerializer.Deserialize(yaml);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Services.ShouldContainKey("web");
        result.Value.Services["web"].Image.ShouldBe("nginx:latest");
    }

    [Fact]
    public void Deserialize_EmptyYaml_ReturnsEmptyComposeFile()
    {
        var result = ComposeSerializer.Deserialize("");
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Services.ShouldBeEmpty();
    }

    [Fact]
    public void Deserialize_InvalidYaml_ReturnsFailure()
    {
        var result = ComposeSerializer.Deserialize("{{invalid yaml");
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
    }

    [Fact]
    public void Deserialize_Ports_ShortSyntax()
    {
        var yaml = "services:\n  web:\n    image: nginx\n    ports:\n      - \"8080:80\"\n";
        var result = ComposeSerializer.Deserialize(yaml);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Services["web"].Ports.ShouldNotBeNull();
        result.Value.Services["web"].Ports!.Count.ShouldBe(1);
        result.Value.Services["web"].Ports![0].Published.ShouldBe("8080:80");
    }

    [Fact]
    public void Deserialize_Ports_LongSyntax()
    {
        var yaml = """
            services:
              web:
                image: nginx
                ports:
                  - target: 80
                    published: "8080"
                    protocol: tcp
            """;
        var result = ComposeSerializer.Deserialize(yaml);

        result.IsSuccess.ShouldBeTrue();
        var port = result.Value!.Services["web"].Ports![0];
        port.Target.ShouldBe(80);
        port.Published.ShouldBe("8080");
        port.Protocol.ShouldBe("tcp");
    }

    [Fact]
    public void Deserialize_Environment_AsMap()
    {
        var yaml = "services:\n  web:\n    image: nginx\n    environment:\n      FOO: bar\n      BAZ: qux\n";
        var result = ComposeSerializer.Deserialize(yaml);

        result.IsSuccess.ShouldBeTrue();
        var env = result.Value!.Services["web"].Environment!;
        env.IsDictionary.ShouldBeTrue();
        env.ToDictionary()["FOO"].ShouldBe("bar");
    }

    [Fact]
    public void Deserialize_Environment_AsList()
    {
        var yaml = "services:\n  web:\n    image: nginx\n    environment:\n      - FOO=bar\n      - BAZ=qux\n";
        var result = ComposeSerializer.Deserialize(yaml);

        result.IsSuccess.ShouldBeTrue();
        var env = result.Value!.Services["web"].Environment!;
        env.IsList.ShouldBeTrue();
        env.ToDictionary()["FOO"].ShouldBe("bar");
    }

    [Fact]
    public void Deserialize_DependsOn_AsList()
    {
        var yaml = "services:\n  web:\n    image: nginx\n    depends_on:\n      - db\n      - redis\n";
        var result = ComposeSerializer.Deserialize(yaml);

        result.IsSuccess.ShouldBeTrue();
        var deps = result.Value!.Services["web"].DependsOn!;
        deps.IsList.ShouldBeTrue();
        deps.List.ShouldContain("db");
        deps.List.ShouldContain("redis");
    }

    [Fact]
    public void Deserialize_DependsOn_AsMap()
    {
        var yaml = """
            services:
              web:
                image: nginx
                depends_on:
                  db:
                    condition: service_healthy
            """;
        var result = ComposeSerializer.Deserialize(yaml);

        result.IsSuccess.ShouldBeTrue();
        var deps = result.Value!.Services["web"].DependsOn!;
        deps.IsList.ShouldBeFalse();
        deps.Map["db"].Condition.ShouldBe("service_healthy");
    }

    [Fact]
    public void Deserialize_Build_AsString()
    {
        var yaml = "services:\n  web:\n    build: ./app\n";
        var result = ComposeSerializer.Deserialize(yaml);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Services["web"].Build!.IsString.ShouldBeTrue();
        result.Value.Services["web"].Build!.StringValue.ShouldBe("./app");
    }

    [Fact]
    public void Deserialize_Build_AsObject()
    {
        var yaml = """
            services:
              web:
                build:
                  context: .
                  dockerfile: Dockerfile
            """;
        var result = ComposeSerializer.Deserialize(yaml);

        result.IsSuccess.ShouldBeTrue();
        var build = result.Value!.Services["web"].Build!;
        build.IsObject.ShouldBeTrue();
        build.ObjectValue.Context.ShouldBe(".");
        build.ObjectValue.Dockerfile.ShouldBe("Dockerfile");
    }

    [Fact]
    public void Deserialize_Networks_Volumes_Secrets_Configs()
    {
        var yaml = """
            services:
              web:
                image: nginx
            networks:
              frontend:
                driver: bridge
            volumes:
              data:
            secrets:
              db_password:
                file: ./secret.txt
            configs:
              app_config:
                file: ./config.yaml
            """;
        var result = ComposeSerializer.Deserialize(yaml);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Networks!.ShouldContainKey("frontend");
        result.Value.Networks["frontend"].Driver.ShouldBe("bridge");
        result.Value.Volumes!.ShouldContainKey("data");
        result.Value.Secrets!.ShouldContainKey("db_password");
        result.Value.Secrets["db_password"].File.ShouldBe("./secret.txt");
        result.Value.Configs!.ShouldContainKey("app_config");
    }

    [Fact]
    public void Deserialize_ExtensionFields_Preserved()
    {
        var yaml = "x-common:\n  image: base\nservices:\n  web:\n    image: nginx\n";
        var result = ComposeSerializer.Deserialize(yaml);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Extensions.ShouldNotBeNull();
        result.Value.Extensions!.ShouldContainKey("x-common");
    }

    [Fact]
    public void Serialize_MinimalFile_ProducesValidYaml()
    {
        var file = new Model.ComposeFile();
        file.Services["web"] = new Model.Service { Image = "nginx:latest" };

        var yaml = ComposeSerializer.Serialize(file);

        yaml.ShouldContain("services");
        yaml.ShouldContain("nginx:latest");
    }

    [Fact]
    public void RoundTrip_MinimalYaml_Preserves()
    {
        var yaml = "services:\n  web:\n    image: nginx:latest\n";
        var result = ComposeSerializer.Deserialize(yaml);
        result.IsSuccess.ShouldBeTrue();

        var serialized = ComposeSerializer.Serialize(result.Value!);
        var result2 = ComposeSerializer.Deserialize(serialized);
        result2.IsSuccess.ShouldBeTrue();

        result2.Value!.Services["web"].Image.ShouldBe("nginx:latest");
    }
}
