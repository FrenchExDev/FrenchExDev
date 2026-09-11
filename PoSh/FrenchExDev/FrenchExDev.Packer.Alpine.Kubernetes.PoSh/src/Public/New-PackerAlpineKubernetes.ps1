enum KubernetesKubeKind {
    JumpBox
    ControlPlan
    Worker
}

function New-PackerAlpineKubernetes {
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $AlpineVersion = $AlpineSymbols.Versions.LatestEdge,
        [ValidateNotNullOrWhiteSpace()] [string] $Arch = $AlpineSymbols.Architectures.x86_64,
        [ValidateNotNullOrWhiteSpace()] [string] $Flavor = $AlpineSymbols.Flavors.Virt,
        [ValidateNotNullOrWhiteSpace()] [string] $PowerShellVersion = $PowershellSymbols.Versions.LatestStable,
        [ValidateNotNullOrWhiteSpace()] [string] $BoxVersion = "1.0.0",
        [ValidateNotNullOrWhiteSpace()] [string] $OutputVagrantBoxName = "alpine-$($AlpineSymbols.Versions.LatestEdge)-v1.0.0",
        [ValidateNotNullOrWhiteSpace()] [string] $Author,
        [int] $Cpus = 4,
        [double] $Memory = 2GB,
        [double] $VideoMemory = 64MB,
        [long] $DiskSize = 20GB,
        [switch] $Gui,
        [scriptblock] $PackerVBoxManageScript,
        [scriptblock] $ProvisionersScript,
        [scriptblock] $ProvisioningFilesScript,
        [KubernetesKubeKind] $KubeKind
    )

    $PackerAlpineKubernetesConfigObjectConfig = @{
        Author                  = $Author
        Description             = "Alpine v$AlpineVersion ($Arch $Flavor) Kubernetes $KubeKind Host w/ PowerShell v$PowerShellVersion"
        AlpineVersion           = $AlpineVersion
        Arch                    = $Arch
        Flavor                  = $Flavor
        Kind                    = "kubernetes-$($KubeKind.ToString().ToLowerInvariant())"
        Gui                     = $Gui
        PowerShellVersion       = $PowerShellVersion
        BoxVersion              = $BoxVersion
        OutputVagrantBoxName    = $OutputVagrantBoxName
        Cpus                    = $Cpus
        Memory                  = $Memory
        VideoMemory             = $VideoMemory
        DiskSize                = $DiskSize
        PackerVBoxManageScript  = $PackerVBoxManageScript
        ProvisionersScript      = $ProvisionersScript
        ProvisioningFilesScript = $ProvisioningFilesScript
        KubeKind                = $KubeKind
    }

    $PackerAlpineKubernetesConfigObject = New-PackerAlpineKubernetesConfigObject @PackerAlpineKubernetesConfigObjectConfig

    New-PackerAlpine @PackerAlpineKubernetesConfigObject
}
