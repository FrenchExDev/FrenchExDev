#requires -Version 7.4
#requires -PSEdition Core
[CmdletBinding()]
param([ValidateSet('Unit','Integration','All')][string]$Suite='All',[string]$Module,[string]$Cmdlet,[switch]$Coverage,[switch]$Internal)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$library=Split-Path $PSScriptRoot
$env:PSModulePath=(Join-Path $library '.cache/Modules')+[IO.Path]::PathSeparator+$env:PSModulePath
Import-Module (Join-Path $library 'Core.psm1') -DisableNameChecking
if(!$Internal) {
    $arguments=@('-NoProfile','-NonInteractive','-File',$PSCommandPath,'-Suite',$Suite,'-Internal')
    if($Module){$arguments+=@('-Module',$Module)}
    if($Cmdlet){$arguments+=@('-Cmdlet',$Cmdlet)}
    if($Coverage){$arguments+='-Coverage'}
    $core=Get-Module | Where-Object Path -eq (Join-Path $library 'Core.psm1') | Select-Object -First 1
    $capture=& $core {param($a,$d) Invoke-PumpProcess -Executable (Join-Path $PSHOME 'pwsh.exe') -Arguments $a -WorkingDirectory $d -TimeoutSeconds 600 -CaptureLimit 1048576} $arguments $library
    try {
        if($capture.StartError -or $capture.TimedOut -or $capture.Truncated){throw 'Test subprocess failed its capture contract.'}
        $line=@($capture.Stdout -split '\r?\n' | Where-Object {$_.StartsWith('{')}) | Select-Object -Last 1
        $result=ConvertFrom-Json -InputObject $line -ErrorAction Stop
        $result | ConvertTo-Json -Depth 24 -Compress
        if($capture.ExitCode -ne 0 -or $result.Outcome -ne 'Success'){exit 1}
    }catch{New-PumpResult -Summary 'Test subprocess could not return a bounded result.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='TestSubprocessFailed'} | ConvertTo-Json -Depth 24 -Compress;exit 1}
    exit 0
}
$reports=[Collections.Generic.List[object]]::new()
try {
    $artifacts=Join-Path $PSScriptRoot 'Artifacts';[void][IO.Directory]::CreateDirectory($artifacts)
    if($Suite -in @('Unit','All')) {
        Import-Module Pester -RequiredVersion 6.2.0 -ErrorAction Stop
        $configuration=New-PesterConfiguration
        $configuration.Run.Parallel=$false
        $configuration.TestResult.OutputFormat='NUnitXml'
        $providerPaths=@(foreach($name in @('Git','Dotnet','Pnpm','Podman','Ripgrep','PowerShell')){Join-Path (Split-Path $library) "FrenchExDev.OPumper.$name.PoSh/test/Unit"})
        $testPath=@((Join-Path $PSScriptRoot 'Unit'))+$providerPaths
        if($Module){
            $providerName=$Module -replace '^FrenchExDev\.OPumper\.(.+)\.PoSh$','$1' -replace '-OPumper$',''
            if($providerName -in @('Git','Dotnet','Pnpm','Podman','Ripgrep','PowerShell')){$testPath=@(Join-Path (Split-Path $library) "FrenchExDev.OPumper.$providerName.PoSh/test/Unit")}
            else{$testPath=@(Join-Path (Join-Path $PSScriptRoot 'Unit') $Module)}
        }
        if($Cmdlet){
            if($Module){$testPath=@(Join-Path $testPath[0] "$Cmdlet.Tests.ps1")}
            else{
                $matches=@(Get-ChildItem -LiteralPath $testPath -Filter "$Cmdlet.Tests.ps1" -File -Recurse)
                if($matches.Count -gt 1){$engineMatches=@($matches | Where-Object {$_.FullName.StartsWith((Join-Path $PSScriptRoot 'Unit')+[IO.Path]::DirectorySeparatorChar)});if($engineMatches.Count -eq 1){$matches=$engineMatches}}
                if($matches.Count -ne 1){throw 'Unknown or ambiguous cmdlet test selection; specify -Module.'}
                $testPath=@($matches[0].FullName)
            }
        }
        foreach($selected in $testPath){if(!(Test-Path -LiteralPath $selected)){throw 'Unknown unit test selection.'}}
        $configuration.Run.Path=@($testPath)
        $configuration.Run.PassThru=$true
        $configuration.Output.Verbosity='None'
        $configuration.TestResult.Enabled=$true
        $reportName=if($Module -or $Cmdlet){'unit-'+(($Module,$Cmdlet | Where-Object {$_}) -join '-') -replace '[^A-Za-z0-9.-]','_'}else{'unit'}
        $configuration.TestResult.OutputPath=Join-Path $artifacts "$reportName.xml"
        if($Coverage){$configuration.CodeCoverage.Enabled=$true;$configuration.CodeCoverage.Path=@((Join-Path $library 'src'),(Join-Path (Split-Path $library) 'FrenchExDev.DevPoSh.PoSh/src'))+@($providerPaths | ForEach-Object {Join-Path (Split-Path (Split-Path $_)) 'src'});$configuration.CodeCoverage.OutputPath=Join-Path $artifacts 'coverage.xml'}
        $result=Invoke-Pester -Configuration $configuration 6>$null
        $failed=@($result.Failed | Select-Object -First 12 | ForEach-Object {@{Test=$_.ExpandedPath;Message=$_.ErrorRecord.Exception.Message}})
        $reports.Add(@{Suite='Unit';Passed=$result.PassedCount;Failed=$result.FailedCount;Skipped=$result.SkippedCount;DiscoveryErrors=$result.FailedContainersCount;Items=$failed})
    }
    if($Suite -in @('Integration','All')) {
        foreach($script in @('Test-OutputPumps.ps1','Test-OutputPumpRuntime.ps1','Test-OutputPumpContributions.ps1','Test-OutputPumpStreaming.ps1')) {
            $path=Join-Path $PSScriptRoot "Integration/$script"
            if(Test-Path -LiteralPath $path){$result=& $path;$reports.Add(@{Suite=$script;Outcome=$result.Outcome;Data=$result.Data})}
        }
        [IO.File]::WriteAllText((Join-Path $artifacts 'integration.json'),(ConvertTo-Json -InputObject @($reports | Where-Object Suite -ne 'Unit') -Depth 24))
    }
    $failed=@($reports|Where-Object {($_.ContainsKey('Failed') -and ($_.Failed -gt 0 -or $_.DiscoveryErrors -gt 0)) -or ($_.ContainsKey('Outcome') -and $_.Outcome -ne 'Success')}).Count
    $result=New-PumpResult -Summary 'OutputPump test suites completed.' -Outcome $(if($failed){'Failed'}else{'Success'}) -Data @{Items=$reports.ToArray()} -RetryAdvice $(if($failed){'CorrectRequest'}else{'SafeToRetry'})
    $result | ConvertTo-Json -Depth 24 -Compress
    if($failed){exit 1}
} catch {New-PumpResult -Summary 'Test runner could not complete.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='TestRunnerFailed';Message=$_.Exception.Message} | ConvertTo-Json -Depth 24 -Compress;exit 1}
