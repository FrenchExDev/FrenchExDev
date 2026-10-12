#requires -Version 7.0
<#
.SYNOPSIS
Creates a BinaryWrapper consumer solution beside BinaryWrapper.
.DESCRIPTION
Generates runtime, Design and xUnit projects, a .slnx, configuration and a README.
Uses the current checkout's project references and central package versions.
Copies BinaryWrapper/.env verbatim to the solution root when that source file exists.
Does not run dotnet, scrape a binary or overwrite an existing directory.
.PARAMETER Binary
Solution name, for example Podman or GitLab.Cli. Namespace segments are capitalized.
.PARAMETER CommandName
Executable name used by BinaryWrapper. Defaults to Binary in lowercase.
.PARAMETER OutputDirectory
Parent directory in which the Binary directory will be created.
Defaults to Net/FrenchExDev, resolved from this script, independently of the working directory.
.PARAMETER Parser
HelpParsers strategy. Auto selects known consumers' parsers, otherwise standard.
.PARAMETER Framework
Target framework(s). Defaults to those of the BinaryWrapper runtime in this checkout.
.EXAMPLE
./BinaryWrapper/scripts/New-BinaryWrapperSolution.ps1 Podman
.EXAMPLE
./BinaryWrapper/scripts/New-BinaryWrapperSolution.ps1 GitLab.Cli -CommandName glab
.EXAMPLE
./BinaryWrapper/scripts/New-BinaryWrapperSolution.ps1 MyTool -CommandName my-tool -Parser cobra -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)*$')]
    [string] $Binary,

    [ValidatePattern('^[A-Za-z][A-Za-z0-9]*([._-][A-Za-z0-9]+)*$')]
    [string] $CommandName,

    [ValidateNotNullOrEmpty()]
    [string] $OutputDirectory = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent),

    [ValidateSet('auto', 'standard', 'cobra', 'argparse', 'gh', 'packer', 'vagrant')]
    [string] $Parser = 'auto',

    [ValidatePattern('^--?[A-Za-z][A-Za-z0-9-]*$')]
    [string] $HelpFlag,

    [ValidateSet('-', '--', '/')]
    [string] $FlagPrefix,

    [ValidateSet(' ', '=')]
    [string] $FlagValueSeparator,

    [switch] $UseBoolEqualsFormat,

    [ValidatePattern('^net[0-9]+\.[0-9]+$')]
    [string[]] $Framework
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$Binary = ($Binary.Split('.') | ForEach-Object {
    $_.Substring(0, 1).ToUpperInvariant() + $_.Substring(1)
}) -join '.'
if ($Binary -match '^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(\.|$)') {
    throw "Reserved directory name: $Binary"
}
if (-not $CommandName) { $CommandName = $Binary.ToLowerInvariant() }
if ($Parser -eq 'auto') {
    $Parser = switch ($CommandName) {
        { $_ -in 'podman', 'docker', 'docker-compose' } { 'cobra'; break }
        'podman-compose' { 'argparse'; break }
        { $_ -in 'gh', 'glab' } { 'gh'; break }
        'packer' { 'packer'; break }
        'vagrant' { 'vagrant'; break }
        default { 'standard' }
    }
}
if (-not $HelpFlag) { $HelpFlag = if ($Parser -in 'packer', 'vagrant') { '-h' } else { '--help' } }
if (-not $FlagPrefix) { $FlagPrefix = if ($Parser -eq 'packer') { '-' } else { '--' } }
if (-not $FlagValueSeparator) { $FlagValueSeparator = if ($Parser -eq 'packer') { '=' } else { ' ' } }
if (-not $PSBoundParameters.ContainsKey('UseBoolEqualsFormat')) { $UseBoolEqualsFormat = $Parser -eq 'packer' }

$parentPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
$solutionPath = Join-Path $parentPath $Binary
if (Test-Path -LiteralPath $solutionPath) {
    throw "Destination already exists: $solutionPath. Choose another name or -OutputDirectory. No files were changed."
}

$projectName = "FrenchExDev.Net.$Binary"
$runtimeDirectory = Join-Path $solutionPath "src/$projectName"
$designDirectory = Join-Path $solutionPath "src/$projectName.Design"
$testDirectory = Join-Path $solutionPath "test/$projectName.Tests"

function Get-RepositoryFile([string] $RelativePath) {
    $path = Join-Path $repositoryRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required checkout file is missing: $path"
    }
    return $path
}

