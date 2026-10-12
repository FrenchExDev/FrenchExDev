enum VosConfigAction {
    Edit
}

function Get-VosConfig {
    [CmdletBinding()]
    param(
        [VosConfigAction[]] $Action,
        [string] $GlobalConfigFile = $VosConfigSymbols.GlobalConfigFile,
        [string] $LocalConfigFile = $VosConfigSymbols.LocalConfigFile,
        [switch] $Global,
        [switch] $Local,
        [switch] $Interactive
    )

    foreach ($currentAction in $Action) {
        switch ([VosConfigAction] $currentAction) {
            ([VosConfigAction]::Edit) {
                if ($Global) {
                    code $GlobalConfigFile
                }
            
                if ($Local) {
                    code $LocalConfigFile
                }
            
                if ($Interactive) {
                    Read-Host "Press any key to continue"
                }
            }
            default {
                throw "Get-VosConfig > Action '$currentAction' is not yet implemented."
            }
        }
    }
}
