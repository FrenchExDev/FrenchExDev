function New-TraefikDockerComposeServiceLabelService {
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $Proto,
        [ValidateNotNullOrWhiteSpace()] [string] $Name,
        [ValidateNotNullOrWhiteSpace()] [string] $Key,
        [ValidateNotNullOrWhiteSpace()] [string] $Value
    )

    $("traefik.${proto}.services.${Name}.${Key}=${Value}")
}
