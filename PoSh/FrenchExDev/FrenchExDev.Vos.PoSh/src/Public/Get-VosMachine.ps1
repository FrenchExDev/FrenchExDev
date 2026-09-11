enum VosMachineAction {
    # Destroy machine instance
    Destroy
    # Halt machine instance
    Halt
    # Reload machine instance
    Reload
    # Up machine instance
    Up
    # Status
    Status
    # Powershell' shell
    Powershell
    # Run Powershell Command
    PowershellCommand
    # Ssh' shell
    Ssh
    # Run Shell command
    SshCommand
    # display machine instance Ssh config
    SshConfig
    # update Ssh config for machine instance
    UpdateSshConfig
    # remove Ssh config for machine instance
    RemoveSshConfig
    # update /etc/hosts for machine instance
    UpdateEtcHosts
    # remove machine instance from /etc/hosts
    RemoveEtcHosts
    # provision machine, all or specific via -ProvisionWith
    Provision
    # save named snapshot
    SnapshotSave
    # restore named snapshot
    SnapshotRestore
    # push a new snapshot
    SnapshotPush
    # restore latest snapshot
    SnapshotPop
    # get Ip of machine instance for -Interface
    Ip
    # ping machine instance
    Ping
    # yield machine object
    Machine
    # yield vagrant machine name
    MachineName
}

<#
.SYNOPSIS
Control your VosMachine instance

.DESCRIPTION
This cmdlet is made to provide you ways to control your VosMachine instance.
It provides tab completion and a structured way to empower your Vagrant usage.

.PARAMETER MachineName
The Machine Name used to resolve Vagrant Machine Name.

.PARAMETER MachineNumber
The Machine Number used to resolve Vagrant Machine Name.

.PARAMETER Action
List of Actions to run in the given order.

.PARAMETER Command
When -Action SshCommand|PowershellCommand, provides the command to execute.

.PARAMETER IncludeDisabled
Parameter description

.PARAMETER UsingVagrant
Running SSH using vagrant ssh command

.PARAMETER Interactive
Interactive session (Powershell or SSH)

.PARAMETER NoProvision
Do not provision when Up

.PARAMETER ProvisionWith
List of provisioners to run when Provision

.PARAMETER Force
Force destroy

.PARAMETER Interface
Interface of machine instance to use to get IPv4

.PARAMETER SnapshotSave
Name of Snapshot to take

.PARAMETER SnapshotRestore
Name of Snapshot to restore

.PARAMETER Configuration
VosConfigObject object

.PARAMETER DebugVagrant
Add --debug to vagrant calls

.PARAMETER DebugInfo
Add debug information to display when -Verbose

.PARAMETER GlobalConfigFile
Global VosConfig file

.PARAMETER LocalConfigFile
Local VosConfig file

.EXAMPLE
Get-VosMachine main 0000 Up,UpdateSshConfig,UpdateEtcHosts -Verbose

