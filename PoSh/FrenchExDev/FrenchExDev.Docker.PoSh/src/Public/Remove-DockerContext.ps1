function Remove-DockerContext {
    [CmdletBinding()]
    param(
        [parameter(mandatory=$true, position=0)] [string] $Name,
        [switch] $Force
    )

    $ForceArg = if ($Force) { "-f" } else { "" }

    Write-Verbose "Remove-DockerContext> $Name> Force: $Force"
    docker context remove $Name $ForceArg
}
