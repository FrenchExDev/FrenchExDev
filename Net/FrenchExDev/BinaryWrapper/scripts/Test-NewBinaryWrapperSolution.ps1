#requires -Version 7.0
<#
.SYNOPSIS
Checks scaffolding without Pester. -Integration also builds, runs Design and tests generated C#.
.EXAMPLE
./BinaryWrapper/scripts/Test-NewBinaryWrapperSolution.ps1 -Integration
#>
[CmdletBinding()]
param(
    [switch] $Integration,
    [ValidateSet('net10.0', 'net11.0')]
    [string] $Framework = 'net10.0'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'New-BinaryWrapperSolution.ps1'
$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$temporaryRoot = Join-Path $repositoryRoot ('.scaffold-tests-' + [guid]::NewGuid().ToString('N'))
$outputDirectory = Join-Path $temporaryRoot 'output with spaces & accents é'
$script:checkCount = 0

function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
    $script:checkCount++
}

function Assert-Throws([scriptblock] $Action, [string] $Message) {
    $failed = $false
    try { & $Action | Out-Null } catch { $failed = $true }
    Assert-True $failed $Message
}

function Invoke-DotNet([string[]] $Arguments, [int] $ExpectedExitCode = 0) {
    & dotnet @Arguments
    Assert-True ($LASTEXITCODE -eq $ExpectedExitCode) "dotnet $($Arguments -join ' ') returned $LASTEXITCODE; expected $ExpectedExitCode."
}

