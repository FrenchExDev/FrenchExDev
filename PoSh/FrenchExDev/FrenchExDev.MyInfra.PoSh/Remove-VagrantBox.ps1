[CmdletBinding()]
param(
    [ValidateNotNullOrWhiteSpace()] [string] $Acme = "frenchexdev",
    [ValidateNotNullOrWhiteSpace()] [string] $Distro = "alpine",
    [ValidateNotNullOrWhiteSpace()] [string] $DistroVersion = $AlpineSymbols.Versions.LatestEdge,
    [ValidateNotNullOrWhiteSpace()] [string] $DistroFlavor = $AlpineSymbols.Flavors.Virt,
    [ValidateNotNullOrWhiteSpace()] [string] $DistroKind
)

$kinds = @(
    "default"
    "docker"
    "kubernetes-$($KubernetesSymbols.MachinesKinds.ControlPlan)"
    "kubernetes-$($KubernetesSymbols.MachinesKinds.JumpBox)"
    "kubernetes-$($KubernetesSymbols.MachinesKinds.Worker)"
)

$boxPrefix = "$($Acme)-$($Distro)-v$($DistroVersion)-$($DistroFlavor)"

$boxes = @()

foreach($kind in $kinds) {
    $boxes += $("$($boxPrefix)-$($kind)")
}

$boxes | ForEach-Object { vagrant box remove $_ -all }