function New-TraefikDockerCompose {
    param(
        [string] $File = "./docker-compose/docker-compose.traefik.yaml",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceName = "traefik",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceImage = "traefik",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceImageVersion = "latest",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceContainerName = "traefik",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceRestart = "unless-stopped",
        [string] $ServiceHostName = 'traefik.${ACME_NAME}.${ACME_TLD}',
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
        [scriptblock] $ServiceCapAdd,
        [string] $Interval = "10s",
        [int] $Retries = 20,
        [string] $StartPeriod = "30s",
        [string] $Timeout = "10s"
    )

    $TraefikDockerComposeServiceConfig = @{
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
        Volumes       = $ServiceVolumes
        Environment   = $ServiceEnvironment
        Logging       = $ServiceLogging
        HealthCheck   = {
            $DockerComposeServiceHealthCheckConfig = @{
                Test        = $(@("traefik", "healthcheck", "--ping") -Join " ")
                Interval    = $Interval
                Retries     = $Retries
                StartPeriod = $StartPeriod
                Timeout     = $Timeout
            }

            New-DockerComposeServiceHealthCheck @DockerComposeServiceHealthCheckConfig
        }.GetNewClosure()
        Links         = $ServiceLinks
        DependsOn     = $ServiceDependsOn
        ExtraHosts    = $ServiceExtraHosts
        Expose        = $ServiceExpose
        CapAdd        = $ServiceCapAdd
    }

    $DockerComposeConfig = @{
        File    = $File
        Service = {
            @{
                "$ServiceName" = New-TraefikDockerComposeService @TraefikDockerComposeServiceConfig
            }
        }.GetNewClosure()
    }

    New-DockerCompose @DockerComposeConfig
}
