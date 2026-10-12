function New-GitLabDockerCompose {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $File = "./docker-compose/docker-compose.gitlab-ce.yaml",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceName = "gitlab",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceImage = "gitlab/gitlab-ce",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceImageVersion = "latest",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceContainerName = "gitlab",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceRestart = "unless-stopped",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceHostName = "gitlab.`${ACME_NAME}.`${ACME_TLD}",
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

    $GitLabDockerComposeServiceConfig = @{
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
        Volumes       = {
            $InternalServiceVolumes = @(
                "gitlab-config:/etc/gitlab"
                "gitlab-logs:/var/log/gitlab"
                "gitlab-data:/var/opt/gitlab"
            )

            if ($null -ne $ServiceVolumes) {
                $InternalServiceVolumes += @(Invoke-Command $ServiceVolumes -ArgumentList $InternalServiceVolumes)
            }

            $InternalServiceVolumes
        }
        Environment   = $ServiceEnvironment
        Logging       = $ServiceLogging
        Links         = $ServiceLinks
        DependsOn     = $ServiceDependsOn
        ExtraHosts    = $ServiceExtraHosts
        Expose        = $ServiceExpose
        CapAdd        = $ServiceCapAdd
        HealthCheck   = {
            $HealthcheckConfig = @{
                Test        = "curl -f http://localhost:80/health"
                Interval    = "10s"
                Retries     = 20
                StartPeriod = "30s"
                Timeout     = "10s"
            }
            New-DockerComposeServiceHealthCheck @HealthcheckConfig
        }
    }

    $DockerComposeConfig = @{
        File     = $File
        Services = {
            @{
                "$ServiceName" = New-GitLabDockerComposeService @GitLabDockerComposeServiceConfig
            }
        }
        Volumes  = {
            @{
                "gitlab-config" = @{}
                "gitlab-data"   = @{}
                "gitlab-logs"   = @{}
            }
        }
        Networks = {
            @{
                "gitlab" = @{}
            }
        }
    }

    New-DockerCompose @DockerComposeConfig
}
