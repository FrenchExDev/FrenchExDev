function Add-EtcHostsEntry {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory=$true,Position=0)] [string] $HostName,
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory=$true,Position=1)] [string] $Ip
    )

    $content = Get-EtcHostsFile GetContent

    $alreadyExists = $($content | select-string -Pattern "^.*$($HostName.Replace(".", "\."))" | Measure-Object).Count -gt 0

    if($alreadyExists) {
        return
    }

    $content += "$Ip    $HostName"

    Get-EtcHostsFile SetContent $content
}
