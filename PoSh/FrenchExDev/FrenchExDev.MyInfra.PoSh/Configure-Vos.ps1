[CmdletBinding()]
param(
    [ValidateNotNullOrWhiteSpace()] [string] $Acme = "FrenchExDev",
    [ValidateNotNullOrWhiteSpace()] [string] $VosInstancePrefix = "fexdev-infra",
    [ValidateNotNullOrWhiteSpace()] [string] $NetworkConfig = "home",
    [ValidateNotNullOrWhiteSpace()] [string] $AlpineVersion = $AlpineSymbols.Versions.LatestEdge,
    [ValidateNotNullOrWhiteSpace()] [string] $Distro = "alpine",
    [ValidateNotNullOrWhiteSpace()] [string] $BoxName = "frenchexdev-alpine-v$($AlpineSymbols.Versions.LatestEdge)-$($AlpineSymbols.Flavors.Virt)-kubernetes",
    [ValidateNotNullOrWhiteSpace()] [string] $BoxVersion = "1.0.0",
    [int] $ControlPlanCpus = 2,
    [double] $ControlPlanMemory = 1GB,
    [double] $ControlPlanVideoMemory = 128MB,
    [int] $WorkerCpus = 6,
    [double] $WorkerMemory = 8GB,
    [double] $WorkerVideoMemory = 128MB,
    [switch] $WorkerVideo3D,
    [int] $JumpBoxCpus = 2,
    [double] $JumpBoxMemory = 512MB,
    [double] $JumpBoxVideoMemory = 128MB,
    [switch] $JumpBoxVideo3D,
    [ValidateNotNullOrWhiteSpace()] [string] $ConfigFile = $VosConfigSymbols.GlobalConfigFile,
    [ValidateNotNullOrWhiteSpace()] [string] $LocalConfigFile = $VosConfigSymbols.LocalConfigFile
)

$AcmeLowInv = $acme.ToLowerInvariant()

$InternalVerboseDebugConfig = @{
    Verbose = $VerbosePreference
    Debug   = $DebugPreference
}

$NetworkConfigObject = @{
    Configuration    = $NetworkConfig
    ScriptArgs       = "-VmPrefix $VosInstancePrefix"
    WorkingDirectory = $(Resolve-Path ./..).Path
}

$Network = Get-DevNetwork @NetworkConfigObject @InternalVerboseDebugConfig

$AlpineVersionConfig = @{
    Version = $AlpineVersion
    Flavor  = $AlpineSymbols.Flavors.Virt
}

$VosAlpineKubernetesConfig = @{
    ConfigFile      = $ConfigFile
    LocalConfigFile = $LocalConfigFile
    AlpineVersion   = $AlpineVersionConfig.Version
    VosPrefix       = $VosInstancePrefix
    PoShPath        = $(Resolve-Path ./../../posh)
    HostNames       = . $PSScriptRoot/Get-Hostnames.ps1 -Prefix $VosInstancePrefix
    Local           = { 
        param(
            [hashtable] $Hostnames
        )

        $JumpBoxConfig = @{
            BoxName     = "${AcmeLowInv}-${Distro}-v$($AlpineVersion)-virt-kubernetes-jumpbox"
            BoxVersion  = $BoxVersion
            Memory      = $JumpBoxMemory
            VideoMemory = 128MB
            Cpus        = $JumpBoxCpus
            Instances   = @(
                New-VosHostName -HostName "$($Hostnames.'jb-00')" -Network $Network
            )
        }

        $ControlPlanConfig = @{
            BoxName     = "${AcmeLowInv}-${Distro}-v$($AlpineVersion)-virt-kubernetes-controlplan"
            BoxVersion  = $BoxVersion
            Memory      = $ControlPlanMemory
            VideoMemory = $ControlPlanVideoMemory
            Cpus        = $ControlPlanCpus
            Instances   = @(
                New-VosHostName -HostName "$($Hostnames.'cp-00')" -Network $Network
                New-VosHostName -HostName "$($Hostnames.'cp-01')" -Network $Network
                New-VosHostName -HostName "$($Hostnames.'cp-02')" -Network $Network
            )
        }

        $WorkerConfig = @{
            BoxName     = "${AcmeLowInv}-${Distro}-v$($AlpineVersion)-virt-kubernetes-worker"
            BoxVersion  = $BoxVersion
            Memory      = $WorkerMemory
            VideoMemory = $WorkerVideoMemory
            Cpus        = $WorkerCpus
            Instances   = @(
                New-VosHostName -HostName "$($Hostnames.'worker-00')" -Network $Network
                New-VosHostName -HostName "$($Hostnames.'worker-01')" -Network $Network
                New-VosHostName -HostName "$($Hostnames.'worker-02')" -Network $Network
            )
        }

        @{
            machines = @{
                "cp"     = New-VosMachine @ControlPlanConfig @InternalVerboseDebugConfig
                "worker" = New-VosMachine @WorkerConfig @InternalVerboseDebugConfig
                "jb"     = New-VosMachine @JumpBoxConfig @InternalVerboseDebugConfig
            }
        }
    }.GetNewClosure()
    WorkerEnable3d  = $false
}

New-VosAlpineKubernetes @VosAlpineKubernetesConfig @InternalVerboseDebugConfig
