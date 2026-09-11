[CmdletBinding()]
param(
    [ValidateNotNullOrWhiteSpace()] [string] $Acme = "FrenchExDev",
    [ValidateNotNullOrWhiteSpace()] [string] $Domain = "frenchexdev",
    [ValidateNotNullOrWhiteSpace()] [string] $DomainTld = "lab",
    [ValidateNotNullOrWhiteSpace()] [string] $Author = "Stéphane Erard",
    [ValidateNotNullOrWhiteSpace()] [string] $Distro = "alpine",
    [ValidateNotNullOrWhiteSpace()] [string] $VosInstancePrefix = "fexdev-infra",
    [ValidateNotNullOrWhiteSpace()] [string] $NetworkConfig = "home",
    [ValidateNotNullOrWhiteSpace()] [string] $AlpineVersion = $AlpineSymbols.Versions.LatestEdge,
    [ValidateNotNullOrWhiteSpace()] [string] $BoxVersion = "1.0.0",
    [ValidateNotNullOrWhiteSpace()] [string] $LogLevel = "DEBUG",
    [ValidateNotNullOrWhiteSpace()] [string] $TimeZone = "Europe/Paris",
    [ValidateNotNullOrWhiteSpace()] [string] $DefaultDockerComposeContext = "default",
    [ValidateNotNullOrWhiteSpace()] [string] $DockerComposeProjectName = "dc",
    [ValidateNotNullOrWhiteSpace()] [string] $PowerShellVersion = $PowershellSymbols.Versions.LatestStable,
    [switch] $Force,
    [switch] $Delete
)

$InternalVerboseDebugConfig = @{
    Verbose = $VerbosePreference
    Debug   = $DebugPreference
}

if ($Delete -or -not (Test-Path "./src")) {
    if (Test-Path src) {
        Remove-Item -Recurse -Force src | Out-Null
    }
    New-Item -ItemType Directory src | Out-Null
}

. $PSScriptRoot/Configure-Network.ps1

Push-Location src; 

$ConfigurePacker = @{
    Acme          = $Acme
    Author        = $Author
    Distro        = $Distro
    AlpineVersion = $AlpineVersion
    BoxVersion    = $BoxVersion
}

. $PSScriptRoot/Configure-Packer.ps1 @ConfigurePacker @InternalVerboseDebugConfig

$ConfigureVos = @{
    Acme                   = $Acme
    VosInstancePrefix      = $VosInstancePrefix
    NetworkConfig          = $NetworkConfig
    AlpineVersion          = $AlpineVersion
    Distro                 = $Distro
    BoxName                = "$($Acme.ToLowerInvariant())-$($Distro)-v$($AlpineVersion)-virt-kubernetes"
    BoxVersion             = $BoxVersion
    ControlPlanCpus        = 2
    ControlPlanMemory      = 512MB
    ControlPlanVideoMemory = 128MB
    WorkerCpus             = 6
    WorkerMemory           = 8GB
    WorkerVideoMemory      = 128MB
    WorkerVideo3D          = $false
    JumpBoxCpus            = 1
    JumpBoxMemory          = 512MB
    JumpBoxVideoMemory     = 128MB
    JumpBoxVideo3D         = $false
}

. $PSScriptRoot/Configure-Vos.ps1 @ConfigureVos @InternalVerboseDebugConfig

$ConfigureDos = @{
    Domain                   = $Domain
    DomainTld                = $DomainTld
    LogLevel                 = $LogLevel
    TimeZone                 = $TimeZone
    DockerComposeProjectName = $DockerComposeProjectName
}

. $PSScriptRoot/Configure-Dos.ps1 @ConfigureDos @InternalVerboseDebugConfig

$ConfigureSsl = @{
    Force             = $Force
    Domain            = "$Domain"
    VosInstancePrefix = $VosInstancePrefix
}

. $PSScriptRoot/Configure-Ssl.ps1 @ConfigureSsl @InternalVerboseDebugConfig

Pop-Location
