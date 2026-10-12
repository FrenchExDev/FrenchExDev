function New-PackerAlpineConfigObject {
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $Author,
        [ValidateNotNullOrWhiteSpace()] [string] $Description,
        [ValidateNotNullOrWhiteSpace()] [string] $Kind,
        [ValidateNotNullOrWhiteSpace()] [string] $AlpineVersion = $AlpineSymbols.Versions.LatestEdge,
        [ValidateNotNullOrWhiteSpace()] [string] $Arch = $AlpineSymbols.Architecture.x86_64,
        [ValidateNotNullOrWhiteSpace()] [string] $Flavor = $AlpineSymbols.Flavors.Virt,
        [ValidateNotNullOrWhiteSpace()] [string] $BoxVersion = "1.0.0",
        [ValidateNotNullOrWhiteSpace()] [string] $OutputVagrant = "output-vagrant",
        [ValidateNotNullOrWhiteSpace()] [string] $OutputVagrantBoxName = "alpine-$($AlpineSymbols.Versions.LatestEdge)-v1.0.0",
        [ValidateNotNullOrWhiteSpace()] [string] $CdnRepository = "nl.alpinelinux.org",
        [ValidateNotNullOrWhiteSpace()] [string] $PowerShellVersion = $PowershellSymbols.Versions.LatestStable,
        [int] $Cpus = 4,
        [double] $Memory = 2GB,
        [double] $VideoMemory = 64MB,
        [long] $DiskSize = 20GB,
        [switch] $Gui,
        [scriptblock] $PackerVBoxManageScript,
        [scriptblock] $ProvisionersScript,
        [scriptblock] $ProvisioningFilesScript,
        [string] $RootPasswd = "vagrant",
        [string] $SshPasswd = "vagrant",
        [string] $SshUsername = "vagrant"
    )

    $InternalProvisioningSympbols = @{
        CliUtils = "06cli-utils"
        PoSh     = "07posh"
    }

    $InternalProvisioningFilesScript = {
        param(
            [object] $ProvisioningFiles
        )

        $ProvisioningFiles."$($InternalProvisioningSympbols.CliUtils)" = New-BashFile -Code { @(
                $(New-ApkCommand Add -Package "jq", "yq")
            ) 
        } 

        $ProvisioningFiles."$($InternalProvisioningSympbols.PoSh)" = New-BashFile -Code { @(
                New-DevPoshPowershellInstallCommand -PowershellVersion $PowerShellVersion -Flavor "linux-musl-x64" -Dependencies {
                    $(New-ApkCommand Add -NoCache -Package "ca-certificates", "less", "ncurses-terminfo-base", "krb5-libs", "libgcc", "libintl", "libssl3", "libstdc++", "tzdata", "userspace-rcu", "zlib", "icu-libs", "curl")
                    $(New-ApkCommand Add -NoCache -Repository "https://dl-cdn.alpinelinux.org/alpine/edge/main" -Package "lttng-ust", "openssh-client")
                }
            ) }

        if ($null -ne $ProvisioningFilesScript) {
            $ProvisioningFiles = Invoke-Command $ProvisioningFilesScript -ArgumentList $ProvisioningFiles
        }

        $ProvisioningFiles
    }.GetNewClosure()

    $InternalProvisionersScript = {
        param([object] $scripts)
        $scripts.additional = @(
            "scripts/$($InternalProvisioningSympbols.CliUtils).sh"
            "scripts/$($InternalProvisioningSympbols.PoSh).sh"
        )

        if ($null -ne $ProvisionersScript) {
            $scripts = Invoke-Command $ProvisionersScript -ArgumentList $scripts
        }

        $scripts
    }.GetNewClosure()

    $InternalPackerVirtualBoxManageScript = {
        $PackerVirtualBoxManageConfig = @{
            Cpus                   = New-PackerTemplateString "cpus"
            Memory                 = New-PackerTemplateString "memory"
            VideoMemory            = New-PackerTemplateString "video_memory"
            IoApic                 = $true
            HwVirtEx               = $true
            HPet                   = $true
            LargePages             = $true
            VtxUx                  = $true
            VtxVPid                = $true
            Pae                    = $true
            Acpi                   = $true
            PageFusion             = $true
            Chipset                = $VirtualBoxSymbols.Chipsets.Ich9
            Vrde                   = $false
            Usb                    = $false
            NestedHwVirt           = $true
            NestedPaging           = $true
            ParaVirtProvider       = $VirtualBoxSymbols.ParaVirtProvider.Kvm
            OsType                 = $VirtualBoxSymbols.OsTypes.Linux64
            GraphicsController     = $VirtualBoxSymbols.Graphics.VBoxSvga
            Nat1LocalHostReachable = $true
            Nat1DnsHostResolver    = $true
            Nat1DnsProxy           = $true
            HwVirtExclusive        = $true
            Sata                   = $true
            SataHostIoCache        = $true
            SataNonRotational      = $true
            SataDiscard            = $true
        }

        if ($null -ne $PackerVBoxManageScript) {
            $PackerVirtualBoxManageConfig = Invoke-Command $PackerVBoxManageScript -ArgumentList $PackerVirtualBoxManageConfig
        }

        New-PackerVirtualBoxManageConfig @PackerVirtualBoxManageConfig
    }

    @{
        Description             = "$Description"
        WorkingDirectory        = "./packer/alpine-${AlpineVersion}-${Flavor}-${Kind}"
        PackerFile              = "alpine.json"
        Cpus                    = $Cpus
        Memory                  = $Memory
        VideoMemory             = $VideoMemory
        DiskSize                = $DiskSize
        CdnRepository           = $CdnRepository
        BoxKind                 = "$Kind"
        BoxVersion              = $BoxVersion
        OutputVagrantBoxName    = $OutputVagrantBoxName
        AlpineVersion           = $AlpineVersion
        Arch                    = $Arch
        Flavor                  = $Flavor
        OutputVagrant           = $OutputVagrant
        RootPasswd              = "$RootPasswd"
        SshPasswd               = "$SshPasswd"
        SshUsername             = "$SshUsername"
        Author                  = "$Author"
        ProvisioningFilesScript = $InternalProvisioningFilesScript
        ProvisionersScript      = $InternalProvisionersScript
        VBoxManageScript        = $InternalPackerVirtualBoxManageScript
    }
}