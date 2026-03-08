<#
.SYNOPSIS
    Checks and applies NuGet package updates for Directory.Packages.props (Central Package Management).
.PARAMETER Apply
    Apply all available updates without prompting.
.PARAMETER IncludePrerelease
    Include pre-release versions in the update check.
.PARAMETER ParallelMax
    Maximum number of concurrent NuGet API requests (default 4).
.EXAMPLE
    .\Update-Packages.ps1
    .\Update-Packages.ps1 -Apply
    .\Update-Packages.ps1 -Apply -IncludePrerelease
    .\Update-Packages.ps1 -ParallelMax 8
#>
[CmdletBinding()]
param(
    [switch]$Apply,
    [switch]$IncludePrerelease,
    [int]$ParallelMax = 4
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$propsFile = Join-Path $PSScriptRoot 'Directory.Packages.props'

if (-not (Test-Path $propsFile)) {
    Write-Error "Directory.Packages.props not found at: $propsFile"
    exit 1
}

# ── Helpers ──────────────────────────────────────────────────────────────────

function Get-LatestNuGetVersion {
    param(
        [string]$PackageId,
        [string]$CurrentVersion,
        [bool]$IncludePrerelease
    )

    $url = "https://api.nuget.org/v3-flatcontainer/$($PackageId.ToLower())/index.json"
    try {
        $response = Invoke-RestMethod -Uri $url -TimeoutSec 10 -ErrorAction Stop
        $versions = $response.versions

        $currentIsPrerelease = $CurrentVersion -match '-'

        $candidates = $versions | Where-Object {
            if ($IncludePrerelease -or $currentIsPrerelease) { $true }
            else { $_ -notmatch '-' }
        }

        return $candidates | Select-Object -Last 1
    }
    catch {
        return $null
    }
}

function Compare-SemVer {
    # Returns negative if a < b, 0 if equal, positive if a > b
    param([string]$a, [string]$b)

    $parse = {
        param([string]$v)
        $parts  = $v -split '-', 2
        $nums   = $parts[0] -split '\.' | ForEach-Object { [int]$_ }
        while ($nums.Count -lt 4) { $nums += @(0) }
        @{ nums = $nums; pre = if ($parts.Count -gt 1) { $parts[1] } else { $null } }
    }

    $pa = & $parse $a
    $pb = & $parse $b

    for ($i = 0; $i -lt 4; $i++) {
        $d = $pa.nums[$i] - $pb.nums[$i]
        if ($d -ne 0) { return $d }
    }

    # No pre-release tag is "higher" than having one (1.0.0 > 1.0.0-preview)
    if ($null -eq $pa.pre -and $null -ne $pb.pre) { return 1  }
    if ($null -ne $pa.pre -and $null -eq $pb.pre) { return -1 }
    if ($null -ne $pa.pre -and $null -ne $pb.pre) {
        return [string]::Compare($pa.pre, $pb.pre, [System.StringComparison]::OrdinalIgnoreCase)
    }

    return 0
}

# ── Load packages ─────────────────────────────────────────────────────────────

[xml]$xml = [System.IO.File]::ReadAllText($propsFile, [System.Text.UTF8Encoding]::new($false))

$packages = @($xml.Project.ItemGroup.PackageVersion | ForEach-Object {
    [PSCustomObject]@{ Id = $_.Include; Current = $_.Version }
})

# ── Check versions ───────────────────────────────────────────────────────────

$cycleChars = @('⠋','⠙','⠹','⠸','⠼','⠴','⠦','⠧','⠇','⠏')
$checkMark  = '✓'
$upArrow    = '↑'
$crossMark  = '✗'

$updates  = [System.Collections.Generic.List[PSCustomObject]]::new()
$errors   = [System.Collections.Generic.List[string]]::new()
$maxLen   = ($packages | Measure-Object -Property Id -Maximum).Maximum.Length + 2
$verColW  = 14
$statColW = 22

function Write-Row {
    param(
        [int]$Y,
        [string]$Cycle,       [ConsoleColor]$CycleColor  = 'Gray',
        [string]$Version,
        [string]$Status,      [ConsoleColor]$StatusColor = 'Gray',
        [string]$Name
    )
    [Console]::SetCursorPosition(0, $Y)
    Write-Host -NoNewline '  '
    Write-Host -NoNewline $Cycle.PadRight(2)  -ForegroundColor $CycleColor
    Write-Host -NoNewline $Version.PadRight($verColW)
    Write-Host -NoNewline $Status.PadRight($statColW) -ForegroundColor $StatusColor
    Write-Host $Name.PadRight($maxLen) -NoNewline
}

Write-Host ''
Write-Host ("  {0}  {1}  {2}  {3}" -f ' ', 'Version'.PadRight($verColW), 'Status'.PadRight($statColW), 'Package') -ForegroundColor Cyan
Write-Host ("  {0}  {1}  {2}  {3}" -f '─', ('─' * $verColW), ('─' * $statColW), ('─' * $maxLen)) -ForegroundColor DarkGray

# Print placeholder rows
foreach ($pkg in $packages) {
    Write-Host ("  {0}  {1}  {2}  {3}" -f '·', $pkg.Current.PadRight($verColW), ''.PadRight($statColW), $pkg.Id) -ForegroundColor DarkGray
}

$tableStartY = [Console]::CursorTop - $packages.Count

# ── Parallel fetch using runspace pool ────────────────────────────────────────

$fetchScript = {
    param([string]$PackageId, [string]$CurrentVersion, [bool]$IncludePrerelease)
    $url = "https://api.nuget.org/v3-flatcontainer/$($PackageId.ToLower())/index.json"
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            $response = Invoke-RestMethod -Uri $url -TimeoutSec 10 -ErrorAction Stop
            $versions = $response.versions
            $currentIsPrerelease = $CurrentVersion -match '-'
            $candidates = $versions | Where-Object {
                if ($IncludePrerelease -or $currentIsPrerelease) { $true }
                else { $_ -notmatch '-' }
            }
            return $candidates | Select-Object -Last 1
        }
        catch {
            if ($attempt -lt 5) { Start-Sleep -Milliseconds (500 * $attempt) }
        }
    }
    return $null
}