function Get-XmlRelativePath([string] $From, [string] $To) {
    return [System.Security.SecurityElement]::Escape([IO.Path]::GetRelativePath($From, $To).Replace('\', '/'))
}

function Get-ProjectReference([string] $From, [string] $Module, [string] $Suffix, [switch] $Analyzer) {
    $name = "FrenchExDev.Net.$Module$Suffix"
    $path = Get-RepositoryFile "$Module/src/$name/$name.csproj"
    $relative = Get-XmlRelativePath $From $path
    $attributes = if ($Analyzer) { ' OutputItemType="Analyzer" ReferenceOutputAssembly="false"' } else { '' }
    return "    <ProjectReference Include=`"$relative`"$attributes />"
}

$runtimeProject = Get-RepositoryFile 'BinaryWrapper/src/FrenchExDev.Net.BinaryWrapper/FrenchExDev.Net.BinaryWrapper.csproj'
if (-not $Framework) {
    $projectXml = [xml](Get-Content -LiteralPath $runtimeProject -Raw)
    $targetNode = $projectXml.SelectSingleNode('//TargetFrameworks | //TargetFramework')
    if (-not $targetNode) { throw "No target framework found in $runtimeProject" }
    $Framework = $targetNode.InnerText.Split(';', [StringSplitOptions]::RemoveEmptyEntries)
}
$runtimeReferences = @(
    Get-ProjectReference $runtimeDirectory 'BinaryWrapper' ''
    Get-ProjectReference $runtimeDirectory 'BinaryWrapper' '.Attributes'
    Get-ProjectReference $runtimeDirectory 'BinaryWrapper' '.SourceGenerator' -Analyzer
    Get-ProjectReference $runtimeDirectory 'Builder' '.SourceGenerator.Lib' -Analyzer
    Get-ProjectReference $runtimeDirectory 'Builder' ''
    Get-ProjectReference $runtimeDirectory 'Result' ''
) -join "`n"
$designReferences = @(
    Get-ProjectReference $designDirectory 'BinaryWrapper' '.Design'
    Get-ProjectReference $designDirectory 'BinaryWrapper' '.Design.Lib'
) -join "`n"
$testingReference = Get-ProjectReference $testDirectory 'BinaryWrapper' '.Testing'
$buildProps = Get-XmlRelativePath $solutionPath (Get-RepositoryFile 'Directory.Build.props')
$packageProps = Get-XmlRelativePath $solutionPath (Get-RepositoryFile 'Directory.Packages.props')
$globalJson = Get-Content -LiteralPath (Get-RepositoryFile 'global.json') -Raw

# Match the source generator's ToPascalCase naming for executable names.
$entryClass = ($CommandName -split '[-_.]' | ForEach-Object {
    $_.Substring(0, 1).ToUpperInvariant() + $_.Substring(1).ToLowerInvariant()
}) -join ''
$descriptorClass = ($Binary -replace '\.', '') + 'Descriptor'
$parserFactory = switch ($Parser) {
    'gh' { 'new GhStyleHelpParser(binaryName: "__COMMAND__")' }
    'vagrant' { 'new __PROJECT__.Design.VagrantHelpParser()' }
    default { 'HelpParsers.Create("__PARSER__")' }
}
$tokens = [ordered]@{
    '__PROJECT__' = $projectName
    '__BINARY__' = $Binary
    '__COMMAND__' = $CommandName
    '__DESCRIPTOR__' = $descriptorClass
    '__ENTRY__' = $entryClass
    '__FRAMEWORKS__' = $Framework -join ';'
    '__FRAMEWORK__' = $Framework[0]
    '__PARSER_FACTORY__' = $parserFactory
    '__PARSER__' = $Parser
    '__HELP_FLAG__' = $HelpFlag
    '__FLAG_PREFIX__' = $FlagPrefix
    '__FLAG_SEPARATOR__' = $FlagValueSeparator
    '__BOOL_EQUALS__' = ([bool]$UseBoolEqualsFormat).ToString().ToLowerInvariant()
    '__RUNTIME_REFERENCES__' = $runtimeReferences
    '__DESIGN_REFERENCES__' = $designReferences
    '__TESTING_REFERENCE__' = $testingReference
    '__BUILD_PROPS__' = $buildProps
    '__PACKAGE_PROPS__' = $packageProps
    '__IMAGE_GUIDE__' = [IO.Path]::GetRelativePath($solutionPath, (Join-Path $repositoryRoot 'BinaryWrapper/doc/UPGRADE-IMAGE-PIPELINES.md')).Replace('\', '/')
}
# Expand the parser expression before using it as a replacement value.
$tokens['__PARSER_FACTORY__'] = $parserFactory.Replace('__COMMAND__', $CommandName).Replace('__PROJECT__', $projectName).Replace('__PARSER__', $Parser)
function Expand-Template([string] $Template) {
    foreach ($token in $tokens.Keys) { $Template = $Template.Replace($token, $tokens[$token]) }
    return $Template.TrimEnd() + "`n"
}

$files = [ordered]@{}
$files["$projectName.slnx"] = @'
<Solution>
  <Folder Name="/src/">
    <Project Path="src/__PROJECT__/__PROJECT__.csproj" />
    <Project Path="src/__PROJECT__.Design/__PROJECT__.Design.csproj" />
  </Folder>
  <Folder Name="/test/">
    <Project Path="test/__PROJECT__.Tests/__PROJECT__.Tests.csproj" />
  </Folder>
</Solution>
'@
$files['Directory.Build.props'] = @'
<Project>
  <Import Project="__BUILD_PROPS__" />
</Project>
'@
$files['Directory.Packages.props'] = @'
<Project>
  <Import Project="__PACKAGE_PROPS__" />
</Project>
'@
$files['global.json'] = $globalJson
$files['.gitignore'] = @'
**/bin/
**/obj/
.vs/
TestResults/
coverage-results/
.images/
.env
'@
$files['.binary-wrapper.yaml'] = @'
binary: src/__PROJECT__/__PROJECT__.csproj
design: src/__PROJECT__.Design/__PROJECT__.Design.csproj
'@
$files["src/$projectName/$projectName.csproj"] = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>__FRAMEWORKS__</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>__PROJECT__</RootNamespace>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)$(Configuration)/$(TargetFramework)/Generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>
  <ItemGroup>
__RUNTIME_REFERENCES__
  </ItemGroup>
  <ItemGroup>
    <_ScrapedCommandTree Include="scrape/__COMMAND__-*.json" />
    <AdditionalFiles Include="@(_ScrapedCommandTree)" />
    <!-- Empty bootstrap only until real help has been collected. Never merged with real versions. -->
    <AdditionalFiles Include="bootstrap/__COMMAND__-0.0.0.json" Condition="'@(_ScrapedCommandTree)' == ''" />
  </ItemGroup>
</Project>
'@
$files["src/$projectName/$descriptorClass.cs"] = @'
using FrenchExDev.Net.BinaryWrapper.Attributes;

namespace __PROJECT__;

[BinaryWrapper("__COMMAND__", FlagPrefix = "__FLAG_PREFIX__", FlagValueSeparator = "__FLAG_SEPARATOR__", UseBoolEqualsFormat = __BOOL_EQUALS__)]
public partial class __DESCRIPTOR__;
'@
$files["src/$projectName/bootstrap/$CommandName-0.0.0.json"] = @'
{
  "binaryName": "__COMMAND__",
  "description": "Empty scaffold, not collected CLI help. Excluded as soon as scrape contains real versions.",
  "root": {
    "name": "__COMMAND__",
    "options": [],
    "arguments": [],
    "subCommands": []
  }
}
'@
$files["src/$projectName/scrape/.gitkeep"] = ''
$files["src/$projectName.Design/$projectName.Design.csproj"] = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFrameworks>__FRAMEWORKS__</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>__PROJECT__.Design</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
__DESIGN_REFERENCES__
  </ItemGroup>
</Project>
'@
$files["src/$projectName.Design/Program.cs"] = @'
using System.Text.RegularExpressions;
using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using FrenchExDev.Net.Wrapper.Versioning;
using Microsoft.Extensions.Logging;

// The local executable is scraped once, under the version explicitly supplied by the caller.
// For multiple versions, replace the local middleware and collector as described in README.md.
string? version = null;
var executablePath = "__COMMAND__";
var runnerArgs = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--help" or "-h":
            Console.WriteLine("""
                Collect installed __COMMAND__ help:
                  --version <installed-version> [--binary-path <executable>] [--missing] [--list]
                Reparse cached help without executing the binary:
                  --reparse [--list]
                Runner options: --output <directory>, --parallel <n>, --scrape-parallel <n>,
                  --min-version <version>, --dashboard, --runtime <podman|docker> (containers only).
                """);
            return 0;
        case "--version" or "--binary-path":
            var option = args[i];
            if (++i >= args.Length || string.IsNullOrWhiteSpace(args[i]) || args[i].StartsWith("--"))
            {
                Console.Error.WriteLine($"Missing value for {option}.");
                return 2;
            }
            if (option == "--version") version = args[i];
            else executablePath = args[i];
            break;
        default:
            runnerArgs.Add(args[i]);
            break;
    }
}

if (version is not null && !Regex.IsMatch(version, @"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$"))
{
    Console.Error.WriteLine("--version must be a version such as 1.2.3 or 1.2.3-rc.1.");
    return 2;
}
var usesCacheOnly = runnerArgs.Contains("--reparse") || runnerArgs.Contains("--list-known-missing")
    || runnerArgs.Contains("--add-known-missing") || runnerArgs.Contains("--remove-known-missing");
if (version is null && !usesCacheOnly)
{
    Console.Error.WriteLine("Supply --version <installed-version>, or use --reparse for cached help. See --help.");
    return 2;
}

Func<string, ILogger, IHelpParser> parser = (_, _) => __PARSER_FACTORY__;
var pipeline = new DesignPipeline()
    .Use(next => async ctx =>
    {
        // Check root help before HelpScraper, which otherwise catches process errors.
        var rootHelp = await ctx.RunProcess([executablePath, "__HELP_FLAG__"]);
        if (string.IsNullOrWhiteSpace(rootHelp))
            throw new InvalidOperationException("The executable returned empty help output.");
        ctx.RunHelp = helpArgs => helpArgs.Length == 2
            ? Task.FromResult(rootHelp)
            : ctx.RunProcess([executablePath, .. helpArgs.Skip(1)]);
        ctx.HelpDumpDir = Path.Combine(ctx.OutputDir, "help", ctx.Version);
        await next(ctx);
    })
    .UseScraper("__COMMAND__", parser, helpFlag: "__HELP_FLAG__")
    .Build();
var reparsePipeline = new DesignPipeline()
    .UseCachedHelp()
    .UseScraper("__COMMAND__", parser, helpFlag: "__HELP_FLAG__")
    .Build();

return await new DesignPipelineRunner
{
    VersionCollector = new StaticVersionCollector(version is null ? [] : [version]),
    Pipeline = pipeline,
    ReparsePipeline = reparsePipeline,
    DefaultParallelism = 1,
    OutputFilePattern = "__COMMAND__-{version}.json",
    OutputDir = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "__PROJECT__", "scrape")),
}.RunAsync(runnerArgs.ToArray());
'@
if ($Parser -eq 'vagrant') {
    $vagrantParser = Get-RepositoryFile 'Vagrant/src/FrenchExDev.Net.Vagrant.Design/VagrantHelpParser.cs'
    $files["src/$projectName.Design/VagrantHelpParser.cs"] = (Get-Content -LiteralPath $vagrantParser -Raw).Replace(
        'namespace FrenchExDev.Net.Vagrant.Design;', "namespace $projectName.Design;")
}
$files["test/$projectName.Tests/$projectName.Tests.csproj"] = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>__FRAMEWORKS__</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="coverlet.collector" PrivateAssets="all" />
    <PackageReference Include="CsCheck" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" PrivateAssets="all" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/__PROJECT__/__PROJECT__.csproj" />
