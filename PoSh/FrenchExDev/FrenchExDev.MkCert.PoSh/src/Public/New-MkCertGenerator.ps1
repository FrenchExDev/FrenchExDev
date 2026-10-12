function New-MkCertGenerator {
    [CmdletBinding()]
    param(
        [string] $Path,
        [string] $CertFile,
        [string] $KeyFile,
        [string[]] $HostName,
        [string[]] $IpAddress,
        [switch] $Force
    )

    $InternalVerboseDebugConfig = @{
        Verbose = $VerbosePreference
        Debug   = $DebugPreference
    }

    $certFile = "$path/$($CertFile)"
    $keyFile = "$path/$($KeyFile)"

    if ($Force -or $($($false -eq $(Test-Path $certFile) -or $false -eq $(Test-Path $keyFile)))) {
        if (!(Test-Path "$Path")) {
            New-Item -ItemType Directory "$Path" | Out-Null
        }

        $NewMkCertConfig = @{
            CertFile  = $CertFile
            KeyFile   = $KeyFile
            HostName  = $HostName
            IpAddress = $IpAddress
        }

        New-MkCert @NewMkCertConfig @InternalVerboseDebugConfig | Out-Null
    }
}
