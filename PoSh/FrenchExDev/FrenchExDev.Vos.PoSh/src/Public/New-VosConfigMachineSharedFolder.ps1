function New-VosConfigMachineSharedFolder {
    [CmdletBinding()]
    param(
        [switch] $Enabled,
        [ValidateNotNullOrWhiteSpace()] [string] $HostPath,
        [ValidateNotNullOrWhiteSpace()] [string] $GuestPath
    )

    [pscustomobject] @{
        enabled    = [bool] $($Enabled)
        host_path  = $HostPath
        guest_path = $GuestPath
    }

    New-Directory $HostPath
}
