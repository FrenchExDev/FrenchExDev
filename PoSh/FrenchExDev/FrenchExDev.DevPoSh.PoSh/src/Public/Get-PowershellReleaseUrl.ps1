function Get-PowershellReleaseUrl {
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $PowerShellVersion,
        [ValidateNotNullOrWhiteSpace()] [string] $Flavor = "linux-musl-x64"
    )

    "https://github.com/PowerShell/PowerShell/releases/download/v${PowerShellVersion}/powershell-${PowerShellVersion}-${flavor}.tar.gz"
}
