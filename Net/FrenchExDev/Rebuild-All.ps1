[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [ValidateRange(1, 64)]
    [int] $MaxCpuCount = 4
)

$ErrorActionPreference = 'Stop'
$solutionPath = Join-Path ([IO.Path]::GetTempPath()) ('FrenchExDev.Net.All-' + [guid]::NewGuid().ToString('N') + '.slnx')
Push-Location -LiteralPath $PSScriptRoot
try {
    $projects = @(rg --files -g '*.csproj' -g '!**/obj/**' -g '!**/bin/**' | Sort-Object)
    if ($LASTEXITCODE -ne 0 -or $projects.Count -eq 0) {
        throw 'Cannot enumerate projects. Ensure ripgrep (rg) is installed.'
    }
    $lines = @('<Solution>') + @($projects | ForEach-Object {
        $projectPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $_))
        '  <Project Path="' + [Security.SecurityElement]::Escape($projectPath) + '" />'
    }) + @('</Solution>')
    [IO.File]::WriteAllLines($solutionPath, $lines)

    Write-Host "Rebuilding $($projects.Count) projects in $Configuration for all declared target frameworks."
    & dotnet restore $solutionPath --disable-parallel -m:1 --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw "Restore failed (exit $LASTEXITCODE)." }

    & dotnet build $solutionPath --no-restore --no-incremental --configuration $Configuration "-m:$MaxCpuCount" --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }
}
finally {
    if (Test-Path -LiteralPath $solutionPath) {
        Remove-Item -LiteralPath $solutionPath -Force
    }
    Pop-Location
}
