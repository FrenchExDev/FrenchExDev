function Get-VosAlpineKubernetesMachineIpConfig {
    [CmdletBinding()]
    param(
        [string[]] $ExcludeLabels,
        [object] $Network,
        [string[]] $Interfaces = @("eth1", "eth2")
    )

    $VerboseAndDebug = @{
        Verbose = $VerbosePreference
        Debug   = $DebugPreference
    }

    $kubernetesHosts = Get-VosAlpineKubernetesMachine -ExcludeLabels $ExcludeLabels -Network $Network @VerboseAndDebug

    $machinesEtcHosts = @()

    foreach ($currHost in $kubernetesHosts.GetEnumerator()) {
        foreach ($currLabel in $currHost.Value.labels) {
            if ($currLabel -match "fqdn=*") {
                $currFqdn = $($currLabel | select-string -pattern "fqdn=(.*)" | Select-CaptureGroup).1
                break;
            }
        }

        $machineConfig = @{
            Ip       = @{}
            Hostname = $currHost.Key
            Fqdn     = $currFqdn
        }

        foreach ($interface in $Interfaces) {
            $machineConfig.Ip."$interface" = $currHost.Value.interfaces."$interface".ip
        }

        $machinesEtcHosts += $machineConfig
    }

    $machinesEtcHosts
}
