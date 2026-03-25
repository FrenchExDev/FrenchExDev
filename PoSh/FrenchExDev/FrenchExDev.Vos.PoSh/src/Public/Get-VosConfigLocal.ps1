enum VosConfigLocalAction {
    New
}

function Get-VosConfigLocal {
    [CmdletBinding()]
    param(
        [VosConfigLocalAction[]] $Action,
        [string] $NetworkMac,
        [string] $NetworkIp,
        [int] $Instances,
        [int] $Vcpus,
        [int] $RamMb,
        [string] $DevFactoryVmPrefixName,
        [ValidateNotNullOrWhiteSpace()] [string] $NamingPattern = "##PREFIX##-#MACHINE-NAME#-#MACHINE-INSTANCE#",
        [ValidateNotNullOrWhiteSpace()] [string] $LocalConfigFile = $VosConfigSymbols.LocalConfigFile
    )

    foreach ($currentAction in $Action) {
        switch ([VosConfigLocalAction] $currentAction) {
            ([VosConfigLocalAction]::New) { 
                $NamingPattern = $NamingPattern.replace("##PREFIX##", $DevFactoryVmPrefixName)

                @{
                    vagrant  = [pscustomobject] @{
                        "naming-pattern" = $NamingPattern
                    }
                    machines = [pscustomobject] @{
                        ram_mb      = $RamMb
                        vcpus       = $Vcpus
                        instances   = $Instances
                        network_mac = $NetworkMac
                        network_ip  = $NetworkIp
                    }
                } | ConvertTo-Yaml | Out-File $LocalConfigFile -Encoding ascii
            }
            Default {
                throw "Get-VosConfigLocal > Action '$currentAction' is not yet implemented."
            }
        }
    }
}
