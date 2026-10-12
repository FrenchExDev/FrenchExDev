function New-GitLabDockerComposeGenerator {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $File = "./docker-compose/docker-compose.gitlab-ce.yaml",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceName = "gitlab",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceImage = "gitlab/gitlab-ce",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceImageVersion = "latest",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceContainerName = "gitlab",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceRestart = "unless-stopped",
        [ValidateNotNullOrWhiteSpace()] [string] $ServiceHostName = "gitlab.`${ACME_NAME}.`${ACME_TLD}",
        [int] $ServiceShmSize,
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

    $GitLabDockerComposeConfig = @{
        File                 = $File
        ServiceImage         = "$ServiceImage"
        ServiceImageVersion  = $ServiceImageVersion
        ServiceShmSize       = $ServiceShmSize
        ServiceHostName      = $ServiceHostName
        ServiceContainerName = $ServiceContainerName
        ServiceRestart       = $ServiceRestart
        ServiceBuild         = $ServiceBuild
        ServiceNetworks      = $ServiceNetworks
        ServicePorts         = $ServicePorts
        ServiceCommand       = $ServiceCommand
        ServiceLabels        = $ServiceLabels
        ServiceVolumes       = $ServiceVolumes
        ServiceEnvironment   = $ServiceEnvironment
        ServiceLogging       = $ServiceLogging
        ServiceLinks         = $ServiceLinks
        ServiceDependsOn     = $ServiceDependsOn
        ServiceExtraHosts    = $ServiceExtraHosts
        ServiceExpose        = $ServiceExpose
        ServiceCapAdd        = $ServiceCapAdd
    }

    New-GitLabDockerCompose @GitLabDockerComposeConfig
}
