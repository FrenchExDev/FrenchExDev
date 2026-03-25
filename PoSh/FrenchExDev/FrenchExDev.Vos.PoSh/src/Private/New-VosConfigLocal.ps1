function New-VosConfigLocal {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory = $true)] [string] $NetworkMac,
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory = $true)] [string] $NetworkIp,
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory = $true)] [int] $Instances,
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory = $true)] [int] $Vcpus,
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory = $true)] [double] $Ram,
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory = $true)] [string] $DevFactoryVmPrefixName,
        [ValidateNotNullOrWhiteSpace()] [string] $NamingPattern = "##PREFIX##-#MACHINE-NAME#-#MACHINE-INSTANCE#",
        [ValidateNotNullOrWhiteSpace()] [string] $LocalConfigFile = $VosConfigSymbols.LocalConfigFile
    )

    $NamingPattern = $NamingPattern.replace("##PREFIX##", $DevFactoryVmPrefixName)

    @{
        vagrant  = [pscustomobject] @{
            "naming-pattern" = $NamingPattern
        }
        machines = [pscustomobject] @{
            ram_mb      = $Ram / 1MB
            vcpus       = $Vcpus
            instances   = $Instances
            network_mac = $NetworkMac
            network_ip  = $NetworkIp
        }
    } | ConvertTo-Yaml | Out-File $LocalConfigFile -Encoding ascii
}
