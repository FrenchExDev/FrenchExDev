function Get-PiHoleDnsCommand {
    [CmdletBinding()]
    param(
        [PiHoleDnsAction] $Action
    )

    foreach ($currentAction in $Action) {
        switch ([PiHoleDnsAction] $currentAction) {
            ([PiHoleDnsAction]::Add) {
                'Get-PiHoleDns Add -Hostname ''#HostName#'' -Ip ''#Ip#'''
            }
            ([PiHoleDnsAction]::Remove) {
                'Get-PiHoleDns Remove -Hostname ''#Hostname#'' -Ip ''#Ip#'''
            }
            default {
                throw "Get-PiHoleDnsCommand > Action '$currentAction' is not yet implemented."
            }
        }
    }
}
