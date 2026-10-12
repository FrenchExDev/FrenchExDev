[CmdletBinding()]
param(
    [ValidateNotNullOrWhiteSpace()] [string] $Acme = "FrenchExDev",
    [ValidateNotNullOrWhiteSpace()] [string] $Author = "Stéphane Erard",
    [ValidateNotNullOrWhiteSpace()] [string] $Distro = "alpine",
    [ValidateNotNullOrWhiteSpace()] [string] $AlpineVersion = $AlpineSymbols.Versions.LatestEdge,
    [ValidateNotNullOrWhiteSpace()] [string] $BoxVersion = "1.0.0",
    [ValidateNotNullOrWhiteSpace()] [string] $PowerShellVersion = $PowershellSymbols.Versions.LatestStable,
    [switch] $Force,
    [switch] $Delete
)

$InternalVerboseDebugConfig = @{
    Verbose = $VerbosePreference
    Debug   = $DebugPreference
}

$AcmeLowInv = $Acme.ToLowerInvariant()

$AlpineVersionConfig = @{
    Version = $AlpineVersion
    Flavor  = $AlpineSymbols.Flavors.Virt
}

$DefaultPackerVmConfig = @{
    Cpus        = 6
    Memory      = 4GB
    VideoMemory = 128MB
}

$AlpineKindsSymbols = @{
    Default               = "default"
    Docker                = "docker"
    KubernetesJumpBox     = "kubernetes-$($KubernetesSymbols.MachinesKinds.JumpBox)"
    KubernetesControlPlan = "kubernetes-$($KubernetesSymbols.MachinesKinds.ControlPlan)"
    KubernetesWorker      = "kubernetes-$($KubernetesSymbols.MachinesKinds.Worker)"
}

$PackerAlpineConfig = @{
    Generator = {
        param([object] $Config)
        New-PackerAlpine @Config @InternalVerboseDebugConfig
    }.GetNewClosure()
    Config    = @{
        Author               = $Author
        AlpineVersion        = $AlpineVersionConfig.Version
        Flavor               = $AlpineVersionConfig.Flavor
        Cpus                 = $DefaultPackerVmConfig.Cpus
        Memory               = $DefaultPackerVmConfig.Memory
        VideoMemory          = $DefaultPackerVmConfig.VideoMemory
        PowerShellVersion    = $PowerShellVersion
        OutputVagrantBoxName = "${AcmeLowInv}-${Distro}-v$($AlpineVersionConfig.Version)-$($AlpineVersionConfig.Flavor)-$($AlpineKindsSymbols.Default)-v${BoxVersion}"
        BoxVersion           = $BoxVersion
        BoxKind              = "default"
        WorkingDirectory     = "packer/alpine-$($AlpineVersionConfig.Version)-$($AlpineVersionConfig.Flavor)-$($AlpineKindsSymbols.Default)"
        PackerFile           = "alpine.json"
        Description          = "Alpine $($AlpineVersion.Version) Default Host w/ PowerShell v$PowerShellVersion"
    }
}

$PackerAlpineDockerConfig = @{
    Generator = {
        param([object] $Config)
        New-PackerAlpineDocker @Config @InternalVerboseDebugConfig
    }.GetNewClosure()
    Config    = @{
        Author               = $Author
        AlpineVersion        = $AlpineVersionConfig.Version
        Flavor               = $AlpineVersionConfig.Flavor
        Cpus                 = $DefaultPackerVmConfig.Cpus
        Memory               = $DefaultPackerVmConfig.Memory
        VideoMemory          = $DefaultPackerVmConfig.VideoMemory
        PowerShellVersion    = $PowerShellVersion
        OutputVagrantBoxName = "${AcmeLowInv}-${Distro}-v$($AlpineVersionConfig.Version)-$($AlpineVersionConfig.Flavor)-$($AlpineKindsSymbols.Docker)-v${BoxVersion}"
        BoxVersion           = $BoxVersion
    }
}

$PackerAlpineKubernetesJumpBoxConfig = @{
    Generator = {
        param([object] $Config)
        New-PackerAlpineKubernetes @Config @InternalVerboseDebugConfig
    }.GetNewClosure()
    Config    = @{
        Author               = $Author
        AlpineVersion        = $AlpineVersionConfig.Version
        Flavor               = $AlpineVersionConfig.Flavor
        Cpus                 = $DefaultPackerVmConfig.Cpus
        Memory               = $DefaultPackerVmConfig.Memory
        VideoMemory          = $DefaultPackerVmConfig.VideoMemory
        PowerShellVersion    = $PowerShellVersion
        OutputVagrantBoxName = "${AcmeLowInv}-${Distro}-v$($AlpineVersionConfig.Version)-$($AlpineVersionConfig.Flavor)-$($AlpineKindsSymbols.KubernetesJumpBox)-v${BoxVersion}"
        BoxVersion           = $BoxVersion
        KubeKind             = $KubernetesSymbols.MachinesKinds.JumpBox
    }
}

$PackerAlpineKubernetesControlPlanConfig = @{
    Generator = {
        param([object] $Config)
        New-PackerAlpineKubernetes @Config @InternalVerboseDebugConfig
    }.GetNewClosure()
    Config    = @{
        Author               = $Author
        AlpineVersion        = $AlpineVersionConfig.Version
        Flavor               = $AlpineVersionConfig.Flavor
        Cpus                 = $DefaultPackerVmConfig.Cpus
        Memory               = $DefaultPackerVmConfig.Memory
        VideoMemory          = $DefaultPackerVmConfig.VideoMemory
        PowerShellVersion    = $PowerShellVersion
        OutputVagrantBoxName = "${AcmeLowInv}-${Distro}-v$($AlpineVersionConfig.Version)-$($AlpineVersionConfig.Flavor)-$($AlpineKindsSymbols.KubernetesControlPlan)-v${BoxVersion}"
        BoxVersion           = $BoxVersion
        KubeKind             = $KubernetesSymbols.MachinesKinds.ControlPlan
    }
}

$PackerAlpineKubernetesWorkerConfig = @{
    Generator = {
        param([object] $Config)
        New-PackerAlpineKubernetes @Config @InternalVerboseDebugConfig
    }.GetNewClosure()
    Config    = @{
        Author               = $Author
        AlpineVersion        = $AlpineVersionConfig.Version
        Flavor               = $AlpineVersionConfig.Flavor
        Cpus                 = $DefaultPackerVmConfig.Cpus
        Memory               = $DefaultPackerVmConfig.Memory
        VideoMemory          = $DefaultPackerVmConfig.VideoMemory
        PowerShellVersion    = $PowerShellVersion
        OutputVagrantBoxName = "${AcmeLowInv}-${Distro}-v$($AlpineVersionConfig.Version)-$($AlpineVersionConfig.Flavor)-$($AlpineKindsSymbols.KubernetesWorker)-v${BoxVersion}"
        BoxVersion           = $BoxVersion
        KubeKind             = $KubernetesSymbols.MachinesKinds.Worker
    }
}

foreach ($PackerConfig in @($PackerAlpineConfig, $PackerAlpineDockerConfig, $PackerAlpineKubernetesControlPlanConfig, $PackerAlpineKubernetesWorkerConfig, $PackerAlpineKubernetesJumpBoxConfig)) {
    Invoke-Command $($PackerConfig.Generator) -ArgumentList $($PackerConfig.Config)
}
