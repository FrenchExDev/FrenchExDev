function Get-VosMachineVar {
    param(
        [parameter(Mandatory = $true, position = 0)] [string] $VagrantMachineName,
        [parameter(Mandatory = $true, position = 1)] [object] $machineType,
        [switch] $Name
    )

    $machineType.base.variables.$Name
}