function Write-DockerCompose {
    [CmdletBinding()]
    param(
        [object] $DockerCompose,
        [string] $File = "docker-compose.yaml"
    )

    if(!(Test-Path $(Split-Path $File -Parent))) {
        new-item -ItemType Directory $(Split-Path $File -Parent) | Out-Null
    }

    $DockerCompose | ConvertTo-Yaml | Out-File $File -Encoding ascii | Out-Null
}