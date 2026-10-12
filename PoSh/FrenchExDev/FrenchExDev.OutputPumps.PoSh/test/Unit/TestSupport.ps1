<#
.SYNOPSIS
Shared offline fixtures for the OutputPumps unit suites.
.DESCRIPTION
Provide isolated configurations, registry resets and deterministic provider modules without changing the user profile.
#>


$script:Library=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$script:DevLibrary=Join-Path (Split-Path $script:Library) 'FrenchExDev.DevPoSh.PoSh'
$script:Cases=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText((Join-Path $script:Library 'test/Fixtures/native.json'))) -AsHashtable
$env:PSModulePath=(Join-Path $script:Library '.cache/Modules')+[IO.Path]::PathSeparator+$env:PSModulePath
function New-TestCapture([string]$Text='',[int]$ExitCode=0) {
    <#
    .SYNOPSIS
    Create an offline native-capture fixture.
    .DESCRIPTION
    Supply controlled output and exit status without starting an external process.
    #>
    
    [pscustomobject]@{Stdout=$Text;Stderr='';StartError=$null;ExitCode=$ExitCode;TimedOut=$false;Truncated=$false;StdoutCharacters=$Text.Length;StderrCharacters=0}
}
function New-TestConfiguration([string]$Directory,[string]$Library=$script:Library) {
    <#
    .SYNOPSIS
    Write an isolated YAML configuration for a test.
    .DESCRIPTION
    Resolve test roots explicitly and keep the real user configuration untouched.
    #>
    
    $path=Join-Path $Directory 'pumps.yaml'
    [IO.File]::WriteAllText($path,"version: 2`nroots:`n  - path: '$($Library.Replace("'","''"))'`n    enabled: true`npumps: {}`n")
    return $path
}
function Reset-TestOutputPumpSession {
    <#
    .SYNOPSIS
    Reset the shared registry between isolated tests.
    .DESCRIPTION
    Clear provider and initialization state so registrations from one test cannot affect another.
    #>
    
    $s=$global:FrenchExDevOutputPumpSession
    $s.Registry=@{};$s.Providers=@{};$s.Initialized=$false;$s.LastError=$null;$s.Transaction=$null;$s.Configuration=$null;$s.ConfigurationPath=$null
}
function New-TestOPumper([string]$Directory,[string]$Name='Fixture') {
    <#
    .SYNOPSIS
    Create a contributor fixture with a private helper.
    .DESCRIPTION
    Replace the scaffolded native pump with deterministic data to exercise module scope and registration offline.
    #>
    
    $path=Join-Path $Directory "FrenchExDev.OPumper.$Name.PoSh"
    $result=New-OPumperModule -Name $Name -Executable $Name.ToLowerInvariant() -Path $path
    if($result.Outcome -ne 'Success'){throw 'Fixture scaffold failed.'}
    $text=@'
function Invoke-Pump__NAME__ {
    <#
    .SYNOPSIS
    Return deterministic pump data while exercising a private provider helper.
    .DESCRIPTION
    Keep the integration or unit fixture deterministic and independent of the real user environment.
    #>
    
    param([string[]]$ArgumentList=@(),[string]$WorkingDirectory='.',[int]$MaxItems=20,[int]$MaxOutputBytes=8192,[int]$TimeoutSeconds=600)
    New-OutputPumpResult -Summary 'Fixture' -Data @{Branch=(Get-PrivateFixtureValue);Items=@(1,2)} -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes
}
'@
    [IO.File]::WriteAllText((Join-Path $path "src/Public/Invoke-Pump$Name.ps1"),$text.Replace('__NAME__',$Name))
    [IO.File]::WriteAllText((Join-Path $path 'src/Private/Get-PrivateFixtureValue.ps1'),"function Get-PrivateFixtureValue {
    <#
    .SYNOPSIS
    Return a private value to verify closure access to module scope.
    .DESCRIPTION
    Keep the integration or unit fixture deterministic and independent of the real user environment.
    #>
     'main' }")
    $path
}
function New-TestPumpLibrary([string]$Path) {
    <#
    .SYNOPSIS
    Create a minimal documentary catalogue fixture.
    .DESCRIPTION
    Write a small module and return its descriptor for registration and validation tests.
    #>
    
    [void][IO.Directory]::CreateDirectory((Join-Path $Path 'Modules'))
    $text=@'
function Get-FixturePump {
    <#
    .SYNOPSIS
    Return fixed business data for catalogue registration tests.
    .DESCRIPTION
    Keep the integration or unit fixture deterministic and independent of the real user environment.
    #>
     param([int]$MaxItems=20,[int]$MaxOutputBytes=8192,[int]$TimeoutSeconds=600,[string]$Path) [pscustomobject]@{ Value=42 } }
Export-ModuleMember -Function Get-FixturePump
'@
    [IO.File]::WriteAllText((Join-Path $Path 'Modules/Fixture.psm1'),$text)
    return @{Id='fixture';Name='Fixture';Purpose='Offline test fixture';Module='Modules/Fixture.psm1';Documentation=@('https://example.org/fixture');VerifiedVersion='1';IdentityMarkdown='Fixture for tests.';Commands=@(@{Id='fixture.run';Function='Get-FixturePump';When='Read fixture';Example='Get-FixturePump -Path .';Parameters=@('Path');Route=@{Executable='fixture';Tokens=@('run');Options=@{};Positional='Path';Multiple=$false}})}
}
