function New-SonatypeNexusDockerCompose {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $File = "./docker-compose/docker-compose.nexus.yaml",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceName = "nexus",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceContainerName = "nexus",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceImage = "sonatype/nexus3",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceImageVersion = "latest",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceRestart = "unless-stopped",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceNetwork = "traefik",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceHostName = "nexus.`${ACME_NAME}.`${ACME_TLD}",
        [scriptblock] $ServiceLabels,
        [scriptblock] $ServiceLogging,
        [scriptblock] $ServiceNetworks
    )

    $DockerComposeServiceConfig = @{
        Image         = "${ServiceImage}:${ServiceImageVersion}"
        ContainerName = $ServiceContainerName
        HostName      = $ServiceHostName
        Restart       = $ServiceRestart
        Labels        = $ServiceLabels
        Logging       = $ServiceLogging
        Volumes       = {
            @(, , "./data/nexus/:/nexus-data:rw")
        }
        Networks      = {
            $InternalNetworks = @(, , "$ServiceNetwork")

            if ($null -ne $ServiceNetworks) {
                $InternalNetworks += @(Invoke-Command $ServiceNetworks)
            }

            $InternalNetworks
        }.GetNewClosure()
        DependsOn     = {
            @(, , "$ServiceNetwork")
        }.GetNewClosure()
        Healthcheck   = {
            $HealthcheckConfig = @{
                Test        = "curl http://localhost:8081"
                Interval    = "10s"
                Retries     = 30
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
                "$ServiceName" = New-DockerComposeService @DockerComposeServiceConfig
            }
        }.GetNewClosure()
    }

    New-DockerCompose @DockerComposeConfig
}
