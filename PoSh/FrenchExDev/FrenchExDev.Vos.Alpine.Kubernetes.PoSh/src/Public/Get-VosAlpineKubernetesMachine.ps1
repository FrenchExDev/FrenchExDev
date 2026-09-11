function Get-VosAlpineKubernetesMachine {
    [CmdletBinding()]
    param(
        [string[]] $ExcludeLabels,
        [object] $Network
    )

    $kubernetesHosts = @{}

    foreach ($networkHost in $Network.Hosts.GetEnumerator()) {
        $currNetHostName = $networkHost.Key
        $currNetHost = $networkHost.Value

        foreach ($label in $currNetHost.labels) {
            if ($label -match "kubernetes-*") {
                $add = $true
                foreach ($notLabel in $excludeLabels) {
                    if ($label -match $notLabel) {
                        $add = $false
                        break
                    }
                }

                if ($add) {
                    $kubernetesHosts."$currNetHostName" = $currNetHost
                }
            }
        }
    }

    $kubernetesHosts
}
