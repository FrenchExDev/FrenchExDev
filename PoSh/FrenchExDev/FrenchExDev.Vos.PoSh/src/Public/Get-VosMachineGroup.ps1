enum VosMachineGroupAction {
    Up
    Destroy
    Reload
    Halt
    SshCommand
    UpdateSshConfig
}

function Get-VosMachineGroup {
    [CmdletBinding()]
    param(
        [VosMachineGroupAction[]] $Action,
        [string[]] $Group,
        [switch] $Force,
        [string] $Command,
        [string] $GlobalConfigFile = $VosConfigSymbols.GlobalConfigFile,
        [string] $LocalConfigFile = $VosConfigSymbols.LocalConfigFile
    )

    $InternalVerboseDebugConfig = @{
        Verbose = $VerbosePreference
        Debug   = $DebugPreference
    }

    $config = Get-VosConfigObject -GlobalConfigFile $GlobalConfigFile -LocalConfigFile $LocalConfigFile @InternalVerboseDebugConfig

    foreach ($currentGroup in $Group) {
        foreach ($currentAction in $Action) {
            switch ([VosMachineGroupAction] $currentAction) {
                ([VosMachineGroupAction]::Up) {
                    Operate-VosMachineGroup $currentGroup $config { 
                        param($group, $id) 
                        Get-VosMachine $group $id Up @InternalVerboseDebugConfig
                    }.GetNewClosure()
                }
                ([VosMachineGroupAction]::Destroy) {
                    Operate-VosMachineGroup $currentGroup $config { 
                        param($group, $id) 
                        Get-VosMachine $group $id Destroy -Force:$Force @InternalVerboseDebugConfig
                    }.GetNewClosure()
                }
                ([VosMachineGroupAction]::Reload) {
                    Operate-VosMachineGroup $currentGroup $config { 
                        param($group, $id) 
                        Get-VosMachine $group $id Reload @InternalVerboseDebugConfig
                    }.GetNewClosure()                
                }
                ([VosMachineGroupAction]::Halt) {
                    Operate-VosMachineGroup $currentGroup $config { 
                        param($group, $id) 
                        Get-VosMachine $group $id Halt @InternalVerboseDebugConfig
                    }.GetNewClosure()
                }
                ([VosMachineGroupAction]::SshCommand) {
                    Operate-VosMachineGroup $currentGroup $config { 
                        param($group, $id) 
                        Get-VosMachine $group $id SshCommand -Command $Command @InternalVerboseDebugConfig
                    }.GetNewClosure()
                }
                ([VosMachineGroupAction]::UpdateSshConfig) {
                    Operate-VosMachineGroup $currentGroup $config { 
                        param($group, $id) 
                        Get-VosMachine $group $id UpdateSshConfig @InternalVerboseDebugConfig 
                    }.GetNewClosure()                
                }
                default {
                    throw "VosMachineGroupAction '$currentAction' is not yet implemented"
                }
            }
        }
    }
}
