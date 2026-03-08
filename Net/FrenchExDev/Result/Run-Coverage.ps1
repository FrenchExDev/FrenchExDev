<#
.SYNOPSIS
    Runs xUnit tests with Coverlet coverage on FrenchExDev.Net.Result.
    Displays a per-class summary in console. Generates an HTML report if
    reportgenerator is installed as a dotnet global tool.
.PARAMETER Watch
    Re-run automatically on any .cs file change (Ctrl+C to stop).
.PARAMETER OpenReport
    Open the HTML report in the browser after generation (requires reportgenerator).
#>
param(
    [switch]$Watch,
    [switch]$OpenReport
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$testProject  = Join-Path $PSScriptRoot 'test\FrenchExDev.Net.Result.Tests\FrenchExDev.Net.Result.Tests.csproj'
$resultsDir   = Join-Path $PSScriptRoot '.coverage'
$reportDir    = Join-Path $PSScriptRoot '.coverage\html'
$watchPattern = Join-Path $PSScriptRoot '**\*.cs'

function Invoke-Coverage {
    # ── Clean previous results ────────────────────────────────────────────────
    if (Test-Path $resultsDir) { Remove-Item $resultsDir -Recurse -Force }
    New-Item $resultsDir -ItemType Directory -Force | Out-Null

    # ── Run tests ─────────────────────────────────────────────────────────────
    Write-Host ""
    Write-Host "Running tests..." -ForegroundColor Cyan

    $testArgs = @(
        'test', $testProject,
        '--collect:XPlat Code Coverage',
        "--results-directory:$resultsDir",
        '--nologo',
        '-v', 'q'
    )

    & dotnet @testArgs
    $exitCode = $LASTEXITCODE

    # ── Find coverage file ────────────────────────────────────────────────────
    $covFile = Get-ChildItem $resultsDir -Recurse -Filter 'coverage.cobertura.xml' |
               Sort-Object LastWriteTime -Descending |
               Select-Object -First 1

    if (-not $covFile) {
        Write-Host ""
        Write-Warning "No coverage.cobertura.xml found. Tests may have failed to build."
        return
    }

    # ── Parse cobertura XML ────────────────────────────────────────────────────
    [xml]$cov = Get-Content $covFile.FullName -Raw

    $lineRate   = [double]$cov.coverage.'line-rate'   * 100
    $branchRate = [double]$cov.coverage.'branch-rate' * 100

    $classes = @($cov.coverage.packages.package) | ForEach-Object { $_.classes.class } | ForEach-Object {
        [PSCustomObject]@{
            Name        = $_.name -replace '.*\.', ''   # short class name
            Lines       = [math]::Round([double]$_.'line-rate'   * 100, 1)
            Branches    = [math]::Round([double]$_.'branch-rate' * 100, 1)
        }
    } | Sort-Object Name

    # ── Display ───────────────────────────────────────────────────────────────
    Write-Host ""
    Write-Host "─── Coverage Report ─────────────────────────────────────────" -ForegroundColor DarkCyan
    Write-Host ""

    $nameW = ($classes | Measure-Object -Property Name -Maximum).Maximum.Length
    $nameW = [math]::Max($nameW, 10)

    $header = "  {0}  {1,8}  {2,9}" -f "Class".PadRight($nameW), "Lines %", "Branches %"
    Write-Host $header -ForegroundColor DarkGray
    Write-Host ("  " + "─" * ($nameW + 24)) -ForegroundColor DarkGray

    foreach ($c in $classes) {
        $lineColor   = if ($c.Lines   -ge 100) { 'Green' } elseif ($c.Lines   -ge 80) { 'Yellow' } else { 'Red' }
        $branchColor = if ($c.Branches -ge 100) { 'Green' } elseif ($c.Branches -ge 80) { 'Yellow' } else { 'Red' }

        Write-Host -NoNewline ("  {0}  " -f $c.Name.PadRight($nameW))
        Write-Host -NoNewline ("{0,7}%  " -f $c.Lines)   -ForegroundColor $lineColor
        Write-Host           ("{0,8}%"   -f $c.Branches) -ForegroundColor $branchColor
    }

    Write-Host ("  " + "─" * ($nameW + 24)) -ForegroundColor DarkGray

    $totalLineColor   = if ($lineRate   -ge 100) { 'Green' } elseif ($lineRate   -ge 80) { 'Yellow' } else { 'Red' }
    $totalBranchColor = if ($branchRate -ge 100) { 'Green' } elseif ($branchRate -ge 80) { 'Yellow' } else { 'Red' }

    Write-Host -NoNewline ("  {0}  " -f "TOTAL".PadRight($nameW))
    Write-Host -NoNewline ("{0,7}%  " -f [math]::Round($lineRate,   1)) -ForegroundColor $totalLineColor
    Write-Host           ("{0,8}%"   -f [math]::Round($branchRate, 1)) -ForegroundColor $totalBranchColor
    Write-Host ""

    if ($exitCode -ne 0) {
        Write-Host "Tests FAILED (exit $exitCode)" -ForegroundColor Red
    } else {
        Write-Host "All tests passed." -ForegroundColor Green
    }

    # ── HTML report (optional) ────────────────────────────────────────────────
    $rgAvailable = $null -ne (Get-Command 'reportgenerator' -ErrorAction SilentlyContinue)
    if ($rgAvailable) {
        Write-Host ""
        Write-Host "Generating HTML report..." -ForegroundColor Cyan
        & reportgenerator `
            "-reports:$($covFile.FullName)" `
            "-targetdir:$reportDir" `
            '-reporttypes:Html' `
            | Out-Null

        $indexPath = Join-Path $reportDir 'index.html'
        Write-Host "Report: $indexPath" -ForegroundColor DarkCyan

        if ($OpenReport -and (Test-Path $indexPath)) {
            Start-Process $indexPath
        }
    }
    else {
        Write-Host "(Install 'dotnet tool install -g dotnet-reportgenerator-globaltool' for HTML reports)" -ForegroundColor DarkGray
    }

    Write-Host ""
}

# ── Entry point ───────────────────────────────────────────────────────────────

if ($Watch) {
    Write-Host "Watch mode — monitoring *.cs files. Press Ctrl+C to stop." -ForegroundColor Cyan

    $watcher = New-Object System.IO.FileSystemWatcher
    $watcher.Path   = $PSScriptRoot
    $watcher.Filter = '*.cs'
    $watcher.IncludeSubdirectories = $true
    $watcher.EnableRaisingEvents   = $true

    Invoke-Coverage   # initial run

    $lastRun = [datetime]::UtcNow
    $action  = {
        $now = [datetime]::UtcNow
        if (($now - $lastRun).TotalSeconds -lt 2) { return }  # debounce
        $script:lastRun = $now
        Write-Host ""
        Write-Host "Change detected — re-running..." -ForegroundColor DarkYellow
        Invoke-Coverage
    }

    Register-ObjectEvent $watcher Changed -Action $action | Out-Null
    Register-ObjectEvent $watcher Created -Action $action | Out-Null

    try { while ($true) { Start-Sleep 1 } }
    finally { $watcher.Dispose() }
}
else {
    Invoke-Coverage
}
