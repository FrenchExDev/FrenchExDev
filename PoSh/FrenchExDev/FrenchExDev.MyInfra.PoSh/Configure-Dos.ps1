[CmdletBinding()]
param(
    [ValidateNotNullOrWhiteSpace()] [string] $DockerComposePath = "./docker-compose",
    [ValidateNotNullOrWhiteSpace()] [string] $Domain = "frenchexdev",
    [ValidateNotNullOrWhiteSpace()] [string] $DomainTld = "lab",
    [ValidateNotNullOrWhiteSpace()] [string] $LogLevel = "DEBUG",
    [ValidateNotNullOrWhiteSpace()] [string] $TimeZone = "Europe/Paris",
    [ValidateNotNullOrWhiteSpace()] [string] $DockerComposeProjectName
)

$InternalVerboseDebugConfig = @{
    Verbose = $VerbosePreference
    Debug   = $DebugPreference
}

$DefaultDockerComposeServiceLogging = {
    @{
        driver  = "json-file"
        options = @{
            "max-size" = "1m"
            "max-file" = 1
        }
    }
}

$DockerComposeSymbol = "docker-compose"

$CompositeSymbols = @{
    Composite = "$DockerComposeSymbol"
    Traefik   = "$($DockerComposeSymbol).traefik"
    Nexus     = "$($DockerComposeSymbol).nexus"
    GitLab    = "$($DockerComposeSymbol).gitlab-omnibus"
}

$CompositeServicesSymbols = @{
    Nexus   = "nexus"
    Traefik = "traefik"
    GitLab  = "gitlab"
}

$ProtoSymbols = @{
    http = "http"
    tcp  = "tcp"
}

$TraefikConfig = New-TraefikDockerComposeOpenConfig -Openness {
    @(
        $(New-TraefikDockerComposeOpenConfigItem -Name "http" -Port 80 -PublishPort 80 -Proto "$($ProtoSymbols.tcp)" -Mode "host")
        $(New-TraefikDockerComposeOpenConfigItem -Name "https" -Port 443 -PublishPort 443 -Proto "$($ProtoSymbols.tcp)" -Mode "host")
        $(New-TraefikDockerComposeOpenConfigItem -Name "ssh" -Port 2222 -PublishPort 2222 -Proto "$($ProtoSymbols.tcp)" -Mode "host")
        $(New-TraefikDockerComposeOpenConfigItem -Name "docker-registry" -Port 5000 -PublishPort 5000 -Proto "$($ProtoSymbols.tcp)" -Mode "host")
    )
}

$TraefikDockerComposeGeneratorConfig = @{
    File                        = "./$DockerComposePath/$($CompositeSymbols.Traefik).yaml"
    ServiceProviders            = {
        @(, ,
            $(New-TraefikDockerComposeServiceLabelProvider -Name "file" -Key "filename" -Value "/etc/traefik/tls.yml")
        )
    }.GetNewClosure()
    ServiceEntryPoints          = {
        $entryPoints = @()

        $TraefikConfig.openess | ForEach-Object { 
            $entryPoints += $(New-TraefikDockerComposeServiceLabelEntryPoint -Name "$($_.name)" -Key "address" -Value ":$($_.port)") 
        }

        $entryPoints
    }.GetNewClosure()
    ServicePorts                = {
        $ports = @()

        $TraefikConfig.openess | ForEach-Object { 
            $ports += $( $(New-DockerComposeServicePort -Target $_.port -Published $_.publish -Proto $_.proto -Mode "host")) 
        }

        $ports
    }.GetNewClosure()
    ServiceVolumes              = {
        @(
            "./data/traefik/dynamic/:/etc/traefik/dynamic/:r",
            "./data/traefik/tls.yml:/etc/traefik/tls.yml:r",
            "./data/certs/:/etc/ssl/traefik/:r"
        )
    }
    ServiceTimeZone             = $TimeZone
    ServiceLogging              = $DefaultDockerComposeServiceLogging
    ServiceLogLevel             = $LogLevel
    TraefikDockerComposeNetwork = "$($DockerComposeProjectName)_$($CompositeServicesSymbols.Traefik)"
}

