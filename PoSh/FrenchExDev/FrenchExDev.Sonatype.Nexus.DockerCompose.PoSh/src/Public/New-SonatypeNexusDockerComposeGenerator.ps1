function New-SonatypeNexusDockerComposeGenerator {
    [CmdletBinding()]
    param(
        [string] $File = "./docker-compose/docker-compose.nexus.yaml",
        [string] $ServiceName = "nexus",
        [string] $ServiceContainerName = "nexus",
        [string] $ServiceImage = "sonatype/nexus3",
        [string] $ServiceImageVersion = "latest",
        [string] $ServiceRestart = "unless-stopped",
        [string] $ServiceNetwork = "traefik",
        [string] $ServiceHostName = "nexus.`${ACME_NAME}.`${ACME_TLD}",
        [scriptblock] $ServiceLabels,
        [scriptblock] $ServiceLogging,
        [scriptblock] $ServiceNetworks
    )

    $SonatypeNexusDockerComposeGeneratorConfig = @{
        File                 = $File
        ServiceName          = $ServiceName
        ServiceContainerName = $ServiceContainerName
        ServiceImage         = $ServiceImage
        ServiceImageVersion  = $ServiceImageVersion
        ServiceRestart       = $ServiceRestart
        ServiceNetwork       = $ServiceNetwork
        ServiceHostName      = $ServiceHostName
        ServiceLabels        = $ServiceLabels
        ServiceLogging       = $ServiceLogging
        ServiceNetworks      = $ServiceNetworks
    }

    New-SonatypeNexusDockerCompose @SonatypeNexusDockerComposeGeneratorConfig
}
