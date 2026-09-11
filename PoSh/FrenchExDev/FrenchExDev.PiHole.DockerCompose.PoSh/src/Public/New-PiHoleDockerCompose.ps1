function New-PiHoleDockerCompose {
    [CmdletBinding()]
    param(
        [string] $ServiceName = "pihole",
        [string] $Image = "pihole/pihole:latest",
        [string] $Tz = "Europe/Paris",
        [string] $WebApiPasswd
    )

    $PiHoleService = @{
        ContainerName = $ServiceName
        Image         = $Image
        Ports         = {
            @(
                $(New-DockerComposeServicePort -Target 53 -Published 53 -Proto tcp),
                $(New-DockerComposeServicePort -Target 53 -Published 53 -Proto udp),
                $(New-DockerComposeServicePort -Target 80 -Published 80 -Proto tcp),
                $(New-DockerComposeServicePort -Target 443 -Published 443 -Proto tcp)
            )
        }
        Environment   = {
            @{
                TZ                             = "$Tz"
                FTLCONF_webserver_api_password = "$WebApiPasswd"
                FTLCONF_dns_listeningMode      = 'all'
            }
        }
        Volumes       = {
            @("./etc-pihole:/etc/pihole")
        }
        CapAdd        = {
            @("NET_ADMIN", "SYS_TIME", "SYS_NICE")
        }
        Restart       = "unless-stopped"
    }

    New-DockerCompose -File "docker-compose.yaml" -Services {
        @{
            "pihole" = New-DockerComposeService @PiHoleService
        }
    }
}