New-TraefikDockerComposeGenerator @TraefikDockerComposeGeneratorConfig @InternalVerboseDebugConfig

$SonatypeNexusDockerComposeSymbols = @{
    Nexus               = "nexus"
    NexusDockerRegistry = "nexus-docker-registry"
}

$SonatypeNexusDockerComposeGeneratorConfig = @{
    File            = "./$DockerComposePath/$($CompositeSymbols.Nexus).yaml"
    ServiceLabels   = {
        $InternalTraefikDockerComposeServiceLabelConfig = @{
            Enable                      = $true
            TraefikDockerComposeNetwork = $TraefikDockerComposeGeneratorConfig.TraefikDockerComposeNetwork
            Routers                     = {
                @(
                    $(New-TraefikDockerComposeServiceLabelRouter -Proto "$($ProtoSymbols.http)" -Name "$($SonatypeNexusDockerComposeSymbols.Nexus)" -Key "$($TraefikSymbols.Keys.Rule)" -value "Host(``nexus.${Domain}.${DomainTld}``)")
                    $(New-TraefikDockerComposeServiceLabelRouter -Proto "$($ProtoSymbols.http)" -Name "$($SonatypeNexusDockerComposeSymbols.Nexus)" -Key "$($TraefikSymbols.Keys.Service)" -value "$($CompositeServicesSymbols.Nexus)")
                    $(New-TraefikDockerComposeServiceLabelRouter -Proto "$($ProtoSymbols.http)" -Name "$($SonatypeNexusDockerComposeSymbols.Nexus)" -Key "$($TraefikSymbols.Keys.Entrypoints)" -value "https")
                    $(New-TraefikDockerComposeServiceLabelRouter -Proto "$($ProtoSymbols.http)" -Name "$($SonatypeNexusDockerComposeSymbols.Nexus)" -Key "$($TraefikSymbols.Keys.Tls)" -value "true")

                    $(New-TraefikDockerComposeServiceLabelRouter -Proto "$($ProtoSymbols.http)" -Name "$($SonatypeNexusDockerComposeSymbols.NexusDockerRegistry)" -Key "$($TraefikSymbols.Keys.Rule)" -Value "Host(``docker-registry.${Domain}.${DomainTld}``)")
                    $(New-TraefikDockerComposeServiceLabelRouter -Proto "$($ProtoSymbols.http)" -Name "$($SonatypeNexusDockerComposeSymbols.NexusDockerRegistry)" -Key "$($TraefikSymbols.Keys.Service)" -Value "$($CompositeServicesSymbols.Nexus)")
                    $(New-TraefikDockerComposeServiceLabelRouter -Proto "$($ProtoSymbols.http)" -Name "$($SonatypeNexusDockerComposeSymbols.NexusDockerRegistry)" -Key "$($TraefikSymbols.Keys.Entrypoints)" -Value "https")
                    $(New-TraefikDockerComposeServiceLabelRouter -Proto "$($ProtoSymbols.http)" -Name "$($SonatypeNexusDockerComposeSymbols.NexusDockerRegistry)" -Key "$($TraefikSymbols.Keys.Tls)" -Value "true")
                )
            }
            Services                    = {
                @(
                    $(New-TraefikDockerComposeServiceLabelService -Proto "$($ProtoSymbols.http)" -Name "$($SonatypeNexusDockerComposeSymbols.Nexus)" -Key "$($TraefikSymbols.Keys.LoadBalancer.Server.Port)" -value "8081")
                    $(New-TraefikDockerComposeServiceLabelService -Proto "$($ProtoSymbols.http)" -Name "$($SonatypeNexusDockerComposeSymbols.Nexus)" -Key "$($TraefikSymbols.Keys.LoadBalancer.PassHostHeader)" -value "true")

                    $(New-TraefikDockerComposeServiceLabelService -Proto "$($ProtoSymbols.http)" -Name "$($SonatypeNexusDockerComposeSymbols.NexusDockerRegistry)" -Key "$($TraefikSymbols.Keys.LoadBalancer.Server.Port)" -Value "5000")
                    $(New-TraefikDockerComposeServiceLabelService -Proto "$($ProtoSymbols.http)" -Name "$($SonatypeNexusDockerComposeSymbols.NexusDockerRegistry)" -Key "$($TraefikSymbols.Keys.LoadBalancer.PassHostHeader)" -Value "true")
                )
            }
        }
        New-TraefikDockerComposeServiceLabel @InternalTraefikDockerComposeServiceLabelConfig
    }.GetNewClosure()
    ServiceLogging  = $DefaultDockerComposeServiceLogging
    ServiceNetwork = "traefik"
}

