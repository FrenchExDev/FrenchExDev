function New-DevPoshPowershellInstallCommand {
    [CmdletBinding()]
    param(
        [scriptblock] $Dependencies,
        [ValidateNotNullOrWhiteSpace()] [string] $PowerShellVersion,
        [ValidateNotNullOrWhiteSpace()] [string] $Flavor = "linux-musl-x64"
    )

    @(
        $($(Invoke-Command $Dependencies) -join [system.environment]::NewLine)
        "curl -L $(Get-PowershellReleaseUrl -PowerShellVersion $PowerShellVersion -Flavor $Flavor) -o /tmp/powershell.tar.gz"
        "mkdir -p /opt/microsoft/powershell/7"
        "tar zxf /tmp/powershell.tar.gz -C /opt/microsoft/powershell/7"
        "chmod +x /opt/microsoft/powershell/7/pwsh"
        "ln -s /opt/microsoft/powershell/7/pwsh /usr/bin/pwsh"
        "sudo /usr/bin/pwsh -Login -Command ""Install-Package powershell-yaml -Force -Scope AllUsers"""
    ) -Join [System.Environment]::NewLine

}
