function New-PackerAlpineDockerConfigObject {
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $Kind = "docker",
        [ValidateNotNullOrWhiteSpace()] [string] $Description,
        [ValidateNotNullOrWhiteSpace()] [string] $AlpineVersion = $AlpineSymbols.Versions.LatestEdge,
        [ValidateNotNullOrWhiteSpace()] [string] $Arch = $AlpineSymbols.Architectures.x86_64,
        [ValidateNotNullOrWhiteSpace()] [string] $Flavor = $AlpineSymbols.Flavors.Virt,
        [ValidateNotNullOrWhiteSpace()] [string] $BoxVersion = "1.0.0",
        [ValidateNotNullOrWhiteSpace()] [string] $OutputVagrantBoxName = "alpine-$($AlpineSymbols.Versions.LatestEdge)-v1.0.0",
        [ValidateNotNullOrWhiteSpace()] [string] $PowerShellVersion = $PowershellSymbols.Versions.LatestStable,
        [ValidateNotNullOrWhiteSpace()] [string] $CdnRepository = "nl.alpinelinux.org",
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

    $InternalFilesSymbols = @{
        docker = "06docker"
    }

    $InternalProvisioningFilesScript = {
        param(
            [object] $ProvisioningFiles
        )

        $ProvisioningFiles."$($InternalFilesSymbols.docker)" = New-BashFile -Code { @(
                "$(New-ApkCommand Add -Package "docker", "docker-cli-compose", "openrc")"
                'rc-update add docker boot'
                'addgroup vagrant docker'
                'service docker start'
            )
        }

        if ($null -ne $ProvisioningFilesScript) {
            $ProvisioningFiles = Invoke-Command $ProvisioningFilesScript -ArgumentList $ProvisioningFiles
        }

        $ProvisioningFiles
    }.GetNewClosure()

    $InternalProvisionersScript = {
        param([object] $scripts)
        $scripts.additional += @("scripts/$($InternalFilesSymbols.docker).sh")

        if ($null -ne $ProvisionersScript) {
            $scripts = Invoke-Command $ProvisionersScript -ArgumentList $scripts
        }

        $scripts
    }.GetNewClosure()

    $PackerAlpineConfigObjectConfig = @{
        Author                  = "$Author"
        Description             = "$Description"
        Kind                    = $Kind
        AlpineVersion           = $AlpineVersion
        Arch                    = $Arch
        Flavor                  = $Flavor
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
        ProvisionersScript      = $InternalProvisionersScript
        ProvisioningFilesScript = $InternalProvisioningFilesScript
        RootPasswd              = "vagrant"
        SshPasswd               = "vagrant"
        SshUsername             = "vagrant"
    }

    New-PackerAlpineConfigObject @PackerAlpineConfigObjectConfig
}