$pool = [RunspaceFactory]::CreateRunspacePool(1, $ParallelMax)
$pool.Open()

$jobs = [System.Collections.Generic.List[hashtable]]::new()
for ($i = 0; $i -lt $packages.Count; $i++) {
    $pkg = $packages[$i]
    $ps  = [PowerShell]::Create().AddScript($fetchScript).
            AddArgument($pkg.Id).
            AddArgument($pkg.Current).
            AddArgument($IncludePrerelease.IsPresent)
    $ps.RunspacePool = $pool
    $jobs.Add(@{ Index = $i; PS = $ps; Handle = $ps.BeginInvoke(); Done = $false })
}

$spinFrame   = 0
$pendingCount = $jobs.Count

while ($pendingCount -gt 0) {
    $spinFrame++

    foreach ($job in $jobs) {
        if ($job.Done) { continue }

        $i   = $job.Index
        $pkg = $packages[$i]
        $row = $tableStartY + $i

        if ($job.Handle.IsCompleted) {
            $result = $job.PS.EndInvoke($job.Handle)
            $latest = if ($result.Count -gt 0) { $result[$result.Count - 1] } else { $null }
            $job.PS.Dispose()
            $job.Done = $true
            $pendingCount--

            if ($null -eq $latest) {
                Write-Row -Y $row `
                    -Cycle $crossMark -CycleColor Red `
                    -Version $pkg.Current `
                    -Status 'fetch error' -StatusColor Red `
                    -Name $pkg.Id
                $errors.Add($pkg.Id)
            }
            elseif ((Compare-SemVer $pkg.Current $latest) -lt 0) {
                Write-Row -Y $row `
                    -Cycle ' ' `
                    -Version $pkg.Current `
                    -Status "$upArrow $latest" -StatusColor Yellow `
                    -Name $pkg.Id
                $updates.Add([PSCustomObject]@{ Id = $pkg.Id; Current = $pkg.Current; Latest = $latest })
            }
            else {
                Write-Row -Y $row `
                    -Cycle ' ' `
                    -Version $pkg.Current `
                    -Status $checkMark -StatusColor Green `
                    -Name $pkg.Id
            }
        }
        else {
            # Animate spinner for in-progress rows
            [Console]::SetCursorPosition(2, $row)
            Write-Host -NoNewline $cycleChars[$spinFrame % $cycleChars.Count] -ForegroundColor Yellow
        }
    }

    if ($pendingCount -gt 0) { Start-Sleep -Milliseconds 80 }
}

