function New-TraefikDockerComposeServiceLabel {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $TraefikDockerComposeNetwork,
        [string[]] $Labels,
        [switch] $Enable,
        [scriptblock] $Services,
        [scriptblock] $Routers,
        [scriptblock] $Middlewares
    )

    $InternalLabels = if ($null -ne $Labels) { $Labels } else { @() }

    $InternalLabels += @(
        "traefik.enable=$(Convert-BoolToJson $Enable.IsPresent)"
        "traefik.docker.network=$TraefikDockerComposeNetwork"
    )

    if ($null -ne $Services) {
        $InternalLabels += Invoke-Command $Services
    }

    if ($null -ne $Routers) {
        $InternalLabels += Invoke-Command $Routers
    }

    if ($null -ne $Middlewares) {
        $InternalLabels += Invoke-Command $Middlewares
    }

    $InternalLabels
}