try {
    $null = New-Item -ItemType Directory -Path $temporaryRoot
    Push-Location $temporaryRoot
    try {
        & $scriptPath Preview -OutputDirectory $outputDirectory -WhatIf | Out-Null
        Assert-True (-not (Test-Path -LiteralPath $outputDirectory)) 'WhatIf created files.'
        & $scriptPath ScaffoldPreview -WhatIf | Out-Null
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot 'ScaffoldPreview'))) 'Default WhatIf created a wrapper.'
        Assert-Throws { & $scriptPath '../Escape' -OutputDirectory $outputDirectory } 'Path traversal was accepted.'
        Assert-Throws { & $scriptPath CON -OutputDirectory $outputDirectory } 'Reserved directory name was accepted.'
        Assert-Throws { & $scriptPath Bad -CommandName 'tool;echo' -OutputDirectory $outputDirectory } 'Invalid executable name was accepted.'
        $solution = & $scriptPath ScaffoldSmoke -CommandName scaffold-smoke -Parser cobra -OutputDirectory $outputDirectory
    }
    finally { Pop-Location }

    $solutionPath = $solution.FullName
    Assert-True ($solutionPath -eq (Join-Path $outputDirectory 'ScaffoldSmoke')) 'Unexpected output location.'
    $projectName = 'FrenchExDev.Net.ScaffoldSmoke'
    $runtimePath = Join-Path $solutionPath "src/$projectName"
    $designProject = Join-Path $solutionPath "src/$projectName.Design/$projectName.Design.csproj"
    $solutionFile = Join-Path $solutionPath "$projectName.slnx"
    $allFiles = @(Get-ChildItem -LiteralPath $solutionPath -File -Recurse -Force)
    Assert-True ($allFiles.Count -ge 14) 'Incomplete generated solution.'
    foreach ($file in $allFiles) {
        if ($file.Name -eq '.env') { continue } # Copied verbatim, not a template.
        $content = Get-Content -LiteralPath $file.FullName -Raw
        Assert-True ($content -notmatch '__[A-Z_]+__') "Unexpanded token in $($file.Name)."
        if ($file.Extension -in '.csproj', '.props', '.slnx') {
            $xml = [xml]$content
            foreach ($node in $xml.SelectNodes('//ProjectReference | //Import | /Solution/Folder/Project')) {
                $relativePath = if ($node.HasAttribute('Include')) { $node.GetAttribute('Include') }
                    elseif ($node.HasAttribute('Project')) { $node.GetAttribute('Project') }
                    else { $node.GetAttribute('Path') }
                Assert-True (Test-Path -LiteralPath (Join-Path $file.DirectoryName $relativePath)) "Broken reference in $($file.Name): $relativePath"
            }
        }
    }
    $hashes = @{}
    foreach ($file in $allFiles) { $hashes[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName).Hash }
    Assert-Throws { & $scriptPath ScaffoldSmoke -OutputDirectory $outputDirectory } 'Existing solution was overwritten.'
    foreach ($path in $hashes.Keys) {
        Assert-True ((Get-FileHash -LiteralPath $path).Hash -eq $hashes[$path]) "Existing file changed: $path"
    }

    foreach ($name in 'Podman', 'Docker', 'Vagrant', 'Packer', 'GitLab.Cli', 'PodmanCompose') {
        $command = switch ($name) { 'GitLab.Cli' { 'glab' } 'PodmanCompose' { 'podman-compose' } default { $name.ToLowerInvariant() } }
        $generated = & $scriptPath $name -CommandName $command -OutputDirectory $outputDirectory -Framework net10.0
        $program = Get-Content -LiteralPath (Join-Path $generated.FullName "src/FrenchExDev.Net.$name.Design/Program.cs") -Raw
        $expected = switch ($name) {
            'Vagrant' { 'VagrantHelpParser' }
            'Packer' { 'HelpParsers.Create("packer")' }
            'GitLab.Cli' { 'GhStyleHelpParser(binaryName: "glab")' }
            'PodmanCompose' { 'HelpParsers.Create("argparse")' }
            default { 'HelpParsers.Create("cobra")' }
        }
        Assert-True ($program.Contains($expected)) "Incorrect parser for $name."
        if ($name -eq 'Vagrant') {
            Assert-True (Test-Path -LiteralPath (Join-Path $generated.FullName 'src/FrenchExDev.Net.Vagrant.Design/VagrantHelpParser.cs')) 'Missing Vagrant parser.'
        }
        if ($name -eq 'Packer') {
            $descriptor = Get-Content -LiteralPath (Join-Path $generated.FullName 'src/FrenchExDev.Net.Packer/PackerDescriptor.cs') -Raw
            Assert-True ($descriptor.Contains('FlagPrefix = "-", FlagValueSeparator = "=", UseBoolEqualsFormat = true')) 'Incorrect Packer flags.'
        }
    }

    if ($Integration) {
        Invoke-DotNet @('build', $solutionFile, '--nologo', '--verbosity', 'quiet')
        Invoke-DotNet @('test', $solutionFile, '--framework', $Framework, '--no-build', '--nologo', '--verbosity', 'minimal')
        Invoke-DotNet @('run', '--project', $designProject, '--framework', $Framework, '--no-build', '--', '--help')
        Invoke-DotNet @('run', '--project', $designProject, '--framework', $Framework, '--no-build', '--') 2
        Invoke-DotNet @('run', '--project', $designProject, '--framework', $Framework, '--no-build', '--', '--version', '../bad') 2
        Invoke-DotNet @('run', '--project', $designProject, '--framework', $Framework, '--no-build', '--', '--version', '1.2.3', '--binary-path', (Join-Path $temporaryRoot 'missing-cli')) 1
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $runtimePath 'scrape/scaffold-smoke-1.2.3.json'))) 'Failed executable produced a JSON file.'

        # Cached help is an offline integration fixture, never collected from a real CLI.
        $helpDirectory = Join-Path $runtimePath 'scrape/help/1.2.3'
        $null = New-Item -ItemType Directory -Path $helpDirectory -Force
        [IO.File]::WriteAllText((Join-Path $helpDirectory 'scaffold-smoke.help.txt'), @'
Fixture CLI

Usage:
  scaffold-smoke [command]

Available Commands:
  run         Run a fixture
'@)
        [IO.File]::WriteAllText((Join-Path $helpDirectory 'scaffold-smoke_run.help.txt'), @'
Run a fixture

Usage:
  scaffold-smoke run [flags]

Flags:
  -d, --detach        Detached execution
  -n, --name string   Name of the fixture
'@)
        # Build a tiny local executable to exercise the real process callback offline.
        $fixtureDirectory = Join-Path $temporaryRoot 'fixture cli'
        $null = New-Item -ItemType Directory -Path $fixtureDirectory
        $fixtureProject = Join-Path $fixtureDirectory 'FixtureCli.csproj'
        [IO.File]::WriteAllText($fixtureProject, '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>' + $Framework + '</TargetFramework></PropertyGroup></Project>')
        [IO.File]::WriteAllText((Join-Path $fixtureDirectory 'Program.cs'), @'
if (args.Length == 1 && args[0] == "--help")
    System.Console.WriteLine("Fixture CLI\n\nUsage:\n  scaffold-smoke [command]\n\nAvailable Commands:\n  run         Run a fixture");
else if (args.Length == 2 && args[0] == "run" && args[1] == "--help")
    System.Console.WriteLine("Run a fixture\n\nUsage:\n  scaffold-smoke run [flags]\n\nFlags:\n  -d, --detach        Detached execution\n  -n, --name string   Name of the fixture");
else
    return 9;
return 0;
'@)
        Invoke-DotNet @('build', $fixtureProject, '--nologo', '--verbosity', 'quiet')
        $executableName = if ($IsWindows) { 'FixtureCli.exe' } else { 'FixtureCli' }
        $fixtureExecutable = Join-Path $fixtureDirectory "bin/Debug/$Framework/$executableName"
        Invoke-DotNet @('run', '--project', $designProject, '--framework', $Framework, '--no-build', '--', '--version', '1.2.4', '--binary-path', $fixtureExecutable)
        Assert-True (Test-Path -LiteralPath (Join-Path $runtimePath 'scrape/help/1.2.4/scaffold-smoke_run.help.txt')) 'Local CLI help was not cached.'

        $findMissing = Join-Path $solutionPath 'scripts/Find-Missing.ps1'
        & $findMissing -Reparse -Framework $Framework
        Assert-True (Test-Path -LiteralPath (Join-Path $runtimePath 'scrape/scaffold-smoke-1.2.3.json')) 'Design did not write the versioned JSON.'
        $tree = Get-Content -LiteralPath (Join-Path $runtimePath 'scrape/scaffold-smoke-1.2.3.json') -Raw | ConvertFrom-Json
        Assert-True ($tree.root.subCommands[0].name -eq 'run') 'Cached command was not parsed.'

        [IO.File]::WriteAllText((Join-Path $solutionPath "test/$projectName.Tests/CollectedCommandTests.cs"), @'
using FrenchExDev.Net.BinaryWrapper;
using FrenchExDev.Net.BinaryWrapper.Testing;
using Shouldly;

namespace FrenchExDev.Net.ScaffoldSmoke.Tests;

public sealed class CollectedCommandTests
{
    [Fact]
    public async Task CollectedCommand_PreservesArgumentBoundariesDuringExecution()
    {
        var command = new ScaffoldSmokeRunCommand { Detach = true, Name = "a name with spaces" };
        command.CommandPath.ShouldBe(new[] { "run" });
        command.ToArguments().ShouldBe(new[] { "--detach", "--name", "a name with spaces" });
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("scaffold-smoke"),
            ExecutablePath = "path with spaces/scaffold-smoke",
        };
        var runner = new FakeProcessRunner(stdout: "fixture output");
        var executor = new CommandExecutor(new DictionaryBinaryResolver([binding]), runner);
        var result = await executor.ExecuteAsync(binding.Identifier, command);
        result.IsSuccess.ShouldBeTrue();
        runner.LastSpec.ShouldNotBeNull();
        runner.LastSpec.Arguments.ShouldBe(new[] { "run", "--detach", "--name", "a name with spaces" });
    }

    [Fact]
    public void Bootstrap_IsExcludedAfterCollection()
    {
        typeof(ScaffoldSmokeDescriptor).Assembly.GetType("FrenchExDev.Net.ScaffoldSmoke.ScaffoldSmokeCommand").ShouldBeNull();
    }
}
'@)
        Invoke-DotNet @('test', $solutionFile, '--framework', $Framework, '--nologo', '--verbosity', 'minimal')
        Invoke-DotNet @('run', '--project', $designProject, '--framework', $Framework, '--no-build', '--', '--reparse', '--list')
        foreach ($name in 'Vagrant', 'Packer', 'GitLab.Cli') {
            Invoke-DotNet @('build', (Join-Path $outputDirectory "$name/FrenchExDev.Net.$name.slnx"), '--nologo', '--verbosity', 'quiet')
        }
    }
    Write-Host "Passed $script:checkCount scaffold checks (Integration=$Integration, Framework=$Framework)."
}
finally {
    # Remove only this run's unique directory, after verifying its absolute parent and name.
    $resolvedPath = [IO.Path]::GetFullPath($temporaryRoot)
    if ([IO.Path]::GetDirectoryName($resolvedPath) -ne [IO.Path]::GetFullPath($repositoryRoot) -or
        [IO.Path]::GetFileName($resolvedPath) -notmatch '^\.scaffold-tests-[0-9a-f]{32}$') {
        throw "Refusing cleanup outside the validation directory: $resolvedPath"
    }
    if (Test-Path -LiteralPath $resolvedPath) { Remove-Item -LiteralPath $resolvedPath -Recurse -Force }
}
