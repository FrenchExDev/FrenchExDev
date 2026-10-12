function New-TraefikDockerComposeGenerator {
    [CmdletBinding()]
    param(
        [string] $File = "./docker-compose/docker-compose.traefik.yaml",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceName = "traefik",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceImage = "traefik",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceImageVersion = "latest",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceContainerName = "traefik",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceRestart = "unless-stopped",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceNetwork = "traefik",
        [ValidateNotNullOrWhiteSpace()] [string] $TraefikDockerComposeNetwork,
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceHostName = "traefik.`${ACME_NAME}.`${ACME_TLD}",
        [ValidateNotNullOrWhiteSpace()] [string] $TraefikConfigServiceNetwork = "dc_traefik",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceDashboardUrl = "dashboard.`${ACME_NAME}.`${ACME_TLD}",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceLogLevel = "INFO",
        [ValidateNotNullOrWhiteSpace()][string] $ServiceTimeZone = "Europe/Paris",
        [switch] $NoServiceDashboard,
        [switch] $ServiceInsecure,
        [switch] $NoServiceDocker,
        [switch] $NoServiceExposeDocker,
        [scriptblock] $ServiceProviders,
        [scriptblock] $ServiceEntryPoints,
        [scriptblock] $ServicePorts,
        [scriptblock] $ServiceRouters,
        [scriptblock] $ServiceMiddlewares,
        [scriptblock] $ServiceServices,
        [scriptblock] $ServiceVolumes,
        [scriptblock] $ServiceNetworks,
        [scriptblock] $ServiceLogging
    )

    $InternalRoutersSymbols = @{
        Dashboard = "dashboard"
        DashboardInsecure = "dashboad-insecure"
    }

    $InternalMiddlewaresSymbols = @{
        Dashboard = "dashboard-https"
        RedirectHttps = "redirect-to-https"
    }

    $TraefikDockerComposeConfig = @{
        File                 = $File
        ServiceName          = $ServiceName
        ServiceImage         = $ServiceImage
        ServiceImageVersion  = $ServiceImageVersion
        ServiceContainerName = $ServiceContainerName
        ServiceHostName      = $ServiceHostName
        ServiceRestart       = $ServiceRestart
        ServiceLogging       = $ServiceLogging
        ServiceCommand       = {
            $InternalCommand = @(
                "--$($TraefiDockerComposeCommandsSymbols.LogLevel)=$ServiceLogLevel",
                "--$($TraefiDockerComposeCommandsSymbols.Api.Dashboard)=$(Convert-BoolToJson $(-not $NoServiceDashboard))",
                "--$($TraefiDockerComposeCommandsSymbols.Api.Insecure)=$(Convert-BoolToJson $ServiceInsecure)",
                "--$($TraefiDockerComposeCommandsSymbols.Provider.Docker._)=$(Convert-BoolToJson $(-not $NoServiceDocker))",
                "--$($TraefiDockerComposeCommandsSymbols.Providers.Docker.ExposedByDefault)=$(Convert-BoolToJson $(-not $NoServiceExposeDocker))",
                "--$($TraefiDockerComposeCommandsSymbols.Ping)"
            )

            if ($null -ne $ServiceProviders) {
                $InternalCommand += Invoke-Command $ServiceProviders
            }

            if ($null -ne $ServiceEntryPoints) {
                $InternalCommand += Invoke-Command $ServiceEntryPoints
            }

            $InternalCommand
        }.GetNewClosure()
        ServicePorts         = $ServicePorts
        ServiceLabels        = {
            $TraefikDockerComposeServiceLabelConfig = @{
                Enable                      = $true
                TraefikDockerComposeNetwork = "$TraefikDockerComposeNetwork"
                Services                    = {
                    $InternalServices = if ($null -ne $ServiceServices) { Invoke-Command $ServiceServices } else { @() }
                    $InternalTraefik = @(New-TraefikDockerComposeServiceLabelService -Proto http -Name "$ServiceName" -Key "$($TraefikSymbols.Keys.LoadBalancer.Server.Port)" -Value "8080")
                    $InternalTraefik + $InternalServices
                }
                Routers                     = {
                    $InternalRouters = if ($null -ne $ServiceRouters) { Invoke-Command $ServiceRouters } else { @() }
                    if (!$NoServiceDashboard) {
                        $InternalRouters += @(
                            $(New-TraefikDockerComposeServiceLabelRouter -Proto http -Name "$($InternalRoutersSymbols.Dashboard)" -Key "$($TraefikSymbols.Keys.Rule)" -Value "Host(``$ServiceDashboardUrl``)"),
                            $(New-TraefikDockerComposeServiceLabelRouter -Proto http -Name "$($InternalRoutersSymbols.Dashboard)" -Key "$($TraefikSymbols.Keys.Service)" -Value "api@internal"),
                            $(New-TraefikDockerComposeServiceLabelRouter -Proto http -Name "$($InternalRoutersSymbols.Dashboard)" -Key "$($TraefikSymbols.Keys.Entrypoints)" -Value "http"),
                            $(New-TraefikDockerComposeServiceLabelRouter -Proto http -Name "$($InternalRoutersSymbols.DashboardInsecure)" -Key "$($TraefikSymbols.Keys.Entrypoints)" -Value "https"),
                            $(New-TraefikDockerComposeServiceLabelRouter -Proto http -Name "$($InternalRoutersSymbols.DashboardInsecure)" -Key "$($TraefikSymbols.Keys.Rule)" -Value "Host(``$ServiceDashboardUrl``)"),
                            $(New-TraefikDockerComposeServiceLabelRouter -Proto http -Name "$($InternalRoutersSymbols.DashboardInsecure)" -Key "tls.domains[0].main" -Value "$ServiceDashboardUrl"),
                            $(New-TraefikDockerComposeServiceLabelRouter -Proto http -Name "$($InternalRoutersSymbols.DashboardInsecure)" -Key "tls.domains[0].sans" -Value '*.${ACME_NAME}.${ACME_TLD}'),
                            $(New-TraefikDockerComposeServiceLabelRouter -Proto http -Name "$($InternalRoutersSymbols.DashboardInsecure)" -Key "$($TraefikSymbols.Keys.Service)" -Value "api@internal")
                        )
                    }
                    $InternalRouters
                }
                Middlewares                 = {
                    $InternalMiddlewares = if ($null -ne $ServiceMiddlewares) { Invoke-Command $ServiceMiddlewares } else { @() }
                    if (!$NoServiceDashboard) {
                        $InternalMiddlewares += @(
                            $(New-TraefikDockerComposeServiceLabelMiddleware -Proto http -Name "$($InternalMiddlewaresSymbols.Dashboard)" -Key "$($TraefikSymbols.Keys.Redirect.Scheme)" -Value "https"),
                            $(New-TraefikDockerComposeServiceLabelMiddleware -Proto http -Name "$($InternalMiddlewaresSymbols.RedirectHttps)" -Key "$($TraefikSymbols.Keys.Redirect.Scheme)" -Value "https"),
                            $(New-TraefikDockerComposeServiceLabelMiddleware -Proto http -Name "$($InternalMiddlewaresSymbols.RedirectHttps)" -Key "$($TraefikSymbols.Keys.Redirect.Port)" -Value "443"),
                            $(New-TraefikDockerComposeServiceLabelMiddleware -Proto http -Name "$($InternalMiddlewaresSymbols.RedirectHttps)" -Key "$($TraefikSymbols.Keys.Redirect.Permanent)" -Value "true")
                        )
                    }
                    $InternalMiddlewares
                }
            }
            New-TraefikDockerComposeServiceLabel @TraefikDockerComposeServiceLabelConfig
        }.GetNewClosure()
        ServiceEnvironment   = {
            @{
                TZ = $ServiceTimeZone
            }
        }.GetNewClosure()
        ServiceVolumes       = {
            $InternalVolumes = @(
                "/etc/localtime:/etc/localtime:ro",
                "/var/run/docker.sock:/var/run/docker.sock"
            )

            if ($null -ne $ServiceVolumes) {
                $InternalVolumes += Invoke-Command $ServiceVolumes   
            }

            $InternalVolumes
        }.GetNewClosure()
        ServiceNetworks      = {
            $InternalNetworks = @(, , "$ServiceNetwork")

            if ($null -ne $ServiceNetworks) {
                $InternalNetworks += Invoke-Command $ServiceNetworks
            }

            $InternalNetworks
        }.GetNewClosure()
    }

    New-TraefikDockerCompose @TraefikDockerComposeConfig
}
