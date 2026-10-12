enum PackerAction {
    Build
    InstallPlugin
}

function Get-Packer {
    [CmdletBinding()]
    param(
        [PackerAction[]] $Action,
        [string] $Name,
        [switch] $Force,
        [hashtable] $Var,
        [string[]] $PluginName
    )

    foreach ($currentAction in $Action) {
        switch ([PackerAction] $currentAction) {
            ([PackerAction]::Build) {
                Build-Packer -Name $Name -Force:$Force -Var $Var
            }
            ([PackerAction]::InstallPlugin) {
                Install-PackerPlugin -PluginName $PluginName
            }
            default {
                throw "Get-Packer > Action '$currentAction' is not yet implemented."
            }
        }
    }
}