.NOTES
This `Vos.PoSh` cmdlet wraps HashiCorp' Vagrant CLI 
#>
function Get-VosMachine {
    [CmdletBinding()]
    [Alias("gvm")]
    param(
        [parameter(Mandatory = $true, Position = 0)] [string] $MachineName,
        [parameter(Mandatory = $true, Position = 1)] [int] $MachineNumber,
        [parameter(Mandatory = $false, Position = 2)] [VosMachineAction[]] $Action,
        [parameter(Mandatory = $false)] [string] $Command,
        [parameter(Mandatory = $false)] [switch] $IncludeDisabled,
        [parameter(mandatory = $false)] [switch] $UsingVagrant,
        [parameter(mandatory = $false)] [switch] $Interactive,
        [parameter(mandatory = $false)] [switch] $NoProvision,
        [parameter(mandatory = $false)] [string[]] $ProvisionWith,
        [parameter(mandatory = $false)] [switch] $Force,
        [parameter(Mandatory = $false)] [string] $Interface,
        [parameter(Mandatory = $false)] [string] $SnapshotSave,
        [parameter(Mandatory = $false)] [string] $SnapshotRestore,
        [parameter(mandatory = $false)] [object] $Configuration,
        [parameter(mandatory = $false)] [switch] $DebugVagrant,
        [parameter(mandatory = $false)] [string] $DebugInfo,
        [parameter(mandatory = $false)] [string] $GlobalConfigFile = $VosConfigSymbols.GlobalConfigFile,   
        [parameter(mandatory = $false)] [string] $LocalConfigFile = $VosConfigSymbols.LocalConfigFile
    )

    $VersboseAndDebug = @{
        Verbose = $VerbosePreference
        Debug   = $DebugPreference
    }

    if (([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) -ne $true) {
        throw "You are not an Administrator. For security reason, execution is not allowed."
    }

    if (![string]::IsNullOrEmpty($DebugInfo)) {
        Write-Verbose "Get-VosMachine > Start> $DebugInfo"
    }

    $Configuration = if ($null -eq $Configuration) { Get-VosConfigObject -GlobalConfigFile $GlobalConfigFile -LocalConfigFile $LocalConfigFile } else { $Configuration }
    
    $machine = if ($IncludeDisabled) { 
        $($Configuration.GetMachines() | Where-Object { $_.Name -eq $MachineName })
    }
    else {
        $($Configuration.GetEnabledMachines() | Where-Object { $_.Name -eq $MachineName })
    }

    $machineTypeName = $machine.value.machine_type_name

    $machineType = $($Configuration.machines_types)."$machineTypeName"

    if ($null -eq $machineType) {
        throw "Get-VosMachine > Machine Type named '$machineTypeName' is not defined."
    }
    
    $newMachine = [pscustomobject] @{
        Name               = $machine.Name
        Machine            = $machine.Value
        Configuration      = $Configuration
        MachineType        = $machineType
        MachineNumber      = $MachineNumber
        FormattedInstance  = $Configuration.FormatInstance($MachineNumber)
        VagrantMachineName = $Configuration.GetVagrantMachineName($($machine.Name), $($MachineNumber)).Trim()
    }

    $VagrantMachineNameStr = $newMachine.VagrantMachineName

    foreach ($currentAction in $Action) {
        [VosMachineAction] $currentActionEnum = $currentAction
        switch ($currentActionEnum) {
            ([VosMachineAction]::MachineName) {
                $VagrantMachineNameStr
            }
            ([VosMachineAction]::Machine) {
                $newMachine
            }
            ([VosMachineAction]::Halt) {
                Write-Verbose "Get-VosMachine > $VagrantMachineNameStr > Halt > Force: $Force, Debug: $DebugVagrant"
                Get-Vagrant Halt $VagrantMachineNameStr -Force:$Force.IsPresent -DebugVagrant:$DebugVagrant.IsPresent @VersboseAndDebug
            }
            ([VosMachineAction]::Destroy) {
                Write-Verbose "Get-VosMachine > $VagrantMachineNameStr > Destroy > Force: $force, Debug: $debugVagrant" 
                Get-Vagrant Destroy $VagrantMachineNameStr -Force:$Force.IsPresent -DebugVagrant:$DebugVagrant.IsPresent @VersboseAndDebug
            }
            ([VosMachineAction]::Reload) {
                Write-Verbose "Get-VosMachine > $VagrantMachineNameStr > Reload > Force: $force, Debug: $debugVagrant"
                Get-Vagrant Reload $VagrantMachineNameStr -Force:$Force.IsPresent -DebugVagrant:$DebugVagrant.IsPresent @VersboseAndDebug
            }
            ([VosMachineAction]::Up) {
                Write-Verbose "Get-VosMachine > $VagrantMachineNameStr > Up > Force: $force, Debug: $debugVagrant"
                Get-Vagrant Up $VagrantMachineNameStr -NoProvision:$NoProvision.IsPresent -Force:$Force.IsPresent -DebugVagrant:$DebugVagrant.IsPresent @VersboseAndDebug
            }
            ([VosMachineAction]::Status) {
                Write-Verbose "Get-VosMachine > $VagrantMachineNameStr > Status" 
                Get-Vagrant Status @VersboseAndDebug
            }
            ([VosMachineAction]::Powershell) {
                Write-Verbose "Get-VosMachine > $VagrantMachineNameStr > Powershell"
                Get-Vagrant Powershell $VagrantMachineNameStr @VersboseAndDebug
            }
            ([VosMachineAction]::PowershellCommand) {
                if (![string]::IsNullOrEmpty($Command)) {
                    Write-Verbose "Get-VosMachine > $VagrantMachineNameStr > PowershellCommand > Command '$Command'"
                    Get-Vagrant PowershellCommand $VagrantMachineNameStr -Command $Command -Elevated @VersboseAndDebug
                }
            }
            ([VosMachineAction]::SshConfig) {
                Write-Verbose "Get-VosMachine > $VagrantMachineNameStr > SshConfig"
                Get-Vagrant SshConfig $VagrantMachineNameStr @VersboseAndDebug
            }
            ([VosMachineAction]::UpdateSshConfig) {
                Write-Verbose "Get-VosMachine > $VagrantMachineNameStr > UpdateSshConfig >" 
                $vosSshConfig = Invoke-Expression "vagrant ssh-config $VagrantMachineNameStr"
                $hostname = $($vosSshConfig | select-string -pattern 'Host (.*)' | Select-CaptureGroup).1
                if ([string]::IsNullOrEmpty($hostname)) {
                    $vosSshConfig
                    throw "hostname is empty"
                }
                Add-SshConfig -Config $vosSshConfig -ConfigFile $env:USERPROFILE/.ssh/config
            }
            ([VosMachineAction]::RemoveSshConfig) {
                Write-Verbose "Get-VosMachine > $VagrantMachineNameStr > RemoveSshConfig >" 
                Remove-SshConfig -HostName $VagrantMachineNameStr
            }
            ([VosMachineAction]::Ssh) {
                Write-Verbose "Get-VosMachine > $VagrantMachineNameStr > Ssh > UsingVagrant: $UsingVagrant, Interactive: $Interactive, DebugVagrant: $DebugVagrant" 
                if ($UsingVagrant) {
                    Get-Vagrant Ssh $VagrantMachineNameStr -DebugVagrant:$DebugVagrant.IsPresent
                } 
                else {
                    $interactiveArg = if ($Interactive) { "-t" }
                    Invoke-Expression "ssh $interactiveArg $VagrantMachineNameStr"
                }
            }
            ([VosMachineAction]::SshCommand) {
                Write-Verbose "Get-VosMachine > $VagrantMachineNameStr > SshCommand > $Command > UsingVagrant: $UsingVagrant, Interactive: $Interactive, Debug: $DebugVagrant" 
                if ($UsingVagrant) {
                    Get-Vagrant SshCommand $VagrantMachineNameStr -Command $command -DebugVagrant:$DebugVagrant.IsPresent
                } 
                else {
                    $interactiveArg = if ($Interactive) { "-t" }
                    $sshCommand = "ssh $interactiveArg $VagrantMachineNameStr -- ""$Command"""
                    Write-Debug "Get-VosMachine > $VagrantMachineNameStr > SshCommand > $Command > '$sshCommand'"
                    Invoke-Expression $sshCommand
                }
            }
            ([VosMachineAction]::Provision) {
                Write-Verbose "Get-VosMachine > $VagrantMachineNameStr > Provision"
                Get-Vagrant Provision $VagrantMachineNameStr -ProvisionWith $ProvisionWith -Force:$Force.IsPresent -DebugVagrant:$DebugVagrant.IsPresent
            }
            ([VosMachineAction]::SnapshotPush) {
                Write-Verbose "Get-VosMachine > $VagrantMachineNameStr > Snapshot > Push"
                Get-Vagrant SnapshotPush $VagrantMachineNameStr @VersboseAndDebug
            }
            ([VosMachineAction]::SnapshotSave) {
                Write-Verbose "Get-VosMachine > $VagrantMachineNameStr > Snapshot > Save $SnapshotSave"
                Get-Vagrant SnapshotSave $VagrantMachineNameStr -Snapshot $SnapshotSave @VersboseAndDebug
            }
            ([VosMachineAction]::SnapshotPop) {
                Write-Verbose "Get-VosMachine > $VagrantMachineNameStr > Snapshot > Pop"
                Get-Vagrant SnapshotPop $VagrantMachineNameStr -Snaptshot $SnapshotSave @VersboseAndDebug
            }
            ([VosMachineAction]::SnapshotRestore) {
                Write-Verbose "Get-VosMachine > $VagrantMachineNameStr > Snapshot > Restore $SnapshotRestore"
                Get-Vagrant SnapshotRestore $VagrantMachineNameStr -Snaptshot $SnapshotSave @VersboseAndDebug
            }
            ([VosMachineAction]::Ip) {
                Get-VosMachineIp $VagrantMachineNameStr $machineType -Interface $Interface @VersboseAndDebug
            }
            ([VosMachineAction]::UpdateEtcHosts) {
                $foundIPs = Get-VosMachineIp $VagrantMachineNameStr $machineType -Interface $Interface @VersboseAndDebug
                if ([string]::IsNullOrEmpty($Interface)) {
                    foreach ($foundIp in $foundIPs) {
                        Set-Chost -VagrantMachineNameStr $VagrantMachineNameStr -Ip $foundIP
                    }
                }
                else {
                    if (![string]::IsNullOrEmpty($foundIPs)) {
                        Set-Chost -VagrantMachineNameStr $VagrantMachineNameStr -Ip $foundIPs
                        continue;
                    }
                    throw "Get-VosMachine > $VagrantMachineNameStr > UpdateEtcHosts > Cannot get IP"
                }
            }
            ([VosMachineAction]::RemoveEtcHosts) {
                Write-Verbose "Get-VosMachine > $VagrantMachineNameStr > RemoveEtcHosts > Removing from Host EtcHosts"
                Remove-CHostsEntry -HostName $VagrantMachineNameStr -Debug:$false
            }
            ([VosMachineAction]::Ping) {
                Write-Verbose "Get-VosMachine > $VagrantMachineNameStr > Ping > Pinging..."
                ping -a -t $VagrantMachineNameStr
            }
        }
    }

    if (![string]::IsNullOrEmpty($DebugInfo)) {
        Write-Verbose "Get-VosMachine > End > $DebugInfo"
    }
}

function Set-Chost { 
    param(
        [string] $VagrantMachineNameStr,
        [string] $Ip
    ) 
    if (![string]::IsNullOrEmpty($Ip)) {
        Write-Debug "Get-VosMachine > $VagrantMachineNameStr > UpdateEtcHosts > Removing existing entry"  
        Remove-CHostsEntry -HostName $VagrantMachineNameStr
        Write-Verbose "Get-VosMachine > $VagrantMachineNameStr > UpdateEtcHosts > Adding new entry with IP '$Ip'"  
        Set-CHostsEntry -IPAddress $Ip -HostName $VagrantMachineNameStr
    }
}