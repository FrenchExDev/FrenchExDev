function New-PackerAlpineAnswerFileContent {
    [CmdletBinding()]
    param(
        [string] $KeyMap,
        [string] $HostName,
        [string] $DevDevice,
        [string] $Interfaces,
        [string] $TimeZone,
        [string] $Proxy,
        [string] $ApkRepos,
        [string] $Sshd,
        [string] $Ntp,
        [string] $Disk,
        [string] $Lbu,
        [string] $ApkCache,
        [string] $EraseDisks
    )

    $str = @()

    if (![string]::IsNullOrEmpty($KeyMap)) {
        $str += @("KEYMAPOPTS=""$KeyMap""")
    }

    if (![string]::IsNullOrEmpty($HostName)) {
        $str += @("HOSTNAMEOPTS=$HostName")
    }

    if (![string]::IsNullOrEmpty($DevDevice)) {
        $str += @("DEVDOPTS=$DevDevice")
    }

    if (![string]::IsNullOrEmpty($Interfaces)) {
        $str += @("INTERFACESOPTS=""$Interfaces""")
    }

    if (![string]::IsNullOrEmpty($TimeZone)) {
        $str += @("TIMEZONEOPTS=""$TimeZone""")
    }

    if (![string]::IsNullOrEmpty($Proxy)) {
        $str += @("PROXYOPTS=$Proxy")
    }

    if (![string]::IsNullOrEmpty($ApkRepos)) {
        $str += @("APKREPOSOPTS=""$ApkRepos""")
    }

    if (![string]::IsNullOrEmpty($Sshd)) {
        $str += @("SSHDOPTS=""$Sshd""")
    }

    if (![string]::IsNullOrEmpty($Ntp)) {
        $str += @("NTPOPTS=""$Ntp""")
    }

    if (![string]::IsNullOrEmpty($Disk)) {
        $str += @("DISKOPTS=""$Disk""")
    }

    if (![string]::IsNullOrEmpty($Lbu)) {
        $str += @("LBUOPTS=$Lbu")
    }

    if (![string]::IsNullOrEmpty($ApkCache)) {
        $str += @("APKCACHEOPTS=$ApkCache")
    }

    if (![string]::IsNullOrEmpty($EraseDisks)) {
        $str += @("export ERASE_DISKS=$EraseDisks")
    }

    $str -join [system.environment]::NewLine
}