[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()] [string] $Acme = "FrenchExDev",
    [ValidateNotNullOrEmpty()] [string] $Distro = "alpine",
    [ValidateNotNullOrEmpty()] [string] $DistroVersion = $AlpineSymbols.Versions.LatestEdge,
    [ValidateNotNullOrEmpty()] [string[]] $Kinds = @("default", "docker", "kubernetes-$($KubernetesSymbols.MachinesKinds.JumpBox)", "kubernetes-$($KubernetesSymbols.MachinesKinds.ControlPlan)", "kubernetes-$($KubernetesSymbols.MachinesKinds.Worker)"),
    [ValidateNotNullOrEmpty()] [string] $BoxVersion = "1.0.0",
    [ValidateNotNullOrEmpty()] [string] $Architecture = $VirtualBoxSymbols.Architectures.amd64,
    [ValidateNotNullOrEmpty()] [string] $Flavor = $AlpineSymbols.Flavors.Virt,
    [ValidateNotNullOrEmpty()] [string] $PowerShellVersion = $PowershellSymbols.Versions.LatestStable,
    [ValidateNotNullOrEmpty()] [string] $OutputVagrant = "output-vagrant",
    [ValidateNotNullOrEmpty()] [string] $Provider = "virtualbox",
    [ValidateScript({ @("default", "docker", "kubernetes-$($KubernetesSymbols.MachinesKinds.JumpBox)", "kubernetes-$($KubernetesSymbols.MachinesKinds.ControlPlan)", "kubernetes-$($KubernetesSymbols.MachinesKinds.Worker)") -contains $_ })] [string[]] $Only,
    [ValidateScript({ @("default", "docker", "kubernetes-$($KubernetesSymbols.MachinesKinds.JumpBox)", "kubernetes-$($KubernetesSymbols.MachinesKinds.ControlPlan)", "kubernetes-$($KubernetesSymbols.MachinesKinds.Worker)") -contains $_ })] [string[]] $Except,
    [switch] $Force
)

Push-Location src

$AcmeLowInv = $acme.ToLowerInvariant()

foreach ($kind in $Kinds) {

    $notOnlyCase = $only -gt 0 -and -not $($only -contains $kind)
    if ($notonlycase) {
        Write-Verbose "Not handling ${Distro}-${DistroVersion}-${Flavor}-${kind}"
        continue
    }

    $exceptCase = $except -gt 0 -and $except -contains $kind
    if ($exceptCase) {
        Write-Verbose "Not handling ${Distro}-${DistroVersion}-${Flavor}-${kind}"
        continue
    }

    Push-Location "packer/${Distro}-${DistroVersion}-${flavor}-${kind}"

    Write-Information "Handling ${Distro}-${DistroVersion}-${flavor}-${kind}"

    $BoxFilePath = $(resolve-path "$OutputVagrant/${AcmeLowInv}-${Distro}-v${DistroVersion}-${flavor}-${kind}-v${BoxVersion}.box")
    
    $TextInfo = (Get-Culture).TextInfo
    
    $kindTitle = $TextInfo.ToTitleCase($kind)
    $distroTitle = $TextInfo.ToTitleCase($distro)
    
    $AddVagrantCatalogVersionConfig = @{
        BoxVersion        = $BoxVersion
        CatalogName       = "${AcmeLowInv}-${Distro}-v${DistroVersion}-${flavor}-${kind}"
        Provider          = $VagrantSymbols.Providers.VirtualBox
        BoxFilePath       = "file://$($BoxFilePath.Path.Replace('\', "/"))"
        Architecture      = $Architecture
        NewVagrantCatalog = {
            $NewVagrantCatalogConfig = @{
                Name        = "${AcmeLowInv}-${Distro}-v${DistroVersion}-${flavor}-${kind}"
                Description = "${Acme} ${distroTitle} v${DistroVersion} ${kindTitle} Host w/ Powershell v${PowerShellVersion}"
                Versions    = {
                    @()
                }
            }
            New-VagrantCatalog @NewVagrantCatalogConfig
        }.GetNewClosure()
    }

    Add-VagrantCatalogVersion @AddVagrantCatalogVersionConfig

    $AddVagrantBox = @{
        Architecture = "$Architecture"
        Provider     = "$Provider"
        BoxVersion   = "$BoxVersion"
        BoxFileName  = "$($AddVagrantCatalogVersionConfig.CatalogName).json"
        Force        = $Force
        Action       = @("Add")
        Verbose      = $VerbosePreference
        Debug        = $DebugPreference
    }

    Get-VagrantBox @AddVagrantBox

    Pop-Location
}

Pop-Location
