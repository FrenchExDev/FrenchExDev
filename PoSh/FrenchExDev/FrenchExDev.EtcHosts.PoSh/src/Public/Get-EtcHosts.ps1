enum EtcHostsAction {
    Add
    Remove
}

function Get-EtcHosts {
    [CmdletBinding()]
    param(
        [EtcHostsAction[]] $Action,
        [string] $HostName,
        [string] $Ip
    )

    foreach($currentAction in $Action) {
        switch([EtcHostsAction] $currentAction) {
            ([EtcHostsAction]::Remove) {
                Remove-EtcHostsEntry -Hostname $HostName -Ip $Ip
            }
            ([EtcHostsAction]::Add) {
                Add-EtcHostsEntry -Hostname $HostName -Ip $Ip
            }
            default {
                throw "Get-EtcHosts > Action '$currentAction' is not yet implemented"
            }
        }
    }
}
