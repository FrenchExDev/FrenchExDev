if ($env:TERM_PROGRAM -eq "vscode") { . "$(code --locate-shell-integration-path pwsh)" }

$OutputEncoding = [console]::InputEncoding = [console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$MyPoShClonePath = $env:FRENCHEXDEV_MYDEVPOSH_PATH # Set in system environment variables

if ($IsLinux -or $IsMac) { $env:USERPROFILE = $env:HOME }

$global:DEVPOSH_LOGLEVEL = "INFO"  # Default log level

function Log-DevPoShModuleLoading {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '', Scope = 'Function')]
    [CmdletBinding()]
    param([parameter(Mandatory = $true, Position = 0)][string] $Message, [string] $Level = "INFO", [string] $Header = "FrenchExDev.PoSh", [string] $ForegroundColor = "WHITE", [string] $BackgroundColor = "BLACK")

    if ($null -eq $ForegroundColor) {
        switch ($Level.ToUpper()) {
            "DEBUG" { $ForegroundColor = "Gray" }
            "INFO" { $ForegroundColor = "Green" }
            "WARN" { $ForegroundColor = "Yellow" }
            "ERROR" { $ForegroundColor = "Red" }
            default { $ForegroundColor = "White" }
        }
    }

    # Respect configured log level: return early if message level is below threshold
    $threshold = $global:DEVPOSH_LOGLEVEL
    if ([string]::IsNullOrWhiteSpace($threshold)) { $threshold = "INFO" }
    $levels = @{ "DEBUG" = 0; "WARN" = 1; "INFO" = 2; "ERROR" = 3 }
    $msgLevel = $Level.ToUpper()
    if (-not $levels.ContainsKey($msgLevel)) { $msgLevel = "INFO" }
    $thr = $threshold.ToUpper()
    if (-not $levels.ContainsKey($thr)) { $thr = "INFO" }

    if ($levels[$msgLevel] -lt $levels[$thr]) { return }

    Write-Host "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss:zzz')@$Header@$Level@$Message" -ForegroundColor $ForegroundColor -BackgroundColor $BackgroundColor
}

function Clean-DevPoShModuleCache {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '', Scope = 'Function')]
    [CmdletBinding()]
    param([string] $Acme, [string] $Module)
    $expression = "remove-item -recurse -force $env:USERPROFILE/.posh.posh.compilations/$Acme/$Module"
    Log-DevPoShModuleLoading "[Clean-DevPoShModuleCache] Cleaning $acme $module" -ForegroundColor Cyan -Level Warn
    Invoke-Expression $expression
}

function Test-DevPoShSyntax {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '', Scope = 'Function')]
    [CmdletBinding()]
    param([string] $Path, [switch] $ThrowOnError)
    
    $allErrors = @()
    $files = Get-ChildItem -Path $Path -Include @("*.ps1", "*.psm1") -Recurse -File
    
    foreach ($file in $files) {
        $content = Get-Content -Path $file.FullName -Raw -ErrorAction SilentlyContinue
        if ([string]::IsNullOrWhiteSpace($content)) { continue }
        
        $parseErrors = $null
        $tokens = $null
        [void][System.Management.Automation.Language.Parser]::ParseInput($content, $file.FullName, [ref]$tokens, [ref]$parseErrors)
        
        if ($parseErrors -and $parseErrors.Count -gt 0) {
            foreach ($err in $parseErrors) {
                $allErrors += [PSCustomObject]@{
                    File    = $file.FullName
                    Line    = $err.Extent.StartLineNumber
                    Message = $err.Message
                }
                Log-DevPoShModuleLoading "SYNTAX ERROR: $($file.Name):$($err.Extent.StartLineNumber) - $($err.Message)" "ERROR"
            }
        }
    }
    
    if ($allErrors.Count -gt 0) {
        Log-DevPoShModuleLoading "`n$($allErrors.Count) syntax error(s) found!" "ERROR"
        if ($ThrowOnError) { throw "Syntax errors prevent module import" }
        return $false
    }
    return $true
}

