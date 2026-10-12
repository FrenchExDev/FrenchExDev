function New-TraefikDockerComposeServiceLabelMiddleware {
    param(
        [string] $Proto,
        [string] $Name,
        [string] $Key,
        [string] $Value
    )

    $("traefik.${proto}.middlewares.${Name}.${Key}=${Value}")
}