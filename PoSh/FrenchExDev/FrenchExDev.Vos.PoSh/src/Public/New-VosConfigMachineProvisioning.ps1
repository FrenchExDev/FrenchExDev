function New-VosConfigMachineProvisioning {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $Version,
        [ValidateNotNullOrWhiteSpace()] [string] $Extension,
        [switch] $Enabled,
        [switch] $ReloadBefore,
        [switch] $ReloadAfter,
        [switch] $Privileged
    )

    [pscustomobject] @{
        enabled         = [bool] $Enabled
        version         = $Version
        ext             = $Extension
        "reload:before" = [bool] $ReloadBefore
        "reload:after"  = [bool] $ReloadAfter
        privileged      = [bool] $Privileged
    }
}
