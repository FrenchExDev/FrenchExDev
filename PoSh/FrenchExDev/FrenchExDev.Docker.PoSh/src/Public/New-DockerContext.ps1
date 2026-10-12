function New-DockerContext {
    [CmdletBinding()]
    param(
        [parameter(mandatory = $true, position = 0)] [string] $Name,
        [parameter(mandatory = $true, position = 1)] [string] $HostName,
        [parameter(Mandatory = $true, Position = 2)] [string] $RemoteUser,
        [switch] $Docker,
        [switch] $Ssh,
        [switch] $SwitchTo
    )

    Write-Verbose "New-DockerContext> Name: $Name, HostName: $HostName, RemoteUser: $RemoteUser"

    $provider = if ($Docker) { "--docker" } else { throw "not supported" }
    $providerHost = if ($Ssh) { "host=ssh://${RemoteUser}@${HostName}" } else { throw "not supported" }

    docker context create "$Name" "$provider" $providerHost

    if ($SwitchTo) {
        Switch-DockerContext -Name "$Name"
    }
}
