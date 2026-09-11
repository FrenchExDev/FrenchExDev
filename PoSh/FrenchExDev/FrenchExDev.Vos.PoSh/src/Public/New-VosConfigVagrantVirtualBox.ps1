function New-VosConfigVagrantVirtualBox {
    [CmdletBinding()]
    param(
        [switch] $LinkedClones,
        [switch] $CheckGuestAdditions,
        [scriptblock] $Manage,
        [scriptblock] $StorageControl,
        [scriptblock] $StorageAttach,
        [scriptblock] $SetProperty
    )

    @{
        linked_clones         = $LinkedClones.IsPresent
        check_guest_additions = $CheckGuestAdditions.IsPresent
        manage                = Invoke-Command $Manage
        setproperty           = Invoke-Command $SetProperty
        storagectl            = @(Invoke-Command $StorageControl)
        storageattach         = @(Invoke-Command $StorageAttach)
    }
}
