enum KubernetesKubeKind {
    JumpBox
    ControlPlan
    Worker
}

function New-PackerAlpineKubernetesConfigObject {
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $Description,
        [ValidateNotNullOrWhiteSpace()] [string] $Kind = "kubernetes",
        [ValidateNotNullOrWhiteSpace()] [string] $Author,
        [ValidateNotNullOrWhiteSpace()] [string] $AlpineVersion = $AlpineSymbols.Versions.LatestEdge,
        [ValidateNotNullOrWhiteSpace()] [string] $Arch = $AlpineSymbols.Architectures.x86_64,
        [ValidateNotNullOrWhiteSpace()] [string] $Flavor = $AlpineSymbols.Flavors.Virt,
        [ValidateNotNullOrWhiteSpace()] [string] $PowerShellVersion = $PowershellSymbols.Versions.LatestStable,
        [ValidateNotNullOrWhiteSpace()] [string] $BoxVersion = "1.0.0",
        [ValidateNotNullOrWhiteSpace()] [string] $CdnRepository = "nl.alpinelinux.org",
        [ValidateNotNullOrWhiteSpace()] [string] $OutputVagrantBoxName = "alpine-$($AlpineSymbols.Versions.LatestEdge)-v1.0.0",
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

    $InternalFilesSymbols = @{
        k8s = "08kubernetes"
    }

    $InternalProvisionersScript = {
        param([object] $scripts)
        $scripts.additional += @("scripts/$($InternalFilesSymbols.k8s).sh")

        if ($null -ne $ProvisionersScript) {
            $scripts = Invoke-Command $ProvisionersScript -ArgumentList $scripts
        }

        $scripts
    }.GetNewClosure()

    $InternalProvisioningFilesScript = {
        param(
            [object] $ProvisioningFiles
        )

        switch ($KubeKind) {
            ([KubernetesKubeKind]::JumpBox) {
                $ProvisioningFiles."$($InternalFilesSymbols.k8s)" = New-BashFile -Parameters "+eux" -Code { @(
                        "$(New-ApkCommand Add -NoCache -Package "kubectl","etcd-ctl","helm","k9s")"
                    ) -Join [System.Environment]::NewLine }
            }
            ([KubernetesKubeKind]::ControlPlan) {
                $ProvisioningFiles."$($InternalFilesSymbols.k8s)" = New-BashFile -Parameters "+eux" -Code { @(
                        "$(New-ApkCommand Add -NoCache -Package "etcd","kube-apiserver","kube-controller-manager", "kube-scheduler")"
                    ) -Join [System.Environment]::NewLine }
            }
            ([KubernetesKubeKind]::Worker) {
                $ProvisioningFiles."$($InternalFilesSymbols.k8s)" = New-BashFile -Parameters "+eux" -Code { @(
                        "$(New-ApkCommand Add -NoCache -Package "cri-tools","containerd","cni-plugins","kubelet","kube-proxy", "runc")"
                    ) -Join [System.Environment]::NewLine }
            }
            default {
                throw "KubeKind: '$KubeKind' has not yet been implemented"
            }
        }
        
        if ($null -ne $ProvisioningFilesScript) {
            $ProvisioningFiles = Invoke-Command $ProvisioningFilesScript -ArgumentList $ProvisioningFiles
        }

        $ProvisioningFiles
    }.GetNewClosure()

    $PackerAlpineDockerConfigObjectConfig = @{
        Author                  = $Author
        Kind                    = $Kind
        Description             = $Description
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
        PackerVBoxManageScript  = $PackerVBoxManageScript
        ProvisionersScript      = $InternalProvisionersScript
        ProvisioningFilesScript = $InternalProvisioningFilesScript
        RootPasswd              = "vagrant"
        SshPasswd               = "vagrant"
        SshUsername             = "vagrant"
    }

    New-PackerAlpineConfigObject @PackerAlpineDockerConfigObjectConfig
}
