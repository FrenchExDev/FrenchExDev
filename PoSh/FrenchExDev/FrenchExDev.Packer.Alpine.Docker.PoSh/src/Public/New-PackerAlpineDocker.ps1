function New-PackerAlpineDocker {
    [CmdletBinding()]
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
        [long] $DiskAdditionalSize = 1TB,
        [switch] $Gui,
        [scriptblock] $PackerVBoxManageScript,
        [scriptblock] $ProvisionersScript,
        [scriptblock] $ProvisioningFilesScript
    )

    $AlpinePackerDockerHostConfigObjectConfig = @{
        Author                  = $Author
        Description             = "Alpine v$AlpineVersion ($Arch $Flavor) Docker Host w/ PowerShell v$PowerShellVersion"
        AlpineVersion           = $AlpineVersion
        Arch                    = $Arch
        Flavor                  = $Flavor
        Kind                    = "docker"
        Gui                     = $Gui
        PowerShellVersion       = $PowerShellVersion
        BoxVersion              = $BoxVersion
        OutputVagrantBoxName    = $OutputVagrantBoxName
        Cpus                    = $Cpus
        Memory                  = $Memory
        VideoMemory             = $VideoMemory
        DiskSize                = $DiskSize
        DiskAdditionalSize      = $DiskAdditionalSize
        PackerVBoxManageScript  = $PackerVBoxManageScript
        ProvisionersScript      = $ProvisionersScript 
        ProvisioningFilesScript = $ProvisioningFilesScript
    }

    $AlpineDockerHostPackerConfig = New-PackerAlpineDockerConfigObject @AlpinePackerDockerHostConfigObjectConfig

    New-PackerAlpine @AlpineDockerHostPackerConfig
}
