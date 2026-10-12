# taken from https://gist.githubusercontent.com/KaiWalter/b5dc222b1ff67f618b9ff076ca3d6a21/raw/ebded1f2fc38ed163ae18ad918ed5c81f3e3b131/ManageSshConfig.psm1
# samples

# $HostList = Get-ConfigHostList

# $HostList = Add-ConfigHostInList -HostList $HostList -HostName "dummy" -HostValues @{
#     identityfile = "~/.ssh/myprivatekey"
#     hostname     = "dummy.somecloud.com"
#     user         = "johndoe"
# }

# $HostList = Update-ConfigHostInList -HostList $HostList -HostName "dummy" -HostValues @{
#     identityfile = "~/.ssh/myprivatekey"
#     hostname     = "dummy.somecloud.com"
#     user         = "johndoe"
# }

# $HostList = Remove-ConfigHostFromList -HostList $HostList -HostName "dummy"

# Set-ConfigHostList $HostList

# Get-ConfigHostList | Remove-ConfigHostFromList -HostName "dummy" | Set-ConfigHostList

function Get-LineBreaks {
    param (
        [Parameter(Mandatory = $true)]
        [string] $Contents
    )

    # determine line break LF or CR/LF
    if ($Contents -match "^[^\n]+\r\n") {
        $splitter = "\r\n"
        $joiner = "`r`n"
    }
    else {
        $splitter = "\n"
        $joiner = "`n"
    }

    return $splitter, $joiner
}

function Get-ConfigFileName {
    return Join-Path $HOME ".ssh" "config" -Resolve
}

function Get-ConfigContents {
    $configFilename = Get-ConfigFileName    
    return Get-Content $configFilename -Raw
}

function Set-ConfigContents {
    param (
        [Parameter(Mandatory = $true)]
        [string] $Contents
    )

    $configFilename = Get-ConfigFileName    
    if ($Contents) {
        $Contents | Set-Content $configFilename
    }
}

function Get-ConfigKeyWords {
    return @("Match",
        "AddressFamily",
        "BatchMode",
        "BindAddress",
        "ChallengeResponseAuthentication",
        "CheckHostIP",
        "Cipher",
        "Ciphers",
        "ClearAllForwardings",
        "Compression",
        "CompressionLevel",
        "ConnectionAttempts",
        "ConnectTimeout",
        "ControlMaster",
        "ControlPath",
        "DynamicForward",
        "EscapeChar",
        "ExitOnForwardFailure",
        "ForwardAgent",
        "ForwardX11",
        "ForwardX11Trusted",
        "GatewayPorts",
        "GlobalKnownHostsFile",
        "GSSAPIAuthentication",
        "GSSAPIKeyExchange",
        "GSSAPIClientIdentity",
        "GSSAPIDelegateCredentials",
        "GSSAPIRenewalForcesRekey",
        "GSSAPITrustDns",
        "HashKnownHosts",
        "HostbasedAuthentication",
        "HostKeyAlgorithms",
        "HostKeyAlias",
        "HostName",
        "IdentitiesOnly",
        "IdentityFile",
        "KbdInteractiveAuthentication",
        "KbdInteractiveDevices",
        "LocalCommand",
        "LocalForward",
        "LogLevel",
        "MACs",
        "NoHostAuthenticationForLocalhost",
        "PreferredAuthentications",
        "Protocol",
        "ProxyCommand",
        "PubkeyAuthentication",
        "RemoteForward",
        "RhostsRSAAuthentication",
        "RSAAuthentication",
        "SendEnv",
        "ServerAliveCountMax",
        "ServerAliveInterval",
        "SmartcardDevice",
        "StrictHostKeyChecking",
        "TCPKeepAlive",
        "Tunnel",
        "TunnelDevice",
        "UsePrivilegedPort",
        "User",
        "UserKnownHostsFile",
        "VerifyHostKeyDNS",
        "VisualHostKey")
}