__TESTING_REFERENCE__
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
'@
$files["test/$projectName.Tests/WrapperTests.cs"] = @'
using System.Reflection;
using FrenchExDev.Net.BinaryWrapper;
using FrenchExDev.Net.BinaryWrapper.Attributes;
using Shouldly;

namespace __PROJECT__.Tests;

public sealed class WrapperTests
{
    [Fact]
    public void Descriptor_UsesExpectedExecutableAndSerialization()
    {
        var descriptor = typeof(__DESCRIPTOR__).GetCustomAttribute<BinaryWrapperAttribute>();
        descriptor.ShouldNotBeNull();
        descriptor.BinaryName.ShouldBe("__COMMAND__");
        descriptor.FlagPrefix.ShouldBe("__FLAG_PREFIX__");
        descriptor.FlagValueSeparator.ShouldBe("__FLAG_SEPARATOR__");
        descriptor.UseBoolEqualsFormat.ShouldBe(__BOOL_EQUALS__);
    }

    [Fact]
    public void GeneratedClient_CanBeCreatedWithoutAnInstalledBinary()
    {
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("__COMMAND__"),
            ExecutablePath = "__COMMAND__",
        };
        var client = global::__PROJECT__.__ENTRY__.Create(binding);
        client.ShouldBeOfType<global::__PROJECT__.__ENTRY__Client>();
    }
}
'@
$files['scripts/Find-Missing.ps1'] = @'
#requires -Version 7.0
[CmdletBinding()]
param(
    [switch] $Missing,
    [switch] $List,
    [switch] $Reparse,
    [string] $Version,
    [string] $BinaryPath,
    [string] $Framework = '__FRAMEWORK__'
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../src/__PROJECT__.Design/__PROJECT__.Design.csproj'
$designArgs = @()
if ($Missing) { $designArgs += '--missing' }
if ($List) { $designArgs += '--list' }
if ($Reparse) { $designArgs += '--reparse' }
if ($Version) { $designArgs += @('--version', $Version) }
if ($BinaryPath) { $designArgs += @('--binary-path', $BinaryPath) }
& dotnet run --project $project --framework $Framework -- @designArgs
if ($LASTEXITCODE -ne 0) { throw "Design failed (exit code $LASTEXITCODE)." }
'@
$files['README.md'] = @'
# __PROJECT__

Wrapper C# de `__COMMAND__`, construit avec BinaryWrapper. Les projets suivent
les conventions de Docker, Podman, Vagrant et des autres consommateurs du dépôt.

Si `BinaryWrapper/.env` existe lors de la création, il est copié à la racine de
cette solution, sans modification. Le fichier `.env` est ignoré par Git.

## Structure

```text
__BINARY__/
  __PROJECT__.slnx
  .binary-wrapper.yaml
  Directory.Build.props         # importe les conventions du dépôt
  Directory.Packages.props      # importe les versions NuGet centrales
  global.json                   # SDK du dépôt lors de la création
  src/
    __PROJECT__/
      __DESCRIPTOR__.cs
      bootstrap/                # arbre vide pour le premier build
      scrape/                   # JSON réels et help/<version>/*.help.txt
    __PROJECT__.Design/
      Program.cs                # collecte locale et relecture du cache
  test/
    __PROJECT__.Tests/
      WrapperTests.cs
  scripts/
    Find-Missing.ps1
```

## Prérequis et premier build

PowerShell 7, le SDK indiqué dans `global.json`, les frameworks `__FRAMEWORKS__`
et les projets BinaryWrapper, Builder, Result et Wrapper.Versioning de ce checkout.
Cette solution utilise des références relatives au dépôt ; elle n'est pas autonome
si on copie uniquement son répertoire ailleurs. Les versions NuGet restent centralisées.

Depuis ce répertoire :

```powershell
dotnet restore ./__PROJECT__.slnx
dotnet build ./__PROJECT__.slnx --no-restore
dotnet test ./__PROJECT__.slnx --no-build
```

Le premier build utilise un arbre **vide** dans `bootstrap/` : il vérifie le câblage
du générateur sans inventer de commandes CLI. Dès qu'un JSON réel existe dans
`scrape/`, cet arbre est automatiquement exclu des `AdditionalFiles` ; sa version
`0.0.0` n'entre donc pas dans la comparaison des versions collectées.

## Lancer le .Design

Installer `__COMMAND__`, puis relever sa version réelle. Le `.Design` utilise le
parseur `__PARSER__` et le drapeau d'aide `__HELP_FLAG__`. Adapter ces choix dans
`Program.cs` si le format d'aide de la CLI diffère.

```powershell
$design = './src/__PROJECT__.Design/__PROJECT__.Design.csproj'
dotnet run --project $design --framework __FRAMEWORK__ -- --help

# Remplacer 1.2.3 par la version réellement installée.
dotnet run --project $design --framework __FRAMEWORK__ -- --version 1.2.3

# Exécutable hors du PATH ; garder le chemin comme un argument unique.
dotnet run --project $design --framework __FRAMEWORK__ -- --version 1.2.3 --binary-path 'C:/Tools/__COMMAND__/__COMMAND__.exe'

# Relire les fichiers help sans lancer la CLI.
dotnet run --project $design --framework __FRAMEWORK__ -- --reparse

# Reconstruire l'API après collecte, puis exécuter les tests.
dotnet build ./__PROJECT__.slnx
dotnet test ./__PROJECT__.slnx --no-build
```

La collecte écrit `src/__PROJECT__/scrape/__COMMAND__-<version>.json` et conserve
l'aide brute dans `scrape/help/<version>/`. Le chemin par défaut correspond au
layout `bin/<configuration>/<framework>` ; utiliser `--output <répertoire>` si
le répertoire de compilation est personnalisé. Vérifier les commandes et options
collectées avant d'ajouter des tests métier : un parseur générique ne garantit
pas de reconnaître toutes les syntaxes d'aide.

```powershell
./scripts/Find-Missing.ps1 -Version 1.2.3 -List
./scripts/Find-Missing.ps1 -Version 1.2.3 -Missing
./scripts/Find-Missing.ps1 -Reparse
```

Ce script fonctionne depuis tout répertoire et ne dépend d'aucun module YAML.
En mode local, `-List` liste uniquement la version fournie ; `-Missing` vérifie
si son JSON existe. `--reparse` découvre les versions dans le cache d'aide.

## Utiliser l'API générée

```csharp
using FrenchExDev.Net.BinaryWrapper;

var binding = new BinaryBinding
{
    Identifier = new BinaryIdentifier("__COMMAND__"),
    ExecutablePath = "__COMMAND__",
};
var client = global::__PROJECT__.__ENTRY__.Create(binding);
// Après collecte : explorer les méthodes *Async et leurs builders dans IntelliSense.
// var command = await client.<Commande>Async(b => b.With<Option>(valeur));
// var executor = new CommandExecutor(new DictionaryBinaryResolver([binding]));
// var output = await executor.ExecuteAsync(binding.Identifier, command);
```

Le code généré se trouve sous `obj/<configuration>/<framework>/Generated`.
Ajouter dans les tests des assertions sur `CommandPath` et sur la liste ordonnée
complète de `ToArguments()`, puis utiliser `FakeProcessRunner` de
`FrenchExDev.Net.BinaryWrapper.Testing` pour tester l'exécution sans CLI installée.

## Passer à plusieurs versions en conteneur

Le squelette collecte **une version installée**. Pour suivre plusieurs versions,
adapter `Program.cs` selon le [guide de migration des images](<__IMAGE_GUIDE__>)
et le projet Dotnet du dépôt :

1. Remplacer `StaticVersionCollector` par `GitHubReleasesVersionCollector`,
   `GitHubTagsVersionCollector` ou un `IVersionCollector` spécifique.
2. Définir `DesignPipelineRunner.ImagePlan` : image système, script commun de
   dépendances et fonction d'installation de chaque version. Remplacer le middleware
   local `.Use(...)` par `.UseVersionImage().UseContainer()`.
3. Retirer l'obligation locale de fournir `--version`, conserver `UseScraper`,
   `ReparsePipeline`, `OutputFilePattern` et `OutputDir`.
4. Utiliser `--build-base`, `--build-images`, puis `--missing --parallel 4 --runtime podman`
   (ou `docker`). Les images restent en cache jusqu'à `--clean-images`.

Pour GitHub, charger le jeton avec `DotEnvLoader.Load()` et le transmettre au
collecteur, comme le font Docker et Podman. Le parseur Vagrant est spécifique ;
le scaffold `-Parser vagrant` en copie la source depuis le wrapper existant.
'@

# Prepare everything before the first write, including the optional Vagrant parser.
$renderedFiles = [ordered]@{}
foreach ($relativePath in $files.Keys) { $renderedFiles[$relativePath] = Expand-Template $files[$relativePath] }
$sourceEnvPath = Join-Path (Split-Path $PSScriptRoot -Parent) '.env'
$copyEnv = Test-Path -LiteralPath $sourceEnvPath -PathType Leaf
$fileCount = $renderedFiles.Count + [int]$copyEnv
if (-not $PSCmdlet.ShouldProcess($solutionPath, "Create BinaryWrapper solution ($fileCount files)")) { return }

# New-Item without -Force also rejects a destination created since the preflight check.
$null = New-Item -ItemType Directory -Path $solutionPath
foreach ($relativePath in $renderedFiles.Keys) {
    $destination = Join-Path $solutionPath $relativePath
    $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
    [IO.File]::WriteAllText($destination, $renderedFiles[$relativePath], [Text.UTF8Encoding]::new($false))
}
# Copy separately from templates to preserve bytes and avoid token expansion.
if ($copyEnv) {
    [IO.File]::Copy($sourceEnvPath, (Join-Path $solutionPath '.env'), $false)
}
Write-Host "Created $solutionPath"
Write-Host "Next: dotnet build `"$(Join-Path $solutionPath "$projectName.slnx")`""
Write-Host "Design: dotnet run --project `"$(Join-Path $designDirectory "$projectName.Design.csproj")`" --framework $($Framework[0]) -- --help"
Write-Output (Get-Item -LiteralPath $solutionPath)
