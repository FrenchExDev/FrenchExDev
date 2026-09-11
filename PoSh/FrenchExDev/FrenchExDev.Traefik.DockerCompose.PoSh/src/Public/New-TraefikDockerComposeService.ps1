function New-TraefikDockerComposeService {
    [CmdletBinding()]
    param(
        [string] $Image = "traefik",
        [string] $ImageVersion = "latest",
        [string] $ContainerName = "traefik",
        [string] $Restart = "unless-stopped",
        [string] $HostName,
        [scriptblock] $Build,
        [scriptblock] $Networks,
        [scriptblock] $Ports,
        [scriptblock] $Command,
        [scriptblock] $Labels,
        [scriptblock] $Volumes,
        [scriptblock] $Environment,
        [scriptblock] $Logging,
        [scriptblock] $Healthcheck,
        [scriptblock] $Links,
        [scriptblock] $DependsOn,
        [scriptblock] $ExtraHosts,
        [scriptblock] $Expose,
        [scriptblock] $CapAdd
    )

    $TraefikDockerComposeServiceConfig = @{
        Image         = "${Image}:${ImageVersion}"
        ContainerName = $ContainerName
        HostName      = $HostName
        Restart       = $Restart
        Build         = $Build
        Networks      = $Networks
        Ports         = $Ports
        Command       = $Command
        Labels        = $Labels
        Volumes       = $Volumes
        Environment   = $Environment
        Logging       = $Logging
        HealthCheck   = $Healthcheck
        Links         = $Links
        DependsOn     = $DependsOn
        ExtraHosts    = $ExtraHosts
        Expose        = $Expose
        CapAdd        = $CapAdd
    }

    New-DockerComposeService @TraefikDockerComposeServiceConfig
}
