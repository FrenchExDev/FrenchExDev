function Install-DevSetupPowershellPackage {
    [CmdletBinding()]
    param(
        [string] $PackagesFile = "$PSScriptRoot/../posh-packages.dat"
    )

    $packagesToInstall = Get-Content $PackagesFile

    foreach($packageToInstall in $packagesToInstall) {
        Write-Verbose "Install-DevSetupPowershellPackages> Installing package '$packageToInstall'"
        Install-Package $packagesToInstall -Force
    }
}
