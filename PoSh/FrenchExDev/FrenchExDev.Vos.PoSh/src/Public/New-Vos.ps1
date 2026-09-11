function New-Vos {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $Path = ".",
        [int] $Zeroes = 2,
        [scriptblock] $MachinesTypes, 
        [scriptblock] $Machines,
        [scriptblock] $Vagrant,
        [scriptblock] $Local,
        [hashtable] $HostNames,
        [ValidateNotNullOrWhiteSpace()] [string] $ConfigFile = $VosConfigSymbols.GlobalConfigFile,
        [ValidateNotNullOrWhiteSpace()] [string] $LocalConfigFile = $VosConfigSymbols.LocalConfigFile
    )

    New-Directory $Path
    Copy-Item "$PSScriptRoot/../../resources/Vagrantfile" "$Path/Vagrantfile" -Force

    $VosConfiguration = {
        $NewVosConfigConfig = @{
            Zeroes        = $Zeroes
            Vagrant       = $Vagrant 
            MachinesTypes = $MachinesTypes
            Machines      = $Machines
        }

        New-VosConfig @NewVosConfigConfig
    }.GetNewClosure()

    Write-VosConfig -VosConfigFile $ConfigFile -Configuration $VosConfiguration

    if ($null -ne $Local) {
        Write-VosConfigLocal -VosConfigFile $LocalConfigFile -Configuration {
            Invoke-Command $Local -ArgumentList $HostNames
        }.GetNewClosure()
    }
}
