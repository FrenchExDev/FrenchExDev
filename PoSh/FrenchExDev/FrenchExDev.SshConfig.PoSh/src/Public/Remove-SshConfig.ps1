function Remove-SshConfig {
    [CmdletBinding()]
    param(
        [parameter(Mandatory = $true, Position = 0, ValueFromPipelineByPropertyName = $true)][string] $HostName
    )

    $config = Get-ConfigHostList

    if ($config.$HostName) {
        $config | Remove-ConfigHostFromList -HostName $HostName | Set-ConfigHostList
    }
}
