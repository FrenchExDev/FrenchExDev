function New-VosAlpineDocker {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $Path = ".",
        [ValidateNotNullOrWhiteSpace()] [string] $AlpineVersion,
        [switch] $NoEnablePoShPoSh,
        [int] $Zeroes = 2,
        [ValidateNotNullOrWhiteSpace()][string] $VosPrefix,
        [ValidateNotNullOrWhiteSpace()] [string] $BoxName,
        [string] $BoxVersion,
        [string] $BoxUrl,
        [int] $Cpus,
        [double] $Memory,
        [double] $VideoMemory,
        [scriptblock] $MachinesTypes,
        [scriptblock] $Machines,
        [scriptblock] $Provisioning,
        [scriptblock] $SharedFolders,
        [scriptblock] $Commands,
        [scriptblock] $Variables,
        [scriptblock] $DockerHostMachineTypeConfig,
        [scriptblock] $Vagrant,
        [scriptblock] $Local,
        [switch] $NoLinkedClones,
        [switch] $NoCheckGuestAdditions,
        [ValidateNotNullOrWhiteSpace()] [string] $ProvisioningPath = "provisioning",
        [ValidateNotNullOrWhiteSpace()] [string] $PoShPath,
        [ValidateNotNullOrWhiteSpace()] [string] $KubernetesHostPath = "./k8s",
        [ValidateNotNullOrWhiteSpace()] [string] $KubernetesGuestPath = "/k8s",
        [ValidateNotNullOrWhiteSpace()] [string] $ConfigFile = $VosConfigSymbols.GlobalConfigFile,
        [ValidateNotNullOrWhiteSpace()] [string] $LocalConfigFile = $VosConfigSymbols.LocalConfigFile
    )

    $NewVosAlpineConfig = @{
        Path                        = $Path
        AlpineVersion               = $AlpineVersion
        NoEnablePoShPoSh            = [bool] $NoEnablePoShPoSh.IsPresent
        Zeroes                      = $Zeroes
        VosPrefix                   = "$VosPrefix"
        BoxName                     = $BoxName
        BoxVersion                  = $BoxVersion
        BoxUrl                      = $BoxUrl
        Cpus                        = $cpus
        Memory                      = $Memory
        VideoMemory                 = $VideoMemory
        MachinesTypes               = {
            $InternalProvisioningScript = {
                $InternalProvisioning = if (-not $NoEnablePoShPoSh) {
                    @{
                        "posh.posh.configure" = New-VosConfigMachineProvisioning -Enabled -Version 1 -BoxVersion $BoxVersion -Extension sh
                    } 
                }
                else { @{} }

                if ($null -ne $Provisioning) {
                    $InternalProvisioning = Invoke-Command $Provisioning -ArgumentList $InternalProvisioning
                }

                $InternalProvisioning
            }

            $InternalSharedFoldersScript = {
                $InternalSharedFolders = @{}

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

            $DockerHostVosConfigMachineTypeConfig = @{
                IsPrimary        = $true
                BoxName          = $BoxName
                BoxVersion       = $BoxVersion
                BoxUrl           = $BoxUrl
                OsType           = $VirtualBoxSymbols.OsTypes.Linux64
                Enable3d         = $false
                VideoMemory      = $VideoMemory / 1MB
                Ram              = $Memory / 1MB
                Vcpus            = $Cpus
                Gui              = $false
                Provider         = $VagrantSymbols.Providers.VirtualBox
                Commands         = $InternalCommandsScript
                Variables        = $InternalVariablesScript
                SharedFolders    = $InternalSharedFoldersScript
                Provisioning     = $InternalProvisioningScript
                ProvisioningPath = "$ProvisioningPath/alpine/$AlpineVersion"
            }

            if ($null -ne $DockerHostMachineTypeConfig) {
                $DockerHostVosConfigMachineTypeConfig = Invoke-Command $DockerHostMachineTypeConfig -ArgumentList $DockerHostVosConfigMachineTypeConfig
            }

            $InternalMachinesTypes = @{
                "docker-host" = New-VosConfigMachineType @DockerHostVosConfigMachineTypeConfig    
            }

            if ($null -ne $MachinesTypes) {
                $InternalMachinesTypes = Invoke-Command $MachinesTypes -ArgumentList $InternalMachinesTypes
            }

            $InternalMachinesTypes
        }.GetNewClosure()
        Machines                    = {
            $InternalMachines = @{
                "main" = New-VosConfigMachine -MachineTypeName "docker-host" -Enable
            }

            if ($null -ne $Machines) {
                $InternalMachines = Invoke-Command $Machines -ArgumentList $InternalMachines
            }

            $InternalMachines
        }.GetNewClosure()
        Provisioning                = $Provisioning
        SharedFolders               = {
            $InternalSharedFolder = @{
                "docker-compose" = New-VosConfigMachineSharedFolder -Enabled -HostPath $DockerComposeHostPath -GuestPath "$DockerComposeGuestPath "
            }

            if (!$NoEnablePoShPoSh) {
                $InternalSharedFolder."posh" = New-VosConfigMachineSharedFolder -Enabled -HostPath $PoShPath -GuestPath "/posh"
            }

            if ($null -ne $SharedFolders) {
                $InternalSharedFolder = Invoke-Command $SharedFolders -ArgumentList $InternalSharedFolder
            }

            $InternalSharedFolder
        }.GetNewClosure()
        Commands                    = $Commands
        Variables                   = $Variables
        DockerHostMachineTypeConfig = $DockerHostMachineTypeConfig
        Vagrant                     = $Vagrant
        NoLinkedClones              = $NoLinkedClones
        NoCheckGuestAdditions       = $NoCheckGuestAdditions
        Local                       = $Local
        ConfigFile                  = $ConfigFile
        LocalConfigFile             = $LocalConfigFile
    }

    New-VosAlpine @NewVosAlpineConfig
}