function Get-ConfigHostList {
    param(
        [string] $contents
    )

    $keywords = Get-ConfigKeyWords
    if([string]::IsNullOrEmpty($contents)) {
        $contents = Get-ConfigContents
    }

    if([string]::IsNullOrEmpty($contents)) {
        return
    }

    $splitter, $joiner = Get-LineBreaks $contents

    # split by "Host" - when at start of file or has prededing line breaks / whitespaces 
    $splitEntries = "(?i)(^|" + $splitter + "+\s+)host\s"
    $list = [regex]::Split($contents, $splitEntries)
    if ($list.Count -le 1) {
        return
    }

    # READ lists of hosts

    $HostList = @{}

    foreach ($entry in $list) {
        # $output += $entry -replace $($splitter+"\s+"), $($joiner+"  ")
        $attributes = [regex]::Split($entry, $splitter) | % { $_.Trim() }
        $HostName = $null
        $HostValues = @{}
        foreach ($attribute in $attributes) {
            if ($attribute -ne "") {
                if ($HostName) {
                    # split key/value and normalize key name
                    $kv = [regex]::Split($attribute, "\s+", 1)
                    $keyName = $kv[0]
                    $keyValue = $kv[1]
                    foreach ($keyword in ($keywords | ? { $_ -eq $keyName })) {
                        $keyName = $keyword                    
                        break
                    }
                    $HostValues[$keyName] = $keyValue
                }
                else {
                    # assume first entry to be the host
                    $HostName = $attribute.ToLower()
                }
            }
        }
        if ($HostName) {
            $HostList[$HostName] = $HostValues
        }
    }

    return $HostList
}

function Get-ConfigHostFromList {
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline=$true)]
        [hashtable]
        $HostList,
        [Parameter(Mandatory = $true)]
        [string]
        $HostName,
        [switch]
        $IgnoreNonExisting
    )

    $hostEntry = $null

    if ($HostList.ContainsKey($HostName.ToLower())) {
        $hostEntry = $HostList[$HostName.ToLower()]
    }
    else {
        if (!$IgnoreNonExisting) {
            throw "HostName $HostName does not exist"
        }
    }

    return $hostEntry
}

function Update-ConfigHostInList {
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline=$true)]
        [hashtable]
        $HostList,
        [Parameter(Mandatory = $true)]
        [string]
        $HostName,
        [Parameter(Mandatory = $true)]
        [hashtable]
        $HostValues
    )

    if ($HostList.ContainsKey($HostName.ToLower())) {
        $HostList = Remove-ConfigHostFromList -HostList $HostList -HostName $HostName
        $HostList = Add-ConfigHostToList -HostList $HostList -HostName $HostName -HostValues $HostValues
    }
    else {
        throw "HostName $HostName does not exist"
    }

    return $HostList
}

function Set-ConfigHostList {
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline=$true)]
        [hashtable] $HostList
    )

    $splitter, $joiner = Get-LineBreaks $(Get-ConfigContents)

    $output = @()

    foreach ($hostEntry in $HostList.GetEnumerator()) {
        $hostOutput = "Host " + $hostEntry.Key + $joiner
        foreach ($kv in $hostEntry.Value.GetEnumerator()) {
            $hostOutput = $hostOutput + "  " + $kv.key + " " + $kv.value + $joiner
        }
        $output += $hostOutput
    }

    if ($output) {
        Set-ConfigContents $($output -join $($joiner))
    }
}

function Add-ConfigHostToList {
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline=$true)]
        [hashtable]
        $HostList,
        [Parameter(Mandatory = $true)]
        [string]
        $HostName,
        [Parameter(Mandatory = $true)]
        [hashtable]
        $HostValues,
        [switch]
        $IgnoreExisting
    )

    if (!$IgnoreExisting) {
        if ($HostList.ContainsKey($HostName.ToLower())) {
            throw "HostName $HostName already exists"
        }
    }

    $keywords = Get-ConfigKeyWords
    $hostValuesCleaned = @{}
    foreach ($kv in $HostValues.GetEnumerator()) {
        $keyName = $null
        foreach ($keyword in ($keywords | ? { $_ -eq $kv.Key })) {
            $keyName = $keyword                    
            break
        }
        if ($keyName) {
            $hostValuesCleaned[$keyName] = $kv.Value.Trim()
        }
        else {
            throw "key $($kv.Key) not found in list of keywords" 
        }
    }
    if ($HostValuesCleaned) {
        $hostList[$HostName.ToLower()] = $HostValuesCleaned
    }

    return $HostList
}

function Remove-ConfigHostFromList {
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline=$true)]
        [hashtable]
        $HostList,
        [Parameter(Mandatory = $true)]
        [string]
        $HostName
    )

    if ($HostList.ContainsKey($HostName.ToLower())) {
        $HostList.Remove($HostName.ToLower())
    }
    else {
        throw "HostName $HostName not found in list"
    }

    return $HostList
}

Export-ModuleMember Get-ConfigHost*
Export-ModuleMember Set-ConfigHost*
Export-ModuleMember Add-ConfigHost*
Export-ModuleMember Remove-ConfigHost*
Export-ModuleMember Update-ConfigHost*