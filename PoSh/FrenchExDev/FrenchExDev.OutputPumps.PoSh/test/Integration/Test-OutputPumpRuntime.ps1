<#
.SYNOPSIS
Integration harness for OutputPumpRuntime.
.DESCRIPTION
Run controlled scenarios, keep raw child-process output local and emit a bounded report of passed and failed checks.
#>


#requires -Version 7.4
#requires -PSEdition Core
$ErrorActionPreference='Stop'
$library=Split-Path (Split-Path $PSScriptRoot)
$devLibrary=Join-Path (Split-Path $library) 'FrenchExDev.DevPoSh.PoSh'
$env:PSModulePath=(Join-Path $library '.cache/Modules')+[IO.Path]::PathSeparator+$env:PSModulePath
$core=Import-Module (Join-Path $library 'Core.psm1') -PassThru
$module=Import-Module (Join-Path $library 'OutputPump.PoSh.psd1') -Force -PassThru -DisableNameChecking
$dev=Import-Module (Join-Path $devLibrary 'DevPoSh.PoSh.psd1') -Force -PassThru -DisableNameChecking
$root=Join-Path $library ('.cache/runtime-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
$failures=[Collections.Generic.List[object]]::new();$passed=0
$oldSourceRoot=$env:FRENCHEXDEV_MYDEVPOSH_PATH
function Assert-Runtime([bool]$Condition,[string]$Message){
    <#
    .SYNOPSIS
    Fail a runtime scenario when its expected condition is false.
    .DESCRIPTION
    Raise a controlled assertion error consumed by the integration harness.
    #>
    if(!$Condition){throw $Message}}
function Test-Runtime([string]$Name,[scriptblock]$Body){
    <#
    .SYNOPSIS
    Run and record one runtime integration scenario.
    .DESCRIPTION
    Convert scenario failures into report entries while allowing subsequent checks to execute.
    #>
    
    try{& $Body;$script:passed++}catch{$script:failures.Add(@{Test=$Name;Message=$_.Exception.Message;Line=$_.InvocationInfo.ScriptLineNumber})}
}
function Invoke-RuntimeNative([string]$Executable,[string[]]$Arguments,[string]$Directory=$root,[int]$Timeout=60){
    <#
    .SYNOPSIS
    Run a controlled native command for a runtime test.
    .DESCRIPTION
    Keep process output inside the test harness for assertions rather than displaying it.
    #>
    
    $capture=& $core {param($e,$a,$d,$t) Invoke-PumpProcess -Executable $e -Arguments $a -WorkingDirectory $d -TimeoutSeconds $t -CaptureLimit 65536} $Executable $Arguments $Directory $Timeout
    return $capture
}
function Invoke-RuntimeChild([string]$Text,[string[]]$Arguments=@(),[string]$UserDirectory){
    <#
    .SYNOPSIS
    Execute a runtime fixture in a fresh PowerShell session.
    .DESCRIPTION
    Verify startup and passthrough behavior without inheriting the parent registry state.
    #>
    
    $path=Join-Path $root ('child-'+[guid]::NewGuid().ToString('N')+'.ps1');[IO.File]::WriteAllText($path,$Text)
    $previous=$env:USERPROFILE
    try{
        if($UserDirectory){$env:USERPROFILE=$UserDirectory}
        $capture=Invoke-RuntimeNative -Executable (Join-Path $PSHOME 'pwsh.exe') -Arguments (@('-NoProfile','-NonInteractive','-File',$path)+$Arguments)
        Assert-Runtime (!$capture.TimedOut -and !$capture.StartError -and !$capture.Truncated) 'Child process did not complete within its capture contract.'
        Assert-Runtime ($capture.ExitCode -eq 0) ('Child failed: '+($capture.Stderr -replace '[\r\n]+',' ').Substring(0,[Math]::Min(300,($capture.Stderr -replace '[\r\n]+',' ').Length)))
        ConvertFrom-Json -InputObject $capture.Stdout -ErrorAction Stop
    }finally{$env:USERPROFILE=$previous}
}
try {
    $configuration=Join-Path $root 'pumps.yaml'
    [IO.File]::WriteAllText($configuration,"version: 2`nroots:`n  - path: '$((Split-Path $library).Replace("'","''"))'`npumps: {}`n")
    Setup-OutputPumps -ConfigurationPath $configuration -InventoryPath (Join-Path $root registry.json) | Out-Null
    $repositories=@(Join-Path $root 'repo un');$repositories+=Join-Path $root 'repo été'
    foreach($repository in $repositories){
        [void][IO.Directory]::CreateDirectory($repository)
        $capture=Invoke-RuntimeNative git @('init','-b','main','--quiet') $repository
        Assert-Runtime ($capture.ExitCode -eq 0) 'Git fixture initialization failed.'
        [IO.File]::WriteAllText((Join-Path $repository 'été file.txt'),'fixture text')
    }
    Test-Runtime 'Dispatch from two repositories and narrowing after truncation' {
        foreach($repository in $repositories){
            $result=& $module.ExportedFunctions['Pump-Me'] git status -WorkingDirectory $repository -ConfigurationPath $configuration
            Assert-Runtime ($result.Outcome -eq 'Success' -and $result.Data.Branch -eq 'main') 'Git dispatch did not return its typed branch.'
            Assert-Runtime ($result.Data.PSTypeNames[0] -match 'OutputPump') 'Business object type missing.'
        }
        1..4|ForEach-Object{[IO.File]::WriteAllText((Join-Path $repositories[0] "file-$_.txt"),'fixture')}
        $wide=& $module.ExportedFunctions['Pump-Me'] git status -MaxItems 1 -WorkingDirectory $repositories[0] -ConfigurationPath $configuration
        Assert-Runtime $wide.Truncation.Applied 'Wide request must signal omissions.'
        $narrow=& $module.ExportedFunctions['Pump-Me'] git status 'été file.txt' -MaxItems 1 -WorkingDirectory $repositories[0] -ConfigurationPath $configuration
        Assert-Runtime (!$narrow.Truncation.Applied) 'Narrowing the actual Git path should remove truncation.'
    }
    Test-Runtime 'Native passthrough preserves argument boundaries and exit code without terminating host' {
        $nativeFile=Join-Path $root 'native-arguments.ps1'
        [IO.File]::WriteAllText($nativeFile,'param($Value); [Console]::OutputEncoding=[Text.UTF8Encoding]::new($false); [Console]::WriteLine($Value); exit 7')
        $child=Invoke-RuntimeChild @'
param($Manifest,$Configuration,$Directory,$NativeFile)
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
Import-Module $Manifest -DisableNameChecking
Setup-OutputPumps -ConfigurationPath $Configuration | Out-Null
$before=$PWD.Path
$output=Pump-Me (Join-Path $PSHOME 'pwsh.exe') -NativeArguments @('-NoProfile','-File',$NativeFile,'space and été') -ConfigurationPath $Configuration -WorkingDirectory $Directory
[pscustomobject]@{Text=$output;Code=$LASTEXITCODE;LocationRestored=($PWD.Path -eq $before)} | ConvertTo-Json -Compress
'@ @((Join-Path $library 'OutputPump.PoSh.psd1'),$configuration,$repositories[0],$nativeFile)
        Assert-Runtime ($child.Code -eq 7 -and $child.LocationRestored -and $child.Text -eq 'space and été') ('Passthrough changed native semantics: '+(ConvertTo-Json $child -Compress))
    }
    Test-Runtime 'Inventory is deterministic and protects the last valid file under contention' {
        $inventory=Join-Path $root registry.json
        $first=New-OutputPumpCompilation -ConfigurationPath $configuration -Path $inventory
        Assert-Runtime ($first.Outcome -eq 'Success') 'Inventory failed.'
        $stamp=(Get-Item $inventory).LastWriteTimeUtc;$hash=(Get-FileHash $inventory).Hash
        $second=New-OutputPumpCompilation -ConfigurationPath $configuration -Path $inventory
        Assert-Runtime (!$second.Data.Items[0].Changed -and (Get-Item $inventory).LastWriteTimeUtc -eq $stamp) 'Identical inventory rewritten.'
        $lock=[IO.File]::Open($inventory+'.lock','OpenOrCreate','ReadWrite','None')
        try {
            $child=Invoke-RuntimeChild @'
param($Manifest,$Configuration,$Inventory)
Import-Module $Manifest -DisableNameChecking
New-OutputPumpCompilation -ConfigurationPath $Configuration -Path $Inventory | ConvertTo-OutputPumpJson
'@ @((Join-Path $library 'OutputPump.PoSh.psd1'),$configuration,$inventory)
            Assert-Runtime ($child.Outcome -eq 'Failed' -and (Get-FileHash $inventory).Hash -eq $hash) 'Concurrent writer replaced inventory.'
        } finally {$lock.Dispose()}
        $fixture=Join-Path $root 'PoSh/FrenchExDev/FrenchExDev.OutputPumps.PoSh'
        [void][IO.Directory]::CreateDirectory($fixture)
        foreach($name in @('src','resources','software','Core.psm1','OutputPump.PoSh.psm1','OutputPump.PoSh.psd1','OutputPumps.psm1','OutputPumps.psd1','catalogue.json','index.md')){Copy-Item -LiteralPath (Join-Path $library $name) -Destination $fixture -Recurse}
        Copy-Item -LiteralPath $devLibrary -Destination (Join-Path (Split-Path $fixture) 'FrenchExDev.DevPoSh.PoSh') -Recurse
        foreach($name in @('Git','Dotnet','Pnpm','Podman','Ripgrep','PowerShell')){Copy-Item -LiteralPath (Join-Path (Split-Path $library) "FrenchExDev.OPumper.$name.PoSh") -Destination (Split-Path $fixture) -Recurse}
        $user=Join-Path $root user;[void][IO.Directory]::CreateDirectory($user)
        $fixtureConfig=Join-Path $user '.pumps-config.yaml'
        [IO.File]::WriteAllText($fixtureConfig,"version: 2`nroots:`n - path: '$((Split-Path $fixture).Replace("'","''"))'`nproviders: {}`npumps: {}`n")
    }
    Test-Runtime 'Common compiler preserves resources, classes, variables and module boundaries' {
        $project=Join-Path $root 'PoSh/Fixture/Example.PoSh'
        foreach($directory in @('Public','Private','Classes','Variables')){[void][IO.Directory]::CreateDirectory((Join-Path $project "src/$directory"))}
        [void][IO.Directory]::CreateDirectory((Join-Path $project 'resources'))
        [IO.File]::WriteAllText((Join-Path $project 'resources/value.txt'),'packaged resource')
        $provider=Join-Path $root 'PoSh/Fixture/Provider.PoSh'
        foreach($name in @('Private','Public')){[void][IO.Directory]::CreateDirectory((Join-Path $provider "src/$name"))}
        [IO.File]::WriteAllText((Join-Path $provider 'src/Private/Get-FixtureSecret.ps1'),'function Get-FixtureSecret {
    <#
    .SYNOPSIS
    Return a private fixture value to verify compiled module scope.
    .DESCRIPTION
    Keep the integration or unit fixture deterministic and independent of the real user environment.
    #>
    "dependency"}')
        [IO.File]::WriteAllText((Join-Path $provider 'src/Public/Get-FixtureDependency.ps1'),'function Get-FixtureDependency {
    <#
    .SYNOPSIS
    Return a dependency value used by the compilation fixture.
    .DESCRIPTION
    Keep the integration or unit fixture deterministic and independent of the real user environment.
    #>
    Get-FixtureSecret}')
        [IO.File]::WriteAllText((Join-Path $provider 'Provider.PoSh.psm1'),'foreach($category in @("Private","Public")){Get-ChildItem (Join-Path $PSScriptRoot "src/$category") -Filter *.ps1 | ForEach-Object {. $_.FullName}}; Export-ModuleMember -Function Get-FixtureDependency')
        [IO.File]::WriteAllText((Join-Path $provider 'Provider.PoSh.psd1'),"@{RootModule='Provider.PoSh.psm1';ModuleVersion='1.0';FunctionsToExport=@('Get-FixtureDependency')}")
        [IO.File]::WriteAllText((Join-Path $project 'src/Classes/FixtureValue.ps1'),'class FixtureValue { [int] $Number=42 }')
        [IO.File]::WriteAllText((Join-Path $project 'src/Variables/State.ps1'),'$script:FixtureState="state"; Set-Alias fixture Get-FixtureValue')
        [IO.File]::WriteAllText((Join-Path $project 'src/Private/Get-FixtureSecret.ps1'),'function Get-FixtureSecret {
    <#
    .SYNOPSIS
    Return a private fixture value to verify compiled module scope.
    .DESCRIPTION
    Keep the integration or unit fixture deterministic and independent of the real user environment.
    #>
    "private"}')
        [IO.File]::WriteAllText((Join-Path $project 'src/Public/Get-FixtureValue.ps1'),'function Get-FixtureValue {
    <#
    .SYNOPSIS
    Read the fixture value through the selected module dependency.
    .DESCRIPTION
    Keep the integration or unit fixture deterministic and independent of the real user environment.
    #>
     [pscustomobject]@{Number=[FixtureValue]::new().Number;Resource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot "../../resources/value.txt"));State=$script:FixtureState;Dependency=Get-FixtureDependency;Local=Get-FixtureSecret} }')
        [IO.File]::WriteAllText((Join-Path $project 'Example.PoSh.psm1'),'foreach($category in @("Classes","Private","Public","Variables")){Get-ChildItem (Join-Path $PSScriptRoot "src/$category") -Filter *.ps1 | ForEach-Object {. $_.FullName}}; Export-ModuleMember -Function Get-FixtureValue -Variable FixtureState -Alias fixture')
        [IO.File]::WriteAllText((Join-Path $project 'Example.PoSh.psd1'),"@{RootModule='Example.PoSh.psm1';ModuleVersion='1.0';RequiredModules=@('Provider.PoSh');FunctionsToExport=@('Get-FixtureValue');VariablesToExport=@('FixtureState');AliasesToExport=@('fixture')}")
        $cache=Join-Path $root 'user/.posh.posh.compilations'
        $env:FRENCHEXDEV_MYDEVPOSH_PATH=Join-Path $root 'PoSh'
        $report=& $dev.ExportedFunctions['Get-DevPoSh'] -Action CompileAll -Path @($project,$provider,$devLibrary) -CompilationRoot $cache
        Assert-Runtime ($report.Outcome -eq 'Success' -and $report.Data.Compiled -eq 3) 'Generic project compilation failed.'
        $again=& $dev.ExportedFunctions['Get-DevPoSh'] -Action CompileAll -Path @($project,$provider,$devLibrary) -CompilationRoot $cache
        Assert-Runtime ($again.Data.Unchanged -eq 3) 'Generic compilation was not deterministic.'
        Move-Item -LiteralPath (Join-Path $project 'src') -Destination (Join-Path $project 'src.hidden')
        Move-Item -LiteralPath (Join-Path $project 'resources') -Destination (Join-Path $project 'resources.hidden')
        $script:genericArtifact=($report.Data.Items | Where-Object Project -eq 'Example.PoSh').Path
        $script:devArtifact=($report.Data.Items | Where-Object Project -eq 'DevPoSh.PoSh').Path
        $child=Invoke-RuntimeChild @'
param($Artifact)
Import-Module $Artifact
if(Get-Command Get-FixtureSecret -ErrorAction SilentlyContinue){throw 'Private function escaped.'}
Get-FixtureValue | ConvertTo-Json -Compress
'@ @($script:genericArtifact)
        Assert-Runtime ($child.Number -eq 42 -and $child.Resource -eq 'packaged resource' -and $child.State -eq 'state' -and $child.Dependency -eq 'dependency' -and $child.Local -eq 'private') 'Compiled source scope/resource or dependencies changed.'
    }
    Test-Runtime 'Development profile imports sources once and ignores compatibility manifests' {
        $fixture=Join-Path $root 'PoSh/FrenchExDev/FrenchExDev.OutputPumps.PoSh'
        if(Test-Path (Join-Path $fixture 'src.hidden')){Move-Item -LiteralPath (Join-Path $fixture 'src.hidden') -Destination (Join-Path $fixture 'src')}
        $project=Join-Path $root 'PoSh/Fixture/Example.PoSh'
        foreach($name in @('src','resources')){Move-Item -LiteralPath (Join-Path $project "$name.hidden") -Destination (Join-Path $project $name)}
        $profile=Join-Path $root 'dev-profile.ps1'
        & $dev.ExportedFunctions['Set-DevPoShPowershellProfile'] -Path $profile | Out-Null
        $child=Invoke-RuntimeChild @'
param($ProfileFile,$SourceRoot)
$env:FRENCHEXDEV_MYDEVPOSH_PATH=$SourceRoot;$env:FRENCHEXDEV_DEVPOSH='';$env:TERM_PROGRAM=''
. $ProfileFile
. $ProfileFile
[pscustomobject]@{Value=(Get-FixtureValue).Number;Pumps=@(Get-Module OutputPump.PoSh).Count;Compat=@(Get-Module OutputPumps).Count;Dev=@(Get-Module DevPoSh.PoSh).Count;Source=(Get-Module OutputPump.PoSh).Path} | ConvertTo-Json -Compress
'@ @($profile,(Join-Path $root 'PoSh')) -UserDirectory (Join-Path $root 'user')
        Assert-Runtime ($child.Value -eq 42 -and $child.Pumps -eq 1 -and $child.Compat -eq 0 -and $child.Dev -eq 1 -and $child.Source -eq (Join-Path $fixture 'OutputPump.PoSh.psm1')) 'Development imported duplicated or compiled modules.'
    }
    Test-Runtime 'Generated provider, private closures, live overrides and explicit transformations' {
        $external=Join-Path $root 'generated providers/FrenchExDev.OPumper.Custom.PoSh'
        $generated=New-OPumperModule -Name Custom -Executable custom -Path $external
        Assert-Runtime ($generated.Outcome -eq 'Success') 'Provider generation failed.'
        [IO.File]::WriteAllText((Join-Path $external 'resources/value.txt'),'resource two')
        [IO.File]::WriteAllText((Join-Path $external 'src/Private/Get-CustomValue.ps1'),'function Get-CustomValue {
    <#
    .SYNOPSIS
    Return deterministic business data from a custom module.
    .DESCRIPTION
    Keep the integration or unit fixture deterministic and independent of the real user environment.
    #>
    [IO.File]::ReadAllText((Join-Path $PSScriptRoot "../../resources/value.txt"))}')
        [IO.File]::WriteAllText((Join-Path $external 'src/Public/Invoke-PumpCustom.ps1'),'function Invoke-PumpCustom {
    <#
    .SYNOPSIS
    Build a custom pump result without executing a native command.
    .DESCRIPTION
    Keep the integration or unit fixture deterministic and independent of the real user environment.
    #>
    param($ArgumentList,$WorkingDirectory,$MaxItems=20,$MaxOutputBytes=8192,$TimeoutSeconds=600) New-OutputPumpResult -Summary "custom" -Data @{Value=(Get-CustomValue)}}')
        $config=Join-Path $root custom.yaml;[IO.File]::WriteAllText($config,"version: 2`nroots:`n - path: '$($external.Replace("'","''"))'`nproviders: {}`npumps: {}`n")
        $child=Invoke-RuntimeChild @'
param($Manifest,$Configuration)
Import-Module $Manifest -DisableNameChecking
$setup=Setup-OutputPumps -ConfigurationPath $Configuration
if($setup.Outcome -ne 'Success'){throw 'Setup failed.'}
$original=Pump-Me custom
$prefix='live:'
Register-OutputPump -Id custom.run -Route @{Executable='custom';Tokens=@();Options=@{};Positional='ArgumentList';Multiple=$true} -Replace -Handler {param($c) New-OutputPumpResult -Summary 'override' -Data @{Value=$prefix}}.GetNewClosure() | Out-Null
Setup-OutputPumps -ConfigurationPath $Configuration | Out-Null
if((Pump-Me custom).Data.Value -ne 'live:'){throw 'Live override lost.'}
Unregister-OutputPump custom.run | Out-Null
Register-OutputPump -Id custom.transform -TestInput {param($x) $null -ne $x.Data} -Transform {param($x) New-OutputPumpResult -Summary 'projected' -Data @{Value=($x.Data.Value+':transformed')}} | Out-Null
$value=Pump-Me custom | Pump-Me -PumpId custom.transform
if($original.Data.Value -ne 'resource two'){throw 'Private function scope lost.'}
$value | ConvertTo-OutputPumpJson
'@ @((Join-Path $library 'OutputPump.PoSh.psd1'),$config)
        Assert-Runtime ($child.Outcome -eq 'Success' -and $child.Data.Value -eq 'resource two:transformed') 'Contribution closure or pipeline failed.'
        $compiled=Get-DevPoSh -Action CompileAll -Path $external -CompilationRoot (Join-Path $root custom-compiled)
        Assert-Runtime ($compiled.Outcome -eq 'Success') 'Provider compilation failed.'
        $artifact=$compiled.Data.Items[0].Path
        $hidden=Join-Path $root 'generated providers/FrenchExDev.OPumper.Custom.PoSh.hidden'
        Assert-Runtime ($external.StartsWith($root) -and $hidden.StartsWith($root)) 'Unsafe fixture move.'
        Move-Item -LiteralPath $external -Destination $hidden
        $child=Invoke-RuntimeChild @'
param($Manifest,$Artifact,$Configuration)
Import-Module $Manifest -DisableNameChecking
$provider=Import-Module $Artifact -PassThru -DisableNameChecking
$before=$provider
$setup=Setup-OutputPumps -ConfigurationPath $Configuration
if($setup.Outcome -ne 'Success'){throw ('Compiled setup failed: '+$setup.Error.Message)}
if(![object]::ReferenceEquals($before,(Get-Module $provider.Name))){throw 'Provider reimported.'}
Pump-Me custom | ConvertTo-OutputPumpJson
'@ @((Join-Path $library 'OutputPump.PoSh.psd1'),$artifact,$config)
        Assert-Runtime ($child.Outcome -eq 'Success' -and $child.Data.Value -eq 'resource two') 'Compiled provider used missing sources or wrong resources.'
    }
    Test-Runtime 'Valid empty configuration supports live registrations without native discovery' {
        $config=Join-Path $root empty.yaml;[IO.File]::WriteAllText($config,"version: 2`nroots: []`nproviders: {}`npumps: {}`n")
        $child=Invoke-RuntimeChild @'
param($Manifest,$Configuration)
Import-Module $Manifest -DisableNameChecking
$setup=Setup-OutputPumps -ConfigurationPath $Configuration
if($setup.Outcome -ne 'Success'){throw 'Empty setup failed.'}
Get-OutputPump | ConvertTo-OutputPumpJson
'@ @((Join-Path $library 'OutputPump.PoSh.psd1'),$config)
        Assert-Runtime ($child.Outcome -eq 'Success' -and $child.Data.Items.Count -eq 0) 'Empty configuration not valid.'
    }
    Test-Runtime 'Copied Production profile loads artifacts without discovery or reconstruction' {
        $profile=Join-Path $root 'copied-profile.ps1'
        & $dev.ExportedFunctions['Set-DevPoShPowershellProfile'] -Path $profile | Out-Null
        $child=Invoke-RuntimeChild @'
param($ProfileFile,$SourceRoot,$ExpectedHome)
if($HOME -ne $ExpectedHome){throw 'Isolated user directory was not applied.'}
$env:FRENCHEXDEV_MYDEVPOSH_PATH=$SourceRoot;$env:FRENCHEXDEV_DEVPOSH='Production';$env:TERM_PROGRAM=''
# Provider inventory discovery is distinct from DevPoSh compilation.
function global:Get-DevPoSh {
    <#
    .SYNOPSIS
    Simulate module discovery for the copied profile startup scenario.
    .DESCRIPTION
    Keep the integration or unit fixture deterministic and independent of the real user environment.
    #>
    throw 'Production attempted compilation.'}
. $ProfileFile
$copied=Set-DevPoShPowershellProfile -Path (Join-Path $ExpectedHome 'from-compiled.ps1')
[pscustomobject]@{Value=(Get-FixtureValue).Number;Copied=$copied.Changed;DevModules=@(Get-Module DevPoSh.PoSh).Count} | ConvertTo-Json -Compress
'@ @($profile,(Join-Path $root 'PoSh'),(Join-Path $root 'user')) -UserDirectory (Join-Path $root 'user')
        Assert-Runtime ($child.Value -eq 42 -and $child.Copied -and $child.DevModules -eq 1) 'Production profile failed to load compiled resources.'
    }
    Test-Runtime 'Invalid candidate and interrupted compilation preserve the previous module' {
        $target=Join-Path $root 'stable.psm1'
        $first=& $dev.ExportedFunctions['Export-DevPoShCompilation'] -ScriptText 'function Get-Stable {
    <#
    .SYNOPSIS
    Provide a stable export for compilation change detection.
    .DESCRIPTION
    Keep the integration or unit fixture deterministic and independent of the real user environment.
    #>
    42}; Export-ModuleMember Get-Stable' -Destination $target
        Assert-Runtime ($first.Outcome -eq 'Success') 'Stable fixture could not be published.'
        $hash=(Get-FileHash $target).Hash
        $bad=& $dev.ExportedFunctions['Export-DevPoShCompilation'] -ScriptText 'throw "invalid module import"' -Destination $target
        Assert-Runtime ($bad.Outcome -eq 'Failed' -and (Get-FileHash $target).Hash -eq $hash) 'Failed validation replaced the artifact.'
        $lock=[IO.File]::Open("$target.lock",'OpenOrCreate','ReadWrite','None')
        try{
            $blocked=Invoke-RuntimeChild @'
param($Manifest,$Target)
Import-Module $Manifest -DisableNameChecking
Export-DevPoShCompilation -ScriptText '# changed' -Destination $Target | ConvertTo-Json -Compress
'@ @((Join-Path $devLibrary 'DevPoSh.PoSh.psd1'),$target)
            Assert-Runtime ($blocked.Outcome -eq 'Failed') 'Concurrent process write was accepted.'
        }finally{$lock.Dispose()}
        $worker=Join-Path $root 'interrupted.ps1'
        $signal=Join-Path $root 'candidate-started.txt'
        [IO.File]::WriteAllText($worker,'param($Manifest,$Target,$Signal); Import-Module $Manifest -DisableNameChecking; $candidate="[IO.File]::WriteAllText(''"+$Signal.Replace("''","''''")+"'',''started''); Start-Sleep -Seconds 60"; Export-DevPoShCompilation -ScriptText $candidate -Destination $Target | Out-Null')
        $capture=Invoke-RuntimeNative -Executable (Join-Path $PSHOME 'pwsh.exe') -Arguments @('-NoProfile','-File',$worker,(Join-Path $devLibrary 'DevPoSh.PoSh.psd1'),$target,$signal) -Timeout 5
        Assert-Runtime ($capture.TimedOut -and (Test-Path -LiteralPath $signal) -and (Get-FileHash $target).Hash -eq $hash) 'Interrupted compilation replaced the previous artifact or did not reach validation.'
    }
    Test-Runtime 'Missing production artifact is diagnosed while later modules still load' {
        $startupPath=Join-Path $root 'user/.posh.posh.compilations/startup.json'
        $startup=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($startupPath))
        $startup.Modules=@([pscustomobject]@{Project='Missing';Path=(Join-Path $root 'missing.psm1');SourceRoot=[IO.Path]::GetFullPath((Join-Path $root 'PoSh'))})+@($startup.Modules)
        [IO.File]::WriteAllText($startupPath,(ConvertTo-Json $startup -Depth 8))
        $child=Invoke-RuntimeChild @'
param($ProfileFile,$SourceRoot)
$env:FRENCHEXDEV_MYDEVPOSH_PATH=$SourceRoot;$env:FRENCHEXDEV_DEVPOSH='Production';$env:TERM_PROGRAM=''
# OutputPumps may refresh its loading inventory.
$warnings=@(& { . $ProfileFile } 3>&1)
[pscustomobject]@{Value=(Get-FixtureValue).Number;Warnings=$warnings.Count;RebuildAdvice=(@($warnings | Where-Object {"$_" -match 'Get-DevPoSh -Action CompileAll'}).Count -gt 0)} | ConvertTo-Json -Compress
'@ @((Join-Path $root 'copied-profile.ps1'),(Join-Path $root 'PoSh')) -UserDirectory (Join-Path $root 'user')
        Assert-Runtime ($child.Value -eq 42 -and $child.Warnings -eq 1 -and $child.RebuildAdvice) 'Missing artifact prevented later modules or omitted reconstruction advice.'
    }
    Test-Runtime 'Invalid source root skips all discovery with one short diagnostic' {
        $emptyUser=Join-Path $root 'empty-user';[void][IO.Directory]::CreateDirectory($emptyUser)
        $child=Invoke-RuntimeChild @'
param($ProfileFile,$InvalidRoot)
$env:FRENCHEXDEV_MYDEVPOSH_PATH=$InvalidRoot;$env:FRENCHEXDEV_DEVPOSH='';$env:TERM_PROGRAM=''
function global:Get-ChildItem {
    <#
    .SYNOPSIS
    Reject source traversal when checking production profile startup.
    .DESCRIPTION
    Keep the integration or unit fixture deterministic and independent of the real user environment.
    #>
    throw 'Invalid root caused discovery.'}
$warnings=@(& { . $ProfileFile } 3>&1)
[pscustomobject]@{Warnings=$warnings.Count;Loaded=@(Get-Module DevPoSh.PoSh,OutputPump.PoSh).Count;Encoding=[Console]::OutputEncoding.WebName} | ConvertTo-Json -Compress
'@ @((Join-Path $root 'copied-profile.ps1'),(Join-Path $root 'invalid-source-root')) -UserDirectory $emptyUser
        Assert-Runtime ($child.Warnings -eq 1 -and $child.Loaded -eq 0 -and $child.Encoding -eq 'utf-8') 'Invalid root did not preserve the profile contract.'
    }
} catch {$failures.Add(@{Test='Runtime setup';Message=$_.Exception.Message;Line=$_.InvocationInfo.ScriptLineNumber})}
finally {
    $env:FRENCHEXDEV_MYDEVPOSH_PATH=$oldSourceRoot
    $expected=[IO.Path]::GetFullPath((Join-Path $library '.cache'))+[IO.Path]::DirectorySeparatorChar
    if([IO.Path]::GetFullPath($root).StartsWith($expected,[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue}
}
& $core.ExportedFunctions['New-PumpResult'] -Summary 'OutputPump runtime integration completed.' -Outcome $(if($failures.Count){'Failed'}else{'Success'}) -Data @{Passed=$passed;Failed=$failures.Count;Items=$failures.ToArray()} -RetryAdvice $(if($failures.Count){'CorrectRequest'}else{'SafeToRetry'})
