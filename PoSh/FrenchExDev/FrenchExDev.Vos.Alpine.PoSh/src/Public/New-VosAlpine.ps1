function New-VosAlpine {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $Path = ".",
        [ValidateNotNullOrWhiteSpace()] [string] $AlpineVersion,
        [switch] $NoEnablePoShPoSh,
        [int] $Zeroes = 2,
        [ValidateNotNullOrWhiteSpace()] [string] $VosPrefix,
        [ValidateNotNullOrWhiteSpace()] [string] $ProvisioningPath = "provisioning",
        [ValidateNotNullOrWhiteSpace()] [string] $ProvisioningVersion = "1",
        [scriptblock] $MachinesTypes,
        [scriptblock] $Machines,
        [scriptblock] $Commands,
        [scriptblock] $Variables,
        [scriptblock] $Vagrant,
        [scriptblock] $Local,
        [hashtable] $HostNames,
        [switch] $NoLinkedClones,
        [switch] $NoCheckGuestAdditions,
        [ValidateNotNullOrWhiteSpace()] [string] $ConfigFile = $VosConfigSymbols.GlobalConfigFile,
        [ValidateNotNullOrWhiteSpace()] [string] $LocalConfigFile = $VosConfigSymbols.LocalConfigFile
    )

    $InternalVerboseDebugConfig = @{
        Verbose = $VerbosePreference
        Debug   = $DebugPreference
    }

    if (!$NoEnablePoShPoSh) {
        New-File -Path "${ProvisioningPath}/alpine/${AlpineVersion}/${BoxVersion}/${ProvisioningVersion}" -Name "posh.posh.configure" -Extension sh -Content {
            New-DevPoShoShConfigureScriptContent
        } @InternalVerboseDebugConfig
    }
    
    $NewVosConfig = @{
        Zeroes          = $Zeroes
        MachinesTypes   = $MachinesTypes        
        Machines        = $Machines
        Vagrant         = {
            $VagrantPlugins = {
                @{
                    "vagrant-hostmanager" = New-VosConfigVagrantPlugin -Enable:$false -Config {
                        @{
                            enabled                    = $false
                            "hostmanager.enabled"      = $false
                            "hostmanager.manage_host"  = $false
                            "hostmanager.manage_guest" = $false
                        }
                    }
                    "vagrant-vbguest"     = New-VosConfigVagrantPlugin -Enable:$false -Config { 
                        @{
                            enabled                      = $false
                            "vbguest.auto_update"        = $false
                            "vbguest.auto_reboot"        = $false
                            "vbguest.installer_argumens" = @("--no-x11")
                        } 
                    }
                    "vagrant-env"         = [pscustomobject] @{
                        enabled = $false
                    }
                }
            }

            $VagrantVirtualBoxStorageAttach = { 
                @(,
                    @("--$($VirtualBoxSymbols.Storage.StorageCtl)", $VirtualBoxSymbols.Storage.SATAController, "--port", "0", "--$($VirtualBoxSymbols.Storage.NonRotational)", $VirtualBoxSymbols.Values.On)
                    #@("--$($VirtualBoxSymbols.Storage.StorageCtl)", $VirtualBoxSymbols.Storage.SATAController, "--port", "1", "--$($VirtualBoxSymbols.Storage.NonRotational)", $VirtualBoxSymbols.Values.On)
                )
            }

            $VagrantVirtualBoxStorageControl = {
                @(,
                    @("--name", $VirtualBoxSymbols.Storage.SATAController, "--$($VirtualBoxSymbols.Storage.HostIoCache)", $VirtualBoxSymbols.Values.On)
                )
            }

            $VagrantVirtualBoxSetProperty = {
                @{
                    hwvirtexclusive = $VirtualBoxSymbols.Values.On
                }
            }

            $VagrantVirtualBoxManageScript = {
                @{
                    "$($VirtualBoxSymbols.ModifyVm.CpuExecutionCap)"     = 100
                    "$($VirtualBoxSymbols.ModifyVm.NicPromisc)1"         = $VirtualBoxSymbols.NicPromisc.AllowAll
                    "$($VirtualBoxSymbols.ModifyVm.NicPromisc)2"         = $VirtualBoxSymbols.NicPromisc.AllowAll
                    "$($VirtualBoxSymbols.ModifyVm.NicPromisc)3"         = $VirtualBoxSymbols.NicPromisc.AllowAll
                    "$($VirtualBoxSymbols.ModifyVm.NatDnsHostResolver)1" = $VirtualBoxSymbols.Values.On
                    "$($VirtualBoxSymbols.ModifyVm.NatDnsProxy)1"        = $VirtualBoxSymbols.Values.On
                    "$($VirtualBoxSymbols.ModifyVm.IoApic)"              = $VirtualBoxSymbols.Values.On
                    "$($VirtualBoxSymbols.ModifyVm.HwVirtex)"            = $VirtualBoxSymbols.Values.On
                    "$($VirtualBoxSymbols.ModifyVm.HPet)"                = $VirtualBoxSymbols.Values.On
                    "$($VirtualBoxSymbols.ModifyVm.LargePages)"          = $VirtualBoxSymbols.Values.On
                    "$($VirtualBoxSymbols.ModifyVm.VtxVPid)"             = $VirtualBoxSymbols.Values.On
                    "$($VirtualBoxSymbols.ModifyVm.VtxUx)"               = $VirtualBoxSymbols.Values.On
                    "$($VirtualBoxSymbols.ModifyVm.Pae)"                 = $VirtualBoxSymbols.Values.On
                    "$($VirtualBoxSymbols.ModifyVm.Chipset)"             = $VirtualBoxSymbols.Chipsets.Ich9
                    "$($VirtualBoxSymbols.ModifyVm.BiosApic)"            = $VirtualBoxSymbols.ModifyVm.X2Apic
                    "$($VirtualBoxSymbols.ModifyVm.Vrde)"                = $VirtualBoxSymbols.Values.Off
                    "$($VirtualBoxSymbols.ModifyVm.Usb)"                 = $VirtualBoxSymbols.Values.Off
                    "$($VirtualBoxSymbols.ModifyVm.NestedHwVirt)"        = $VirtualBoxSymbols.Values.On
                    "$($VirtualBoxSymbols.ModifyVm.NestedPaging)"        = $VirtualBoxSymbols.Values.On
                    "$($VirtualBoxSymbols.ModifyVm.OsType)"              = $VirtualBoxSymbols.OsTypes.Linux64
                    "$($VirtualBoxSymbols.ModifyVm.VRam)"                = 128MB / 1MB
                    "$($VirtualBoxSymbols.ModifyVm.Accelerate3d)"        = $VirtualBoxSymbols.Values.Off
                    "$($VirtualBoxSymbols.ModifyVm.GraphicsController)"  = $VirtualBoxSymbols.Graphics.VmSvga
                }
            }.GetNewClosure()

            $NewVosConfigVagrantConfig = @{
                NamingPatternPrefix   = $VosPrefix
                Plugins               = $VagrantPlugins
                Manage                = $VagrantVirtualBoxManageScript
                StorageAttach         = $VagrantVirtualBoxStorageAttach
                StorageControl        = $VagrantVirtualBoxStorageControl
                SetProperty           = $VagrantVirtualBoxSetProperty
                NoLinkedClones        = $NoLinkedClones
                NoCheckGuestAdditions = $NoCheckGuestAdditions
            }

            if ($null -ne $Vagrant) {
                $NewVosConfigVagrantConfig = Invoke-Command $Vagrant -ArgumentList $NewVosConfigVagrantConfig
            }

            New-VosConfigVagrant @NewVosConfigVagrantConfig @InternalVerboseDebugConfig
        }.GetNewClosure()
        Local           = $Local
        HostNames       = $HostNames
        ConfigFile      = $ConfigFile
        LocalConfigFile = $LocalConfigFile
    }

    New-Vos @NewVosConfig @InternalVerboseDebugConfig
}