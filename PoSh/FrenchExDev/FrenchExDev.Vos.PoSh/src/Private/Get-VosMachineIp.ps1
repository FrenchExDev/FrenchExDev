function Get-VosMachineIp {
    param(
        [parameter(Mandatory = $true, position = 0)] [string] $VagrantMachineName,
        [parameter(Mandatory = $true, position = 1)] [object] $machineType,
        [switch] $UsingVagrant,
        [string] $Interface
    )

    $GetIPCommandPattern = $machineType.base.commands."get-ip"

    $publicInterfaces = if ([string]::IsNullOrEmpty($Interface)) { @($machineType.base.variables."public.interfaces") } else { @($Interface) }

    foreach ($publicInterface in $publicInterfaces) {
        $getIPCommand = $GetIPCommandPattern.replace('##INTERFACE-ALIAS##', $publicInterface)

        if ([string]::IsNullOrEmpty($GetIPCommand)) {
            throw "Get-VosMachineIp > 'get-ip' Command is empty or not set."
        }

        Write-Debug "Get-VosMachineIp > $VagrantMachineName > Command : '$GetIPCommand'"

        switch ($machineType.base.os_type) {
            Windows {
                $foundIp = Invoke-Expression "vagrant powershell $VagrantMachineName --command '$GetIPCommand'" | Select-Object -First 1
                $($foundIp | Select-String -Pattern '(.*): (.*)' | Select-CaptureGroup).2
            }
            Linux_64 {
                if ($UsingVagrant) {
                    Invoke-Expression "vagrant ssh $VagrantMachineName --command ""$GetIPCommand"""
                } 
                else {
                    Invoke-Expression "ssh $VagrantMachineName -- $GetIPCommand"
                }
            }
            default {
                throw "Get-VosMachineIp > $(machineType.base.os_type) get-ip not yet implemented."
            }
        }
    }
}