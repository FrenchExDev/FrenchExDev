
enum VosAlpineKubernetesNetworkMapAction {
    Create
    Set
}

function Get-VosAlpineKubernetesNetworkMap {
    [CmdletBinding()]
    param(
        [parameter(Position = 0)] [VosAlpineKubernetesNetworkMapAction[]] $Action,
        [ValidateNotNull()] [object] $Network
    )

    $VerboseAndDebug = @{
        Verbose = $VerbosePreference
        Debug   = $DebugPreference
    }

    foreach ($currentAction in $Action) {
        switch ([VosAlpineKubernetesNetworkMapAction] $currentAction) {
            ([VosAlpineKubernetesNetworkMapAction]::Create) {
                $kubernetesHostsIPConfigs = Get-VosAlpineKubernetesMachineIpConfig -ExcludeLabels "kubernetes-jumpbox" -Network $Network @VerboseAndDebug

                $machinesEtcHosts = @()

                foreach ($currHost in $kubernetesHostsIPConfigs.GetEnumerator()) {
                    $currHostIPConfigValue = $currHost.Value
                    $machinesEtcHosts += "$($currHostIPConfigValue.Ip) $($currHostIPConfigValue.Fqdn) $($currHostIPConfigValue.Hostname)"
                }

                $machinesEtcHosts -join [System.Environment]::NewLine | Out-File ./config-etc-hosts -encoding utf8
            }
            ([VosAlpineKubernetesNetworkMapAction]::Set) {

            }
            default {
                throw "Get-VosAlpineKubernetesNetworkMap > Action '$currentAction' is not yet implemented."
            }
        }
    }
}