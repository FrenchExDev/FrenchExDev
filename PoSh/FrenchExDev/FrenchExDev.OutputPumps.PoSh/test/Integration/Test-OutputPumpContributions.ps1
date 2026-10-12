<#
.SYNOPSIS
Integration harness for OutputPumpContributions.
.DESCRIPTION
Run controlled scenarios, keep raw child-process output local and emit a bounded report of passed and failed checks.
#>


#requires -Version 7.4
#requires -PSEdition Core
$ErrorActionPreference='Stop'
$library=Split-Path (Split-Path $PSScriptRoot)
$parent=Split-Path $library
$env:PSModulePath=(Join-Path $library '.cache/Modules')+[IO.Path]::PathSeparator+$env:PSModulePath
$engine=Import-Module (Join-Path $library 'OutputPump.PoSh.psd1') -PassThru -Global -DisableNameChecking
$dev=Import-Module (Join-Path $parent 'FrenchExDev.DevPoSh.PoSh/DevPoSh.PoSh.psd1') -PassThru -DisableNameChecking
$root=Join-Path $library ('.cache/contributions-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
$failures=[Collections.Generic.List[object]]::new();$passed=0
function Assert-Contribution([bool]$Condition,[string]$Message){
    <#
    .SYNOPSIS
    Fail a contribution scenario when an invariant is false.
    .DESCRIPTION
    Raise a controlled assertion error for the integration harness to record as a bounded failure.
    #>
    if(!$Condition){throw $Message}}
function Test-Contribution([string]$Name,[scriptblock]$Body){
    <#
    .SYNOPSIS
    Run and record one contribution integration scenario.
    .DESCRIPTION
    Isolate scenario errors in the harness report so later scenarios can still run.
    #>
    try{& $Body;$script:passed++}catch{$script:failures.Add(@{Test=$Name;Message=$_.Exception.Message;Line=$_.InvocationInfo.ScriptLineNumber})}}
function Invoke-ContributionNative([string]$Executable,[string[]]$Arguments,[string]$Directory=$root){
    <#
    .SYNOPSIS
    Run a native fixture inside the contribution harness.
    .DESCRIPTION
    Keep captured process details local to the test instead of emitting its raw output.
    #>
    
    $capture=& $engine {param($e,$a,$d) Invoke-PumpProcess -Executable $e -Arguments $a -WorkingDirectory $d -TimeoutSeconds 120 -CaptureLimit 65536} $Executable $Arguments $Directory
    Assert-Contribution (!$capture.TimedOut -and !$capture.StartError -and !$capture.Truncated -and $capture.ExitCode -eq 0) 'Fixture process failed its capture contract.'
    $capture
}
function Invoke-ContributionChild([string]$Code,[string[]]$Arguments=@(),[switch]$ScriptScope){
    <#
    .SYNOPSIS
    Execute a contribution scenario in a fresh PowerShell process.
    .DESCRIPTION
    Use a child session to verify loading and compilation independently of the current module state.
    #>
    
    $file=Join-Path $root ('child-'+[guid]::NewGuid().ToString('N')+'.ps1');[IO.File]::WriteAllText($file,$Code)
    # A real startup profile runs in global scope, unlike a script passed to -File.
    $encodedArguments=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject @($Arguments) -Compress)))
    $bootstrap='$values=@(ConvertFrom-Json -InputObject ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('''+$encodedArguments+''')))); . '''+$file.Replace("'","''")+''' @values'
    $encodedCommand=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($bootstrap))
    $nativeArguments=if($ScriptScope){@('-NoProfile','-NonInteractive','-File',$file)+$Arguments}else{@('-NoProfile','-NonInteractive','-EncodedCommand',$encodedCommand)}
    $capture=Invoke-ContributionNative (Join-Path $PSHOME $(if($IsWindows){'pwsh.exe'}else{'pwsh'})) $nativeArguments
    $value=ConvertFrom-Json -InputObject $capture.Stdout -ErrorAction Stop
    $value
}
try {
    $origin=Join-Path $root origin;Copy-Item -LiteralPath (Join-Path $parent FrenchExDev.OPumper.Git.PoSh) -Destination $origin -Recurse
    Invoke-ContributionNative git @('init','--quiet','-b','main') $origin | Out-Null
    Invoke-ContributionNative git @('add','--all') $origin | Out-Null
    Invoke-ContributionNative git @('-c','user.name=Fixture','-c','user.email=fixture@example.test','commit','--quiet','-m','Provider fixture') $origin | Out-Null
    $providerRoot=Join-Path $root 'PoSh/Contributions/FrenchExDev.OPumper.Git.PoSh'
    [void][IO.Directory]::CreateDirectory((Split-Path $providerRoot))
    $repo=Join-Path $root 'repository été';[void][IO.Directory]::CreateDirectory($repo)
    Invoke-ContributionNative git @('init','--quiet','-b','main') $repo | Out-Null
    [IO.File]::WriteAllText((Join-Path $repo 'file with spaces.txt'),'fixture')
    $config=Join-Path $root pumps.yaml
    [IO.File]::WriteAllText($config,"version: 2`nroots:`n - path: '$($providerRoot.Replace("'","''"))'`nproviders: {}`npumps: {}`n")
    $inventory=Join-Path $root registry.json
    $profile=Join-Path $root standalone-profile.ps1
    [IO.File]::WriteAllText($profile,"Import-Module '$((Join-Path $library 'OutputPump.PoSh.psd1').Replace("'","''"))' -DisableNameChecking`n`$global:ContributionSetup=Setup-OutputPumps -ConfigurationPath '$($config.Replace("'","''"))' -InventoryPath '$($inventory.Replace("'","''"))'`n")
    Test-Contribution 'Cloning into a configured root activates Git in a fresh standalone profile' {
        Invoke-ContributionNative git @('clone','--quiet','--local',$origin,$providerRoot) | Out-Null
        $value=Invoke-ContributionChild @'
param($ProfileFile,$Repository)
. $ProfileFile
if($global:ContributionSetup.Outcome -ne 'Success'){$global:ContributionSetup | ConvertTo-OutputPumpJson;return}
$result=Pump-Me git status -WorkingDirectory $Repository
if(@(Get-Module DevPoSh.PoSh).Count){throw 'Standalone path loaded DevPoSh.'}
$result | ConvertTo-OutputPumpJson
'@ @($profile,$repo)
        Assert-Contribution ($value.Outcome -eq 'Success' -and $value.Data.Branch -eq 'main') ('Cloned provider did not return Git data: '+(ConvertTo-Json $value -Depth 12 -Compress))
    }
    Test-Contribution 'A preloaded provider is reused and registration is idempotent' {
        $value=Invoke-ContributionChild @'
param($Engine,$Provider,$Configuration,$Inventory,$Repository)
Import-Module $Engine -DisableNameChecking
$module=Import-Module $Provider -PassThru -Global -DisableNameChecking
if($global:FrenchExDevOutputPumpSession.Registry.Count){throw 'Ordinary import registered routes.'}
$first=Setup-OutputPumps -ConfigurationPath $Configuration -InventoryPath $Inventory
$second=Setup-OutputPumps -ConfigurationPath $Configuration -InventoryPath $Inventory
if($first.Outcome -ne 'Success' -or $second.Outcome -ne 'Success'){throw 'Setup failed.'}
if(![object]::ReferenceEquals($module,(Get-Module Git-OPumper))){throw 'Module reimported.'}
if(@($second.Data.Items | Where-Object Changed).Count){throw 'Unchanged setup executed contribution again.'}
Pump-Me git status -WorkingDirectory $Repository | ConvertTo-OutputPumpJson
'@ @((Join-Path $library OutputPump.PoSh.psd1),(Join-Path $providerRoot Git-OPumper.psd1),$config,$inventory,$repo)
        Assert-Contribution ($value.Outcome -eq 'Success' -and $value.Data.Items.Count -eq 1) 'Loaded-provider route failed.'
    }
    Test-Contribution 'A script-local engine import also initializes an absent provider' {
        $value=Invoke-ContributionChild -ScriptScope -Code @'
param($Engine,$Configuration,$Inventory,$Repository)
Import-Module $Engine -DisableNameChecking
$setup=Setup-OutputPumps -ConfigurationPath $Configuration -InventoryPath $Inventory
if($setup.Outcome -ne 'Success'){$setup | ConvertTo-OutputPumpJson;return}
Pump-Me git status -WorkingDirectory $Repository | ConvertTo-OutputPumpJson
'@ -Arguments @((Join-Path $library OutputPump.PoSh.psd1),$config,$inventory,$repo)
        Assert-Contribution ($value.Outcome -eq 'Success' -and $value.Data.Branch -eq 'main') ('Script scope initialization failed: '+(ConvertTo-Json $value.Error -Compress))
    }
    Test-Contribution 'DevPoSh source profile loads the provider before OutputPumps setup' {
        $devFixture=Join-Path $root 'PoSh/FrenchExDev'
        [void][IO.Directory]::CreateDirectory($devFixture)
        Copy-Item -LiteralPath (Join-Path $parent FrenchExDev.DevPoSh.PoSh) -Destination $devFixture -Recurse
        $user=Join-Path $root user;[void][IO.Directory]::CreateDirectory($user)
        Copy-Item -LiteralPath $config -Destination (Join-Path $user '.pumps-config.yaml')
        $devProfile=Join-Path $root dev-profile.ps1
        Set-DevPoShPowershellProfile -Path $devProfile | Out-Null
        $previous=$env:USERPROFILE
        try {
            $env:USERPROFILE=$user
            $value=Invoke-ContributionChild @'
param($Engine,$Dev,$ProfileFile,$SourceRoot,$Repository)
Import-Module $Engine -DisableNameChecking
Import-Module $Dev -DisableNameChecking
$env:FRENCHEXDEV_MYDEVPOSH_PATH=$SourceRoot;$env:FRENCHEXDEV_DEVPOSH='';$env:TERM_PROGRAM=''
. $ProfileFile
if(@(Get-Module -All -Name Git-OPumper).Count -ne 1){throw 'DevPoSh provider duplicated.'}
Pump-Me git status -WorkingDirectory $Repository | ConvertTo-OutputPumpJson
'@ @((Join-Path $library OutputPump.PoSh.psd1),(Join-Path $parent FrenchExDev.DevPoSh.PoSh/DevPoSh.PoSh.psd1),$devProfile,(Join-Path $root PoSh),$repo)
            Assert-Contribution ($value.Outcome -eq 'Success' -and $value.Data.Branch -eq 'main') 'DevPoSh source profile failed.'
        } finally {$env:USERPROFILE=$previous}
    }
    Test-Contribution 'Compiled engine and Git keep private capture and registration without their sources' {
        $engineCopy=Join-Path $root 'PoSh/Contributions/FrenchExDev.OutputPumps.PoSh'
        [void][IO.Directory]::CreateDirectory($engineCopy)
        foreach($name in @('src','resources','catalogue.json','OutputPump.PoSh.psd1','OutputPump.PoSh.psm1')){Copy-Item -LiteralPath (Join-Path $library $name) -Destination $engineCopy -Recurse}
        $compilation=Get-DevPoSh -Action CompileAll -Path @($engineCopy,$providerRoot) -CompilationRoot (Join-Path $root compiled)
        Assert-Contribution ($compilation.Outcome -eq 'Success') 'Engine and provider compilation failed.'
        $engineArtifact=($compilation.Data.Items | Where-Object Project -eq OutputPump.PoSh).Path
        $providerArtifact=($compilation.Data.Items | Where-Object Project -eq Git-OPumper).Path
        foreach($source in @((Join-Path $providerRoot src),$engineCopy)){
            $hidden=Join-Path $root ((Split-Path $source -Leaf)+'.hidden')
            Assert-Contribution ($source.StartsWith($root+[IO.Path]::DirectorySeparatorChar) -and $hidden.StartsWith($root+[IO.Path]::DirectorySeparatorChar)) 'Unsafe fixture move.'
            Move-Item -LiteralPath $source -Destination $hidden
        }
        $value=Invoke-ContributionChild @'
param($Engine,$Provider,$Configuration,$Inventory,$Repository)
Import-Module $Engine -Global -DisableNameChecking
$module=Import-Module $Provider -PassThru -Global -DisableNameChecking
$result=Setup-OutputPumps -ConfigurationPath $Configuration -InventoryPath $Inventory
if($result.Outcome -ne 'Success'){throw 'Compiled setup failed.'}
$result=Pump-Me git status -WorkingDirectory $Repository
if($result.Outcome -ne 'Success'){throw 'Compiled capture failed.'}
Register-OutputPump -Id git.status -Replace -Route @{Executable='git';Tokens=@('status');Options=@{};Positional='Path';Multiple=$true} -Handler {param($c) New-OutputPumpResult -Summary live -Data @{Branch='live';Items=@()}} | Out-Null
$localCall=New-OutputPumpScript -SoftwareId git -Function Get-PumpGitStatus -Parameters @{WorkingDirectory=$Repository}
$local=ConvertFrom-Json -InputObject (& $localCall.Data.Items[0].ScriptPath)
if($local.Data.Branch -ne 'live'){throw 'Same-process caller lost the live override.'}
$call=New-OutputPumpScript -SoftwareId git -Function Get-PumpGitStatus -Parameters @{WorkingDirectory=$Repository}
if($call.Outcome -ne 'Success'){throw 'Compiled caller generation failed.'}
$scriptPath=$call.Data.Items[0].ScriptPath
$process=Invoke-OutputPumpNative -Executable (Join-Path $PSHOME 'pwsh.exe') -ArgumentList @('-NoProfile','-NonInteractive','-File',$scriptPath) -Parse {param($capture) @{Result=(ConvertFrom-Json -InputObject $capture.Stdout)}} -Summary 'Generated compiled caller'
if($process.Outcome -ne 'Success' -or $process.Data.Result.Outcome -ne 'Success' -or $process.Data.Result.Data.Branch -ne 'main'){throw 'Fresh generated caller failed or transported a live closure.'}
$task=Invoke-OutputPumpTask {
    $local=Invoke-OutputPumpStep compiled-stream {
        Invoke-OutputPumpNative -Executable (Join-Path $PSHOME 'pwsh.exe') -ArgumentList @('-NoProfile','-Command','[Console]::WriteLine("one");[Console]::WriteLine("two")') -State @{Count=0} -OnRecord {param($r,$s) if($r.Stream -eq 'stdout'){$s.Count++}} -Parse {param($s,$c) @{Count=$s.Count}}
    }
    @{Count=$local.Data.Count}
}
if($task.Outcome -ne 'Success' -or $task.Data.Items[0].Count -ne 2 -or $task.Data.Status.Steps -ne 1){throw 'Compiled streaming or task API depended on missing sources.'}
$result | ConvertTo-OutputPumpJson
'@ @($engineArtifact,$providerArtifact,$config,$inventory,$repo)
        Assert-Contribution ($value.Outcome -eq 'Success' -and $value.Data.Branch -eq 'main') 'Compiled Git used missing sources.'
    }
}catch{$failures.Add(@{Test='Contribution fixtures';Message=$_.Exception.Message;Line=$_.InvocationInfo.ScriptLineNumber})}
finally{
    $expected=[IO.Path]::GetFullPath((Join-Path $library '.cache'))+[IO.Path]::DirectorySeparatorChar
    if([IO.Path]::GetFullPath($root).StartsWith($expected,[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue}
}
New-OutputPumpResult -Summary 'OutputPumps contribution integration completed.' -Outcome $(if($failures.Count){'Failed'}else{'Success'}) -Data @{Passed=$passed;Failed=$failures.Count;Items=$failures.ToArray()} -RetryAdvice $(if($failures.Count){'CorrectRequest'}else{'SafeToRetry'})
