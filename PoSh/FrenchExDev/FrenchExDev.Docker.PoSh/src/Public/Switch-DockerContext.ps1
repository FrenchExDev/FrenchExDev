function Switch-DockerContext {
    [CmdletBinding()]
    param(
        [parameter(mandatory = $true, position = 0)] [string] $Name
    )

    Write-Verbose "Switch-DockerContext> $Name"
    docker context use $Name
}
