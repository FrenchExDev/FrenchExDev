function New-KeycloakDockerCompose {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $File = "./docker-compose/docker-compose.keycloak.yaml",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceName = "keycloak",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceImage = "keycloak/keycloak",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceImageVersion = "latest",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceContainerName = "keycloak",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceRestart = "unless-stopped",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceHostName = "keycloak.`${ACME_NAME}.`${ACME_TLD}",
        [int] $ServiceShmSize = 512MB / 1MB,
        [scriptblock] $ServiceBuild,
        [scriptblock] $ServiceNetworks,
        [scriptblock] $ServicePorts,
        [scriptblock] $ServiceCommand,
        [scriptblock] $ServiceLabels,
        [scriptblock] $ServiceVolumes,
        [scriptblock] $ServiceEnvironment,
        [scriptblock] $ServiceLogging,
        [scriptblock] $ServiceLinks,
        [scriptblock] $ServiceDependsOn,
        [scriptblock] $ServiceExtraHosts,
        [scriptblock] $ServiceExpose,
        [scriptblock] $ServiceCapAdd
    )

    $KeycloakbDockerComposeServiceConfig = @{
        Image         = $ServiceImage
        ImageVersion  = $ServiceImageVersion
        ContainerName = $ServiceContainerName
        HostName      = $ServiceHostName
        Restart       = $ServiceRestart
        Build         = $ServiceBuild
        Networks      = $ServiceNetworks
        Ports         = $ServicePorts
        Command       = $ServiceCommand
        Labels        = $ServiceLabels
        Environment   = $ServiceEnvironment
        Logging       = $ServiceLogging
        Links         = $ServiceLinks
        DependsOn     = $ServiceDependsOn
        ExtraHosts    = $ServiceExtraHosts
        Expose        = $ServiceExpose
        CapAdd        = $ServiceCapAdd
    }

    $DockerComposeConfig = @{
        File     = $File
        Services = {
            @{
                "$ServiceName" = New-KeycloakDockerComposeService @KeycloakbDockerComposeServiceConfig
            }
        }
        Volumes  = {
            @{
            }
        }
        Networks = {
            @{
            }
        }
    }

    New-DockerCompose @DockerComposeConfig
}
