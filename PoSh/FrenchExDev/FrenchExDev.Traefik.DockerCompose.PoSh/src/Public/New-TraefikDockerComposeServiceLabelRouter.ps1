function New-TraefikDockerComposeServiceLabelRouter {
    param(
        [string] $Proto,
        [string] $Name,
        [string] $Key,
        [string] $Value
    )

    $("traefik.${proto}.routers.${name}.${Key}=${Value}")
}
