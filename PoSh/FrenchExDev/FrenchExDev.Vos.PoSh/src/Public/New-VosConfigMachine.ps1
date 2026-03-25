function New-VosConfigMachine {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory, Position = 0)] [string] $MachineTypeName,
        [switch] $Enable
    )

    [pscustomobject] @{
        is_enabled        = [bool] $($Enable.IsPresent)
        machine_type_name = $MachineTypeName
    }
}
