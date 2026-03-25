function Remove-EtcHostsEntry {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory = $true, Position = 0)] [string] $HostName,
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory = $true, Position = 1)] [string] $Ip
    )

    $EtcHostsFile = Get-EtcHostsFile

    $changed = $false

    if (![string]::IsNullOrEmpty($HostName)) {
        $changed = $true
        $content = Get-Content $EtcHostsFile | Select-String -Pattern "^.*$($HostName.Replace(".", "\."))" -NotMatch
    }

    if (![string]::IsNullOrEmpty($Ip)) {
        $changed = $true
        $content = Get-Content $EtcHostsFile | Select-String -Pattern "^.*$($Ip.Replace(".", "\."))" -NotMatch
    }

    if ($changed) {
        $content | Out-File $EtcHostsFile -Encoding ascii
    }
}
