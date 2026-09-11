[CmdletBinding()]
param(
    [string] $Acme = "frenchexdev",
    [string] $Distro = "alpine",
    [string] $DistroVersion = $AlpineSymbols.Versions.LatestEdge,
    [string] $Flavor = $AlpineSymbols.Flavors.virt,
    [string[]] $Kinds = @("default", "docker", "kubernetes-$($KubernetesSymbols.MachinesKinds.JumpBox)", "kubernetes-$($KubernetesSymbols.MachinesKinds.ControlPlan)", "kubernetes-$($KubernetesSymbols.MachinesKinds.Worker)"),
    [string] $BoxVersion = "1.0.0",
    [ValidateScript({ @("default", "docker", "kubernetes-$($KubernetesSymbols.MachinesKinds.JumpBox)", "kubernetes-$($KubernetesSymbols.MachinesKinds.ControlPlan)", "kubernetes-$($KubernetesSymbols.MachinesKinds.Worker)") -contains $_ })] [string[]] $Only,
    [ValidateScript({ @("default", "docker", "kubernetes-$($KubernetesSymbols.MachinesKinds.JumpBox)", "kubernetes-$($KubernetesSymbols.MachinesKinds.ControlPlan)", "kubernetes-$($KubernetesSymbols.MachinesKinds.Worker)") -contains $_ })] [string[]] $Except,
    [switch] $Force
)

Push-Location src 

foreach ($kind in $Kinds) {

    $notOnlyCase = $only -gt 0 -and -not $($only -contains $kind)
    if ($notonlycase) {
        write-verbose "Not building ${Distro}-${DistroVersion}-${Flavor}-${kind}"
        continue
    }

    $exceptCase = $except -gt 0 -and $except -contains $kind
    if ($exceptCase) {
        write-verbose "Not building ${Distro}-${DistroVersion}-${Flavor}-${kind}"
        continue
    }

    Push-Location "packer/${Distro}-${DistroVersion}-${Flavor}-${kind}"

    write-information "Building ${Distro}-${DistroVersion}-${Flavor}-${kind}"

    $BuildPackerConfig = @{
        Force = $Force
        Name  = "${Distro}"
        Var   = @{
            date        = $(Get-Date -Format "dd-MM-yyyy-hh-mm-ss") 
            box_version = $BoxVersion
        }
    }

    Build-Packer @BuildPackerConfig

    Pop-Location
}

Pop-Location