$pool.Close()
$pool.Dispose()

# Move cursor below the table
[Console]::SetCursorPosition(0, $tableStartY + $packages.Count)

Write-Host ""

if ($errors.Count -gt 0) {
    Write-Host "Could not fetch: $($errors -join ', ')" -ForegroundColor Yellow
    Write-Host ""
}

if ($updates.Count -eq 0) {
    Write-Host "All packages are up to date." -ForegroundColor Green
    exit 0
}

# ── Select updates ────────────────────────────────────────────────────────────

$toApply = [System.Collections.Generic.List[PSCustomObject]]::new()

if ($Apply) {
    $toApply.AddRange($updates)
}
else {
    Write-Host "Available updates:" -ForegroundColor Cyan
    Write-Host ""
    for ($i = 0; $i -lt $updates.Count; $i++) {
        $u = $updates[$i]
        Write-Host ("  [{0,2}]  {1}  {2} -> {3}" -f ($i + 1), $u.Id.PadRight($maxLen), $u.Current.PadRight(12), $u.Latest)
    }
    Write-Host ""
    Write-Host "  Enter package numbers (e.g.  1,3), 'all', or 'none' (default: all)"
    $raw = (Read-Host "  Selection").Trim()

    if ($raw -eq '' -or $raw.ToLower() -eq 'all') {
        $toApply.AddRange($updates)
    }
    elseif ($raw.ToLower() -eq 'none') {
        Write-Host "No updates selected. Exiting."
        exit 0
    }
    else {
        foreach ($token in ($raw -split '[,\s]+')) {
            $idx = 0
            if ([int]::TryParse($token, [ref]$idx) -and $idx -ge 1 -and $idx -le $updates.Count) {
                $toApply.Add($updates[$idx - 1])
            }
            else {
                Write-Warning "Ignored invalid selection: '$token'"
            }
        }
    }
}

if ($toApply.Count -eq 0) {
    Write-Host "No updates selected."
    exit 0
}

# ── Apply updates ─────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "Applying $($toApply.Count) update(s)..." -ForegroundColor Cyan
Write-Host ""

$content = [System.IO.File]::ReadAllText($propsFile, [System.Text.UTF8Encoding]::new($false))

foreach ($u in $toApply) {
    $escapedId  = [regex]::Escape($u.Id)
    $escapedVer = [regex]::Escape($u.Current)
    $pattern    = "(<PackageVersion\s+Include=""$escapedId""\s+Version="")$escapedVer("")"
    $replacement = "`${1}$($u.Latest)`${2}"
    $newContent  = $content -replace $pattern, $replacement

    if ($newContent -eq $content) {
        Write-Warning "  Pattern not matched for $($u.Id) — skipped"
        continue
    }

    $content = $newContent
    Write-Host ("  {0}  {1} -> {2}" -f $u.Id.PadRight($maxLen), $u.Current, $u.Latest) -ForegroundColor Green
}

[System.IO.File]::WriteAllText($propsFile, $content, [System.Text.UTF8Encoding]::new($false))

Write-Host ""
Write-Host "Done. $propsFile updated." -ForegroundColor Green