New-SonatypeNexusDockerComposeGenerator @SonatypeNexusDockerComposeGeneratorConfig @InternalVerboseDebugConfig

$GitLabDockerComposeSymbols = @{
    GitLab    = "gitlab"
    GitLabSsh = "gitlab-ssh"
}

$GitLabDockerComposeGeneratorConfig = @{
    File                = "./$DockerComposePath/$($CompositeSymbols.GitLab).yaml"
    ServiceLabels       = {
        $TraefikDockerComposeServiceLabelConfig = @{
            Enable                      = $true
            TraefikDockerComposeNetwork = $TraefikDockerComposeGeneratorConfig.TraefikDockerComposeNetwork
            Routers                     = {
                @(
                    $(New-TraefikDockerComposeServiceLabelRouter -Proto "$($ProtoSymbols.http)" -Name "$($GitLabDockerComposeSymbols.GitLab)" -Key "$($TraefikSymbols.Keys.Rule)" -Value "Host(``gitlab.${ACME_NAME}.${ACME_TLD}``)")
                    $(New-TraefikDockerComposeServiceLabelRouter -Proto "$($ProtoSymbols.http)" -Name "$($GitLabDockerComposeSymbols.GitLab)" -Key "$($TraefikSymbols.Keys.Service)" -Value "$($CompositeServicesSymbols.GitLab)")
                    $(New-TraefikDockerComposeServiceLabelRouter -Proto "$($ProtoSymbols.http)" -Name "$($GitLabDockerComposeSymbols.GitLab)" -Key "$($TraefikSymbols.Keys.Entrypoints)" -Value "https")
                    $(New-TraefikDockerComposeServiceLabelRouter -Proto "$($ProtoSymbols.http)" -Name "$($GitLabDockerComposeSymbols.GitLab)" -Key "$($TraefikSymbols.Keys.Tls)" -Value "true")
                    $(New-TraefikDockerComposeServiceLabelRouter -Proto "$($ProtoSymbols.tcp)" -Name "$($GitLabDockerComposeSymbols.GitLabSsh)" -Key "$($TraefikSymbols.Keys.Entrypoints)" -Value "ssh")
                    $(New-TraefikDockerComposeServiceLabelRouter -Proto "$($ProtoSymbols.tcp)" -Name "$($GitLabDockerComposeSymbols.GitLabSsh)" -Key "$($TraefikSymbols.Keys.Rule)" -Value 'HostSNI(`*`)')
                )
            }
            Services                    = {
                @(
                    $(New-TraefikDockerComposeServiceLabelService -Proto "$($ProtoSymbols.http)" -Name "$($GitLabDockerComposeSymbols.GitLab)" -Key "$($TraefikSymbols.Keys.LoadBalancer.Server.Port)" -Value 80)
                    $(New-TraefikDockerComposeServiceLabelService -Proto "$($ProtoSymbols.http)" -Name "$($GitLabDockerComposeSymbols.GitLab)" -Key "$($TraefikSymbols.Keys.LoadBalancer.PassHostHeader)" -Value "true")
                    $(New-TraefikDockerComposeServiceLabelService -Proto "$($ProtoSymbols.tcp)" -Name "$($GitLabDockerComposeSymbols.GitLabSsh)" -Key "$($TraefikSymbols.Keys.LoadBalancer.Server.Port)" -Value 22)
                )
            }
        }

        New-TraefikDockerComposeServiceLabel @TraefikDockerComposeServiceLabelConfig
    }
    ServiceImageVersion = "18.1.0-ce.0"
    ServiceShmSize      = 1GB / 1MB
    ServiceRestart      = $DockerComposeSymbols.Restart.UnlessStopped
    ServiceLogging      = $DefaultServiceLogging
    ServiceVolumes      = {
        @(
            "./data/certs/:/etc/gitlab/ssl:r"
            "./data/gitlab/gitlab.rb:/etc/gitlab/gitlab.rb"
        )
    }
    ServiceNetworks     = {
        @(, , "$($CompositeServicesSymbols.Traefik)")
    }
}

