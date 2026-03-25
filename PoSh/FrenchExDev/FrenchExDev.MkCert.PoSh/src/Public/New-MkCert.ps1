function New-MkCert {
    [CmdletBinding()]
    param(
        [string] $CertFile,
        [string] $KeyFile,
        [string[]] $HostName,
        [string[]] $IPAddress
    )

    $IPAddresses = $IPAddress -join " "
    $Hostnames = $Hostname -join " "
    $expression = "mkcert -cert-file ""$CertFile"" -key-file ""$KeyFile"" $HostNames $IPAddresses"
    Write-Debug "New-MkCert > CertFile: $CertFile, KeyFile: $KeyFile, HostNames: $HostNames, IpAddresses: $IPAddresses, Command: '$expression'"
    Invoke-Expression $expression
}
