function New-VosConfig {
    [CmdletBinding()]
    param(
        [parameter(Mandatory)][int] $Zeroes,
        [scriptblock] $Vagrant,
        [scriptblock] $MachinesTypes,
        [scriptblock] $Machines
    )

    [pscustomobject] @{
        format         = New-VosConfigFormat -Zeroes $Zeroes
        vagrant        = Invoke-Command $Vagrant
        machines_types = Invoke-Command $MachinesTypes
        machines       = Invoke-Command $Machines
    }
}