New-GitLabDockerComposeGenerator @GitLabDockerComposeGeneratorConfig @InternalVerboseDebugConfig

$GitLabConfig = @{
    LetsEncryptEnable   = 'false'
    Domain              = $Domain
    DomainTld           = $DomainTld
    WebPort             = 80
    SshPort             = 2222
    RegistryPort        = 5000
    InitialRootPassword = "dummypassword01"
}

$NewFiles = @(
    @{
        Path      = "./docker-compose/data/gitlab"
        Name      = "gitlab"
        Extension = "rb"
        Content   = {
            @(
                "external_url 'https://$($CompositeSymbols.GitLab).$($GitLabConfig.Domain).$($GitLabConfig.DomainTld)'"
                "gitlab_rails['initial_root_password'] = '$($GitLabConfig.InitialRootPassword)'"
                "letsencrypt['enable'] = $($GitLabConfig.LetsEncryptEnable)"
                "nginx['listen_port'] = $($GitLabConfig.WebPort)"
                "nginx['listen_https'] = false"
                "registry['enable'] = true"
                "registry['listen_port'] = $($GitLabConfig.RegistryPort)"
                "registry['listen_https'] = false"
                "gitlab_rails['gitlab_shell_ssh_port'] = $($GitLabConfig.SshPort)"
                "gitlab_rails['gitlab_ssh_host'] = '$($CompositeSymbols.GitLab).$($GitLabConfig.Domain).$($GitLabConfig.DomainTld)'"
                "gitlab_sshd['enable'] = true"
                "gitlab_sshd['generate_host_keys'] = true"
                "gitlab_sshd['listen_address'] = 'localhost:$($GitLabConfig.SshPort)'"
            ) -Join [System.Environment]::NewLine
        }
    }
)

foreach ($NewFile in $NewFiles) {
    New-File @NewFile @InternalVerboseDebugConfig
}

$DockerComposeConfig = @{
    File     = "./docker-compose/$($CompositeSymbols.Composite).yaml"
    Networks = {
        @{
            "default" = @{
                "driver" = "bridge"
            }
            "traefik" = @{
                "driver" = "bridge"
            }
        }
    }
}

New-DockerCompose @DockerComposeConfig @InternalVerboseDebugConfig

$DockerComposeContextsConfigDefaultContext = {
    @{
        name         = "$DefaultDockerComposeContext"
        project_name = "$DockerComposeProjectName"
        files        = @(
            "$($CompositeSymbols.Composite).yaml"
            "$($CompositeSymbols.Traefik).yaml"
            "$($CompositeSymbols.GitLab).yaml"
            "$($CompositeSymbols.Nexus).yaml"
        )
    }
}

$DockerComposeContextsConfigGitLabContext = {
    @{
        name         = "gitlab"
        project_name = "$DockerComposeProjectName"
        files        = @(
            "$($CompositeSymbols.Composite).yaml"
            "$($CompositeSymbols.Traefik).yaml"
            "$($CompositeSymbols.GitLab).yaml"
        )
    }
}

$DockerComposeContextsConfig = @{
    File    = "$dockerComposePath/$($DosSymbols.DcContexts.DefaultFile)"
    Active  = $DefaultDockerComposeContext
    Context = $DockerComposeContextsConfigDefaultContext, $DockerComposeContextsConfigGitLabContext
}

New-DockerComposeContext @DockerComposeContextsConfig @InternalVerboseDebugConfig

$TraefikTlsConfig = @{
    Domain  = "frenchexdev.lab"
    OutFile = "./docker-compose/data/traefik/tls.yml"
}

New-TraefikTlsConfiguration @TraefikTlsConfig @InternalVerboseDebugConfig