function Format-Elapsed {
    param(
        [Parameter(Mandatory = $true, Position = 0)] [System.TimeSpan] $ts
    )
    $mins = [Math]::Floor($ts.TotalMinutes)
    $secs = $ts.TotalSeconds - ($mins * 60)
    if ($mins -ge 1) { "{0}m{1:N3}s" -f $mins, $secs } else { "{0:N3}s" -f $ts.TotalSeconds }
}

function Import-DevPoShModule {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '', Scope = 'Function')]
    [CmdletBinding()]
    param([switch] $Clean, [switch] $Force, [switch] $SkipValidation, [string] $Acme, [string] $Module, [string] $LogLevel = "INFO")
    
    if ($Clean) { Clean-DevPoshModuleCache }
    
    $modulePath = "$MyPoShClonePath/$Acme/$Module"
    
    # PRODUCTION MODE: Use compiled cache for faster loading
    if ($env:FRENCHEXDEV_DEVPOSH -eq "Production") {
        if ($PSBoundParameters.ContainsKey('Module') -and -not [string]::IsNullOrWhiteSpace($Module)) {
            $productionCachePath = Join-Path $env:USERPROFILE ".posh.posh.compilations\$Acme\$Module\production-cache.psm1"
        }
        else {
            $productionCachePath = Join-Path $env:USERPROFILE ".posh.posh.compilations\$Acme\production-cache.psm1"
        }
        $productionCacheDir = Split-Path $productionCachePath -Parent

        # Check if cache needs rebuilding
        $needsRebuild = -not (Test-Path $productionCachePath)
        if (-not $needsRebuild) {
            $cacheTime = (Get-Item $productionCachePath).LastWriteTime
            $newerFiles = Get-ChildItem -Path $modulePath -Include @("*.ps1", "*.psm1", "*.psd1") -Recurse -File |
            Where-Object { $_.LastWriteTime -gt $cacheTime }
            $needsRebuild = $newerFiles.Count -gt 0
        }

        if ($needsRebuild) {
            Log-DevPoShModuleLoading "Rebuild needed => recompiling production cache..." -Level DEBUG
            $compileSw = [System.Diagnostics.Stopwatch]::StartNew()

            if (-not (Test-Path $productionCacheDir)) {
                New-Item -ItemType Directory -Path $productionCacheDir -Force | Out-Null
            }

            $allCode = [System.Text.StringBuilder]::new()
            $exportFunctionNames = New-Object System.Collections.Generic.List[string]

            # Get load order based on dependencies
            Log-DevPoShModuleLoading "Discovering module manifests..." -Level DEBUG
            $discoverSw = [System.Diagnostics.Stopwatch]::StartNew()
            $manifests = Get-ChildItem -Filter "*.psd1" -Recurse $modulePath | Sort-Object {
                if ($_.BaseName -match '\.Types\.') { "0_$($_.BaseName)" }
                elseif ($_.BaseName -match '\.Core\.') { "1_$($_.BaseName)" }
                else { "2_$($_.BaseName)" }
            }
            $discoverSw.Stop()
            $discoverDuration = Format-Elapsed $discoverSw.Elapsed
            Log-DevPoShModuleLoading "Found $($manifests.Count) manifests, took $discoverDuration" -Level DEBUG

            foreach ($manifest in $manifests) {
                $moduleDir = Split-Path $manifest.FullName -Parent
                $srcDir = Join-Path $moduleDir "src"
                foreach ($folder in @("Classes", "Private", "Public", "Completers", "Variables")) {
                        $folderPath = Join-Path $srcDir $folder
                        $files = Get-ChildItem -Path $folderPath -Filter "*.ps1" -Recurse -File
                            foreach ($file in $files) {
                                [void]$allCode.AppendLine("# Source: $($file.FullName)")
                                $fileContent = Get-Content $file.FullName -Raw
                                [void]$allCode.AppendLine($fileContent)
                                [void]$allCode.AppendLine("")
                                
                                if ($folder -eq 'Public') {
                                    $exportFunctionNames.Add($file.Name -Replace "\.ps1$", "")
                                }
                            }

                    }
            }

            $compiledText = $allCode.ToString()
            $fnNames = $exportFunctionNames | Select-Object -Unique
            if ($fnNames.Count -gt 0) {
                $quotedFnNames = $fnNames | ForEach-Object { "'$_'" }
                $exportLine = "Export-ModuleMember -Function " + ($quotedFnNames -join ",")
                $compiledText += "`n`n# Auto-generated exports`n" + $exportLine + "`n"
            }

            $compiledText | Out-File $productionCachePath -Encoding utf8 -Force
            $compileSw.Stop()
            $compileDuration = Format-Elapsed $compileSw.Elapsed
            Log-DevPoShModuleLoading "Production cache compiled, took $compileDuration" -Level DEBUG
        }

        # Import compiled production module
        Log-DevPoShModuleLoading "Importing compiled production module..." -Level DEBUG
        $loadSw = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            Import-Module -Force -DisableNameChecking $productionCachePath
        }
        catch {
            Log-DevPoShModuleLoading "Failed to import production cache as module: $_" -Level ERROR
            throw
        }
        $loadSw.Stop()
        $loadDuration = Format-Elapsed $loadSw.Elapsed

        if ($needsRebuild) {
            $totalDuration = Format-Elapsed ($compileSw.Elapsed + $loadSw.Elapsed)
            Log-DevPoShModuleLoading "Total=$totalDuration@(compile=${compileDuration};loading=${loadDuration})." -Level INFO
        }
        else {
            Log-DevPoShModuleLoading "Total=$loadDuration." -Level INFO
        }

        return
    }
    
    # DEVELOPMENT MODE: Load normally with syntax validation
    if (-not $SkipValidation) {
        Log-DevPoShModuleLoading "Validating PS1 syntax..." -ForegroundColor Cyan -Level DEBUG
        $syntaxSw = [System.Diagnostics.Stopwatch]::StartNew()
        $isValid = Test-DevPoShSyntax -Path $modulePath -ThrowOnError
        $syntaxSw.Stop()
        if (-not $isValid) { return }
        $syntaxDuration = Format-Elapsed $syntaxSw.Elapsed
        Log-DevPoShModuleLoading "Syntax validation passed, took $syntaxDuration" -ForegroundColor Green -Level INFO
    }
    
    # Get load order based on dependencies (Types modules first, then Core, then others)
    Log-DevPoShModuleLoading "Discovering module manifests..." -ForegroundColor Cyan -Level INFO
    $discoverSw = [System.Diagnostics.Stopwatch]::StartNew()
    $manifests = Get-ChildItem -Filter "*.psd1" -Recurse $modulePath | Sort-Object { 
        # Load .Types. modules first, then .Core. modules, then alphabetically
        if ($_.BaseName -match '\.Types\.') { "0_$($_.BaseName)" }
        elseif ($_.BaseName -match '\.Core\.') { "1_$($_.BaseName)" }
        else { "2_$($_.BaseName)" }
    }
    $discoverSw.Stop()
    $discoverDuration = Format-Elapsed $discoverSw.Elapsed
    Log-DevPoShModuleLoading "Found $($manifests.Count) manifests, took $discoverDuration" -ForegroundColor Green -Level INFO
    
    Log-DevPoShModuleLoading "Importing modules..." -ForegroundColor Cyan -Level INFO
    $importSw = [System.Diagnostics.Stopwatch]::StartNew()
    foreach ($manifest in $manifests) {
        Write-Debug "Import-PoShModule > $($manifest.Name) > Importing"
        Import-Module -DisableNameChecking $manifest.FullName -Force:$Force.IsPresent
    }
    $importSw.Stop()
    $importDuration = Format-Elapsed $importSw.Elapsed
    Log-DevPoShModuleLoading "Modules imported, took $importDuration" -ForegroundColor Green -Level INFO
}

try {
    Import-DevPoShModule -SkipValidation -ErrorAction Stop
}
catch {
    Write-Error "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss:zzz')[FrenchExDev.PoSh][ERROR] Initial import failed, retrying with -Force -Clean..."
    Import-DevPoShModule -Force -Clean
}

Write-Debug "PowerShell profile loaded."
