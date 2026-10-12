enum VirtualBoxVmAction {
    Clean
    CleanAll
}

function Clean-VirtualBoxVm {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '', Scope = 'Function')]    
    [CmdletBinding()]
    param(
        [VirtualBoxVmAction[]] $Action,
        [string] $Name,
        [switch] $Force
    )

    foreach ($currentAction in $Action) {
        switch ([VirtualBoxVmAction] $currentAction) {
            ([VirtualBoxVmAction]::CleanAll) {
                Remove-Item -Recurse -Force:$Force "$env:USERPROFILE/VirtualBox VMs"
            }
            ([VirtualBoxVmAction]::Clean) {
                if ([string]::IsNullOrEmpty($Name)) {
                    throw "Get-VirtualBox > name is empty"
                }

                Remove-Item -Recurse -Force:$Force "$env:USERPROFILE/VirtualBox VMs/$Name"
            }
            default {
                throw "Clean-VirtualBoxVm > Action '$currentAction' is not yet implemented."
            }
        }
    }
}
