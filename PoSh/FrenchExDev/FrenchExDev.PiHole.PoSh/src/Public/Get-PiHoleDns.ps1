enum PiHoleDnsAction {
    Add
    Remove
}

function Get-PiHoleDns {
    [CmdletBinding()]
    param(
        [PiHoleDnsAction[]] $Action,
        [string] $HostName,
        [string] $Ip
    )

    foreach ($currentAction in $Action) {
        switch ([PiHoleDnsAction] $currentAction) {
            ([PiHoleDnsAction]::Add) {
                ssh pihole -C "pwsh -L -C '$(Get-EtcHostsCommand Add)'"
            }
            ([PiHoleDnsAction]::Remove) {
                ssh pihole -C "pwsh -L -C '$(Get-EtcHostsCommand Remove)'"
            }
            default {
                throw "Get-PiHoleDns > Action '$currentAction' is not yet implemented."
            }
        }
    }
}
