function Get-VosConfigObject {
    [CmdletBinding()]
    param(
        [string] $GlobalConfigFile = $VosConfigSymbols.GlobalConfigFile,
        [string] $LocalConfigFile = $VosConfigSymbols.LocalConfigFile,
        [parameter(mandatory = $false)] [string] $DebugInfo
    )

    Write-Debug "Get-VosConfigObject > Config: $GlobalConfigFile, Local: $LocalConfigFile"

    if (!(Test-Path $GlobalConfigFile) -or !(Test-Path $LocalConfigFile)) {
        throw "missing configuration file $GlobalConfigFile or $LocalConfigFile"
    }

    $configuration = yq -s '.[0] * .[1]' $GlobalConfigFile $LocalConfigFile | ConvertFrom-Yaml

    $configuration | Add-Member ScriptMethod -Name FormatInstance -Value {
        param(
            [int] $Instance
        )

        $($this.format.posh) -f $Instance
    }

    $configuration | Add-Member ScriptMethod -Name GetMachine -Value {
        param(
            [string] $Machine
        )

        $($this.machines.PSObject.Properties | Where-Object { $_.Name -eq $machine } | Select-Object -Property Value).Value
    }

    $configuration | Add-Member ScriptMethod -Name GetVagrantMachineName -Value {
        param(
            [string] $MachineName,
            [int] $Instance
        )

        $ConfigNamingPattern = $($this.vagrant.'naming-pattern')
        $MachineInstance = $this.FormatInstance($Instance)

        $Name = $ConfigNamingPattern -replace "#MACHINE-NAME#", $MachineName
        $Name = $Name -replace "#MACHINE-INSTANCE#", $MachineInstance

        $Name
    }

    $configuration | Add-Member ScriptMethod -Name GetEnabledMachines -Value {
        $this.machines.PSObject.Properties | Where-Object { $_.Value.is_enabled -eq $true }
    }

    $configuration | Add-Member ScriptMethod -Name GetMachines -Value {
        $this.machines.PSObject.Properties
    }

    $configuration | Add-Member ScriptMethod -Name GetVagrantMachinesNames -Value {
        $machines = $this.machines.PSObject.Properties

        $machines | ForEach-Object { 
            $machineName = $_.Name
            $instances = $_.Value.instances

            for ($i = 0; $i -ne $instances; $i++) {
                $this.GetVagrantMachineName($machineName, $i)
            }
        }
    }

    $configuration
}
