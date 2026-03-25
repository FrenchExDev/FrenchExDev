function New-VosAlpineKubernetes {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $Path = ".",
        [ValidateNotNullOrWhiteSpace()] [string] $AlpineVersion,
        [switch] $NoEnablePoShPoSh,
        [int] $Zeroes = 2,
        [ValidateNotNullOrWhiteSpace()][string] $VosPrefix,
        [hashtable] $HostNames,
        [scriptblock] $MachinesTypes,
        [scriptblock] $Machines,
        [scriptblock] $SharedFolders,
        [scriptblock] $Commands,
        [scriptblock] $Variables,
        [scriptblock] $JumpBoxHostMachineTypeConfig,
        [scriptblock] $ControlPlanHostMachineTypeConfig,
        [scriptblock] $WorkerHostMachineTypeConfig,
        [scriptblock] $Vagrant,
        [scriptblock] $Local,
        [switch] $NoLinkedClones,
        [switch] $NoCheckGuestAdditions,
        [int] $JumpBoxCpus = 2,
        [double] $JumpBoxMemory = 1GB,
        [double] $JumpBoxVideoMemory = 128MB,
        [switch] $JumpBoxVideo3D,
        [int] $ControlPlanCpus = 2,
        [double] $ControlPlanMemory = 1GB,
        [double] $ControlPlanVideoMemory = 128MB,
        [int] $WorkerCpus = 6,
        [double] $WorkerMemory = 8GB,
        [double] $WorkerVideoMemory = 128MB,
        [switch] $WorkerEnable3d,
        [ValidateNotNullOrWhiteSpace()] [string] $PoShPath,
        [ValidateNotNullOrWhiteSpace()] [string] $ProvisioningPath = "provisioning",
        [ValidateNotNullOrWhiteSpace()] [string] $ConfigFile = $VosConfigSymbols.GlobalConfigFile,
        [ValidateNotNullOrWhiteSpace()] [string] $LocalConfigFile = $VosConfigSymbols.LocalConfigFile
    )

    $InternalMachinesTypesSymbols = @{
        ControlPlan = "kubernetes-$($KubernetesSymbols.MachinesKinds.ControlPlan)-host"
        Worker      = "kubernetes-$($KubernetesSymbols.MachinesKinds.Worker)-host"
        JumpBox     = "kubernetes-$($KubernetesSymbols.MachinesKinds.jumpbox)-host"
    }

    $NewVosAlpineConfig = @{
        Path                  = $Path
        AlpineVersion         = $AlpineVersion
        NoEnablePoShPoSh      = [bool] $NoEnablePoShPoSh.IsPresent
        Zeroes                = $Zeroes
        VosPrefix             = "$VosPrefix"
        MachinesTypes         = {
            $InternalProvisioningScript = {
                $InternalProvisioning = if (!$NoEnablePoShPoSh) {
                    @{
                        "posh.posh.configure" = New-VosConfigMachineProvisioning -Enabled -Version 1 -Extension sh
                    } 
                }
                else { @{} }

                if ($null -ne $Provisioning) {
                    $InternalProvisioning = Invoke-Command $Provisioning -ArgumentList $InternalProvisioning
                }

                $InternalProvisioning
            }

            $InternalSharedFoldersScript = {
                $InternalSharedFolders = if (!$NoEnablePoShPoSh) {
                    @{
                        "posh" = New-VosConfigMachineSharedFolder -Enable -GuestPath "/posh" -HostPath $PoShPath
                    }
                }

                if ($null -ne $SharedFolders) {
                    $InternalSharedFolders = Invoke-Command $SharedFolders -ArgumentList $InternalSharedFolders
                }

                $InternalSharedFolders
            }

            $InternalCommandsScript = {
                $InternalCommands = @{
                    "get-ip" = 'sudo ip -f inet addr show ##INTERFACE-ALIAS## | sed -En -e ''s/.*inet ([0-9.]+).*/\1/p'''
                }

                if ($null -ne $Commands) {
                    $InternalCommands = Invoke-Command $Commands -ArgumentList $InternalCommands
                }

                $InternalCommands
            }

            $InternalVariablesScript = {
                $InternalVariables = @{
                    "public.interfaces" = @("eth1", "eth2")
                }

                if ($null -ne $Variables) {
                    $InternalVariables = Invoke-Command $Variables -ArgumentList $InternalVariables
                }

                $InternalVariables
            }

            $KubernetesJumpBoxHostVosConfigMachineTypeConfig = @{
                IsPrimary        = $true
                BoxName          = $BoxName
                BoxVersion       = $BoxVersion
                BoxUrl           = $BoxUrl
                OsType           = $VirtualBoxSymbols.OsTypes.Linux64
                Enable3d         = $JumpBoxVideo3D
                VideoMemory      = [int] $($JumpBoxVideoMemory / 1MB)
                Ram              = [int] $($JumpBoxMemory / 1MB)
                Vcpus            = $JumpBoxCpus
                Gui              = $false
                Provider         = $VagrantSymbols.Providers.VirtualBox
                Commands         = $InternalCommandsScript
                Variables        = $InternalVariablesScript
                SharedFolders    = $InternalSharedFoldersScript
                Provisioning     = $InternalProvisioningScript
                ProvisioningPath = "$ProvisioningPath/alpine/$AlpineVersion"
            }
            
            if ($null -ne $JumpBoxHostMachineTypeConfig) {
                $KubernetesJumpBoxHostVosConfigMachineTypeConfig = Invoke-Command $JumpBoxHostMachineTypeConfig -ArgumentList $KubernetesJumpBoxHostVosConfigMachineTypeConfig
            }

            $KubernetesControlPlanHostVosConfigMachineTypeConfig = @{
                IsPrimary        = $false
                BoxName          = $BoxName
                BoxVersion       = $BoxVersion
                BoxUrl           = $BoxUrl
                OsType           = $VirtualBoxSymbols.OsTypes.Linux64
                Enable3d         = $false
                VideoMemory      = [int] $($ControlPlanVideoMemory / 1MB)
                Ram              = [int] $($ControlPlanMemory / 1MB)
                Vcpus            = $ControlPlanCpus
                Gui              = $false
                Provider         = $VagrantSymbols.Providers.VirtualBox
                Commands         = $InternalCommandsScript
                Variables        = $InternalVariablesScript
                SharedFolders    = $InternalSharedFoldersScript
                Provisioning     = $InternalProvisioningScript
                ProvisioningPath = "$ProvisioningPath/alpine/$AlpineVersion"
            }

            if ($null -ne $ControlPlanHostMachineTypeConfig) {
                $KubernetesControlPlanHostVosConfigMachineTypeConfig = Invoke-Command $ControlPlanHostMachineTypeConfig -ArgumentList $KubernetesControlPlanHostVosConfigMachineTypeConfig
            }

            $KubernetesWorkerHostVosConfigMachineTypeConfig = @{
                IsPrimary        = $false
                BoxName          = $BoxName
                BoxVersion       = $BoxVersion
                BoxUrl           = $BoxUrl
                OsType           = $VirtualBoxSymbols.OsTypes.Linux64
                Enable3d         = $WorkerEnable3d
                VideoMemory      = [int] $($WorkerVideoMemory / 1MB)
                Ram              = [int] $($WorkerMemory / 1MB)
                Vcpus            = $Cpus
                Gui              = $false
                Provider         = $VagrantSymbols.Providers.VirtualBox
                Commands         = $InternalCommandsScript
                Variables        = $InternalVariablesScript
                SharedFolders    = $InternalSharedFoldersScript
                Provisioning     = $InternalProvisioningScript
                ProvisioningPath = "$ProvisioningPath/alpine/$AlpineVersion"
            }

            if ($null -ne $WorkerHostMachineTypeConfig) {
                $KubernetesWorkerHostVosConfigMachineTypeConfig = Invoke-Command $WorkerHostMachineTypeConfig -ArgumentList $KubernetesWorkerHostVosConfigMachineTypeConfig
            }

            $InternalMachinesTypes = @{
                "$($InternalMachinesTypesSymbols.ControlPlan)" = New-VosConfigMachineType @KubernetesControlPlanHostVosConfigMachineTypeConfig @InternalVerboseDebugConfig
                "$($InternalMachinesTypesSymbols.Worker)"      = New-VosConfigMachineType @KubernetesWorkerHostVosConfigMachineTypeConfig @InternalVerboseDebugConfig
                "$($InternalMachinesTypesSymbols.JumpBox)"     = New-VosConfigMachineType @KubernetesJumpBoxHostVosConfigMachineTypeConfig @InternalVerboseDebugConfig
            }

            if ($null -ne $MachinesTypes) {
                $InternalMachinesTypes = Invoke-Command $MachinesTypes -ArgumentList $InternalMachinesTypes
            }

            $InternalMachinesTypes
        }.GetNewClosure()
        Machines              = {
            $InternalMachines = @{
                "cp"     = New-VosConfigMachine -Enable -MachineTypeName "$($InternalMachinesTypesSymbols.ControlPlan)"
                "worker" = New-VosConfigMachine -Enable -MachineTypeName "$($InternalMachinesTypesSymbols.Worker)"
                "jb"     = New-VosConfigMachine -Enable -MachineTypeName "$($InternalMachinesTypesSymbols.JumpBox)"
            }

            if ($null -ne $Machines) {
                $InternalMachines = Invoke-Command $Machines -ArgumentList $InternalMachines
            }

            $InternalMachines
        }.GetNewClosure()
        Commands              = $Commands
        Variables             = $Variables
        Vagrant               = $Vagrant
        NoLinkedClones        = $NoLinkedClones
        NoCheckGuestAdditions = $NoCheckGuestAdditions
        Local                 = $Local
        HostNames             = $HostNames
        ConfigFile            = $ConfigFile
        LocalConfigFile       = $LocalConfigFile
    }

    New-VosAlpine @NewVosAlpineConfig
}
