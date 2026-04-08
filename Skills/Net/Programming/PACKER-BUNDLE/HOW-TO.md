# PACKER-BUNDLE — How-To

## Compose a bundle in code

```csharp
var bundle = new PackerBundle();

// Required plugins
bundle.Config.WithRequiredPlugin("virtualbox", "github.com/hashicorp/virtualbox", "~> 1");

// Variables
bundle.Variables.Add(
    new PackerVariableBuilder()
        .WithName("alpine_version")
        .WithType("string")
        .WithDefault("3.21")
        .Build().Value);

// Source
var source = new VirtualBoxIsoSourceBuilder()
    .WithName("alpine")
    .WithIsoUrl("https://dl-cdn.alpinelinux.org/.../alpine-virt-3.21-x86_64.iso")
    .WithSshUsername("root")
    .WithBootCommand(["root<enter>"])
    .Build().Value;
bundle.Sources.Add(source);

// Build
bundle.Build
    .WithSources(["source.virtualbox-iso.alpine"])
    .WithProvisioner("shell", p => p.WithScript("scripts/install.sh"));

// Provisioning script as a companion file
bundle.AddFile("scripts", "install", "sh", """
#!/bin/sh
set -eux
apk update
apk add docker
""");

new PackerBundleWriter().Write(bundle, "/tmp/alpine-template");
```

Then run `packer init` and `packer build` against the directory:

```bash
cd /tmp/alpine-template
packer init .
packer validate .
packer build .
```

## Use contributors for composition

Most real bundles are assembled from contributors:

```csharp
var bundle = new PackerBundle()
    .Apply(new VirtualBoxBaseContributor())
    .Apply(new AlpineIsoContributor("3.21"))
    .Apply(new DockerHostContributor());
```

Each contributor is responsible for one concern.

## Write a contributor

```csharp
public sealed class DockerHostContributor : IPackerBundleContributor
{
    public void Contribute(PackerBundle bundle)
    {
        bundle.AddFile("scripts", "install-docker", "sh", """
#!/bin/sh
set -eux
apk add docker docker-cli-compose
rc-update add docker
service docker start
""");

        bundle.Build.WithProvisioner("shell", p => p
            .WithScript("scripts/install-docker.sh"));

        bundle.EnvTemplate.Set("DOCKER_HOST_TYPE", "");
        bundle.EnvValues.Set("DOCKER_HOST_TYPE", "alpine");
    }
}
```

## Bump plugin scrapes

When a Packer plugin adds a new field upstream:

```bash
export GITHUB_TOKEN=ghp_xxx   # 5000 req/hr instead of 60
dotnet run --project Packer/src/FrenchExDev.Net.Packer.Bundle.Design
```

The Design project iterates `PluginRegistry`, fetches every `.hcl2spec.go`,
parses each, and writes `scrape/{plugin-type}.json` files into
`Bundle/scrape/`. Commit the result.

A subsequent build of `Bundle/` re-runs the SG and the new field appears on
the C# record + builder.

## Add a new plugin to the registry

1. Open `Bundle.Design/PluginRegistry.cs`.
2. Add a tuple `(org, repo, paths[])` for the plugin's repository.
3. Run the scraper.
4. Verify `scrape/{type}.json` was produced and commit it.
5. Build the consumer to see the new generated types.

Example:

```csharp
new PluginEntry("hashicorp", "packer-plugin-newcloud", new[]
{
    "builder/newcloud/iso/builder.hcl2spec.go",
    "builder/newcloud/vm/config.hcl2spec.go",
})
```

## Use the HclWriter directly

For ad-hoc HCL2 emission outside the bundle:

```csharp
using var sw = new StringWriter();
using (var w = new HclWriter(sw))
{
    using (w.Block("source", "virtualbox-iso", "alpine"))
    {
        w.Argument("iso_url",      "https://example.com/alpine.iso");
        w.Argument("ssh_username", "root");
        w.Argument("memory",       2048);
        w.Argument("headless",     true);
        w.Argument("boot_command", new[] { "root<enter>" });
        w.ArgumentListOfLists("vboxmanage", new[]
        {
            new[] { "modifyvm", "{{.Name}}", "--vram", "16" },
        });
        w.Expression("ssh_timeout", "var.ssh_timeout");
    }
}
Console.WriteLine(sw.ToString());
```

## Read existing HCL2 (rare)

If you absolutely must read existing `.pkr.hcl` files (e.g. for migration
tooling), shell out to `hcl2json`:

```bash
hcl2json file.pkr.hcl > file.json
```

Then parse the JSON. There is no native C# HCL2 parser in the bundle library
because we do not need one for the primary use case.

## Test a contributor

```csharp
[Fact]
public void DockerHostContributor_AddsInstallScript()
{
    var bundle = new PackerBundle().Apply(new DockerHostContributor());

    bundle.Files.Should().ContainKey("scripts/install-docker.sh");
    bundle.Files["scripts/install-docker.sh"].Content
        .Should().Contain("apk add docker");
}
```

## Test the writer

```csharp
[Fact]
public void Writer_EmitsExpectedFiles()
{
    var bundle = new PackerBundle();
    bundle.Variables.Add(new PackerVariableBuilder().WithName("v").WithType("string").Build().Value);

    var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    new PackerBundleWriter().Write(bundle, dir);

    File.Exists(Path.Combine(dir, "variables.pkr.hcl")).Should().BeTrue();
    File.ReadAllText(Path.Combine(dir, "variables.pkr.hcl"))
        .Should().Contain("variable \"v\"");
}
```

## Things to never do

- Never write JSON Packer templates. HCL2 only.
- Never edit a `scrape/*.json` by hand. Re-run the Design project.
- Never edit a `.g.cs` file. Re-run the build.
- Never make `PackerBundle` a record or serializable — it is meant to be
  mutated.
- Never call the Design project from the build pipeline. It hits GitHub.
- Never add a hand-written record for a plugin type. Add it to
  `PluginRegistry` and let the scraper pick it up.
- Never bypass `HclWriter` to emit raw HCL2 strings — escaping rules will
  bite you.
