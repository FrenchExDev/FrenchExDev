function Invoke-DevSetup {
    param(
        [string] $DevSetupClonePath,
        [string[]] $Dotnet = @('9')
    )

    Install-DevSetupPowershellPackage
    Install-DevSetupDotnet -Versions $Dotnet
    Install-DevSetupSoftware `
        -WingetSoftwaresFile ./softwares-winget-source-winget.dat `
        -MsStoreSoftwaresFile ./softwares-winget-source-msstore.dat

    Set-NetIPInterface -Forwarding Enabled

    Install-VagrantPlugins -Reload
}
